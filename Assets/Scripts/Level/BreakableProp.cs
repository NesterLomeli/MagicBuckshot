using UnityEngine;

// anything that breaks when you shoot it: barrels, stands, food, chairs, bookshelves, the big door
// one hit breaks it and it's loud so nearby enemies come check it out
public class BreakableProp : MonoBehaviour, IDamageable
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("Broken version of the sprite. If empty the prop disappears.")]
    [SerializeField] private Sprite brokenSprite;
    [Tooltip("Spawned on break, e.g. splinters or a potion puddle (Decal).")]
    [SerializeField] private GameObject debrisPrefab;
    [SerializeField] private AudioClip[] breakClips;
    [SerializeField] private float breakNoiseRadius = 7f;

    [Tooltip("Keep blocking movement after breaking (e.g. a cracked but standing crate).")]
    [SerializeField] private bool stillBlocksWhenBroken = false;

    private bool broken;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void TakeHit(Vector2 hitDirection, int damage)
    {
        if (broken) return;
        broken = true;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        // remember the space it took up so pathfinding can open it back up
        Bounds area = new Bounds(transform.position, Vector3.one);
        foreach (Collider2D c in colliders) area.Encapsulate(c.bounds);

        if (!stillBlocksWhenBroken)
            foreach (Collider2D c in colliders) c.enabled = false;

        if (brokenSprite != null) spriteRenderer.sprite = brokenSprite;
        else spriteRenderer.enabled = false;

        if (debrisPrefab)
        {
            float angle = Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg;
            Instantiate(debrisPrefab, transform.position, Quaternion.Euler(0f, 0f, angle));   // debris flies the way the bullet was going
        }

        AudioManager.PlayRandom(breakClips, 0.8f, 0.1f);
        NoiseSystem.Emit(transform.position, breakNoiseRadius);
        CameraFollow.Shake(0.06f, 0.08f);

        if (!stillBlocksWhenBroken)
            GridPathfinder.Instance?.RefreshArea(area);   // enemies can walk through here now
    }
}
