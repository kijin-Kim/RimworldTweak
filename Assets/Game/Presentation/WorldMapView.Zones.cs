using System.Collections.Generic;
using System.Linq;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RimworldTweak.Presentation
{
    public sealed partial class WorldMapView
    {
        private sealed class ZoneLayer
        {
            public Tilemap Map;
            public bool Drawn;
        }

        private readonly Dictionary<int, ZoneLayer> zoneLayers = new Dictionary<int, ZoneLayer>();
        private readonly List<int> deletedZoneIds = new List<int>();
        private Tile storageTile, growingTile;
        private static readonly Color StorageTint = new Color(.4f, .70f, 1f, .62f);
        private static readonly Color GrowingTint = new Color(.70f, .90f, .42f, .6f);

        private void DrawStorageZone(StorageZoneState zone)
        {
            if (!zoneLayers.TryGetValue(zone.Id, out var layer))
                layer = CreateZoneLayer(zone.Id, true, zone.Slots.Select(slot => slot.Position));
            DrawZoneCaption(layer, zone.Id, zone.Slots[0].Position, true);
        }

        private void DrawGrowingZone(GrowingZoneState zone)
        {
            if (!zoneLayers.TryGetValue(zone.Id, out var layer))
                layer = CreateZoneLayer(zone.Id, false, zone.Cells.Where(cell => cell.Exists).Select(cell => cell.Position));
            DrawZoneCaption(layer, zone.Id, zone.Cells[0].Position, false);
        }

        private void DrawZoneCaption(ZoneLayer layer, int id, GridPosition first, bool storage)
        {
            layer.Drawn = true;
            var key = storage ? "overlay.storage" : "overlay.growing";
            var mark = Draw("zone." + id, key, CellCenter(first), Color.white, -29000,
                storage ? "저장 구역" : "경작 구역", new Vector2(.94f, .94f));
            mark.Sprite.enabled = false;
        }

        // Slots and growing cells are fixed when the zone is created. A chunked tilemap
        // lets the renderer cull them without spawning or updating objects per cell.
        private ZoneLayer CreateZoneLayer(int id, bool storage, IEnumerable<GridPosition> cells)
        {
            if (storageTile == null)
            {
                storageTile = ScriptableObject.CreateInstance<Tile>();
                growingTile = ScriptableObject.CreateInstance<Tile>();
                RefreshZoneTiles();
            }
            var go = new GameObject("Zone tiles " + id, typeof(Tilemap), typeof(TilemapRenderer));
            go.layer = MapLayer;
            go.transform.SetParent(ground.transform.parent, false);
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = storage ? -29000 : -28900;
            if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;
            var layer = new ZoneLayer { Map = go.GetComponent<Tilemap>() };
            var positions = cells.Select(cell => new Vector3Int(cell.X, cell.Y, 0)).ToArray();
            var tiles = new TileBase[positions.Length];
            for (int i = 0; i < tiles.Length; ++i) tiles[i] = storage ? storageTile : growingTile;
            layer.Map.SetTiles(positions, tiles);
            zoneLayers.Add(id, layer);
            return layer;
        }

        private void RefreshZoneTiles()
        {
            if (storageTile == null) return;
            UpdateZoneTile(storageTile, "overlay.storage", StorageTint);
            UpdateZoneTile(growingTile, "overlay.growing", GrowingTint);
            foreach (var layer in zoneLayers.Values) layer.Map.RefreshAllTiles();
        }

        private void UpdateZoneTile(Tile tile, string key, Color tint)
        {
            var sprite = SpriteFor(key);
            var anchor = new Vector2(.5f, .5f);
            if (catalog != null && catalog.TryGet(key, out var visual) && visual.Sprite != null)
            {
                tint *= visual.Tint;
                anchor = visual.GroundAnchorNormalized;
            }
            var pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            tile.sprite = sprite;
            tile.color = tint;
            tile.transform = Matrix4x4.TRS((pivot - anchor) * .94f, Quaternion.identity,
                new Vector3(.94f / sprite.bounds.size.x, .94f / sprite.bounds.size.y, 1));
        }

        private void RemoveDeletedZoneTiles()
        {
            deletedZoneIds.Clear();
            foreach (var entry in zoneLayers)
                if (!entry.Value.Drawn) deletedZoneIds.Add(entry.Key);
            foreach (var id in deletedZoneIds)
            {
                zoneLayers[id].Map.gameObject.SetActive(false);
                Destroy(zoneLayers[id].Map.gameObject);
                zoneLayers.Remove(id);
            }
        }

        private void ClearZoneTiles()
        {
            foreach (var layer in zoneLayers.Values)
            {
                layer.Map.gameObject.SetActive(false);
                Destroy(layer.Map.gameObject);
            }
            zoneLayers.Clear();
        }

        private void DestroyZoneTiles()
        {
            if (storageTile != null) Destroy(storageTile);
            if (growingTile != null) Destroy(growingTile);
        }
    }
}
