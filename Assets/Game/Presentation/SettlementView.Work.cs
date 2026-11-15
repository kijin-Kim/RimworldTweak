using System.Collections.Generic;
using System.Text;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public sealed partial class SettlementView
    {
        public enum WorkTool { None, Gather, Storage }
        public WorkTool ActiveWorkTool { get; private set; }
        public GridPosition? StorageFirstCorner { get; private set; }
        public GridPosition? SelectedWorldTile { get; private set; }
        public int? SelectedObjectId { get; private set; }
        public Button GatherButton { get; private set; }
        public Button StorageButton { get; private set; }
        public Button CancelGatherButton { get; private set; }
        public Button DeleteStorageButton { get; private set; }
        public Button NextObjectButton { get; private set; }
        public Button WorkTabButton { get; private set; }
        public Button LifeTabButton { get; private set; }
        public Text WorkInfoText { get; private set; }
        public Text StockTotalsText { get; private set; }
        public IReadOnlyList<Button> FilterButtons => filterButtons.AsReadOnly();
        private readonly List<Button> filterButtons = new List<Button>();
        private readonly List<RectTransform> workMarkers = new List<RectTransform>();
        private readonly List<int> objectIds = new List<int>();
        private RectTransform workPanel, storagePreview;
        private bool showWork;
        private int displayedResourceCount;
        private GridPosition? hoverTile;

        private void BuildWorkUI()
        {
            LifeTabButton = MakeButton("Life / pawn", uiRoot.transform, 900, 714, 150);
            WorkTabButton = MakeButton("Work / stock", uiRoot.transform, 1066, 714, 150);
            LifeTabButton.onClick.AddListener(() => { showWork = false; CancelWorkTool(); RefreshNow(); });
            WorkTabButton.onClick.AddListener(() => { showWork = true; RefreshNow(); });
            workPanel = Panel("Work panel", sidebar, 0, 0, 556, 312, new Color(0.08f, 0.12f, 0.14f));
            workPanel.GetComponent<Image>().raycastTarget = true;
            GatherButton = MakeButton("Gather", workPanel, 28, 260, 150);
            StorageButton = MakeButton("Stock zone", workPanel, 194, 260, 150);
            var select = MakeButton("Select / Esc", workPanel, 360, 260, 168);
            GatherButton.onClick.AddListener(() => SetWorkTool(WorkTool.Gather));
            StorageButton.onClick.AddListener(() => SetWorkTool(WorkTool.Storage));
            select.onClick.AddListener(() => { CancelWorkTool(); RefreshNow(); });
            WorkInfoText = Label("Work detail", workPanel, "", 28, 147, 500, 108, 14, Ink);
            CancelGatherButton = MakeButton("Cancel gather", workPanel, 28, 100, 150);
            DeleteStorageButton = MakeButton("Delete zone", workPanel, 194, 100, 150);
            NextObjectButton = MakeButton("Next object", workPanel, 360, 100, 168);
            CancelGatherButton.onClick.AddListener(() => { if (SelectedObjectId.HasValue) Runner.DesignateGather(SelectedObjectId.Value, false); RefreshNow(); });
            DeleteStorageButton.onClick.AddListener(() => { if (SelectedObjectId.HasValue) Runner.DeleteStorage(SelectedObjectId.Value); RefreshNow(); });
            NextObjectButton.onClick.AddListener(() => {
                if (objectIds.Count > 0) SelectedObjectId = objectIds[(objectIds.IndexOf(SelectedObjectId ?? -1) + 1) % objectIds.Count];
                RefreshNow();
            });
            for (var i = 0; i < 3; i++)
            {
                var kind = (ResourceKind)i;
                var button = MakeButton(kind.ToString(), workPanel, 28 + i * 166, 52, i == 2 ? 168 : 150);
                button.onClick.AddListener(() => ToggleStorageFilter(kind)); filterButtons.Add(button);
            }
            StockTotalsText = Label("Stock totals", workPanel, "", 28, 2, 500, 48, 12, Ink);
            storagePreview = Outline("Storage preview", PendingColor);
            workPanel.gameObject.SetActive(false);
        }
        public void SetWorkTool(WorkTool tool)
        {
            ActiveWorkTool = tool; StorageFirstCorner = null; showWork = true; RefreshNow();
        }
        private bool CancelWorkTool()
        {
            if (ActiveWorkTool == WorkTool.None) return false;
            ActiveWorkTool = WorkTool.None; StorageFirstCorner = null; hoverTile = null;
            if (storagePreview != null) storagePreview.gameObject.SetActive(false);
            RefreshNow(); return true;
        }
        private void ClearObjectSelection() { SelectedWorldTile = null; SelectedObjectId = null; objectIds.Clear(); RefreshNow(); }
        private bool HandleWorkToolClick(GridPosition position, bool rightButton, out bool result)
        {
            result = false;
            if (ActiveWorkTool == WorkTool.None) return false;
            if (rightButton) { CancelWorkTool(); result = true; return true; }
            if (ActiveWorkTool == WorkTool.Gather)
            {
                foreach (var node in Runner.Session.World.Gatherables)
                    if (!node.Depleted && node.Position == position)
                    { result = Runner.DesignateGather(node.Id, true); SelectWorldObject(position); break; }
            }
            else if (!StorageFirstCorner.HasValue) { StorageFirstCorner = position; hoverTile = position; result = true; }
            else
            {
                result = Runner.CreateStorage(StorageFirstCorner.Value, position);
                StorageFirstCorner = null;
                if (result) { ActiveWorkTool = WorkTool.None; hoverTile = null; SelectWorldObject(position); }
            }
            RefreshNow(); return true;
        }
        public void HandleMapHover(GridPosition position) { hoverTile = position; }
        private void SelectWorldObject(GridPosition position)
        {
            SelectedWorldTile = position; SelectedObjectId = null; RebuildObjectList(Runner.Session.World);
            if (objectIds.Count > 0) { SelectedObjectId = objectIds[0]; showWork = true; }
        }
        private void RebuildObjectList(WorldState world)
        {
            objectIds.Clear(); if (!SelectedWorldTile.HasValue) return;
            foreach (var node in world.Gatherables) if (node.Position == SelectedWorldTile.Value && !node.Depleted) objectIds.Add(node.Id);
            foreach (var bed in world.Beds) if (bed.Exists && bed.Position == SelectedWorldTile.Value) objectIds.Add(bed.Id);
            foreach (var item in world.Resources) if (item.Position == SelectedWorldTile.Value && item.Exists && item.Amount > 0) objectIds.Add(item.Id);
            foreach (var zone in world.StorageZones) if (zone.Exists)
                foreach (var slot in zone.Slots) if (slot.Position == SelectedWorldTile.Value) { objectIds.Add(zone.Id); break; }
            if (!objectIds.Contains(SelectedObjectId ?? -1)) SelectedObjectId = objectIds.Count > 0 ? objectIds[0] : (int?)null;
        }
        private void ResetWorkView(WorldState world)
        {
            ActiveWorkTool = WorkTool.None; StorageFirstCorner = null; hoverTile = null;
            SelectedWorldTile = null; SelectedObjectId = null; objectIds.Clear();
            showWork = world.Gatherables.Count > 0;
        }
        public bool ToggleStorageFilter(ResourceKind kind)
        {
            if (!SelectedObjectId.HasValue) return false;
            foreach (var zone in Runner.Session.World.StorageZones)
                if (zone.Exists && zone.Id == SelectedObjectId.Value)
                { var result = Runner.SetStorageFilter(zone.Id, kind, !zone.Allows(kind)); RefreshNow(); return result; }
            return false;
        }
        private void RefreshWorkUI(WorldState world)
        {
            if (workPanel == null) return;
            workPanel.gameObject.SetActive(true);
            RebuildObjectList(world);
            var detail = new StringBuilder();
            detail.Append(ActiveWorkTool == WorkTool.Gather ? "채집 지정: 나무 또는 식물을 클릭" : ActiveWorkTool == WorkTool.Storage ?
                StorageFirstCorner.HasValue ? "저장 구역: 반대쪽 모서리를 클릭" : "저장 구역: 첫 모서리를 클릭" : "오브젝트 선택: 맵 클릭 / 다음 오브젝트");
            detail.Append("\n설정 즉시 적용 · 이동 대기 ").Append(world.PendingCommandCount);
            detail.Append("\n같은 칸의 대상 "); foreach (var id in objectIds) detail.Append(id == SelectedObjectId ? ">" : " ").Append(id).Append(' ');
            GatherState selectedNode = null; StorageZoneState selectedZone = null;
            foreach (var node in world.Gatherables) if (node.Id == SelectedObjectId) selectedNode = node;
            foreach (var zone in world.StorageZones) if (zone.Exists && zone.Id == SelectedObjectId) selectedZone = zone;
            if (selectedNode != null) detail.Append("\n").Append(selectedNode.Kind == GatherKind.Tree ? "나무" : "식용 식물").Append(selectedNode.Designated ? " · 채집 지정됨" : " NOT · 채집 지정됨")
                .Append(" ").Append(selectedNode.Progress).Append('/').Append(selectedNode.Duration).Append(" C").Append(selectedNode.OwnerId?.ToString() ?? "--");
            foreach (var item in world.Resources) if (item.Id == SelectedObjectId)
                detail.Append("\n").Append(ResourceName(item.Kind)).Append(" x").Append(item.Amount).Append(item.StorageSlotId.HasValue ? " · 보관 중" : " · 미보관")
                    .Append(" C").Append(item.OwnerId?.ToString() ?? "--").Append(" · 접근 가능 ").Append(item.AccessibleAmount);
            if (selectedZone != null)
                foreach (var slot in selectedZone.Slots) if (slot.Position == SelectedWorldTile)
                    detail.Append("\n저장 구역 ").Append(selectedZone.Id).Append(" / 칸 ").Append(slot.Id).Append(" ").Append(slot.Amount).Append("/50 C").Append(slot.OwnerId?.ToString() ?? "--");
            foreach(var bed in world.Beds) if(bed.Id==SelectedObjectId)
                detail.Append("\n침대 #").Append(bed.Id).Append(!bed.OwnerId.HasValue?" · 비어 있음":bed.IsOccupied?" · 수면 중 ":" · 접근 중 ").Append(bed.OwnerId?.ToString()??"");
            if (!string.IsNullOrEmpty(world.LastCommandError)) detail.Append("\n").Append(ErrorMessage(world.LastCommandError));
            WorkInfoText.text = detail.ToString();
            CancelGatherButton.interactable = selectedNode != null && selectedNode.Designated;
            DeleteStorageButton.interactable = selectedZone != null;
            NextObjectButton.interactable = objectIds.Count > 1;
            for (var i = 0; i < filterButtons.Count; i++)
            {
                var kind = (ResourceKind)i; filterButtons[i].interactable = selectedZone != null;
                filterButtons[i].GetComponentInChildren<Text>().text = (selectedZone == null ? "-- " : selectedZone.Allows(kind) ? "[x] " : "[ ] ") + ResourceName(kind);
            }
            var totals = new StringBuilder();
            foreach (var kind in new[] { ResourceKind.Wood, ResourceKind.Ingredients, ResourceKind.Meal })
            {
                var loose = world.ResourceTotal(kind, false); var stored = world.ResourceTotal(kind, true); var cargo = world.CargoTotal(kind);
                totals.Append(ResourceName(kind)).Append("  미보관 ").Append(loose).Append(" / 운반 ").Append(cargo).Append(" / 보관 ").Append(stored).Append(" / 총량 ").Append(loose + stored + cargo).Append('\n');
            }
            StockTotalsText.text = totals.ToString();
            DrawWorkMarkers(world);
        }
        private RectTransform WorkMarker(int index, GridPosition position, string text, Color color, float size)
        {
            if (index == workMarkers.Count)
            {
                var rect = Panel("Work marker", board, 0, 0, 1, 1, color);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.GetComponent<Image>().sprite = outlineSprite;
                var label = Label("Label", rect, "", 0, 0, 1, 1, 11, Ink);
                label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.alignment = TextAnchor.LowerCenter; workMarkers.Add(rect);
            }
            var marker = workMarkers[index]; marker.GetComponent<Image>().color = color;
            marker.GetComponentInChildren<Text>().text = text;
            PlaceOverlay(marker, position, 480f / Mathf.Max(Runner.Session.World.Width, Runner.Session.World.Height), size);
            return marker;
        }
        private void DrawWorkMarkers(WorldState world)
        {
            var index = 0;
            DrawStorageGround(world);
            foreach (var node in world.Gatherables) if (!node.Depleted)
            {
                var marker=WorkMarker(index++,node.Position,(node.Kind==GatherKind.Tree?"나무":"식용 식물")+(node.Designated?" [채집]":"")+(node.OwnerId.HasValue?" ["+node.OwnerId+"]":""),ColonistColor,.66f);
                SymbolOn(marker,node.Kind==GatherKind.Tree?MapSymbol.Tree:MapSymbol.Plant,new Color(.40f,.72f,.38f));
                var label=marker.GetComponentInChildren<Text>(); label.alignment=TextAnchor.MiddleCenter;
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,.5f); label.rectTransform.pivot=new Vector2(.5f,0);
                label.rectTransform.sizeDelta=new Vector2(100,18); label.rectTransform.anchoredPosition=new Vector2(0,2-240f/Mathf.Max(world.Width,world.Height));
                label.color=node.OwnerId.HasValue?PawnColor(node.OwnerId.Value):Ink;
            }
            for (var i = index; i < workMarkers.Count; i++) workMarkers[i].gameObject.SetActive(false);
            storagePreview.gameObject.SetActive(StorageFirstCorner.HasValue);
            if (StorageFirstCorner.HasValue)
            {
                var a = StorageFirstCorner.Value; var b = hoverTile ?? a; var cell = 480f / Mathf.Max(world.Width, world.Height);
                storagePreview.pivot = Vector2.zero;
                storagePreview.anchoredPosition = new Vector2(Mathf.Min(a.X, b.X) * cell, Mathf.Min(a.Y, b.Y) * cell);
                storagePreview.sizeDelta = new Vector2((Mathf.Abs(a.X - b.X) + 1) * cell, (Mathf.Abs(a.Y - b.Y) + 1) * cell);
                storagePreview.SetAsLastSibling();
            }
        }
    }
}
