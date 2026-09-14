# Planet Cracking — image-model prompts for the sprite set

How to use: paste **Block A** (style) once as the standing instruction, then send one **Block B** prompt per sheet.
Ask for the base sheet first, then ask for its `-unshaded` sheet as an *edit of that same image* so the glow layer
lines up pixel for pixel. Sizes below are the final sizes; if the model cannot hit them exactly, ask for an exact
integer multiple (4×) with hard pixel edges and downscale with nearest-neighbour. Save as PNG with transparency,
drop the file into the matching `.rsi` folder from `SPRITE_LIST.md` under the state's name, and leave `meta.json`
alone except where that list says otherwise.

---

## Block A — standing style instruction

```
You are producing pixel-art sprite sheets for Space Station 14, a 2D game viewed from slightly above (a shallow
three-quarter top-down view: the top surface of an object is fully visible and a thin slice of its south-facing
front shows underneath). Every image is a sprite sheet on a fully transparent background at the exact pixel size
I state. One tile is 32×32 px. Hard pixel edges only: no anti-aliasing, no blur, no soft gradients, no glow halos
outside the object, no drop shadow on the ground, no background, no border, no text, no watermark. Shade with a
small palette (12–24 colours per sprite) and dithering. Light comes from the top-left. Machines get a 1 px dark
outline; decals and effects get no outline.

Sheet layout rules, always the same: animation frames are laid out LEFT TO RIGHT in equal cells with no gutter;
directional sprites are laid out TOP TO BOTTOM in the order SOUTH (facing the viewer), NORTH (facing away),
EAST, WEST, again in equal cells with no gutter. Every cell of an animation must keep the object in exactly the
same place so it does not wobble. Looping animations must loop seamlessly (last frame flows into the first).
Nothing may cross a cell boundary.

Art direction — cassette futurism with hyper-modern cyberpunk bolted on top. Two eras share the set:
OLD-ERA heavy industry (anchors, centrifuge, miners, crates): painted steel, worn edges, mismatched repair plates,
riveted seams, physical levers and toggle switches, recessed amber readouts, hazard stripes with faded paint.
Colours: charcoal, dusty olive, faded orange, warm ivory, rust.
NEW-ERA tech (projectors, beams, consoles, surveyor, UI icons): sleek dark housings, glass, holographic screens,
sharp neon accents in cyan (#3ee0ff) with a little magenta (#ff3fd0).
Where they meet, show the adapter: modern coils on a riveted plate, a taped cable run, a bracket that does not
quite fit. Natural features (fissures, hole rim, veins) are dark rock with a faint inner glow.

When I ask for an "-unshaded" sheet: take the exact base sheet I just approved and return an image of identical
size that contains ONLY the light-emitting pixels (lamps, coils, screens, glow, sparks) in full saturation; every
other pixel fully transparent. Same cell layout, same frame count, same positions.
```

---

## Block B — one prompt per sheet

### Gravity anchor (`Structures/gravity_anchor.rsi`, 3×3 tiles)

**deployed** — 96×96, single image.
```
A 96×96 sprite of a squat old-era planetary drilling rig, 3 tiles wide, seen from slightly above. A heavy
cylindrical body with a huge central drill bit pointing down into the ground, four hydraulic outrigger legs splayed
to the corners, one oversized physical lever on the south face, riveted olive and charcoal steel with faded orange
hazard stripes and a rust-streaked repair plate. Small amber readout dark. It must look far too heavy to carry.
Standing on bare ground (no ground drawn). Transparent background.
```
**off** — 96×96: `Same rig, but folded for transport: legs retracted against the body, drill bit raised, lever up, all lights dark, a lift strap still hanging off one side.`
**drilling** — 768×96, 8 frames: `Same deployed rig running: the drill bit rotates (stripes on the bit shift each frame), the legs vibrate 1 px, dust puffs at the base, the amber readouts lit, one warning beacon on top blinking. Seamless loop.`
**locked** — 384×96, 4 frames: `Same rig drilled fully in: the bit is buried, legs clamped, a ring of cyan indicator lamps around the collar breathing slowly bright–dim–bright. Seamless loop.`
**broken** — 96×96: `Same rig wrecked: body dented and blackened, two legs snapped, drill bit bent, one panel hanging open, no lights.`
**damaged** — 384×96, 4 frames, overlay: `Only sparks and short electric arcs jumping off the rig's collar and one leg joint, plus a wisp of smoke, over a transparent background; nothing else drawn. This is layered over the rig.`
**drilling-unshaded** — 768×96: edit of `drilling`, emissive only. **locked-unshaded** — 384×96: edit of `locked`, emissive only.

