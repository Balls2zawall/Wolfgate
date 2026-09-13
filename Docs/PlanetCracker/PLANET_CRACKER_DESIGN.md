# Planet Cracking — design, revision 3

**Status:** proposal organised into features, decisions D1–D25 taken (§7). Nothing implemented. Branch `clanker/planet-cracker-design-a8c500`.

Revision 3 resolves the proofread issues (chunk berth, cut radius, mining numbers, no abort after begin, chunk watchdog, grace hysteresis) and corrects F0: the sector already has planets (Far Horizons star system) and a planet-surface prototype pipeline (DeltaV, used by Monolith's desert world), so F0 is the glue between them and the z-level stack, not planets from scratch.

§0 is what the codebase already gives us (verified, paths inline). §1 glossary, §2 the corrected player loop, §3 the state machine, §4 the features F0–F9, §5 numbers, §6 pointer to the asset requirements, §7 decisions taken, §8 remaining questions, §9 testing.

---

## 0. What already exists

| Capability | Where | Notes |
|---|---|---|
| **Planets in the sector** | `Content.Server/_FarHorizons/StarSystem/StarSystemMapSystem.cs`, `Resources/Prototypes/_FarHorizons/Space/{systems,planets}.yml`, `Entities/Objects/StarSystem/star_system.yml` | The `StarSystemKyphrus` rule (in every Monolith preset) spawns a `PlanetEntity` per planet on the sector map at a distance and angle from the star: Fervidus, Merak, Asclepiu, Aerumna, Thrascias. Each is a warp point, an FTL beacon and an IFF blip, rendered by a client shader overlay (`PlanetOverlay.cs`). **That is the planet you see in space.** They have no surface and nothing happens when you fly into one. |
| **Planet surface prototype** | `Content.Shared/_DV/Planet/PlanetPrototype.cs`, `Content.Server/_DV/Planet/PlanetSystem.cs`, `_DV/Station/.../StationPlanetSpawner*` | `- type: planet` = biome template + atmosphere + map light + marker layers + extra components. `PlanetSystem.SpawnPlanet` builds a flat surface map from it; `LoadPlanet` also drops a hand-made grid on top and reserves its tiles so rocks do not spawn inside. Monolith's `DesertWorld` (`Resources/Prototypes/_Mono/Planets/permanent_planet.yml`, biome `MonoDesertPermanentPlanet`, surface outpost `/Maps/_Mono/POI/surface_outpost_desert.yml`) uses it and is reachable by FTL. It is **commented out** on the live Caelestinus map and only on in the dev map. Monolith also ships ocean and desert biomes, planet ore tables, fauna and weather under `_Mono/Planets`. |
| Planets as stacked z-levels | `Content.Server/_CE/ZLevels`, `Resources/Prototypes/_CE/ZLevels/zmaps.yml` | See F0. Only the round-start station builds a network today. |
| Grids fall without a gravity generator | `CEZLevelsSystem.Gravity.cs` | Every 0.5 s a grid on an air layer with no active gravgen (or whose `FixturesMass` exceeds pooled `maxHandledMass`) enters a transit map and plummets at up to 1.2 levels/s. |
| Grids crash into the ground | `CEZLevelsSystem.Gravity.cs` `CrashGrid` | Touchdown at ≥ 0.35 levels/s explodes every hull tile plus a central blast scaled by tile count. **This is the fall and the chunk explosion for free.** |
| Piloted ascend/descend, spool, landing, dust | `CEZLevelsSystem.PilotControl.cs`, `ShuttleButtons.AscendZ/DescendZ` | Vertical accel = thrust/mass, so a heavy hull is slow vertically without extra work. Landed ships get thrusters disabled. Vertical flight needs a working gravgen. |
| Gravity generator capacity and spin-up | `Content.Server/Gravity/GravityGeneratorComponent.cs` (`maxHandledMass`), `Power/Components/PowerChargeComponent.cs` (`Charge` 0–1, `ChargeRate`, `Intact`, `SwitchedOn`) | Standard gravgen carries 250 mass, mini 50 (`gravity_generator.yml`). Charge ramps up and down with power. **The centrifuge is a gravgen variant.** |
| Crushing whatever is under a landing grid | `ShuttleSystem.FasterThanLight.cs` `Smimsh` | |
| Immobilising a grid | `Content.Server/_NF/Shuttles/Components/ForceAnchorComponent.cs` | Static body, thrusters off, FTL refused. **This is the "becomes a station" primitive.** Reacts to map-init and FTL completion only; runtime use needs a helper. |
| Ore veins and mining | `Content.Shared/Mining/Components/OreVeinComponent.cs`, `Content.Server/Mining/MiningSystem.cs`, `_NF/Gatherable`, `_Mono/.../laser_drill.yml` | Ore drops when a vein entity is destroyed or gathered. Monolith already has a stationary laser drill machine (`ItemMiner`) worth looking at for the crack miner. |
| Planet surface procgen, ore, hostile factions | `Content.Server/Salvage/SpawnSalvageMissionJob.cs` + `BiomeSystem`, `Procedural/salvage_factions.yml`, `biome_markers.yml` | Expedition planets already spawn biomes with ore and mob factions. Same biome machinery as the planet prototype above. |
| Sector-wide announcements | `ChatSystem.DispatchGlobalAnnouncement` | |
| Shipyard vessel definitions | `Content.Shared/_NF/Shipyard/Prototypes/VesselPrototype.cs` | `price`, `category` (Micro–Large), `class` (includes `Capital`), `limit`, `requireCrew`, `addComponents`. Current most expensive hull: 1,540,950. |
| Beams between two entities | `Content.Server/Beam/BeamSystem.cs` | Same map only; not used (see F5). |
| Grid-wide camera shake | `Content.Shared/Gravity/GravityShakeComponent.cs`, `SharedGravitySystem.Shake.cs` | |
| Custom-drawn console controls | `Content.Client/_WF/Shuttles/UI/ShipViewControl.cs` | Precedent for diagram controls drawn in code with the Wolfgate theme. |
| Looking up and down through layers | `CEZLevelViewerComponent`, `CEZLevelBlurOverlay`, `CEToggleZLevelLookUpAction` | Players see the layer below rendered under them, and can toggle to look up. Beams and the crack animation use this. |
| TSF | Locale only (TSF Comms, TSF Contractor, TSF Diplomat) | A faction name; no automated response. |
| Necromorphs | nothing | No mobs, no event, no marker. |

---

## 1. Glossary

- **Cracker** — the planet cracker vessel.
- **Transport** — the micro shuttle docked to the cracker; carries one anchor per trip.
- **Sector planet** — the Far Horizons planet entity you see and FTL to in the sector map.
- **Planet network** — the stack of maps that is one planet's orbit, air and surface (F0).
- **Orbit layer** — the top map of a planet network; space atmosphere; where ships arrive and where the cracker parks.
- **Anchor** — one of two heavy deployable drilling machines. The pair defines the circle.
- **Circle** — the crack region; the anchors are the two ends of its diameter.
- **Berth** — the zone on the cracker, marked by the mapper, where the chunk hangs. Can be on any side of the hull.
- **Projector** — one of two ship machines that beam down to the anchors. Both must be powered.
- **Centrifuge** — the cracker's oversized gravity generator. Its spin (charge) holds ship and chunk in orbit during the crack.
- **Chunk** — the extracted disc of surface, held in the berth on the orbit layer, directly above the hole.
- **Crack miner** — a machine placed on the chunk over a deep vein.
- **Fissure** — cracks that spread around a drilling anchor; hostile mobs crawl out of them.
- **Fall** — the cracker and chunk losing orbit and crashing.

---

## 2. Player loop (corrected)

1. **Choose a planet** on the cracker's sector survey console: name, sanctioned or not, rough vein rating, already cracked or not.
2. **Fly** there. FTL to the planet's beacon in the sector as today, then take the *Enter orbit* jump that appears once you are close. You arrive on the planet's orbit layer.
3. **Survey** — take the transport down, walk the surface with the handheld surveyor. It reveals deep veins that only a crack can reach.
4. **Deploy anchors** — two transport trips, one anchor each. Drag each anchor to position and wrench it down. Once both are within the distance band the circle preview is drawn on the ground. Activate each; it drills unattended for 5 minutes. Fissures spread around each drill and mobs crawl out (F8); the anchors need defending.
5. **Return** to the cracker. Pilot so the berth ghost on the console sits over the circle; within 8 tiles is good enough. On the crack console, target the pair.
6. **Spin up** the centrifuge to full and press *begin crack*. The gravity lock engages: the ship shifts the last few tiles so the berth is exactly above the circle, then force-anchors. The **fall stage** begins: from here until release, a projector or the centrifuge dropping out starts a 5 minute countdown to the fall. **There is no abort.**
7. **Crack** — beams from the projectors to the anchors, shaking, rumble, a crack ring growing around the circle on the surface. Duration depends on circle size and projector parts.
8. **Extract** — the circle, anchors included, is cut out of the ground layer into a new grid in the berth, directly above the hole it left. The planet is flagged cracked.
9. **Mine** — wrench crack miners onto the chunk over revealed veins. High yield. EVA required on the chunk.
10. **Disconnect** — on the chunk, switch both anchors off within 60 s of each other. 60 s evacuation alarm, then the chunk drops straight down through the air layers into the hole and its crash explosion goes off.
11. The cracker is released and can leave orbit.

Other ships can take the same *Enter orbit* jump at any point, see the cracker and chunk on radar, dock, board, or shoot. That is intended.

---

## 3. State machine

State lives on `PlanetCrackerComponent` on the cracker grid, mirrored to the crack console.

| State | Entered by | Exits to | Rules in force |
|---|---|---|---|
| `Idle` | purchase, or `Released` | `Surveying` | none |
| `Surveying` | cracker parked on an orbit layer | `AnchorsPlaced`, `Idle` (ship leaves) | surveyor works; anchors deployable |
| `AnchorsPlaced` | both anchors wrenched down within the band | `AnchorsLocked`, `Surveying` (anchor unwrenched or destroyed) | circle preview on the surface; drills can start; fissures and mobs while drilling |
| `AnchorsLocked` | both drills finished | `Cracking`, `Surveying` (anchor destroyed) | console can target the pair once the berth is aligned; ship may leave and come back |
| `Cracking` (fall stage) | *begin crack* with centrifuge at full and berth aligned | `Cracked`, `AnchorsPlaced` (anchor broken), `Surveying` (anchor destroyed), `Falling` | gravity lock snap, then ForceAnchor on the cracker; grace timer watches projectors and centrifuge; an anchor below half health **pauses** the crack timer until repaired; **no voluntary exit** |
| `Cracked` | crack timer done | `Disconnecting`, `Falling` | chunk exists in the berth, both grids force-anchored; miners run; planet flagged cracked |
| `Disconnecting` | both anchors switched off within 60 s | `Released` | 60 s evacuation timer; anchors cannot be re-armed |
| `Released` | chunk dropped | `Idle` | ForceAnchor removed from the cracker |
| `Falling` | grace expired in `Cracking`/`Cracked` | terminal | ForceAnchor removed, centrifuge capacity zeroed, both grids pushed into downward transit; existing gravity code crashes them |

**Grace timer (D25):** the centrifuge counts as "at full" once charge reaches 0.98 and stops counting as full when it drops below 0.95, so power ripple does not flap the timer. Any tick where it is not at full, or either projector is unpowered or broken, starts or continues the 5 minute countdown. Restoring all three resets it. The console shows the countdown and which condition is failing.

**Anchor damage (D9):** anchors are damageable machines with the usual three stages. Below 50% health during `Cracking` the beam sputters and the crack timer pauses; repair with a welder to resume. **Broken** (the destructible threshold; the machine is still there and repairable): the crack aborts to `AnchorsPlaced`, the pair must re-lock. **Destroyed** (the entity is gone): abort to `Surveying` with one anchor left. In both abort cases the projectors spin down for 30 s, then the cracker's ForceAnchor is released. The fall is reserved for the ship's own systems failing, because anchor loss is usually the work of hostiles on the surface while the crew is in orbit and cannot defend them, and a crash for that would be a punishment with no counterplay. Replacement anchors are a cargo purchase at a steep price.

**Chunk watchdog (D24):** the chunk carries `PlanetChunkComponent { Cracker }`. If its cracker is deleted, or is no longer on the same orbit layer, the chunk drops as if disconnected, with the evacuation alarm. Nothing hangs in orbit without a cracker under it.

---

## 4. Features

Build order: F0 → F1 → F3 → F4 → F5 → F2 → F6 → F7 → F8 → F9.

### F0 — Giving sector planets an orbit and a surface (prerequisite, separate feature)

**What exists.** Two halves, unconnected:

1. **Sector planets** are Far Horizons `PlanetEntity`s spawned by `StarSystemMapSystem.SetSystem` from `SystemKyphrus` (`Resources/Prototypes/_FarHorizons/Space/systems.yml`): Fervidus, Merak, Asclepiu, Aerumna, Thrascias, each at a distance and angle from the star. They are warp points, FTL beacons and IFF blips, drawn by the client's `PlanetOverlay` shader. Players already FTL to them; there is nothing there but space.
2. **Planet surfaces** come from the DeltaV `- type: planet` prototype and `PlanetSystem.SpawnPlanet` / `LoadPlanet`: a biome template, atmosphere, light, marker layers, optional hand-made grid with reserved tiles. Monolith's `DesertWorld` is a working example (biome `MonoDesertPermanentPlanet`, outpost grid `/Maps/_Mono/POI/surface_outpost_desert.yml`), currently disabled on Caelestinus. The surface is generated chunk by chunk around players (`BiomeSystem.ChunkLoader`), so it is unbounded and free until someone stands on it. Expedition planets use the same biome machinery with ore and faction marker layers.

**What a planet network is.** In the CE z-level code a stack of ordinary maps owned by one `CEZMapNetworkComponent` entity, each at an integer depth, sharing world XY, with a component set applied to every map. Grids move between layers by changing map while keeping XY, through a temporary transit map. Players see the layer below rendered under their own and can look up. Only the station builds one today.

```
depth 4   ORBIT LAYER   (new)  space atmosphere, arrival point, grids do not fall  <- cracker parks, chunk in the berth
depth 3   cloud layer          /Maps/_CE/clouds.yml
depth 2   air layer            empty map
depth 1   air layer            empty map
depth 0   GROUND LAYER         DeltaV planet prototype surface (CEZGroundLayerComponent)  <- anchors, fissures, hole
```

**What F0 builds.**

- `PlanetSurfaceComponent` on a sector `PlanetEntity`, set from a new field on the planet type (`surface: DesertWorld` style, pointing at a `planet` prototype plus optional hand-made grid). On first approach (or at round start for sanctioned planets) it builds the network: `PlanetSystem.SpawnPlanet` for the ground layer, empty maps for the air layers, a new orbit map with space atmosphere on top, all added with `TryAddMapsIntoNetwork`. F2's deep vein spawner is one more marker layer.
- **Enter orbit / leave orbit.** The orbit layer gets an `FTLDestination`. A ship within range of the sector planet sees *Enter orbit: Asclepiu* on its shuttle console; a ship on the orbit layer sees *Leave orbit*, which puts it back next to the sector planet. No new physics, and pirates intercept in the sector or in orbit as they like.
- **The orbit mechanic.** Today a grid on any non-ground layer falls unless a gravgen holds it. The orbit layer gets `CEZOrbitLayerComponent` and `UpdateGridGravity` skips grids parked there the same way it skips the ground layer. Sitting in orbit costs nothing. Descending is the existing `DescendZ` pilot action, which already requires a working gravgen; ascending back ends in `TryExitTransit` onto the orbit layer. The cracker's fall is therefore explicit: `Falling` pushes the grids into downward transit with `TryEnterTransit` and zeroes the centrifuge's capacity. During `Cracking` and `Cracked` the centrifuge does no physical work; it is the fiction that justifies the grace timer.
- A round-scoped planet registry (name, sector entity, network entity, flags) that F2's survey console reads. Planets that never get a surface stay as they are.

### F1 — The cracker vessel

**Contents:** crack control room with the console and two projectors on a hull edge; centrifuge hall; power plant sized for the crack; docking port with the transport already docked; cargo bay with two crated anchors and the surveyor; sector survey console on the bridge; point-defence mounts only.

**Berth (D20).** The mapper places a `ChunkBerthComponent` marker on the ship: a rectangle in grid space, on any side of the hull, big enough for the largest circle (44 tiles across, see §5) with clearance from the hull. The projectors sit on the hull edge facing the berth. Where the berth is decides where the chunk hangs, so the control room can be wherever access is best; it only needs a view of the berth.

**Shipyard entry:** `category: Large`, `class: [Capital]`, `limit: 1`, `requireCrew: true`, price 3,000,000, `addComponents: PlanetCracker`. Thrust-to-mass deliberately low so it is slow in every axis.

**Transport:** micro shuttle, one anchor per trip. Its mini gravgen is rated to carry the shuttle plus exactly one anchor, so two anchors aboard exceed capacity and the shuttle drops (`GridHasActiveGravgen`). The transport's console warns before take-off when overloaded (D16).

### F2 — Survey: sector console and surface surveyor

**Sector survey console (cracker bridge, and one at the outpost).** Lists every planet in the round's registry: name, sector position and distance, sanctioned or unsanctioned, cracked or not, and a vein rating (poor / fair / rich / very rich, derived from the planet's vein table without revealing exact contents). Selecting a planet sets it as the FTL target on the shuttle console. Reuses the registry from F0 and the existing shuttle console targeting.

