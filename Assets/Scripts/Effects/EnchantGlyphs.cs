using UnityEngine;

// library effect around the staff, basically the minecraft enchanting table. runes and sparkles
// come off the nearby bookshelves and float into it. stops once you pick the staff up
public class EnchantGlyphs : MonoBehaviour
{
    [SerializeField] private Sprite[] glyphSprites;
    [SerializeField] private Sprite sparkSprite;
    [Tooltip("Where letters fly to (the staff). Defaults to this object.")]
    [SerializeField] private Transform target;
    [Tooltip("Bookshelves within this range are where the letters come from.")]
    [SerializeField] private float shelfSearchRadius = 6f;
    [SerializeField] private float spawnInterval = 0.12f;
    [SerializeField] private float flightTime = 1.4f;
    [SerializeField] private Color[] colors = { new Color(0.85f, 0.7f, 1f), new Color(0.55f, 0.85f, 1f), Color.white };

    private Transform[] shelves;
    private float timer;

    private void Start()
    {
        if (target == null) target = transform;

        // remember which bookshelves are close enough to send runes
        var found = new System.Collections.Generic.List<Transform>();
        foreach (BreakableProp prop in FindObjectsByType<BreakableProp>(FindObjectsSortMode.None))
            if (prop.name.StartsWith("Bookshelf") &&
                Vector2.Distance(prop.transform.position, transform.position) <= shelfSearchRadius)
                found.Add(prop.transform);
        shelves = found.ToArray();
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = spawnInterval * Random.Range(0.6f, 1.4f);

        Vector3 from = transform.position + (Vector3)(Random.insideUnitCircle.normalized * 3f);
        if (shelves.Length > 0)
        {
            Transform shelf = shelves[Random.Range(0, shelves.Length)];
            if (shelf != null && shelf.GetComponent<Collider2D>() != null && shelf.GetComponent<Collider2D>().enabled)   // only from shelves that haven't been shot
                from = shelf.position + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.3f, 0.3f), 0f);
        }

        bool spark = sparkSprite != null && Random.value < 0.25f;
        Sprite sprite = spark ? sparkSprite : glyphSprites[Random.Range(0, glyphSprites.Length)];
        var go = new GameObject(spark ? "EnchantSpark" : "EnchantGlyph");
        go.transform.position = from;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 18;
        sr.color = colors[Random.Range(0, colors.Length)];
        if (spark) go.transform.localScale = Vector3.one * 0.35f;
        var glyph = go.AddComponent<EnchantGlyph>();
        glyph.Launch(from, target.position, flightTime * Random.Range(0.8f, 1.2f));
    }
}
