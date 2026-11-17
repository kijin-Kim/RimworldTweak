using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using RimworldTweak.Simulation;

namespace RimworldTweak.Runtime
{
    [DataContract]
    public sealed class SaveEnvelope
    {
        [DataMember(Name="formatVersion",IsRequired=true)] public int FormatVersion=1;
        [DataMember(Name="slotId",IsRequired=true)] public string SlotId;
        [DataMember(Name="displayName",IsRequired=true)] public string DisplayName;
        [DataMember(Name="savedAtUtc",IsRequired=true)] public string SavedAtUtc;
        [DataMember(Name="tick",IsRequired=true)] public long Tick;
        [DataMember(Name="colonistCount",IsRequired=true)] public int ColonistCount;
        [DataMember(Name="payload",IsRequired=true)] public SaveData Payload;
    }

    public sealed class SaveSlotInfo
    {
        public string SlotId {get; internal set;}
        public string DisplayName {get; internal set;}
        public DateTime SavedAtUtc {get; internal set;}
        public long Tick {get; internal set;}
        public int ColonistCount {get; internal set;}
        public bool IsLegacy {get; internal set;}
        public bool IsValid => string.IsNullOrEmpty(ErrorCode);
        public string ErrorCode {get; internal set;}=string.Empty;
        internal string DeclaredSlotId;
    }
    public sealed class SaveSlotListResult
    {
        public bool Success {get;}
        public string ErrorCode {get;}
        public IReadOnlyList<SaveSlotInfo> Slots {get;}
        internal SaveSlotListResult(bool success,string error,IReadOnlyList<SaveSlotInfo> slots)
        {Success=success;ErrorCode=error;Slots=slots;}
    }
    public sealed class SaveSlotWriteResult
    {
        public bool Success {get;}
        public string ErrorCode {get;}
        public SaveSlotInfo Slot {get;}
        internal SaveSlotWriteResult(bool success,string error,SaveSlotInfo slot=null)
        {Success=success;ErrorCode=error;Slot=slot;}
    }
    public sealed class SaveSlotLoadResult
    {
        public bool Success {get;}
        public string ErrorCode {get;}
        public SaveData Data {get;}
        public SaveSlotInfo Slot {get;}
        internal SaveSlotLoadResult(bool success,string error,SaveData data=null,SaveSlotInfo slot=null)
        {Success=success;ErrorCode=error;Data=data;Slot=slot;}
    }

    public sealed class SaveRepository
    {
        public const string LegacySlotId="legacy";
        private readonly SaveFileService files;
        public string SaveDirectory {get;}
        public SaveRepository(string saveDirectory,SaveFileService files=null)
        {SaveDirectory=Path.GetFullPath(saveDirectory??throw new ArgumentNullException(nameof(saveDirectory)));this.files=files??new SaveFileService();}

        public static bool IsSlotId(string id) => id!=null&&id.Length==32&&id.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f'));
        public static bool TryNormalizeName(string input,out string name,out string error)
        {
            name=(input??string.Empty).Trim();error="SaveInvalidName";
            if(name.Length==0||new StringInfo(name).LengthInTextElements>40)return false;
            foreach(var c in name)
                if(!((c>='\uAC00'&&c<='\uD7A3')||(c>='\u3131'&&c<='\u3163')||(c>='\u0020'&&c<='\u007E')))return false;
            error=string.Empty;return true;
        }
        private string SlotPath(string id) => Path.Combine(SaveDirectory,id==LegacySlotId?"settlement-v1.json":id+".save.json");
        private static string Validate(SaveEnvelope value,string expectedId)
        {
            if(value==null)return "SaveInvalidData";
            if(value.FormatVersion!=1)return "SaveUnsupportedVersion";
            if(!IsSlotId(value.SlotId)||value.SlotId!=expectedId)return "SaveInvalidSlot";
            if(!TryNormalizeName(value.DisplayName,out var name,out _)||name!=value.DisplayName)return "SaveInvalidName";
            if(!DateTime.TryParseExact(value.SavedAtUtc,"O",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var date)||date.Kind!=DateTimeKind.Utc)return "SaveInvalidData";
            var valid=SaveValidation.Validate(value.Payload);if(!valid.IsValid)return valid.ErrorCode;
            if(value.Tick!=value.Payload.World.Tick||value.ColonistCount!=value.Payload.World.Colonists.Count)return "SaveInvalidSummary";
            return SimulationSession.TryRestoreSave(value.Payload,out _,out var error)?string.Empty:error;
        }
        private static SaveSlotInfo Info(SaveEnvelope value) => new SaveSlotInfo {
            SlotId=value.SlotId,DeclaredSlotId=value.SlotId,DisplayName=value.DisplayName,
            SavedAtUtc=DateTime.ParseExact(value.SavedAtUtc,"O",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),Tick=value.Tick,ColonistCount=value.ColonistCount };

