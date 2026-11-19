using System;
using System.Linq;
using System.Globalization;
namespace RimworldTweak.Simulation
{
    public sealed partial class SimulationSession
    {
        public SaveCaptureResult CaptureSaveData(bool paused=false,int speed=1)
        {
            if(!World.CanCaptureSave)return new SaveCaptureResult(false,errorCode:"SavePendingCommands");
            if(speed!=1&&speed!=3)return new SaveCaptureResult(false,errorCode:"SaveInvalidData");
            try
            {
                return new SaveCaptureResult(true,new SaveData{
                    SchemaVersion=SaveData.CurrentSchemaVersion,ScenarioId="settlement-custom",ScenarioVersion=1,ContractVersion="settlement-raid-v1",RulesVersion=World.EquipmentEnabled ? 4 : World.NormalRaid ? 3 : 2,
                    Session=new SessionSaveData{ResetCount=ResetCount,PendingSeconds=PendingSeconds.ToString(CultureInfo.InvariantCulture),Paused=paused,Speed=speed},
                    Scenario=new ScenarioSaveData{
                        StartWithWeapons=scenario.StartWithWeapons,InitialRaidStove=scenario.InitialRaidStove.HasValue?PositionSaveData.From(scenario.InitialRaidStove.Value):null,
                        Width=scenario.Width,Height=scenario.Height,NeedsEnabled=scenario.NeedsEnabled,BlockedTiles=scenario.BlockedTiles.Select(PositionSaveData.From).ToList(),
                        Colonists=scenario.Colonists.Select(x=>new ColonistSeedSaveData{Id=x.Id,Position=PositionSaveData.From(x.Position),Nutrition=x.Nutrition,Rest=x.Rest}).ToList(),
                        Resources=scenario.Resources.Select(x=>new ResourceSeedSaveData{Id=x.Id,Position=PositionSaveData.From(x.Position),Kind=x.Kind.ToString(),Amount=x.Amount}).ToList(),
                        Beds=scenario.Beds.Select(x=>new BedSeedSaveData{Id=x.Id,Position=PositionSaveData.From(x.Position)}).ToList(),
                        Gatherables=scenario.Gatherables.Select(x=>new GatherSeedSaveData{Id=x.Id,Position=PositionSaveData.From(x.Position),Kind=x.Kind.ToString()}).ToList()},
                    World=World.CaptureSave()});
            }
            catch(OverflowException){return new SaveCaptureResult(false,errorCode:"SaveInvalidData");}
        }
        public static bool TryRestoreSave(SaveData data,out SimulationSession restored,out string error)
        {
            restored=null;error="SaveInvalidData";
            var validation=SaveValidation.Validate(data);if(!validation.IsValid){error=validation.ErrorCode;return false;}
            try
            {
                var s=data.Scenario;
                var scenario=new ScenarioDefinition(s.Width,s.Height,s.BlockedTiles.Select(x=>x.ToPosition()),
                    s.Colonists.Select(x=>new ColonistSeed(x.Id,x.Position.ToPosition(),x.Nutrition,x.Rest)),
                    s.Resources.Select(x=>new ResourceSeed(x.Id,SaveState.Enum<ResourceKind>(x.Kind),x.Amount,x.Position.ToPosition())),
                    s.Beds.Select(x=>new BedSeed(x.Id,x.Position.ToPosition())),s.NeedsEnabled,
                    s.Gatherables.Select(x=>new GatherSeed(x.Id,SaveState.Enum<GatherKind>(x.Kind),x.Position.ToPosition())),s.InitialRaidStove?.ToPosition(),s.StartWithWeapons);
                var candidate=new SimulationSession(scenario);
                candidate.World=WorldState.RestoreSave(data.World);candidate.World.ValidateInitialRaidStove(scenario);candidate.World.ValidateSaveLedger(scenario);candidate.ResetCount=data.Session.ResetCount;
                candidate.PendingSeconds=decimal.Parse(data.Session.PendingSeconds,CultureInfo.InvariantCulture);
                restored=candidate;error=string.Empty;return true;
            }
            catch(Exception ex) when(ex is ArgumentException||ex is InvalidOperationException||ex is OverflowException||ex is System.Collections.Generic.KeyNotFoundException||ex is NullReferenceException)
            {return false;}
        }
    }
}
