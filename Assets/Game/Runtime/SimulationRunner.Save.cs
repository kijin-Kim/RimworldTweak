using System;
using System.IO;
using RimworldTweak.Simulation;
using UnityEngine;
namespace RimworldTweak.Runtime
{
    public sealed partial class SimulationRunner
    {
        private readonly SaveFileService saveFiles=new SaveFileService();
        private string savePath;
        private bool skipNextAutomaticFrame;
        public string SavePath {get=>savePath??Path.Combine(Application.persistentDataPath,"Saves","settlement-v1.json");set=>savePath=value;}
        public string SaveMessage {get;private set;}=string.Empty;
        public bool SaveBusy {get;private set;}
        public bool IsLoadConfirmationOpen {get;private set;}
        public bool TryApplySave(SaveData data)
        {
            if(!SimulationSession.TryRestoreSave(data,out var candidate,out var error)){SaveMessage=SaveError(error);return false;}
            Session=candidate;IsPaused=data.Session.Paused;SpeedMultiplier=data.Session.Speed;
            SaveMessage="저장한 정착지를 불러왔습니다.";return true;
        }
        public bool SaveNow()
        {
            if(SaveBusy||IsLoadConfirmationOpen){SaveMessage=SaveError("SaveBusy");return false;}
            SaveBusy=true;
            try
            {
                var capture=Session.CaptureSaveData(IsPaused,SpeedMultiplier);
                if(!capture.Success){SaveMessage=SaveError(capture.ErrorCode);return false;}
                var result=saveFiles.Save(SavePath,capture.Data);
                SaveMessage=result.Success?"현재 정착지를 저장했습니다.":SaveError(result.ErrorCode);
                return result.Success;
            }
            finally{SaveBusy=false;skipNextAutomaticFrame=true;}
        }
        public bool BeginLoad()
        {
            if(SaveBusy||IsLoadConfirmationOpen){SaveMessage=SaveError("SaveBusy");return false;}
            IsLoadConfirmationOpen=true;return true;
        }
        public void CancelLoad(){if(SaveBusy)return;IsLoadConfirmationOpen=false;skipNextAutomaticFrame=true;SaveMessage="불러오기를 취소했습니다.";}
        public bool ConfirmLoad()
        {
            if(SaveBusy||!IsLoadConfirmationOpen){SaveMessage=SaveError("SaveBusy");return false;}
            SaveBusy=true;
            try
            {
                var result=saveFiles.Load(SavePath);
                if(!result.Success){SaveMessage=SaveError(result.ErrorCode);return false;}
                return TryApplySave(result.Data);
            }
            finally{SaveBusy=false;IsLoadConfirmationOpen=false;skipNextAutomaticFrame=true;}
        }
        private static string SaveError(string error)
        {
            switch(error)
            {
                case "SaveBusy":return "저장 또는 불러오기를 처리 중입니다.";
                case "SavePendingCommands":return "아직 적용되지 않은 명령이 있습니다. 한 Tick 진행한 뒤 저장하세요.";
                case "SaveNotFound":return "저장 파일이 없습니다. 먼저 저장하세요.";
                case "SaveUnsupportedVersion":return "지원하지 않는 저장 파일 버전입니다.";
                case "SaveIoError":return "파일을 읽거나 쓰지 못했습니다. 저장 경로와 여유 공간을 확인하세요.";
                default:return "저장 데이터가 올바르지 않아 불러오거나 저장할 수 없습니다.";
            }
        }
    }
}
