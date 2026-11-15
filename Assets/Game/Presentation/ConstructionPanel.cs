using System;
using System.Collections.Generic;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.UI;
namespace RimworldTweak.Presentation
{
    public sealed class ConstructionPanel
    {
        public RectTransform Root { get; private set; }
        public IReadOnlyList<Button> BuildButtons => buttons.AsReadOnly();
        public Button CancelButton { get; private set; }
        public Button DemolishButton { get; private set; }
        public Text DetailText { get; private set; }
        private readonly List<Button> buttons = new List<Button>();
        private Font font;
        public void Initialize(Transform parent, Font textFont, Action<BuildingKind> beginPlacement,
            Action cancelBlueprint, Action toggleDemolition)
        {
            font = textFont;
            Root = Rect("Construction panel", parent, 0, 0, 708, 145);
            foreach (BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
            {
                var captured = kind;
                buttons.Add(Button(Name(kind) + " " + ConstructionRules.Cost(kind), buttons.Count * 114, 105, 108,
                    () => beginPlacement(captured)));
            }
            CancelButton = Button("설계도 취소", 460, 105, 118, cancelBlueprint);
            DemolishButton = Button("철거 지정", 584, 105, 118, toggleDemolition);
            DetailText = Label("Construction details", Root, 0, 0, 702, 98, 15);
            Refresh(null, null, string.Empty);
        }
        public void Refresh(BlueprintState blueprint, BuildingState building, string error)
        {
            CancelButton.interactable = blueprint != null && blueprint.Exists;
            DemolishButton.interactable = building != null && building.Exists;
            DemolishButton.GetComponentInChildren<Text>().text = building != null && building.DemolitionDesignated ? "철거 취소" : "철거 지정";
            var detail = "건설: 종류 선택 후 한 칸 지정 · 성공하면 도구 해제\n우클릭/Esc: 도구 취소 · 조리대 완공 후 조리 주문을 설정하세요";
            if (blueprint != null && blueprint.Exists)
                detail = Name(blueprint.Kind) + " 설계도 #" + blueprint.Id + " · 목재 " + blueprint.DeliveredWood + "/" + blueprint.Cost +
                    " · 시공 " + blueprint.Progress + "/" + blueprint.Duration + "\n담당 " + Owner(blueprint.OwnerId) + " · " + Reason(blueprint.Reason);
            else if (building != null && building.Exists)
                detail = Name(building.Kind) + " #" + building.Id + (building.DemolitionDesignated ? " · 철거 " + building.DemolitionProgress + "/30" : " · 완공") +
                    "\n담당 " + Owner(building.OwnerId) + " · " + Reason(building.Reason) + (building.Kind == BuildingKind.Stove ? " · 조리 주문 별도 설정" : "");
            if (!string.IsNullOrEmpty(error)) detail += "\n" + Error(error);
            DetailText.text = detail;
        }
        public static string Name(BuildingKind kind)
        { switch (kind) { case BuildingKind.Wall: return "벽"; case BuildingKind.Door: return "문";
            case BuildingKind.Bed: return "침대"; default: return "조리대"; } }
        private static string Owner(int? id) => id.HasValue ? "정착민 " + id.Value : "없음";
        private static string Reason(WorkReasonCode reason)
        { switch (reason) { case WorkReasonCode.None: return "진행 가능"; case WorkReasonCode.MissingMaterials: return "목재 부족";
            case WorkReasonCode.Unreachable: return "접근 불가"; case WorkReasonCode.TargetReserved: return "다른 작업자 점유";
            case WorkReasonCode.AwaitingClearance: return "완공 위치 비우기 대기"; case WorkReasonCode.NoWorker: return "담당자 대기";
            default: return "작업 대기"; } }
        private static string Error(string error)
        { switch (error) { case "InvalidBlueprint": return "설계도를 지정하거나 취소할 수 없습니다";
            case "InvalidBuilding": return "유효한 시설이 아닙니다"; case "OccupiedServiceCell": return "조리대 남쪽 사용 칸을 비워 주세요";
            default: return "지정할 수 없습니다. 위치와 겹침을 확인하세요"; } }
        private Button Button(string text, float x, float y, float width, Action action)
        {
            var rect = Rect(text, Root, x, y, width, 34); var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.19f,.29f,.31f); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Label("Label", rect, 0, 0, width, 34, 15); label.text = text; label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() => action()); return button;
        }
        private Text Label(string name, Transform parent, float x, float y, float width, float height, int size)
        {
            var rect = Rect(name, parent, x, y, width, height); var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.color = new Color(.88f,.94f,.95f); text.raycastTarget = false;
            text.alignment = TextAnchor.UpperLeft; return text;
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x,y);
            rect.sizeDelta = new Vector2(width,height); return rect;
        }
    }
}
