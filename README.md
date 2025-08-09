# VRCloth-Fitter

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

An open-source Unity editor tool designed to easily fit clothing to your VRChat avatars. It leverages the **[Non-Destructive Modular Framework (NDMF)](https://github.com/bdunderscore/ndmf)** used by Modular Avatar to apply all changes safely at build time.

[For Japanese instructions, please see README(jp).md](./README(jp).md).

## Features

- **Bone Mapping**: Automatically maps clothing bones to your avatar's bones and allows for manual adjustments.
- **NDMF-Based Scaling**: Calculates bone scaling (both length and thickness) and saves it to a component. The scaling is applied by an NDMF pass during the avatar build process, leaving your original assets untouched.
- **NDMF-Based Mesh Deformation**: Fit clothing to the avatar's body shape by placing anchor points. An NDMF pass generates a new, deformed mesh at build time, ensuring a completely non-destructive workflow.
- **Blendshape Sync Helper**: Automatically sets up Modular Avatar's `ModularAvatarBlendshapeSync` component by mapping corresponding blendshape names.
- **Material Converter**: A utility to convert clothing materials to your desired shader (e.g., lilToon) while attempting to preserve textures.
- **Preset System**: Export and import your fitting data (both scaling and deformation) as JSON files to share with the community.

## Installation

This package is distributed via the VRChat Creator Companion (VCC).

1.  Open the VCC and go to **Settings** > **Community Packages**.
2.  Click **Add** and paste the following URL:
    ```
    https://raw.githubusercontent.com/omeletteak/vpm-listing/main/index.json
    ```
3.  Click **Confirm**. `VRCloth-Fitter` will now be available in the package list to add to your projects.

## How to Use

1.  **Bone Fitting**:
    - Open the tool from **Tools > VRCloth Fitter**.
    - Assign your Avatar and Cloth GameObjects.
    - In the "Bone Mapping" section, verify the automatic mapping and correct any mismatched bones using the dropdowns.
    - Click **Fit Bones** to re-parent the bones in the cloth's Skinned Mesh Renderer.
2.  **Scale Fitting**:
    - After mapping bones, click **Calculate & Save Scale**. This creates a `VRClothFitterScalingData` component on your cloth object.
    - Use the **Toggle Preview** button to see the changes in the Scene view.
    - The scaling will be applied by NDMF when you upload your avatar.
3.  **Mesh Deformation**:
    - Add a `VRClothFitterDeformationData` component to your cloth object.
    - Assign the Avatar Root.
    - Use the "Add New Anchor Pair" button and click on your avatar and then your cloth in the Scene view to create anchor pairs.
    - Adjust anchor positions using the handles in the Scene view.
    - The deformation will be applied by NDMF when you upload your avatar.

## Development

For details on the development plan and feature history, please see [ROADMAP.md](./ROADMAP.md).

## License

This project is licensed under the [MIT License](./LICENSE).
