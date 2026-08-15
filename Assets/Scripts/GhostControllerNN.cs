
using UnityEngine;

/// <summary>
/// Neural-network-driven ghost controller. Uses Unity Sentis to run a small
/// MLP (trained by train_ghost_ai.py) that picks a movement direction each
/// time the ghost reaches a grid intersection. Drop-in alternative to
/// GhostController.cs -- same public shape, same prefab wiring.
///
/// Requires:
///   1. The "Sentis" package installed via Package Manager
///      (Window -> Package Manager -> Add package by name -> com.unity.sentis)
///   2. ghost_ai.onnx imported into the project (drag into Assets/Models/)
///      Unity will auto-convert it into a ModelAsset.
///   3. That ModelAsset assigned to the `modelAsset` field below.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GhostControllerNN : MonoBehaviour, IGhost
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
    private static readonly Vector2[] Directions =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

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
        currentDirection = Directions[Random.Range(0, Directions.Length)];

        if (modelAsset != null)
        {
            runtimeModel = Unity.InferenceEngine.ModelLoader.Load(modelAsset);
            worker = new Unity.InferenceEngine.Worker(runtimeModel, backend);
        }
        else
        {
            Debug.LogWarning($"{name}: No ModelAsset assigned to GhostControllerNN -- falling back to random movement.");
        }
    }

    void OnDestroy()
    {
        worker?.Dispose(); // Sentis workers hold native/GPU resources; always dispose
    }

    void FixedUpdate()
    {
        if (IsNearGridCenter())
        {
            SnapToGrid();
            ChooseDirectionNN();
        }
        rb.MovePosition(rb.position + currentDirection * moveSpeed * Time.fixedDeltaTime);

        if (spriteRenderer != null)
            spriteRenderer.color = GameManager.Instance.PowerModeActive ? frightenedColor : normalColor;
    }

    void ChooseDirectionNN()
    {
        if (worker == null || GameManager.Instance.PlayerTransform == null)
        {
            ChooseDirectionFallback();
            return;
        }

        using Unity.InferenceEngine.Tensor<float> input = BuildInputTensor();
        worker.Schedule(input);

        using Unity.InferenceEngine.Tensor<float> output = worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>;
        using Unity.InferenceEngine.Tensor<float> cpuOutput = output.ReadbackAndClone(); // pull result off GPU/async queue to CPU

        // Mask out illegal moves (walls, and reversing unless it's a dead end)
        // by driving their score to -infinity before picking the argmax.
        float[] logits = cpuOutput.DownloadToArray();
        int opposite = currentDirIndex >= 0 ? OppositeOf(currentDirIndex) : -1;

        int bestIdx = -1;
        float bestScore = float.NegativeInfinity;
        bool anyValid = false;

        for (int i = 0; i < 4; i++)
        {
            if (i == opposite) continue;
            if (IsBlocked(Directions[i])) continue;

            anyValid = true;
            if (logits[i] > bestScore)
            {
                bestScore = logits[i];
                bestIdx = i;
            }
        }

        if (!anyValid)
        {
            // dead end -- forced reverse
            bestIdx = opposite >= 0 ? opposite : Random.Range(0, 4);
        }

        currentDirIndex = bestIdx;
        currentDirection = Directions[bestIdx];
    }

    Unity.InferenceEngine.Tensor<float> BuildInputTensor()
    {
        // Must match encode_state() in train_ghost_ai.py exactly:
        // [canUp, canDown, canLeft, canRight, dx, dy, powerFlag, dirOneHot x4]
        Vector2 player = GameManager.Instance.PlayerTransform.position;
        bool power = GameManager.Instance.PowerModeActive;

        float[] data = new float[11];
        for (int i = 0; i < 4; i++)
            data[i] = IsBlocked(Directions[i]) ? 0f : 1f;

        // Unity world-space "up" is +Y; keep this sign convention consistent
        // with how you generated the training maze coordinates.
        data[4] = (player.x - rb.position.x) / 15f;  // COLS from MazeGenerator
        data[5] = (player.y - rb.position.y) / 11f;  // ROWS from MazeGenerator
        data[6] = power ? 1f : 0f;

        if (currentDirIndex >= 0)
            data[7 + currentDirIndex] = 1f;

        return new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, 11), data);
    }

    void ChooseDirectionFallback()
    {
        // Used only if no model is assigned -- keeps the ghost from freezing.
        var candidates = new System.Collections.Generic.List<Vector2>();
        foreach (var dir in Directions)
            if (!IsBlocked(dir)) candidates.Add(dir);

        currentDirection = candidates.Count > 0
            ? candidates[Random.Range(0, candidates.Count)]
            : -currentDirection;
    }

    static int OppositeOf(int dirIndex) => dirIndex switch
    {
        0 => 1, // up -> down
        1 => 0, // down -> up
        2 => 3, // left -> right
        3 => 2, // right -> left
        _ => -1
    };

    bool IsBlocked(Vector2 dir)
    {
        Vector2 checkPos = rb.position + dir;
        return Physics2D.OverlapCircle(checkPos, 0.2f, wallLayer) != null;
    }

    bool IsNearGridCenter()
    {
        float dx = Mathf.Abs(Mathf.Round(rb.position.x) - rb.position.x);
        float dy = Mathf.Abs(Mathf.Round(rb.position.y) - rb.position.y);
        return dx < 0.05f && dy < 0.05f;
    }

    void SnapToGrid()
    {
        rb.position = new Vector2(Mathf.Round(rb.position.x), Mathf.Round(rb.position.y));
    }

    public void GetEaten()
    {
        rb.position = startPosition;
        currentDirIndex = -1;
    }
}