### Anchor crate (`Structures/anchor_crate.rsi`, 2×2 tiles)

**closed** — 64×64: `A reinforced 2×2 old-era shipping crate for a machine: heavy steel frame, olive panels, corner lift points, a stencilled crate number, hazard stripe on the lid, rust at the seams, two latches.`
**open** — 64×64: `The same crate with the lid swung open and the sides folded down, packing foam and straps visible inside, empty.`
**replacement** — 64×64: `The same closed crate with a bright orange stencil "SPARE" band across the lid and a tag hanging from one latch.`

### Gravity projector (`Structures/gravity_projector.rsi`, 2×2 tiles, 4 directions)

Every sheet is 64 px wide; rows are south, north, east, west. The emitter faces the row's direction.

**off** — 64×256 (4 rows of 64×64): `A 2×2 new-era gravity projector on a riveted old hull plate: a low dark housing with a glass emitter dish on the front, a coiled cyan induction ring around the dish (dark, unlit), a thick cable run taped to the plate, small hologram bracket on top. Unpowered, all dark.`
**idle** — 64×256: `Same projector powered: the induction ring has a faint steady cyan line, the hologram bracket shows a tiny standby glyph.`
**charging** — 384×256 (6 frames × 4 rows): `Same projector spooling up: the induction ring lights segment by segment around the dish over the 6 frames, magenta arcs flicker inside the glass. Loops.`
**firing** — 384×256: `Same projector firing: the ring fully lit, the dish glowing white-cyan at the centre, a short bright cone leaving the dish face (no more than 8 px past the housing), heat shimmer pixels around the emitter. Loops.`
**broken** — 64×256: `Same projector wrecked: dish shattered, ring bent, housing scorched, cable torn loose, no light.`
**emitter-unshaded** — 384×256: edit of `firing`, emissive only.

### Centrifuge (`Structures/centrifuge.rsi`, 3×3 tiles)

**off** — 96×96: `A 3×3 old-era gravitic centrifuge: a huge ring rotor lying flat inside a caged steel housing, thick spokes, a central hub with a bolted cap, hazard-striped guard rails around the outside, a bank of physical breakers and one amber gauge on the south face, heavy conduit leaving the base. Stopped, dark.`
**spinning** — 768×96, 8 frames: `Same centrifuge running: the rotor turns exactly one spoke-spacing across the 8 frames so the loop is seamless, motion streaks on the rim, gauge lit, the hub glows dull orange.`
**broken** — 96×96: `Same centrifuge wrecked: rotor cracked and stopped at an angle, cage bent open, scorch marks, gauge shattered.`
**glow-unshaded** — 768×96: edit of `spinning`, emissive only (hub, gauge, rim heat).

### Crack miner (`Structures/crack_miner.rsi`, 2×2 tiles)

**idle** — 64×64: `A 2×2 old-era portable mining drill: a drill head on a squat A-frame, a visible slot on the side holding a large power cell, a hopper for ore on the back, olive and charcoal steel with faded orange stripes, hand-painted stencil. Idle.`
**mining** — 384×64, 6 frames: `Same miner running: drill head bobbing and spinning, rock chips flying, cell slot indicator lit cyan. Loops.`
**exhausted** — 64×64: `Same miner stopped with the drill head raised, an amber "EMPTY" lamp lit, a small pile of tailings beside it.`
**broken** — 64×64: `Same miner wrecked: frame bent, drill head snapped off, cell slot smoking.`
**mining-unshaded** — 384×64: edit of `mining`, emissive only.

### Crack control console (`Structures/crack_console.rsi`, screen only, 32×32)

These are only the monitor face: draw the screen image inside the area a standard Space Station 14 computer's screen
occupies (roughly the top two-thirds of the tile, centred), everything else transparent. Dark screen, cyan UI.

**idle** — 32×32: `A tiny holographic console screen showing a planet disc outline with two small anchor markers and a dim standby bar.`
**targeting** — 32×32: `The same screen with a bright cyan circle drawn between the two anchor markers and a pulsing crosshair.`
**cracking** — 64×32, 2 frames: `The same screen with the circle filling magenta and a rising progress bar; frame two slightly brighter.`
**alert** — 64×32, 2 frames: `The same screen flashing: frame one a red warning triangle over the diagram, frame two the diagram alone.`

### Survey console (`Structures/survey_console.rsi`, screen only, 32×32)

**idle** — 32×32: `A tiny holographic console screen showing a star map: five dots on faint orbit rings, one selected.`
**scanning** — 128×32, 4 frames: `The same star map with a sweep line rotating a quarter turn per frame and dots lighting as it passes. Loops.`

