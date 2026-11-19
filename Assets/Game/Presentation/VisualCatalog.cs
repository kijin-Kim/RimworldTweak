using System;
using System.Collections.Generic;
using RimworldTweak.Simulation;
using UnityEngine;

namespace RimworldTweak.Presentation
{
    [Serializable]
    public sealed class VisualDefinition
    {
        [SerializeField] private string key;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Vector2 worldSize = Vector2.one;
        [SerializeField] private Vector2 groundAnchorNormalized = new Vector2(.5f, .5f);
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private bool isFinalArt;
        [SerializeField] private string source;

        public string Key => key;
        public Sprite Sprite => sprite;
        public Vector2 WorldSize => worldSize;
        public Vector2 GroundAnchorNormalized => groundAnchorNormalized;
        public Color Tint => tint;
        public bool IsFinalArt => isFinalArt;
        public string Source => source;

        public VisualDefinition(string key, Sprite sprite, Vector2 size, Vector2 groundAnchor,
            Color tint, bool isFinalArt, string source)
        {
            this.key = key; this.sprite = sprite; worldSize = size;
            groundAnchorNormalized = groundAnchor; this.tint = tint;
            this.isFinalArt = isFinalArt; this.source = source;
            Validate();
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A visual key is required.");
            if (sprite == null) throw new ArgumentException("Missing sprite: " + key);
            if (!Finite(worldSize.x) || !Finite(worldSize.y) || worldSize.x <= 0 || worldSize.y <= 0)
                throw new ArgumentException("Invalid world size: " + key);
            if (!Finite(groundAnchorNormalized.x) || !Finite(groundAnchorNormalized.y) ||
                groundAnchorNormalized.x < 0 || groundAnchorNormalized.x > 1 ||
                groundAnchorNormalized.y < 0 || groundAnchorNormalized.y > 1)
                throw new ArgumentException("Invalid ground anchor: " + key);
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Missing visual provenance: " + key);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // The anchor is authored, not guessed from alpha. Sprite pivot and picture height may change.
        public Vector2 PivotOffset => new Vector2(
            (sprite.pivot.x / sprite.rect.width - groundAnchorNormalized.x) * worldSize.x,
            (sprite.pivot.y / sprite.rect.height - groundAnchorNormalized.y) * worldSize.y);
        public Vector3 Scale => new Vector3(worldSize.x / sprite.bounds.size.x,
            worldSize.y / sprite.bounds.size.y, 1);
    }

    [CreateAssetMenu(menuName = "RimworldTweak/Visual catalog")]
    public sealed class VisualCatalog : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Game/Content/Visuals/DefaultVisualCatalog.asset";
        [SerializeField] private VisualDefinition[] entries = Array.Empty<VisualDefinition>();
        public IReadOnlyList<VisualDefinition> Entries => Array.AsReadOnly(entries);

        public bool TryGet(string key, out VisualDefinition visual)
        {
            foreach (var entry in entries)
                if (entry != null && string.Equals(entry.Key, key, StringComparison.Ordinal))
                { visual = entry; return entry.Sprite != null; }
            visual = null; return false;
        }

        public void SetEntries(IEnumerable<VisualDefinition> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var proposed = new List<VisualDefinition>(values);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in proposed)
            {
                if (entry == null) throw new ArgumentException("Null visual entry.");
                entry.Validate();
                if (!keys.Add(entry.Key)) throw new ArgumentException("Duplicate visual key: " + entry.Key);
            }
            entries = proposed.ToArray();
        }

        public static string ResourceKey(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Wood: return "item.wood";
                case ResourceKind.Ingredients: return "item.ingredients";
                case ResourceKind.Medicine: return "item.medicine";
                case ResourceKind.Meal: return "item.meal";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static string BuildingKey(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Wall: return "building.wall";
                case BuildingKind.Door: return "building.door";
                case BuildingKind.Bed: return "building.bed";
                case BuildingKind.Stove: return "building.stove";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static string CropKey(CropState crop) => crop == null || !crop.Exists ? null : CropKey(crop.Stage, crop.GrowthTicks);
        public static string CropKey(CropStage stage, int growthTicks)
        {
            if (stage == CropStage.Empty) return null;
            if (stage == CropStage.Mature) return "crop.mature";
            if (stage != CropStage.Growing) throw new ArgumentOutOfRangeException(nameof(stage));
            return growthTicks < FarmingRules.GrowthDuration / 2 ? "crop.young" : "crop.growing";
        }

        public static string PawnKey(Vector2 direction) => Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? "pawn.side" : direction.y > 0 ? "pawn.back" : "pawn.front";

        // Bits use north=1, east=2, south=4, west=8. No gameplay state is changed.
        public static string WallKey(int mask)
        {
            if (mask < 0 || mask > 15) throw new ArgumentOutOfRangeException(nameof(mask));
            return "building.wall." + mask;
        }
    }
}
