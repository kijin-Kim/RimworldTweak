using System.IO;
using UnityEngine;

namespace RimworldTweak.Runtime
{
    public sealed class SettlementStorage
    {
        public SaveRepository Slots { get; }
        public UserSettingsStore SettingsStore { get; }
        public UserSettings Settings { get; }
        public string SettingsWarning { get; internal set; }

        private SettlementStorage(string root)
        {
            Slots = new SaveRepository(Path.Combine(root, "Saves"));
            SettingsStore = new UserSettingsStore(Path.Combine(root, "Settings", "user-settings.json"));
            var loaded = SettingsStore.Load();
            Settings = loaded.Settings ?? new UserSettings();
            SettingsWarning = loaded.Success ? string.Empty : "표시 설정을 읽지 못해 기본값을 사용해.";
        }

        public static SettlementStorage Open(string root = null)
        {
            return new SettlementStorage(root ?? Application.persistentDataPath);
        }

        public bool SetUiScale(int percent)
        {
            if (percent != 100 && percent != 125 && percent != 150) return false;
            Settings.UiScalePercent = percent;
            var saved = SettingsStore.Save(Settings);
            SettingsWarning = saved.Success ? string.Empty : "이번 화면에는 적용했지만 표시 설정을 파일에 저장하지 못했어.";
            return saved.Success;
        }
    }
}
