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
        private readonly HashSet<int> selectedPawns=new HashSet<int>();
        public IReadOnlyCollection<int> SelectedPawnIds => selectedPawns.ToArray();
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
        private static readonly Color PanelColor = new Color(.22f, .22f, .21f, .98f);
        private static readonly Color ControlColor = new Color(.30f, .30f, .28f);
        private static readonly Color ActiveColor = new Color(.48f, .53f, .36f);
        private static readonly Color TextColor = new Color(.96f, .94f, .88f);
        private static readonly Color Accent = new Color(.73f, .80f, .56f);

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
            MapInput.Configure(Map, () => IsInputBlocked, () => ActiveTool != Tool.None, () => ActiveTool == Tool.None || ActiveTool == Tool.Storage || ActiveTool == Tool.Growing || ActiveTool == Tool.Gather, HandleMapClick, HandleRectangle, CancelTool);
            displayed = Runner.Session.World;
            HasSettlement = !startAtMenu;
            RefreshLayout();
            Refresh();
            if (startAtMenu) ShowMainMenu();
        }

        public bool IsInputBlocked => !HasSettlement || IsMenuOpen || IsContextMenuOpen || Runner == null || Runner.SaveBusy || TextFocused();
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
            if (keys.tabKey.wasPressedThisFrame) ProcessShortcut(Key.Tab);
            if (keys.f1Key.wasPressedThisFrame) ProcessShortcut(Key.F1);
            if (keys.commaKey.wasPressedThisFrame) ProcessShortcut(Key.Comma);
            if (keys.periodKey.wasPressedThisFrame) ProcessShortcut(Key.Period);
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
                if (IsContextMenuOpen) { CloseContextMenu(); return true; }
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
                case Key.Tab: ToggleTab("architect"); break;
                case Key.F1: ToggleTab("work"); break;
                case Key.Comma: SelectAdjacentPawn(-1); break;
                case Key.Period: SelectAdjacentPawn(1); break;
                case Key.F3: if (!developmentUi) return false; ToggleTab("development"); break;
                case Key.Space: Runner.SetPaused(!Runner.IsPaused); break;
                case Key.Digit1: Runner.SetSpeed(1); break;
                case Key.Digit3: Runner.SetSpeed(3); break;
                case Key.R: if (!SelectedPawnId.HasValue) return false; ToggleSelectedDraft(); break;
                default: return false;
            }
            return true;
        }

        private void SelectAdjacentPawn(int direction)
        {
            var pawns=Runner.Session.World.Colonists;
            if(pawns.Count==0)return;
            var index=-1;
            for(var i=0;i<pawns.Count;i++)if(pawns[i].Id==SelectedPawnId)index=i;
            index=index<0?(direction>0?0:pawns.Count-1):(index+direction+pawns.Count)%pawns.Count;
            SelectPawn(pawns[index].Id);
            FocusTarget(pawns[index].Position);
        }

        private void LateUpdate()
        {
            if (Runner == null) return;
            if(IsContextMenuOpen&&Mouse.current!=null&&(Mouse.current.leftButton.wasPressedThisFrame||Mouse.current.rightButton.wasPressedThisFrame)&&
                !RectTransformUtility.RectangleContainsScreenPoint(contextMenu,Mouse.current.position.ReadValue()))
            {
                CloseContextMenu();MapInput.CancelGesture();
            }
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
            if (!focused) { CloseContextMenu(); MapInput?.CancelGesture(); draftHeld = true; hadFocus = false; }
        }
        private void OnDestroy() { if (Runner != null) Runner.SetUiBlocked(false); }

        private void Refresh()
        {
            var world = Runner.Session.World;
            if (!ReferenceEquals(displayed, world))
            {
                Feedback=string.Empty;CloseContextMenu();
                MapInput.CancelGesture(); CancelTool(); ClearSelection();
                displayed = world; Map.Bind(world); BuildColonistBar();ClearAlerts();OpenTab(string.Empty);
            }
            if (SelectedPawnId.HasValue && !world.Colonists.Any(x => x.Id == SelectedPawnId)) ClearSelection();
            if (SelectedObjectId.HasValue && !ObjectExists(SelectedObjectId.Value)) ClearSelection();
            Map.Refresh(world, SelectedPawnId, SelectedObjectId, Runner.IsPaused || Runner.IsUiBlocked, Runner.Session.PendingSeconds);
            Map.ShowPawnSelections(selectedPawns,SelectedPawnId);
            RefreshPreview();
            RefreshAlerts();
            RefreshResources(world);
            RefreshPortraitTooltip();
            timeStatus.text = (Runner.IsPaused ? "일시정지" : Runner.SpeedMultiplier + "배속") + " · " + (world.Tick / 10) + "초";
            buttons["pause"].GetComponentInChildren<Text>().text = Runner.IsPaused ? "재생" : "정지";
            SetButtonActive(buttons["pause"], Runner.IsPaused);
            SetButtonActive(buttons["speed1"], !Runner.IsPaused && Runner.SpeedMultiplier == 1);
            SetButtonActive(buttons["speed3"], !Runner.IsPaused && Runner.SpeedMultiplier == 3);
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
                var input = kind == ResourceKind.Wood ? world.ConstructionWood + world.Buildings.Where(b=>b.Exists).Sum(b=>b.RepairWood)
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

        public void SelectPawn(int id, bool additive=false)
        {
            if (!Runner.Session.World.Colonists.Any(x=>x.Id==id)) return;
            OpenTab(string.Empty);
            if(!additive)selectedPawns.Clear();
            if(additive&&selectedPawns.Contains(id))selectedPawns.Remove(id);else selectedPawns.Add(id);
            SelectedPawnId = selectedPawns.Count==0?(int?)null:selectedPawns.OrderBy(x=>x).First();
            SelectedObjectId = null; BuildInspector();
        }
        public void ClearSelection() { selectedPawns.Clear(); SelectedPawnId = null; SelectedObjectId = null; BuildInspector(); }
        public void SelectObject(int id) => SelectObject(id, null);

        private void SelectObject(int id, GridPosition? hitCell)
        {
            OpenTab(string.Empty);
            selectedPawns.Clear();
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
                var enemy = Runner.Session.World.Raiders.LastOrDefault(r => r.Position == position);
                if (enemy != null && Runner.Session.World.RaidPhase == RaidPhase.Active)
                {
                    var ordered = false;
                    foreach (var id in selectedPawns.OrderBy(id => id))
                        ordered |= Runner.OrderAttack(id, enemy.Id);
                    Feedback = ordered ? "적 #" + enemy.Id + " 공격 지정" : selectedPawns.Count==0 ? "소집한 정착민을 선택해." : DirectFailure(Runner.Session.World.LastCommandError);
                    Refresh();
                    return;
                }
                if(ObjectsAt(position).Count>0 || Runner.Session.World.Colonists.Any(p=>p.IsDowned && p.Position==position)){ShowContextMenu(position);return;}
                if (SelectedPawnId.HasValue)MoveSelectedPawns(position);
                return;
            }
            if (ActiveTool == Tool.Building)
            {
                var ok=Runner.PlaceBlueprint(buildingKind,position);

                Report(ok,"건설을 지정했어."); return;
            }
            if (ActiveTool == Tool.Gather)
            {
                var node=Runner.Session.World.Gatherables.FirstOrDefault(x=>!x.Depleted&&x.Position==position);
                Report(node!=null&&Runner.DesignateGather(node.Id,true),"채집을 지정했어."); return;
            }
            var world = Runner.Session.World;
            var pawn = world.Colonists.Where(x =>
            {
                var rendered = Map.DisplayedPawnPosition(world, x);
                return Mathf.FloorToInt(rendered.x) == position.X && Mathf.FloorToInt(rendered.y) == position.Y;
            }).OrderBy(x => x.Id).FirstOrDefault();
            if(pawn!=null){SelectPawn(pawn.Id,ShiftHeld);return;}
            var ids=ObjectsAt(position);if(ids.Count>0)SelectObject(ids[0],position);else ClearSelection();
        }
        public void HandleRectangle(GridPosition first, GridPosition last)
        {
            if(IsInputBlocked)return;
            if(ActiveTool==Tool.None)
            {
                if(first==last){HandleMapClick(last,false);return;}
                var ids=Runner.Session.World.Colonists.Where(p=>InRectangle(p.Position,first,last)).Select(p=>p.Id).ToArray();
                if(!ShiftHeld)ClearSelection();
                foreach(var id in ids)if(!selectedPawns.Contains(id))SelectPawn(id,true);
                return;
            }
            if (ActiveTool == Tool.Gather)
            {
                var count = 0;
                foreach (var node in Runner.Session.World.Gatherables)
                    if (!node.Depleted && InRectangle(node.Position, first, last) && Runner.DesignateGather(node.Id, true)) count++;
                Report(count > 0, "채집 대상 " + count + "개를 지정했어.");
                return;
            }
            var ok=ActiveTool==Tool.Storage?Runner.CreateStorage(first,last):ActiveTool==Tool.Growing&&Runner.CreateGrowingZone(first,last);
            Report(ok,"구역을 지정했어.");
        }
        private void MoveSelectedPawns(GridPosition position)
        {
            var count=0;
            foreach(var id in selectedPawns.OrderBy(x=>x))if(Runner.MoveColonist(id,position))count++;
            Report(count>0,count+"명에게 이동 명령을 보냈어.");
        }
        private static bool ShiftHeld => Keyboard.current!=null&&(Keyboard.current.leftShiftKey.isPressed||Keyboard.current.rightShiftKey.isPressed);
        private void ToggleSelectedDraft()
        {
            var world=Runner.Session.World;
            var draft=world.Colonists.Any(p=>selectedPawns.Contains(p.Id)&&!p.IsDrafted);
            foreach(var pawn in world.Colonists)
                if(selectedPawns.Contains(pawn.Id)&&pawn.IsDrafted!=draft)Runner.ToggleDraft(pawn.Id);
        }
        private static bool InRectangle(GridPosition position, GridPosition first, GridPosition last)
        {
            return position.X >= Math.Min(first.X, last.X) && position.X <= Math.Max(first.X, last.X)
                && position.Y >= Math.Min(first.Y, last.Y) && position.Y <= Math.Max(first.Y, last.Y);
        }
        public void BeginTool(Tool tool, BuildingKind kind=BuildingKind.Wall)
        {
            if(IsMenuOpen)return;MapInput?.CancelGesture();ActiveTool=tool;buildingKind=kind;
            Feedback=tool==Tool.Building?ConstructionPanel.Name(kind)+" 배치 · 목재 "+ConstructionRules.Cost(kind)+" · 우클릭 취소":ToolName(tool)+" 지정 · 우클릭 취소";
        }
        private static string ToolName(Tool tool) => tool==Tool.Storage?"저장 구역":tool==Tool.Growing?"경작 구역":tool==Tool.Gather?"채집":"선택";
        private void RefreshPreview()
        {
            if(IsInputBlocked||!MapInput.HoverPosition.HasValue){Map.SetPreview(null,null,true);return;}
            if(ActiveTool==Tool.None)
            {
                var start=MapInput.DragStart;var end=MapInput.HoverPosition.Value;
                Map.SetPreview(start.HasValue&&start.Value!=end?start:null,start.HasValue&&start.Value!=end?end:(GridPosition?)null,true);
                return;
            }
            var last=MapInput.HoverPosition.Value;var first=MapInput.DragStart??last;var w=Runner.Session.World;
            var reason=string.Empty;
            var valid=ActiveTool==Tool.Building?w.CanPlaceBlueprint(buildingKind,last,out reason)
                :ActiveTool==Tool.Storage?w.CanCreateStorage(first,last)
                :ActiveTool==Tool.Growing?w.CanCreateGrowingZone(first,last)
                :w.Gatherables.Any(x=>!x.Depleted&&InRectangle(x.Position,first,last));
            Map.SetPreview(ActiveTool==Tool.Building?last:first,last,valid,ActiveTool==Tool.Building ? buildingKind : (BuildingKind?)null);
            Feedback=valid?"지정 가능 · "+(ActiveTool==Tool.Building?ConstructionPanel.Name(buildingKind)+" · 목재 "+ConstructionRules.Cost(buildingKind):ToolName(ActiveTool))+" · 우클릭 취소"
                :reason=="OccupiedServiceCell"?"조리대 아래 사용 칸도 비어 있어야 해.":"지정 불가 · 대상이나 다른 구역이 없는 유효한 칸을 선택해줘.";
        }
        public void CancelTool(){if(ActiveTool!=Tool.None)Feedback=string.Empty;ActiveTool=Tool.None;MapInput?.CancelGesture();Map?.SetPreview(null,null,true);}
        private void ShowResult(string message)
        {
            Feedback = message;
            if (ActiveTool == Tool.None) feedbackSeconds = 3f;
        }

        private static string WeaponName(WeaponItemState weapon) => weapon==null ? "미장착" :
            weapon.Definition.Kind==WeaponKind.Melee ? "칼" : weapon.Definition.Id=="rifle" ? "소총" : "카빈";

        private void ReportDirect(bool ok,string action) => ShowResult(ok ? action : DirectFailure(Runner.Session.World.LastCommandError));

        private static string DirectFailure(string code) => code switch
        {
            "WeaponReserved" or "TargetReserved" => "다른 정착민이 이미 맡고 있어.",
            "WeaponNotOnGround" => "바닥에 있는 무기를 선택해.",
            "AttackRequiresDraft" => "소집한 정착민을 선택해.",
            "Unreachable" => "대상까지 갈 수 있는 길이 없어.",
            "ColonistDowned" => "전투 불능인 정착민은 명령을 수행할 수 없어.",
            "MissingMaterials" => "사용할 수 있는 자재가 부족해.",
            "NoRescueBed" => "구조에 쓸 빈 침대가 없어.",
            "PatientReserved" => "다른 정착민이 이미 환자를 돌보고 있어.",
            "RescuerUnavailable" or "Drafted" => "소집을 해제한 활동 가능한 정착민을 선택해.",
            "PatientInBed" => "이미 침대에 있는 환자야.",
            "RescueRequiresDowned" => "전투 불능인 동료를 선택해.",
            "Disabled" => "작업표에서 이 직종을 먼저 켜줘.",
            "NeedsPriority" => "지금은 생존이나 치료가 먼저야.",
            "AwaitingClearance" => "건설 위치가 비어야 작업할 수 있어.",
            "NoDesignation" => "대상에 먼저 작업을 지정해.",
            "WeaponRequired" or "NoWeapon" or "NoWeaponEquipped" => "공격하려면 무기를 장착해.",
            "NoDirectOrder" => "취소할 개인 지시가 없어.",
            _ => "명령을 수행할 수 없어 ("+code+")."
        };

        private void Report(bool ok, string success)
        {
            ShowResult(ok ? success : Runner.Session.World.LastCommandError == "NotDrafted"
                ? "R로 소집한 정착민만 직접 이동할 수 있어."
                : "지금은 지정할 수 없어. 대상과 빈 공간을 확인해줘.");
        }

        private List<int> ObjectsAt(GridPosition p)
        {
            var w=Runner.Session.World;var ids=new List<int>();
            ids.AddRange(w.Weapons.Where(x=>!x.HolderId.HasValue&&x.Position==p).Select(x=>x.Id));
            ids.AddRange(w.Raiders.Where(x=>x.Position==p).Select(x=>x.Id).OrderBy(id=>id));
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
            return w.Weapons.Any(x=>x.Id==id&&!x.HolderId.HasValue)||w.Raiders.Any(x=>x.Id==id)||w.Buildings.Any(x=>x.Id==id&&x.Exists)||w.Blueprints.Any(x=>x.Id==id&&x.Exists)||w.Beds.Any(x=>x.Id==id&&x.Exists)
                ||w.Resources.Any(x=>x.Id==id&&x.Exists)||w.Gatherables.Any(x=>x.Id==id&&!x.Depleted)||w.StorageZones.Any(x=>x.Id==id&&x.Exists)
                ||w.GrowingZones.Any(x=>x.Id==id&&x.Exists)||w.Crops.Any(x=>x.Id==id&&x.Exists);
        }
    }
}
