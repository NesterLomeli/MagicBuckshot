using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// runs each level: counts enemies, death and restart, level clear,
// the tiny freeze on every kill (hit-stop), and loading the next scene
public class GameManager : MonoBehaviour
{
    public enum State { Playing, PlayerDead, LevelCleared }

    public static GameManager Instance { get; private set; }
    public State CurrentState { get; private set; } = State.Playing;

    [Header("Level")]
    [Tooltip("Shown on screen when the level starts, e.g. \"I. THE CASTLE GATE\".")]
    [SerializeField] private string levelTitle = "I. THE CASTLE GATE";

    [Tooltip("Optional exit zone. If set, the player must walk to it after clearing the level. " +
             "If empty, the next level loads automatically.")]
    [SerializeField] private LevelExit levelExit;

    [Tooltip("Seconds before auto-advancing when there is no exit zone.")]
    [SerializeField] private float autoAdvanceDelay = 2.5f;

    [Tooltip("Scene loaded after the last level (must be in Build Settings).")]
    [SerializeField] private string victorySceneName = "Victory";

    [Tooltip("Tick this on the final level so it goes to the victory screen.")]
    [SerializeField] private bool isFinalLevel = false;

    [Header("Juice")]
    [Tooltip("Real-time seconds the game freezes when an enemy dies.")]
    [SerializeField] private float killHitStop = 0.06f;

    [Header("Sounds")]
    [SerializeField] private AudioClip levelClearClip;
    [SerializeField] private AudioClip playerDeathStingClip;
    [SerializeField] private AudioClip levelStartClip;

    [Header("Flavour text")]
    [SerializeField] private string[] deathLines =
    {
        "Ma's gonna hear about this.",
        "That knight owes me a new truck.",
        "Dang wizard. Dang portal. Dang king.",
        "Pa's shotgun deserved better.",
        "Should've stayed on the porch.",
        "Well, butter my biscuit. Try again."
    };

    [SerializeField] private string[] clearLines =
    {
        "Yeehaw. Next.",
        "Somewhere, a bard is writing a country song about this.",
        "The King's gonna need more knights."
    };

    private int enemiesAlive;
    private bool loadingScene;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;   // reset in case a freeze or bullet time got cut off by a reload
        Time.fixedDeltaTime = 0.02f;
    }

    private void Start()
    {
        HUD.Instance?.ShowLevelTitle(levelTitle);
        HUD.Instance?.SetEnemiesRemaining(enemiesAlive);
        AudioManager.Play(levelStartClip, 0.7f, 0f);
    }

    private void Update()
    {
        if (CurrentState == State.Playing)
            GameStats.RunTime += Time.deltaTime;

        if (CurrentState == State.PlayerDead && Input.GetKeyDown(KeyCode.Space))
            RestartLevel();
    }

    // every enemy calls this when it spawns so the level knows how many there are
    public void RegisterEnemy()
    {
        enemiesAlive++;
        HUD.Instance?.SetEnemiesRemaining(enemiesAlive);
    }

    public void EnemyKilled()
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);
        GameStats.Kills++;
        HUD.Instance?.SetEnemiesRemaining(enemiesAlive);
        StartCoroutine(HitStop(killHitStop));   // freeze for a split second so kills feel punchy

        if (enemiesAlive == 0 && CurrentState == State.Playing)
            OnLevelCleared();
    }

    private void OnLevelCleared()
    {
        CurrentState = State.LevelCleared;
        AudioManager.Play(levelClearClip, 0.9f, 0f);

        string line = clearLines.Length > 0 ? clearLines[Random.Range(0, clearLines.Length)] : "";

        // levels with a portal wait for you to walk in, the rest move on by themselves
        if (levelExit != null)
        {
            levelExit.Activate();
            HUD.Instance?.ShowCenterMessage($"<color=#E0B13A>QUEST COMPLETE</color>\n<size=45%>{line}\n<color=#E0B13A>Get to the portal</color></size>", 6f);
        }
        else
        {
            HUD.Instance?.ShowCenterMessage($"<color=#E0B13A>QUEST COMPLETE</color>\n<size=45%>{line}</size>", autoAdvanceDelay);
            StartCoroutine(LoadNextAfter(autoAdvanceDelay));
        }
    }

    public void PlayerKilled()
    {
        if (CurrentState != State.Playing) return;

        CurrentState = State.PlayerDead;
        GameStats.Deaths++;
        AudioManager.Play(playerDeathStingClip, 0.8f, 0f);

        string line = deathLines.Length > 0 ? deathLines[Random.Range(0, deathLines.Length)] : "";
        HUD.Instance?.ShowCenterMessage($"<color=#C83028>YOU HAVE FALLEN</color>\n<size=45%>{line}\n\n<color=#E0B13A>Press SPACE to restart</color></size>", -1f);
    }

    public void RestartLevel()
    {
        if (loadingScene) return;
        loadingScene = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadNextLevel()
    {
        if (loadingScene) return;
        loadingScene = true;

        if (isFinalLevel)
        {
            SceneManager.LoadScene(victorySceneName);
            return;
        }

        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            SceneManager.LoadScene(victorySceneName);
    }

    private IEnumerator LoadNextAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadNextLevel();
    }

    // hit-stop: basically pauses time for a moment. waits in real time so it always ends
    // then goes back to normal speed (or slow-mo if Skeeter's smoking)
    private IEnumerator HitStop(float duration)
    {
        if (duration <= 0f) yield break;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = SmokeBreak.WorldTimeScale;
    }
}
