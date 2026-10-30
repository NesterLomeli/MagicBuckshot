using UnityEngine;

// burst of colored puffs that spread out and fade (potion stands, staff bolt hits)
public class PuffCloud : MonoBehaviour
{
    [SerializeField] private Sprite puffSprite;
    [SerializeField] private Color[] colors =
    {
        new Color(0.88f, 0.28f, 0.28f), new Color(0.37f, 0.83f, 0.37f), new Color(0.31f, 0.56f, 0.88f),
        new Color(0.88f, 0.44f, 0.82f), new Color(0.94f, 0.75f, 0.25f),
    };
    [SerializeField] private int count = 14;
    [SerializeField] private float speed = 2.2f;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.6f, 1.1f);
    [SerializeField] private float lifetime = 0.9f;
    [SerializeField] private GameObject decalPrefab;

    private void Start()
    {
        if (decalPrefab) Instantiate(decalPrefab, transform.position, Quaternion.identity);

        for (int i = 0; i < count; i++)
        {
            Color c = colors[Random.Range(0, colors.Length)];
            c.a = 0.85f;
            Vector2 dir = Random.insideUnitCircle.normalized;
            SmokePuff puff = SmokePuff.Spawn(puffSprite, transform.position + (Vector3)(dir * 0.2f), c,
                                             Random.Range(scaleRange.x, scaleRange.y), 16);
            puff.velocity = dir * speed * Random.Range(0.5f, 1.2f);
            puff.growth = 1.2f;
            puff.drag = 3f;
            puff.lifetime = lifetime * Random.Range(0.7f, 1.2f);
        }
        Destroy(gameObject);
    }
}