### Handheld surveyor (`Objects/surveyor.rsi`)

**icon** — 32×32: `A new-era handheld survey scanner as an item icon: a dark slab with a folding antenna, a small cyan holo screen and one physical thumb dial. Sized to fit inside 26×26 px.`
**inhand-left** / **inhand-right** — 32×128 each (4 rows south, north, east, west): `The same scanner as held in a humanoid's left (right) hand, standard Space Station 14 in-hand sprite placement, antenna unfolded.`
**scanning** — 128×32, 4 frames: `The item icon with the antenna extended and a cyan pulse ring leaving the tip, growing each frame. Loops.`

### Effects (`Effects/…`)

**crack_beam / beam** — 128×32, 4 frames: `A 32×32 vertical beam segment: a bright cyan core 6 px wide with a magenta fringe, energy motes drifting upward one frame at a time. The top edge must continue perfectly from the bottom edge so the tile stacks vertically without a seam; the loop must also be seamless. Transparent outside the beam.`
**sky_beam / skybeam** — 128×160, 4 frames of 32×160: `A vertical column of cyan light 32 px wide and 160 px tall standing on the ground: solid and bright at the base, tapering and fading to transparent at the top, faint rising motes shifting between frames. Loops.`
**survey_pulse / pulse** — 576×96, 6 frames of 96×96: `A ground-level cyan ring expanding from a point at the centre to the full 96 px width over 6 frames, thinning and fading as it grows. One-shot, last frame nearly transparent.`
**chunk_burst / burst** — 768×96, 8 frames of 96×96: `A burst of dust, rock chips and a flash: frame one a bright white-orange flash at the centre, then an expanding cloud of brown-grey dust and tumbling rock fragments that thins out and fades by frame 8. One-shot.`
**mob_emerge / emerge** — 192×32, 6 frames of 32×32: `Dirt, pebbles and cracked rock plates being thrown outward and falling away from a 32×32 tile centre, as if something climbed up through the ground; ends with a few pebbles settling. One-shot.`

### Decals (`Decals/…`, single direction, 32×32 each)

The game rotates these itself. Base orientation: the ring runs LEFT TO RIGHT across the tile and the hole (or the
anchor) is on the TOP side of the tile; solid ground is on the bottom side.

**crack_rim / rim-straight** — 32×32: `A straight lip of freshly torn rock running horizontally across the tile: the top edge of the tile is the void of the hole with a thin magenta-orange glow line, then a jagged dark rock lip, then loose rubble on the bottom half fading to transparent.`
**crack_rim / rim-curve** — 32×32: `The same lip bowing gently downward (away from the hole) so the ends sit higher than the middle: a curve piece for a large circle.`
**fissure / fissure-1 … fissure-4** — 32×32 each, four separate images: `A jagged ground fissure running roughly left to right with side splits reaching toward the top edge. Stage 1: a hairline crack. Stage 2: wider, small chips. Stage 3: a gaping split with dark depth. Stage 4: the same split with a magma-orange glow inside and smoke pixels.`
**deep_vein / vein** — 32×32: `A faint mineral shimmer under the ground: a cluster of small pale grey crystal flecks and a soft diagonal streak, low contrast, mostly transparent, drawn in neutral grey so it can be tinted.`
**deep_vein / vein-rich** — 32×32: `The same shimmer denser and brighter with larger flecks, still neutral grey.`
**fissure / burst** — 192×32, 6 frames: `Rock plates cracking outward from a tile centre with a puff of dust and an orange flash in frame one, settling by frame 6. One-shot.`

### Interface icons (`Interface/icons.rsi`, 16×16 each, six images)

```
A flat, single-colour 16×16 pixel icon in pure white on transparency, 1 px strokes, readable at size, no outline:
projector — a dish emitting three short lines.
beam — a vertical bolt with two short motion lines.
warning — a triangle with an exclamation mark.
chunk — a rough disc with a cracked edge.
anchor — a drill bit over a horizontal line.
centrifuge — a ring with three spokes.
```

---

## After generation

1. Check every sheet is the exact size and the cells line up; crop or pad with transparency, never rescale
   non-integer amounts.
2. Compare each `-unshaded` sheet against its base sheet at 800% zoom and make sure the glow pixels sit exactly on
   the lamps.
3. Loop test the animations by scrubbing frame 8 → frame 1.
4. Drop the PNGs into the `.rsi` folders. For the three decal RSIs set `directions` to 1 on each ground state in
   `meta.json` (the placeholders shipped four directions the decal renderer never uses).
