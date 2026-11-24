"""Verify the resource envelopes and embedded CodeDirectories of the Unity 6.6 Universal player."""
import hashlib
import json
import plistlib
import re
import shutil
import struct
import subprocess
from pathlib import Path


SEAL = "_CodeSignature/CodeResources"


def sign_bundle(app, binary2text, executable_source):
    """Run the same Unity 6000.6 MacStandalonePlayerBuildProgram Bee action as the original build."""
    editor_data = Path(shutil.which(str(binary2text)) or binary2text).resolve(strict=True).parent.parent
    support = editor_data / "PlaybackEngines/MacStandaloneSupport"
    host = editor_data / "DotNetSdk" / ("dotnet.exe" if (editor_data / "DotNetSdk/dotnet.exe").is_file() else "dotnet")
    program = support / "MacStandalonePlayerBuildProgram.exe"
    signer = support / "MacOSCodeSigning.dll"
    for tool in (host, program, signer):
        if not tool.is_file():
            raise ValueError(f"Unity Mac build support is required to sign the staged app: {tool}")
    contents = app / "Contents"
    plist = plistlib.loads((contents / "Info.plist").read_bytes())
    main = contents / "MacOS" / plist["CFBundleExecutable"]
    scratch = app.parent
    inputs = sorted(path.as_posix() for path in contents.rglob("*") if path.is_file()
                    and path != main and "_CodeSignature" not in path.relative_to(contents).parts)
    action = {"MacOSCodeSignBundleInPlace+Data": {
        "bundle": app.as_posix(), "executableSource": executable_source.as_posix(), "executableName": main.name,
        "bundleId": plist["CFBundleIdentifier"], "hashCache": (scratch / "sign-hashes.json").as_posix()},
        "Bee.TundraBackend.CSharpActionInvocationInformation": {
            "typeFullName": "MacOSCodeSignBundleInPlace", "methodName": "Run", "assemblyLocation": str(program),
            "targets": [main.as_posix(), (contents / SEAL).as_posix()], "inputs": inputs + [executable_source.as_posix()],
            "targetDirectories": []}}
    request = scratch / "sign.info"
    request.write_text(json.dumps(action, ensure_ascii=False), encoding="utf-8")
    subprocess.run([str(host), str(program), f"{support / 'Bee'};{editor_data / 'Tools/BuildPipeline'}",
                    "invoke_static_method", str(request)], cwd=scratch, check=True)
    return {"name": "MacOSCodeSignBundleInPlace.Run", "programSha256": hashlib.sha256(program.read_bytes()).hexdigest(),
            "signingLibrarySha256": hashlib.sha256(signer.read_bytes()).hexdigest()}


def _read(stream, size):
    data = stream.read(size)
    if len(data) != size:
        raise ValueError("Truncated macOS code signature")
    return data


def _blobs(signature):
    magic, length, count = struct.unpack_from(">3I", signature)
    if magic != 0xFADE0CC0 or length > len(signature) or 12 + count * 8 > length:
        raise ValueError("Invalid embedded signature directory")
    blobs = {}
    for slot, start in struct.iter_unpack(">II", signature[12:12 + count * 8]):
        if slot in blobs or start < 12 + count * 8 or start + 8 > length:
            raise ValueError("Invalid code signature slot")
        _, size = struct.unpack_from(">II", signature, start)
        if size < 8 or start + size > length:
            raise ValueError("Invalid code signature component length")
        blobs[slot] = signature[start:start + size]
    return blobs


def _slice_signature(stream, base, length, external_slots):
    stream.seek(base)
    header = struct.unpack("<8I", _read(stream, 32))
    if header[0] != 0xFEEDFACF or header[5] > length - 32:
        raise ValueError("Invalid 64-bit macOS executable header")
    commands = _read(stream, header[5])
    offset = 0
    locations = []
    for _ in range(header[4]):
        kind, size = struct.unpack_from("<II", commands, offset)
        if size < 8 or offset + size > len(commands):
            raise ValueError("Invalid macOS load command")
        if kind == 0x1D and size == 16:  # LC_CODE_SIGNATURE
            locations.append(struct.unpack_from("<II", commands, offset + 8))
        offset += size
    if offset != len(commands) or len(locations) != 1:
        raise ValueError("Missing or ambiguous embedded code signature")
    start, size = locations[0]
    if start < 32 + len(commands) or start + size != length:
        raise ValueError("Invalid embedded signature range")
    stream.seek(base + start)
    blobs = _blobs(_read(stream, size))
    directory = blobs.get(0, b"")
    if len(directory) < 44:
        raise ValueError("Missing CodeDirectory")
    magic, _, _, flags, hashes, _, special, pages, limit, width, algorithm, _, shift = struct.unpack_from(">9I4B", directory)
    if (magic != 0xFADE0C02 or algorithm != 2 or width != 32 or shift > 20 or limit != start
            or hashes - special * width < 44 or hashes + pages * width > len(directory)):
        raise ValueError("Invalid Unity SHA-256 CodeDirectory")
    if external_slots and not flags & 2:
        raise ValueError("Expected an ad-hoc outer app signature")
    if external_slots and (special < 3 or 2 not in blobs):
        raise ValueError("Missing required Info, requirements or resource special slot")
    for slot in range(1, special + 1):
        payload = external_slots.get(slot, blobs.get(slot))
        expected = bytes(width) if payload is None else hashlib.sha256(payload).digest()
        if directory[hashes - slot * width:hashes - (slot - 1) * width] != expected:
            raise ValueError(f"CodeDirectory special slot {slot} differs")
    page_size = (1 << shift) if shift else limit
    if not page_size or pages != (limit + page_size - 1) // page_size:
        raise ValueError("CodeDirectory page count differs")
    stream.seek(base)
    code_hash = hashlib.sha256()
    for index in range(pages):
        page = _read(stream, min(page_size, limit - index * page_size))
        code_hash.update(page)
        if hashlib.sha256(page).digest() != directory[hashes + index * width:hashes + (index + 1) * width]:
            raise ValueError("CodeDirectory executable page hash differs")
    return header[1], hashlib.sha256(directory).digest()[:20], code_hash.hexdigest()


