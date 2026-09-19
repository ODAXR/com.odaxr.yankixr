# Yanki XR

An obstacle-based audio occlusion package for Unity. It bakes the acoustic effects of scene obstacles, then smoothly adjusts volume and low-pass filtering at runtime based on source and listener positions.

## Features

- Floor-based grid generation and scene previews.
- Acoustic material presets such as concrete, wood, and glass.
- Approximate sound propagation through openings and around obstacles.
- Baked data lookups without runtime raycasts.

<img width="842" height="434" alt="simulation" src="https://github.com/user-attachments/assets/762a945a-f49e-403f-9472-004bda808d65" />

## Installation

This package targets **Unity 6000.3**. Download the repository, then use **Install package from disk** in Unity Package Manager and select the `package.json` file in this folder.

## Quick Start

1. Create a data asset via **Tools > ODAXR > Yanki > Create Grid Data**.
2. Select the floor object in your scene. In **Grid Recognizer**, assign the data asset and configure the cell size and ground layer. Click **Recognize Environment & Generate GUIDs**, then **Save to YankiGridData**.
3. Add a `Collider` to each obstacle and optionally add `YankiAcousticMaterial`.
4. Open **Tools > ODAXR > Yanki > Acoustic Baker**, select the data asset and obstacle layers, then click **Bake**.
5. Add `YankiBridge` to your audio source and assign `Grid Data`, `Target Audio Source`, and `Listener Transform`. Keep the source and listener inside the grid during playback.

Rebake after changing obstacles or acoustic materials. To calculate paths through openings, the grid must cover those routes and its cells must be smaller than the openings.

## Performance

<img width="1178" height="646" alt="yanki-performance" src="https://github.com/user-attachments/assets/57728972-3ffc-4061-88d5-634df2b2d306" />


## License

[Apache 2.0](LICENSE)
