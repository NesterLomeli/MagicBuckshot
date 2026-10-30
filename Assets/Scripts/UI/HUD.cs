using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// everything on screen during a level: shells or the staff's bolt belt, weapon name, hostiles,
// messages, level title, quips, cig meter, screen flash and the bullet time look
// other scripts use HUD.Instance and null check it so a level still runs without it
public class HUD : MonoBehaviour
{
    public static HUD Instance { get; private set; }

    [Header("Text")]
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private TMP_Text weaponText;
    [SerializeField] private TMP_Text weaponTextShadow;
    [SerializeField] private TMP_Text enemiesText;
    [SerializeField] private TMP_Text centerText;
    [SerializeField] private TMP_Text levelTitleText;
    [SerializeField] private TMP_Text hintText;
    [Tooltip("Skeeter's floating one-liners (positioned by PlayerBarks).")]
    [SerializeField] private TMP_Text barkText;

    public TMP_Text BarkText => barkText;

    [Header("Shell ammo counter")]
    [Tooltip("Row of shell images, used when the gun holds this many rounds or fewer (the shotgun).")]
    [SerializeField] private Image[] shellIcons;
    [Tooltip("Loaded-shell sprites, used in order so every slot looks a little different.")]
    [SerializeField] private Sprite[] shellSprites;
    [SerializeField] private Sprite emptyShellSprite;

    [Header("Gatling Staff ammo belt")]
    [Tooltip("Rows of little bolt icons, used for big magazines (the staff). Each icon stands for a few rounds.")]
    [SerializeField] private Image[] boltIcons;
    [Tooltip("Bolt sprites in firing order (ice, arcane, star), repeated along the belt.")]
    [SerializeField] private Sprite[] boltSprites;
    [SerializeField] private Sprite emptyBoltSprite;

    [Header("Cigarette (bullet time)")]
    [Tooltip("Filled Image (horizontal, from the left): the cigarette, cropped as it burns.")]
    [SerializeField] private Image cigaretteFill;
    [Tooltip("Glowing tip, moved to the burning end while lit.")]
    [SerializeField] private RectTransform cigaretteEmber;
    [SerializeField] private TMP_Text cigaretteLabel;
    [Tooltip("Full-screen pink wash during bullet time.")]
    [SerializeField] private Image bulletTimeWash;
    [Tooltip("Full-screen green-edged vignette during bullet time.")]
    [SerializeField] private Image bulletTimeVignette;
    [SerializeField] private float washAlpha = 0.13f;
    [SerializeField] private float vignetteAlpha = 0.55f;
    [Tooltip("Black cinematic bars that slide in from the top and bottom during bullet time.")]
    [SerializeField] private RectTransform letterboxTop;
    [SerializeField] private RectTransform letterboxBottom;
    [SerializeField] private float letterboxHeight = 110f;

    [Header("Screen flash")]
    [Tooltip("Full-screen white image, invisible except for a split second on big shots.")]
    [SerializeField] private Image screenFlash;

    [Header("Reload bar")]
    [Tooltip("UI Image with Image Type = Filled. Hidden when not reloading.")]
    [SerializeField] private Image reloadFill;
    [SerializeField] private GameObject reloadBarRoot;

    [Header("Colours")]
    [SerializeField] private Color normalAmmoColor = new Color(0.92f, 0.88f, 0.78f);
    [SerializeField] private Color lowAmmoColor = new Color(0.9f, 0.25f, 0.2f);

    private Coroutine centerRoutine;
    private Coroutine hintRoutine;

    private void Awake()
    {
        Instance = this;
        if (centerText) centerText.text = "";
        if (hintText) hintText.text = "";
        if (levelTitleText) levelTitleText.text = "";
        SetReloadProgress(-1f);
    }

