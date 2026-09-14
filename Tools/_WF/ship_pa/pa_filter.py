#!/usr/bin/env python3
"""Turns a clean voice recording into a ship PA line: band-limited, compressed, a touch of room, Vorbis mono.

Usage:
    python Tools/_WF/ship_pa/pa_filter.py input.wav Resources/Audio/_WF/ShipPa/code_red.ogg
    python Tools/_WF/ship_pa/pa_filter.py input.mp3 out.ogg --chime Resources/Audio/Announcements/attention.ogg

Needs ffmpeg on PATH. No Python packages required.
"""

import argparse
import os
import shutil
import subprocess
import sys

# 300-3400 Hz is a telephone/intercom band; the compressor keeps the level even so the
# speakers never surprise anyone, and the short echo reads as a hard-walled compartment.
PA_CHAIN = (
    "highpass=f=300,"
    "lowpass=f=3400,"
    "acompressor=threshold=-18dB:ratio=4:attack=5:release=80:makeup=3,"
    "aecho=0.8:0.5:14:0.12,"
    "alimiter=limit=0.9"
)


def filter_file(input_path: str, output_path: str, chime: str | None = None, gap: float = 0.35, quality: int = 4) -> None:
    """Runs one voice line through the PA chain, optionally with a clean chime in front, and writes Vorbis."""
    if shutil.which("ffmpeg") is None:
        raise RuntimeError("ffmpeg not found on PATH")

    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)

    cmd = ["ffmpeg", "-y", "-hide_banner", "-loglevel", "error"]

    if chime:
        # The chime is left clean; only the voice goes through the PA chain.
        cmd += ["-i", chime, "-i", input_path]
        graph = (
            f"[1:a]{PA_CHAIN},aformat=channel_layouts=mono,aresample=44100[v];"
            f"[0:a]aformat=channel_layouts=mono,aresample=44100[c];"
            f"[c]apad=pad_dur={gap}[cp];"
            f"[cp][v]concat=n=2:v=0:a=1[out]"
        )
        cmd += ["-filter_complex", graph, "-map", "[out]"]
    else:
        cmd += ["-i", input_path, "-af", f"{PA_CHAIN},aformat=channel_layouts=mono,aresample=44100"]

    cmd += ["-c:a", "libvorbis", "-q:a", str(quality), output_path]
    subprocess.run(cmd, check=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("input", help="voice line (wav/mp3/ogg/flac)")
    parser.add_argument("output", help="destination .ogg")
    parser.add_argument("--chime", help="sound to prepend, e.g. Resources/Audio/Announcements/attention.ogg")
    parser.add_argument("--gap", type=float, default=0.35, help="seconds of silence between chime and voice")
    parser.add_argument("--quality", type=int, default=4, help="libvorbis -q:a (0-10)")
    args = parser.parse_args()

    try:
        filter_file(args.input, args.output, chime=args.chime, gap=args.gap, quality=args.quality)
    except (RuntimeError, subprocess.CalledProcessError) as e:
        print(str(e), file=sys.stderr)
        return 1

    size = os.path.getsize(args.output)
    print(f"wrote {args.output} ({size / 1024:.0f} KB)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
