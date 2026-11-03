---
name: mandible-lighting-audit
description: Set and audit lighting, exposure and materials in Mandible (Godot 4.7 .NET, Forward+, procedural shaders in src/Render/Shaders.cs). Use when changing the sun, environment, nest/tunnel lighting, or any organic material (leaf, food, pebble, rock, soil, ant chitin), or when the owner says something "looks shiny", "flat", or "same light inside and outside".
---

# Mandible lighting & material audit

Where things live (verify with `rg` first, line numbers drift):
- Sun + WorldEnvironment: `src/Game/GameSession.cs` `BuildLighting()` and `ApplyQuality()`; `SunAngles` constant.
- Quality tiers: `src/Game/Settings.cs` (Ssao/Ssil/Glow/Haze flags).
- Camera exposure / DOF: `src/Game/RtsCamera.cs` (`CameraAttributesPractical`).
- All materials: `src/Render/Shaders.cs` string constants (`Ant`, `Terrain`, `Rock`, `Leaf`, `Grass`, `Twig`, `Food`, `Pebble`, `Needle`, `Pool`...).
- Nest/tunnels: `src/Render/NestInterior.cs`, `NestOrganic.cs`, `NestGeometry.cs`, `ChamberIdentity.cs`.
- Captures: `src/Capture.cs`, `tools/capture.ps1`, `--interior-capture=`, `--command-capture=`.

## Step 1 — Baseline captures (before touching anything)
1. `dotnet build --no-restore`.
2. Capture the same fixed views at `--gfx=High` and `--gfx=Low`: surface wide, surface close (ant on a leaf), nest entrance, chamber close-up, gallery. Save to `captures/before/`.
3. Note for each frame: histogram (use `magick img -format %[fx:mean] info:` for mean luminance), clipped-white %, crushed-black %.

## Step 2 — One sun, one direction (consistency rules)
- Exactly ONE `DirectionalLight3D` for the surface. Sky sun disc must follow it (ProceduralSkyMaterial does this automatically when `SunAngleMax/SunCurve` are used).
- Elevation 35–55° for readable ant shadows (pitch −35…−55). Azimuth fixed per map; never rotate per camera.
- Any baked/vertex AO direction (`ComputeAO` in `WorldBuilder.cs`), painted contact shadows, decals or textures must agree with the sun azimuth or be non-directional. Generated textures must be flat-lit (no baked highlights) – see prompt rules in `docs/interior-graphics.md`.
- Shadow softness = sun angular size: `LightAngularDistance` 0.5–1.0° (real sun is 0.53°); higher only on Ultra. Keep `ShadowBlur` ≤ 1.5. `ShadowNormalBias` low enough that ant legs still touch their shadows (check close-up for "peter-panning").
- Check: every cast shadow in a frame points the same way; tall objects (grass, twigs) give long thin shadows, pebbles short dark contact shadows.

## Step 3 — Exposure zones (outdoor vs underground)
- Outdoor key: sun energy ~1.6–2.0, ambient (sky) energy 0.3–0.5, ACES/AgX, exposure ≈1.0. Ratio sun:fill on a horizontal surface ≈ 4:1 to 6:1.
- Underground must NOT inherit sky ambient. Give nest geometry its own path: either a second `WorldEnvironment` swap / tween when `NestView` is on, or (better) a material uniform `ambient_scale` (0.05–0.12) and set `ambient_light_disabled` render mode on tunnel walls, lighting them only by local OmniLights.
- Tunnel lights: warm (2700–3200 K ≈ Color(1.0,0.72,0.45)), sparse (one per chamber + one every ~8–12 world units of gallery), `OmniRange` 3–6, `OmniAttenuation` 1.5–2, energy 1.5–4, `ShadowEnabled` on the 4–8 nearest to camera only (pool and reassign each frame), cubemap shadows off on Low.
- Target luminance: surface mid-grey ~0.45 mean, tunnels ~0.12–0.18 mean with pools reaching 0.5. When camera transitions into the nest, tween `TonemapExposure` (eye adaptation) 1.0 → 1.6 over ~0.6 s so blacks stay rich but readable.

## Step 4 — Roughness / specular rules for organic materials
| Material | Roughness | SPECULAR | Notes |
|---|---|---|---|
| Dry soil / sand | 0.9–1.0 | 0.2–0.3 | damp patches 0.6–0.75, darken albedo 20–30% |
| Fallen leaf (dry) | 0.8–0.95 | 0.15–0.25 | NEVER < 0.6; fresh green 0.6–0.7 with SSS/backlight; no clearcoat |
| Pebble / rock | 0.75–0.95 | 0.2–0.3 | wet only near water: 0.3–0.4 |
| Bread crumb | 0.85–0.95 | 0.2 | |
| Fruit / berry | 0.35–0.55 | 0.4 | SSS 0.5–0.9, rotting areas rough 0.8 |
| Sugar crystal | 0.15–0.3 | 0.5 | small, OK to sparkle |
| Ant chitin | 0.3–0.5 | 0.35–0.5 | clearcoat 0.2–0.4 for glossy species; dead +0.3 rough |
| Larva / grub | 0.45–0.6 | 0.35 | SSS 0.6, warm transmittance |
| Water | 0.02–0.08 | 0.5 | depth-tinted, fresnel, refraction via SCREEN_TEXTURE |
Albedo ranges (linear-ish sRGB): nothing natural on the ground above 0.75 or below 0.03. Keep `METALLIC` 0 for everything except stylised hero metal.
Always add roughness variation (noise ±0.08) – uniform roughness reads as plastic.

## Step 5 — Audit checklist (each item pass/fail, with screenshot pair)
- [ ] All shadows parallel and pointing the sun's way; sun disc in sky matches.
- [ ] No mirror highlights on leaves, soil, pebbles, crumbs at any camera angle (rotate camera 360° at grazing angle; specular hotspots must stay broad and dim).
- [ ] Leaves show colour spread (green→yellow→ochre→brown→dark skeleton) with no two neighbours identical.
- [ ] Underground mean luminance < 40% of surface; local light pools visible; ants cast moving shadows from tunnel lights.
- [ ] Contact shadows/AO under every object; nothing floats.
- [ ] SSAO radius matches ant scale (ants ~0.3–0.6 units: radius 0.5–1.2).
- [ ] Low tier still has the same sun direction and inside/outside contrast (cheap: ambient scale + unshadowed omnis).
- [ ] Before/after captures saved side by side (`magick before.png after.png +append cmp.png`) and reviewed by eye; shader compilation passing is NOT proof of visual quality.

## Step 6 — Report
List changed constants with old→new values, the capture pairs, luminance numbers, and remaining failures. Do not push without the owner's OK.
