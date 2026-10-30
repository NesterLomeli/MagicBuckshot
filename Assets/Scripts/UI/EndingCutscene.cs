using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// the ending: Skeeter wakes up in the grass, freaks out that someone stole his moonshine,
// then remembers he drank it. space, enter or click skips it
public class EndingCutscene : MonoBehaviour
{
    [SerializeField] private Image fader;
    [SerializeField] private RectTransform skeeter;
    [SerializeField] private TMP_Text line;
    [SerializeField] private TMP_Text lineShadow;
    [SerializeField] private GameObject alertIcon;
    [SerializeField] private GameObject curiousIcon;
    [SerializeField] private string nextScene = "Victory";

    [Header("Sounds")]
    [SerializeField] private AudioClip birdsClip;
    [SerializeField] private AudioClip[] gruntClips;
    [SerializeField] private AudioClip[] chuckleClips;

    private Vector2 skeeterHome;
    private bool breathing = true;
    private bool leaving;

    private void Start()
    {
        Time.timeScale = 1f;
        skeeterHome = skeeter.anchoredPosition;
        SetLine("");
        ShowIcon(null);
        StartCoroutine(Play());
    }

    private void Update()
    {
        if (breathing)
            skeeter.localScale = new Vector3(1f, 1f + Mathf.Sin(Time.time * 2.2f) * 0.015f, 1f);

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            Leave();
    }

    // the whole scene beat by beat
    private IEnumerator Play()
    {
        yield return Fade(1f, 0f, 1.6f);
        AudioManager.Play(birdsClip, 0.5f, 0f);
        yield return new WaitForSeconds(1.6f);

        breathing = false;
        skeeter.localScale = Vector3.one;
        yield return Jolt(18f, 0.25f);
        Say("...hnngh?", gruntClips);
        yield return new WaitForSeconds(1.6f);

        ShowIcon(curiousIcon);
        Say("Where... where's my moonshine?", gruntClips);
        yield return LookAround(1.8f);

        ShowIcon(alertIcon);
        Say("SOMEBODY STOLE MY MOONSHINE!", gruntClips);
        yield return Shake(14f, 0.6f);
        yield return new WaitForSeconds(1.4f);

        ShowIcon(null);
        Say("...", null);
        yield return new WaitForSeconds(1.2f);
        Say("...oh. I drank it.", null);
        yield return new WaitForSeconds(1.5f);
        Say("Heh heh heh.", chuckleClips);
        yield return new WaitForSeconds(2.4f);

        Leave();
    }

    private void Leave()
    {
        if (leaving) return;
        leaving = true;
        StopAllCoroutines();
        StartCoroutine(FadeOutAndLoad());
    }

    private IEnumerator FadeOutAndLoad()
    {
        yield return Fade(fader.color.a, 1f, 0.8f);
        SceneManager.LoadScene(nextScene);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetFade(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetFade(to);
    }

    private void SetFade(float alpha)
    {
        Color c = fader.color;
        c.a = alpha;
        fader.color = c;
    }

    private IEnumerator Jolt(float height, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            skeeter.anchoredPosition = skeeterHome + Vector2.up * Mathf.Sin(t / duration * Mathf.PI) * height;
            yield return null;
        }
        skeeter.anchoredPosition = skeeterHome;
    }

    // looks side to side by flipping the sprite back and forth
    private IEnumerator LookAround(float duration)
    {
        for (float t = 0f; t < duration; t += 0.45f)
        {
            skeeter.localScale = new Vector3(1f, skeeter.localScale.y > 0 ? -1f : 1f, 1f);
            yield return new WaitForSeconds(0.45f);
        }
        skeeter.localScale = Vector3.one;
    }

    private IEnumerator Shake(float strength, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            skeeter.anchoredPosition = skeeterHome + Random.insideUnitCircle * strength;
            yield return null;
        }
        skeeter.anchoredPosition = skeeterHome;
    }

    private void Say(string text, AudioClip[] voice)
    {
        SetLine(text);
        AudioManager.PlayRandom(voice, 0.85f, 0.05f);
    }

    private void SetLine(string text)
    {
        if (line) line.text = text;
        if (lineShadow) lineShadow.text = text;
    }

    private void ShowIcon(GameObject icon)
    {
        if (alertIcon) alertIcon.SetActive(icon == alertIcon);
        if (curiousIcon) curiousIcon.SetActive(icon == curiousIcon);
    }
}
