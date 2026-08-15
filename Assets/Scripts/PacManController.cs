using UnityEngine;

/// <summary>
/// Grid-based movement for Pac-Man. Attach to the Player prefab
/// along with a CircleCollider2D (isTrigger) and Rigidbody2D (kinematic).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PacManController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public LayerMask wallLayer;

    private Vector2 currentDirection = Vector2.zero;
    private Vector2 queuedDirection = Vector2.zero;
    private Rigidbody2D rb;
    private float cellSize = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    void Update()
    {
        ReadInput();
    }

    void FixedUpdate()
    {
        TryChangeDirection();
        Move();
    }

    void ReadInput()
    {
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) queuedDirection = Vector2.up;
        else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) queuedDirection = Vector2.down;
        else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) queuedDirection = Vector2.left;
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) queuedDirection = Vector2.right;
        else if (Input.GetKey(KeyCode.Space))
        {
            GameManager.Instance.ReloadScene();
            Time.timeScale = 1f;
        }
    }

    // Only allow turning when close to being aligned with the grid,
    // and only if the new direction isn't blocked by a wall.
    void TryChangeDirection()
    {
        if (queuedDirection == Vector2.zero) return;

        bool aligned = IsNearGridCenter();
        if (aligned)
        {
            SnapToGrid();
            if (!IsBlocked(queuedDirection))
            {
                currentDirection = queuedDirection;
            }
        }
    }

    void Move()
    {
        if (currentDirection == Vector2.zero) return;

        if (IsBlocked(currentDirection) && IsNearGridCenter())
        {
            currentDirection = Vector2.zero;
            return;
        }

        rb.MovePosition(rb.position + currentDirection * moveSpeed * Time.fixedDeltaTime);
    }

    bool IsBlocked(Vector2 dir)
    {
        Vector2 checkPos = rb.position + dir * cellSize;
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

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Pellet"))
        {
            GameManager.Instance.CollectPellet(10);
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("PowerPellet"))
        {
            GameManager.Instance.CollectPellet(50);
            GameManager.Instance.ActivatePowerMode();
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("Ghost"))
        {
            IGhost ghost = other.GetComponent<IGhost>();
            if (GameManager.Instance.PowerModeActive)
            {
                ghost.GetEaten();
                GameManager.Instance.CollectPellet(200);
            }
            else
            {
                GameManager.Instance.PlayerCaught();
            }
        }
    }
}
