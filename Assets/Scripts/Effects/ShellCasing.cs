using UnityEngine;

// anything that gets thrown and skids to a stop: shells, fruit, swords, coins, planks
// moves itself instead of using physics, bounces off walls and stays on the floor once it stops
public class ShellCasing : MonoBehaviour
{
    [SerializeField] private Vector2 speedRange = new Vector2(3f, 5f);
    [SerializeField] private float friction = 9f;
    [SerializeField] private float spinSpeed = 900f;
    [SerializeField] private LayerMask wallMask;
    [SerializeField] private AudioClip[] landClips;
    [SerializeField] private float landVolume = 0.25f;

    private Vector2 velocity;
    private float spin;
    private bool landed;

    private void Start()
    {
        velocity = (Vector2)transform.right * Random.Range(speedRange.x, speedRange.y);
        spin = spinSpeed * (Random.value > 0.5f ? 1f : -1f);
    }

    private void Update()
    {
        if (landed) return;

        Vector2 step = velocity * Time.deltaTime;
        // look ahead so it bounces off walls instead of going through
        RaycastHit2D hit = Physics2D.Raycast(transform.position, velocity.normalized, step.magnitude + 0.05f, wallMask);
        if (hit.collider != null && !hit.collider.isTrigger)
        {
            velocity = Vector2.Reflect(velocity, hit.normal) * 0.5f;
            step = Vector2.zero;
        }
        transform.position += (Vector3)step;
        transform.Rotate(0f, 0f, spin * Time.deltaTime);

        // friction slows the movement and the spin until it lands
        float speed = Mathf.Max(0f, velocity.magnitude - friction * Time.deltaTime);
        velocity = velocity.normalized * speed;
        spin = Mathf.MoveTowards(spin, 0f, spinSpeed * 2f * Time.deltaTime);

        if (speed <= 0.01f)
        {
            landed = true;
            AudioManager.PlayRandom(landClips, landVolume, 0.2f);
        }
    }
}
