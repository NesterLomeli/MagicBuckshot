using TMPro;
using UnityEngine;
using UnityEngine.Events;

// hotline miami style menu. W/S or arrows to move, enter/space to pick, mouse works too
// each item runs whatever's hooked up in the inspector (like TitleScreen.StartGame)
public class MenuSelector : MonoBehaviour
{
    public TMP_Text[] items;
    public UnityEvent[] actions;
    public RectTransform arrow;

    [SerializeField] private Color selectedColor = new Color(0.88f, 0.69f, 0.23f);
    [SerializeField] private Color normalColor = new Color(0.94f, 0.91f, 0.84f);
    [Tooltip("Arrow position relative to the selected item.")]
    [SerializeField] private Vector2 arrowOffset = new Vector2(-80f, 0f);
    [SerializeField] private AudioClip moveClip;

    private int index;
    private Vector3 lastMouse;

    private void Start()
    {
        lastMouse = Input.mousePosition;
        Select(0, playSound: false);
    }

    private void Update()
    {
        if (items == null || items.Length == 0) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) Select(index - 1);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) Select(index + 1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) Activate();

        // only let the mouse change the selection when it actually moves so it doesn't fight the keyboard
        bool mouseMoved = (Input.mousePosition - lastMouse).sqrMagnitude > 1f;
        lastMouse = Input.mousePosition;
        int hovered = ItemUnderMouse();
        if (mouseMoved && hovered >= 0 && hovered != index) Select(hovered);
        if (Input.GetMouseButtonDown(0) && hovered >= 0) { Select(hovered, playSound: false); Activate(); }
    }

    private int ItemUnderMouse()
    {
        for (int i = 0; i < items.Length; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(items[i].rectTransform, Input.mousePosition, null))
                return i;
        return -1;
    }

    private void Select(int i, bool playSound = true)
    {
        index = (i % items.Length + items.Length) % items.Length;   // wraps around from the top to the bottom
        for (int k = 0; k < items.Length; k++)
            items[k].color = k == index ? selectedColor : normalColor;

        if (arrow) arrow.anchoredPosition = items[index].rectTransform.anchoredPosition + arrowOffset;
        if (playSound) AudioManager.Play(moveClip, 0.6f, 0.05f);
    }

    private void Activate()
    {
        if (actions != null && index < actions.Length) actions[index]?.Invoke();
    }
}
