using System;
using System.IO;
using System.Runtime.Serialization;

namespace RimworldTweak.Runtime
{
    [DataContract]
    public sealed class UserSettings
    {
        [DataMember(Name="version",IsRequired=true)] public int Version=1;
        [DataMember(Name="uiScalePercent",IsRequired=true)] public int UiScalePercent=100;
        [DataMember(Name="recentSlotId",IsRequired=true)] public string RecentSlotId=string.Empty;
    }
    public sealed class UserSettingsLoadResult
    {
        public bool Success {get;}
        public string ErrorCode {get;}
        public UserSettings Settings {get;}
        internal UserSettingsLoadResult(bool success,string error,UserSettings settings)
        {Success=success;ErrorCode=error;Settings=settings;}
    }
    public sealed class UserSettingsStore
    {
        private readonly string path;
        private readonly SaveFileService files;
        private const int MaximumSettingsBytes=65536;
        public UserSettingsStore(string path,SaveFileService files=null)
        {this.path=Path.GetFullPath(path??throw new ArgumentNullException(nameof(path)));this.files=files??new SaveFileService();}
        private static string Validate(UserSettings value)
        {
            if(value==null||value.Version!=1)return "SettingsInvalid";
            if(value.UiScalePercent!=100&&value.UiScalePercent!=125&&value.UiScalePercent!=150)return "SettingsInvalid";
            return value.RecentSlotId!=null&&(value.RecentSlotId.Length==0||value.RecentSlotId==SaveRepository.LegacySlotId||SaveRepository.IsSlotId(value.RecentSlotId))?string.Empty:"SettingsInvalid";
        }
        public UserSettingsLoadResult Load()
        {
            if(!files.ReadJson<UserSettings>(path,out var value,out var error,MaximumSettingsBytes))
                return new UserSettingsLoadResult(error=="SaveNotFound",error=="SaveNotFound"?string.Empty:error,new UserSettings());
            error=Validate(value);
            return string.IsNullOrEmpty(error)?new UserSettingsLoadResult(true,string.Empty,value):new UserSettingsLoadResult(false,error,new UserSettings());
        }
        public SaveFileResult Save(UserSettings value) => files.WriteJson(path,value,Validate,MaximumSettingsBytes);
    }
}