**Surface surveyor (handheld).** Used on the surface it pulses and reveals deep vein markers within its radius on the client overlay, with ore type and estimated yield. Deep veins are hidden entities from a marker layer added when the network is built, seeded from the planet, and only mineable once they are on a chunk.

**Rules:** `DeepVeinComponent { Ore, TotalYield, Rate }`, a new prototype family. `CrackablePlanetComponent { Sanctioned, Cracked, VeinTable }` lives on the planet network entity. Unsanctioned planets roll richer tables. `Cracked` is set at extraction (F5); the crack console refuses a cracked planet.

**New:** survey console BUI, surveyor item and overlay, deep vein prototypes and marker layer, planet flags.

### F3 — Gravity anchors

**Flow:** uncrate on the surface, drag (standard pulling) to position, wrench down. When both anchors are wrenched down within the distance band they pair and the circle preview appears on the ground. Activate each: 5 minute unattended drill with progress on examine and an animated sprite. Both locked → the pair is targetable.

**Rules:** distance band 16–40 tiles between anchor centres; the cut circle's radius is half that distance plus 2 tiles so both 3×3 anchors are inside the cut and ride up with the chunk (D21). No alignment rule. Circle size drives crack time and vein count (§5). Unwrenching a locked anchor is refused. Anchors are owned by the cracker that bought them so two crackers cannot share a pair. Anchors have health and can be repaired; see D9 in §3.

