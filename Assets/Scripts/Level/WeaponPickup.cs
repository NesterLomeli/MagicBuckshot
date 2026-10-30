using UnityEngine;

[RequireComponent(typeof(Collider2D))]
// the gatling staff pickup, bobs up and down and gives you the weapon when you walk over it
public class WeaponPickup : MonoBehaviour
{
    [SerializeField] private WeaponData weapon;
    [Tooltip("Small text under the weapon name when picked up.")]
    [SerializeField] private string announcement = "Hold LEFT CLICK to unleash it.";
    [SerializeField] private AudioClip pickupClip;

    [Header("Idle animation")]
    [SerializeField] private Transform visual;
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 3f;
    [SerializeField] private float spinSpeed = 0f;

    private Vector3 visualStart;
    private bool taken;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (visual) visualStart = visual.localPosition;
    }

    private void Update()
    {
        if (!visual) return;
        visual.localPosition = visualStart + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        if (spinSpeed != 0f) visual.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (taken || weapon == null) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead) return;

        PlayerGun gun = player.GetComponent<PlayerGun>();
        if (gun == null) return;

        taken = true;
        gun.AddWeapon(weapon);
        AudioManager.Play(pickupClip, 1f, 0f);
        CameraFollow.Shake(0.2f, 0.25f);
        HUD.Instance?.ShowCenterMessage($"{weapon.displayName.ToUpper()}\n<size=50%>{announcement}</size>", 2.5f);
        Destroy(gameObject);
    }
}
