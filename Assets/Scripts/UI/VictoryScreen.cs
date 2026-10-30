using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// final screen: time, kills, deaths, accuracy and a joke rank
public class VictoryScreen : MonoBehaviour
{
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private string titleScene = "Title";
    [SerializeField] private AudioClip victoryClip;

    private void Start()
    {
        Time.timeScale = 1f;
        AudioManager.Play(victoryClip, 0.9f, 0f);

        float accuracy = GameStats.ShotsFired > 0 ? (float)GameStats.Hits / GameStats.ShotsFired : 0f;

        if (statsText)
        {
            statsText.text =
                $"TIME      {GameStats.FormattedTime()}\n" +
                $"KILLS     {GameStats.Kills}\n" +
                $"DEATHS    {GameStats.Deaths}\n" +
                $"ACCURACY  {accuracy * 100f:0}%\n\n" +
                "<size=60%>Press SPACE to return</size>";
        }

        if (rankText) rankText.text = GetRank(accuracy);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            SceneManager.LoadScene(titleScene);
    }

    private static string GetRank(float accuracy)
    {
        if (GameStats.Deaths == 0 && accuracy >= 0.4f) return "RANK: ARCHMAGE OF AMMUNITION";
        if (GameStats.Deaths <= 3) return "RANK: BIGFOOT WRANGLER";
        if (GameStats.Deaths <= 10) return "RANK: PORCH SITTER";
        return "RANK: COURT JESTER";
    }
}
