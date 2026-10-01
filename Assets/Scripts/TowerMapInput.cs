using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AdamsHaven.Tower
{
    // Pointer input on the RawImage that shows the expedition map: tap, drag to pan, wheel or pinch to zoom.
    // Positions are reported as viewport coordinates (0..1) of the map image.
    public sealed class TowerMapInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler
    {
        public Action<Vector2> Tapped;
        public Action<Vector2> Dragged;             // viewport delta
        public Action<float, Vector2> Zoomed;       // factor, around viewport point

        private RectTransform rect;
        private Vector2 downAt;
        private bool dragging;
        private int pointers;
        private float lastPinch;

        private void Awake() { rect = (RectTransform)transform; }

        private Vector2 Viewport(PointerEventData e)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out local);
            var r = rect.rect;
            return new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
        }

        public void OnPointerDown(PointerEventData e) { pointers++; downAt = e.position; dragging = false; lastPinch = 0; }

        public void OnPointerUp(PointerEventData e)
        {
            pointers = Mathf.Max(0, pointers - 1);
            if (!dragging && pointers == 0 && (e.position - downAt).magnitude < 12f) Tapped?.Invoke(Viewport(e));
        }

        public void OnDrag(PointerEventData e)
        {
            if ((e.position - downAt).magnitude > 12f) dragging = true;
            if (!dragging || pointers > 1) return;
            var r = rect.rect;
            float scale = rect.lossyScale.x;
            Dragged?.Invoke(new Vector2(e.delta.x / (r.width * scale), e.delta.y / (r.height * rect.lossyScale.y)));
        }

        public void OnScroll(PointerEventData e)
        {
            if (Mathf.Abs(e.scrollDelta.y) > 0.01f) Zoomed?.Invoke(e.scrollDelta.y > 0 ? 1.15f : 1 / 1.15f, Viewport(e));
        }

        // Two-finger pinch (touch screens).
        private void Update()
        {
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            if (touch == null) return;
            var a = touch.touches[0]; var b = touch.touches[1];
            if (!a.isInProgress || !b.isInProgress) { lastPinch = 0; return; }
            float d = Vector2.Distance(a.position.ReadValue(), b.position.ReadValue());
            if (lastPinch > 0 && d > 1)
            {
                dragging = true;
                Vector2 mid = (a.position.ReadValue() + b.position.ReadValue()) / 2, local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, mid, null, out local);
                var r = rect.rect;
                Zoomed?.Invoke(d / lastPinch, new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height));
            }
            lastPinch = d;
        }
    }
}
