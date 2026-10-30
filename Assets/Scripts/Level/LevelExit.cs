using UnityEngine;

[RequireComponent(typeof(Collider2D))]
// the exit portal, stays closed until GameManager opens it when everyone's dead
public class LevelExit : MonoBehaviour
{
    [Tooltip("Arrow / glow shown only once the exit is open.")]
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private AudioClip exitOpenClip;

    private bool isActive;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (activeVisual) activeVisual.SetActive(false);
    }

    public void Activate()
    {
        isActive = true;
        if (activeVisual) activeVisual.SetActive(true);
        AudioManager.Play(exitOpenClip, 0.8f, 0f);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isActive) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null && !player.IsDead)
            GameManager.Instance?.LoadNextLevel();
    }
}
