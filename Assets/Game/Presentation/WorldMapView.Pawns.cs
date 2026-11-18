using System;
using System.Collections.Generic;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace RimworldTweak.Presentation
{
    public sealed partial class WorldMapView
    {
        // The group sorts as one object in the world. Only the children use clothing order.
        private sealed class PawnVisual
        {
            public readonly SortingGroup Group;
            public readonly Transform Facing;
            public readonly List<SpriteRenderer> Parts;
            public readonly PawnAppearance Appearance;
            public readonly VisualDefinition[][] Directions = new VisualDefinition[3][];
            public readonly VisualDefinition[] Fallbacks = new VisualDefinition[3];
            public VisualCatalog Catalog;
            public int DisplayedDirection = -1;
            public Vector2 BodySize;
            public float Top;

            public PawnVisual(Mark mark, int id)
            {
                var pose = new GameObject("Pose", typeof(SortingGroup));
                pose.layer = MapLayer;
                pose.transform.SetParent(mark.Root.transform, false);
                Group = pose.GetComponent<SortingGroup>();
                Facing = new GameObject("Facing").transform;
                Facing.gameObject.layer = MapLayer;
                Facing.SetParent(pose.transform, false);
                mark.Sprite.transform.SetParent(Facing, false);
                Parts = new List<SpriteRenderer>(5) { mark.Sprite };
                Appearance = PawnAppearance.ForId(id);
            }

            public void Invalidate()
            {
                Array.Clear(Directions, 0, Directions.Length);
                Array.Clear(Fallbacks, 0, Fallbacks.Length);
                DisplayedDirection = -1;
            }
        }

        private void DrawPawn(ColonistState pawn, Vector2 position, Vector2 direction, bool inBed, bool sleeping,
            string caption)
        {
            var mark = GetMark("pawn." + pawn.Id);
            if (mark.Pawn == null) mark.Pawn = new PawnVisual(mark, pawn.Id);
            var visual = mark.Pawn;
            if (visual.Catalog != catalog)
            {
                visual.Invalidate();
                visual.Catalog = catalog;
            }
            var directionKey = VisualCatalog.PawnKey(direction);
            var index = directionKey == "pawn.front" ? 0 : directionKey == "pawn.back" ? 1 : 2;
            if (visual.DisplayedDirection != index)
            {
                if (visual.Directions[index] == null)
                {
                    var side = index == 0 ? "front" : index == 1 ? "back" : "side";
                    if (catalog != null && visual.Appearance.TryResolve(catalog, side, out var layers))
                        visual.Directions[index] = layers;
                    else
                    {
                        visual.Directions[index] = Array.Empty<VisualDefinition>();
                        if (catalog != null) catalog.TryGet(directionKey, out visual.Fallbacks[index]);
                    }
                }
                ApplyPawnArt(visual, index, PawnColor(pawn.Id));
            }

            mark.Root.transform.position = position + (inBed ? new Vector2(0, .04f) : Vector2.zero);
            var pose = visual.Group.transform;
            visual.Group.sortingOrder = Sort(position.y, 40);
            visual.Facing.localScale = new Vector3(direction.x < 0 ? -1 : 1, 1, 1);
            pose.localRotation = Quaternion.Euler(0, 0, sleeping && !inBed ? 75 : 0);
            pose.localScale = inBed
                ? new Vector3(.72f / visual.BodySize.x, .72f / visual.BodySize.y, 1)
                : Vector3.one;
            var labelHeight = visual.Top * pose.localScale.y;
            SetMarkLabel(mark, caption, PawnColor(pawn.Id), labelHeight);
        }

        private void ApplyPawnArt(PawnVisual visual, int index, Color identityColor)
        {
            var layers = visual.Directions[index];
            var modular = layers.Length > 0;
            var count = modular ? layers.Length : 1;
            visual.Top = 0;
            for (var i = 0; i < count; i++)
            {
                if (i == visual.Parts.Count)
                {
                    var child = new GameObject("Part " + i, typeof(SpriteRenderer));
                    child.layer = MapLayer;
                    child.transform.SetParent(visual.Facing, false);
                    var added = child.GetComponent<SpriteRenderer>();
                    if (spriteMaterial != null) added.sharedMaterial = spriteMaterial;
                    visual.Parts.Add(added);
                }
                var part = visual.Parts[i];
                var definition = modular ? layers[i] : visual.Fallbacks[index];
                var sprite = definition?.Sprite ?? fallback;
                var size = definition?.WorldSize ?? new Vector2(.85f, .85f);
                var anchor = definition?.GroundAnchorNormalized ?? new Vector2(.5f, .5f);
                part.enabled = true;
                part.sprite = sprite;
                part.sortingOrder = i;
                part.color = (definition?.Tint ?? Color.white) * (modular ? Color.white : identityColor);
                part.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
                var pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
                part.transform.localPosition = Vector2.Scale(pivot - anchor, size);
                if (i == 0) visual.BodySize = size;
                visual.Top = Mathf.Max(visual.Top, size.y * (1 - anchor.y));
            }
            for (var i = count; i < visual.Parts.Count; i++)
            {
                visual.Parts[i].enabled = false;
                visual.Parts[i].sprite = null;
            }
            visual.DisplayedDirection = index;
        }
    }
}