**New:** `GravityAnchorComponent` + system (pairing, drill timer, lock, health thresholds), client circle overlay, sprites and sounds.

### F4 — Crack control: console, projectors, centrifuge

**Berth alignment (D20).** The console's site diagram and the shuttle console radar both show the berth as a ghost rectangle projected onto the surface below, next to the circle. The readout is the offset between berth centre and circle centre with arrows. The pair can be targeted once the offset is within 8 tiles. On *begin crack* the gravity lock engages: the ship is translated by that offset so the berth centre sits exactly over the circle centre (shake, sound), then force-anchored. Pilots only have to get close.

**Console UI.** Every panel is a diagram drawn in code (custom `Control`s with `DrawingHandleScreen`, Wolfgate theme, the same approach as the ship view control), not text tables. Layout:

```
+----------------------------------------------------------------------------------+
| CRACK CONTROL              state: CRACKING          crack remaining 07:42         |
+------------------------------------------+---------------------------------------+
|  SITE (top-down)                         |  CENTRIFUGE                            |
|                                          |         .-'''-.                        |
|      hull ===========[P]=====[P]=====    |       /  \  |  /  \     spin 100%      |
|            +-------- berth --------+     |      |   --( o )--  |   load 1,830 /   |
|            |        .------.       |     |       \  /  |  \  /      3,000 mass    |
|            |      (A)      (A)     |     |         '-...-'                        |
|            |        '------'       |     |  rotor animates at charge speed;       |
|            +-----------------------+     |  slows when losing power               |
|                                          +---------------------------------------+
|  anchors: LOCKED / LOCKED                |  PROJECTORS                            |
|  berth offset 0 tiles (locked)           |   [P1] power ok  integrity 100%  beam  |
|                                          |   [P2] power ok  integrity  92%  beam  |
+------------------------------------------+---------------------------------------+
|  TIMELINE  Survey > Anchors > Locked > [CRACKING] > Cracked > Disconnect > Released|
|  GRACE  --:--  (all systems nominal)                          [ BEGIN CRACK ]      |
+----------------------------------------------------------------------------------+
```

