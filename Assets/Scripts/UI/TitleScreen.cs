using UnityEngine;
using UnityEngine.SceneManagement;

// start and quit for the title menu, starting resets the stats
public class TitleScreen : MonoBehaviour
{
    [Tooltip("Scene name of level 1 (must be in Build Settings).")]
    [SerializeField] private string firstLevelScene = "01_Gate";
    [SerializeField] private AudioClip startClip;

    private bool starting;

    private void Start()
    {
        Time.timeScale = 1f;
    }

    public void StartGame()
    {
        if (starting) return;
        starting = true;

        GameStats.Reset();
        AudioManager.Play(startClip, 0.9f, 0f);
        SceneManager.LoadScene(firstLevelScene);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // Application.Quit doesn't do anything in the editor so just stop play mode
#endif
    }
}
