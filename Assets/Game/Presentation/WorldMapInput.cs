using System;
using System.Collections.Generic;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RimworldTweak.Presentation
{
    /// <summary>Routes actual pointer input only after UI hit testing. No simulation state is owned here.</summary>
    public sealed class WorldMapInput : MonoBehaviour
    {
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private WorldMapView view;
        private Func<bool> blocked, toolActive, rectangleTool;
        private Action<GridPosition, bool> click;
        private Action<GridPosition, GridPosition> rectangle;
        private Action cancelTool;
        private bool leftCaptured, middleCaptured, suppressUntilRelease, suppressKeysUntilRelease;
        private GridPosition start;
        public GridPosition? HoverPosition { get; private set; }
        public GridPosition? DragStart => leftCaptured && (rectangleTool?.Invoke() ?? false) ? start : (GridPosition?)null;
        public bool IsCapturing => leftCaptured || middleCaptured;
        public Action<GridPosition?> Hovered { get; set; }
        public float PanSpeed = 6;

        public void Configure(WorldMapView map, Func<bool> inputBlocked, Func<bool> isToolActive,
            Func<bool> isRectangleTool, Action<GridPosition, bool> onClick,
            Action<GridPosition, GridPosition> onRectangle, Action onCancelTool)
        {
            CancelGesture(); view = map; blocked = inputBlocked; toolActive = isToolActive;
            rectangleTool = isRectangleTool; click = onClick; rectangle = onRectangle; cancelTool = onCancelTool;
        }

        public void CancelGesture()
        {
            leftCaptured = middleCaptured = false;
            var mouse = Mouse.current;
            suppressUntilRelease = mouse != null && (mouse.leftButton.isPressed || mouse.middleButton.isPressed || mouse.rightButton.isPressed);
            var keyboard = Keyboard.current;
            suppressKeysUntilRelease = keyboard != null && (keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed);
            HoverPosition = null;
            if (view != null) view.SetPreview(null, null, false);
            Hovered?.Invoke(null);
        }

        public bool IsPointerOverUI(Vector2 screen)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, uiHits);
            foreach (var hit in uiHits)
                if (hit.module is UnityEngine.UI.GraphicRaycaster) return true;
            return false;
        }

        private void Update()
        {
            if (view == null || view.World == null) return;
            var mouse = Mouse.current;
            if (blocked?.Invoke() ?? false) { CancelGesture(); return; }
            if (mouse != null)
            {
                if (suppressUntilRelease)
                {
                    if (!mouse.leftButton.isPressed && !mouse.middleButton.isPressed && !mouse.rightButton.isPressed) suppressUntilRelease = false;
                    return;
                }
                var screen = mouse.position.ReadValue();
                var ui = IsPointerOverUI(screen);
                var inside = view.TryScreenToGrid(screen, out var cell);
                var onMap = !ui && inside;
                HoverPosition = onMap ? cell : (GridPosition?)null;
                Hovered?.Invoke(HoverPosition);
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    leftCaptured = middleCaptured = false;
                    view.SetPreview(null, null, false);
                    if (!ui)
                    {
                        if (toolActive?.Invoke() ?? false) cancelTool?.Invoke();
                        else if (onMap) click?.Invoke(cell, true);
                    }
                }
                if (mouse.leftButton.wasPressedThisFrame && onMap) { leftCaptured = true; start = cell; }
                if (mouse.leftButton.wasReleasedThisFrame && leftCaptured)
                {
                    leftCaptured = false;
                    if (onMap)
                    {
                        if (rectangleTool?.Invoke() ?? false) rectangle?.Invoke(start, cell);
                        else click?.Invoke(cell, false);
                    }
                    view.SetPreview(null, null, false);
                }
                if (mouse.middleButton.wasPressedThisFrame && onMap) middleCaptured = true;
                if (mouse.middleButton.wasReleasedThisFrame) middleCaptured = false;
                if (middleCaptured && !ui && mouse.middleButton.isPressed)
                {
                    var pixelsToWorld = 2 * view.ZoomSize / Math.Max(1, view.MapCamera.pixelHeight);
                    view.Pan(-mouse.delta.ReadValue() * pixelsToWorld);
                }
                if (onMap && !leftCaptured && !middleCaptured)
                {
                    var wheel = mouse.scroll.ReadValue().y;
                    if (Math.Abs(wheel) > .01f) view.Zoom(Mathf.Clamp(wheel / 120f, -3, 3), screen);
                }
            }
            var keyboard = Keyboard.current;
            if (keyboard == null || leftCaptured || middleCaptured || suppressUntilRelease) return;
            if (suppressKeysUntilRelease)
            {
                if (!keyboard.wKey.isPressed && !keyboard.aKey.isPressed && !keyboard.sKey.isPressed && !keyboard.dKey.isPressed) suppressKeysUntilRelease = false;
                return;
            }
            var delta = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            if (delta != Vector2.zero) view.Pan(delta.normalized * PanSpeed * Time.unscaledDeltaTime);
        }
        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) CancelGesture(); }
        private void OnDisable() { CancelGesture(); }
    }
}