- **Site diagram:** hull edge, berth rectangle, the two anchors as dots with the circle between them, projector positions with beam lines when firing, berth offset readout with arrows. Colours follow state (grey unpaired, amber drilling, green locked, red damaged).
- **Centrifuge dial:** a rotor whose rotation speed follows `Charge`, a spin percentage, and a load bar of grid mass against capacity. Visibly slows when spinning down.
- **Projectors:** two icons with power and integrity bars, beam indicator.
- **Timeline strip:** the state machine as a row with the current state lit and timers under it.
- **Grace countdown** in red when running, naming the failing system.
- Buttons: *target pair* / *untarget* before begin, *begin crack* (enabled only when every precondition passes; hover lists the failing ones). **No abort after begin (D23).** The only exits from `Cracking` are completion, anchor loss, or the fall.

**Centrifuge:** gravity generator prototype variant with `maxHandledMass` above cracker + chunk mass, slow `ChargeRate` (full spin from cold in about 4 minutes), heavy `ActivePowerUse`. Its own machine UI is the same rotor dial.

**Projectors:** two powered machines with machine-part slots. Part tier multiplies crack time (§5). Both must be powered and intact during `Cracking` and `Cracked`.

**Fall stage:** see §3 and F0. Nothing new falls; the feature only decides when to stop holding the ship up. `ForceAnchorSystem` only reacts to `MapInitEvent` and FTL completion, so adding the component at runtime needs a small helper that also calls `ShuttleSystem.Disable` and adds `PreventGridAnchorChangesComponent`, and a matching release helper.