    public void SetAmmo(int rounds, int magazineSize)
    {
        // small mags (shotgun) show shells, big ones (staff) show the bolt belt
        bool useShells = shellIcons != null && shellIcons.Length >= magazineSize
                         && shellSprites != null && shellSprites.Length > 0;
        bool useBolts = !useShells && boltIcons != null && boltIcons.Length > 0
                        && boltSprites != null && boltSprites.Length > 0;

        if (boltIcons != null)
        {
            // 80 rounds over 40 icons = 2 rounds an icon, an icon stays lit while any of its rounds are left
            int perIcon = useBolts ? Mathf.CeilToInt(magazineSize / (float)boltIcons.Length) : 1;
            int iconsUsed = useBolts ? Mathf.CeilToInt(magazineSize / (float)perIcon) : 0;
            int iconsLit = useBolts ? Mathf.CeilToInt(rounds / (float)perIcon) : 0;
            for (int i = 0; i < boltIcons.Length; i++)
            {
                bool slotExists = i < iconsUsed;
                boltIcons[i].gameObject.SetActive(slotExists);
                if (slotExists)
                    boltIcons[i].sprite = i < iconsLit ? boltSprites[i % boltSprites.Length] : emptyBoltSprite;
            }
        }
        if (shellIcons != null)
            for (int i = 0; i < shellIcons.Length; i++)
            {
                bool slotExists = useShells && i < magazineSize;
                shellIcons[i].gameObject.SetActive(slotExists);
                if (slotExists)
                    shellIcons[i].sprite = i < rounds ? shellSprites[i % shellSprites.Length] : emptyShellSprite;
            }

        if (!ammoText) return;
        ammoText.gameObject.SetActive(!useShells && !useBolts);
        ammoText.text = $"{rounds} / {magazineSize}";
        int lowThreshold = Mathf.Max(1, magazineSize / 4);
        ammoText.color = rounds <= lowThreshold ? lowAmmoColor : normalAmmoColor;
    }

    public void SetWeaponName(string weaponName)
    {
        string shown = weaponName.ToUpperInvariant();
        if (weaponText) weaponText.text = shown;
        if (weaponTextShadow) weaponTextShadow.text = shown;
    }

    public void SetReloadProgress(float progress)
    {
        bool visible = progress >= 0f;
        if (reloadBarRoot) reloadBarRoot.SetActive(visible);
        if (reloadFill && visible) reloadFill.fillAmount = progress;
    }

    public void SetCigarette(float amount, bool lit, bool ready)
    {
        if (!cigaretteFill) return;
        float fill = 0.25f + 0.75f * Mathf.Clamp01(amount);   // the filter always shows, the paper burns from the right
        cigaretteFill.fillAmount = fill;
        cigaretteFill.color = ready || lit ? Color.white : new Color(1f, 1f, 1f, 0.45f);

        if (cigaretteEmber)
        {
            cigaretteEmber.gameObject.SetActive(lit);
            float width = cigaretteFill.rectTransform.sizeDelta.x;
            cigaretteEmber.anchoredPosition = cigaretteFill.rectTransform.anchoredPosition + new Vector2(width * fill, 0f);
        }
        if (cigaretteLabel) cigaretteLabel.alpha = ready || lit ? 1f : 0.45f;
    }

    public void SetBulletTime(float strength)
    {
        if (bulletTimeWash)
        {
            Color c = bulletTimeWash.color; c.a = washAlpha * strength; bulletTimeWash.color = c;
        }
        if (bulletTimeVignette)
        {
            Color c = bulletTimeVignette.color; c.a = vignetteAlpha * strength; bulletTimeVignette.color = c;
        }
        float bar = letterboxHeight * Mathf.SmoothStep(0f, 1f, strength);   // letterbox bars slide in smooth instead of snapping
        if (letterboxTop) letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, bar);
        if (letterboxBottom) letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, bar);
    }

    private float flashAlpha;

    public void FlashScreen(float strength)
    {
        flashAlpha = Mathf.Max(flashAlpha, strength);
    }

    private void Update()
    {
        if (!screenFlash) return;
        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 2.5f);   // flash fades out almost right away
        Color c = screenFlash.color;
        c.a = flashAlpha;
        screenFlash.color = c;
    }

    public void SetEnemiesRemaining(int count)
    {
        if (enemiesText) enemiesText.text = count > 0 ? $"HOSTILES: {count}" : "AREA CLEAR";
    }

    public void ShowCenterMessage(string message, float duration)
    {
        if (!centerText) return;
        if (centerRoutine != null) StopCoroutine(centerRoutine);
        centerText.text = message;
        if (duration > 0f) centerRoutine = StartCoroutine(ClearAfter(centerText, duration));
    }

    public void ShowHint(string message, float duration = 1f)
    {
        if (!hintText) return;
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintText.text = message;
        hintRoutine = StartCoroutine(ClearAfter(hintText, duration));
    }

    public void ShowLevelTitle(string title)
    {
        if (!levelTitleText) return;
        StartCoroutine(FadeTitle(title));
    }

    // real time so slow-mo or hit-stop can't make messages stick around
    private IEnumerator ClearAfter(TMP_Text text, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        text.text = "";
    }

    private IEnumerator FadeTitle(string title)
    {
        levelTitleText.text = title;
        Color c = levelTitleText.color;

        c.a = 1f;
        levelTitleText.color = c;
        yield return new WaitForSecondsRealtime(1.5f);

        const float fadeTime = 1f;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            c.a = 1f - t / fadeTime;
            levelTitleText.color = c;
            yield return null;
        }

        levelTitleText.text = "";
        c.a = 1f;
        levelTitleText.color = c;
    }
}
