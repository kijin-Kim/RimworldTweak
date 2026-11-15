using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Xml;
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
        private static int busy;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false,true);
        private sealed class SizeLimitException : Exception { }
        private sealed class LimitedStream : MemoryStream
        {
            public override void Write(byte[] buffer,int offset,int count)
            { if(Position+count>SaveValidation.MaximumFileBytes)throw new SizeLimitException();base.Write(buffer,offset,count); }
            public override void WriteByte(byte value)
            { if(Position>=SaveValidation.MaximumFileBytes)throw new SizeLimitException();base.WriteByte(value); }
        }
        private static DataContractJsonSerializer Serializer() => new DataContractJsonSerializer(typeof(SaveData),new DataContractJsonSerializerSettings {
            EmitTypeInformation=EmitTypeInformation.Never,MaxItemsInObjectGraph=1000000,UseSimpleDictionaryFormat=true });
        private static byte[] Encode(SaveData data)
        { using(var stream=new LimitedStream()){Serializer().WriteObject(stream,data);return stream.ToArray();} }
        private static SaveData Decode(byte[] bytes)
        {
            if(bytes.Length==0||bytes.Length>SaveValidation.MaximumFileBytes)throw new SizeLimitException();
            var json=Utf8.GetString(bytes); // Reject damaged UTF-8 and extra roots before the BCL reader can ignore them.
            RequireSingleObject(json);
            var quotas=new XmlDictionaryReaderQuotas {MaxDepth=64,MaxStringContentLength=SaveValidation.MaximumFileBytes,MaxArrayLength=1000000,MaxBytesPerRead=4096,MaxNameTableCharCount=16384};
            using(var reader=JsonReaderWriterFactory.CreateJsonReader(bytes,0,bytes.Length,Utf8,quotas,null))
            {
                var data=(SaveData)Serializer().ReadObject(reader);
                if(reader.Read())throw new SerializationException("Trailing JSON content.");
                return data;
            }
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
                var bytes=Encode(data);var full=Path.GetFullPath(path);var directory=Path.GetDirectoryName(full);
                Directory.CreateDirectory(directory);temporary=full+".tmp";
                using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
                {created=true;stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                var roundTrip=Decode(Read(temporary));valid=ValidateForWrite(roundTrip);
                if(!valid.IsValid)return new SaveFileResult(false,valid.ErrorCode);
                if(File.Exists(full))File.Replace(temporary,full,null);else File.Move(temporary,full);
                created=false;return new SaveFileResult(true);
            }
            catch(SizeLimitException){return new SaveFileResult(false,"SaveInvalidData");}
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
            catch(SerializationException){return new SaveLoadResult(false,errorCode:"SaveInvalidData");}
            catch(XmlException){return new SaveLoadResult(false,errorCode:"SaveInvalidData");}
            catch(DecoderFallbackException){return new SaveLoadResult(false,errorCode:"SaveInvalidData");}
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is NotSupportedException)
            {return new SaveLoadResult(false,errorCode:"SaveIoError");}
            finally{Volatile.Write(ref busy,0);}
        }
    }
}
