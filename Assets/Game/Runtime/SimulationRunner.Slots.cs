using System;
using System.IO;
using RimworldTweak.Simulation;
using UnityEngine;

namespace RimworldTweak.Runtime
{
    public sealed partial class SimulationRunner
    {
        public SaveRepository Slots { get; private set; }
        public UserSettingsStore SettingsStore { get; private set; }
        public UserSettings Settings { get; private set; }
        public string ActiveSlotId { get; private set; }
        public string SettingsWarning { get; private set; } = string.Empty;

        public void ConfigureStorage(string root = null)
        {
            root = root ?? Application.persistentDataPath;
            Slots = new SaveRepository(Path.Combine(root, "Saves"));
            SettingsStore = new UserSettingsStore(Path.Combine(root, "Settings", "user-settings.json"));
            var loaded = SettingsStore.Load();
            Settings = loaded.Settings ?? new UserSettings();
            SettingsWarning = loaded.Success ? string.Empty : "표시 설정을 읽지 못해 기본값을 사용해.";
            ActiveSlotId = null;
        }

        public void StartNewSettlement()
        {
            if (SaveBusy) return;
            Initialize(WorkScenario.CreateTestMap());
            ActiveSlotId = null;
            SaveMessage = string.Empty;
        }

        public bool SaveSlot(string displayName, string overwriteId = null)
        {
            if (SaveBusy || IsLoadConfirmationOpen) return false;
            if (Slots == null) ConfigureStorage();
            SaveBusy = true;
            SettingsWarning = string.Empty;
            try
            {
                var capture = Session.CaptureSaveData(IsPaused, SpeedMultiplier);
                if (!capture.Success) { SaveMessage = SlotError(capture.ErrorCode); return false; }
                var result = string.IsNullOrEmpty(overwriteId)
                    ? Slots.SaveNew(displayName, capture.Data)
                    : Slots.Overwrite(overwriteId, displayName, capture.Data);
                if (!result.Success) { SaveMessage = SlotError(result.ErrorCode); return false; }
                ActiveSlotId = result.Slot.SlotId;
                SaveMessage = "정착지를 저장했어: " + result.Slot.DisplayName;
                RememberSlot(ActiveSlotId, result.Slot.DisplayName);
                return true;
            }
            finally { SaveBusy = false; skipNextAutomaticFrame = true; }
        }

        public bool LoadSlot(string slotId)
        {
            if (SaveBusy) return false;
            if (Slots == null) ConfigureStorage();
            SaveBusy = true;
            SettingsWarning = string.Empty;
            try
            {
                var result = Slots.Load(slotId);
                if (!result.Success) { SaveMessage = SlotError(result.ErrorCode); return false; }
                if (!TryApplySave(result.Data)) return false;
                ActiveSlotId = result.Slot.IsLegacy ? null : result.Slot.SlotId;
                RememberSlot(slotId, result.Slot.DisplayName);
                return true;
            }
            finally { SaveBusy = false; skipNextAutomaticFrame = true; }
        }

        private void RememberSlot(string id, string name)
        {
            var previous = Settings.RecentSlotId;
            Settings.RecentSlotId = id;
            var saved = SettingsStore.Save(Settings);
            if (!saved.Success)
            {
                Settings.RecentSlotId = previous;
                SettingsWarning = "게임은 '" + name + "'에 저장되어 있지만 이어하기 기록을 갱신하지 못했어. 다음 실행에는 목록에서 선택해줘.";
            }
        }

        public bool SetUiScale(int percent)
        {
            if (percent != 100 && percent != 125 && percent != 150) return false;
            if (Settings == null) ConfigureStorage();
            Settings.UiScalePercent = percent;
            var saved = SettingsStore.Save(Settings);
            SettingsWarning = saved.Success ? string.Empty : "이번 화면에는 적용했지만 표시 설정을 파일에 저장하지 못했어.";
            return saved.Success;
        }

        public static string SlotError(string code)
        {
            if (code == "SavePendingCommands") return "아직 적용되지 않은 명령이 있어. 메뉴를 닫고 시간을 재개한 뒤 다시 저장해줘.";
            if (code == "SaveInvalidName") return "이름은 한글·영문·숫자·일반 기호 1~40자로 입력해줘.";
            return SaveError(code);
        }
    }
}
