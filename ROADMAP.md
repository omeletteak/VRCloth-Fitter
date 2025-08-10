# VRCloth-Fitter Development Roadmap

This document outlines the development plan and feature history for VRCloth-Fitter.

## Goal

The goal of this tool is to provide a robust, community-driven suite of features for avatar outfit customization, ensuring a non-destructive workflow.

### Phase 1: Basic Scale Fitting (Backend)
- [x] **Scale Calculation**
- [x] **Scaling Application (NDMF Pass)**

### Phase 2: Advanced Mesh Deformation (Backend)
- [x] **Deformation Data Component**
- [x] **Mesh Deformation Algorithm**
- [x] **Non-Destructive Application (NDMF Pass)**

### Phase 3: Feature Enhancements (Backend & Logic)
- [x] **Blendshape (Shape Key) Sync Logic**
- [x] **Material & Shader Utility Logic**
- [x] **UI Localization System**

### Phase 4: Community Features
- [x] **Preset Import/Export**:
    - [x] Implement functionality to export/import scaling and deformation data as JSON files.
- [ ] **Improve Preset System with GUIDs**:
    - [ ] Store the source avatar's Prefab GUID in the exported preset file.
    - [ ] On import, search the project for a matching GUID to automatically detect the target avatar.
    - [ ] Implement a fallback to search by avatar name if no matching GUID is found.

### Phase 5: UI/UX Overhaul (Component-Based Workflow)
- [x] **Create Main Component (`VRClothFitter.cs`)**:
    - [x] Develop a new main component that users will add to their cloth objects. This component will hold the reference to the target avatar.
- [x] **Create Custom Editor (`VRClothFitterEditor.cs`)**:
    - [x] Migrate all UI and logic from the old `VRClothFitterWindow` into a new custom editor for the `VRClothFitter` component.
    - [x] This will provide a more intuitive, Inspector-based workflow similar to Modular Avatar.
- [x] **Integrate All Features into Custom Editor**:
    - [x] **Bone Mapping UI**: Re-implement the bone mapping interface within the new custom editor.
    - [x] **Blendshape Sync UI**: Re-implement the blendshape sync interface.
    - [x] **Material Utility UI**: Re-implement the material conversion utility.
    - [x] **Preview Functionality**: Integrate the real-time preview button.
- [x] **Deprecate Old Editor Window**:
    - [x] Remove the `[MenuItem]` attribute to hide the old window, eventually phasing out the `VRClothFitterWindow.cs` file.

---

*This roadmap is subject to change based on community feedback and development progress.*