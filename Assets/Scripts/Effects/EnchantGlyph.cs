using UnityEngine;

// one rune flying from a bookshelf into the staff on a curve, fades in and out
public class EnchantGlyph : MonoBehaviour
{
    private Vector3 start, end, bend;
    private float duration, age;
    private SpriteRenderer sr;
    private Color baseColor;

    public void Launch(Vector3 from, Vector3 to, float time)
    {
        start = from;
        end = to;
        duration = time;
        Vector3 side = Vector3.Cross((to - from).normalized, Vector3.forward);
        // a middle point off to the side so the runes swirl in instead of flying straight
        bend = (from + to) * 0.5f + Vector3.up * 0.8f + side * Random.Range(-1f, 1f);
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = age / duration;
        if (t >= 1f) { Destroy(gameObject); return; }

        // curve from start to bend to end (bezier curve), speeds up as it gets close
        float e = t * t;
        Vector3 a = Vector3.Lerp(start, bend, e);
        Vector3 b = Vector3.Lerp(bend, end, e);
        transform.position = Vector3.Lerp(a, b, e);

        float alpha = Mathf.Min(t / 0.2f, (1f - t) / 0.25f, 1f);
        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
    }
}