**New:** `PlanetCrackerComponent`, `ChunkBerthComponent`, console BUI and diagram controls, berth ghost on the radar, centrifuge and projector prototypes and systems, grace timer, admin verbs (set state, complete drill, complete crack, force disconnect, mark planet cracked) in the Wolfgate admin tab.

### F5 — The crack: extraction, chunk, hole, effects

**Extraction:** on timer completion, copy every tile inside the cut circle from the ground layer into a new grid; move every entity inside it (walls, deep veins, anchors, mobs, players) onto it; stamp the hole (tiles inside the circle become the crack-hole tile, the rim gets a decal ring). The chunk grid gets `PlanetChunkComponent` and `ForceAnchorComponent` and is placed in the berth on the orbit layer. Because the gravity lock aligned the berth over the circle, the chunk is directly above the hole and the beams run straight down. `Smimsh` on placement for safety. The tether is logical: both grids are static, so neither can move. The chunk has no atmosphere (D3).

**Biome gotchas:** the ground layer is generated lazily, so extraction must call `BiomeSystem.Preload` on the circle's bounds before reading tiles, or unloaded chunks come back empty. The hole must be written through the biome's `ModifiedTiles` path (any tile set through the map API is recorded there), otherwise the biome regenerates the surface over the hole the next time the area unloads and reloads.

