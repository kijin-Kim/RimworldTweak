using System;
using System.Collections.Generic;
using System.Linq;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    // The shipping interface. Legacy SettlementView stays available only to historical test scenes.
    public sealed partial class SettlementShell : MonoBehaviour
    {
        public enum Tool { None, Gather, Building, Storage, Growing }
        public SimulationRunner Runner { get; private set; }
        public WorldMapView Map { get; private set; }
        public WorldMapInput MapInput { get; private set; }
        public int? SelectedPawnId { get; private set; }
        public int? SelectedObjectId { get; private set; }
        public Tool ActiveTool { get; private set; }
        public bool IsMenuOpen => menuRoot != null && menuRoot.gameObject.activeSelf;
        public bool HasSettlement { get; private set; }
        private string feedback = string.Empty;
        private float feedbackSeconds;
        public string Feedback
        {
            get => feedback;
            private set
            {
                feedback = value;
                feedbackSeconds = 0;
            }
        }
        public IReadOnlyDictionary<string, Button> Buttons => buttons;
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        private readonly List<Action> refreshers = new List<Action>();
        private readonly List<Action> detailRefreshers = new List<Action>();
        private readonly List<Action> panelRefreshers = new List<Action>();
        private RectTransform canvasRoot, inspection, tabs, panel, messages;
        private CanvasScaler scaler;
        private Font font;
        private VisualCatalog visualCatalog;
        private Text feedbackLabel;
        private BuildingKind buildingKind;
        private string openTab = string.Empty;
        private WorldState displayed;
        private Vector2 layoutSize;
        private int layoutScale;
        private bool draftHeld;
        private bool developmentUi;
        private bool hadFocus = true;
        private static readonly Color PanelColor = new Color(.08f,.105f,.12f,.98f);
        private static readonly Color TextColor = new Color(.91f,.92f,.88f);
        private static readonly Color Accent = new Color(.69f,.81f,.52f);

        public void Initialize(SimulationRunner runner, bool startAtMenu = true, string storageRoot = null, VisualCatalog visuals = null, bool enableDevelopmentUi = false)
        {
            developmentUi = enableDevelopmentUi;
            Runner = runner ?? throw new ArgumentNullException(nameof(runner));
            if (Runner.Slots == null || storageRoot != null) Runner.ConfigureStorage(storageRoot);
            font = Resources.Load<Font>("UI/NotoSansCJKkr-Regular");
            if (font == null) throw new InvalidOperationException("Bundled Korean font is missing.");
            visualCatalog=visuals;BuildShell();OpenTab(string.Empty);
            Map = new GameObject("Settlement world").AddComponent<WorldMapView>();
            Map.transform.SetParent(transform, false);
            Map.Initialize(visuals);
            Map.Bind(Runner.Session.World);
            MapInput = Map.gameObject.AddComponent<WorldMapInput>();
            MapInput.Configure(Map, () => IsInputBlocked, () => ActiveTool != Tool.None, () => ActiveTool == Tool.Storage || ActiveTool == Tool.Growing, HandleMapClick, HandleRectangle, CancelTool);
            displayed = Runner.Session.World;
            HasSettlement = !startAtMenu;
            RefreshLayout();
            Refresh();
            if (startAtMenu) ShowMainMenu();
        }

        public bool IsInputBlocked => !HasSettlement || IsMenuOpen || Runner == null || Runner.SaveBusy || TextFocused();
        private static bool TextFocused()
        {
            var current = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            return current != null && current.GetComponentInParent<InputField>() != null;
        }

        private void Update()
        {
            if (Runner == null || Keyboard.current == null) return;
            var keys = Keyboard.current;
            if (keys.f3Key.wasPressedThisFrame) ProcessShortcut(Key.F3);
            if (keys.escapeKey.wasPressedThisFrame) ProcessShortcut(Key.Escape);
            if (keys.spaceKey.wasPressedThisFrame) ProcessShortcut(Key.Space);
            if (keys.digit1Key.wasPressedThisFrame) ProcessShortcut(Key.Digit1);
            if (keys.digit3Key.wasPressedThisFrame) ProcessShortcut(Key.Digit3);
            var held = keys.rKey.isPressed;
            if (held && !draftHeld && hadFocus) ProcessShortcut(Key.R);
            draftHeld = held;
            hadFocus = true;
        }

        public bool ProcessShortcut(Key key)
        {
            if (Runner == null || Runner.SaveBusy || TextFocused()) return false;
            if (key == Key.Escape)
            {
                if (IsMenuOpen) { CancelMenuLevel(); return true; }
                if (ActiveTool != Tool.None) { CancelTool(); return true; }
                if (!string.IsNullOrEmpty(openTab)) { OpenTab(string.Empty); return true; }
                if (detailOpen) { SetDetailOpen(false); return true; }
                if (SelectedPawnId.HasValue || SelectedObjectId.HasValue) { ClearSelection(); return true; }
                ShowPauseMenu(); return true;
            }
            if (IsInputBlocked) return false;
            switch (key)
            {
                case Key.F3: if (!developmentUi) return false; ToggleTab("development"); break;
                case Key.Space: Runner.SetPaused(!Runner.IsPaused); break;
                case Key.Digit1: Runner.SetSpeed(1); break;
                case Key.Digit3: Runner.SetSpeed(3); break;
                case Key.R: if (!SelectedPawnId.HasValue) return false; Runner.ToggleDraft(SelectedPawnId.Value); break;
                default: return false;
            }
            return true;
        }

        private void LateUpdate()
        {
            if (Runner == null) return;
            if (feedbackSeconds > 0 && HasSettlement && !IsMenuOpen)
            {
                feedbackSeconds -= Time.unscaledDeltaTime;
                if (feedbackSeconds <= 0) Feedback = string.Empty;
            }
            RefreshLayout();
            Refresh();
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) { MapInput?.CancelGesture(); draftHeld = true; hadFocus = false; }
        }
        private void OnDestroy() { if (Runner != null) Runner.SetUiBlocked(false); }

        private void Refresh()
        {
            var world = Runner.Session.World;
            if (!ReferenceEquals(displayed, world))
            {
                Feedback=string.Empty;
                MapInput.CancelGesture(); CancelTool(); ClearSelection();
                displayed = world; Map.Bind(world); BuildColonistBar();ClearAlerts();OpenTab(string.Empty);
            }
            if (SelectedPawnId.HasValue && !world.Colonists.Any(x => x.Id == SelectedPawnId)) ClearSelection();
            if (SelectedObjectId.HasValue && !ObjectExists(SelectedObjectId.Value)) ClearSelection();
            Map.Refresh(world, SelectedPawnId, SelectedObjectId, Runner.IsPaused || Runner.IsUiBlocked);
            RefreshPreview();
            RefreshAlerts();
            RefreshResources(world);
            RefreshPortraitTooltip();
            timeStatus.text = (Runner.IsPaused ? "일시정지" : Runner.SpeedMultiplier + "배속") + " · " + (world.Tick / 10) + "초";
            buttons["pause"].GetComponentInChildren<Text>().text = Runner.IsPaused ? "재생" : "정지";
            feedbackLabel.text = Feedback;
            if (architectDescription != null) architectDescription.text = HasArchitectTool ? Feedback : string.Empty;
            foreach (var refresh in refreshers) refresh();
            foreach (var refresh in detailRefreshers) refresh();
            foreach (var refresh in panelRefreshers) refresh();
        }
        private void RefreshResources(WorldState world)
        {
            var mouse = Mouse.current;
            var pointer = mouse == null ? Vector2.zero : mouse.position.ReadValue();
            var showTooltip = false;
            foreach (var resource in resourceTotals)
            {
                var kind = resource.Key;
                var ground = world.ResourceTotal(kind, false);
                var stored = world.ResourceTotal(kind, true);
                var carried = world.CargoTotal(kind);
                var input = kind == ResourceKind.Wood ? world.ConstructionWood
                    : kind == ResourceKind.Ingredients ? world.CookingInputTotal : 0;
                var total = ground + stored + carried + input;
                var name = SettlementView.ResourceName(kind);
                resource.Value.text = name + " " + total;
                if (IsInputBlocked || mouse == null || MapInput.IsCapturing ||
                    !RectTransformUtility.RectangleContainsScreenPoint(resource.Value.rectTransform, pointer)) continue;

                showTooltip = true;
                resourceBreakdown.text = name + " 합계 " + total + "\n바닥 " + ground + " · 보관 " + stored +
                    "\n운반 " + carried + " · 작업 투입 " + input;
            }
            if (showTooltip && !resourceTooltip.gameObject.activeSelf) resourceTooltip.SetAsLastSibling();
            resourceTooltip.gameObject.SetActive(showTooltip);
        }

        public void SelectPawn(int id)
        {
            if (!Runner.Session.World.Colonists.Any(x=>x.Id==id)) return;
            OpenTab(string.Empty);
            SelectedPawnId = id; SelectedObjectId = null; BuildInspector();
        }
        public void ClearSelection() { SelectedPawnId = null; SelectedObjectId = null; BuildInspector(); }
        public void SelectObject(int id) => SelectObject(id, null);

        private void SelectObject(int id, GridPosition? hitCell)
        {
            OpenTab(string.Empty);
            SelectedPawnId = null;
            SelectedObjectId = id;
            BuildInspector(hitCell);
        }
        public void HandleMapClick(GridPosition position, bool right)
        {
            if (IsInputBlocked) return;
            if (right)
            {
                if (ActiveTool != Tool.None) { CancelTool(); return; }
                if (SelectedPawnId.HasValue) Report(Runner.MoveColonist(SelectedPawnId.Value,position), "이동 명령을 보냈어.");
                return;
            }
            if (ActiveTool == Tool.Building)
            {
                var ok=Runner.PlaceBlueprint(buildingKind,position);
                if(ok){CancelTool();var b=Runner.Session.World.Blueprints.Last(x=>x.Exists&&x.Position==position);SelectObject(b.Id);}
                Report(ok,"건설을 지정했어."); return;
            }
            if (ActiveTool == Tool.Gather)
            {
                var node=Runner.Session.World.Gatherables.FirstOrDefault(x=>!x.Depleted&&x.Position==position);
                Report(node!=null&&Runner.DesignateGather(node.Id,true),"채집을 지정했어."); return;
            }
            var pawn=Runner.Session.World.Colonists.Where(x=>x.Position==position).OrderBy(x=>x.Id).FirstOrDefault();
            if(pawn!=null){SelectPawn(pawn.Id);return;}
            var ids=ObjectsAt(position);if(ids.Count>0)SelectObject(ids[0],position);else ClearSelection();
        }
        public void HandleRectangle(GridPosition first, GridPosition last)
        {
            if(IsInputBlocked)return;
            var ok=ActiveTool==Tool.Storage?Runner.CreateStorage(first,last):ActiveTool==Tool.Growing&&Runner.CreateGrowingZone(first,last);
            if(ok)OpenTab(string.Empty);
            Report(ok,"구역을 지정했어.");
        }
        public void BeginTool(Tool tool, BuildingKind kind=BuildingKind.Wall)
        {
            if(IsMenuOpen)return;MapInput?.CancelGesture();ActiveTool=tool;buildingKind=kind;
            Feedback=tool==Tool.Building?ConstructionPanel.Name(kind)+" 배치 · 목재 "+ConstructionRules.Cost(kind)+" · 우클릭 취소":ToolName(tool)+" 지정 · 우클릭 취소";
        }
        private static string ToolName(Tool tool) => tool==Tool.Storage?"저장 구역":tool==Tool.Growing?"경작 구역":tool==Tool.Gather?"채집":"선택";
        private void RefreshPreview()
        {
            if(IsInputBlocked||ActiveTool==Tool.None||!MapInput.HoverPosition.HasValue){Map.SetPreview(null,null,true);return;}
            var last=MapInput.HoverPosition.Value;var first=MapInput.DragStart??last;var w=Runner.Session.World;
            var reason=string.Empty;
            var valid=ActiveTool==Tool.Building?w.CanPlaceBlueprint(buildingKind,last,out reason)
                :ActiveTool==Tool.Storage?w.CanCreateStorage(first,last)
                :ActiveTool==Tool.Growing?w.CanCreateGrowingZone(first,last)
                :w.Gatherables.Any(x=>!x.Depleted&&x.Position==last);
            Map.SetPreview(ActiveTool==Tool.Building||ActiveTool==Tool.Gather?last:first,last,valid,ActiveTool==Tool.Building ? buildingKind : (BuildingKind?)null);
            Feedback=valid?"지정 가능 · "+(ActiveTool==Tool.Building?ConstructionPanel.Name(buildingKind)+" · 목재 "+ConstructionRules.Cost(buildingKind):ToolName(ActiveTool))+" · 우클릭 취소"
                :reason=="OccupiedServiceCell"?"조리대 아래 사용 칸도 비어 있어야 해.":"지정 불가 · 대상이나 다른 구역이 없는 유효한 칸을 선택해줘.";
        }
        public void CancelTool(){if(ActiveTool!=Tool.None)Feedback=string.Empty;ActiveTool=Tool.None;MapInput?.CancelGesture();Map?.SetPreview(null,null,true);}
        private void ShowResult(string message)
        {
            Feedback = message;
            if (ActiveTool == Tool.None) feedbackSeconds = 3f;
        }

        private void Report(bool ok, string success)
        {
            ShowResult(ok ? success : Runner.Session.World.LastCommandError == "NotDrafted"
                ? "R로 소집한 정착민만 직접 이동할 수 있어."
                : "지금은 지정할 수 없어. 대상과 빈 공간을 확인해줘.");
        }

        private List<int> ObjectsAt(GridPosition p)
        {
            var w=Runner.Session.World;var ids=new List<int>();
            ids.AddRange(w.Buildings.Where(x=>x.Exists&&x.Position==p).Select(x=>x.Id).OrderBy(id=>id));
            ids.AddRange(w.Blueprints.Where(x=>x.Exists&&x.Position==p).Select(x=>x.Id).OrderBy(id=>id));
            ids.AddRange(w.Beds.Where(x=>x.Exists&&x.Position==p).Select(x=>x.Id).OrderBy(id=>id));
            ids.AddRange(w.Resources.Where(x=>x.Exists&&x.Position==p).Select(x=>x.Id).OrderBy(id=>id));
            ids.AddRange(w.Gatherables.Where(x=>!x.Depleted&&x.Position==p).Select(x=>x.Id).OrderBy(id=>id));
            ids.AddRange(w.GrowingZones.Where(x=>x.Exists&&x.Cells.Any(c=>c.Position==p)).Select(x=>x.Id).OrderBy(id=>id));
            ids.AddRange(w.StorageZones.Where(x=>x.Exists&&x.Slots.Any(c=>c.Position==p)).Select(x=>x.Id).OrderBy(id=>id));
            ids.AddRange(w.Crops.Where(x=>x.Exists&&x.Position==p).Select(x=>x.Id).OrderBy(id=>id));
            return ids.Distinct().ToList();
        }
        private List<int> SelectionCandidates(GridPosition position)
        {
            var ids = Runner.Session.World.Colonists.Where(p => p.Position == position).Select(p => p.Id).OrderBy(id => id).ToList();
            ids.AddRange(ObjectsAt(position));
            return ids;
        }

        private void SelectNextAt(GridPosition position)
        {
            var candidates = SelectionCandidates(position);
            if (candidates.Count < 2) return;
            var selected = SelectedPawnId ?? SelectedObjectId ?? -1;
            var next = candidates[(candidates.IndexOf(selected) + 1) % candidates.Count];
            if (Runner.Session.World.Colonists.Any(p => p.Id == next)) SelectPawn(next);
            else SelectObject(next, position);
        }

        private bool ObjectExists(int id)
        {
            var w=Runner.Session.World;
            return w.Buildings.Any(x=>x.Id==id&&x.Exists)||w.Blueprints.Any(x=>x.Id==id&&x.Exists)||w.Beds.Any(x=>x.Id==id&&x.Exists)
                ||w.Resources.Any(x=>x.Id==id&&x.Exists)||w.Gatherables.Any(x=>x.Id==id&&!x.Depleted)||w.StorageZones.Any(x=>x.Id==id&&x.Exists)
                ||w.GrowingZones.Any(x=>x.Id==id&&x.Exists)||w.Crops.Any(x=>x.Id==id&&x.Exists);
        }
    }
}
