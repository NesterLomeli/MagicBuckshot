using UnityEngine;

// one puff that drifts, grows and fades then deletes itself. used for smoke, potion clouds,
// blood mist, bolt trails and Skeeter's bullet time afterimages
public class SmokePuff : MonoBehaviour
{
    public Vector2 velocity;
    [Tooltip("Scale added per second.")]
    public float growth = 0.5f;
    public float lifetime = 0.6f;
    [Tooltip("Velocity lost per second (0 = keeps drifting).")]
    public float drag = 2f;
    [Tooltip("Ignore slow motion (cigarette smoke and afterimages stay real-time).")]
    public bool realTime;   // true = ignores slow-mo (cig smoke, afterimages)

    private SpriteRenderer sr;
    private float age;
    private float startAlpha;

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        startAlpha = sr ? sr.color.a : 1f;
    }

    private void Update()
    {
        float dt = realTime ? Time.unscaledDeltaTime : Time.deltaTime;
        age += dt;
        if (age >= lifetime) { Destroy(gameObject); return; }

        transform.position += (Vector3)(velocity * dt);
        velocity = Vector2.MoveTowards(velocity, Vector2.zero, drag * dt);
        transform.localScale += Vector3.one * growth * dt;

        if (sr)
        {
            Color c = sr.color;
            c.a = startAlpha * (1f - age / lifetime);
            sr.color = c;
        }
    }

    // makes a puff from code without needing a prefab
    public static SmokePuff Spawn(Sprite sprite, Vector3 position, Color color, float scale, int sortingOrder)
    {
        var go = new GameObject("Puff");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.color = color;
        r.sortingOrder = sortingOrder;
        return go.AddComponent<SmokePuff>();
    }
}
