using UnityEngine;

// follows Skeeter and leans toward the mouse so you can see where you're aiming
// also does screen shake (random rumble) and punch (quick kick in one direction)
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Tooltip("How far (0-1) the camera moves from the player toward the cursor.")]
    [Range(0f, 1f)] [SerializeField] private float lookAhead = 0.3f;

    [Tooltip("Maximum world units the camera may lean away from the player.")]
    [SerializeField] private float maxLookDistance = 3.5f;

    [Tooltip("Smaller = snappier follow.")]
    [SerializeField] private float smoothTime = 0.12f;

    private static float shakeStrength;
    private static float shakeTimeLeft;
    private static float shakeDuration;
    private static Vector2 punch;

    private Camera cam;
    private Vector3 velocity;
    private Vector3 basePosition;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        basePosition = transform.position;
        shakeTimeLeft = 0f;   // don't carry shake over from the last level
        punch = Vector2.zero;
    }

    private void Start()
    {
        if (target == null && PlayerController.Instance != null)
            target = PlayerController.Instance.transform;
    }

    // a weak shake can't cut off a stronger one that's still going
    public static void Shake(float strength, float duration)
    {
        if (strength >= shakeStrength || shakeTimeLeft <= 0f)
        {
            shakeStrength = strength;
            shakeTimeLeft = duration;
            shakeDuration = Mathf.Max(duration, 0.001f);
        }
    }

    public static void Punch(Vector2 direction, float amount)
    {
        punch = Vector2.ClampMagnitude(punch + direction.normalized * amount, 0.6f);
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            // lean part way toward the mouse but never so far that Skeeter goes off screen
            Vector2 lean = Vector2.ClampMagnitude((mouseWorld - target.position) * lookAhead, maxLookDistance);
            Vector3 desired = target.position + (Vector3)lean;
            desired.z = basePosition.z;

            basePosition = Vector3.SmoothDamp(basePosition, desired, ref velocity, smoothTime);
        }

        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimeLeft > 0f)
        {
            // real time so shake still ends during hit-stop and bullet time, fades out as it goes
            shakeTimeLeft -= Time.unscaledDeltaTime;
            float fade = Mathf.Clamp01(shakeTimeLeft / shakeDuration);
            shakeOffset = (Vector3)(Random.insideUnitCircle * shakeStrength * fade);
            if (shakeTimeLeft <= 0f) shakeStrength = 0f;
        }

        shakeOffset += (Vector3)punch;
        // punch springs back to the middle fast
        punch = Vector2.Lerp(punch, Vector2.zero, 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime));

        transform.position = basePosition + shakeOffset;
    }
}