        public SaveSlotListResult ListSlots()
        {
            var entries=new List<SaveSlotInfo>();
            try
            {
                if(!Directory.Exists(SaveDirectory))return new SaveSlotListResult(true,string.Empty,entries.AsReadOnly());
                foreach(var path in Directory.GetFiles(SaveDirectory,"*.save.json",SearchOption.TopDirectoryOnly).OrderBy(x=>x,StringComparer.Ordinal))
                {
                    var fileName=Path.GetFileName(path);var id=fileName.Substring(0,fileName.Length-".save.json".Length);
                    var bad=new SaveSlotInfo {SlotId=id,DisplayName=fileName,ErrorCode="SaveInvalidSlot"};
                    if(!files.ReadJson<SaveEnvelope>(path,out var data,out var error)){bad.ErrorCode=error;entries.Add(bad);continue;}
                    bad.DeclaredSlotId=data?.SlotId;
                    error=Validate(data,id);
                    if(!string.IsNullOrEmpty(error)){bad.ErrorCode=error;entries.Add(bad);continue;}
                    entries.Add(Info(data));
                }
                // A copied file must not silently shadow the canonical slot.
                foreach(var group in entries.Where(x=>IsSlotId(x.DeclaredSlotId)).GroupBy(x=>x.DeclaredSlotId).Where(x=>x.Count()>1))
                    foreach(var item in group)item.ErrorCode="SaveDuplicateSlot";
                if(File.Exists(SlotPath(LegacySlotId)))
                {
                    var loaded=files.Load(SlotPath(LegacySlotId));var error=loaded.ErrorCode;
                    if(loaded.Success&&!SimulationSession.TryRestoreSave(loaded.Data,out _,out error))loaded=new SaveLoadResult(false,errorCode:error);
                    entries.Add(new SaveSlotInfo {SlotId=LegacySlotId,DisplayName="기존 저장",IsLegacy=true,ErrorCode=loaded.Success?string.Empty:error,
                        SavedAtUtc=File.GetLastWriteTimeUtc(SlotPath(LegacySlotId)),Tick=loaded.Success?loaded.Data.World.Tick:0,
                        ColonistCount=loaded.Success?loaded.Data.World.Colonists.Count:0});
                }
                return new SaveSlotListResult(true,string.Empty,entries.OrderByDescending(x=>x.SavedAtUtc).ThenBy(x=>x.SlotId,StringComparer.Ordinal).ToList().AsReadOnly());
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException)
            {return new SaveSlotListResult(false,"SaveIoError",entries.AsReadOnly());}
        }

        public SaveSlotWriteResult SaveNew(string displayName,SaveData data)
        {
            string id;
            do{id=Guid.NewGuid().ToString("N");}while(File.Exists(SlotPath(id)));
            return Write(id,displayName,data);
        }
        public SaveSlotWriteResult Overwrite(string slotId,string displayName,SaveData data)
        {
            if(!IsSlotId(slotId))return new SaveSlotWriteResult(false,"SaveInvalidSlot");
            var list=ListSlots();if(!list.Success)return new SaveSlotWriteResult(false,list.ErrorCode);
            var slot=list.Slots.FirstOrDefault(x=>x.SlotId==slotId);
            if(slot==null)return new SaveSlotWriteResult(false,"SaveNotFound");
            if(!slot.IsValid)return new SaveSlotWriteResult(false,slot.ErrorCode);
            return Write(slotId,displayName,data);
        }
        private SaveSlotWriteResult Write(string id,string displayName,SaveData data)
        {
            if(!TryNormalizeName(displayName,out var name,out var error))return new SaveSlotWriteResult(false,error);
            var value=new SaveEnvelope {SlotId=id,DisplayName=name,SavedAtUtc=DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture),
                Tick=data?.World?.Tick??0,ColonistCount=data?.World?.Colonists?.Count??0,Payload=data};
            var result=files.WriteJson(SlotPath(id),value,v=>Validate(v,id));
            return new SaveSlotWriteResult(result.Success,result.ErrorCode,result.Success?Info(value):null);
        }
        public SaveSlotLoadResult Load(string slotId)
        {
            if(slotId!=LegacySlotId&&!IsSlotId(slotId))return new SaveSlotLoadResult(false,"SaveInvalidSlot");
            var list=ListSlots();if(!list.Success)return new SaveSlotLoadResult(false,list.ErrorCode);
            var slot=list.Slots.FirstOrDefault(x=>x.SlotId==slotId);
            if(slot==null)return new SaveSlotLoadResult(false,"SaveNotFound");
            if(!slot.IsValid)return new SaveSlotLoadResult(false,slot.ErrorCode);
            if(slotId==LegacySlotId)
            {
                var old=files.Load(SlotPath(slotId));
                if(!old.Success)return new SaveSlotLoadResult(false,old.ErrorCode);
                if(!SimulationSession.TryRestoreSave(old.Data,out _,out var restoreError))return new SaveSlotLoadResult(false,restoreError);
                return new SaveSlotLoadResult(true,string.Empty,old.Data,slot);
            }
            if(!files.ReadJson<SaveEnvelope>(SlotPath(slotId),out var data,out var error))return new SaveSlotLoadResult(false,error);
            error=Validate(data,slotId);
            return string.IsNullOrEmpty(error)?new SaveSlotLoadResult(true,string.Empty,data.Payload,Info(data)):new SaveSlotLoadResult(false,error);
        }
    }
}
