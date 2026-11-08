"""Encode PNG capture frames at their recorded times, then verify decoded pixels.

Requires Pillow, ffmpeg with libx264rgb, and ffprobe. No Unity writes or resampling.
FFconcat syntax: https://ffmpeg.org/ffmpeg-formats.html#concat
"""
from __future__ import annotations

import argparse
import contextlib
import hashlib
import json
from pathlib import Path
import struct
import subprocess

from PIL import Image


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run_json(command: list[str]) -> dict:
    return json.loads(subprocess.check_output(command, text=True, encoding="utf-8"))


def movie_duration(path: Path) -> float:
    """Read MP4 movie duration, including the initial empty edit.

    ffprobe's format.duration instead reports this file's media span, excluding
    the initial empty edit; mvhd stores the actual capture-relative end time.
    """
    data = path.read_bytes()
    def atoms(start: int, end: int):
        while start + 8 <= end:
            size, kind = struct.unpack_from(">I4s", data, start)
            if size < 8 or start + size > end:
                raise ValueError("Unsupported or invalid MP4 atom")
            yield start, size, kind
            start += size
    for offset, length, kind in atoms(0, len(data)):
        if kind == b"moov":
            for child, _, child_kind in atoms(offset + 8, offset + length):
                if child_kind == b"mvhd":
                    version = data[child + 8]
                    cursor = child + (20 if version == 0 else 28)
                    timescale = struct.unpack_from(">I", data, cursor)[0]
                    duration = struct.unpack_from(">I" if version == 0 else ">Q", data, cursor + 4)[0]
                    return duration / timescale
    raise ValueError("No MP4 movie header")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording", type=Path)
    parser.add_argument("--ffmpeg", type=Path, required=True)
    parser.add_argument("--ffprobe", type=Path, required=True)
    parser.add_argument("--verify-existing", action="store_true", help="Read-only recheck; never rewrite video, logs, samples or prior verification")
    alpha = parser.add_mutually_exclusive_group()
    alpha.add_argument("--rgba-mov", action="store_true", help="Preserve every RGBA byte using PNG-in-MOV")
    alpha.add_argument("--rgb-preview", action="store_true", help="Explicit RGB-only MP4 preview; report discarded non-opaque alpha")
    args = parser.parse_args()
    root = args.recording.resolve()
    ffmpeg, ffprobe = str(args.ffmpeg.resolve()), str(args.ffprobe.resolve())
    report_path = root / "recording.json"
    capture = json.loads(report_path.read_text(encoding="utf-8-sig"))
    frames = capture["frames"]
    width, height = capture["width"], capture["height"]
    if not frames:
        raise ValueError("Empty capture")
    times_us = [round(frame["seconds"] * 1_000_000) for frame in frames]
    if times_us[0] < 0 or any(b <= a for a, b in zip(times_us, times_us[1:])):
        raise ValueError("Capture timestamps must be non-negative and strictly increasing")
    duration_us = round(capture["duration"] * 1_000_000)
    if duration_us < times_us[-1]:
        raise ValueError("Capture ends before last frame")
    # A container sample must have positive duration; never invent another frame.
    last_duration_us = max(1, duration_us - times_us[-1])
    durations = [b - a for a, b in zip(times_us, times_us[1:])] + [last_duration_us]
    protected = {str(path): sha(path) for path in [report_path, *root.glob("state-*.json")]}
    pixel_mode = "RGBA" if args.rgba_mov else "RGB"
    pixel_format = "rgba" if args.rgba_mov else "rgb24"
    pixel_channels = 4 if args.rgba_mov else 3
    hash_field = "rgbaSha256" if args.rgba_mov else "rgbSha256"
    decoded_hash_field = "decodedRgbaSha256" if args.rgba_mov else "decodedRgbSha256"
    source_hashes, rows, nonopaque = [], [], []
    concat = ["ffconcat version 1.0"]
    for frame, timestamp, duration in zip(frames, times_us, durations):
        path = (root / frame["file"]).resolve()
        if not path.is_relative_to(root) or not path.is_file():
            raise ValueError(f"Invalid frame path: {path}")
        with Image.open(path) as image:
            if image.size != (width, height):
                raise ValueError(f"Frame size changed: {path}")
            if "A" in image.getbands() and image.getchannel("A").getextrema() != (255, 255):
                if not (args.rgba_mov or args.rgb_preview):
                    raise ValueError("H.264 RGB cannot preserve non-opaque alpha; use --rgba-mov or explicit --rgb-preview")
                channel = image.getchannel("A")
                nonopaque.append({"file": frame["file"], "seconds": frame["seconds"],
                                  "alphaRange": channel.getextrema(), "pixels": sum(channel.histogram()[:255])})
            pixel_hash = hashlib.sha256(image.convert(pixel_mode).tobytes()).hexdigest()
        file_hash = sha(path)
        protected[str(path)] = file_hash
        source_hashes.append(pixel_hash)
        rows.append({"source": frame["file"], "pngSha256": file_hash,
                     "recordedSeconds": frame["seconds"], "encodedPtsUs": timestamp,
                     hash_field: pixel_hash})
        escaped = path.as_posix().replace("'", "'\\''")
        concat += [f"file '{escaped}'", "option framerate 1000000",
                   f"duration {duration / 1_000_000:.6f}"]
    stem = "recording-vfr-rgba" if args.rgba_mov else "recording-vfr-rgb-preview" if args.rgb_preview else "recording-vfr"
    output = root / (stem + ("-lossless.mov" if args.rgba_mov else ".mp4" if args.rgb_preview else "-lossless.mp4"))
    concat_path = root / (stem + ".ffconcat")
    encode_log = root / (stem + "-encode.log")
    verification_path = root / (stem + "-verification.json")
    concat_text = "\n".join(concat) + "\n"
    if verification_path.exists() and not args.verify_existing:
        raise FileExistsError("Verification already exists; preserve it")
    if args.verify_existing:
        if not output.exists() or concat_path.read_text(encoding="utf-8") != concat_text:
            raise ValueError("Existing encoding is absent or concat manifest differs")
    else:
        if any(path.exists() for path in (output, concat_path, encode_log)):
            raise FileExistsError("Encoding outputs already exist; use --verify-existing or a new capture directory")
        concat_path.write_text(concat_text, encoding="utf-8")
    codec = ["-c:v", "png", "-pred", "mixed", "-compression_level", "6"] if args.rgba_mov else ["-c:v", "libx264rgb", "-crf", "0", "-preset", "fast"]
    command = [ffmpeg, "-hide_banner", "-n", "-copyts", "-itsoffset",
               f"{times_us[0] / 1_000_000:.6f}", "-safe", "0", "-f", "concat",
               "-i", str(concat_path), "-map", "0:v:0", "-an", *codec,
               "-pix_fmt", pixel_format, "-fps_mode", "vfr",
               "-enc_time_base", "1:1000000", "-video_track_timescale", "1000000",
               "-movie_timescale", "1000000", "-movflags", "+faststart", str(output)]
    if not args.verify_existing:
        with encode_log.open("w", encoding="utf-8") as log:
            subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True)
    probe_command = [ffprobe, "-v", "error", "-select_streams", "v:0", "-show_frames",
                     "-show_streams", "-show_format", "-of", "json", str(output)]
    probe = run_json(probe_command)
    if not args.verify_existing:
        (root / (stem + "-ffprobe.json")).write_text(
            json.dumps(probe, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    decoded_frames = probe["frames"]
    if len(decoded_frames) != len(frames):
        raise AssertionError(f"Frame count: {len(decoded_frames)} != {len(frames)}")
    pts_error = []
    for original, encoded, row in zip(frames, decoded_frames, rows):
        actual = float(encoded["best_effort_timestamp_time"])
        row["decodedPtsSeconds"] = actual
        pts_error.append(abs(actual - original["seconds"]))
    if max(pts_error) > 0.0000011:
        raise AssertionError(f"PTS error exceeds 1.1 microseconds: {max(pts_error)}")
    decode_command = [ffmpeg, "-v", "error", "-i", str(output), "-map", "0:v:0",
                      "-fps_mode", "passthrough", "-f", "rawvideo", "-pix_fmt", pixel_format, "pipe:1"]
    sample_indexes = {0, len(frames) // 2, len(frames) - 1}
    samples = root / ("decoded-samples-rgba" if args.rgba_mov else "decoded-samples-rgb-preview" if args.rgb_preview else "decoded-samples")
    if not args.verify_existing:
        samples.mkdir(exist_ok=True)
    log_context = contextlib.nullcontext(subprocess.DEVNULL) if args.verify_existing else (root / (stem + "-decode.log")).open("w", encoding="utf-8")
    with log_context as log:
        process = subprocess.Popen(decode_command, stdout=subprocess.PIPE, stderr=log)
        try:
            for index, expected in enumerate(source_hashes):
                pixels = process.stdout.read(width * height * pixel_channels)
                actual = hashlib.sha256(pixels).hexdigest()
                rows[index][decoded_hash_field] = actual
                if len(pixels) != width * height * pixel_channels or actual != expected:
                    raise AssertionError(f"Decoded {pixel_mode} differs from source at frame {index}")
                if index in sample_indexes and not args.verify_existing:
                    Image.frombytes(pixel_mode, (width, height), pixels).save(samples / f"{index:05}.png")
            if process.stdout.read(1):
                raise AssertionError("Unexpected extra decoded frame")
            if process.wait() != 0:
                raise RuntimeError("Decoder failed")
        except BaseException:
            process.kill()
            process.wait()
            raise
    if any(sha(Path(path)) != expected for path, expected in protected.items()):
        raise AssertionError("A source frame or original JSON changed during encoding")
    media_span = float(probe["format"]["duration"])
    encoded_duration = movie_duration(output)
    if abs(encoded_duration - (times_us[-1] + last_duration_us) / 1_000_000) > 0.0000011:
        raise AssertionError(f"Unexpected container duration: {encoded_duration}")
    if abs(media_span - (times_us[-1] - times_us[0] + last_duration_us) / 1_000_000) > 0.0000011:
        raise AssertionError(f"Unexpected media span: {media_span}")
    result = {
        "status": "RGB-only preview; alpha discarded" if args.rgb_preview else "encoded_and_losslessly_decoded; visual review remains separate",
        "sourceReport": str(report_path), "sourceReportSha256": protected[str(report_path)],
        "artVersion": capture.get("artVersion"), "runtimeMvid": capture.get("runtimeMvid"),
        "coreMvid": capture.get("coreMvid"), "recordedDurationSeconds": capture["duration"],
        "recordedFirstFrameSeconds": frames[0]["seconds"],
        "containerDurationSeconds": encoded_duration, "ffprobeMediaSpanSeconds": media_span,
        "frameCount": len(frames),
        "maxPtsErrorSeconds": max(pts_error), "lastSampleDurationUs": last_duration_us,
        "pixelMode": pixel_mode, "pixelMatches": len(rows), "allOriginalFilesUnchanged": True,
        "rgbaPixelMatches" if args.rgba_mov else "rgbPixelMatches": len(rows),
        "sourceNonOpaqueFrames": nonopaque, "alphaPreserved": bool(args.rgba_mov),
        "discardedNonOpaqueAlphaPixels": sum(r["pixels"] for r in nonopaque) if args.rgb_preview else 0,
        "notes": ["Real capture timestamps quantized to 1 microsecond; no inserted or dropped frames.",
                  "First sample retains the capture's initial delay; no unrecorded initial frame is invented.",
                  "Final sample has at least 1 microsecond duration because MP4 samples need positive duration.",
                  "MP4/MOV mvhd duration includes initial empty edit; ffprobe media span excludes that delay.",
                  "All RGBA pixels including alpha are verified byte-identical." if args.rgba_mov else
                  "RGB pixels are byte-identical; alpha is explicitly discarded in this preview." if args.rgb_preview else
                  "Fully opaque source alpha is omitted; all RGB pixels are verified byte-identical.",
                  "PNG-in-MOV or H.264 RGB 4:4:4 may require a compatible player. No 60 fps or performance claim."],
        "output": str(output), "outputSha256": sha(output),
        "ffmpegVersion": subprocess.check_output([ffmpeg, "-version"], text=True).splitlines()[0],
        "ffprobeVersion": subprocess.check_output([ffprobe, "-version"], text=True).splitlines()[0],
        "encodeCommand": command, "probeCommand": probe_command, "decodeCommand": decode_command,
        "frameVerification": rows,
    }
    if not args.verify_existing:
        verification_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: result[key] for key in ("output", "frameCount", "containerDurationSeconds",
                     "maxPtsErrorSeconds", "pixelMode", "pixelMatches", "discardedNonOpaqueAlphaPixels", "allOriginalFilesUnchanged")}, indent=2))


if __name__ == "__main__":
    main()
