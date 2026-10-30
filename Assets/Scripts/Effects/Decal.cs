using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
// stuff left on the floor (blood, splinters, puddles) with a random sprite, size and angle
public class Decal : MonoBehaviour
{
    [SerializeField] private Sprite[] variants;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.8f, 1.3f);

    [Tooltip("Keep the spawn rotation (e.g. blood spraying away from a shot) instead of a random one.")]
    [SerializeField] private bool keepSpawnRotation = true;

    [Tooltip("Random extra rotation added on top, in degrees.")]
    [SerializeField] private float rotationJitter = 25f;

    private void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (variants != null && variants.Length > 0)
            sr.sprite = variants[Random.Range(0, variants.Length)];

        float baseAngle = keepSpawnRotation ? transform.eulerAngles.z : Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, 0f, baseAngle + Random.Range(-rotationJitter, rotationJitter));
        transform.localScale *= Random.Range(scaleRange.x, scaleRange.y);
    }
}