def code_signatures(path, external_slots):
    """Check actual executable pages, returning the per-CPU cdhash and signed code digest."""
    with path.open("rb") as stream:
        if _read(stream, 8) != struct.pack(">II", 0xCAFEBABE, 2):
            raise ValueError("Expected the Unity Universal x86_64/arm64 binary")
        table = list(struct.iter_unpack(">5I", _read(stream, 40)))
        size = path.stat().st_size
        result = {}
        end = 48
        for cpu, _, base, length, _ in sorted(table, key=lambda row: row[2]):
            if base < end or base + length > size or length < 32:
                raise ValueError("Invalid Universal slice range")
            actual_cpu, cdhash, code_hash = _slice_signature(stream, base, length, external_slots)
            if actual_cpu != cpu or cpu in result:
                raise ValueError("Unexpected Universal signed CPU")
            result[cpu] = (cdhash, code_hash)
            end = base + length
        if set(result) != {0x01000007, 0x0100000C}:
            raise ValueError("Missing signed x86_64 or arm64 slice")
    return result


def verify_bundle(app, executables):
    contents = app / "Contents"
    info = (contents / "Info.plist").read_bytes()
    executable = plistlib.loads(info)["CFBundleExecutable"]
    if not executable or Path(executable).name != executable:
        raise ValueError("Invalid app executable name")
    main = "MacOS/" + executable
    envelope = (contents / SEAL).read_bytes()
    seal = plistlib.loads(envelope)
    inventory = {p.relative_to(contents).as_posix(): p for p in contents.rglob("*") if p.is_file()}
    if main not in executables or set(seal["files2"]) != set(inventory) - {"Info.plist", SEAL, main}:
        raise ValueError("Unsealed or missing macOS bundle resource")
    resources = {name for name in inventory if name.startswith("Resources/")}
    if set(seal["files"]) != resources:
        raise ValueError("Incomplete legacy macOS resource seal")
    for name, entry in seal["files2"].items():
        if name in executables:
            signatures = code_signatures(inventory[name], {})
            cdhashes = {cdhash for cdhash, _ in signatures.values()}
            # Apple's nested-code requirement written by Unity lists each signed architecture.
            requirements = {f'cdhash H"{h.hex()}"' for h in cdhashes}
            if (set(entry) != {"cdhash", "requirement"} or entry["cdhash"] not in cdhashes
                    or set(entry["requirement"].split(" or ")) != requirements):
                raise ValueError("Nested macOS signature differs: " + name)
        else:
            if set(entry) != {"hash2"}:
                raise ValueError("Missing SHA-256 macOS resource seal: " + name)
            sha1, sha256 = hashlib.sha1(), hashlib.sha256()
            with inventory[name].open("rb") as stream:
                while data := stream.read(1024 * 1024):
                    if name in resources:
                        sha1.update(data)
                    sha256.update(data)
            if (entry["hash2"] != sha256.digest() or
                    (name in resources and seal["files"][name] != sha1.digest())):
                raise ValueError("macOS sealed resource digest differs: " + name)
        # Check the winning rule as well as the entry. An omit rule cannot stand in for sealing.
        matches = [rule if isinstance(rule, dict) else {} for pattern, rule in seal["rules2"].items()
                   if re.search(pattern, name)]
        if not matches or max(matches, key=lambda rule: rule.get("weight", 1)).get("omit"):
            raise ValueError("macOS signature rules omit a packaged file: " + name)
    signed = code_signatures(inventory[main], {1: info, 3: envelope})
    return main, signed, seal
