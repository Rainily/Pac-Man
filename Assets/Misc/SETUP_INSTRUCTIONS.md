# Simplified Pac-Man — Unity Setup Guide

These 6 scripts give you a playable, grid-based Pac-Man clone: a maze built
from a text layout, Pac-Man movement, simple ghost AI (chase + frighten),
pellets, score/lives, and win/lose states.

**Scripts included:**
- `MazeGenerator.cs` — builds the maze from a text grid
- `PacManController.cs` — player movement + input
- `GhostController.cs` — ghost AI
- `GameManager.cs` — score, lives, power mode, win/lose
- `CameraFraming.cs` — optional, auto-fits camera to the maze

---

## 1. Create the project
1. Open Unity Hub → New Project → **2D (URP or Built-in, either works)**.
2. Copy all `.cs` files from this package into `Assets/Scripts/`.
3. Import **TextMeshPro** (Window → TextMeshPro → Import TMP Essential Resources) — needed for `GameManager`'s UI text.

## 2. Set up tags and layers
Go to **Edit → Project Settings → Tags and Layers**:
- Add Tags: `Player`, `Pellet`, `PowerPellet`, `Ghost`
- Add Layer: `Walls`

## 3. Build the prefabs
Create each as a GameObject, drag into `Assets/Prefabs/` to make it a prefab, then delete it from the scene (MazeGenerator will instantiate them).

**Wall**
- 2D Object → Square Sprite, scale 1×1
- Add `Box Collider 2D`
- Set Layer = `Walls`
- Color it blue

**Pellet**
- 2D Object → Circle Sprite, scale ~0.25×0.25
- Add `Circle Collider 2D`, check **Is Trigger**
- Tag = `Pellet`, color yellow/white

**PowerPellet**
- Same as Pellet but larger (~0.5×0.5), Tag = `PowerPellet`

**Player (Pac-Man)**
- 2D Object → Circle Sprite (or a yellow "pac" sprite), scale 0.8×0.8
- Add `Rigidbody2D` (Body Type = Kinematic)
- Add `Circle Collider 2D`, check **Is Trigger**
- Tag = `Player`
- Add component `PacManController`
  - Set **Wall Layer** to the `Walls` layer in the Inspector

**Ghost**
- 2D Object → Square or Circle Sprite, scale 0.8×0.8, color red
- Add `Rigidbody2D` (Body Type = Kinematic)
- Add `Circle Collider 2D`, check **Is Trigger**
- Tag = `Ghost`
- Add component `GhostController`
  - Set **Wall Layer** to `Walls`
  - Assign its own `SpriteRenderer` to the **Sprite Renderer** field

## 4. Set up the maze
1. Create an empty GameObject named `MazeGenerator`.
2. Add the `MazeGenerator` component.
3. Drag the Wall, Pellet, PowerPellet, Player, and Ghost prefabs into their matching slots.
4. The default layout is a 15×11 grid defined directly in `MazeGenerator.cs` (edit the `layout` array to design your own — `#` wall, `.` pellet, `o` power pellet, `P` player start, `G` ghost start, space = empty path). Add more `G` characters for extra ghosts.

## 5. Set up the GameManager and UI
1. Create an empty GameObject named `GameManager`, add the `GameManager` component.
2. Create a **Canvas** (UI → Canvas), then add two TextMeshPro - Text UI elements for Score and Lives, plus one for Messages (win/lose text), positioned in the corners.
3. Drag those three text objects into the `GameManager`'s Score/Lives/Message fields.

## 6. Camera
1. Select **Main Camera**, add the `CameraFraming` component (optional but recommended).
2. Match `mazeWidth`/`mazeHeight` to your layout's column/row count.

## 7. Press Play
- Move with **arrow keys** or **WASD**.
- Eating a power pellet turns ghosts blue and vulnerable for `powerModeDuration` seconds (default 8s) — touching them then sends them back to their start position and gives you 200 points.
- Getting caught outside power mode costs a life and reloads the scene.
- Clearing all pellets triggers the win state.

---

## Ideas to extend it
- Replace the single-scene reload in `GameManager.PlayerCaught()` with a proper respawn (reposition player/ghosts without reloading).
- Give each ghost a distinct personality (e.g., one always chases, one ambushes, one wanders) by tweaking the scoring logic in `GhostController.ChooseDirection()`.
- Add sprite animations and a tunnel/wraparound edge.
- Add sound effects on pellet pickup, power mode, and ghost collision.
