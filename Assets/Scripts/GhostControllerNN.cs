using System;

using UnityEngine;

/// <summary>
/// Neural-network-driven ghost controller. Uses Unity Sentis to run a small
/// MLP (trained by train_ghost_ai.py) that picks a movement direction each
/// time the ghost reaches a grid intersection.
///
/// This class is split into:
///   - MonoBehaviour lifecycle + Sentis plumbing (Awake, InitializeModel,
///     ChooseDirectionNN, RunInference) -- exercised by PlayMode tests and
///     EditMode tests that provide a real ModelAsset.
///   - Pure, static, side-effect-free logic (EncodeState, SelectDirection)
///     -- exercised directly by fast EditMode unit tests with no Sentis
///     dependency at all.
///
/// Requires:
///   1. The "Sentis" package (com.unity.sentis) installed via Package Manager.
///   2. ghost_ai.onnx imported into the project (Unity auto-converts it to
///      a ModelAsset).
///   3. That ModelAsset assigned to the `modelAsset` field below.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GhostControllerNN : MonoBehaviour, IGhost, IDisposable
{
    [Header("Sentis Model")]
    public Unity.InferenceEngine.ModelAsset modelAsset;
    public Unity.InferenceEngine.BackendType backend = Unity.InferenceEngine.BackendType.CPU; // GPUCompute also works; CPU is plenty for a model this small

    [Header("Movement")]
    public float moveSpeed = 4f;
    public LayerMask wallLayer;
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.red;
    public Color frightenedColor = Color.blue;

    // Direction order MUST match train_ghost_ai.py: 0=up,1=down,2=left,3=right
    public static readonly Vector2[] Directions =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    /// <summary>True once ModelLoader.Load has succeeded for the current modelAsset.</summary>
    public bool IsModelLoaded { get; private set; }

    /// <summary>True once a Worker has been created for the loaded model.</summary>
    public bool HasWorker => worker != null;

    private Rigidbody2D rb;
    private Unity.InferenceEngine.Worker worker;
    private Unity.InferenceEngine.Model runtimeModel;
    private int currentDirIndex = -1; // -1 = none yet
    private Vector2 currentDirection;
    private Vector2 startPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        startPosition = rb.position;
        currentDirection = Directions[UnityEngine.Random.Range(0, Directions.Length)];

        InitializeModel();
    }

    void OnDestroy()
    {
        Dispose();
    }

    public void Dispose()
    {
        worker?.Dispose(); // Sentis workers hold native/GPU resources; always dispose
        worker = null;
        IsModelLoaded = false;
    }

    /// <summary>
    /// Loads the model and creates the inference worker. Wrapped in try/catch
    /// so a missing, corrupt, or incompatible asset degrades to fallback
    /// movement instead of crashing ghost spawn. Safe to call more than once
    /// (e.g. from tests, or if you swap modelAsset at runtime) -- any
    /// existing worker is disposed first.
    /// </summary>
    public void InitializeModel()
    {
        Dispose();

        if (modelAsset == null)
        {
            Debug.LogWarning($"{name}: No ModelAsset assigned to GhostControllerNN -- falling back to random movement.");
            return;
        }

        try
        {
            runtimeModel = Unity.InferenceEngine.ModelLoader.Load(modelAsset);
            if (runtimeModel == null)
                throw new InvalidOperationException("ModelLoader.Load returned null.");

            worker = new Unity.InferenceEngine.Worker(runtimeModel, backend);
            IsModelLoaded = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"{name}: Failed to load Sentis model ({ex.GetType().Name}: {ex.Message}). Falling back to random movement.");
            worker = null;
            IsModelLoaded = false;
        }
    }

    void FixedUpdate()
    {
        if (IsNearGridCenter())
        {
            SnapToGrid();
            ChooseDirectionNN();
        }
        rb.MovePosition(rb.position + currentDirection * moveSpeed * Time.fixedDeltaTime);

        if (spriteRenderer != null && GameManager.Instance != null)
            spriteRenderer.color = GameManager.Instance.PowerModeActive ? frightenedColor : normalColor;
    }

    void ChooseDirectionNN()
    {
        if (!IsModelLoaded || worker == null || GameManager.Instance == null || GameManager.Instance.PlayerTransform == null)
        {
            ChooseDirectionFallback();
            return;
        }

        float[] logits;
        try
        {
            logits = RunInference();
        }
        catch (Exception ex)
        {
            Debug.LogError($"{name}: Inference failed ({ex.GetType().Name}: {ex.Message}). Falling back to random movement this step.");
            ChooseDirectionFallback();
            return;
        }

        bool[] validMoves = new bool[4];
        for (int i = 0; i < 4; i++)
            validMoves[i] = !IsBlocked(Directions[i]);

        int opposite = currentDirIndex >= 0 ? OppositeOf(currentDirIndex) : -1;
        int chosen = SelectDirection(logits, validMoves, opposite);

        if (chosen < 0)
        {
            // Malformed model output or genuinely no valid move -- don't
            // trust the network's decision this step.
            ChooseDirectionFallback();
            return;
        }

        currentDirIndex = chosen;
        currentDirection = Directions[chosen];
    }

    /// <summary>Runs one forward pass and returns the raw 4-length logit array.</summary>
    private float[] RunInference()
    {
        using Unity.InferenceEngine.Tensor<float> input = BuildInputTensor();
        worker.Schedule(input);

        using Unity.InferenceEngine.Tensor<float> output = worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>;
        if (output == null)
            throw new InvalidOperationException("Worker output was null or not a Tensor<float>.");

        using Unity.InferenceEngine.Tensor<float> cpuOutput = output.ReadbackAndClone(); // pull result off GPU/async queue to CPU
        return cpuOutput.DownloadToArray();
    }

    private Unity.InferenceEngine.Tensor<float> BuildInputTensor()
    {
        Vector2 player = GameManager.Instance.PlayerTransform.position;
        bool power = GameManager.Instance.PowerModeActive;

        bool[] canMove = new bool[4];
        for (int i = 0; i < 4; i++)
            canMove[i] = !IsBlocked(Directions[i]);

        // Unity world-space "up" is +Y; keep this sign convention consistent
        // with how the training maze coordinates were generated.
        float dx = (player.x - rb.position.x) / 15f; // COLS from MazeGenerator
        float dy = (player.y - rb.position.y) / 11f; // ROWS from MazeGenerator

        float[] data = EncodeState(canMove, dx, dy, power, currentDirIndex);
        return new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, 11), data);
    }

    // -------------------------------------------------------------------
    // Pure logic below. No MonoBehaviour state, no Sentis, no GameObject
    // access -- fully unit-testable in isolation.
    // -------------------------------------------------------------------

    /// <summary>
    /// Encodes ghost/player state into the 11-float input vector the
    /// network expects: [canUp, canDown, canLeft, canRight, dx, dy,
    /// powerFlag, dirOneHot(4)]. Must exactly match encode_state() in
    /// train_ghost_ai.py.
    /// </summary>
    public static float[] EncodeState(bool[] canMove, float dx, float dy, bool powerMode, int currentDirIndex)
    {
        if (canMove == null || canMove.Length != 4)
            throw new ArgumentException("canMove must have exactly 4 elements (up, down, left, right).", nameof(canMove));

        float[] data = new float[11];
        for (int i = 0; i < 4; i++)
            data[i] = canMove[i] ? 1f : 0f;

        data[4] = dx;
        data[5] = dy;
        data[6] = powerMode ? 1f : 0f;

        if (currentDirIndex >= 0 && currentDirIndex < 4)
            data[7 + currentDirIndex] = 1f;
        // an out-of-range index (including -1, meaning "no direction yet")
        // is intentionally ignored, leaving the one-hot block all-zero.

        return data;
    }

    /// <summary>
    /// Picks the highest-scoring walkable, non-reversing direction from the
    /// model's raw logits. Returns -1 if the input is malformed (wrong
    /// array length, null) or if there's genuinely no valid move -- callers
    /// should treat -1 as "don't trust this, fall back."
    ///
    /// NaN/Infinity entries (e.g. from a corrupted or diverged model) are
    /// skipped rather than allowed to win by comparison quirks.
    /// </summary>
    public static int SelectDirection(float[] logits, bool[] validMoves, int oppositeIndex)
    {
        if (logits == null || logits.Length != 4 || validMoves == null || validMoves.Length != 4)
            return -1;

        int bestIdx = -1;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < 4; i++)
        {
            if (i == oppositeIndex) continue;
            if (!validMoves[i]) continue;
            if (float.IsNaN(logits[i]) || float.IsInfinity(logits[i])) continue;

            if (logits[i] > bestScore)
            {
                bestScore = logits[i];
                bestIdx = i;
            }
        }

        if (bestIdx == -1 && oppositeIndex >= 0 && oppositeIndex < 4 && validMoves[oppositeIndex])
        {
            bestIdx = oppositeIndex; // dead end: forced reverse is the only option
        }

        return bestIdx;
    }

    public static int OppositeOf(int dirIndex) => dirIndex switch
    {
        0 => 1, // up -> down
        1 => 0, // down -> up
        2 => 3, // left -> right
        3 => 2, // right -> left
        _ => -1
    };

    // -------------------------------------------------------------------

    private void ChooseDirectionFallback()
    {
        // Used whenever the model isn't available or its output can't be
        // trusted -- keeps the ghost moving instead of freezing.
        var candidates = new System.Collections.Generic.List<Vector2>();
        foreach (var dir in Directions)
            if (!IsBlocked(dir)) candidates.Add(dir);

        currentDirection = candidates.Count > 0
            ? candidates[UnityEngine.Random.Range(0, candidates.Count)]
            : -currentDirection;
    }

    private bool IsBlocked(Vector2 dir)
    {
        Vector2 checkPos = rb.position + dir;
        return Physics2D.OverlapCircle(checkPos, 0.2f, wallLayer) != null;
    }

    private bool IsNearGridCenter()
    {
        float dx = Mathf.Abs(Mathf.Round(rb.position.x) - rb.position.x);
        float dy = Mathf.Abs(Mathf.Round(rb.position.y) - rb.position.y);
        return dx < 0.05f && dy < 0.05f;
    }

    private void SnapToGrid()
    {
        rb.position = new Vector2(Mathf.Round(rb.position.x), Mathf.Round(rb.position.y));
    }

    public void GetEaten()
    {
        rb.position = startPosition;
        currentDirIndex = -1;
    }
}
