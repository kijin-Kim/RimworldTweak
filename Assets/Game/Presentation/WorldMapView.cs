using System.Linq;
using System;
using System.Collections.Generic;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RimworldTweak.Presentation
{
    /// <summary>Read-only projection of a simulation into world-space sprites. Never owns game rules.</summary>
    public sealed partial class WorldMapView : MonoBehaviour
    {
        public const int MapLayer = 30;
        private sealed class Mark
        {
            public GameObject Root;
            public SpriteRenderer Sprite;
            public TextMesh Label;
            public TextMesh Shadow;
            public bool Drawn;
            public PawnVisual Pawn;
        }

        private readonly Dictionary<string, Mark> marks = new Dictionary<string, Mark>();
        private readonly List<string> removedMarks = new List<string>();
        private readonly Dictionary<int, Vector2> pawnDirections = new Dictionary<int, Vector2>();
        private Transform content;
        private Tilemap ground;
        private Tile soilTile, blockedTile;
        private LineRenderer preview, route;
        private SpriteRenderer selection;
        private Sprite selectionSprite;
        private Texture2D selectionTexture;
        private static readonly Vector2 PawnSelectionOffset = new Vector2(0, .38f);
        private readonly Dictionary<int, SpriteRenderer> pawnSelections = new Dictionary<int, SpriteRenderer>();
        private SpriteRenderer placementGhost;
        private Material spriteMaterial;
        private Sprite fallback;
        private Texture2D fallbackTexture;
        private Font labelFont;
        private WorldState displayedWorld;
        private long terrainRevision = -1;
        private HashSet<GridPosition> renderedBlocked = new HashSet<GridPosition>();
        private HashSet<GridPosition> nextBlocked = new HashSet<GridPosition>();
        public int FullTerrainUpdates { get; private set; }
        public int ChangedTerrainCells { get; private set; }
        private VisualCatalog catalog;
        private Rect viewport = new Rect(0, 0, 1, 1);

        public Camera MapCamera { get; private set; }
        public WorldState World => displayedWorld;
        public float MinimumZoom { get; private set; } = 2f;
        public float MaximumZoom { get; private set; } = 12f;
        public float ZoomSize => MapCamera == null ? 0 : MapCamera.orthographicSize;
        public int VisibleEntityCount { get; private set; }
        public int MarkerObjectCount => marks.Count;
        public Rect Viewport => viewport;
        private readonly List<Rect> placedLabels = new List<Rect>();

        public void Initialize(VisualCatalog visuals = null)
        {
            catalog = visuals;
            if (MapCamera != null) return;
            content = new GameObject("World map content").transform;
            content.SetParent(transform, false);
            content.gameObject.layer = MapLayer;
            var cameraObject = new GameObject("Settlement map camera");
            cameraObject.transform.SetParent(transform, false);
            MapCamera = cameraObject.AddComponent<Camera>();
            MapCamera.orthographic = true;
            MapCamera.transform.position = new Vector3(0, 0, -10);
            MapCamera.cullingMask = 1 << MapLayer;
            MapCamera.clearFlags = CameraClearFlags.SolidColor;
            MapCamera.backgroundColor = new Color(.08f, .11f, .10f);
            MapCamera.depth = 5;
            MapCamera.nearClipPlane = .1f;
            MapCamera.farClipPlane = 30;
            MapCamera.rect = viewport;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) spriteMaterial = new Material(shader);
            fallbackTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            fallbackTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            fallbackTexture.Apply();
            fallback = Sprite.Create(fallbackTexture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 2);
            labelFont = Resources.Load<Font>("UI/NotoSansCJKkr-Regular");
            var grid = new GameObject("Ground grid", typeof(Grid));
            grid.transform.SetParent(content, false);
            grid.layer = MapLayer;
            var tileObject = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
            tileObject.transform.SetParent(grid.transform, false);
            tileObject.layer = MapLayer;
            ground = tileObject.GetComponent<Tilemap>();
            var tileRenderer = tileObject.GetComponent<TilemapRenderer>();
            tileRenderer.sortingOrder = -30000;
            if (spriteMaterial != null) tileRenderer.sharedMaterial = spriteMaterial;
            soilTile = ScriptableObject.CreateInstance<Tile>();
            blockedTile = ScriptableObject.CreateInstance<Tile>();
            UpdateGroundVisuals();
            BuildSelectionSprite();
            selection = CreateSelection("Selection", new Color(.94f, .96f, .77f));
            preview = CreateLine("Placement preview", Color.cyan, 32001, .045f);
            var ghost = new GameObject("Building ghost", typeof(SpriteRenderer));
            ghost.layer = MapLayer;
            ghost.transform.SetParent(content, false);
            placementGhost = ghost.GetComponent<SpriteRenderer>();
            placementGhost.sortingOrder = 32000;
            if (spriteMaterial != null) placementGhost.sharedMaterial = spriteMaterial;
            placementGhost.enabled = false;
            route = CreateLine("Selected route", new Color(.8f, .85f, 1, .7f), 31000, .025f);
        }

        public void SetCatalog(VisualCatalog visuals)
        {
            catalog = visuals;
            foreach (var mark in marks.Values) mark.Pawn?.Invalidate();
            if (soilTile != null)
            {
                UpdateGroundVisuals();
            }
            if (ground != null) ground.RefreshAllTiles();
            RefreshZoneTiles();
        }

        private void UpdateGroundVisuals()
        {
            soilTile.sprite = SpriteFor("terrain.soil");
            soilTile.color = HasVisual("terrain.soil") ? Color.white : new Color(.28f,.34f,.25f);
            blockedTile.sprite = SpriteFor("terrain.blocked");
            blockedTile.color = new Color(.26f,.29f,.28f);
            foreach(var tile in new[]{soilTile,blockedTile})
            {
                var size=tile.sprite.bounds.size;
                tile.transform=Matrix4x4.Scale(new Vector3(1/Math.Max(.001f,size.x),1/Math.Max(.001f,size.y),1));
            }
        }

        public void Bind(WorldState world)
        {
            Initialize(catalog);
            if (ReferenceEquals(displayedWorld, world)) return;
            ClearMarks();
            ClearWorkBars();
            ClearZoneTiles();
            foreach(var line in pawnSelections.Values)Destroy(line.gameObject);
            pawnSelections.Clear();
            displayedWorld = world;
            pawnDirections.Clear();
            terrainRevision = -1;
            renderedBlocked.Clear();
            nextBlocked.Clear();
            FullTerrainUpdates = ChangedTerrainCells = 0;
            selection.enabled = false;
            preview.positionCount = route.positionCount = 0;
            placementGhost.enabled = false;
            ground.ClearAllTiles();
            if (world == null) return;
            MinimumZoom = Math.Min(2f, Math.Max(1f, Math.Min(world.Width, world.Height) * .25f));
            MaximumZoom = Math.Max(MinimumZoom, Math.Max(world.Height * .6f, world.Width / CameraAspect * .6f));
            MapCamera.orthographicSize = Mathf.Clamp(Math.Max(world.Height * .52f, world.Width / CameraAspect * .52f), MinimumZoom, MaximumZoom);
            MapCamera.transform.position = new Vector3(world.Width / 2f, world.Height / 2f, -10);
            if (world.Width == WorkScenario.SettlementSize && world.Height == WorkScenario.SettlementSize && world.Colonists.Count > 0)
            {
                MapCamera.orthographicSize = Mathf.Clamp(8, MinimumZoom, MaximumZoom);
                Focus(world.Colonists[0].Position);
            }
            RebuildGround();
        }

        public void Refresh(WorldState world, int? selectedPawnId, int? selectedObjectId, bool paused, decimal pendingSeconds = 0m)
        {
            Bind(world);
            if (world == null) return;
            // The remainder belongs to simulation time. It stays fixed when the clock is paused.
            var tickFraction = (float)(pendingSeconds / SimulationSession.SecondsPerTick);
            if (terrainRevision != world.MapRevision) UpdateBlockedCells();
            foreach (var mark in marks.Values) mark.Drawn = false;
            workBarsThisFrame.Clear();
            foreach (var zoneTiles in zoneLayers.Values) zoneTiles.Drawn = false;
            VisibleEntityCount = 0;
            foreach (var zone in world.StorageZones)
            {
                if (!zone.Exists) continue;
                if (zone.Slots.Count > 0) DrawStorageZone(zone);
            }
            foreach (var zone in world.GrowingZones)
            {
                if (!zone.Exists) continue;
                if (zone.Cells.Count > 0) DrawGrowingZone(zone);
            }
            RemoveDeletedZoneTiles();
            foreach (var node in world.Gatherables)
                if (!node.Depleted)
                    Draw("gather." + node.Id, node.Kind == GatherKind.Tree ? "tree" : "plant.gather", CellCenter(node.Position),
                        node.Designated ? new Color(1, .85f, .65f) : Color.white, Sort(CellCenter(node.Position).y, 20), node.Designated ? "채집" : null);
            foreach(var weapon in world.Weapons)
                if(!weapon.HolderId.HasValue)
                    Draw("weapon."+weapon.Id,"preview."+weapon.Definition.Id,CellCenter(weapon.Position),Color.white,Sort(weapon.Position.Y+.5f,25),
                        weapon.ReservedById.HasValue?"예약 #"+weapon.ReservedById:null);
            foreach (var enemy in world.Raiders)
                DrawRaider(enemy, world);
            foreach (var bed in world.Beds)
                if (bed.Exists && !HasBuilding(world, bed.Id)) Draw("bed." + bed.Id, "building.bed", CellCenter(bed.Position), Color.white, Sort(CellCenter(bed.Position).y, 10), null);
            foreach (var building in world.Buildings)
                if (building.Exists)
                {
                    var key = VisualCatalog.BuildingKey(building.Kind);
                    if (building.Kind == BuildingKind.Wall && HasVisual(key + "." + WallMask(world, building.Position))) key += "." + WallMask(world, building.Position);
                    Draw((building.InitialPlacement && building.Kind == BuildingKind.Bed ? "bed." : "building.") + building.Id, key, CellCenter(building.Position), building.DemolitionDesignated ? new Color(1, .55f, .45f) : Color.white,
                        Sort(CellCenter(building.Position).y, 10), building.DemolitionDesignated ? "철거" : null);
                }
            foreach (var blueprint in world.Blueprints)
                if (blueprint.Exists) Draw("blueprint." + blueprint.Id, VisualCatalog.BuildingKey(blueprint.Kind), CellCenter(blueprint.Position),
                    new Color(.4f, .8f, 1, .55f), Sort(CellCenter(blueprint.Position).y, 12), blueprint.Progress + "/" + blueprint.Duration);
            foreach (var crop in world.Crops)
                if (crop.Exists && crop.Stage != CropStage.Empty)
                    Draw("crop." + crop.Id, VisualCatalog.CropKey(crop), CellCenter(crop.Position), Color.white, Sort(CellCenter(crop.Position).y, 15), null);
            foreach (var item in world.Resources)
                if (item.Exists && item.Amount > 0)
                    Draw("resource." + item.Id, VisualCatalog.ResourceKey(item.Kind), CellCenter(item.Position), Color.white, Sort(CellCenter(item.Position).y, 25), "×" + item.Amount);
            foreach (var pawn in world.Colonists)
            {
                var position = PawnPosition(pawn, tickFraction, world.NeedsEnabled);
                if (pawn.Status == MovementStatus.Moving && pawn.Path.Count > 0)
                    pawnDirections[pawn.Id] = new Vector2(pawn.Path[0].X - pawn.Position.X, pawn.Path[0].Y - pawn.Position.Y);
                var direction = pawnDirections.TryGetValue(pawn.Id, out var facing) ? facing : Vector2.down;
                var working = world.TryGetWorkFeedback(pawn.Id, out var work);
                if (working)
                {
                    direction = new Vector2(work.TargetCell.X - pawn.Position.X, work.TargetCell.Y - pawn.Position.Y);
                    if (direction == Vector2.zero) direction = Vector2.down;
                }
                var inBed = pawn.LifeActivity == LifeActivity.SleepingInBed || world.RescuedBedFor(pawn.Id).HasValue;
                var sleeping = inBed || pawn.LifeActivity == LifeActivity.SleepingOnFloor;
                if (inBed) direction = Vector2.down;
                var label = "정착민 " + pawn.Id + (pawn.IsDrafted ? " [R]" : sleeping ? " Zz" : pawn.IsWorking ? " · 작업" : "");
                if(world.RescuerFor(pawn.Id) is int rescuer && world.RescueFor(rescuer)?.Carrying==true)
                {
                    var carrier=world.Colonists.First(p=>p.Id==rescuer);
                    position=PawnPosition(carrier,tickFraction,world.NeedsEnabled)+new Vector2(.27f,.08f);
                    label="운반 중 #"+pawn.Id;
                }
                else if(pawn.IsDowned) label="전투 불능 #"+pawn.Id;
                DrawPawn(pawn, position, direction, inBed, sleeping || pawn.IsDowned, working, work, label, world.Tick, pendingSeconds);
                if (working) workBarsThisFrame.Add((pawn.Id, work));
                if (pawn.CargoAmount > 0)
                    Draw("cargo." + pawn.Id, VisualCatalog.ResourceKey(pawn.CargoKind), position + new Vector2(.28f, .08f), Color.white,
                        Sort(position.y, 45), pawn.CargoAmount.ToString(), new Vector2(.28f, .28f));
            }
            selection.enabled = false;
            route.positionCount = 0;
            if (selectedPawnId.HasValue)
            {
                foreach (var pawn in world.Colonists) if (pawn.Id == selectedPawnId.Value)
                {
                    var position = DisplayedPawnPosition(world,pawn);
                    ShowSelection(selection, position + PawnSelectionOffset);
                    route.positionCount = pawn.Path.Count > 0 ? pawn.Path.Count + 1 : 0;
                    if (route.positionCount > 0)
                    {
                        route.SetPosition(0, position);
                        for (var i = 0; i < pawn.Path.Count; i++) route.SetPosition(i + 1, CellCenter(pawn.Path[i]));
                    }
                    break;
                }
            }
            else if (selectedObjectId.HasValue && TryFindPosition(world, selectedObjectId.Value, out var selected))
            {
                var center = CellCenter(selected);
                if (world.Raiders.Any(enemy => enemy.Id == selectedObjectId.Value))
                    center += PawnSelectionOffset;
                ShowSelection(selection, center);
            }
            removedMarks.Clear();
            foreach (var pair in marks) if (!pair.Value.Drawn) removedMarks.Add(pair.Key);
            foreach (var key in removedMarks) { marks[key].Root.SetActive(false); Destroy(marks[key].Root); marks.Remove(key); }
            LayoutWorkBars(selectedPawnId);
            ArrangeLabels();
        }

        private void ArrangeLabels()
        {
            placedLabels.Clear();
            placedLabels.AddRange(workBarRects);
            var screen=MapCamera.pixelRect;
            for (var index = 1; index < route.positionCount; index++)
            {
                var start = MapCamera.WorldToScreenPoint(route.GetPosition(index - 1));
                var end = MapCamera.WorldToScreenPoint(route.GetPosition(index));
                placedLabels.Add(Rect.MinMaxRect(Mathf.Min(start.x,end.x)-3, Mathf.Min(start.y,end.y)-3,
                    Mathf.Max(start.x,end.x)+3, Mathf.Max(start.y,end.y)+3));
            }
            foreach(var mark in marks.Values)
            {
                var label=mark.Label;
                if(!label.gameObject.activeSelf)continue;
                labelFont.RequestCharactersInTexture(label.text,label.fontSize);
                var width=4f;
                foreach(var character in label.text)
                    if(labelFont.GetCharacterInfo(character,out var glyph,label.fontSize))
                        width+=glyph.advance*16f/label.fontSize;
                var anchor=MapCamera.WorldToScreenPoint(label.transform.position);
                if (!screen.Contains(MapCamera.WorldToScreenPoint(mark.Root.transform.position)) && !screen.Contains(anchor))
                {
                    label.gameObject.SetActive(false);
                    continue;
                }
                var candidate=new Rect(Mathf.Clamp(anchor.x-width/2,screen.xMin,screen.xMax-width),anchor.y,width,20);
                for(var attempt=0;attempt<=placedLabels.Count;attempt++)
                {
                    var moved=false;
                    foreach(var previous in placedLabels)
                        if(candidate.Overlaps(previous)){candidate.y=previous.yMax+2;moved=true;}
                    if(!moved)break;
                }
                if(candidate.yMax>screen.yMax)
                {
                    candidate.y=Mathf.Min(anchor.y-40,screen.yMax-20);
                    for(var attempt=0;attempt<=placedLabels.Count;attempt++)
                    {
                        var moved=false;
                        foreach(var previous in placedLabels)
                            if(candidate.Overlaps(previous)){candidate.y=previous.yMin-candidate.height-2;moved=true;}
                        if(!moved)break;
                    }
                }
                label.transform.position=MapCamera.ScreenToWorldPoint(new Vector3(candidate.center.x,candidate.y,10));
                placedLabels.Add(candidate);
            }
        }

        public void SetViewport(Rect normalized)
        {
            viewport = new Rect(Mathf.Clamp(normalized.x, 0, .99f), Mathf.Clamp(normalized.y, 0, .99f),
                Mathf.Clamp(normalized.width, .01f, 1), Mathf.Clamp(normalized.height, .01f, 1));
            viewport.width = Mathf.Min(viewport.width, 1 - viewport.x);
            viewport.height = Mathf.Min(viewport.height, 1 - viewport.y);
            if (MapCamera == null) return;
            MapCamera.rect = viewport;
            ClampCamera();
        }

        public bool TryScreenToGrid(Vector2 screenPosition, out GridPosition position)
        {
            position = default;
            if (MapCamera == null || displayedWorld == null || !MapCamera.pixelRect.Contains(screenPosition)) return false;
            var point = MapCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 10));
            position = new GridPosition(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
            return position.X >= 0 && position.Y >= 0 && position.X < displayedWorld.Width && position.Y < displayedWorld.Height;
        }

        public Vector2 GridToScreen(GridPosition position) => MapCamera == null ? Vector2.zero : (Vector2)MapCamera.WorldToScreenPoint(CellCenter(position));
        public void Focus(GridPosition position)
        {
            if (MapCamera == null) return;
            MapCamera.transform.position = new Vector3(position.X + .5f, position.Y + .5f, -10);
            ClampCamera();
        }
        public void Pan(Vector2 worldDelta)
        {
            if (MapCamera == null) return;
            MapCamera.transform.position += (Vector3)worldDelta;
            ClampCamera();
        }
        public void Zoom(float steps, Vector2 cursor)
        {
            if (MapCamera == null || !MapCamera.pixelRect.Contains(cursor)) return;
            var before = MapCamera.ScreenToWorldPoint(new Vector3(cursor.x, cursor.y, 10));
            MapCamera.orthographicSize = Mathf.Clamp(MapCamera.orthographicSize * Mathf.Pow(.85f, steps), MinimumZoom, MaximumZoom);
            var after = MapCamera.ScreenToWorldPoint(new Vector3(cursor.x, cursor.y, 10));
            MapCamera.transform.position += before - after;
            ClampCamera();
        }
        public void SetPreview(GridPosition? first, GridPosition? last, bool valid, BuildingKind? building = null)
        {
            if (preview == null) return;
            placementGhost.enabled = false;
            if (first.HasValue && building.HasValue && catalog != null &&
                catalog.TryGet(VisualCatalog.BuildingKey(building.Value), out var visual))
            {
                placementGhost.sprite = visual.Sprite;
                placementGhost.transform.localScale = visual.Scale;
                placementGhost.transform.position = CellCenter(first.Value) + visual.PivotOffset;
                placementGhost.color = valid ? new Color(.55f,1,.72f,.62f) : new Color(1,.35f,.35f,.62f);
                placementGhost.enabled = true;
            }
            if (!first.HasValue) { preview.positionCount = 0; return; }
            var end = last ?? first.Value;
            var minX = Math.Min(first.Value.X, end.X); var minY = Math.Min(first.Value.Y, end.Y);
            var maxX = Math.Max(first.Value.X, end.X); var maxY = Math.Max(first.Value.Y, end.Y);
            preview.startColor = preview.endColor = valid ? new Color(.35f, 1, .65f) : new Color(1, .3f, .3f);
            DrawPreviewCorners(minX, minY, maxX + 1, maxY + 1);
        }

        private void DrawPreviewCorners(float left, float bottom, float right, float top)
        {
            const float cut = .14f;
            preview.positionCount = 9;
            preview.SetPosition(0, new Vector3(left + cut, bottom));
            preview.SetPosition(1, new Vector3(right - cut, bottom));
            preview.SetPosition(2, new Vector3(right, bottom + cut));
            preview.SetPosition(3, new Vector3(right, top - cut));
            preview.SetPosition(4, new Vector3(right - cut, top));
            preview.SetPosition(5, new Vector3(left + cut, top));
            preview.SetPosition(6, new Vector3(left, top - cut));
            preview.SetPosition(7, new Vector3(left, bottom + cut));
            preview.SetPosition(8, new Vector3(left + cut, bottom));
        }

        public static Vector2 CellCenter(GridPosition cell) => new Vector2(cell.X + .5f, cell.Y + .5f);
        public static Vector2 PawnPosition(ColonistState pawn, float tickFraction = 0f, bool needsEnabled = false)
        {
            var origin = CellCenter(pawn.Position);
            if (pawn.Status != MovementStatus.Moving || pawn.Path.Count == 0) return origin;
            var progress = (pawn.MovementProgress + Mathf.Clamp01(tickFraction)) / pawn.MovementTicksForNextTick(needsEnabled);
            return Vector2.Lerp(origin, CellCenter(pawn.Path[0]), progress);
        }

        internal Vector2 DisplayedPawnPosition(WorldState world, ColonistState pawn)
        {
            if (ReferenceEquals(world, displayedWorld) && marks.TryGetValue("pawn." + pawn.Id, out var mark))
                return mark.Root.transform.position;
            return PawnPosition(pawn);
        }
        public static Color PawnColor(int id)
        {
            switch ((id % 3 + 3) % 3)
            { case 0: return new Color(1, .75f, .48f); case 1: return new Color(.55f, .9f, .9f); default: return new Color(.85f, .65f, .95f); }
        }
        public static int WallMask(WorldState world, GridPosition position)
        {
            var mask = 0;
            foreach (var b in world.Buildings)
                if (b.Exists && b.Kind == BuildingKind.Wall)
                {
                    if (b.Position.X == position.X && b.Position.Y == position.Y + 1) mask |= 1;
                    if (b.Position.X == position.X + 1 && b.Position.Y == position.Y) mask |= 2;
                    if (b.Position.X == position.X && b.Position.Y == position.Y - 1) mask |= 4;
                    if (b.Position.X == position.X - 1 && b.Position.Y == position.Y) mask |= 8;
                }
            return mask;
        }

        private float CameraAspect => MapCamera == null ? 1 : Math.Max(.1f, MapCamera.pixelRect.width / Math.Max(1, MapCamera.pixelRect.height));
        private void ClampCamera()
        {
            if (displayedWorld == null || MapCamera == null) return;
            var p = MapCamera.transform.position;
            // The camera may look beyond an edge so border targets can be centered clear of the HUD.
            p.x = Mathf.Clamp(p.x, 0, displayedWorld.Width);
            p.y = Mathf.Clamp(p.y, 0, displayedWorld.Height);
            p.z = -10; MapCamera.transform.position = p;
        }
        private void RebuildGround()
        {
            var bounds = new BoundsInt(0, 0, 0, displayedWorld.Width, displayedWorld.Height, 1);
            var tiles = new TileBase[displayedWorld.Width * displayedWorld.Height];
            Array.Fill(tiles, soilTile);
            ground.SetTilesBlock(bounds, tiles);
            FullTerrainUpdates++;
            UpdateBlockedCells();
        }
        private void UpdateBlockedCells()
        {
            nextBlocked.Clear();
            nextBlocked.UnionWith(displayedWorld.BlockedTiles);
            // The stove sprite already identifies this occupied cell; show the soil through its outline.
            foreach (var building in displayedWorld.Buildings)
                if (building.Exists && building.Kind == BuildingKind.Stove)
                    nextBlocked.Remove(building.Position);
            foreach (var p in renderedBlocked)
                if (!nextBlocked.Contains(p)) SetGroundTile(p, soilTile);
            foreach (var p in nextBlocked)
                if (!renderedBlocked.Contains(p)) SetGroundTile(p, blockedTile);
            (renderedBlocked, nextBlocked) = (nextBlocked, renderedBlocked);
            terrainRevision = displayedWorld.MapRevision;
        }
        private void SetGroundTile(GridPosition position, Tile tile)
        {
            ground.SetTile(new Vector3Int(position.X, position.Y, 0), tile);
            ChangedTerrainCells++;
        }
        private bool HasVisual(string key) => catalog != null && catalog.TryGet(key, out var visual) && visual.Sprite != null;
        private Sprite SpriteFor(string key) => catalog != null && catalog.TryGet(key, out var visual) && visual.Sprite != null ? visual.Sprite : fallback;
        private Mark GetMark(string identity)
        {
            if (!marks.TryGetValue(identity, out var mark))
            {
                var go = new GameObject(identity); go.transform.SetParent(content, false); go.layer = MapLayer;
                var spriteObject = new GameObject("Sprite", typeof(SpriteRenderer)); spriteObject.transform.SetParent(go.transform, false); spriteObject.layer = MapLayer;
                var textObject = new GameObject("Label", typeof(TextMesh)); textObject.transform.SetParent(go.transform, false); textObject.layer = MapLayer;
                mark = new Mark { Root = go, Sprite = spriteObject.GetComponent<SpriteRenderer>(), Label = textObject.GetComponent<TextMesh>() };
                if (spriteMaterial != null) mark.Sprite.sharedMaterial = spriteMaterial;
                mark.Label.font = labelFont; mark.Label.fontSize = 40; mark.Label.characterSize = .06f;
                mark.Label.anchor = TextAnchor.LowerCenter; mark.Label.alignment = TextAlignment.Center; mark.Label.richText = false;
                var renderer = textObject.GetComponent<MeshRenderer>(); renderer.sharedMaterial = labelFont.material; renderer.sortingOrder = 32500;
                var shadow = new GameObject("Label shadow", typeof(TextMesh));
                shadow.transform.SetParent(textObject.transform, false);
                shadow.layer = MapLayer;
                mark.Shadow = shadow.GetComponent<TextMesh>();
                mark.Shadow.font = labelFont;
                mark.Shadow.fontSize = mark.Label.fontSize;
                mark.Shadow.anchor = mark.Label.anchor;
                mark.Shadow.alignment = mark.Label.alignment;
                mark.Shadow.richText = false;
                mark.Shadow.color = new Color(.02f,.025f,.025f,.94f);
                var shadowRenderer = shadow.GetComponent<MeshRenderer>();
                shadowRenderer.sharedMaterial = labelFont.material;
                shadowRenderer.sortingOrder = 32499;
                marks.Add(identity, mark);
            }
            mark.Drawn = true; VisibleEntityCount++;
            return mark;
        }

        private Mark Draw(string identity, string key, Vector2 groundPoint, Color tint, int order, string caption, Vector2? sizeOverride = null)
        {
            var mark = GetMark(identity);
            var sprite = SpriteFor(key);
            var size = sizeOverride ?? new Vector2(.85f, .85f);
            var anchor = new Vector2(.5f, .5f);
            var visualTint = Color.white;
            if (catalog != null && catalog.TryGet(key, out var visual) && visual.Sprite != null)
            { if (!sizeOverride.HasValue) size = visual.WorldSize; anchor = visual.GroundAnchorNormalized; visualTint = visual.Tint; }
            mark.Root.transform.position = groundPoint;
            mark.Sprite.sprite = sprite;
            mark.Sprite.color = tint * visualTint;
            mark.Sprite.sortingOrder = order;
            mark.Sprite.flipX = false;
            mark.Sprite.transform.localRotation = Quaternion.identity;
            mark.Sprite.transform.localScale = new Vector3(size.x / Math.Max(.001f, sprite.bounds.size.x), size.y / Math.Max(.001f, sprite.bounds.size.y), 1);
            var pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            mark.Sprite.transform.localPosition = Vector2.Scale(pivot - anchor, size);
            SetMarkLabel(mark, caption, Color.white, size.y * (1 - anchor.y));
            return mark;
        }

        private void SetMarkLabel(Mark mark, string caption, Color color, float top)
        {
            mark.Label.gameObject.SetActive(!string.IsNullOrEmpty(caption));
            mark.Label.text = caption ?? string.Empty;
            mark.Label.color = color;
            mark.Label.transform.localPosition = new Vector3(0, top + .04f, 0);
            mark.Label.characterSize = 16f * 2f * MapCamera.orthographicSize / Math.Max(1,MapCamera.pixelHeight) * 10f / mark.Label.fontSize;
            mark.Label.transform.localScale = Vector3.one;
            mark.Shadow.text = mark.Label.text;
            mark.Shadow.characterSize = mark.Label.characterSize;
            var pixel = 2f * MapCamera.orthographicSize / Math.Max(1, MapCamera.pixelHeight);
            mark.Shadow.transform.localPosition = new Vector3(pixel, -pixel);
        }
        private static bool HasBuilding(WorldState world, int id) { foreach (var b in world.Buildings) if (b.Id == id && b.Exists) return true; return false; }
        private static int Sort(float y, int offset) => Mathf.Clamp(10000 - Mathf.RoundToInt(y * 100) + offset, -20000, 30000);
        public void ShowPawnSelections(IEnumerable<int> ids,int? primary)
        {
            foreach (var marker in pawnSelections.Values) marker.enabled = false;
            if(displayedWorld==null)return;
            foreach(var id in ids)
            {
                if(id==primary)continue;
                var pawn=displayedWorld.Colonists.FirstOrDefault(p=>p.Id==id);
                if(pawn==null)continue;
                if(!pawnSelections.TryGetValue(id,out var marker))
                {
                    marker = CreateSelection("Selected pawn " + id, new Color(.79f, .87f, .65f));
                    pawnSelections.Add(id, marker);
                }
                ShowSelection(marker, DisplayedPawnPosition(displayedWorld, pawn) + PawnSelectionOffset);
            }
        }
        private LineRenderer CreateLine(string name, Color color, int order, float width)
        {
            var go = new GameObject(name, typeof(LineRenderer)); go.transform.SetParent(content, false); go.layer = MapLayer;
            var line = go.GetComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = 0;
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = color; line.sortingOrder = order;
            if (spriteMaterial != null) line.sharedMaterial = spriteMaterial;
            return line;
        }
        private void BuildSelectionSprite()
        {
            const int size = 64, arm = 16, stroke = 3;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = Mathf.Min(x, size - 1 - x);
                var dy = Mathf.Min(y, size - 1 - y);
                if ((dx < arm && dy < stroke) || (dy < arm && dx < stroke))
                    pixels[y * size + x] = Color.white;
            }
            selectionTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            selectionTexture.wrapMode = TextureWrapMode.Clamp;
            selectionTexture.SetPixels32(pixels);
            selectionTexture.Apply(false, true);
            selectionSprite = Sprite.Create(selectionTexture, new Rect(0, 0, size, size),
                new Vector2(.5f, .5f), size, 0, SpriteMeshType.FullRect);
        }
        private SpriteRenderer CreateSelection(string name, Color color)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.layer = MapLayer;
            go.transform.SetParent(content, false);
            var marker = go.GetComponent<SpriteRenderer>();
            marker.sprite = selectionSprite;
            marker.color = color;
            marker.sortingOrder = 32000;
            if (spriteMaterial != null) marker.sharedMaterial = spriteMaterial;
            marker.enabled = false;
            return marker;
        }
        private static void ShowSelection(SpriteRenderer marker, Vector2 center)
        {
            marker.transform.position = center;
            marker.enabled = true;
        }
        private static bool TryFindPosition(WorldState world, int id, out GridPosition position)
        {
            foreach(var weapon in world.Weapons) if(weapon.Id==id&&!weapon.HolderId.HasValue){position=weapon.Position;return true;}
            foreach (var enemy in world.Raiders) if (enemy.Id == id) { position=enemy.Position; return true; }
            foreach (var b in world.Buildings) if (b.Exists && b.Id == id) { position = b.Position; return true; }
            foreach (var b in world.Blueprints) if (b.Exists && b.Id == id) { position = b.Position; return true; }
            foreach (var b in world.Beds) if (b.Exists && b.Id == id) { position = b.Position; return true; }
            foreach (var r in world.Resources) if (r.Exists && r.Id == id) { position = r.Position; return true; }
            foreach (var n in world.Gatherables) if (!n.Depleted && n.Id == id) { position = n.Position; return true; }
            foreach (var c in world.Crops) if (c.Exists && c.Id == id) { position = c.Position; return true; }
            foreach (var z in world.StorageZones) if (z.Exists && z.Id == id && z.Slots.Count > 0) { position = z.Slots[0].Position; return true; }
            foreach (var z in world.GrowingZones) if (z.Exists && z.Id == id && z.Cells.Count > 0) { position = z.Cells[0].Position; return true; }
            position = default; return false;
        }
        private void ClearMarks() { foreach (var m in marks.Values) { m.Root.SetActive(false); Destroy(m.Root); } marks.Clear(); VisibleEntityCount = 0; }
        private void OnEnable() { if (MapCamera != null) MapCamera.enabled = true; if (content != null) content.gameObject.SetActive(true); }
        private void OnDisable() { if (MapCamera != null) MapCamera.enabled = false; if (content != null) content.gameObject.SetActive(false); }
        private void OnDestroy()
        {
            if (soilTile != null) Destroy(soilTile); if (blockedTile != null) Destroy(blockedTile);
            DestroyZoneTiles();
            if (selectionSprite != null) Destroy(selectionSprite);
            if (selectionTexture != null) Destroy(selectionTexture);
            if (fallback != null) Destroy(fallback); if (fallbackTexture != null) Destroy(fallbackTexture);
            if (spriteMaterial != null) Destroy(spriteMaterial);
            if (content != null) Destroy(content.gameObject); if (MapCamera != null) Destroy(MapCamera.gameObject);
        }
    }
}
