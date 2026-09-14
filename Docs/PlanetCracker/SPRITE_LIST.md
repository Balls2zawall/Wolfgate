# Planet Cracking — sprite list (as built)

Every RSI the code and prototypes load, with the exact canvas each state needs. Paths are under
`Resources/Textures/_WF/PlanetCracker/`. Placeholders with these exact names and sizes are already checked in, so a
real sprite is a drop-in replacement of the PNGs; `meta.json` only changes where noted.

Conventions: 32 px per tile. "Canvas" is one frame of one direction. Animated states are horizontal strips
(frames left to right); directional states are vertical rows in the order south, north, east, west. Frame delays are
what the placeholder `meta.json` declares; change them freely, the code reads the RSI.

## Structures

| RSI | Footprint | State | Canvas | Dirs | Frames | Delay | Used for |
|---|---|---|---|---|---|---|---|
| `Structures/gravity_anchor.rsi` | 3×3 | `off` | 96×96 | 1 | 1 | | loose, not deployed |
| | | `deployed` | 96×96 | 1 | 1 | | deployed and paired |
| | | `drilling` | 96×96 | 1 | 8 | 0.10 | drilling loop |
| | | `locked` | 96×96 | 1 | 4 | 0.25 | drilled in, slow pulse |
| | | `broken` | 96×96 | 1 | 1 | | destroyed |
| | | `damaged` | 96×96 | 1 | 4 | 0.10 | sparks overlay while damaged |
| | | `drilling-unshaded` | 96×96 | 1 | 8 | 0.10 | glow layer for drilling |
| | | `locked-unshaded` | 96×96 | 1 | 4 | 0.25 | glow layer for locked |
| `Structures/anchor_crate.rsi` | 2×2 | `closed` | 64×64 | 1 | 1 | | crate |
| | | `open` | 64×64 | 1 | 1 | | crate opened |
| | | `replacement` | 64×64 | 1 | 1 | | replacement-anchor crate |
| `Structures/gravity_projector.rsi` | 2×2 | `off` | 64×64 | **4** | 1 | | unpowered |
| | | `idle` | 64×64 | 4 | 1 | | powered, waiting |
| | | `charging` | 64×64 | 4 | 6 | 0.10 | spool-up |
| | | `firing` | 64×64 | 4 | 6 | 0.10 | beam active |
| | | `broken` | 64×64 | 4 | 1 | | destroyed |
| | | `emitter-unshaded` | 64×64 | 4 | 6 | 0.10 | emitter glow |
| `Structures/centrifuge.rsi` | 3×3 | `off` | 96×96 | 1 | 1 | | stopped |
| | | `spinning` | 96×96 | 1 | 8 | 0.08 | one seamless rotor loop |
| | | `broken` | 96×96 | 1 | 1 | | destroyed |
| | | `glow-unshaded` | 96×96 | 1 | 8 | 0.08 | interior glow while spinning |
| `Structures/crack_miner.rsi` | 2×2 | `idle` | 64×64 | 1 | 1 | | idle, also doubles as off |
| | | `mining` | 64×64 | 1 | 6 | 0.10 | drilling loop |
| | | `exhausted` | 64×64 | 1 | 1 | | vein under it is spent |
| | | `broken` | 64×64 | 1 | 1 | | destroyed |
| | | `mining-unshaded` | 64×64 | 1 | 6 | 0.10 | glow while mining |
| `Structures/crack_console.rsi` | screen only | `idle` | 32×32 | 1 | 1 | | screen art on the shared computer body |
| | | `targeting` | 32×32 | 1 | 1 | | anchors paired, waiting |
| | | `cracking` | 32×32 | 1 | 2 | 0.50 | crack running |
| | | `alert` | 32×32 | 1 | 2 | 0.30 | grace or fault blink |
| `Structures/survey_console.rsi` | screen only | `idle` | 32×32 | 1 | 1 | | screen art |
| | | `scanning` | 32×32 | 1 | 4 | 0.20 | scan running |

## Objects

| RSI | State | Canvas | Dirs | Frames | Delay | Used for |
|---|---|---|---|---|---|---|
| `Objects/surveyor.rsi` | `icon` | 32×32 | 1 | 1 | | item |
| | `inhand-left` | 32×32 | 4 | 1 | | in hand |
| | `inhand-right` | 32×32 | 4 | 1 | | in hand |
| | `scanning` | 32×32 | 1 | 4 | 0.15 | while scanning |

## Effects (entities, drawn unshaded)

| RSI | State | Canvas | Frames | Delay | Used for |
|---|---|---|---|---|---|
| `Effects/crack_beam.rsi` | `beam` | 32×32 | 4 | 0.06 | projector → anchor beam; repeats along the line, must tile vertically |
| `Effects/sky_beam.rsi` | `skybeam` | 32×160 | 4 | 0.10 | stands on each anchor during the crack, fades upward |
| `Effects/survey_pulse.rsi` | `pulse` | 96×96 | 6 | 0.08 | one-shot ring at the surveyor |
| `Effects/chunk_burst.rsi` | `burst` | 96×96 | 8 | 0.07 | one-shot at hole and chunk on extraction |
| `Effects/mob_emerge.rsi` | `emerge` | 32×32 | 6 | 0.08 | one-shot on a creature leaving a fissure (F8) |
| `Decals/fissure.rsi` | `burst` | 32×32 | 6 | 0.08 | one-shot where a fissure spawns (F8); lives in the decal RSI |

## Decals (ground)

Decals render frame 0 and ignore RSI directions; the code rotates each decal itself. Deliver these as **single
direction 32×32** and the `meta.json` `directions` will be set to 1 when the art lands. The placeholders carry four
directions only because they were generated that way.

| RSI | State | Canvas | Used for |
|---|---|---|---|
| `Decals/crack_rim.rsi` | `rim-straight` | 32×32 | lip of the hole, straight run |
| | `rim-curve` | 32×32 | lip of the hole, corner |
| `Decals/fissure.rsi` | `fissure-1` … `fissure-4` | 32×32 each | fissure growth stages around a drilling anchor (F8); stage 4 glows |
| `Decals/deep_vein.rsi` | `vein` | 32×32 | grey, tinted in code per ore |
| | `vein-rich` | 32×32 | rich vein variant |

## Interface

| RSI | States | Canvas | Used for |
|---|---|---|---|
| `Interface/icons.rsi` | `projector`, `beam`, `warning`, `chunk` | 16×16 | console diagram and legend |
| | `anchor`, `centrifuge` | 16×16 | declared, not drawn yet; keep for the legend |

## Not needed

- `Decals/crack_ring.rsi` (`ring-straight-1..3`, `ring-curve-1..3`): nothing loads it. The surface scar uses
  `crack_rim.rsi`; the in-progress circle is drawn by an overlay in code. Skip unless you want stage rings later.
- `Tiles/crack_hole.rsi`: nothing loads it. The hole is open space (empty tiles), not a tile sprite.

## Totals

| Group | Distinct PNGs | Largest canvas |
|---|---|---|
| Structures | 31 | 96×96, with 8-frame strips at 768×96 |
| Objects | 4 | 32×32 |
| Effects | 6 | 96×96 strips, sky beam 32×160 |
| Decals | 8 | 32×32 |
| Interface | 6 | 16×16 |

Maps (cracker hull, transport) are separate and unchanged from `ASSET_REQUIREMENTS.md`.
