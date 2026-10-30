using UnityEngine;

// glowing trail behind a projectile, every few frames it leaves a fading copy of itself
public class SpriteTrail : MonoBehaviour
{
    [SerializeField] private float interval = 0.02f;
    [SerializeField] private float lifetime = 0.14f;
    [SerializeField] private Color tint = new Color(1f, 1f, 1f, 0.6f);
    [Tooltip("Only leave a trail during bullet time (shotgun pellets).")]
    [SerializeField] private bool onlyInBulletTime = false;

    private SpriteRenderer sr;
    private float timer;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (sr == null || !sr.enabled) return;
        if (onlyInBulletTime && !SmokeBreak.Active) return;   // shotgun pellets only leave trails in bullet time
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = interval;

        float scale = transform.lossyScale.x;
        SmokePuff ghost = SmokePuff.Spawn(sr.sprite, transform.position, tint, scale, sr.sortingOrder - 1);
        ghost.transform.rotation = transform.rotation;
        ghost.velocity = Vector2.zero;
        ghost.drag = 0f;
        ghost.growth = -scale * 0.8f / lifetime;
        ghost.lifetime = lifetime;
    }
}
