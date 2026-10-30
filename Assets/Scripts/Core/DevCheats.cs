using UnityEngine;

// testing shortcuts: F1 skip level, F2 god mode, F3 restart, F4 kill everyone
// only works in the editor or a dev build
public class DevCheats : MonoBehaviour
{
    public static bool GodMode { get; private set; }

    private void Update()
    {
        // isDebugBuild is false in a real build so players can't cheat
        if (!Debug.isDebugBuild || GameManager.Instance == null) return;

        if (Input.GetKeyDown(KeyCode.F1))
            GameManager.Instance.LoadNextLevel();

        if (Input.GetKeyDown(KeyCode.F2))
        {
            GodMode = !GodMode;
            HUD.Instance?.ShowHint(GodMode ? "DEV: GOD MODE ON" : "DEV: GOD MODE OFF", 1.5f);
        }

        if (Input.GetKeyDown(KeyCode.F3))
            GameManager.Instance.RestartLevel();

        if (Input.GetKeyDown(KeyCode.F4))
        {
            foreach (EnemyAI enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                if (enemy.enabled) enemy.TakeHit(Vector2.right, 999);
        }
    }
}