**Beams (D4).** Projectors and anchors are on different maps, so `BeamSystem` cannot link them. Each projector carries a networked `CrackBeamComponent { Target: NetEntity }` and the client draws the beam in an overlay: from the projector's screen position to the anchor's position projected onto the layer below, using the same transform the z-level renderer already uses to draw the lower layer under the player. On the surface, looking up, the same overlay draws the beam from the anchor to the projector's projected position above; without look-up, a tall sky-beam sprite stands at each anchor. Animated beam texture, additive shader.

**Effects during `Cracking`:** repeating grid shake on the cracker and near the anchors, looping rumble, crack-ring decals spreading around the circle perimeter on the surface in stages, and the same ring visible from orbit through the z-view.

**Extraction moment:** heavy shake, one-shot boom, the chunk is teleported into the berth (D5) with a burst effect at the hole and at the chunk.

**New:** extraction system (tile and entity copy is the hard part), `PlanetChunkComponent`, crack-hole tile, decals, beam overlay, sounds.

### F6 — Chunk mining

**Flow:** crack miners are machines wrenched onto the chunk over a deep vein marker. Each converts the vein's total yield into ore stacks at the vein's rate while powered. The chunk has no power grid, so miners run on an internal swappable power cell (D15). Move the miner to the next vein when one is exhausted. Monolith's laser drill (`ItemMiner`) is the closest existing machine and a good starting point.

