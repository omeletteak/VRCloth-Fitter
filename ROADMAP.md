# VRCloth-Fitter Development Roadmap

This document outlines the development plan and feature history for VRCloth-Fitter.

## Core Philosophy

The goal of this tool is to provide a robust, intuitive suite of features for avatar outfit customization. Our core philosophy is to **avoid reinventing the wheel** by leveraging the powerful, industry-standard features of **Modular Avatar (MA)** wherever possible.

VRCloth-Fitter's primary role is to act as an **intelligent setup utility** for MA components, providing advanced calculations and automation that MA itself does not offer, ensuring a seamless and non-destructive workflow for the user.

### Phase 1: Proof of Concept (Legacy)

- [x] **Initial Implementation**: Developed a standalone, window-based tool with custom NDMF passes for bone scaling and mesh deformation. This phase validated the core fitting algorithms.

### Phase 2: Architectural Refactor - Synergy with Modular Avatar

This phase marks a fundamental shift in architecture to deeply integrate with Modular Avatar, delegating core functionality to it for maximum stability and simplicity.

- [x] **UI/UX Overhaul**:
    - [x] Migrated from an editor window to a modern, intuitive component-based workflow (`VRClothFitter` component and custom editor).
- [x] **Deprecate Custom Scaling System**:
    - [x] **Remove `ScalingPass.cs` and `VRClothFitterScalingData.cs`**. Build-time scaling is now fully delegated to `MA Merge Armature`.
- [x] **Redefine "Calculate Scale" Feature**:
    - [x] The "Calculate Scale" button no longer saves data to a custom component. Its new role is to:
        1.  Calculate the **proportional differences** (bone length and thickness) between the avatar and the cloth's original armature.
        2.  Apply these calculated scales **directly to the cloth prefab's bone transforms** as a one-time setup step.
- [x] **Automate `MA Merge Armature` Setup**:
    - [x] The "Calculate Scale" button now automatically adds the `MA Merge Armature` component to the cloth.
    - [x] It ensures the `Match Bone Scale` option is enabled, allowing MA to handle all future scaling adjustments (e.g., from avatar height sliders) non-destructively at build time.
- [x] **Deprecate `Fit Bones` Feature**:
    - [x] Manual bone parenting is now fully delegated to `MA Merge Armature`, making the `Fit Bones` button obsolete.

### Phase 3: Advanced Fitting Features

- [ ] **Enhance Proportional Scaling with Multi-Mode System**:
    - [x] **High-Precision Mode**: Add an optional "Source Avatar" field. If provided, the tool calculates the **true proportional difference** by directly comparing the source and target avatars for maximum accuracy.
    - [ ] **Ghost Avatar Estimation Mode**: If the source avatar is not provided, implement a new feature to **estimate the source avatar's body shape**.
    - [ ] **Identify Hard Parts**: Implement a hybrid system to distinguish rigid parts (e.g., buckles, armor) from soft cloth.
        - [x] **Step 1 (Auto-Detect)**: Automatically identify vertex groups that are weighted 100% to a single bone, as these are likely rigid.
        - [ ] **Step 2 (User Confirmation)**: Present the materials used by these candidate groups to the user, allowing them to confirm which ones are actually hard surfaces.
    - [ ] **Generate Ghost Mesh**: Create a "ghost" mesh by moving the vertices of soft parts inwards along their normals. Vertices belonging to confirmed hard parts will not be moved.
    - [ ] This ghost is then used for a highly accurate comparison.
    - [ ] **Fallback Mode**: As a simple alternative, continue to support direct comparison between the target avatar's bones and the cloth's bones.

These features provide value beyond what Modular Avatar offers natively.

- [x] **Advanced Mesh Deformation**:
    - [x] A system to directly modify the clothing mesh using control anchors to fix complex fitting issues, especially where proportions differ significantly (e.g., lower body). This remains a core, unique feature of VRCloth-Fitter.
- [x] **Blendshape Sync Helper**:
    - [x] A UI to easily map blendshapes and automatically configure the `MA Blendshape Sync` component.
- [x] **Material & Shader Utility**:
    - [x] A utility to batch-convert materials to a target shader.

### Phase 4: Community & Future Features

- [x] **Preset Import/Export**:
    - [x] A system to save and load fitting settings (mesh deformation anchors, etc.) as JSON files.
- [ ] **Improve Preset System with GUIDs**:
    - [ ] Enhance presets to use avatar Prefab GUIDs for more reliable, automatic avatar detection.

---

*This roadmap is subject to change based on community feedback and development progress.*
