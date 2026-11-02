# Hero portraits

The Warlord, Acid Matron and Sky Marshal have their own portrait art. Until v0.10.0 their Colony Command cards reused the painted Major, Acid Gunner and Flying Ant tiles from `assets/ui/colony-reference.png`.

## Where they appear

| Place | Art |
| --- | --- |
| Colony Command, Heroes tab cards (`ColonyCommandHud.RebuildCards`) | wide card |
| Colony Command, colony queue (current, next, waiting slots) | wide card |
| Colony Command, selection panel (your selected hero, or an inspected enemy hero) | upright bust |
| Lane War hero picker (`HeroPicker`) | wide card |
| Lane War HUD hero panel (`LaneHud`) | upright bust |
| Field Guide hero entries (`FieldGuide.Card`) | upright bust |

Ordinary castes keep their painted atlas tiles in the command HUD and their live 3D specimens in the Field Guide. The legacy production and hero buttons in `Hud.cs` are built only for Lane War, where they are hidden. They keep their vector `AntGlyph` icons.

Each colony has its own set: `black` for the player colony (team 0) and `red` for the enemy (team 1). The HUD picks the set from `Local.Me`, so a client playing as team 1 sees red portraits. An inspected enemy hero shows the enemy colour.

## Source and provenance

No image generator was used. `assets/ui/heroes/*.png` are offline renders of the game's own hero meshes and materials. They use `AntMesh.Build` with `HeroRegalia`, `AntRenderer.AntMat` with hero inlays, the acid sac and team colours, and `Shaders.Wing` for the Marshal's wings. `HeroStudio` in `src/UI/HeroArt.cs` renders them in a dedicated scene:

- The set uses the same soil photograph as the nest chambers (`assets/materials/nest-soil-albedo.png`), with scattered pebbles and straw like the painted caste cards.
- A low warm key light comes from the front left, with a strong warm rim light from behind and a soft warm bounce. ACES tonemapping, SSAO, a little glow and a far depth-of-field blur complete the look.
- Each hero is posed for its special. The Warlord raises its head with jaws flung wide for the war cry; its crest, shoulder spines, gold-rimmed gaster plates and serrated mandibles come from the game mesh. The Acid Matron shows a swollen glowing acid sac with gland studs and gold ruff, and a bead of venom hangs from her acidopore over a splash on the soil. The Sky Marshal stands with its wings swung open, plus its plume, epaulettes and pennants.
- Each image is rendered at 2× size with 4× MSAA, reduced with Lanczos, then finished with a warm shadow tint and vignette.

Wide cards are 640 × 320, matching the 2:1 caste tiles. Busts are 400 × 490, matching the 155 × 190 selection portrait.

To regenerate them after a hero mesh or material changes, run Godot in a window, not headless:

```text
godot --audio-driver Dummy --windowed --path . -- --bake-heroes=<absolute path>/assets/ui/heroes
godot --headless --audio-driver Dummy --path . --import
```

## Level aura

In the field, every hero of either colony stands in a faint aura. `AntRenderer.HeroAura` draws it with `Shaders.Aura` as two additive layers: a soft swirling pool of light on the soil under the hero (kept on the ground under the flying Sky Marshal) and a camera-facing halo just behind the body. Both layers slowly turn and blend.

- **Level colour.** Each level moves the aura one step along `AntRenderer.AuraLevel`: 1 pale gold, 2 amber, 3 orange, 4 ember, 5 rose-crimson, 6 violet, 7 indigo, 8 azure, 9 cyan, 10 radiant white-gold. Higher levels also make it slightly larger and stronger.
- **Abilities.** While the special is ready, the aura breathes gently. When the hero casts, detected by its cooldown jumping back up, the aura flares outward and blends toward the special's own colour, then settles back over about 1.5 seconds. The colours are ember for the Warlord's War Cry, acid green for the Acid Matron's Acid Burst, and sky blue for the Sky Marshal's Dive Bomb.

The aura is presentation only. It reads simulation state (`Level`, `AbilityCd`) but changes nothing in the simulation and is not synchronised.
