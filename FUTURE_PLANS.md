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

## Community Growth: The Preset Hub Concept

To make preset sharing seamless, a future goal is to create an online database of presets that the tool can sync with directly. This would eliminate the need for users to manually download and manage JSON files.

### High-Level Architecture

-   **Web Backend**: A server with a database and API to store and manage preset data (JSONs, metadata like avatar GUIDs, tags, ratings, etc.).
-   **Unity Editor Client**: A UI integrated into the VRCloth-Fitter inspector that communicates with the backend to search, download, and upload presets.

### Potential Technical Stack

-   **Backend**: Node.js (Express), Python (FastAPI), Ruby on Rails, etc.
-   **Database**: PostgreSQL, MySQL, SQLite, etc.
-   **Hosting**: Heroku, Vercel, AWS, GCP, or other cloud providers.

### Draft API Endpoints (v1)

-   `GET /presets/search?avatarGuid=...&clothName=...`: Search for presets.
-   `POST /presets/upload`: Upload a new preset.

### A Call for Collaboration

This is a significant undertaking that requires web development expertise. If you are a web developer interested in making this platform a reality, your contributions would be highly welcome! Please open an issue on GitHub to discuss this further.
