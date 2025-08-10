# VRCloth-Fitter Development Roadmap

This document outlines the development plan and feature history for VRCloth-Fitter.

## Goal

The goal of this tool is to provide a robust, community-driven suite of features for avatar outfit customization, ensuring a non-destructive workflow.

### Phase 1: Basic Scale Fitting

This phase focuses on adjusting the scale of clothing bones to match the avatar's proportions.

- [x] **Bone Mapping UI**:
    - [x] Automatically detect and list bone pairs between the avatar and the cloth based on name matching.
    - [x] Provide a user interface to manually edit and confirm these bone mappings.
- [x] **Scale Calculation**:
    - [x] Calculate the "length" of a bone (distance to its child).
    - [x] Analyze surrounding mesh vertices to estimate the "thickness" or "volume" around a bone.
- [x] **Scaling Application (Non-Destructive Workflow)**:
    - [x] Create a dedicated component (`VRClothFitterScalingData`) to store the calculated scale ratios.
    - [x] The editor window writes the scale information to this component instead of modifying bones directly.
    - [x] Implement an NDMF pass to apply the stored scale data to temporary objects during the avatar build process.
- [x] **Feature Improvements**:
    - [x] Add consideration for bone "thickness" to the scale calculation for a more three-dimensional fit.
    - [x] Add a real-time preview feature for immediate feedback.
    - [x] Improve Bone Mapping UX (e.g., highlighting Humanoid bones).

### Phase 2: Advanced Mesh Deformation

This is the core feature, aiming to directly modify the clothing mesh to fit the avatar's body shape.

- [x] **Deformation Data Component**:
    - [x] Create a new component `VRClothFitterDeformationData` to store all necessary deformation information, ensuring a non-destructive workflow. This component holds a list of control point pairs.
- [x] **Control Point (Anchor) System**:
    - [x] **Step 1: Custom Editor**: Create a custom editor script to replace the default inspector for the `VRClothFitterDeformationData` component.
    - [x] **Step 2: Anchor Edit Mode**: Implement an "Anchor Edit Mode" button in the inspector, which disables normal scene view interactions.
    - [x] **Step 3: Mesh Raycasting**: Implement the ability to detect points on the surface of the avatar or cloth mesh when the user clicks in the scene view.
    - [x] **Step 4: Anchor Placement & Visualization**: Place and display anchors (gizmos) at the clicked positions in the scene view, using different colors for avatar and cloth anchors.
    - [x] **Step 5: Anchor Pair Creation & Data Storage**: Implement the logic to save the placed avatar and cloth anchors as a pair in the `VRClothFitterDeformationData` component and visualize the pair with a connecting line.
- [x] **Mesh Deformation Algorithm**:
    - [x] **Calculate Difference Vectors**: For each anchor pair, calculate the difference vector (displacement) between the avatar and cloth anchor positions.
    - [x] **Weighted Average Vertex Movement**: For each vertex in the cloth mesh, determine its new position by calculating a weighted average of the difference vectors based on its distance to all anchors.
- [x] **Non-Destructive Application via NDMF Pass**:
    - [x] Create a new NDMF Pass that executes during the avatar build process.
    - [x] This pass finds the `VRClothFitterDeformationData`, generates a new, deformed mesh asset by duplicating and modifying the original, and replaces the mesh on the `SkinnedMeshRenderer` of the temporary object.
    - [x] The `VRClothFitterDeformationData` component is removed from the final built avatar.

### Phase 3: Feature Enhancements

- [x] **Blendshape (Shape Key) Sync**:
    - [x] Create a UI to link avatar blendshapes to corresponding cloth blendshapes.
    - [x] Automate the setup of Modular Avatar's `Blendshape Sync` component.
- [x] **Material & Shader Utility**:
    - [x] A utility to batch-convert materials on a cloth item to a user-specified shader (e.g., lilToon) and attempt to map textures.
- [x] **UI Localization**:
    - [x] Add Japanese language support for the editor window UI.

### Phase 4: Community Features

- [x] **Preset Import/Export**:
    - [x] Implement functionality to export scaling (Phase 1) and mesh deformation (Phase 2) data as JSON files.
    - [x] Include metadata such as the target avatar and cloth names in the JSON file.
    - [x] Implement functionality to import JSON preset files and restore the data to the corresponding components.
- [ ] **Improve Preset System with GUIDs**:
    - [ ] Store the source avatar's Prefab GUID in the exported preset file.
    - [ ] On import, search the project for a matching GUID to automatically detect the target avatar.
    - [ ] Implement a fallback to search by avatar name if no matching GUID is found.

---

*This roadmap is subject to change based on community feedback and development progress.*
