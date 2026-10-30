using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
// Skeeter himself: WASD movement, facing the mouse, footsteps, recoil,
// getting pushed back from shooting, and dying in one hit. shooting is in PlayerGun
public class PlayerController : MonoBehaviour, IDamageable
{
    public static PlayerController Instance { get; private set; }

    public bool IsDead { get; private set; }

    public Vector2 AimDirection { get; private set; } = Vector2.right;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;

    [Tooltip("0 if the sprite art faces right, -90 if it faces up.")]
    [SerializeField] private float spriteFacingOffset = 0f;

    [Header("Footsteps")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float stepInterval = 0.3f;
    [SerializeField] private float footstepVolume = 0.35f;

    [Header("Death")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [Tooltip("Optional sprite swapped in when the player dies.")]
    [SerializeField] private Sprite deadSprite;
    [SerializeField] private GameObject bloodSplatPrefab;
    [SerializeField] private AudioClip[] deathClips;

    private Rigidbody2D rb;
    private PlayerGun gun;
    private float kick;
    private Vector2 knockback;
    private Camera cam;
    private Vector2 moveInput;
    private float aimAngle;
    private float stepTimer;

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        gun = GetComponent<PlayerGun>();
        rb.gravityScale = 0f;
        cam = Camera.main;
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (IsDead) return;

        // raw input so movement is snappy, normalized so diagonals aren't faster
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toMouse = mouseWorld - (Vector2)transform.position;
        if (toMouse.sqrMagnitude > 0.0001f) AimDirection = toMouse.normalized;
        aimAngle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg + spriteFacingOffset;

        HandleFootsteps();

        // recoil: the sprite snaps back when you shoot then slides forward again
        kick = Mathf.MoveTowards(kick, 0f, 0.9f * Time.deltaTime);
        if (bodyRenderer) bodyRenderer.transform.localPosition = new Vector3(-kick, 0f, 0f);
    }

    public void Knockback(Vector2 velocity)
    {
        knockback += velocity;
    }

    public void Kick(float amount)
    {
        kick = Mathf.Max(kick, amount);
    }

    private void FixedUpdate()
    {
        if (IsDead) return;

        // bullet time slows the world down so divide by timeScale to keep Skeeter at full speed
        // (skipped during the hit-stop freeze)
        float speedUp = Time.timeScale > 0.2f && Time.timeScale < 1f ? 1f / Time.timeScale : 1f;
        float heavy = gun != null ? gun.MoveMultiplier : 1f;   // the gatling staff slows you down while you're firing it
        PhysicsUtil.SetVelocity(rb, (moveInput * moveSpeed * heavy + knockback) * speedUp);
        knockback = Vector2.MoveTowards(knockback, Vector2.zero, 60f * Time.fixedDeltaTime);

        rb.angularVelocity = 0f;   // stop bumps from spinning him, always face the mouse
        rb.MoveRotation(aimAngle);
    }

    private void HandleFootsteps()
    {
        if (moveInput.sqrMagnitude < 0.01f)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            AudioManager.PlayRandom(footstepClips, footstepVolume, 0.12f);
            stepTimer = stepInterval;
        }
    }

    public void SetBodySprite(Sprite sprite)
    {
        if (bodyRenderer && sprite && !IsDead) bodyRenderer.sprite = sprite;
    }

    // one hit and he's dead (unless F2 god mode is on), falls the way the shot came from
    public void TakeHit(Vector2 hitDirection, int damage)
    {
        if (IsDead || DevCheats.GodMode) return;
        IsDead = true;

        PhysicsUtil.SetVelocity(rb, Vector2.zero);
        rb.simulated = false;

        PlayerGun gun = GetComponent<PlayerGun>();
        if (gun) gun.enabled = false;

        float fallAngle = Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg + spriteFacingOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, fallAngle);
        if (deadSprite && bodyRenderer) bodyRenderer.sprite = deadSprite;

        if (bloodSplatPrefab)
            Instantiate(bloodSplatPrefab, transform.position, Quaternion.Euler(0f, 0f, fallAngle));

        AudioManager.PlayRandom(deathClips);
        CameraFollow.Shake(0.35f, 0.3f);
        HUD.Instance?.SetReloadProgress(-1f);

        GameManager.Instance?.PlayerKilled();
    }
}
