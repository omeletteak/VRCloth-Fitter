# VRCloth-Fitter Development Roadmap

This document outlines the development plan and feature history for VRCloth-Fitter.

## Core Philosophy

The goal of this tool is to provide a robust, intuitive suite of features for avatar outfit customization. Our core philosophy is to **avoid reinventing the wheel** by leveraging the powerful, industry-standard features of **Modular Avatar (MA)** wherever possible.

VRCloth-Fitter's primary role is to act as an **intelligent setup utility** for MA components, providing advanced calculations and automation that MA itself does not offer, ensuring a seamless and non-destructive workflow for the user.

### Phase 1: Proof of Concept (Legacy)

- [x] **Initial Implementation**: Developed a standalone, window-based tool with custom NDMF passes for bone scaling and mesh deformation.

### Phase 2: Architectural Refactor - Deep Synergy with Modular Avatar (Final)

This phase marks the definitive architecture, deeply integrating with Modular Avatar's extension points for a fully non-destructive build process, while providing immediate visual feedback via a temporary, destructive preview.

- [x] **UI/UX Overhaul**:
    - [x] Migrated to a modern, component-based workflow (`VRClothFitter` component and custom editor).
- [x] **Adopt `MergeArmatureHook` for Scaling**:
    - [x] Create a new component/script that implements `MergeArmatureHook`.
    - [x] This hook will read scale data from `VRClothFitterScalingData` at build time and apply it to the bones on-the-fly, ensuring a **completely non-destructive build**.
- [x] **Reinstate Data Component**:
    - [x] Re-introduce `VRClothFitterScalingData.cs` to store the calculated bone scale ratios as pure data, without modifying the prefab.
- [x] **Implement a "Destructive Preview" System**:
    - [x] Add "Start Preview" and "Stop Preview" buttons to the editor.
    - [x] "Start Preview" will temporarily apply the scales from the data component directly to the cloth prefab's bones for instant visual feedback.
    - [x] "Stop Preview" will revert the bones to their original scales, ensuring the prefab remains clean.
- [x] **Redefine "Calculate Scale" Feature**:
    - [x] Its role is now to calculate the proportional differences and **save the results to the `VRClothFitterScalingData` component**.
- [x] **Deprecate `Fit Bones` Feature**:
    - [x] Bone parenting is fully delegated to `MA Merge Armature`.

### Phase 3: Advanced Fitting Features

- [x] **Advanced Mesh Deformation**
- [x] **Blendshape Sync Helper**
- [x] **Material & Shader Utility**

### Phase 4: Community & Future Features

- [x] **Preset Import/Export**
- [ ] **Improve Preset System with GUIDs**
- [ ] **Enhance Proportional Scaling with Multi-Mode System** (High-Precision, Ghost Avatar)

---

*This roadmap is subject to change based on community feedback and development progress.*