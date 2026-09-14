#!/usr/bin/env python3
"""Renders utterances with Kokoro-82M through kokoro-onnx. Run with the venv under KOKORO_HOME.

    python kokoro_render.py --voice af_heart --out line.wav "All hands, condition red."
    python kokoro_render.py --voice af_heart:0.6,af_kore:0.4 --jobs jobs.json

--jobs takes a JSON list of {"text": ..., "out": ...} so the 300 MB model loads once for a batch.
"""

import argparse
import json
import os
import sys

import numpy as np
import soundfile
from kokoro_onnx import Kokoro

HOME = os.environ.get("KOKORO_HOME") or os.path.join(os.environ.get("LOCALAPPDATA", ""), "Wolfgate", "kokoro")


def resolve_voice(kokoro: Kokoro, spec: str):
    """A voice id, or a weighted blend like af_heart:0.6,af_kore:0.4."""
    if "," not in spec and ":" not in spec:
        return spec

    mix = None
    total = 0.0

    for part in spec.split(","):
        name, _, weight = part.partition(":")
        w = float(weight) if weight else 1.0
        style = kokoro.get_voice_style(name.strip()) * w
        mix = style if mix is None else mix + style
        total += w

    return mix / total


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("text", nargs="?", help="what to say (omit with --jobs)")
    parser.add_argument("--out", help="wav to write (omit with --jobs)")
    parser.add_argument("--jobs", help="JSON list of {text, out}")
    parser.add_argument("--voice", default="af_heart")
    parser.add_argument("--speed", type=float, default=1.0)
    parser.add_argument("--lang", default="en-us")
    parser.add_argument("--model", default=os.path.join(HOME, "kokoro-v1.0.onnx"))
    parser.add_argument("--voices", default=os.path.join(HOME, "voices-v1.0.bin"))
    parser.add_argument("--list", action="store_true", help="print the voice ids and exit")
    args = parser.parse_args()

    kokoro = Kokoro(args.model, args.voices)

    if args.list:
        print("\n".join(sorted(kokoro.get_voices())))
        return 0

    if args.jobs:
        with open(args.jobs, encoding="utf-8") as f:
            jobs = json.load(f)
    elif args.text and args.out:
        jobs = [{"text": args.text, "out": args.out}]
    else:
        parser.error("give text and --out, or --jobs")

    voice = resolve_voice(kokoro, args.voice)

    for job in jobs:
        samples, rate = kokoro.create(job["text"], voice=voice, speed=args.speed, lang=args.lang)
        os.makedirs(os.path.dirname(os.path.abspath(job["out"])), exist_ok=True)
        soundfile.write(job["out"], samples, rate)

    return 0


if __name__ == "__main__":
    sys.exit(main())
