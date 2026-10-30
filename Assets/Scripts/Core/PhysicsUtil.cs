using UnityEngine;

// unity 6 renamed velocity to linearVelocity so everything sets velocity through here
public static class PhysicsUtil
{
    public static void SetVelocity(Rigidbody2D rb, Vector2 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = velocity;
#else
        rb.velocity = velocity;
#endif
    }
}
