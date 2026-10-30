using UnityEngine;

[CreateAssetMenu(menuName = "Magic Buckshot/Weapon", fileName = "NewWeapon")]
// all the numbers for one weapon saved as an asset. the shotgun and the gatling staff
// are the same script with different values, PlayerGun reads whichever one is equipped
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Pa's Pump Shotgun";
    [Tooltip("Optional player sprite holding this weapon. Empty = keep the current sprite.")]
    public Sprite playerSprite;

    [Header("Firing")]
    [Tooltip("Projectile spawned per pellet/bolt. Its own Damage field sets how hard it hits.")]
    public Bullet projectilePrefab;
    [Tooltip("Optional: fire these in turn instead of Projectile Prefab " +
             "(the Gatling Staff alternates ice, arcane and star bolts).")]
    public Bullet[] alternatingProjectiles;
    [Tooltip("Hold the mouse button to keep firing (Gatling). Off = one shot per click.")]
    public bool fullAuto = false;
    [Tooltip("Seconds between shots at full speed.")]
    public float fireCooldown = 0.2f;
    [Min(1)] public int pelletsPerShot = 12;
    [Tooltip("Total width of the shot cone in degrees.")]
    public float spreadAngle = 34f;
    [Tooltip("Random wobble per pellet/bolt in degrees.")]
    public float pelletJitter = 2f;

    [Header("Spin-up (Gatling)")]
    [Tooltip("Seconds of holding the trigger to reach full fire rate. 0 = no spin-up.")]
    public float spinUpTime = 0f;
    [Tooltip("At the start of the spin-up, shots are this many times slower than full speed.")]
    public float spinUpStartSlowdown = 4f;

    [Header("Pump action")]
    [Tooltip("Rack the slide after every shot: plays the pump sound and ejects the hull.")]
    public bool pumpAction = false;
    [Tooltip("Seconds after the shot before the pump is racked (keep below Fire Cooldown).")]
    public float pumpDelay = 0.2f;
    public AudioClip[] pumpClips;

    [Header("Ammo")]
    [Tooltip("Rounds the gun holds. MP-133 style pump: 4 in the tube + 1 in the chamber = 5.")]
    public int magazineSize = 5;
    [Tooltip("Whole-magazine reload time (used when Load One At A Time is off).")]
    public float reloadTime = 0.9f;
    [Tooltip("Tube-fed: shells go in one by one, and firing interrupts the reload.")]
    public bool loadOneAtATime = false;
    [Tooltip("Seconds to push one shell into the tube.")]
    public float shellInsertTime = 0.42f;
    public AudioClip[] shellInsertClips;

    [Header("Noise and feel")]
    [Tooltip("Enemies within this many units hear each shot.")]
    public float noiseRadius = 11f;
    public float shakeStrength = 0.22f;
    public Color muzzleFlashColor = Color.white;

    [Header("Heat and heft (the Gatling Staff)")]
    [Tooltip("Heat added per shot (0-1). A hot barrel smokes, and keeps smoking after you stop.")]
    public float heatPerShot = 0f;
    [Tooltip("Heat lost per second.")]
    public float heatDecay = 0.35f;
    [Tooltip("Colours of the barrel smoke (mixed with grey).")]
    public Color[] smokeColors;
    [Tooltip("Walking speed multiplier while firing (below 1 = the gun is heavy).")]
    [Range(0.2f, 1f)] public float firingMoveMultiplier = 1f;
    [Tooltip("Muzzle flash size multiplier (shotgun ~1.8).")]
    public float muzzleFlashScale = 1f;
    [Tooltip("Speed Skeeter is shoved backwards per shot (shotgun ~5, staff ~0.3).")]
    public float selfKnockback = 0f;
    [Tooltip("Brief white flash over the whole screen per shot (0 = none, shotgun ~0.12).")]
    [Range(0f, 1f)] public float screenFlash = 0f;

    [Header("Shells")]
    public GameObject shellCasingPrefab;
    [Tooltip("Break-action guns dump all spent shells on reload instead of one per shot.")]
    public bool ejectShellsOnReload = true;

    [Header("Sounds")]
    public AudioClip[] shotClips;
    public AudioClip dryFireClip;
    public AudioClip reloadStartClip;
    public AudioClip reloadEndClip;
    public AudioClip equipClip;
}
