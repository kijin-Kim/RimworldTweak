using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public sealed partial class SettlementView : MonoBehaviour
    {
        private readonly List<WorldMarker> markers = new List<WorldMarker>();
        private readonly List<Button> colonistButtons = new List<Button>();
        private readonly List<RectTransform> pathDots = new List<RectTransform>();
        private IReadOnlyList<WorldMarker> readonlyMarkers;
        private IReadOnlyList<Button> readonlyButtons;
        private GameObject uiRoot;
        private RectTransform board, sidebar, selectedOutline, destinationOutline, pendingOutline;
        private Font font;
        private Sprite triangle, square, outlineSprite;
        private Texture2D triangleTexture, squareTexture, outlineTexture;
        private Text statusText, pauseLabel, feedbackText;
        private WorldState displayedWorld;
        private bool draftKeyHeld;
        private long displayedRevision = -1;
        private static readonly Color Ink = new Color(0.88f, 0.91f, 0.90f);
        private static readonly Color Muted = new Color(0.56f, 0.65f, 0.66f);
        private static readonly Color ColonistColor = new Color(0.45f, 0.83f, 0.76f);
        private static readonly Color SelectedColor = new Color(0.98f, 0.87f, 0.47f);
        private static readonly Color PendingColor = new Color(0.80f, 0.62f, 0.94f);

        public SimulationRunner Runner { get; private set; }
        public IReadOnlyList<WorldMarker> Markers => readonlyMarkers ?? (readonlyMarkers = markers.AsReadOnly());
        public IReadOnlyList<Button> ColonistButtons => readonlyButtons ?? (readonlyButtons = colonistButtons.AsReadOnly());
        public int? SelectedColonistId { get; private set; }
        public RectTransform BoardRect => board;
        public Text TickText { get; private set; }
        public Text SelectionText { get; private set; }
        public Text NeedsText { get; private set; }
        public Text LifeText { get; private set; }
        public Text ReservationText { get; private set; }
        public Text CommandFeedbackText => feedbackText;
        public Button ResetButton { get; private set; }
        public Button PauseButton { get; private set; }
        public Button StepButton { get; private set; }
        public Button Speed1Button { get; private set; }
        public Button Speed3Button { get; private set; }
        public Button DraftButton { get; private set; }

        public void Bind(SimulationRunner runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (Runner != runner) SelectedColonistId = null;
            Runner = runner;
            if (uiRoot == null) BuildUI();
            RefreshNow();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) { draftKeyHeld = false; return; }
            if (keyboard.spaceKey.wasPressedThisFrame) ProcessShortcut(Key.Space);
            if (keyboard.digit1Key.wasPressedThisFrame) ProcessShortcut(Key.Digit1);
            if (keyboard.digit3Key.wasPressedThisFrame) ProcessShortcut(Key.Digit3);
            if (keyboard.periodKey.wasPressedThisFrame) ProcessShortcut(Key.Period);
            if (keyboard.escapeKey.wasPressedThisFrame) ProcessShortcut(Key.Escape);
            var draftPressed = keyboard.rKey.isPressed;
            if (draftPressed && !draftKeyHeld) ProcessShortcut(Key.R);
            draftKeyHeld = draftPressed;
        }

        public bool ProcessShortcut(Key key)
        {
            if (Runner == null || TextInputFocused()) return false;
            switch (key)
            {
                case Key.Space: Runner.SetPaused(!Runner.IsPaused); break;
                case Key.Digit1: Runner.SetSpeed(1); break;
                case Key.Digit3: Runner.SetSpeed(3); break;
                case Key.Period:
                    if (!Runner.IsPaused) return false;
                    Runner.StepOnce(); break;
                case Key.Escape: if (CancelWorkTool()) return true; ClearSelection(); ClearObjectSelection(); return true;
                case Key.R: return ToggleDraftSelected();
                default: return false;
            }
            RefreshNow();
            return true;
        }

        private static bool TextInputFocused()
        {
            var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            return selected != null && selected.GetComponentInParent<InputField>() != null;
        }

        public void SelectColonist(int id)
        {
            RefreshNow();
            SelectedColonistId = null;
            if (Runner != null)
                foreach (var colonist in Runner.Session.World.Colonists)
                    if (colonist.Id == id) { SelectedColonistId = id; break; }
            RefreshNow();
        }

        public void ClearSelection() { SelectedColonistId = null; RefreshNow(); }

        public bool MoveSelected(GridPosition target)
        {
            RefreshNow();
            if (Runner == null || !SelectedColonistId.HasValue) return false;
            var accepted = Runner.MoveColonist(SelectedColonistId.Value, target);
            RefreshNow();
            return accepted;
        }

        public bool ToggleDraftSelected()
        {
            if (Runner == null || !SelectedColonistId.HasValue) return false;
            var accepted = Runner.ToggleDraft(SelectedColonistId.Value);
            RefreshNow();
            return accepted;
        }

        public bool HandleMapClick(GridPosition position, bool rightButton)
        {
            if (Runner == null) return false;
            var world = Runner.Session.World;
            if (position.X < 0 || position.Y < 0 || position.X >= world.Width || position.Y >= world.Height) return false;
            if (TextInputFocused()) return false;
            if (HandleWorkToolClick(position, rightButton, out var handled)) return handled;
            if (rightButton) return MoveSelected(position);
            SelectWorldObject(position);
            int? found = null;
            foreach (var colonist in world.Colonists)
                if (colonist.Position == position && (!found.HasValue || colonist.Id < found.Value)) found = colonist.Id;
            if (found.HasValue) SelectColonist(found.Value); else ClearSelection();
            return true;
        }

        public bool TryScreenToGrid(Vector2 screenPosition, out GridPosition position)
        {
            position = default;
            if (board == null || Runner == null) return false;
            var canvas = board.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screenPosition, camera, out var local)) return false;
            var rect = board.rect;
            if (local.x < rect.xMin || local.y < rect.yMin || local.x >= rect.xMax || local.y >= rect.yMax) return false;
            var world = Runner.Session.World;
            position = new GridPosition(Mathf.FloorToInt((local.x - rect.xMin) * world.Width / rect.width),
                Mathf.FloorToInt((local.y - rect.yMin) * world.Height / rect.height));
            return position.X >= 0 && position.Y >= 0 && position.X < world.Width && position.Y < world.Height;
        }

        private void LateUpdate() { RefreshNow(); }

        public void RefreshNow()
        {
            if (Runner == null || Runner.Session == null || uiRoot == null) return;
            var world = Runner.Session.World;
            if (!ReferenceEquals(displayedWorld, world))
            {
                SelectedColonistId = null;
                ResetWorkView(world);
                DrawWorld(world);
                EnsureSelectionUI(world);
                displayedWorld = world;
                displayedRevision = world.MapRevision;
            }
            else if (displayedRevision != world.MapRevision || displayedResourceCount != world.Resources.Count)
            {
                DrawWorld(world);
                displayedRevision = world.MapRevision;
            }
            UpdateColonistMarkers(world);
            UpdateLivingMarkers(world);
            TickText.text = "TICK  " + world.Tick.ToString(CultureInfo.InvariantCulture);
            statusText.text = (Runner.IsPaused ? "PAUSED" : "RUNNING") + "  /  " + Runner.SpeedMultiplier + "x   /   RESETS " + Runner.Session.ResetCount;
            pauseLabel.text = Runner.IsPaused ? "재개" : "일시정지";
            StepButton.interactable = Runner.IsPaused;
            Speed1Button.GetComponent<Image>().color = Runner.SpeedMultiplier == 1 ? new Color(0.30f, 0.44f, 0.42f) : new Color(0.19f, 0.29f, 0.31f);
            Speed3Button.GetComponent<Image>().color = Runner.SpeedMultiplier == 3 ? new Color(0.30f, 0.44f, 0.42f) : new Color(0.19f, 0.29f, 0.31f);
            RefreshSelection(world);
            RefreshWorkUI(world);
            RefreshInspection(world);
        }

        private void BuildUI()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
            square = MakeSprite(false, out squareTexture);
            triangle = MakeSprite(true, out triangleTexture);
            outlineSprite = MakeOutlineSprite(out outlineTexture);
            uiRoot = new GameObject("Settlement UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot.transform.SetParent(transform, false);
            uiRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = uiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = Panel("Background", uiRoot.transform, 0, 0, 1280, 800, new Color(0.055f, 0.085f, 0.10f));
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            Label("Title", uiRoot.transform, "SETTLEMENT / 01", 64, 706, 900, 44, 32, Ink);
            Label("Subtitle", uiRoot.transform, "SETTLEMENT LIFE   /   SELECT A COLONIST TO INSPECT NEEDS AND AUTOMATIC ACTIONS", 66, 673, 1150, 28, 15, Muted);
            board = Panel("Board", uiRoot.transform, 64, 100, 552, 552, new Color(0.10f, 0.15f, 0.17f));
            board.GetComponent<Image>().raycastTarget = true;
            board.gameObject.AddComponent<BoardPointerHandler>().View = this;
            sidebar = Panel("Information", uiRoot.transform, 660, 100, 556, 552, new Color(0.08f, 0.12f, 0.14f));
            sidebar.GetComponent<Image>().raycastTarget = true;
            TickText = Label("Tick", sidebar, "", 28, 500, 500, 38, 30, Ink);
            statusText = Label("Status", sidebar, "", 28, 467, 500, 28, 16, Muted);
            PauseButton = MakeButton("Pause", sidebar, 28, 418, 150);
            pauseLabel = PauseButton.GetComponentInChildren<Text>();
            StepButton = MakeButton("Step +1", sidebar, 194, 418, 150);
            ResetButton = MakeButton("Reset", sidebar, 360, 418, 168);
            Speed1Button = MakeButton("1x [1]", sidebar, 28, 366, 150);
            Speed3Button = MakeButton("3x [3]", sidebar, 194, 366, 150);
            DraftButton = MakeButton("Draft [R]", sidebar, 360, 366, 168);
            PauseButton.onClick.AddListener(() => { if (Runner != null) Runner.SetPaused(!Runner.IsPaused); RefreshNow(); });
            StepButton.onClick.AddListener(() => { if (Runner != null) Runner.StepOnce(); RefreshNow(); });
            ResetButton.onClick.AddListener(() => { if (Runner != null) Runner.ResetWorld(); RefreshNow(); });
            Speed1Button.onClick.AddListener(() => { if (Runner != null) Runner.SetSpeed(1); RefreshNow(); });
            Speed3Button.onClick.AddListener(() => { if (Runner != null) Runner.SetSpeed(3); RefreshNow(); });
            DraftButton.onClick.AddListener(() => ToggleDraftSelected());
            SelectionText = Label("Selection", sidebar, "", 28, 270, 500, 42, 15, Ink);
            NeedsText = Label("Needs", sidebar, "", 28, 243, 500, 26, 16, Ink);
            LifeText = Label("Life", sidebar, "", 28, 164, 500, 76, 14, Ink);
            feedbackText = Label("Command feedback", sidebar, "", 28, 137, 500, 26, 13, new Color(0.97f, 0.66f, 0.50f));
            ReservationText = Label("Reservations", sidebar, "", 28, 47, 500, 113, 13, Ink);
            Legend(sidebar, "Colonist", 24, triangle, ColonistColor, 28);
            Legend(sidebar, "Meal", 24, square, ResourceColor(ResourceKind.Meal), 198);
            Legend(sidebar, "Bed", 24, outlineSprite, new Color(0.48f, 0.69f, 0.88f), 366);
            Label("Route key", sidebar, "Gold: selected / target   Teal: route   Violet: pending", 28, 2, 500, 20, 12, Muted);
            Label("Controls", uiRoot.transform, "SPACE pause / resume     . step while paused     ESC clear selection     R draft / undraft", 64, 53, 1150, 26, 16, Ink);
            Label("Scope", uiRoot.transform, "R toggles player control. Objects have one owner, including while approaching.", 64, 22, 1150, 25, 15, Muted);
            selectedOutline = Outline("Selected colonist", SelectedColor);
            destinationOutline = Outline("Active destination", SelectedColor);
            pendingOutline = Outline("Pending destination", PendingColor);
            BuildWorkUI();
            BuildInspectionLayout();
        }

        private RectTransform Outline(string name, Color color)
        {
            var rect = Panel(name, board, 0, 0, 1, 1, color);
            rect.GetComponent<Image>().sprite = outlineSprite;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.gameObject.SetActive(false);
            return rect;
        }

        private void EnsureSelectionUI(WorldState world)
        {
            var capacity = world.Width * world.Height;
            while (pathDots.Count < capacity)
            {
                var dot = Panel("Route dot", board, 0, 0, 1, 1, ColonistColor);
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.gameObject.SetActive(false);
                pathDots.Add(dot);
            }
            while (colonistButtons.Count < world.Colonists.Count)
            {
                var index = colonistButtons.Count;
                var button = MakeButton("", uiRoot.transform, 32 + index * 158, 750, 150);
                button.onClick.AddListener(() => {
                    if (Runner != null && index < Runner.Session.World.Colonists.Count)
                        SelectColonist(Runner.Session.World.Colonists[index].Id);
                });
                colonistButtons.Add(button);
            }
            while (colonistButtons.Count > world.Colonists.Count)
            {
                var last = colonistButtons.Count - 1;
                var obsolete = colonistButtons[last].gameObject;
                obsolete.SetActive(false);
                obsolete.transform.SetParent(null, false);
                Destroy(obsolete);
                colonistButtons.RemoveAt(last);
            }
            for (var i = 0; i < colonistButtons.Count; i++)
            {
                colonistButtons[i].gameObject.SetActive(i < world.Colonists.Count);
                if (i < world.Colonists.Count) colonistButtons[i].GetComponentInChildren<Text>().text = "Colonist " + world.Colonists[i].Id;
            }
        }

        private void RefreshSelection(WorldState world)
        {
            ColonistState selected = null;
            foreach (var colonist in world.Colonists)
                if (SelectedColonistId == colonist.Id) selected = colonist;
            if (selected == null) SelectedColonistId = null;
            DraftButton.interactable = selected != null;
            DraftButton.GetComponentInChildren<Text>().text = selected != null && selected.IsDrafted ? "Undraft [R]" : "Draft [R]";
            foreach (var dot in pathDots) dot.gameObject.SetActive(false);
            selectedOutline.gameObject.SetActive(false);
            destinationOutline.gameObject.SetActive(false);
            pendingOutline.gameObject.SetActive(false);
            var cell = 480f / Mathf.Max(world.Width, world.Height);
            if (selected == null)
            {
                SelectionText.text = "NO COLONIST SELECTED\nClick a triangle or a colonist button.";
                NeedsText.text = "NUTRITION --   REST --";
                LifeText.text = "Select a colonist to see action, progress and reason.\nRight-click a tile to request movement.";
            }
            else
            {
                SelectionText.text = "COLONIST " + selected.Id + " @ " + selected.Position + "   " + selected.Status.ToString().ToUpperInvariant() +
                    "   STEP " + selected.MovementProgress + " / " + selected.MovementTicksPerTile + "\nTARGET " + (selected.Destination.HasValue ? selected.Destination.Value.ToString() : "--");
                if (selected.HasPendingCommand) SelectionText.text += "   PENDING " + (selected.PendingDestination.HasValue ? selected.PendingDestination.Value.ToString() : "STOP");
                NeedsText.text = "NUTRITION " + NeedValue(selected.Nutrition) + " / 100   REST " + NeedValue(selected.Rest) + " / 100";
                LifeText.text = selected.IsDrafted ? "DRAFTED - PLAYER CONTROL\nAutomatic needs actions suspended.\nRight-click to move; own tile to stop.\nR / Undraft returns to automatic life." : world.NeedsEnabled ? ActivityName(selected.LifeActivity) + "   TARGET " + (selected.LifeTargetId.HasValue ? selected.LifeTargetId.Value.ToString(CultureInfo.InvariantCulture) : "--") +
                    "\n" + ActionProgressText(selected) + (selected.Nutrition == 0 ? "   STARVING" : "") + (selected.Rest == 0 ? "   EXHAUSTED" : "") +
                    "\nFood: " + LifeReasonMessage(selected.FoodReason) + "\nSleep: " + LifeReasonMessage(selected.SleepReason) : "Automatic needs are disabled in this scenario.";
                PlaceOverlay(selectedOutline, selected.Position, cell, 0.80f);
                if (selected.Destination.HasValue) PlaceOverlay(destinationOutline, selected.Destination.Value, cell, 0.90f);
                if (selected.HasPendingCommand && selected.PendingDestination.HasValue) PlaceOverlay(pendingOutline, selected.PendingDestination.Value, cell, 0.72f);
                for (var i = 0; i < selected.Path.Count - 1 && i < pathDots.Count; i++)
                {
                    PlaceOverlay(pathDots[i], selected.Path[i], cell, 0.13f);
                    // Keep the route cue inside the tile's lower margin, clear of resource amounts.
                    pathDots[i].anchoredPosition += Vector2.down * cell * 0.4f;
                }
            }
            for (var i = 0; i < colonistButtons.Count && i < world.Colonists.Count; i++)
                colonistButtons[i].GetComponent<Image>().color = SelectedColonistId == world.Colonists[i].Id ? new Color(0.39f, 0.38f, 0.22f) : new Color(0.19f, 0.29f, 0.31f);
            if (selected != null && !selected.IsDrafted && selected.LifeActivity == LifeActivity.Idle && (selected.IsWorking || !string.IsNullOrEmpty(selected.WorkReason)))
                LifeText.text = "WORK " + selected.WorkActivity + "  TARGET " + selected.WorkTargetId +
                    "\nCARGO " + selected.CargoKind + " " + selected.CargoAmount + "\n" + selected.WorkReason +
                    "\nFood: " + LifeReasonMessage(selected.FoodReason) + " / Sleep: " + LifeReasonMessage(selected.SleepReason);
            feedbackText.text = ErrorMessage(world.LastCommandError);
        }

        private static string ErrorMessage(string code)
        {
            switch (code)
            {
                case "InvalidColonist": return "No valid colonist selected.";
                case "OutOfBounds": return "Outside the map. Previous order kept.";
                case "BlockedDestination": return "Target is blocked. Previous order kept.";
                case "Unreachable": return "No route to target. Previous order kept.";
                case "OccupiedTile": return "Cannot block an occupied tile.";
                case "WorkBusy": return "Automatic work active. Press R to draft, then move.";
                case "LifeBusy": return "Automatic life active. Press R to draft, then move.";
                default: return string.IsNullOrEmpty(code) ? "" : code;
            }
        }

        private static string NeedValue(int value) => (value / 100m).ToString("0.00", CultureInfo.InvariantCulture);

        private static string ActionProgressText(ColonistState colonist)
        {
            if (colonist.LifeActivity == LifeActivity.Eating)
                return "ACTION " + colonist.ActionProgress + " / " + colonist.ActionDuration + " ticks";
            if (colonist.LifeActivity == LifeActivity.SleepingInBed || colonist.LifeActivity == LifeActivity.SleepingOnFloor)
                return "SLEPT " + colonist.ActionProgress + " ticks / WAKE REST 90";
            return "ACTION --";
        }

        private static string ActivityName(LifeActivity activity)
        {
            switch (activity)
            {
                case LifeActivity.GoingToFood: return "GOING TO FOOD";
                case LifeActivity.Eating: return "EATING";
                case LifeActivity.GoingToBed: return "GOING TO BED";
                case LifeActivity.SleepingInBed: return "SLEEPING IN BED";
                case LifeActivity.SleepingOnFloor: return "SLEEPING ON FLOOR";
                default: return "IDLE";
            }
        }

        private static string LifeReasonMessage(string code)
        {
            switch (code)
            {
                case "NoFood": return "No usable food; waiting for supply.";
                case "FoodReserved": return "Food is reserved by other colonists.";
                case "FoodUnreachable": return "No route to food.";
                case "NoBed": return "No bed; using the floor.";
                case "BedReserved": return "Beds reserved; using the floor.";
                case "BedUnreachable": return "No route to beds; using the floor.";
                case "Starving": return "Starving: the next tile takes 10 ticks.";
                default: return string.IsNullOrEmpty(code) ? "No warning." : code;
            }
        }

        private static void PlaceOverlay(RectTransform rect, GridPosition position, float cell, float scale)
        {
            rect.gameObject.SetActive(true);
            rect.anchoredPosition = new Vector2((position.X + 0.5f) * cell, (position.Y + 0.5f) * cell);
            rect.sizeDelta = Vector2.one * cell * scale;
            rect.SetAsLastSibling();
        }

        private void UpdateColonistMarkers(WorldState world)
        {
            var cell = 480f / Mathf.Max(world.Width, world.Height);
            var offset = world.Width * world.Height;
            for (var i = 0; i < world.Colonists.Count; i++)
            {
                var colonist = world.Colonists[i];
                var marker = markers[offset + i];
                marker.SetState(ViewKind.Colonist, colonist.Id, colonist.Position, 0, false);
                var rect = (RectTransform)marker.transform;
                rect.anchoredPosition = new Vector2((colonist.Position.X + 0.5f) * cell, (colonist.Position.Y + 0.5f) * cell);
                // Identity rendering offsets the person inside this logical tile when sharing an object.
                rect.sizeDelta = Vector2.one * cell * 0.57f;
                rect.SetAsLastSibling();
            }
        }

        private void UpdateLivingMarkers(WorldState world)
        {
            var index = world.Width * world.Height + world.Colonists.Count;
            long wood = 0, meals = 0, ingredients = 0, mealReserved = 0, ingredientReserved = 0, mealOpen = 0, ingredientOpen = 0;
            foreach (var resource in world.Resources)
            {
                var marker = markers[index++];
                marker.SetState(ViewKind.Resource, resource.Id, resource.Position, resource.Amount, false);
                marker.gameObject.SetActive(resource.Exists && resource.Amount > 0);
                var label = marker.GetComponentInChildren<Text>(true);
                label.text = resource.Amount.ToString(CultureInfo.InvariantCulture);
                var occupied = false;
                foreach (var colonist in world.Colonists)
                    if (colonist.Position == resource.Position) occupied = true;
                label.alignment = occupied ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter;
                label.resizeTextMaxSize = occupied ? 12 : 21;
                if (!resource.Exists) continue;
                if (resource.Kind == ResourceKind.Wood) wood += resource.Amount;
                else if (resource.Kind == ResourceKind.Meal) { meals += resource.Amount; mealReserved += resource.ReservedAmount; mealOpen += resource.AccessibleAmount; }
                else { ingredients += resource.Amount; ingredientReserved += resource.ReservedAmount; ingredientOpen += resource.AccessibleAmount; }
            }
            var summary = new StringBuilder("STOCK    Total / Held / Open to others");
            summary.Append("\nWOOD ").Append(wood).Append("    MEALS ").Append(meals).Append(" / ").Append(mealReserved).Append(" / ").Append(mealOpen);
            summary.Append("\nINGREDIENTS ").Append(ingredients).Append(" / ").Append(ingredientReserved).Append(" / ").Append(ingredientOpen);
            foreach (var resource in world.Resources)
                if (resource.Exists && resource.Kind != ResourceKind.Wood)
                    summary.Append("\nFOOD ").Append(resource.Id).Append(!resource.OwnerId.HasValue ? " FREE" : (resource.IsInUse ? " USING C" : " APPROACH C") + resource.OwnerId.Value);
            foreach (var bed in world.Beds)
            {
                var marker = markers[index++];
                marker.gameObject.SetActive(bed.Exists);
                marker.GetComponent<Image>().color = bed.IsOccupied ? PendingColor : bed.OwnerId.HasValue ? SelectedColor : new Color(0.48f, 0.69f, 0.88f);
                summary.Append("\nBED ").Append(bed.Id).Append("  ");
                if (!bed.Exists) summary.Append("REMOVED");
                else if (!bed.OwnerId.HasValue) summary.Append("FREE");
                else summary.Append(bed.IsOccupied ? "SLEEPING C" : "APPROACH C").Append(bed.OwnerId.Value);
            }
            ReservationText.text = summary.ToString();
        }

        private void DrawWorld(WorldState world)
        {
            displayedResourceCount = world.Resources.Count;
            var cell = 480f / Mathf.Max(world.Width, world.Height);
            board.sizeDelta = new Vector2(cell * world.Width, cell * world.Height);
            var blocked = new HashSet<GridPosition>(world.BlockedTiles);
            var index = 0;
            for (var y = 0; y < world.Height; y++)
                for (var x = 0; x < world.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    var isBlocked = blocked.Contains(position);
                    SetMarker(index++, ViewKind.Tile, 0, position, 0, isBlocked, cell, 0.95f, square,
                        isBlocked ? new Color(0.035f, 0.05f, 0.06f) : new Color(0.18f, 0.25f, 0.26f));
                }
            foreach (var colonist in world.Colonists)
                SetMarker(index++, ViewKind.Colonist, colonist.Id, colonist.Position, 0, false, cell, 0.57f, triangle, ColonistColor);
            foreach (var resource in world.Resources)
            {
                SetMarker(index++, ViewKind.Resource, resource.Id, resource.Position, resource.Amount, false, cell, 0.66f, square, ResourceColor(resource.Kind));
            }
            foreach (var bed in world.Beds)
                SetMarker(index++, ViewKind.Bed, bed.Id, bed.Position, 0, false, cell, 0.94f, outlineSprite, Muted);
            for (var i = markers.Count - 1; i >= index; i--)
            {
                var obsolete = markers[i].gameObject;
                obsolete.SetActive(false);
                obsolete.transform.SetParent(null, false);
                Destroy(obsolete);
                markers.RemoveAt(i);
            }
        }

        private void SetMarker(int index, ViewKind kind, int id, GridPosition position, int amount, bool blocked,
            float cell, float scale, Sprite sprite, Color color)
        {
            WorldMarker marker;
            if (index == markers.Count)
            {
                var rect = Panel("Marker", board, 0, 0, 1, 1, color);
                marker = rect.gameObject.AddComponent<WorldMarker>();
                var amountText = Label("Amount", rect, "", 0, 0, 1, 1, 21, new Color(0.07f, 0.09f, 0.10f));
                var textRect = amountText.rectTransform;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                amountText.alignment = TextAnchor.MiddleCenter;
                amountText.resizeTextForBestFit = true;
                amountText.resizeTextMinSize = 8;
                amountText.resizeTextMaxSize = 21;
                markers.Add(marker);
            }
            else marker = markers[index];
            marker.gameObject.SetActive(true);
            marker.name = kind + " " + (kind == ViewKind.Tile ? position.ToString() : id.ToString(CultureInfo.InvariantCulture));
            marker.SetState(kind, id, position, amount, blocked);
            var markerRect = (RectTransform)marker.transform;
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.anchoredPosition = new Vector2((position.X + 0.5f) * cell, (position.Y + 0.5f) * cell);
            markerRect.sizeDelta = Vector2.one * cell * scale;
            markerRect.SetSiblingIndex(index);
            var image = marker.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            var label = marker.GetComponentInChildren<Text>(true);
            label.gameObject.SetActive(kind == ViewKind.Resource || kind == ViewKind.Bed);
            label.text = kind == ViewKind.Bed ? "B" + id : amount.ToString(CultureInfo.InvariantCulture);
            label.color = kind == ViewKind.Bed ? Ink : new Color(0.07f, 0.09f, 0.10f);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.alignment = kind == ViewKind.Bed ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter;
            label.resizeTextMaxSize = kind == ViewKind.Bed ? 10 : 21;
        }

        private static Color ResourceColor(ResourceKind kind)
        {
            return kind == ResourceKind.Wood ? new Color(0.82f, 0.62f, 0.36f) :
                kind == ResourceKind.Meal ? new Color(0.78f, 0.81f, 0.48f) : new Color(0.63f, 0.76f, 0.49f);
        }

        private void Legend(Transform parent, string text, float y, Sprite sprite, Color color, float x)
        {
            var icon = Panel(text + " icon", parent, x, y, 20, 20, color);
            icon.GetComponent<Image>().sprite = sprite;
            Label(text, parent, text, x + 30, y - 3, 120, 26, 15, Ink);
        }

        private Button MakeButton(string text, Transform parent, float x, float y, float width)
        {
            var rect = Panel(text, parent, x, y, width, 42, new Color(0.19f, 0.29f, 0.31f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.targetGraphic.raycastTarget = true;
            var label = Label("Label", rect, text, 0, 0, width, 42, 18, Ink);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var rect = Rect(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private Text Label(string name, Transform parent, string value, float x, float y, float width, float height, int size, Color color)
        {
            var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.text = value;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }

        private static Sprite MakeSprite(bool isTriangle, out Texture2D texture)
        {
            const int size = 64;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = isTriangle ? "Colonist triangle" : "Resource square", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = !isTriangle || Mathf.Abs(x - 31.5f) <= (63 - y) * 0.5f ? Color.white : Color.clear;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite MakeOutlineSprite(out Texture2D texture)
        {
            const int size = 64;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Selection border", filterMode = FilterMode.Point };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = x < 3 || y < 3 || x >= size - 3 || y >= size - 3 ? Color.white : Color.clear;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
        private void OnDestroy()
        {
            if (uiRoot != null) { uiRoot.SetActive(false); Destroy(uiRoot); }
            if (triangle != null) Destroy(triangle);
            if (square != null) Destroy(square);
            if (outlineSprite != null) Destroy(outlineSprite);
            if (triangleTexture != null) Destroy(triangleTexture);
            if (squareTexture != null) Destroy(squareTexture);
            if (outlineTexture != null) Destroy(outlineTexture);
            if (font != null) Destroy(font);
            markers.Clear();
            colonistButtons.Clear();
            pathDots.Clear();
        }
    }
}
