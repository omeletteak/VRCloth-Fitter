# Future Plans: Advanced Preset System Concept

## Core Idea

The current preset system in VRCloth-Fitter is designed to save and share adjustment data for fitting a **specific cloth** to a **specific base avatar**.

This concept takes it a step further: to create and share data representing the **body shape difference between two base avatars**, for example, between "Avatar A" and "Avatar B".

This would allow a user who does not own the original base avatar (Avatar A) to fit a cloth designed for it onto their own avatar (Avatar B), provided they have the adjustment preset for the cloth.

## Workflow Comparison

### Current Workflow

1.  User A creates and shares **[Preset A]**, the adjustment data for fitting "Cloth X" onto "Avatar: Manuka".
2.  User B, who uses "Avatar: Selestia", acquires "Cloth X".
3.  User B must create **[Preset B]** from scratch to fit "Cloth X" onto "Avatar: Selestia".

### New Concept Workflow

1.  Someone creates and shares a **[Body Shape Diff Preset: Manuka -> Selestia]**.
2.  User A creates and shares **[Cloth Preset A]** for fitting "Cloth X" onto "Avatar: Manuka".
3.  User B (using "Avatar: Selestia") acquires both **[Cloth Preset A]** and the **[Body Shape Diff Preset: Manuka -> Selestia]**.
4.  The tool automatically combines these two presets to generate the necessary adjustment data to fit "Cloth X" onto "Avatar: Selestia".

## Technical Requirements

The "diff data" required to realize this concept would consist of the following information:

1.  **Bone Differences**:
    *   The **scale ratio** (length, thickness) between corresponding bones.
    *   (Optional) The **positional and rotational offsets** of bones in their initial T-Pose.

2.  **Mesh Differences**:
    *   The **difference vectors of vertex coordinates** at representative anchor points between the two avatars.
    *   This defines a mapping to transform the body shape of one avatar to another.

## Benefits

*   **Increased Reusability of Presets**: Cloth presets created for one popular avatar could be utilized across a wide range of other avatars.
*   **Reduced User Workload**: Users would only need a "Body Shape Diff Preset" between their own avatar and another popular avatar to easily fit many clothes.
*   **Community Growth**: "Body Shape Diff Presets" themselves could become valuable, shareable assets created and distributed by the community.
