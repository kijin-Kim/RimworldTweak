using System;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public sealed class FarmingPanel
    {
        public RectTransform Root { get; private set; }
        public Button ZoneButton { get; private set; }
        public Button ToggleSowingButton { get; private set; }
        public Button DeleteZoneButton { get; private set; }
        public Text InfoText { get; private set; }

        public void Build(Transform parent, Font font, Action beginZone, Action toggleSowing, Action deleteZone)
        {
            if (Root != null) throw new InvalidOperationException("Farming panel already built.");
            Root = Rect("Farming panel", parent, 0, 0, 708, 100);
            ZoneButton = ButtonAt("재배 구역", Root, font, 0, beginZone);
            ToggleSowingButton = ButtonAt("파종 끄기", Root, font, 164, toggleSowing);
            DeleteZoneButton = ButtonAt("재배 구역 삭제", Root, font, 328, deleteZone);
            InfoText = TextAt("Farming detail", Root, font, 0, 0, 708, 54, 14);
            Refresh(null, null, string.Empty);
        }

        public void Refresh(GrowingZoneState selected, CropState cell, string error)
        {
            var valid = selected != null && selected.Exists;
            ToggleSowingButton.interactable = DeleteZoneButton.interactable = valid;
            ToggleSowingButton.GetComponentInChildren<Text>().text = valid && !selected.SowingEnabled ? "파종 켜기" : "파종 끄기";
            var detail = valid ? "재배 구역 #" + selected.Id + " / " + selected.Cells.Count + "칸 / 파종 " + (selected.SowingEnabled ? "허용" : "중지") : "재배 구역: 두 모서리를 지정하세요";
            if (cell != null && cell.Exists)
            {
                var stage = cell.Stage == CropStage.Empty ? "파종 " + cell.SowProgress + "/20" :
                    cell.Stage == CropStage.Growing ? "성장 " + cell.GrowthTicks + "/2400" : "수확 " + cell.HarvestProgress + "/30";
                detail += " / " + stage + " / 담당 " + (cell.OwnerId.HasValue ? cell.OwnerId.Value.ToString() : "없음");
                if (cell.Reason != WorkReasonCode.None) detail += " / " + ReasonText(cell.Reason);
            }
            InfoText.text = detail + "\n삭제: 미수확 작물 제거 · 수확물 없음" + (string.IsNullOrEmpty(error) ? "" : " / " + ErrorText(error));
        }

        private static string ReasonText(WorkReasonCode reason)
        {
            switch (reason)
            {
                case WorkReasonCode.Unreachable: return "접근 불가";
                case WorkReasonCode.TargetReserved: return "다른 정착민 작업 중";
                case WorkReasonCode.Disabled: return "파종 설정 중지";
                case WorkReasonCode.Drafted: return "소집 중";
                case WorkReasonCode.NeedsPriority: return "생활 행동 우선";
                case WorkReasonCode.NoWorker: return "담당자 없음";
                case WorkReasonCode.InvalidTarget: return "대상 사용 불가";
                default: return "작업 대기";
            }
        }

        private static string ErrorText(string error)
        {
            switch (error)
            {
                case "InvalidGrowingArea": return "재배할 수 없는 위치입니다";
                case "InvalidGrowingZone": return "재배 구역이 없습니다";
                default: return "지정할 수 없습니다";
            }
        }
        private static Button ButtonAt(string label, Transform parent, Font font, float x, Action action)
        {
            var rect = Rect(label, parent, x, 58, 156, 36);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.19f, .29f, .31f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var text = TextAt("Label", rect, font, 0, 0, 156, 36, 16); text.text = label; text.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() => action?.Invoke()); return button;
        }
        private static Text TextAt(string name, Transform parent, Font font, float x, float y, float width, float height, int size)
        {
            var text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.color = new Color(.88f, .94f, .94f);
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; return text;
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
    }
}
