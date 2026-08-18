# Ghost AI — Automated Tests

Six test cases, covering the six points you listed, split across two Unity
Test Framework suites: fast EditMode tests for anything that's pure logic,
and PlayMode tests for anything that depends on Unity's object lifecycle
(Awake/Start actually running).

| Requirement | Suite | Test file |
|---|---|---|
| Model loads successfully | EditMode | `GhostModelLoadingTests.cs` |
| Worker is created | EditMode | `GhostModelLoadingTests.cs` |
| Exception handling | EditMode | `GhostModelLoadingTests.cs` |
| Scene initialization | PlayMode | `SceneInitializationTests.cs` |
| Output values are correct | EditMode | `GhostInferenceLogicTests.cs` |
| Invalid input handling | EditMode | `GhostInferenceLogicTests.cs` |

## Why the refactor

`GhostControllerNN.cs` was restructured (behavior unchanged) so the
decision logic is testable in isolation:

- **`InitializeModel()`** wraps `ModelLoader.Load` + `new Worker(...)` in a
  try/catch, exposes `IsModelLoaded` / `HasWorker` as public read-only
  properties, and is idempotent (safe to call repeatedly — it disposes any
  existing worker first). This is what makes the model-loading and
  exception-handling tests possible without touching private fields.
- **`EncodeState()`** and **`SelectDirection()`** are now `public static`
  and take plain data (bool arrays, floats) instead of reading
  `Rigidbody2D`/`GameManager` state directly. That means the "output
  values are correct" and "invalid input handling" tests run in
  milliseconds with zero Sentis/GameObject overhead, and can't be flaky.
- **`GhostControllerNN` now implements `IDisposable`**, and `GameManager`
  clears its own static `Instance` in `OnDestroy` — both just good hygiene
  that also happens to stop tests from leaking state into each other.

## Project setup

1. Copy `Scripts/PacManGame.asmdef` next to your other scripts in
   `Assets/Scripts/`. This gives the test assemblies something explicit to
   reference instead of the implicit `Assembly-CSharp`.
2. Copy the `Tests/EditMode/` and `Tests/PlayMode/` folders (including the
   `.asmdef` files) into your project's `Assets/Tests/` folder, preserving
   that structure:
   ```
   Assets/Tests/EditMode/PacManGame.EditModeTests.asmdef
   Assets/Tests/EditMode/GhostModelLoadingTests.cs
   Assets/Tests/EditMode/GhostInferenceLogicTests.cs
   Assets/Tests/PlayMode/PacManGame.PlayModeTests.asmdef
   Assets/Tests/PlayMode/SceneInitializationTests.cs
   ```
3. If the **Test Framework** package isn't already in your project:
   **Window → Package Manager → Unity Registry → "Test Framework" → Install.**
4. (Optional, but needed for the two model-loading tests that require a
   real asset) Run `train_ghost_ai.py`, then copy the resulting
   `ghost_ai.onnx` to:
   ```
   Assets/Tests/EditMode/Resources/TestModels/ghost_ai.onnx
   ```
   Unity auto-imports anything under a `Resources/` folder, so this makes
   it loadable via `Resources.Load<ModelAsset>("TestModels/ghost_ai")` at
   test time. If you skip this, those two tests report **Inconclusive**
   (not a failure) — everything else in the suite still runs.

## Running the tests

**Window → General → Test Runner**, then switch between the **EditMode**
and **PlayMode** tabs and click **Run All**. EditMode tests run instantly
without entering Play Mode; PlayMode tests briefly enter Play Mode to let
`Awake`/`Start` execute for real.

## What's deliberately *not* covered here

Actually simulating a Sentis `Worker.Schedule()` throwing mid-inference
(e.g. from a corrupted native backend) isn't practical to unit-test
without real hardware and a genuinely broken model — `Worker` isn't
mockable. Instead, `ChooseDirectionNN()`'s inference call is wrapped in
its own try/catch that falls back to `ChooseDirectionFallback()`
(see `GhostControllerNN.cs`), and the tests cover everything upstream and
downstream of that call: bad assets on load (`InitializeModel`), and bad
*output* values from a hypothetically-misbehaving model
(`SelectDirection`'s NaN/Infinity/wrong-length handling). Between those
two, the failure modes that matter in practice — missing model, corrupt
model, garbage output — are all exercised; only "the native inference
call itself throws mid-flight" is left as a manual/integration-test
concern.
