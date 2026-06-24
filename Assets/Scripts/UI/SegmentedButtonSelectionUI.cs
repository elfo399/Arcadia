using UnityEngine;
using UnityEngine.UI;

public sealed class SegmentedButtonSelectionUI : MonoBehaviour
{
    [SerializeField] private Image leftFrame;
    [SerializeField] private Image centerFrame;
    [SerializeField] private Image rightFrame;
    [SerializeField] private Color selectionColor = new(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Vector2 selectionThickness = new(3f, 3f);

    private QuestSelectionFrameEffect leftEffect;
    private QuestSelectionFrameEffect centerEffect;
    private QuestSelectionFrameEffect rightEffect;
    private bool focused;

    private void Awake()
    {
        ResolveFrames();
        EnsureEffects();
        RefreshVisual();
    }

    private void OnDisable()
    {
        focused = false;
        RefreshVisual();
    }

    private void LateUpdate()
    {
        if (centerFrame == null)
            return;

        Color buttonTint = centerFrame.canvasRenderer.GetColor();
        if (leftFrame != null)
            leftFrame.canvasRenderer.SetColor(buttonTint);
        if (rightFrame != null)
            rightFrame.canvasRenderer.SetColor(buttonTint);
    }

    public void SetFocused(bool value)
    {
        if (focused == value)
            return;

        focused = value;
        EnsureEffects();
        RefreshVisual();
    }

    private void ResolveFrames()
    {
        if (centerFrame == null)
            centerFrame = GetComponent<Image>();
        if (leftFrame == null)
            leftFrame = transform.Find("LeftFrame")?.GetComponent<Image>();
        if (rightFrame == null)
            rightFrame = transform.Find("RightFrame")?.GetComponent<Image>();
    }

    private void EnsureEffects()
    {
        ResolveFrames();

        float horizontal = Mathf.Max(1f, selectionThickness.x);
        float vertical = Mathf.Max(1f, selectionThickness.y);
        leftEffect = ConfigureEffect(leftFrame, new Vector2(-horizontal, -vertical), new Vector2(-horizontal, vertical));
        centerEffect = ConfigureEffect(centerFrame, new Vector2(0f, -vertical), new Vector2(0f, vertical));
        rightEffect = ConfigureEffect(rightFrame, new Vector2(horizontal, -vertical), new Vector2(horizontal, vertical));
    }

    private QuestSelectionFrameEffect ConfigureEffect(Image frame, Vector2 firstOffset, Vector2 secondOffset)
    {
        if (frame == null)
            return null;

        QuestSelectionFrameEffect effect = frame.GetComponent<QuestSelectionFrameEffect>();
        if (effect == null)
            effect = frame.gameObject.AddComponent<QuestSelectionFrameEffect>();

        effect.Configure(selectionColor, firstOffset, secondOffset);
        return effect;
    }

    private void RefreshVisual()
    {
        if (leftEffect != null)
            leftEffect.enabled = focused;
        if (centerEffect != null)
            centerEffect.enabled = focused;
        if (rightEffect != null)
            rightEffect.enabled = focused;
    }
}
