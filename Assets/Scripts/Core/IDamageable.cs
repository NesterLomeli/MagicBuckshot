using UnityEngine;

// anything that can get shot: the player, enemies, props and the door
// bullets just look for this, so anything new that breaks only needs a TakeHit
public interface IDamageable
{
    void TakeHit(Vector2 hitDirection, int damage);
}
