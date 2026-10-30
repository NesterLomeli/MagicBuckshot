using UnityEngine;

// every projectile in the game: pellets, staff bolts, wizard spells
// raycasts ahead every frame instead of using physics so fast bullets can't skip through walls
public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 30f;
    [SerializeField] private float maxLifetime = 2f;
    [SerializeField] private LayerMask hitMask;

    [Tooltip("Health removed by a hit at point-blank range. Knights have 6 HP, wizards 3.")]
    [Min(1)] [SerializeField] private int damage = 1;

    [Tooltip("Damage fades from Damage (at the muzzle) down to Min Damage (at max range). " +
             "Buckshot uses 3 → 1: two pellets kill a knight up close, at range it takes a second blast.")]
    [SerializeField] private bool damageFalloff = false;
    [Min(1)] [SerializeField] private int minDamage = 1;

    [Header("Impact (walls / props)")]
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private AudioClip[] impactClips;
    [SerializeField] private float impactVolume = 0.5f;

    private Vector2 direction;
    private float distanceTravelled;
    private bool hasHit;
    private ContactFilter2D filter;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[4];

    public void Fire(Vector2 dir)
    {
        direction = dir.normalized;
        transform.right = direction;

        filter = new ContactFilter2D();
        filter.SetLayerMask(hitMask);
        filter.useTriggers = false;   // ignore trigger zones like pickups

        Destroy(gameObject, maxLifetime);
        Step(0f);   // check right away in case it spawned touching something
    }

    private void Update()
    {
        Step(speed * Time.deltaTime);
    }

    private void Step(float distance)
    {
        if (hasHit) return;

        // look ahead exactly as far as it moves this frame
        int count = Physics2D.Raycast(transform.position, direction, filter, hits, Mathf.Max(distance, 0.01f));

        for (int i = 0; i < count; i++)
        {
            if (!hits[i].collider.enabled) continue;   // skip props that are already broken
            OnHit(hits[i]);
            return;
        }

        transform.position += (Vector3)(direction * distance);
        distanceTravelled += distance;
    }

    // buckshot falloff: full damage up close, less the farther it flies
    private int CurrentDamage()
    {
        if (!damageFalloff) return damage;
        float maxRange = speed * maxLifetime;
        float t = Mathf.Clamp01(distanceTravelled / maxRange);
        return Mathf.Max(minDamage, Mathf.RoundToInt(Mathf.Lerp(damage, minDamage, t)));
    }

    private void OnHit(RaycastHit2D hit)
    {
        hasHit = true;

        // if it can take damage hurt it, otherwise it's a wall so spark and play the impact sound
        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            target.TakeHit(direction, CurrentDamage());
        }
        else
        {
            if (impactEffectPrefab)
                Instantiate(impactEffectPrefab, hit.point, Quaternion.FromToRotation(Vector3.right, hit.normal));
            AudioManager.PlayRandom(impactClips, impactVolume, 0.15f);
        }

        Destroy(gameObject);
    }
}
