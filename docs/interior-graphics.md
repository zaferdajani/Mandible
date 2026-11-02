# Chamber interior graphics pass

This rendering pass builds on `codex/colony-command-campaign` at `bb51e6f2429c258ac5eb6338fdea53aec149a2ba`. It responds to the owner's concern that the contents of the nest holes still look poor. The new branch is `codex/interior-materials`; it includes the earlier HUD, Episode 1, skirmish, incubator and PR #1 tunnel work.

## Changes

- Chamber floors and banks use a new photographic soil albedo with triplanar mapping, mipmaps, restrained surface relief and darker wall crevices. The live connected excavation geometry remains intact.
- Shared instanced meshes replace the simpler chamber props: tapered eggs, curled segmented larvae, fibrous cocoons, grooved seeds, amber resin, toothed curved mandibles, overlapping chitin segments, leaves with veins, and silk bundles with real wound strands.
- Contents use irregular beds and variation in pose, scale and material. Separate egg, larva and cocoon beds make the incubator readable. Storage contents still respond to food level.
- Roots, grit and soft contact shadows ground the props. The quality setting controls dressing density and prop shadows. Meshes and materials are cached and shared.
- Gallery floors are wide enough for the simulation's lateral walking allowance and the worker's load. This prevents the visible banks from burying workers that are on valid tunnel routes.
- Low quality uses inexpensive directional shading for the cutaway floor. This avoids relying on lighting buffers during quality changes; earlier iterations showed intermittent black floor striping when switching from High to Low. The final transport capture and repeated High/Low capture sequence were clean. Test other GPUs before treating this as a universal driver fix.
- `--interior-capture=<absolute-directory>` captures the full HUD, chamber close-ups, rotated geometry and quality changes. It checks that all chamber materials compile and the soil asset loads. Screenshots still require visual review; shader compilation alone is not proof of visual quality.

The chamber dressing adds no simulated ants, food, eggs, population or orders. Brood beds and decorative winged ants are visual props, not nurse AI or individually simulated brood. Neither the supplied reference nor the generated soil asset is used as a fake screenshot or world background.

## Visible food transport

The owner's subsequent request also changes actual worker delivery behavior. Workers hold distinct crumb, berry, sugar, seed or insect-protein meshes in their mandibles. Cargo shares the ant's head animation, turn, slope alignment and individual scale. Jaws keep a steady grip while loaded. Visibility follows the real `Carrying` and `CarryKind` fields and the same fog/layer occlusion as the ant; there is no decorative or screen-space cargo icon.

In Colony War, skirmish and Episode 1, a loaded worker walks through the entrance and galleries to a completed storage chamber before crediting food and clearing its load. The royal chamber serves as the temporary larder if storage is not built. Empty workers leave through the shaft before gathering again. Before the colony is dug, deliveries still go to the surface entrance. Lane War retains its entrance delivery route. Food value, effects and storage capacity are unchanged, but the added journey changes collection timing; long-match economy should be checked before release. An interrupted delivery resumes before picking up another item.

`WorkerCargo.cs` shares the Ant shader's vertex animation and supplies cargo-specific meshes/materials. `FoodTransportValidation.cs` checks five food types, both teams, surface-to-storage travel, credit exactly once and return empty handed. Its graphical capture checks visible load counts against simulation state and includes surface, underground, Low quality and post-deposit frames.

The result is still **not an exact visual match** to the supplied concept. The room composition, ant activity, model detail, lighting and interface trim need further art direction. Mobile performance, export builds and long matches have not been established by the desktop captures.

## Asset provenance

`assets/materials/nest-soil-albedo.png` is an original image generated with the built-in `image_gen` tool in Codex on 2 November 2026, using the imagegen skill. It is an opaque soil texture, not a generated game preview. The result was copied unmodified into the project; surface relief is calculated in the shader. The original owner's reference remains unchanged in `assets/ui/colony-reference.png`.

Final generation prompt:

> Use case: photorealistic-natural. Asset type: seamless tiling albedo texture for the dug floor and banks of a realistic miniature ant-colony game, not a scene or screenshot. Produce a square, edge-to-edge, orthographic overhead macro photograph of dry warm ochre-tan sandy loam, compacted unevenly by ant feet, with tiny quartz grains, fine earthy pores, small irregular dirt aggregates, and a few short broken brown root fibres embedded in the soil. Grain scale is fine: approximately a 25 by 25 cm patch viewed very close. Subtle natural variation between honey-brown earth, muted tawny clay, light warm beige sand grains and small dark pores. Microrelief should be visible but NO cast shadows, NO directional lighting, NO vignette, NO depth of field, NO horizon, NO large stones, NO plants, NO insects, NO eggs, NO UI, NO text. Completely even diffuse neutral lighting suitable for a physically based albedo material; no glossy highlights or baked ambient occlusion. Photographic organic detail, not noise, painted plaster, orange plastic, checker patterns or procedural-looking swirls. Tile seamlessly along both axes, with similar detail and brightness at every edge.

## Validation commands

Use the complete Godot 4.7.2 .NET distribution with `GodotSharp/Api/Debug` beside the executable and .NET 9. Run asset import and restore on a fresh checkout before building. Pass an absolute path to `--log-file` on Windows.

```text
dotnet build --no-restore
godot --headless --audio-driver Dummy --path . -- --campaign-test
godot --headless --audio-driver Dummy --path . -- --nesttest=headless
godot --headless --audio-driver Dummy --path . -- --transport-test
godot --audio-driver Dummy --windowed --resolution 1672x941 --path . -- --gfx=High --interior-capture=<absolute-directory>
godot --audio-driver Dummy --windowed --resolution 1672x941 --path . -- --gfx=Low --interior-capture=<another-absolute-directory>
godot --audio-driver Dummy --windowed --resolution 1672x941 --path . -- --gfx=High --command-capture=<absolute-directory>
godot --audio-driver Dummy --windowed --resolution 1672x941 --path . -- --gfx=High --transport-capture=<absolute-directory>
```

The transfer package includes actual captures and logs. See its README for final run results and integration instructions. Earlier gameplay details and remaining campaign work are documented in [colony-command-campaign.md](colony-command-campaign.md).

The final Windows checks passed: build (0 errors), five-kind transport for both teams, Episode 1 (255 simulated seconds, 702 food gathered), geometry (32,874 triangles in its fixture), mouse-driven HUD/placement/research checks, nine interior views including repeated High/Low changes, and four transport views using actual simulation pickups. A 90-second AI smoke run completed; local 60-second checksums were Colony `F742FDCD` and Lane War `C53B88D9`. These are local exercises, not multiplayer or cross-platform certification. Interior capture rates varied with window focus, so they are not a performance benchmark.

Existing C# warnings and an inaccessible NuGet vulnerability cache remain. Godot reports 8 leaked ObjectDB instances and 4 resources in use at shutdown on some runs; asset import also logged an editor-settings/Android shutdown warning. No shader compilation errors remained in the final graphical runs. Investigate the shutdown warnings and test exports before a release. The scene captures use scripted fixtures; the carried objects come from actual pickup/transport/deposit simulation states.
