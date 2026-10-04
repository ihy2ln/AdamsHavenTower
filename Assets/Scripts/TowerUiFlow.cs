using UnityEngine;

// TT 10.4.2: panels and popups ease in instead of popping: a short fade and a small slide from the side they live on.
[RequireComponent(typeof(RectTransform))]
public sealed class TowerUiFlow : MonoBehaviour
{
    public Vector2 from = new Vector2(0, -18);   // offset the panel starts at, relative to its resting place
    public float seconds = 0.16f;

    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 rest;
    private bool hasRest;
    private float t;

    public static TowerUiFlow Add(Component target, Vector2 from)
    {
        var flow = target.gameObject.GetComponent<TowerUiFlow>();   // not ??: Unity's destroyed-object null is not C# null
        if (flow == null) flow = target.gameObject.AddComponent<TowerUiFlow>();
        flow.from = from;
        return flow;
    }

    private void OnEnable()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        if (!hasRest) { rest = rect.anchoredPosition; hasRest = true; }
        t = 0;
        Apply(0);
    }

    private void OnDisable()
    {
        if (rect != null && hasRest) rect.anchoredPosition = rest;
        if (group != null) group.alpha = 1;
    }

    private void Update()
    {
        if (t >= 1) return;
        t = Mathf.Min(1, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
        Apply(1 - (1 - t) * (1 - t));   // ease out
    }

    private void Apply(float k)
    {
        rect.anchoredPosition = rest + from * (1 - k);
        group.alpha = k;
    }
}
