# Contributing to VRCloth-Fitter

Thank you for your interest in contributing to VRCloth-Fitter! We welcome bug reports, feature requests, and pull requests.

## Development Environment Setup

1.  **Unity Version**: This project is developed with Unity `2022.3.22f1`. Please use the same version to ensure compatibility.
2.  **Required Packages**: Make sure you have the following packages installed in your project, preferably via the VRChat Creator Companion:
    - `nadena.dev.ndmf`
    - `nadena.dev.modular-avatar`

## Coding Style

Please follow the existing coding style for consistency. Key points include:
- Use `camelCase` for private fields and local variables.
- Use `PascalCase` for public fields, properties, and methods.
- Keep lines reasonably short (around 120 characters).

## Pull Request Process

1.  **Discuss First**: Before starting work on a new feature, please open an issue on GitHub to discuss your ideas. This helps ensure your work aligns with the project's goals. For bug fixes, you can submit a pull request directly.
2.  **Create a Fork**: Fork the repository and create a new branch for your changes.
3.  **Submit a Pull Request**: Once your changes are ready, submit a pull request with a clear description of what you've done.

## Project Structure

-   **`/Assets/VRCloth-Fitter/Core`**: Pure C# math and data types (e.g., `BodyCapsule`, `PenetrationHit`) with no scene dependencies. Covered by EditMode tests.
-   **`/Assets/VRCloth-Fitter/Runtime`**: Contains `MonoBehaviour` components that are attached to GameObjects (e.g., `VRClothFitter.cs`). These hold the data.
-   **`/Assets/VRCloth-Fitter/Editor`**: Contains `Editor` scripts: custom inspectors, the fitting pipeline, and scene-view visualization.
-   **`/Assets/VRCloth-Fitter/Tests`**: EditMode tests.
-   **`/Assets/VRCloth-Fitter/Scripts`**: Miscellaneous scripts pending reorganization into the folders above.

See [docs/DESIGN.md](docs/DESIGN.md) for the design rationale — in particular the **No Cache** principle (never persist or export data that could reconstruct an avatar's body shape), which all contributions must respect.

We look forward to your contributions!
