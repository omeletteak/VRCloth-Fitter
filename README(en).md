# VRCloth-Declipper

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-2022.3-blue.svg)](#requirements)
[![Status](https://img.shields.io/badge/Status-WIP-orange.svg)](#project-status)

*日本語版: [README.md](README.md)*

**An open-source Unity editor tool that automatically fixes body-through-clothing clipping left after dressing an avatar.**

Even after you fit an outfit with Modular Avatar's Merge Armature (Setup Outfit), differences in body shape leave the body poking through the clothing — "penetration." VRCloth-Declipper fixes this automatically with a detect → push-out → Laplacian-smoothing pipeline.

## What it does / doesn't do

**Does:**

- Automatically fixes the mesh penetration that remains after Merge Armature
- Works as a post-process regardless of how the outfit was fitted (manual fitting of a supported outfit, or an outfit produced by a body-conversion tool)
- Non-destructive workflow (the original mesh assets are never modified)

**Doesn't:**

- Retarget unsupported outfits to a different body shape — that is the domain of conversion tools such as [もちふぃった～](https://booth.pm/ja/items/7657840) and [Alterith](https://booth.pm/ja/items/7131644). VRCloth-Declipper complements them by handling the **stage after** conversion (none of those tools address penetration)
- Save or export avatar shape data (see the design principle below)

## Design principle: No Cache

**No data that could reconstruct an avatar's body shape is written to disk or sent outside the tool.**

The higher its resolution, the more a body-shape difference can reconstruct the original avatar's body, which collides with avatar terms of use and authors' rights. VRCloth-Declipper computes each penetration fix in memory, applies it on the spot, and keeps no shape-derived intermediate data. Being open source is also a way to make this promise verifiable in code.

For the full background and the relationship to prior tools, see [docs/DESIGN.md](docs/DESIGN.md) (Japanese).

## Project status

**The MVP (penetration-fix core) is implemented. What remains is hands-on visual E2E verification and distribution (VPM packaging).** No distribution package is published yet.

| Pipeline | Status |
|---|---|
| Proxy body generation (bone capsules) | ✅ Implemented |
| Capsule distance (SDF) | ✅ Implemented (tested) |
| Scene-view visualization (capsules + penetration heatmap) | ✅ Implemented |
| Penetration detection | ✅ Implemented (tested) |
| Push-out + Laplacian smoothing | ✅ Implemented (tested) |
| Non-destructive apply (mesh copy, Undo support) | ✅ Implemented (tested) |
| Preflight diagnostic (green/yellow/red scope verdict) | ✅ Implemented (tested) |
| Body representation: mesh-SDF collider (BVH + fast winding number) | ✅ Implemented (tested) |
| Hands-on visual E2E verification | 🚧 Remaining (guide: [docs/E2E_TEST_GUIDE.md](docs/E2E_TEST_GUIDE.md), Japanese) |

89 EditMode tests are green. See [ROADMAP.md](ROADMAP.md) (Japanese) for the plan.

## Requirements

- Unity **2022.3.22f1** (the VRChat-recommended version)
- A VPM project managed by [ALCOM](https://vrc-get.anatawa12.com/ja/alcom/) or the VRChat Creator Companion (VCC)
- [NDMF](https://github.com/bdunderscore/ndmf) / [Modular Avatar](https://modular-avatar.nadena.dev/)

## Try it (for developers)

There is no distribution package yet. For now, clone the repository:

```powershell
git clone https://github.com/omeletteak/VRCloth-Declipper.git
cd VRCloth-Declipper
vrc-get resolve   # or open in ALCOM / VCC to resolve dependencies (the VRChat SDK etc. are not bundled)
```

To try it in another VPM project, junction-link only the package folder (do not copy the whole repository — that would duplicate the SDK):

```powershell
New-Item -ItemType Junction `
  -Path "<test project>\Assets\VRCloth-Declipper" `
  -Target "<clone path>\Assets\VRCloth-Declipper"
```

Usage: after fitting an outfit with Modular Avatar's Setup Outfit, add the `VRClothDeclipper` component to the outfit GameObject; it auto-detects the outfit mesh and the parent avatar. Use **Preview Body Proxy** in the inspector to show the proxy in the scene view, then **Run Fitting** to fix penetration (non-destructive; Undo restores). For a more accurate body, enable **Use Mesh SDF Collider** to collide against a signed-distance field built from the body mesh instead of bone capsules (see [docs/DESIGN.md](docs/DESIGN.md) §6, Japanese).

## Contributing

Bug reports, suggestions, and pull requests are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT License](LICENSE)
