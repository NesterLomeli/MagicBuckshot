using UnityEngine;

// spins something forever (the portal swirl)
public class Spinner : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = -60f;

    private void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
    }
}
