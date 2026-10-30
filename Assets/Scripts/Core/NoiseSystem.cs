using System;
using UnityEngine;

// basically how enemies "hear" stuff. gunshots and breaking props call Emit()
// and every enemy listening checks if it was close enough to hear it
public static class NoiseSystem
{
    public static event Action<Vector2, float> NoiseMade;

    public static void Emit(Vector2 position, float radius)
    {
        NoiseMade?.Invoke(position, radius);
    }
}
