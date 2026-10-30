using UnityEngine;

// when a stand or shelf breaks this throws the pieces (fruit, swords, coins, pages)
// out the way the bullet was going, leaves a mess on the floor, then deletes itself
public class DebrisBurst : MonoBehaviour
{
    [SerializeField] private GameObject[] piecePrefabs;
    [SerializeField] private Vector2Int countRange = new Vector2Int(4, 6);
    [Tooltip("Pieces fly within this many degrees either side of the shot direction.")]
    [SerializeField] private float spreadAngle = 60f;
    [Tooltip("Optional floor decal left where the stand was.")]
    [SerializeField] private GameObject decalPrefab;

    private void Start()
    {
        if (decalPrefab) Instantiate(decalPrefab, transform.position, Quaternion.identity);

        if (piecePrefabs != null && piecePrefabs.Length > 0)
        {
            float baseAngle = transform.eulerAngles.z;   // BreakableProp turns this to face the shot direction
            int count = Random.Range(countRange.x, countRange.y + 1);
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = piecePrefabs[Random.Range(0, piecePrefabs.Length)];
                float angle = baseAngle + Random.Range(-spreadAngle, spreadAngle);
                Vector3 offset = (Vector3)(Random.insideUnitCircle * 0.3f);
                Instantiate(prefab, transform.position + offset, Quaternion.Euler(0f, 0f, angle));
            }
        }
        Destroy(gameObject);
    }
}
