using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using RimworldTweak.Simulation;
namespace RimworldTweak.Runtime
{
    public sealed class SaveFileResult
    {
        public SaveFileResult(bool success,string errorCode="") { Success=success;ErrorCode=errorCode; }
        public bool Success { get; }
        public string ErrorCode { get; }
    }
    public sealed class SaveLoadResult
    {
        public SaveLoadResult(bool success,SaveData data=null,string errorCode="") { Success=success;Data=data;ErrorCode=errorCode; }
        public bool Success { get; }
        public SaveData Data { get; }
        public string ErrorCode { get; }
    }
    public sealed class SaveFileService
    {
        // Narrow fault-injection seam for atomic file tests. Production leaves this unset.
        public Action<string> BeforeAtomicStage { get; set; }
        // Diagnostic only: no path or file contents; never used to decide persistence success.
        public string LastFailureDiagnostic { get; private set; } = string.Empty;
        private static int busy;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false,true);
        private sealed class SizeLimitException : Exception { }
        private sealed class UnsupportedSaveVersionException : Exception { }
        private sealed class LimitedStream : MemoryStream
        {
            public override void Write(byte[] buffer,int offset,int count)
            { if(Position+count>SaveValidation.MaximumFileBytes)throw new SizeLimitException();base.Write(buffer,offset,count); }
            public override void WriteByte(byte value)
            { if(Position>=SaveValidation.MaximumFileBytes)throw new SizeLimitException();base.WriteByte(value); }
        }
        private static DataContractJsonSerializer Serializer() => new DataContractJsonSerializer(typeof(SaveData),new DataContractJsonSerializerSettings {
            EmitTypeInformation=EmitTypeInformation.Never,MaxItemsInObjectGraph=1000000,UseSimpleDictionaryFormat=true });
        private static SaveData Decode(byte[] bytes)
        {
            if(bytes.Length==0||bytes.Length>SaveValidation.MaximumFileBytes)throw new SizeLimitException();
            var json=Utf8.GetString(bytes); // Reject damaged UTF-8 and extra roots before the BCL reader can ignore them.
            RequireSingleObject(json);
            var quotas=new XmlDictionaryReaderQuotas {MaxDepth=64,MaxStringContentLength=SaveValidation.MaximumFileBytes,MaxArrayLength=1000000,MaxBytesPerRead=4096,MaxNameTableCharCount=16384};
            using(var reader=JsonReaderWriterFactory.CreateJsonReader(bytes,0,bytes.Length,Utf8,quotas,null))
            {
                try
                {
                    var data=(SaveData)Serializer().ReadObject(reader);
                    if(reader.Read())throw new SerializationException("Trailing JSON content.");
                    return data;
                }
                catch (SerializationException)
                {
                    var upgraded=UpgradeSettlement<SaveData>(bytes,quotas);
                    if(upgraded!=null)return upgraded;
                    // Older saves can lack required fields of the current schema.
                    using (var header = JsonReaderWriterFactory.CreateJsonReader(bytes,0,bytes.Length,Utf8,quotas,null))
                    {
                        header.MoveToContent();
                        header.ReadStartElement();
                        while (header.MoveToContent() == XmlNodeType.Element)
                        {
                            if (header.LocalName == "schemaVersion" && header.GetAttribute("type") == "number")
                            {
                                if (header.ReadElementContentAsInt() != SaveData.CurrentSchemaVersion)
                                    throw new UnsupportedSaveVersionException();
                                break;
                            }
                            header.Skip();
                        }
                    }
                    throw;
                }
            }
        }
        // Upgrade the complete pre-combat contract in memory. Slot metadata and the original file stay intact.
        private static T UpgradeSettlement<T>(byte[] bytes, XmlDictionaryReaderQuotas quotas)
        {
            XElement root;
            using (var reader=JsonReaderWriterFactory.CreateJsonReader(bytes,0,bytes.Length,Utf8,quotas,null))
                root=XElement.Load(reader);
            var save=typeof(T)==typeof(SaveEnvelope)?root.Element("payload"):root;
            if ((string)save?.Element("schemaVersion")!="1" || (string)save.Element("rulesVersion")!="1" ||
                (string)save.Element("contractVersion")!="settlement-five-v2") return default;
            XElement Required(XElement parent,string name) => parent?.Element(name) ?? throw new SerializationException("Missing "+name);
            void Add<V>(XElement parent,string name,V value)
            {
                if(parent.Element(name)!=null)throw new SerializationException("Duplicate "+name);
                var encoded=EncodeJson(value);
                using(var reader=JsonReaderWriterFactory.CreateJsonReader(encoded,quotas))
                {
                    var field=XElement.Load(reader);field.Name=name;parent.Add(field);
                }
            }
            var world=Required(save,"world");var scenario=Required(save,"scenario");
            Add<PositionSaveData>(scenario,"InitialRaidStove",null);
            Add(world,"Raid",new RaidSaveData {Phase="Idle",Outcome="None",Injured=new List<int>(),Downed=new List<int>(),Raiders=new List<RaiderSaveData>()});
            Add(world,"MedicineConsumed",0);Add(world,"RepairWoodConsumed",0);
            foreach(var pawn in Required(world,"Colonists").Elements())
            {
                Add(pawn,"Health",100);Add(pawn,"Attack",new AttackSaveData {BlockedReason=""});
                Add(pawn,"CareProgress",0);Add(pawn,"PatientClaim",new ClaimSaveData());
            }
            foreach(var policy in Required(world,"WorkPolicies").Elements())
                AddArrayItem(Required(policy,"Priorities"),new WorkPrioritySaveData {Category="Care",Priority=1},quotas);
            var buildings=Required(world,"Buildings");
            foreach(var building in buildings.Elements())
            {
                Add(building,"InitialPlacement",false);Add(building,"Durability",100);Add(building,"RepairDesignated",false);
                Add(building,"RepairWood",0);Add(building,"RepairProgress",0);
            }
            foreach(var bed in Required(scenario,"Beds").Elements())
            {
                var id=(int)Required(bed,"Id");var position=Required(bed,"Position");
                var live=Required(world,"Beds").Elements().FirstOrDefault(b=>(string)b.Element("Id")==id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                AddArrayItem(buildings,new BuildingSaveData {Id=id,Kind="Bed",Position=new PositionSaveData((int)Required(position,"X"),(int)Required(position,"Y")),
                    Exists=(bool)Required(live,"Exists"),Durability=100,InitialPlacement=true,Reason="None",Claim=new ClaimSaveData()},quotas);
            }
            save.Element("schemaVersion").Value="2";save.Element("rulesVersion").Value="2";save.Element("contractVersion").Value="settlement-raid-v1";
            using(var reader=root.CreateReader())return (T)new DataContractJsonSerializer(typeof(T),
                new DataContractJsonSerializerSettings {MaxItemsInObjectGraph=1000000,UseSimpleDictionaryFormat=true}).ReadObject(reader);
        }
        private static void AddArrayItem<V>(XElement array,V value,XmlDictionaryReaderQuotas quotas)
        {
            using(var reader=JsonReaderWriterFactory.CreateJsonReader(EncodeJson(value),quotas))
            {var item=XElement.Load(reader);item.Name="item";array.Add(item);}
        }
        private static bool JsonSpace(char c) => c==' '||c=='\t'||c=='\r'||c=='\n';
        private static void RequireSingleObject(string json)
        {
            var index=0;if(json.Length>0&&json[0]=='\uFEFF')index++;
            while(index<json.Length&&JsonSpace(json[index]))index++;
            if(index>=json.Length||json[index]!='{')throw new SerializationException("Expected one JSON object.");
            var brackets=new System.Collections.Generic.Stack<char>();var quoted=false;var escaped=false;
            for(;index<json.Length;index++)
            {
                var c=json[index];
                if(quoted)
                {
                    if(escaped){escaped=false;continue;}
                    if(c=='\\'){escaped=true;continue;}
                    if(c=='"')quoted=false;
                    continue;
                }
                if(c=='"'){quoted=true;continue;}
                if(c=='{'||c=='['){brackets.Push(c);if(brackets.Count>64)throw new SerializationException("JSON nesting limit.");continue;}
                if(c!='}'&&c!=']')continue;
                if(brackets.Count==0||brackets.Pop()!=(c=='}'?'{':'['))throw new SerializationException("Mismatched JSON container.");
                if(brackets.Count!=0)continue;
                for(index++;index<json.Length;index++)if(!JsonSpace(json[index]))throw new SerializationException("Trailing JSON content.");
                return;
            }
            throw new SerializationException("Incomplete JSON object.");
        }
        private static byte[] Read(string path)
        {
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<=0||stream.Length>SaveValidation.MaximumFileBytes)throw new SizeLimitException();
                var bytes=new byte[(int)stream.Length];var offset=0;
                while(offset<bytes.Length){var count=stream.Read(bytes,offset,bytes.Length-offset);if(count==0)throw new EndOfStreamException();offset+=count;}
                if(stream.ReadByte()!=-1)throw new SizeLimitException();return bytes;
            }
        }
        private static SaveValidationResult ValidateForWrite(SaveData data)
        {
            var basic=SaveValidation.Validate(data);if(!basic.IsValid)return basic;
            return SimulationSession.TryRestoreSave(data,out _,out var error)?basic:new SaveValidationResult(false,error,"World.Restore");
        }
        public SaveFileResult Save(string path,SaveData data)
        {
            if(Interlocked.CompareExchange(ref busy,1,0)!=0)return new SaveFileResult(false,"SaveBusy");
            string temporary=null;var created=false;
            try
            {
                var valid=ValidateForWrite(data);if(!valid.IsValid)return new SaveFileResult(false,valid.ErrorCode);
                var bytes=EncodeJson(data);var full=Path.GetFullPath(path);var directory=Path.GetDirectoryName(full);
                Directory.CreateDirectory(directory);temporary=full+".tmp";
                using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
                {created=true;stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                var roundTrip=Decode(Read(temporary));valid=ValidateForWrite(roundTrip);
                if(!valid.IsValid)return new SaveFileResult(false,valid.ErrorCode);
                if(File.Exists(full))ReplaceValidatedFile(temporary,full);else File.Move(temporary,full);
                created=false;return new SaveFileResult(true);
            }
            catch(SizeLimitException){return new SaveFileResult(false,"SaveInvalidData");}
            catch(UnsupportedSaveVersionException){return new SaveFileResult(false,"SaveUnsupportedVersion");}
            catch(SerializationException){return new SaveFileResult(false,"SaveInvalidData");}
            catch(XmlException){return new SaveFileResult(false,"SaveInvalidData");}
            catch(DecoderFallbackException){return new SaveFileResult(false,"SaveInvalidData");}
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is NotSupportedException)
            {return new SaveFileResult(false,"SaveIoError");}
            finally
            {
                if(created&&temporary!=null){try{File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
                Volatile.Write(ref busy,0);
            }
        }
        public SaveLoadResult Load(string path)
        {
            if(Interlocked.CompareExchange(ref busy,1,0)!=0)return new SaveLoadResult(false,errorCode:"SaveBusy");
            try
            {
                var full=Path.GetFullPath(path);if(!File.Exists(full))return new SaveLoadResult(false,errorCode:"SaveNotFound");
                var data=Decode(Read(full));var valid=SaveValidation.Validate(data);
                return valid.IsValid?new SaveLoadResult(true,data):new SaveLoadResult(false,errorCode:valid.ErrorCode);
            }
            catch(SizeLimitException){return new SaveLoadResult(false,errorCode:"SaveInvalidData");}
            catch(UnsupportedSaveVersionException){return new SaveLoadResult(false,errorCode:"SaveUnsupportedVersion");}
            catch(FormatException){return new SaveLoadResult(false,errorCode:"SaveInvalidData");}
            catch(OverflowException){return new SaveLoadResult(false,errorCode:"SaveInvalidData");}
            catch(SerializationException){return new SaveLoadResult(false,errorCode:"SaveInvalidData");}
            catch(XmlException){return new SaveLoadResult(false,errorCode:"SaveInvalidData");}
            catch(DecoderFallbackException){return new SaveLoadResult(false,errorCode:"SaveInvalidData");}
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is NotSupportedException)
            {return new SaveLoadResult(false,errorCode:"SaveIoError");}
            finally{Volatile.Write(ref busy,0);}
        }

        internal static byte[] EncodeJson<T>(T data)
        {
            var serializer=new DataContractJsonSerializer(typeof(T),new DataContractJsonSerializerSettings {
                EmitTypeInformation=EmitTypeInformation.Never,MaxItemsInObjectGraph=1000000,UseSimpleDictionaryFormat=true });
            using(var stream=new LimitedStream())
            {
                try { serializer.WriteObject(stream,data); }
                catch(System.Reflection.TargetInvocationException error) when(error.GetBaseException() is SizeLimitException)
                { throw new SizeLimitException(); }
                return stream.ToArray();
            }
        }
        internal static T DecodeJson<T>(byte[] bytes)
        {
            if(bytes.Length==0||bytes.Length>SaveValidation.MaximumFileBytes)throw new SizeLimitException();
            RequireSingleObject(Utf8.GetString(bytes));
            var quotas=new XmlDictionaryReaderQuotas {MaxDepth=64,MaxStringContentLength=SaveValidation.MaximumFileBytes,MaxArrayLength=1000000,MaxBytesPerRead=4096,MaxNameTableCharCount=16384};
            var serializer=new DataContractJsonSerializer(typeof(T),new DataContractJsonSerializerSettings {
                EmitTypeInformation=EmitTypeInformation.Never,MaxItemsInObjectGraph=1000000,UseSimpleDictionaryFormat=true });
            using(var reader=JsonReaderWriterFactory.CreateJsonReader(bytes,0,bytes.Length,Utf8,quotas,null))
            {
                try {var value=(T)serializer.ReadObject(reader);if(reader.Read())throw new SerializationException("Trailing JSON content.");return value;}
                catch(SerializationException) when(typeof(T)==typeof(SaveEnvelope))
                {var upgraded=UpgradeSettlement<T>(bytes,quotas);if((object)upgraded==null)throw;return upgraded;}
            }
        }
        private static bool ExpectedFileException(Exception e) => e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is NotSupportedException||
            e is SizeLimitException||e is SerializationException||e is XmlException||e is DecoderFallbackException||e is FormatException||e is OverflowException;
        private static string FileError(Exception e) => e is IOException||e is UnauthorizedAccessException ? "SaveIoError" : "SaveInvalidData";

        internal bool ReadJson<T>(string path,out T data,out string error,int maximumBytes=SaveValidation.MaximumFileBytes)
        {
            data=default;error=string.Empty;LastFailureDiagnostic=string.Empty;
            if(Interlocked.CompareExchange(ref busy,1,0)!=0){error="SaveBusy";return false;}
            try
            {
                if(!File.Exists(path)){error="SaveNotFound";return false;}
                if(new FileInfo(path).Length>maximumBytes){error="SaveInvalidData";return false;}
                data=DecodeJson<T>(Read(path));return true;
            }
            catch(Exception e) when(ExpectedFileException(e)){LastFailureDiagnostic="Read:"+e.GetType().Name+":"+e.HResult;error=FileError(e);return false;}
            finally{Volatile.Write(ref busy,0);}
        }

        private void ReplaceValidatedFile(string temporary,string destination)
        {
            // Win32 1175 leaves both names intact. Retry only this observed failure;
            // never delete the destination or retry partially completed rename errors.
            for(var attempt=0;;attempt++)
            {
                try
                {
                    BeforeAtomicStage?.Invoke("Replace");
                    File.Replace(temporary,destination,null);
                    return;
                }
                catch(IOException error) when(error.HResult==unchecked((int)0x80070497)&&attempt<2)
                { Thread.Sleep(10*(attempt+1)); }
            }
        }

        internal SaveFileResult WriteJson<T>(string path,T data,Func<T,string> validate,int maximumBytes=SaveValidation.MaximumFileBytes)
        {
            LastFailureDiagnostic=string.Empty;var stage="Validate";
            if(Interlocked.CompareExchange(ref busy,1,0)!=0)return new SaveFileResult(false,"SaveBusy");
            string temporary=null;var created=false;
            try
            {
                var error=validate(data);if(!string.IsNullOrEmpty(error))return new SaveFileResult(false,error);
                var bytes=EncodeJson(data);if(bytes.Length>maximumBytes)return new SaveFileResult(false,"SaveInvalidData");
                var full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));temporary=full+".tmp";
                stage="Write";BeforeAtomicStage?.Invoke(stage);
                using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
                {created=true;stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                stage="ReadBack";BeforeAtomicStage?.Invoke(stage);
                var copy=DecodeJson<T>(Read(temporary));error=validate(copy);
                if(!string.IsNullOrEmpty(error))return new SaveFileResult(false,error);
                stage="Replace";
                if(File.Exists(full))ReplaceValidatedFile(temporary,full);
                else { BeforeAtomicStage?.Invoke(stage);File.Move(temporary,full); }
                created=false;return new SaveFileResult(true);
            }
            catch(Exception e) when(ExpectedFileException(e)){LastFailureDiagnostic=stage+":"+e.GetType().Name+":"+e.HResult;return new SaveFileResult(false,FileError(e));}
            finally
            {
                if(created&&temporary!=null){try{File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
                Volatile.Write(ref busy,0);
            }
        }
    }
}

