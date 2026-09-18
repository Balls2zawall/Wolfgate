# Kyphrus terrain and ambient wildlife

Planet-specific recipes live in `Resources/Prototypes/_WF/PlanetCracker/open_biomes.yml`;
ambient animal tables live in `fauna.yml`; `carcinoma.yml` defines the quarantined sixth world. The upstream biome templates and the dev
map's automatic DesertWorld are unchanged.

- Fervidus: basalt flats, narrow lava channels and isolated basalt ore outcrops.
  Native argocytes have heat-adapted variants; they still take normal combat damage.
- Merak: adapted from MonoDesertPermanentPlanet, retaining piluma/orakim plants,
  desert animals and richer sand ore. Ore walls occupy smaller patches.
- Asclepiu: grassy plains, patches of woodland, broad coastal water and snowy regions.
  Foxes, lizards, crabs and snakes populate land clearings.
- Aerumna: chromite clearings between shadow groves, sparse crystals and rock outcrops.
  Slurvas are common relative to the occasional hostile xeno.
- Thrascias: open snowfields and ice, occasional ridges and narrow liquid-plasma channels.
  Cold-tolerant argocytes and occasional space bears inhabit this fictional frozen surface.

- Carcinoma: flesh ground, static meat-wall outcrops and flesh clumps. Chimera beasts
  roam with rarer large horrors. The sector body is red/pink, the survey marks it
  unsanctioned, and existing TSF notices apply when a crack starts and completes.
  No self-spreading kudzu is generated.

Ambient animals are separate from anchor/fissure attack waves. Sparse biome entity
layers place one-shot WFPlanetFaunaSpawner entities backed by entity tables; directly placing mobs in those layers
would anchor them. Hazard and rock layers take precedence over fauna. Existing
biome modification tracking prevents a consumed spawner from reappearing when its
terrain unloads and reloads. Mobile animals persist under normal entity lifecycle
rules. Ambient spawning is capped at 32 living animals per origin planet and 128
across all planets. Deleted/dead animals release slots; captured animals still count.
A capped site is consumed rather than rerolled on every visit. These caps govern
ambient generation, not fissure mobs, admin spawns or infection-driven transformations.
Corpses, items and shipwrecks are not removed by this system.

Deep-vein markers, yield tables, planet atmospheres and flight rules are unchanged.
These recipes use the existing generation system and existing art. No engine edits
or new outpost maps are required.

Use fresh planet networks for verification: already-generated terrain is not
retroactively replaced. PlanetEcologyTest samples three distant regions per world,
checks open-space and rock coverage, exercises real biome spawning, checks that
animals are mobile and climate-compatible, and reloads their chunk to catch duplicate
spawns. Planet/radar tests cover the existing network and terrain display paths.

## Performance scope

Planet networks create five maps each, but terrain/entities stream around players
and subscribed viewers (including views from orbit). Unvisited networks have no
loaded terrain chunks. Normal biome unload checks run every 10 seconds; modified
tiles and mobile animals can persist. NPC AI defaults to sleeping beyond 32 tiles
from players when npc.pause_when_no_players_in_range is enabled. Sleeping AI still
has entity/component overhead. No per-tick population scan was added: admission
checks prune a bounded list only when a wildlife spawner is encountered.

PlanetPopulationTest exercises all six networks, verifies no eager terrain generation,
and saturates per-planet/global caps, including moving an animal and releasing a slot.
This is a correctness check, not an FPS or server-tick benchmark. A representative
multi-player round is still needed to measure tick time, pathfinding, networking,
loaded chunks, persistent objects and ship-impact spikes on the target server.
