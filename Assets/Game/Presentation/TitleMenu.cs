using System;
using System.Linq;
using RimworldTweak.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public sealed class TitleMenu : MonoBehaviour
    {
        private static readonly Color Background = new Color(.115f, .12f, .11f);
        private static readonly Color Muted = new Color(.72f, .72f, .65f);

        private SettlementStorage storage;
        private VisualCatalog visuals;
        private Font font;
        private CanvasScaler scaler;
        private RectTransform root, card, body, footer;
        private ScrollRect scroll;
        private RectTransform viewport;
        private float preferredWidth, preferredHeight;
        private Action<string> enterPlay;
        private Action exit, back;
        private GameObject previousSelection;
        private bool transitioning;

        public void Initialize(string storageRoot, VisualCatalog visuals, Action<string> enterPlay, Action exit)
        {
            this.enterPlay = enterPlay ?? throw new ArgumentNullException(nameof(enterPlay));
            this.exit = exit ?? throw new ArgumentNullException(nameof(exit));
            this.visuals = visuals;
            storage = SettlementStorage.Open(storageRoot);
            font = Resources.Load<Font>("UI/NotoSansCJKkr-Regular");
            if (!font) throw new InvalidOperationException("Bundled Korean font is missing.");

            root = Rect("Title Canvas", transform);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            ApplyScale();
            root.gameObject.AddComponent<GraphicRaycaster>();
            var background = root.gameObject.AddComponent<Image>();
            background.color = Background;
            ShowHome();
        }

        private void ApplyScale()
        {
            scaler.referenceResolution = new Vector2(1920, 1080) * (100f / storage.Settings.UiScalePercent);
        }

        private void Update()
        {
            if (!transitioning && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                back?.Invoke();
        }

        private void LateUpdate()
        {
            if (!card) return;
            SizeCard();
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == previousSelection) return;
            previousSelection = selected;
            if (selected == null || !selected.transform.IsChildOf(body) || !scroll) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
            var clip = viewport.rect;
            var delta = bounds.max.y > clip.yMax ? clip.yMax - bounds.max.y :
                bounds.min.y < clip.yMin ? clip.yMin - bounds.min.y : 0;
            if (delta != 0)
            {
                scroll.StopMovement();
                body.anchoredPosition += Vector2.up * delta;
            }
        }

        private void ShowHome()
        {
            var listing = storage.Slots.ListSlotSummaries();
            var canContinue = !string.IsNullOrEmpty(storage.Settings.RecentSlotId) || listing.Slots.Any(slot => slot.IsValid);
            OpenPage("정착지", null, 460, 480, true);
            var first = AddButton(body, "new", "새 게임", () => Enter(null), 62);
            AddButton(body, "load.menu", "불러오기", () => ShowLoad(), 62);
            var resume = AddButton(body, "continue", "이어하기", Continue, 54);
            SetAvailable(resume, canContinue);
            if (!listing.Success) AddNote(SimulationRunner.SlotError(listing.ErrorCode));
            else if (!canContinue) AddNote("이어갈 저장이 없어.");
            if (!string.IsNullOrEmpty(storage.SettingsWarning)) AddNote(storage.SettingsWarning);
            AddButton(footer, "settings", "화면 설정", ShowSettings, 48);
            AddButton(footer, "exit", "종료", () => exit(), 48);
            Focus(first);
        }

        private void Continue()
        {
            var current = storage.Slots.ListSlotSummaries();
            if (!current.Success)
            {
                ShowLoad(SimulationRunner.SlotError(current.ErrorCode));
                return;
            }
            var recent = storage.Settings.RecentSlotId;
            if (string.IsNullOrEmpty(recent))
            {
                ShowLoad("이어하기 기록이 없어. 최근 저장 후보를 확인하고 선택해줘.",
                    current.Slots.FirstOrDefault(slot => slot.IsValid)?.SlotId);
                return;
            }
            if (current.Slots.Any(slot => slot.IsValid && slot.SlotId == recent)) Enter(recent);
            else ShowLoad("최근 저장을 읽을 수 없어. 목록에서 확인해줘.");
        }

        public void ShowLoadError(string message)
        {
            transitioning = false;
            ShowLoad(message);
        }

        private void ShowLoad(string message = null, string suggested = null)
        {
            OpenPage("불러오기", ShowHome, 620, 558);
            if (!string.IsNullOrEmpty(message)) AddNote(message);
            var listing = storage.Slots.ListSlotSummaries();
            if (!listing.Success) AddNote(SimulationRunner.SlotError(listing.ErrorCode));
            else if (listing.Slots.Count == 0) AddNote("저장된 정착지가 없어.");
            Button first = null;
            foreach (var slot in listing.Slots)
            {
                var isCandidate = suggested == slot.SlotId;
                var caption = Caption(slot);
                var button = AddButton(body, "load." + slot.SlotId,
                    (isCandidate ? "[최근 저장 후보] " : "") + caption, () =>
                    {
                        if (isCandidate) ShowCandidate(slot);
                        else Enter(slot.SlotId);
                    }, 76);
                // Slot names and localized errors can wrap to several lines at larger UI scales.
                var row = button.GetComponent<LayoutElement>();
                row.preferredHeight = -1;
                var content = button.gameObject.AddComponent<VerticalLayoutGroup>();
                content.padding = new RectOffset(8, 8, 2, 2);
                content.childControlWidth = content.childControlHeight = true;
                content.childForceExpandWidth = content.childForceExpandHeight = true;
                SetAvailable(button, slot.IsValid);
                if (slot.IsValid && (first == null || isCandidate)) first = button;
            }
            var cancel = AddButton(footer, "cancel", "돌아가기", ShowHome, 48);
            Focus(first ?? cancel);
        }

        private static string Caption(SaveSlotInfo slot)
        {
            var details = slot.IsValid
                ? $"{slot.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm} · {slot.Tick} Tick · 정착민 {slot.ColonistCount}"
                : SimulationRunner.SlotError(slot.ErrorCode);
            return slot.DisplayName + (slot.IsLegacy ? " [기존 저장]" : "") + "\n" + details;
        }

        private void ShowCandidate(SaveSlotInfo slot)
        {
            Action cancel = () => ShowLoad(null, slot.SlotId);
            OpenPage("이 저장에서 이어갈까?", cancel, 620, 320);
            AddNote(Caption(slot));
            var confirm = AddButton(footer, "load.confirm", "선택한 저장 불러오기", () => Enter(slot.SlotId), 48);
            AddButton(footer, "cancel", "취소", cancel, 48);
            Focus(confirm);
        }

        private void ShowSettings()
        {
            OpenPage("화면 설정", ShowHome, 460, 450);
            AddNote("UI 크기");
            Button selected = null;
            foreach (var percent in new[] { 100, 125, 150 })
            {
                var button = AddButton(body, "scale." + percent, $"UI {percent}%", () =>
                {
                    storage.SetUiScale(percent);
                    ApplyScale();
                    ShowSettings();
                }, 52);
                if (percent == storage.Settings.UiScalePercent)
                {
                    button.targetGraphic.color = MenuStyle.Active;
                    selected = button;
                }
            }
            if (!string.IsNullOrEmpty(storage.SettingsWarning)) AddNote(storage.SettingsWarning);
            AddButton(footer, "cancel", "돌아가기", ShowHome, 48);
            Focus(selected);
        }

        private void Enter(string slot)
        {
            if (transitioning) return;
            transitioning = true;
            var group = card.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            enterPlay(slot);
        }

        private void OpenPage(string title, Action onBack, int width, int height, bool home = false)
        {
            if (card)
            {
                EventSystem.current?.SetSelectedGameObject(null);
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
            back = onBack;
            preferredWidth = width;
            preferredHeight = height;
            card = Rect("Title panel", root);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            SizeCard();
            Surface(card, MenuStyle.Panel);
            card.gameObject.AddComponent<CanvasGroup>();
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 24, 24);
            layout.spacing = 14;
            layout.childControlWidth = layout.childControlHeight = layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = Label(card, "Heading", title, home ? 52 : 33, MenuStyle.Accent);
            header.alignment = home ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            Height(header.rectTransform, home ? 76 : 52);

            viewport = Rect("Menu viewport", card);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            body = Rect("Menu options", viewport);
            body.anchorMin = new Vector2(0, 1);
            body.anchorMax = Vector2.one;
            body.pivot = new Vector2(.5f, 1);
            body.sizeDelta = Vector2.zero;
            var options = body.gameObject.AddComponent<VerticalLayoutGroup>();
            options.spacing = 8;
            options.childControlWidth = options.childControlHeight = options.childForceExpandWidth = true;
            options.childForceExpandHeight = false;
            var fit = body.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = body;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;

            footer = Rect("Menu actions", card);
            Height(footer, 48);
            var actions = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            actions.spacing = 10;
            actions.childControlWidth = actions.childControlHeight = true;
            actions.childForceExpandWidth = actions.childForceExpandHeight = true;
        }

        private void SizeCard()
        {
            var available = root.rect.size;
            var size = new Vector2(Mathf.Clamp(available.x - 32, 1, preferredWidth),
                Mathf.Clamp(available.y - 32, 1, preferredHeight));
            if (card.sizeDelta != size) card.sizeDelta = size;
        }

        private void AddNote(string value)
        {
            var text = Label(body, "Menu message", value, 18, Muted);
            text.alignment = TextAnchor.MiddleCenter;
            // Text provides its wrapped preferred height to the vertical layout.
            text.gameObject.AddComponent<LayoutElement>().minHeight = 32;
        }

        private Button AddButton(RectTransform parent, string key, string caption, Action action, int height)
        {
            var rect = Rect(key, parent);
            Height(rect, height);
            var image = Surface(rect, MenuStyle.Control);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            MenuStyle.ApplyButtonColors(button);
            var label = Label(rect, "Caption", caption, height > 48 ? 23 : 19, MenuStyle.Text);
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8, 2);
            label.rectTransform.offsetMax = new Vector2(-8, -2);
            button.onClick.AddListener(() => { if (!transitioning && button.IsInteractable()) action(); });
            return button;
        }

        private static void SetAvailable(Button button, bool available)
        {
            button.interactable = available;
            if (!available) button.GetComponentInChildren<Text>().color = Muted;
        }

        private static void Focus(Button button)
        {
            if (button) EventSystem.current?.SetSelectedGameObject(button.gameObject);
        }

        private Image Surface(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            MenuStyle.ApplySurface(image, visuals);
            return image;
        }

        private Text Label(RectTransform parent, string name, string value, int size, Color color)
        {
            var text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Height(RectTransform rect, float height)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = height;
            element.flexibleHeight = 0;
        }
    }
}
