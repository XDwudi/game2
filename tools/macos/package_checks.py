"""Platform-independent, fail-closed ZIP/Mach-O checks; no native tools required."""
import posixpath
from pathlib import Path
import stat
import struct
import zipfile

CPU_NAMES = {0x01000007: "x86_64", 0x0100000C: "arm64", 7: "i386", 12: "arm"}
THIN = {b"\xcf\xfa\xed\xfe": ("<", 64), b"\xfe\xed\xfa\xcf": (">", 64),
        b"\xce\xfa\xed\xfe": ("<", 32), b"\xfe\xed\xfa\xce": (">", 32)}
FAT = {b"\xca\xfe\xba\xbe": (">", 32), b"\xbe\xba\xfe\xca": ("<", 32),
       b"\xca\xfe\xba\xbf": (">", 64), b"\xbf\xba\xfe\xca": ("<", 64)}


def macho_info(path):
    """Return None for ordinary files, reject malformed native binaries."""
    path = Path(path)
    size = path.stat().st_size
    with path.open("rb") as stream:
        magic = stream.read(4)
        if magic in THIN:
            endian, bits = THIN[magic]
            rest = stream.read(12)
            if len(rest) != 12:
                raise ValueError(f"Truncated Mach-O: {path}")
            cpu, subtype, filetype = struct.unpack(endian + "III", rest)
            return {"architectures": [CPU_NAMES.get(cpu, hex(cpu))], "slices": [{"cpu": cpu, "subtype": subtype, "bits": bits, "filetype": filetype}]}
        if magic not in FAT:
            return None
        endian, bits = FAT[magic]
        count_data = stream.read(4)
        if len(count_data) != 4:
            raise ValueError(f"Truncated universal header: {path}")
        count, = struct.unpack(endian + "I", count_data)
        if count < 1 or count > 32:
            raise ValueError(f"Invalid universal architecture count: {path}")
        record_size = 20 if bits == 32 else 32
        table = stream.read(count * record_size)
        if len(table) != count * record_size:
            raise ValueError(f"Truncated universal architecture table: {path}")
        slices, ranges = [], []
        for i in range(count):
            row = table[i * record_size:(i + 1) * record_size]
            data = struct.unpack(endian + ("IIIII" if bits == 32 else "IIQQII"), row)
            cpu, subtype, offset, length, align = data[:5]
            if offset < 8 + count * record_size or length < 28 or offset + length > size or align > 63 or offset % (1 << align):
                raise ValueError(f"Invalid universal slice bounds/alignment: {path}")
            if any(offset < high and low < offset + length for low, high in ranges):
                raise ValueError(f"Overlapping universal slices: {path}")
            ranges.append((offset, offset + length))
            stream.seek(offset)
            nested_magic = stream.read(4)
            if nested_magic not in THIN:
                raise ValueError(f"Universal slice is not Mach-O: {path}")
            nested_endian, nested_bits = THIN[nested_magic]
            nested_cpu, nested_subtype, filetype = struct.unpack(nested_endian + "III", stream.read(12))
            if nested_cpu != cpu or nested_subtype != subtype:
                raise ValueError(f"Universal slice CPU disagrees with table: {path}")
            slices.append({"cpu": cpu, "subtype": subtype, "bits": nested_bits, "filetype": filetype})
        arches = [CPU_NAMES.get(row["cpu"], hex(row["cpu"])) for row in slices]
        if len(set(arches)) != len(arches):
            raise ValueError(f"Duplicate universal architecture: {path}")
        return {"architectures": arches, "slices": slices}


def safe_archive_inventory(path):
    """Preflight before system ditto extraction into a new directory."""
    entries, links, names = [], {}, set()
    with zipfile.ZipFile(path) as archive:
        for info in archive.infolist():
            # Python normalizes the host separator in filename on Windows; inspect
            # orig_filename so preflight rejects the same bytes on every host.
            name = info.orig_filename.rstrip("/")
            parts = name.split("/")
            if not name or "\0" in name or name.startswith("/") or "\\" in name or any(part in ("..", ".") for part in parts) or ":" in parts[0]:
                raise ValueError(f"Unsafe archive member: {info.filename!r}")
            if any(not part for part in parts):
                raise ValueError(f"Noncanonical archive member: {info.filename!r}")
            folded = name.casefold()
            if folded in names:
                raise ValueError(f"Duplicate/case-colliding archive member: {name}")
            names.add(folded)
            mode = info.external_attr >> 16
            entry = {"path": name, "byteSize": info.file_size, "zipMode": oct(mode), "kind": "directory" if info.is_dir() else "file"}
            if stat.S_ISLNK(mode):
                target = archive.read(info).decode("utf-8")
                if not target or target.startswith("/") or "\\" in target:
                    raise ValueError(f"Unsafe archive symlink: {name}")
                dest = posixpath.normpath(posixpath.join(posixpath.dirname(name), target))
                if dest == ".." or dest.startswith("../"):
                    raise ValueError(f"Archive symlink escapes extraction: {name}")
                links[name] = dest
                entry.update(kind="symlink", target=target)
            entries.append(entry)
        # Resolve every member through every link ancestor before letting ditto write it.
        for entry in entries:
            current = entry["path"]
            visited = set()
            for _ in range(len(links) + 1):
                if current in visited:
                    raise ValueError(f"Archive symlink cycle: {entry['path']}")
                visited.add(current)
                pieces = current.split("/")
                hit = next((i for i in range(1, len(pieces) + 1) if "/".join(pieces[:i]) in links), None)
                if hit is None:
                    break
                current = posixpath.normpath(posixpath.join(links["/".join(pieces[:hit])], *pieces[hit:]))
                if current == ".." or current.startswith("../") or current.startswith("/"):
                    raise ValueError(f"Archive member traverses external link: {entry['path']}")
            else:
                raise ValueError(f"Unresolved archive symlink: {entry['path']}")
    if not entries:
        raise ValueError("Empty archive")
    return entries
