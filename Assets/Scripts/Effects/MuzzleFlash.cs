using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
// the flash at the end of the barrel, random size and flip each shot so it doesn't look the same
public class MuzzleFlash : MonoBehaviour
{
    [SerializeField] private float visibleTime = 0.05f;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.8f, 1.2f);
    [Tooltip("Optional: several flash sprites to pick from at random.")]
    [SerializeField] private Sprite[] variants;

    private SpriteRenderer sr;
    private Vector3 baseScale;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        sr.enabled = false;
    }

    public void Flash(float sizeMultiplier = 1f)
    {
        if (!isActiveAndEnabled) return;
        StopAllCoroutines();
        StartCoroutine(FlashRoutine(sizeMultiplier));
    }

    private IEnumerator FlashRoutine(float sizeMultiplier)
    {
        if (variants != null && variants.Length > 0)
            sr.sprite = variants[Random.Range(0, variants.Length)];

        transform.localScale = baseScale * Random.Range(scaleRange.x, scaleRange.y) * sizeMultiplier;
        sr.flipY = Random.value > 0.5f;
        sr.enabled = true;

        yield return new WaitForSecondsRealtime(visibleTime);   // real time so it still goes away during hit-stop
        sr.enabled = false;
    }

    private void OnDisable()
    {
        if (sr) sr.enabled = false;
    }
}
