using TMPro;
using UnityEngine;

// Skeeter's one-liners. every shot has a chance to pop a yellow line over his head with a chuckle
// or a grunt, with a cooldown so you notice it but it's not every shot
public class PlayerBarks : MonoBehaviour
{
    private TMP_Text barkText;
    private Camera cam;

    [SerializeField] private string[] lines =
    {
        "Yeehaw!",
        "Git off my land!",
        "That's for my moonshine!",
        "Tell the King I said howdy.",
        "Pa'd be proud.",
        "Magic THIS.",
        "Hold still, tin can!",
        "Buckshot beats spells.",
        "Should've knocked first.",
        "Y'all ain't so tough.",
        "That one's goin' on the wall.",
        "Witchcraft, my foot.",
    };

    [Tooltip("Chance (0-1) that a shot triggers a line, once the cooldown is over.")]
    [Range(0f, 1f)] [SerializeField] private float chancePerShot = 0.3f;
    [Tooltip("Minimum seconds between lines.")]
    [SerializeField] private float cooldown = 5f;
    [SerializeField] private float displayTime = 1.6f;
    [SerializeField] private float heightAboveHead = 0.9f;
    [SerializeField] private float riseSpeed = 0.35f;

    [Tooltip("Chuckles and grunts; one plays with each line.")]
    [SerializeField] private AudioClip[] voiceClips;

    private PlayerController player;
    private float nextBarkTime;
    private float shownAt = -99f;
    private int lastLine = -1;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    private void Start()
    {
        cam = Camera.main;
        barkText = HUD.Instance != null ? HUD.Instance.BarkText : null;
        if (barkText) barkText.alpha = 0f;
    }

    public void OnShot()
    {
        if (Time.time < nextBarkTime || Random.value > chancePerShot || lines.Length == 0) return;
        Bark();
    }

    private void Bark()
    {
        int i = Random.Range(0, lines.Length);
        if (i == lastLine && lines.Length > 1) i = (i + 1) % lines.Length;   // never the same line twice in a row
        lastLine = i;

        if (barkText) barkText.text = lines[i];
        shownAt = Time.time;
        nextBarkTime = Time.time + cooldown;
        AudioManager.PlayRandom(voiceClips, 0.8f, 0.06f);
    }

    private void LateUpdate()
    {
        if (!barkText) return;

        float t = Time.time - shownAt;
        if (t > displayTime || (player != null && player.IsDead))
        {
            barkText.alpha = 0f;
            return;
        }

        // float up from over his head then fade out
        Vector3 world = transform.position + Vector3.up * (heightAboveHead + t * riseSpeed);
        if (cam) barkText.rectTransform.position = cam.WorldToScreenPoint(world);
        barkText.alpha = Mathf.Clamp01((displayTime - t) / 0.4f);
    }
}
