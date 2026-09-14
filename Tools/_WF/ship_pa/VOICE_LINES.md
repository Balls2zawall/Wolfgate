# Ship PA voice lines

The PA ships without voice lines: speakers play a tone and show the announcement text in a bubble
plus a chat line. Everything below is optional tooling for adding spoken lines later.

The shipped lines are synthesised with eSpeak NG, a formant synthesiser in the Dr. Sbaitso / DECtalk
family, so the robotic delivery is the voice itself rather than an effect. Regenerate every line from
the Fluent text (keeps voice and chat identical) with:

```bash
winget install --id eSpeak-NG.eSpeak-NG
python Tools/_WF/ship_pa/gen_voice_lines.py                 # default "sbaitso" flavour into Resources/Audio/_WF/ShipPa
python Tools/_WF/ship_pa/gen_voice_lines.py --voice dectalk  # Klatt variant, closer to Hawking
python Tools/_WF/ship_pa/gen_voice_lines.py --out samples --reel   # A/B copies plus a demo reel
```

Edit a line in `Resources/Locale/en-US/_WF/ship-pa.ftl`, rerun the script, done. The `VOICES` table in
the script holds the eSpeak flags (speed, pitch, word gap) if the delivery needs tuning.

## Using a different generator

Every line below can be swapped for a recorded or
generated voice file without touching C#: drop the `.ogg` in `Resources/Audio/_WF/ShipPa/`, add an
`attributions.yml` entry, and point the prototype field at it.

## Lines to produce

| Line | Suggested text | Where it's wired |
|---|---|---|
| Code green | "All hands, condition green. Resume normal operations." | `sound:` on `ShipCodeGreen` in `Resources/Prototypes/_WF/ShipPa/alert_codes.yml` |
| Code yellow | "All hands, condition yellow. Secure loose equipment and stand by at your stations." | `ShipCodeYellow` |
| Code red | "All hands, condition red. Secure all compartments. This is not a drill." | `ShipCodeRed` |
| Code black | "All hands, abandon ship. Proceed to the nearest escape pod or docking port. This is not a drill." | `ShipCodeBlack` |
| General quarters | "General quarters, general quarters. All hands man your battle stations." | `GeneralQuartersSound` in `Content.Shared/_WF/ShipPa/ShipAlertComponent.cs` |
| Secure from GQ | "Secure from general quarters. All hands stand down." | `GeneralQuartersSecureSound` (same file) |
| GQ klaxon loop | 2–4 s seamless klaxon | `GeneralQuartersAlarm` (same file); must loop cleanly |
| Attention chime | two-tone bong before free-text announcements | `AnnouncementChime` (same file) |
| Planet cracker | "Crack sequence initiated. Clear the excavation zone." etc. | passed by the cracker code to `ShipPaSystem.Announce` |

Keep lines under six seconds; the text also appears in chat, so the voice can be terse.

## Generators

Pick by licence first: the repo's audio is CC-BY-SA-3.0 / CC0, and this fork flags non-commercial
assets, so avoid NC-licensed models and free tiers that forbid redistribution.

| Tool | Cost | Licence for the output | Notes |
|---|---|---|---|
| Kokoro-82M (open weights, Apache-2.0) | free, runs on CPU | yours; mark CC0 | Best free quality; `pip install kokoro` or the web demos. Voices `af_heart`, `bm_george` suit a PA. |
| Piper TTS (MIT) | free, local | yours; mark CC0 | Slightly synthetic, which actually suits an intercom. Voices `en_US-ryan-high`, `en_GB-alan-medium`. |
| OpenAI TTS (`gpt-4o-mini-tts`) | pennies per line | commercial use allowed under OpenAI terms | Steerable: prompt "flat, clipped naval public-address voice". |
| ElevenLabs | paid tiers | commercial licence on paid plans only | Highest realism. The free tier requires attribution and is non-commercial. |
| Azure AI Speech / Google Cloud TTS / Amazon Polly | pay-as-you-go, free quotas | commercial use allowed | Neural voices are fine; pick a low, even male or female voice. |

Avoid: Coqui XTTS (CPML, non-commercial), F5-TTS and most "voice clone" checkpoints trained on
Emilia (CC-BY-NC), and anything cloned from a real person.

Prompt/style: calm, flat delivery, no rising intonation, slight pause after "All hands". Generate at
the tool's highest sample rate; the filter below throws away everything outside the intercom band.

## Post-processing

`Tools/_WF/ship_pa/pa_filter.py` runs ffmpeg with a 300–3400 Hz band-pass, a compressor, a short
hard-walled echo and a limiter, then writes mono 44.1 kHz Vorbis:

```bash
python Tools/_WF/ship_pa/pa_filter.py raw/code_red.wav Resources/Audio/_WF/ShipPa/code_red.ogg
python Tools/_WF/ship_pa/pa_filter.py raw/gq.wav Resources/Audio/_WF/ShipPa/general_quarters.ogg --chime Resources/Audio/Announcements/attention.ogg
```

Damage distortion is applied live by the game (pitch, dropouts, muffling), so do not bake a
"broken" variant.

## Attribution entry

```yaml
- files: ["code_red.ogg", "general_quarters.ogg"]
  license: "CC0-1.0"
  copyright: "Generated with Kokoro-82M (voice bm_george) for Wolfgate, processed with Tools/_WF/ship_pa/pa_filter.py"
  source: "https://github.com/VanguardControl/Wolfgate"
```

Use the licence the generator actually grants; if a service only allows use under its own terms,
say so in `copyright` and keep the file out of CC-BY-SA claims.
