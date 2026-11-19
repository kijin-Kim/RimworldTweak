using System.Collections.Generic;
using RimworldTweak.Simulation;
using UnityEngine;

namespace RimworldTweak.Presentation
{
    public sealed partial class WorldMapView
    {
        private sealed class WorkBar
        {
            public SpriteRenderer Back;
            public SpriteRenderer Fill;
            public bool Drawn;
        }

        private readonly Dictionary<int, WorkBar> workBars = new Dictionary<int, WorkBar>();
        private readonly List<(int pawnId, WorkFeedback work)> workBarsThisFrame = new List<(int, WorkFeedback)>();
        private readonly List<Rect> workBarRects = new List<Rect>();

        private static void AnimateWork(Transform pose, Vector2 direction, WorkFeedbackKind kind,
            long tick, decimal pendingSeconds, int pawnId)
        {
            // Limit the integer time before converting to float, including in very long saves.
            var striking = kind == WorkFeedbackKind.Construct || kind == WorkFeedbackKind.Demolish || kind == WorkFeedbackKind.FellTree;
            var period = striking ? 11 : kind == WorkFeedbackKind.Cook ? 14 : 16;
            var phase = (float)(tick % period + pendingSeconds * 10m + pawnId % period) / period;
            var pulse = .5f - .5f * Mathf.Cos(phase * 2f * Mathf.PI);
            if (striking) pulse *= pulse;
            var side = direction.x == 0 ? 0 : Mathf.Sign(direction.x);
            var angle = -side * pulse * (striking ? 14f : 7f);
            // A frontal/back pose reads as a bow through foreshortening; a side pose leans toward its target.
            if (side == 0 && striking) angle = pulse * 6f;
            var squash = 1f - pulse * (kind == WorkFeedbackKind.Cook ? .08f : striking ? .10f : .16f);
            var scale = pose.localScale;
            scale.y *= squash;
            pose.localRotation = Quaternion.Euler(0, 0, angle);
            pose.localScale = scale;
        }

        private void LayoutWorkBars(int? selectedPawnId)
        {
            foreach (var bar in workBars.Values) bar.Drawn = false;
            workBarRects.Clear();
            if (selectedPawnId.HasValue)
                foreach (var entry in workBarsThisFrame)
                    if (entry.pawnId == selectedPawnId.Value) PlaceWorkBar(entry.pawnId, entry.work);
            foreach (var entry in workBarsThisFrame)
                if (entry.pawnId != selectedPawnId) PlaceWorkBar(entry.pawnId, entry.work);
            foreach (var bar in workBars.Values)
                if (!bar.Drawn && bar.Back.gameObject.activeSelf) bar.Back.gameObject.SetActive(false);
        }

        private void PlaceWorkBar(int pawnId, WorkFeedback work)
        {
            var screen = MapCamera.pixelRect;
            var point = CellCenter(work.TargetCell);
            if (!screen.Contains(MapCamera.WorldToScreenPoint(point))) return;
            var top = point.y + .5f;
            var targetKey = WorkTargetMark(work);
            if (marks.TryGetValue(targetKey, out var target) && target.Drawn)
                top = target.Label.transform.position.y;
            var pawnMark = marks["pawn." + pawnId];
            if (Vector2.Distance(pawnMark.Root.transform.position, point) < .5f)
                top = Mathf.Max(top, pawnMark.Label.transform.position.y);
            var pixelsPerUnit = MapCamera.pixelHeight / (2f * ZoomSize);
            var width = Mathf.Clamp(.8f * pixelsPerUnit, 22, 62);
            const float height = 6;
            var anchor = MapCamera.WorldToScreenPoint(new Vector3(point.x, top));
            var rect = new Rect(
                Mathf.Clamp(anchor.x - width / 2, screen.xMin, screen.xMax - width),
                Mathf.Clamp(anchor.y + 2, screen.yMin, screen.yMax - height), width, height);
            foreach (var placed in workBarRects)
            {
                if (!rect.Overlaps(placed)) continue;
                if (pixelsPerUnit < 22) return;
                rect.y = placed.yMax + 2;
                if (rect.yMax > screen.yMax) return;
            }
            workBarRects.Add(rect);
            if (!workBars.TryGetValue(pawnId, out var bar))
            {
                bar = new WorkBar();
                bar.Back = CreateBarSprite("Work bar " + pawnId, content, new Color(.08f, .08f, .08f, .96f), 32400);
                bar.Fill = CreateBarSprite("Fill", bar.Back.transform, new Color(.92f, .82f, .46f), 32401);
                workBars.Add(pawnId, bar);
            }
            bar.Drawn = true;
            if (!bar.Back.gameObject.activeSelf) bar.Back.gameObject.SetActive(true);
            var worldCenter = MapCamera.ScreenToWorldPoint(new Vector3(rect.center.x, rect.center.y, 10));
            bar.Back.transform.position = worldCenter;
            bar.Back.transform.localScale = new Vector3(width / pixelsPerUnit, height / pixelsPerUnit, 1);
            var progress = Mathf.Clamp01((float)work.Progress / work.Duration);
            var fillWidth = (1f - 2 / width) * progress;
            bar.Fill.transform.localScale = new Vector3(fillWidth, 1f - 2 / height, 1);
            bar.Fill.transform.localPosition = new Vector3(-.5f + 1f / width + fillWidth / 2, 0);
        }

        private static string WorkTargetMark(WorkFeedback work)
        {
            switch (work.Kind)
            {
                case WorkFeedbackKind.Construct: return "blueprint." + work.TargetId;
                case WorkFeedbackKind.Cook:
                case WorkFeedbackKind.Demolish: return "building." + work.TargetId;
                case WorkFeedbackKind.FellTree:
                case WorkFeedbackKind.GatherPlant: return "gather." + work.TargetId;
                default: return "crop." + work.TargetId;
            }
        }

        private SpriteRenderer CreateBarSprite(string name, Transform parent, Color tint, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.layer = MapLayer;
            go.transform.SetParent(parent, false);
            var sprite = go.GetComponent<SpriteRenderer>();
            sprite.sprite = fallback;
            sprite.color = tint;
            sprite.sortingOrder = order;
            if (spriteMaterial != null) sprite.sharedMaterial = spriteMaterial;
            return sprite;
        }

        private void ClearWorkBars()
        {
            foreach (var bar in workBars.Values)
            {
                bar.Back.gameObject.SetActive(false);
                Destroy(bar.Back.gameObject);
            }
            workBars.Clear();
            workBarRects.Clear();
            workBarsThisFrame.Clear();
        }
    }
}
