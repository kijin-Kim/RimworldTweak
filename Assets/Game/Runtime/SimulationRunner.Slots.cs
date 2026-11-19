using RimworldTweak.Simulation;

namespace RimworldTweak.Runtime
{
    public sealed partial class SimulationRunner
    {
        private SettlementStorage storage;
        public SaveRepository Slots => storage?.Slots;
        public UserSettingsStore SettingsStore => storage?.SettingsStore;
        public UserSettings Settings => storage?.Settings;
        public string ActiveSlotId { get; private set; }
        public string SettingsWarning => storage?.SettingsWarning ?? string.Empty;

        public void ConfigureStorage(string root = null)
        {
            storage = SettlementStorage.Open(root);
            ActiveSlotId = null;
        }

        public void StartNewSettlement()
        {
            if (SaveBusy) return;
            Initialize(WorkScenario.CreateSettlement());
            ActiveSlotId = null;
            SaveMessage = string.Empty;
        }

        public bool SaveSlot(string displayName, string overwriteId = null)
        {
            if (SaveBusy || IsLoadConfirmationOpen) return false;
            if (Slots == null) ConfigureStorage();
            SaveBusy = true;
            storage.SettingsWarning = string.Empty;
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
            storage.SettingsWarning = string.Empty;
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
                storage.SettingsWarning = "게임은 '" + name + "'에 저장되어 있지만 이어하기 기록을 갱신하지 못했어. 다음 실행에는 목록에서 선택해줘.";
            }
        }

        public bool SetUiScale(int percent)
        {
            if (storage == null) ConfigureStorage();
            return storage.SetUiScale(percent);
        }

        public static string SlotError(string code)
        {
            if (code == "SavePendingCommands") return "아직 적용되지 않은 명령이 있어. 메뉴를 닫고 시간을 재개한 뒤 다시 저장해줘.";
            if (code == "SaveInvalidName") return "이름은 한글·영문·숫자·일반 기호 1~40자로 입력해줘.";
            return SaveError(code);
        }
    }
}
