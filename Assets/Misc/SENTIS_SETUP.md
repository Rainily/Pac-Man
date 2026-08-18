# Neural-Network Ghost AI — Setup Guide

This adds a Sentis-driven ghost brain (`GhostControllerNN.cs`) alongside your
existing heuristic one (`GhostController.cs`). You can mix and match — e.g.
two heuristic ghosts and two neural ones, or swap all of them over.

## How it works

1. `train_ghost_ai.py` simulates the maze in Python, uses the *same*
   chase/flee distance logic your `GhostController.ChooseDirection()`
   already uses as a "teacher," and trains a small MLP (11 inputs → 32 →
   32 → 4 outputs) to imitate it via supervised learning.
2. The trained network is exported to `ghost_ai.onnx`.
3. Unity imports that ONNX file and runs it every time a ghost reaches a
   grid intersection, via the Sentis `Worker` API, in place of the
   hand-coded scoring loop.

Because the network starts by imitating your existing heuristic, ghost
behavior should look nearly identical at first — the value of this setup
is that you now have a differentiable model you can keep training (e.g.
via reinforcement learning against real player data) instead of hand-
tuning more `if` statements.

---

## 1. Train the model (outside Unity)

Requires Python 3.9+ with PyTorch:

```bash
pip install torch onnx
python train_ghost_ai.py
```

This prints validation accuracy per epoch (should climb into the 90%+
range — the task is simple enough that near-perfect imitation is
expected) and writes `ghost_ai.onnx` in the same folder.

> **Important:** if you change the maze layout in `MazeGenerator.cs`,
> copy the updated `layout` array into the `LAYOUT` constant at the top
> of `train_ghost_ai.py` and retrain — the network has memorized this
> specific maze's geometry implicitly through the training distribution,
> not explicitly, so a very different maze shape benefits from retraining.

## 2. Install the Sentis package in Unity

**Window → Package Manager → "+" → Add package by name…**
```
com.unity.sentis
```
(If your Unity version shows it as "Inference Engine" in the package
list, that's the same package — Unity renamed and then renamed back.)

## 3. Import the trained model

1. Drag `ghost_ai.onnx` into `Assets/Models/` in your project.
2. Unity will automatically import it as a `ModelAsset`. Select it in the
   Project window to confirm it shows input/output tensor info in the
   Inspector.

## 4. Wire up the ghost prefab

1. Add `GhostControllerNN.cs` to your Ghost prefab (or duplicate the
   prefab as "Ghost_NN" if you want to keep some heuristic ghosts too).
2. If the prefab still has the old `GhostController` component, remove
   it — don't run both on the same object.
3. In the Inspector:
   - **Model Asset** → drag in the imported `ghost_ai` model
   - **Backend** → `CPU` is fine for a model this small; `GPUCompute` also works
   - **Wall Layer** → same `Walls` layer as before
   - **Sprite Renderer** → assign the ghost's own renderer

4. Everything else (tags, colliders, `MazeGenerator` wiring) stays the
   same as in the original setup guide — `MazeGenerator` just needs
   whichever ghost prefab you want spawned dragged into its **Ghost
   Prefab** field.

## 5. Press Play

Ghosts should chase/flee just like before, now driven by the network.
Check the Console for the fallback warning ("No ModelAsset assigned") if
nothing seems to be happening — that means the model reference didn't
get set in step 4.

---

## Where to take it from here

- **Different personalities per ghost:** train 2–3 variants (e.g. train
  one teacher that's more aggressive, one that's more erratic/random)
  and assign a different `ModelAsset` to each ghost instance.
- **Reinforcement learning instead of imitation:** once the imitation
  baseline works end-to-end, you can replace the supervised loss in
  `train_ghost_ai.py` with a proper RL loop (e.g. via `ml-agents` or a
  custom Gym-style environment) so ghosts discover strategies beyond
  what the heuristic teacher knew.
- **Bigger input window:** the current 11-float input only looks at the
  four adjacent cells. Feeding a wider local patch (e.g. a 5×5 grid
  flattened to 25 floats) would let the network anticipate dead ends and
  loops further ahead, at the cost of a slightly bigger network.
