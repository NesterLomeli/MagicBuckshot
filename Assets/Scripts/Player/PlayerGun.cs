using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// all the shooting, for every weapon. the numbers come from the equipped WeaponData so the
// shotgun and the gatling staff share this one script. spread, pump, reloading, spin-up, heat, swapping
public class PlayerGun : MonoBehaviour
{
    // a weapon Skeeter's carrying plus how many rounds are in it
    private class WeaponSlot
    {
        public WeaponData data;
        public int rounds;
        public WeaponSlot(WeaponData d) { data = d; rounds = d.magazineSize; }
    }

    [Tooltip("Weapons the player starts this level with. Level 1-2: just the shotgun. " +
             "Level 3: shotgun AND staff, since the staff was found in level 2.")]
    [SerializeField] private WeaponData[] startingWeapons;
    [SerializeField] private MuzzleFlash muzzleFlash;

    private readonly List<WeaponSlot> slots = new List<WeaponSlot>();
    private int currentIndex;
    private PlayerController player;
    private PlayerBarks barks;
    private SpriteRenderer muzzleRenderer;

    private bool isReloading;
    private Coroutine reloadRoutine;
    private Coroutine pumpRoutine;
    private float nextFireTime;
    private float spin;
    private float lastShotTime = -99f;

    // the gun has its own clock. normally it's the same as game time but in bullet time it runs
    // faster than the slowed world so Skeeter can still pump and shoot at a decent pace
    private float gunClock;
    private static float GunDelta => Time.deltaTime * SmokeBreak.GunTimeFactor;

    // same as WaitForSeconds but on the gun's clock
    private IEnumerator WaitGun(float seconds)
    {
        float end = gunClock + seconds;
        while (gunClock < end) yield return null;
    }

    public float Heat { get; private set; }
    public WeaponData CurrentWeapon => slots.Count > 0 ? Current.data : null;
    public float MoveMultiplier =>
        slots.Count > 0 && Time.time - lastShotTime < 0.15f ? Current.data.firingMoveMultiplier : 1f;
    private int projectileCycle;

    private WeaponSlot Current => slots[currentIndex];
    public bool IsReloading => isReloading;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        barks = GetComponent<PlayerBarks>();
        if (muzzleFlash) muzzleRenderer = muzzleFlash.GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        foreach (WeaponData w in startingWeapons)
            if (w != null) slots.Add(new WeaponSlot(w));

