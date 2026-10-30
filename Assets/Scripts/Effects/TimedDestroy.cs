using UnityEngine;

// deletes short effects like impact sparks after a bit
public class TimedDestroy : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.5f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
