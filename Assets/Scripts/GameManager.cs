using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Singleton that tracks score, lives, and power-mode state.
/// Attach to an empty GameObject called "GameManager".
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI (assign TextMeshPro objects)")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI livesText;
    public TextMeshProUGUI messageText;

    [Header("Settings")]
    public int startingLives = 3;
    public float powerModeDuration = 8f;

    public bool PowerModeActive { get; private set; }
    public Transform PlayerTransform { get; private set; }

    private MazeGenerator mazeGenerator;
    private GameObject player;
    private GameObject[] ghosts;

    private int score = 0;
    private int lives;
    private int pelletsRemaining;
    private float powerModeTimer;
    private bool gameOver;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        mazeGenerator = GameObject.FindAnyObjectByType<MazeGenerator>();
        lives = startingLives;
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) PlayerTransform = player.transform;

        ghosts = GameObject.FindGameObjectsWithTag("Ghost");

        pelletsRemaining = GameObject.FindGameObjectsWithTag("Pellet").Length +
                            GameObject.FindGameObjectsWithTag("PowerPellet").Length;

        UpdateUI();
    }

    void Update()
    {
        if (PowerModeActive)
        {
            powerModeTimer -= Time.deltaTime;
            if (powerModeTimer <= 0f)
            {
                PowerModeActive = false;
            }
        }
    }

    public void CollectPellet(int points)
    {
        score += points;
        pelletsRemaining--;
        UpdateUI();

        if (pelletsRemaining <= 0)
        {
            WinGame();
        }
    }

    public void ActivatePowerMode()
    {
        PowerModeActive = true;
        powerModeTimer = powerModeDuration;
    }

    public void PlayerCaught()
    {
        lives--;
        UpdateUI();

        if (lives <= 0)
        {
            LoseGame();
        }
        else
        {
            // Simple reset: reload the scene. For a fuller game,
            // reposition player/ghosts instead of reloading.
            if (messageText != null) messageText.text = "Caught! Lives left: " + lives;

            // Reset player and ghosts position
            player.transform.position = mazeGenerator.PlayerStart;

            foreach(GameObject ghost in ghosts)
                ghost.transform.position = mazeGenerator.GhostStarts[0];

        }
    }

    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void WinGame()
    {
        gameOver = true;
        if (messageText != null) messageText.text = "You Win! Final Score: " + score;
        Time.timeScale = 0f;
    }

    void LoseGame()
    {
        gameOver = true;
        if (messageText != null) messageText.text = "Game Over! Final Score: " + score;
        Time.timeScale = 0f;
    }

    void UpdateUI()
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
        if (livesText != null) livesText.text = "Lives: " + lives;
    }
}
