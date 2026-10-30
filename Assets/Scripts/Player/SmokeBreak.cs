using UnityEngine;

// Max payne 3 style bullet time from "smoke break" hell yea
// shift or right click lights up: time slows down, sounds get deeper, heartbeat, letterbox bars,
// color wash and afterimages. Skeeter still moves full speed and his gun runs faster than the world
// one cig that burns down while it's lit and slowly grows back
public class SmokeBreak : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static float SlowScale { get; private set; } = 0.55f;
    public static float WorldTimeScale { get; private set; } = 1f;
    // how much faster than the world PlayerGun's timers run, 1 normally
    public static float GunTimeFactor =>
        Active && Time.timeScale > 0.2f ? gunSpeedStatic / Time.timeScale : 1f;
    private static float gunSpeedStatic = 0.7f;

    [Header("Timing")]
    [SerializeField] private float slowScale = 0.3f;
    [Tooltip("Real seconds to ease from normal speed into slow motion (and back out).")]
    [SerializeField] private float rampTime = 0.25f;
    [Tooltip("Skeeter's gun speed during bullet time, as a fraction of normal (the world runs at slowScale).")]
    [SerializeField] private float gunSpeed = 0.7f;
    [Tooltip("Sound pitch at full slow motion (lower = deeper).")]
    [SerializeField] private float slowPitch = 0.7f;
    [Tooltip("Real seconds a full cigarette lasts.")]
    [SerializeField] private float burnTime = 5f;
    [Tooltip("Real seconds to regrow from nothing to full.")]
    [SerializeField] private float regrowTime = 14f;
    [Tooltip("Need at least this much cigarette (0-1) to light up.")]
    [SerializeField] private float minToLight = 0.2f;

    [Header("Visuals")]
    [Tooltip("Cigarette sprite at Skeeter's mouth, shown while lit.")]
    [SerializeField] private SpriteRenderer mouthCigarette;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Sprite puffSprite;
    [Tooltip("Seconds between smoke puffs.")]
    [SerializeField] private float smokeInterval = 0.09f;
    [Tooltip("Seconds between afterimages.")]
    [SerializeField] private float ghostInterval = 0.07f;
    [Tooltip("Afterimage colours, cycled in order: Max Payne 3 style colour bleed (blue, red, green, pink).")]
    [SerializeField] private Color[] ghostColors =
    {
        new Color(0.3f, 0.55f, 1f, 0.35f),
        new Color(1f, 0.25f, 0.3f, 0.35f),
        new Color(0.3f, 1f, 0.5f, 0.3f),
        new Color(1f, 0.4f, 0.85f, 0.35f),
    };

    [Header("Sounds")]
    [SerializeField] private AudioClip lightClip;
    [SerializeField] private AudioClip exhaleClip;
    [SerializeField] private AudioClip slowInClip;
    [SerializeField] private AudioClip slowOutClip;
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField] private float heartbeatInterval = 0.85f;

    private float cigarette = 1f;
    private float effect;
    private float smokeTimer, ghostTimer;
    private int ghostIndex;
    private float worldScale = 1f;
    private float heartbeatTimer;
    private PlayerController player;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        SlowScale = slowScale;
        gunSpeedStatic = gunSpeed;
        Active = false;
        WorldTimeScale = 1f;
        if (mouthCigarette) mouthCigarette.enabled = false;
    }

    private void Update()
    {
        bool pressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetMouseButtonDown(1);
        bool dead = player != null && player.IsDead;

        if (pressed && !dead)
        {
            if (Active) StopSmoking();
            else if (cigarette >= minToLight) StartSmoking();
        }

        float dt = Time.unscaledDeltaTime;   // the cig burns in real time, not slowed time
        if (Active)
        {
            cigarette -= dt / burnTime;
            if (cigarette <= 0f || dead) { cigarette = Mathf.Max(cigarette, 0f); StopSmoking(); }
            else SpawnTrails(dt);
        }
        else
        {
            cigarette = Mathf.Min(1f, cigarette + dt / regrowTime);
        }

        // ease into and out of slow-mo instead of snapping, feels more dramatic
        float target = Active ? slowScale : 1f;
        worldScale = Mathf.MoveTowards(worldScale, target, dt * (1f - slowScale) / Mathf.Max(0.01f, rampTime));
        WorldTimeScale = worldScale;
        if (Time.timeScale > 0.1f)   // don't fight the hit-stop freeze, GameManager puts the speed back when it's done
        {
            Time.timeScale = worldScale;
            Time.fixedDeltaTime = 0.02f * worldScale;   // shrink the physics steps too so slow-mo stays smooth
        }

        effect = Mathf.MoveTowards(effect, Active ? 1f : 0f, dt * 4f);
        AudioManager.PitchScale = Mathf.Lerp(1f, slowPitch, effect);

        if (Active)
        {
            heartbeatTimer -= dt;
            if (heartbeatTimer <= 0f)
            {
                heartbeatTimer = heartbeatInterval;
                AudioManager.Play(heartbeatClip, 0.7f, 0f);
            }
        }
        HUD.Instance?.SetBulletTime(effect);
        HUD.Instance?.SetCigarette(cigarette, Active, cigarette >= minToLight);
    }

    private void StartSmoking()
    {
        Active = true;
        heartbeatTimer = 0.3f;
        if (mouthCigarette) mouthCigarette.enabled = true;
        AudioManager.Play(lightClip, 0.7f, 0.03f);
        AudioManager.Play(slowInClip, 0.9f, 0f);
        HUD.Instance?.FlashScreen(0.25f);
        CameraFollow.Shake(0.15f, 0.2f);
    }

    private void StopSmoking()
    {
        Active = false;
        if (mouthCigarette) mouthCigarette.enabled = false;
        AudioManager.Play(exhaleClip, 0.5f, 0.05f);
        AudioManager.Play(slowOutClip, 0.8f, 0f);
    }

    private static void SetWorldSpeed(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = 0.02f * scale;
    }

    // thin smoke coming off the cig plus the colored afterimages
    private void SpawnTrails(float dt)
    {
        smokeTimer -= dt;
        if (smokeTimer <= 0f && mouthCigarette && puffSprite)
        {
            smokeTimer = smokeInterval;
            Vector3 tip = mouthCigarette.transform.TransformPoint(new Vector3(0.12f, 0f, 0f));
            SmokePuff puff = SmokePuff.Spawn(puffSprite, tip, new Color(0.9f, 0.9f, 0.9f, 0.6f), 0.18f, 9);
            puff.velocity = new Vector2(Random.Range(-0.12f, 0.12f), 1.1f);
            puff.growth = 0.12f;
            puff.drag = 0.8f;
            puff.lifetime = 0.35f;
            puff.realTime = true;
        }

        ghostTimer -= dt;
        if (ghostTimer <= 0f && body && body.sprite)
        {
            ghostTimer = ghostInterval;
            ghostIndex = (ghostIndex + 1) % ghostColors.Length;
            SmokePuff ghost = SmokePuff.Spawn(body.sprite, body.transform.position, ghostColors[ghostIndex],
                                              1f, body.sortingOrder - 1);
            ghost.transform.rotation = body.transform.rotation;
            ghost.transform.localScale = body.transform.lossyScale;
            ghost.velocity = Vector2.zero;
            ghost.growth = 0f;
            ghost.lifetime = 0.22f;
            ghost.realTime = true;
        }
    }

    // leaving the level while smoking: put time and sound back to normal
    private void OnDestroy()
    {
        SetWorldSpeed(1f);
        Active = false;
        WorldTimeScale = 1f;
        AudioManager.PitchScale = 1f;
    }
}
