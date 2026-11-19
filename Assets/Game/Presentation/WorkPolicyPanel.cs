using System;
using System.Collections.Generic;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public sealed class WorkPolicyPanel
    {
        private readonly List<Button> buttons = new List<Button>();
        private WorkPolicyState selected;
        public RectTransform Root { get; private set; }
        public IReadOnlyList<Button> PriorityButtons => buttons.AsReadOnly();
        public Text InfoText { get; private set; }
        public void Build(Transform parent, Font font, Action<WorkCategory, int> change)
        {
            if (Root != null) throw new InvalidOperationException("Work policy panel already built.");
            Root = Rect("Work policy panel", parent, 0, 0, 708, 80);
            for (var i = 0; i < Enum.GetValues(typeof(WorkCategory)).Length; i++)
            {
                var category = (WorkCategory)i;
                var rect = Rect("Priority " + category, Root, i * 89, 40, 85, 36);
                var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.19f, .29f, .31f);
                var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
                var label = TextAt("Label", rect, font, 0, 0, 85, 36); label.alignment = TextAnchor.MiddleCenter;
                button.onClick.AddListener(() => { if (selected != null) change?.Invoke(category, (selected.GetPriority(category) + 1) % 4); });
                buttons.Add(button);
            }
            InfoText = TextAt("Work policy detail", Root, font, 0, 0, 708, 36);
            Refresh(null, string.Empty);
        }
        public void Refresh(WorkPolicyState policy, string reason)
        {
            selected = policy;
            for (var i = 0; i < buttons.Count; i++)
            {
                var category = (WorkCategory)i; var priority = policy?.GetPriority(category) ?? 0;
                buttons[i].interactable = policy != null;
                buttons[i].GetComponentInChildren<Text>().text = WorkDiagnostics.GetCategoryText(category) + " " + (policy == null ? "-" : priority == 0 ? "끔" : priority.ToString());
            }
            InfoText.text = policy == null ? "작업계획: 정착민을 선택하세요" : "정착민 #" + policy.PawnId + " 작업계획 · 1 우선 / 3 후순위 / 끔 제외";
            if (!string.IsNullOrEmpty(reason)) InfoText.text += "\n" + reason;
        }
        private static Text TextAt(string name, Transform parent, Font font, float x, float y, float width, float height)
        {
            var text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = 14; text.color = new Color(.88f, .94f, .94f); text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; return text;
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
    }
}