**Rules:** miners refuse to run anywhere except on a chunk over a deep vein. Output is ore, so the existing ore economy prices it.

**New:** `CrackMinerComponent` + system, prototype, sprites.

### F7 — Disconnect protocol and chunk fall

**Flow:** on the chunk, switch both anchors off within 60 s of each other (the first switch-off starts a visible countdown; if the second is late, the first re-arms). Success starts a 60 s evacuation alarm on chunk and cracker. On expiry the chunk loses its ForceAnchor and capacity and is pushed into downward transit (the orbit layer does not drop grids on its own). It falls straight through the air layers into the hole below the berth and `CrashGrid` explodes it. The chunk grid is deleted after the crash; the crater decals stay. The cracker's ForceAnchor is removed as it enters `Released`.

**Rules:** anchors cannot be switched off before `Cracked`. There is no tether entity to cut. Anyone still on the chunk goes down with it. The chunk watchdog (§3, D24) uses the same drop path when the cracker is gone.

**Fall of both grids (`Falling`):** cracker and chunk enter the same transit gap at the same moment with the same gravity, so they fall in lockstep and never swap order; the transit collision check only fires on an order swap with overlapping bounds, and the berth keeps their bounds apart anyway.

**New:** anchor off-switch logic and timers, alarms, watchdog, post-crash cleanup.

### F8 — Site threats: fissures

**Flow:** when an anchor starts drilling, fissure decals begin spreading outward from it in rings, one ring per minute of the 5 minute drill. Each new ring picks a few fissure tiles and spawns mobs from them with a crawl-out effect (burst decal, a short emerge animation on the mob, a crack sound). Mobs come from the planet's faction table and target the anchor first, then players. Unsanctioned planets use a nastier table and more spawns. At extraction a final surge crawls out of the crack ring on the perimeter before the circle lifts.

**Rules:** fissure decals persist for the round (D19) and are cosmetic. Spawn counts are capped per anchor. Mobs keep spawning only while the drill runs, so a locked anchor is quiet until the crack.

**Reuses:** `salvage_factions.yml`, the NPC spawners expeditions use.

**New:** `FissureSpawnerComponent` hooked to drill start/progress and extraction, decals, emerge effect, sounds.

### F9 — Flavour and escalation (later)

- **Sanctioning:** `CrackablePlanetComponent.Sanctioned` per planet. Beginning a crack on an unsanctioned planet raises a sector-wide announcement naming the planet and the ship. TSF players respond freely; no automated response.
- **Unsanctioned yield:** richer vein tables and a chance of a marker vein.
- **Necromorph marker:** nothing exists in the codebase. Separate feature. Extraction raises `PlanetCrackedEvent { Sanctioned, VeinTable }` on the chunk as the hook.

---

## 5. Initial numbers (balance later)

| Thing | Value |
|---|---|
| Cracker price | 3,000,000 |
| Anchor distance band (`d`, centre to centre) | 16–40 tiles |
| Cut circle radius | `d` / 2 + 2 tiles (20–44 tiles across) |
| Berth size | at least 48 × 48 tiles of clear space |
| Berth alignment tolerance | 8 tiles between berth centre and circle centre |
| Anchor drill time | 5 min each, in parallel |
| Centrifuge full spin from cold | 4 min |
| Centrifuge "at full" hysteresis | counts as full at ≥ 0.98 charge, stops at < 0.95 |
| Crack time | 12 min × (`d` / 24) × part multiplier; part multiplier 1.0 (tier 1) to 0.7 (tier 4). Range 5.6 min (small circle, best parts) to 20 min (largest circle, stock parts) |
| Deep veins per circle | 2 + floor(`d` / 8): 4 at 16 tiles, 7 at 40 |
| Vein yield | 1,500–4,000 ore each; unsanctioned ×2 |
| Crack miner rate | 150 ore/min (10–27 min per vein per miner) |
| Grace before fall | 5 min |
| Anchor damage pause threshold | 50% health |
| Disconnect pairing window | 60 s |
| Evacuation window | 60 s |
| Fissure spawns | 2–4 mobs per ring per anchor, cap 15 per anchor; unsanctioned ×1.5 |
| Replacement anchor | 400,000 |

