# VRCloth-Fitter Development Roadmap

This document outlines the development plan and feature history for VRCloth-Fitter.

## Core Philosophy

The goal of this tool is to provide a robust, intuitive suite of features for avatar outfit customization. Our core philosophy is to **avoid reinventing the wheel** by leveraging the powerful, industry-standard features of **Modular Avatar (MA)** wherever possible.

VRCloth-Fitter's primary role is to act as an **intelligent setup utility** for MA components, providing advanced features that MA itself does not offer, ensuring a seamless and non-destructive workflow for the user.

---

### Phase 1 & 2: Legacy (Bone-Based Fitting)

- [x] **Initial Implementation**: Developed systems for proportional bone scaling and anchor-based mesh deformation.
- [x] **Architectural Refactor**: Deeply integrated with Modular Avatar hooks for a non-destructive build process.
- **Outcome**: While functional for bone scaling, the mesh deformation approach proved to be unstable and fundamentally flawed, often causing mesh shrinkage. This approach is now considered **deprecated**.

---

## Phase 3: The New Foundation - Cage-Based Deformation

This phase marks a complete overhaul of the fitting strategy, moving away from programmatic deformation to a user-driven, interactive approach. The goal is to provide a tool that solves the problem of **body shape differences**, which `MA Merge Armature` does not handle.

- [ ] **Core Feature: Cage (Lattice) Deformation**
    - [ ] **Cage Generation**: Implement a feature to automatically generate a simple, configurable cage (e.g., a 3x3x3 lattice) around the target cloth mesh.
    - [ ] **Interactive Manipulation**: In the Scene View, allow the user to select and move the control points of the cage.
    - [ ] **Real-time Mesh Deformation**: As the user manipulates the cage, the enclosed cloth mesh deforms smoothly in real-time. The core logic will be based on Free-Form Deformation (FFD) or similar lattice-based algorithms.
    - [ ] **Non-destructive Workflow**: All edits will be performed on a temporary mesh instance.

- [ ] **Core Feature: Blend Shape Export**
    - [ ] Implement a function to save the result of the cage deformation as a new **Blend Shape** on the original mesh.
    - [ ] This ensures the entire process is non-destructive and integrates perfectly with Unity's standard features and other tools like Modular Avatar.

- [ ] **New Component-Based UI/UX**
    - [ ] Create a new `VRClothFitterCageDeformer` MonoBehaviour.
    - [ ] Design a custom editor for this component to manage cage generation, editing state (start/stop editing), and saving to a blend shape.
    - [ ] Deprecate the old `VRClothFitterDeformationData` and its related systems.

## Phase 4: Usability and Refinements

- [ ] **UX Enhancements**:
    - [ ] Improve the visual feedback in the Scene View (e.g., clearer handles, cage visualization).
    - [ ] Add features like cage subdivision levels and reset functionality.
- [ ] **Integration & Documentation**:
    - [ ] Create detailed documentation and tutorials explaining the new workflow, especially how it complements Modular Avatar.
    - [ ] Ensure the blend shapes created by the tool are correctly handled by MA's build process.

## Phase 5: Future Possibilities

- [ ] **Projection-Based Tools**: Explore adding simple projection tools (e.g., "Shrinkwrap" or "Conform") for handling minor clipping issues after cage deformation.
- [ ] **Symmetry Editing**: Add an option to edit the cage symmetrically.

---

*This roadmap is subject to change based on community feedback and development progress.*
