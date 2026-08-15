using UnityEngine;

/// <summary>
/// Simple ghost AI: picks a random valid direction at each intersection,
/// with a bias toward moving closer to the player. When power mode is
/// active, it flees instead. Attach to Ghost prefab with Rigidbody2D
/// (kinematic) + CircleCollider2D (isTrigger).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GhostController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public LayerMask wallLayer;
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.red;
    public Color frightenedColor = Color.blue;

    private Rigidbody2D rb;
    private Vector2 currentDirection;
    private Vector2 startPosition;
    private static readonly Vector2[] Directions =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        startPosition = rb.position;
        currentDirection = Directions[Random.Range(0, Directions.Length)];
    }

    void FixedUpdate()
    {
        if (IsNearGridCenter())
        {
            SnapToGrid();
            ChooseDirection();
        }
        rb.MovePosition(rb.position + currentDirection * moveSpeed * Time.fixedDeltaTime);

        if (spriteRenderer != null)
            spriteRenderer.color = GameManager.Instance.PowerModeActive ? frightenedColor : normalColor;
    }

    void ChooseDirection()
    {
        Vector2 opposite = -currentDirection;
        var candidates = new System.Collections.Generic.List<Vector2>();

        foreach (var dir in Directions)
        {
            if (dir == opposite) continue; // avoid reversing unless forced
            if (!IsBlocked(dir)) candidates.Add(dir);
        }

        if (candidates.Count == 0)
        {
            // dead end, forced to reverse
            currentDirection = opposite;
            return;
        }

        Transform player = GameManager.Instance.PlayerTransform;
        bool fleeing = GameManager.Instance.PowerModeActive;

        Vector2 best = candidates[Random.Range(0, candidates.Count)];
        if (player != null)
        {
            float bestScore = fleeing ? float.MinValue : float.MaxValue;
            foreach (var dir in candidates)
            {
                Vector2 nextPos = rb.position + dir;
                float dist = Vector2.Distance(nextPos, player.position);
                // 70% chance to use chase/flee logic, 30% random for variety
                if (Random.value < 0.7f)
                {
                    if (fleeing && dist > bestScore) { bestScore = dist; best = dir; }
                    else if (!fleeing && dist < bestScore) { bestScore = dist; best = dir; }
                }
            }
        }

        currentDirection = best;
    }

    bool IsBlocked(Vector2 dir)
    {
        Vector2 checkPos = rb.position + dir;
        Collider2D hit = Physics2D.OverlapCircle(checkPos, 0.2f, wallLayer);
        return hit != null;
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
    }
}