Whole loop with no interference and three or four miners running: roughly 60–80 minutes.

---

## 6. Asset and mapping requirements

Moved to `Docs/PlanetCracker/ASSET_REQUIREMENTS.md` (sprites with sizes and states, sounds, maps, priorities). Keep that file as the single list so the two never drift.

---

## 7. Decisions taken

| ID | Decision |
|---|---|
| D1 | Dedicated orbit layer with space atmosphere; an ordinary map, so every ship can jump to it, see the cracker and chunk on radar, dock, board and shoot. |
| D2 | Chunk is one layer deep (ground layer only). |
| D3 | Chunk has no atmosphere; EVA on the chunk. |
| D4 | Beams are client-drawn from a networked target, projected across layers; no cross-map `BeamSystem`. |
| D5 | Extraction teleports the chunk into place with effects. |
| D6 | Anyone inside the circle rides the chunk up. |
| D7 | Unsanctioned cracks trigger a sector announcement; TSF players intervene freely; no automated response. |
| D8 | The cracker may leave between survey and lock; anchors persist; the console re-validates on return. |
| D9 | Anchor damage pauses the crack below half health, aborts when broken or destroyed; it never triggers the fall. Replacement anchors are purchasable. |
| D10 | Price 3,000,000, one active per round. |
| D11 | One anchor per transport trip, enforced by the transport gravgen's capacity. |
| D12 | Crack time scales with circle diameter and projector part tier; vein count scales with diameter. |
| D13 | Crack console and centrifuge UI are diagram controls drawn in code. |
| D14 | Hostile mobs arrive by crawling out of fissures that spread around drilling anchors, with a final surge at extraction. |
| D15 | Crack miners run on an internal swappable power cell; no cabling across the tether gap. |
| D16 | The transport's shuttle console warns before take-off when its gravgen is overloaded. |
| D17 | Replacement anchors are sold at any cargo console. |
| D18 | The sector survey console also exists at the outpost so crews can plan before buying. |
| D19 | Fissure decals persist for the round. |
| D20 | A mapper-placed berth marker on the cracker defines where the chunk hangs, on any side of the hull. Targeting needs the berth within 8 tiles of the circle; *begin crack* snaps the ship the rest of the way, so the chunk sits directly above the hole. |
| D21 | Cut radius is half the anchor distance plus 2 tiles, so both anchors ride up with the chunk. |
| D22 | Mining numbers: 150 ore/min per miner, 1,500–4,000 ore per vein. Adjust later. |
| D23 | No abort after *begin crack*. Untargeting is only possible before it. |
| D24 | The chunk has a watchdog: if its cracker is gone it drops into the hole on its own. |
| D25 | The centrifuge "at full" check uses hysteresis (≥ 0.98 on, < 0.95 off). |

---

## 8. Remaining questions

None open as of revision 3.

---

## 9. Testing and tooling

- Headless integration tests per the Wolfgate convention: a fixture that builds a ground layer, two air layers and an orbit layer, spawns a cracker grid with a berth marker, centrifuge and projectors, places two anchors on the ground, and drives the state machine directly (no UI). Cover: orbit layer exempts grids from falling; descent still needs a gravgen; pairing refuses out-of-band distances; drill timer; berth alignment refusal outside 8 tiles and the snap on begin; grace timer hysteresis, reset and expiry; fall pushes both grids into transit; extraction tile count includes both anchors and leaves the hole; once-per-round refusal; anchor damage pause and abort; disconnect pairing window; chunk drop lands on the hole's footprint; watchdog drop when the cracker is deleted.
- Admin verbs under the Wolfgate admin tab: set crack state, complete drill, complete crack, force disconnect, mark planet cracked, give a sector planet a surface network at the admin's request.
