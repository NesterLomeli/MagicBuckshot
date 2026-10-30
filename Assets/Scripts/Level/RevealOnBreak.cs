using UnityEngine;

// goes on the big door in the last level. once the door gets shot open
// it shows "THE KING'S FEAST" with a sting and a camera shake
public class RevealOnBreak : MonoBehaviour
{
    [SerializeField] private string title = "THE KING'S FEAST";
    [SerializeField] private AudioClip stingClip;
    [SerializeField] private float shake = 0.4f;

    private Collider2D doorCollider;
    private bool revealed;

    private void Awake()
    {
        doorCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (revealed || doorCollider == null || doorCollider.enabled) return;
        revealed = true;
        HUD.Instance?.ShowLevelTitle(title);
        AudioManager.Play(stingClip, 0.8f, 0f);
        CameraFollow.Shake(shake, 0.35f);
        enabled = false;
    }
}
