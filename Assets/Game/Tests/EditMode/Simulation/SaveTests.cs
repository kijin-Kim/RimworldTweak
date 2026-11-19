using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
namespace RimworldTweak.Tests.EditMode
{
    public sealed class SaveTests
    {
        private string directory;
        private string path;
        private SaveFileService files;
        private static GridPosition P(int x,int y)=>new GridPosition(x,y);
        private static SimulationSession Session()=>new SimulationSession(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
            new[]{new ColonistSeed(1,P(1,1))},new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(2,1))},null,false));
        private static SaveData Data()
        {
            var session=Session();session.AdvanceTime(0.037m);var captured=session.CaptureSaveData(true,3);
            Assert.That(captured.Success,Is.True,captured.ErrorCode);return captured.Data;
        }
        [SetUp] public void Setup()
        {
            directory=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"..","TestResults","SaveFileTests",Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);path=Path.Combine(directory,"settlement-v1.json");files=new SaveFileService();
        }
        [TearDown] public void Cleanup()
        {
            var root=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"..","TestResults","SaveFileTests"))+Path.DirectorySeparatorChar;
            if(directory!=null&&directory.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(directory))Directory.Delete(directory,true);
        }
        [Test] public void JsonRoundTripKeepsExactLongDecimalAndStringEnums()
        {
            var data=Data();data.World.Tick=9007199254740993L;
            var saved=files.Save(path,data);Assert.That(saved.Success,Is.True,saved.ErrorCode);
            var text=File.ReadAllText(path);Assert.That(text,Does.Contain("9007199254740993"));Assert.That(text,Does.Contain("\"Kind\":\"Wood\""));
            Assert.That(text,Does.Contain("\"PendingSeconds\":\"0.037\""));Assert.That(text,Does.Not.Contain("__type"));
            var loaded=files.Load(path);Assert.That(loaded.Success,Is.True,loaded.ErrorCode);
            Assert.That(loaded.Data.World.Tick,Is.EqualTo(data.World.Tick));Assert.That(loaded.Data.Session.PendingSeconds,Is.EqualTo("0.037"));
            Assert.That(loaded.Data.Session.Paused,Is.True);Assert.That(loaded.Data.Session.Speed,Is.EqualTo(3));Assert.That(File.Exists(path+".tmp"),Is.False);
        }
        [Test] public void OverwriteReplacesSlotAfterValidationAndKeepsNoTemporaryFile()
        {
            var data=Data();Assert.That(files.Save(path,data).Success,Is.True);data.World.Tick=10;
            Assert.That(files.Save(path,data).Success,Is.True);Assert.That(files.Load(path).Data.World.Tick,Is.EqualTo(10));Assert.That(File.Exists(path+".tmp"),Is.False);
        }
        [Test] public void SettlementMapKeepsStartingLayoutAndValidatesBothSavedDimensions()
        {
            var small = WorkScenario.CreateTestMap();
            var large = WorkScenario.CreateSettlement();
            Assert.That(small.Width, Is.EqualTo(32));
            Assert.That(large.Width, Is.EqualTo(250));
            Assert.That(large.Height, Is.EqualTo(250));
            Assert.That(large.Colonists[0].Position, Is.EqualTo(P(125,125)));
            var offsetX = large.Colonists[0].Position.X - small.Colonists[0].Position.X;
            var offsetY = large.Colonists[0].Position.Y - small.Colonists[0].Position.Y;
            GridPosition Shift(GridPosition p) => P(p.X + offsetX, p.Y + offsetY);
            Assert.That(large.BlockedTiles, Is.EqualTo(small.BlockedTiles.Select(Shift)));
            Assert.That(large.Colonists.Select(p=>p.Position), Is.EqualTo(small.Colonists.Select(p=>Shift(p.Position))));
            Assert.That(large.Resources.Select(r=>r.Position), Is.EqualTo(small.Resources.Select(r=>Shift(r.Position))));
            Assert.That(large.Beds.Select(b=>b.Position), Is.EqualTo(small.Beds.Select(b=>Shift(b.Position))));
            Assert.That(large.Gatherables.Select(g=>g.Position), Is.EqualTo(small.Gatherables.Select(g=>Shift(g.Position))));

            var session = new SimulationSession(large);
            Assert.That(session.World.QueueSetBlocked(P(0,249),true), Is.True);
            Assert.That(session.World.QueueSetBlocked(P(250,249),true), Is.False);
            session.Step(1);
            var data = session.CaptureSaveData().Data;
            Assert.That(files.Save(path,data).Success, Is.True);
            var loaded = files.Load(path);
            Assert.That(loaded.Success, Is.True);
            Assert.That(SimulationSession.TryRestoreSave(loaded.Data,out var restored,out var error),Is.True,error);
            Assert.That(restored.World.BlockedTiles, Does.Contain(P(0,249)));
            data.World.Width=251;Assert.That(SaveValidation.Validate(data).IsValid, Is.False);
            data.World.Width=250;data.Scenario.Height=251;Assert.That(SaveValidation.Validate(data).IsValid, Is.False);
            data.Scenario.Height=249;Assert.That(SaveValidation.Validate(data).IsValid, Is.False);
            data.Scenario.Height=250;data.World.BlockedTiles[0].X=250;Assert.That(SaveValidation.Validate(data).IsValid, Is.False);
        }

        [Test] public void LargeReversedZoneAndPathQueriesSurviveTerrainChangesAndRestore()
        {
            var session = new SimulationSession(WorkScenario.CreateSettlement());
            var world = session.World;
            var start = P(0,0);
            var goal = P(249,249);
            var original = world.FindPath(start,goal);
            Assert.That(original.Count, Is.EqualTo(498));
            Assert.That(world.FindPath(start,P(248,249)).Count, Is.EqualTo(497));
            Assert.That(world.QueueSetBlocked(P(248,249),true), Is.True);
            Assert.That(world.QueueSetBlocked(P(249,248),true), Is.True);
            session.Step(1);
            Assert.That(world.FindPath(start,goal), Is.Null);
            Assert.That(world.QueueSetBlocked(P(248,249),false), Is.True);
            session.Step(1);
            Assert.That(world.FindPath(start,goal).Count, Is.EqualTo(498));
            Assert.That(original.Last(), Is.EqualTo(goal));
            Assert.That(world.QueueCreateStorage(P(31,29),P(0,0)), Is.True);
            Assert.That(world.CanCreateGrowingZone(P(33,33),P(31,29)), Is.False);
            Assert.That(world.CreateGrowingZone(P(60,43),P(32,30)), Is.True);
            Assert.That(world.CanCreateStorage(P(61,44),P(60,43)), Is.False);
            var captured = session.CaptureSaveData();
            Assert.That(captured.Success, Is.True);
            Assert.That(SimulationSession.TryRestoreSave(captured.Data,out var restored,out var error), Is.True,error);
            Assert.That(restored.World.StorageZones.Single().Slots.Count,Is.EqualTo(32*30));
            Assert.That(restored.World.GrowingZones.Single().Cells.Count,Is.EqualTo(29*14));
            captured.Data.World.GrowingZones.Single().CellIds.RemoveAt(0);
            Assert.That(SaveValidation.Validate(captured.Data).IsValid,Is.False);
        }
        [Test] public void InvalidSaveCannotOverwritePreviouslyValidFile()
        {
            var data=Data();Assert.That(files.Save(path,data).Success,Is.True);var before=File.ReadAllBytes(path);
            data.World.Resources[0].Amount=-1;var result=files.Save(path,data);
            Assert.That(result.Success,Is.False);Assert.That(result.ErrorCode,Is.EqualTo("SaveInvalidData"));CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            Assert.That(files.Load(path).Success,Is.True);
            data=Data();data.World.Resources[0].Amount++;
            Assert.That(files.Save(path,data).Success,Is.False);CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            data=Data();data.World.Resources[0].Amount--;data.World.Colonists[0].CargoAmount=1;
            data.World.Colonists[0].WorkActivity=nameof(WorkActivity.GoingToStore);
            Assert.That(SaveValidation.Validate(data).IsValid,Is.True,"Ledger is preserved, but action lacks a destination claim.");
            Assert.That(files.Save(path,data).Success,Is.False);CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }
        [Test] public void ReplaceFailureLeavesOriginalFileAndCleansOnlyTemporaryFile()
        {
            var data=Data();Assert.That(files.Save(path,data).Success,Is.True);var before=File.ReadAllBytes(path);data.World.Tick=20;
            using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {var result=files.Save(path,data);Assert.That(result.Success,Is.False);Assert.That(result.ErrorCode,Is.EqualTo("SaveIoError"));}
            CollectionAssert.AreEqual(before,File.ReadAllBytes(path));Assert.That(File.Exists(path+".tmp"),Is.False);
        }
        [Test] public void MissingOrTemporaryOnlySlotIsNotLoaded()
        {
            var result=files.Load(path);Assert.That(result.Success,Is.False);Assert.That(result.ErrorCode,Is.EqualTo("SaveNotFound"));
            Assert.That(files.Save(path,Data()).Success,Is.True);File.Move(path,path+".tmp");
            Assert.That(files.Load(path).ErrorCode,Is.EqualTo("SaveNotFound"));Assert.That(File.Exists(path+".tmp"),Is.True);
        }
        [Test] public void CorruptTrailingAndOversizeFilesAreRejectedWithoutMutation()
        {
            Assert.That(files.Save(path,Data()).Success,Is.True);var valid=File.ReadAllText(path);
            foreach(var bad in new[]{"{broken",valid+"{}",valid.Replace("\"schemaVersion\":1,","")})
            {
                File.WriteAllText(path,bad);var result=files.Load(path);Assert.That(result.Success,Is.False,"Malformed JSON suffix: "+bad.Substring(Math.Max(0,bad.Length-80)));
                Assert.That(result.ErrorCode,Is.EqualTo("SaveInvalidData"));Assert.That(File.ReadAllText(path),Is.EqualTo(bad));
            }
            using(var stream=new FileStream(path,FileMode.Create,FileAccess.Write))stream.SetLength(SaveValidation.MaximumFileBytes+1L);
            Assert.That(files.Load(path).ErrorCode,Is.EqualTo("SaveInvalidData"));Assert.That(new FileInfo(path).Length,Is.EqualTo(SaveValidation.MaximumFileBytes+1L));
        }
        [Test] public void UnsupportedVersionAndNumericEnumAreRejected()
        {
            var data=Data();Assert.That(files.Save(path,data).Success,Is.True);var valid=File.ReadAllText(path);
            File.WriteAllText(path,valid.Replace("\"schemaVersion\":1","\"schemaVersion\":99"));
            Assert.That(files.Load(path).ErrorCode,Is.EqualTo("SaveUnsupportedVersion"));
            data.World.Resources[0].Kind="0";Assert.That(SaveValidation.Validate(data).IsValid,Is.False);
        }
        [Test] public void ValidationRejectsDuplicateIdsMissingReferencesAndBadNextId()
        {
            var data=Data();data.World.NextEntityId=data.World.Resources[0].Id;Assert.That(SaveValidation.Validate(data).IsValid,Is.False);
            data=Data();data.World.Resources[0].Id=data.World.Colonists[0].Id;Assert.That(SaveValidation.Validate(data).IsValid,Is.False);
            data=Data();data.World.Colonists[0].WorkTargetId=99999;Assert.That(SaveValidation.Validate(data).IsValid,Is.False);
            data=Data();data.World.WorkPolicies[0].Priorities[0].Priority=4;Assert.That(SaveValidation.Validate(data).IsValid,Is.False);
            data=Data();data.Session.PendingSeconds="0,037";Assert.That(SaveValidation.Validate(data).IsValid,Is.False);
            data=Data();data.World.Resources[0].Amount++;
            Assert.That(SimulationSession.TryRestoreSave(data,out var invalidWorld,out var restoreError),Is.False);
            Assert.That(invalidWorld,Is.Null);Assert.That(restoreError,Is.EqualTo("SaveInvalidData"));
        }
        [Test] public void MismatchedClaimOwnerActionIsRejectedWithoutChangingDto()
        {
            var data=Data();var resource=data.World.Resources[0];resource.Claim.OwnerId=1;resource.Claim.ActionId=999;
            var result=SaveValidation.Validate(data);Assert.That(result.IsValid,Is.False);Assert.That(result.FieldPath,Does.Contain("Claim"));
            Assert.That(resource.Claim.OwnerId,Is.EqualTo(1));Assert.That(resource.Claim.ActionId,Is.EqualTo(999));
        }
        [Test] public void StoredStackSharedClaimMustMatchSlotAuthority()
        {
            var session=Session();var w=session.World;w.QueueCreateStorage(P(6,1),P(6,1));
            for(var i=0;i<300&&w.ResourceTotal(ResourceKind.Wood,true)==0;i++)session.Step(1);
            var captured=session.CaptureSaveData();Assert.That(captured.Success,Is.True,captured.ErrorCode);var data=captured.Data;
            Assert.That(SaveValidation.Validate(data).IsValid,Is.True);var slot=data.World.StorageZones.Single().Slots.Single();
            var stack=data.World.Resources.Single(r=>r.Id==slot.StackId);stack.Claim.OwnerId=1;stack.Claim.ActionId=data.World.Colonists[0].ActionId;
            Assert.That(SaveValidation.Validate(data).IsValid,Is.False);
            session=Session();w=session.World;Assert.That(w.PlaceBlueprint(BuildingKind.Stove,P(4,4)),Is.True);
            for(var i=0;i<600&&!w.Buildings.Any(b=>b.Exists&&b.Kind==BuildingKind.Stove);i++)session.Step(1);
            var stove=w.Buildings.Single(b=>b.Exists&&b.Kind==BuildingKind.Stove);
            Assert.That(w.DeleteCookingOrder(stove.Id),Is.True);captured=session.CaptureSaveData();
            Assert.That(captured.Success,Is.True,captured.ErrorCode);data=captured.Data;
            Assert.That(SaveValidation.Validate(data).IsValid,Is.True);
            var deletedOrder=data.World.CookingOrders.Single(o=>!o.Exists);
            deletedOrder.Claim=new ClaimSaveData{OwnerId=1,ActionId=data.World.Colonists[0].ActionId};
            var invalid=SaveValidation.Validate(data);Assert.That(invalid.IsValid,Is.False);
            Assert.That(invalid.FieldPath,Is.EqualTo("CookingOrder.SharedClaim"));
            Assert.That(SimulationSession.TryRestoreSave(data,out _,out _),Is.False);
        }
        [Test] public void InvalidPlayerRoutesRebuildOrWaitWithoutMutatingSavedFiles()
        {
            foreach(var unreachable in new[]{false,true})
            {
                var original=Session();original.World.SetDrafted(1,true);original.World.QueueMove(1,P(6,1));original.Step(2);
                var pawn=original.World.Colonists[0];var position=pawn.Position;var progress=pawn.MovementProgress;
                Assert.That(progress,Is.GreaterThan(0));var captured=original.CaptureSaveData(true,3);
                Assert.That(captured.Success,Is.True,captured.ErrorCode);
                if(unreachable)
                    for(var y=0;y<8;y++)captured.Data.World.BlockedTiles.Add(new PositionSaveData{X=4,Y=y});
                else captured.Data.World.Colonists[0].Path[0]=new PositionSaveData{X=7,Y=7};
                var legacyPath = Path.Combine(directory, unreachable ? "blocked-v1.json" : "rerouted-v1.json");
                var legacySaved = files.Save(legacyPath, captured.Data);
                Assert.That(legacySaved.Success, Is.True, legacySaved.ErrorCode);
                var repository=new SaveRepository(directory);var slot=repository.SaveNew(unreachable?"경로 차단":"경로 재생성",captured.Data);
                Assert.That(slot.Success,Is.True,slot.ErrorCode);
                var slotPath=Path.Combine(directory,slot.Slot.SlotId+".save.json");
                var legacyBytes=File.ReadAllBytes(legacyPath);var slotBytes=File.ReadAllBytes(slotPath);
                var legacy=files.Load(legacyPath);var named=new SaveRepository(directory).Load(slot.Slot.SlotId);
                Assert.That(legacy.Success,Is.True,legacy.ErrorCode);Assert.That(named.Success,Is.True,named.ErrorCode);
                foreach(var data in new[]{legacy.Data,named.Data})
                {
                    Assert.That(SimulationSession.TryRestoreSave(data,out var restored,out var error),Is.True,error);
                    var result=restored.World.Colonists[0];
                    Assert.That(result.IsDrafted,Is.True);Assert.That(result.Position,Is.EqualTo(position));
                    Assert.That(result.Destination,Is.EqualTo(P(6,1)));Assert.That(result.MovementProgress,Is.Zero);
                    Assert.That(result.Status,Is.EqualTo(unreachable?MovementStatus.Blocked:MovementStatus.Moving));
                    restored.Step(50);
                    Assert.That(result.Position,Is.EqualTo(unreachable?position:P(6,1)));
                    Assert.That(result.Status,Is.EqualTo(unreachable?MovementStatus.Blocked:MovementStatus.Arrived));
                    Assert.That(restored.World.ResourceTotal(ResourceKind.Wood,false),Is.EqualTo(20));
                }
                Assert.That(pawn.Position,Is.EqualTo(position));Assert.That(pawn.MovementProgress,Is.EqualTo(progress));
                Assert.That(original.World.Tick,Is.EqualTo(2));
                CollectionAssert.AreEqual(legacyBytes,File.ReadAllBytes(legacyPath));CollectionAssert.AreEqual(slotBytes,File.ReadAllBytes(slotPath));
            }
        }

        [Test] public void PausedMoveQueueAndFractionalTimeSurviveFileAndSessionRestore()
        {
            var original=Session();original.World.ToggleDraft(1);Assert.That(original.World.QueueMove(1,P(6,1)),Is.True);original.AdvanceTime(0.037m);
            var capture=original.CaptureSaveData(true,3);Assert.That(capture.Success,Is.True,capture.ErrorCode);Assert.That(files.Save(path,capture.Data).Success,Is.True);
            var loaded=files.Load(path);Assert.That(loaded.Success,Is.True,loaded.ErrorCode);
            Assert.That(SimulationSession.TryRestoreSave(loaded.Data,out var restored,out var error),Is.True,error);
            Assert.That(restored.World.Tick,Is.EqualTo(original.World.Tick));Assert.That(restored.PendingSeconds,Is.EqualTo(original.PendingSeconds));
            Assert.That(restored.World.PendingCommandCount,Is.EqualTo(1));original.AdvanceTime(0.063m);restored.AdvanceTime(0.063m);
            original.Step(30);restored.Step(30);Assert.That(restored.World.Colonists[0].Position,Is.EqualTo(original.World.Colonists[0].Position));
            Assert.That(restored.World.PathSearchCount,Is.EqualTo(original.World.PathSearchCount));
        }
    }
}