        if (slots.Count == 0)
        {
            Debug.LogError("PlayerGun has no starting weapons assigned.", this);
            enabled = false;
            return;
        }
        Equip(0, playSound: false);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.State.PlayerDead)
            return;

        if (Input.GetKeyDown(KeyCode.Q)) Equip((currentIndex + 1) % slots.Count);
        if (Input.GetKeyDown(KeyCode.Alpha1)) Equip(0);
        if (Input.GetKeyDown(KeyCode.Alpha2) && slots.Count > 1) Equip(1);

        if (Input.GetKeyDown(KeyCode.R)) TryStartReload();

        gunClock += GunDelta;

        WeaponData w = Current.data;
        Heat = Mathf.MoveTowards(Heat, 0f, w.heatDecay * Time.deltaTime);   // barrel cools down
        bool triggerHeld = Input.GetMouseButton(0);
        bool triggerPressed = Input.GetMouseButtonDown(0);

        // gatling spin-up: builds while you hold the trigger, winds down twice as fast when you let go
        if (w.spinUpTime > 0f)
        {
            float rate = GunDelta / w.spinUpTime;
            spin = Mathf.Clamp01(spin + (triggerHeld && !isReloading ? rate : -rate * 2f));
        }

        if (w.fullAuto ? triggerHeld : triggerPressed)
            TryFire(triggerPressed);
    }

    public void AddWeapon(WeaponData weapon)
    {
        int existing = slots.FindIndex(s => s.data == weapon);
        if (existing >= 0) { Equip(existing); return; }

        slots.Add(new WeaponSlot(weapon));
        Equip(slots.Count - 1);
    }

    private void Equip(int index, bool playSound = true)
    {
        if (index < 0 || index >= slots.Count) return;
        if (index == currentIndex && playSound) return;   // already holding it

        CancelReload();
        if (pumpRoutine != null) StopCoroutine(pumpRoutine);
        currentIndex = index;
        spin = 0f;
        nextFireTime = gunClock + 0.15f;   // short draw time so swapping isn't free

        WeaponData w = Current.data;
        if (w.playerSprite) player.SetBodySprite(w.playerSprite);
        if (playSound)
        {
            AudioManager.Play(w.equipClip, 0.8f, 0f);
            if (w.pumpAction) AudioManager.PlayRandom(w.pumpClips, 0.8f, 0.03f);
        }

        HUD.Instance?.SetWeaponName(w.displayName + (slots.Count > 1 ? "   [Q] swap" : ""));
        HUD.Instance?.SetAmmo(Current.rounds, w.magazineSize);
    }

    private void TryFire(bool freshPress)
    {
        WeaponData w = Current.data;

        if (isReloading)
        {
            if (!(w.loadOneAtATime && Current.rounds > 0)) return;   // shotgun: shooting cancels the reload as long as there's at least one shell in
            CancelReload();
        }
        if (gunClock < nextFireTime) return;

        if (Current.rounds <= 0)
        {
            if (freshPress)
            {
                AudioManager.Play(w.dryFireClip, 0.8f);
                HUD.Instance?.ShowHint("EMPTY — RELOAD [R]", 1.2f);
            }
            return;
        }

        Fire(w);
    }

    private void Fire(WeaponData w)
    {
        Current.rounds--;

        // shoots slower until the staff is spun up
        float slowdown = w.spinUpTime > 0f ? Mathf.Lerp(w.spinUpStartSlowdown, 1f, spin) : 1f;
        nextFireTime = gunClock + w.fireCooldown * slowdown;

        for (int i = 0; i < w.pelletsPerShot; i++)
        {
            float angle;
            // single bolts get random spread, buckshot pellets spread evenly across the cone with a little jitter
            if (w.pelletsPerShot == 1)
            {
                angle = Random.Range(-w.spreadAngle * 0.5f, w.spreadAngle * 0.5f);
            }
            else
            {
                float t = (float)i / (w.pelletsPerShot - 1);
                angle = Mathf.Lerp(-w.spreadAngle * 0.5f, w.spreadAngle * 0.5f, t)
                      + Random.Range(-w.pelletJitter, w.pelletJitter);
            }

            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * player.AimDirection;

            // spawn from Skeeter's center not the barrel so hugging a wall can't shoot through it
            Bullet projectile = Instantiate(NextProjectile(w), transform.position, Quaternion.identity);
            projectile.Fire(direction);
            GameStats.ShotsFired++;
        }

        lastShotTime = Time.time;
        Heat = Mathf.Min(1f, Heat + w.heatPerShot);

        AudioManager.PlayRandom(w.shotClips, w.fullAuto ? 0.6f : 0.9f);
        if (muzzleFlash)
        {
            if (muzzleRenderer) muzzleRenderer.color = w.muzzleFlashColor;
            muzzleFlash.Flash(w.muzzleFlashScale);
        }
        if (w.pumpAction) pumpRoutine = StartCoroutine(PumpRoutine(w));   // pump guns spit the shell out when racked
        else if (!w.ejectShellsOnReload) EjectShells(w, 1);
        CameraFollow.Shake(w.shakeStrength, w.fullAuto ? 0.08f : 0.18f);
        CameraFollow.Punch(-player.AimDirection, w.shakeStrength * 0.8f);   // camera kicks back toward Skeeter
        player.Kick(w.fullAuto ? 0.03f : 0.12f);
        player.Knockback(-player.AimDirection * w.selfKnockback);   // and he gets shoved back
        if (w.screenFlash > 0f) HUD.Instance?.FlashScreen(w.screenFlash);

        NoiseSystem.Emit(transform.position, w.noiseRadius);   // gunshots are loud, nearby enemies come running

        if (barks) barks.OnShot();

        HUD.Instance?.SetAmmo(Current.rounds, w.magazineSize);
        if (Current.rounds == 0) HUD.Instance?.ShowHint("RELOAD [R]", 1.5f);
    }

    // the staff cycles ice, arcane and star bolts, other guns just use one
    private Bullet NextProjectile(WeaponData w)
    {
        if (w.alternatingProjectiles == null || w.alternatingProjectiles.Length == 0) return w.projectilePrefab;
        Bullet next = w.alternatingProjectiles[projectileCycle % w.alternatingProjectiles.Length];
        projectileCycle++;
        return next;
    }

    // the pump action delays to give the effect of the main character racking it back and forth
    private IEnumerator PumpRoutine(WeaponData w)
    {
        yield return WaitGun(w.pumpDelay);
        AudioManager.PlayRandom(w.pumpClips, 0.85f, 0.03f);
        player.Kick(0.07f);
        EjectShells(w, 1);
        pumpRoutine = null;
    }

    private void TryStartReload()
    {
        if (isReloading || Current.rounds == Current.data.magazineSize) return;
        reloadRoutine = StartCoroutine(Current.data.loadOneAtATime ? LoadShellsRoutine(Current) : ReloadRoutine(Current));
    }

    private IEnumerator ReloadRoutine(WeaponSlot slot)
    {
        WeaponData w = slot.data;
        isReloading = true;
        AudioManager.Play(w.reloadStartClip, 0.9f, 0.02f);

        if (w.ejectShellsOnReload) EjectShells(w, w.magazineSize - slot.rounds);

        HUD.Instance?.ShowHint("RELOADING...", w.reloadTime);
        for (float t = 0f; t < w.reloadTime; t += GunDelta)
        {
            HUD.Instance?.SetReloadProgress(t / w.reloadTime);
            yield return null;
        }

        slot.rounds = w.magazineSize;
        FinishReload(w, slot);
    }

    // shotgun reload: one shell at a time and each one shows up on the HUD as it goes in
    private IEnumerator LoadShellsRoutine(WeaponSlot slot)
    {
        WeaponData w = slot.data;
        isReloading = true;
        bool wasEmpty = slot.rounds == 0;
        AudioManager.Play(w.reloadStartClip, 0.8f, 0.02f);
        yield return WaitGun(0.15f);

        while (slot.rounds < w.magazineSize)
        {
            for (float t = 0f; t < w.shellInsertTime; t += GunDelta)
            {
                float progress = (slot.rounds + t / w.shellInsertTime) / w.magazineSize;
                HUD.Instance?.SetReloadProgress(progress);
                yield return null;
            }
            slot.rounds++;
            AudioManager.PlayRandom(w.shellInsertClips, 0.8f, 0.05f);
            HUD.Instance?.SetAmmo(slot.rounds, w.magazineSize);
        }

        // reloading from empty means racking one into the chamber at the end
        if (wasEmpty && w.pumpAction)
        {
            AudioManager.PlayRandom(w.pumpClips, 0.85f, 0.03f);
            yield return WaitGun(0.25f);
        }
        FinishReload(w, slot);
    }

    private void FinishReload(WeaponData w, WeaponSlot slot)
    {
        isReloading = false;
        reloadRoutine = null;
        AudioManager.Play(w.reloadEndClip, 0.9f, 0.02f);
        HUD.Instance?.SetReloadProgress(-1f);
        HUD.Instance?.SetAmmo(slot.rounds, w.magazineSize);
    }

    private void CancelReload()
    {
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        isReloading = false;
        HUD.Instance?.SetReloadProgress(-1f);
    }

    private void EjectShells(WeaponData w, int count)
    {
        if (!w.shellCasingPrefab) return;
        float aim = Mathf.Atan2(player.AimDirection.y, player.AimDirection.x) * Mathf.Rad2Deg;
        for (int i = 0; i < count; i++)
        {
            float angle = aim - 90f + Random.Range(-25f, 25f);   // shells fly out to Skeeter's right
            Instantiate(w.shellCasingPrefab, transform.position, Quaternion.Euler(0f, 0f, angle));
        }
    }

    private void OnDisable()
    {
        CancelReload();
        StopAllCoroutines();
    }
}
