using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using NUnit.Framework;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class SaveRepositoryTests
    {
        private string directory;
        private SaveFileService files;
        private SaveRepository repository;
        private static SaveData Data(long ticks=0)
        {
            var session=new SimulationSession(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,new GridPosition(1,1))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,20,new GridPosition(2,1))},needsEnabled:false));
            session.Step((int)ticks);session.AdvanceTime(.037m);
            var capture=session.CaptureSaveData(true,3);Assert.That(capture.Success,Is.True,capture.ErrorCode);return capture.Data;
        }
        [SetUp] public void Setup()
        {
            directory=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"..","TestResults","SaveRepositoryTests",Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);files=new SaveFileService();repository=new SaveRepository(directory,files);
        }
        [TearDown] public void Cleanup()
        {
            files.BeforeAtomicStage=null;
            var root=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"..","TestResults","SaveRepositoryTests"))+Path.DirectorySeparatorChar;
            if(directory.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(directory))Directory.Delete(directory,true);
        }
        private string SlotPath(string id)=>Path.Combine(directory,id+".save.json");
        private SaveSlotInfo Saved(string name="테스트",long ticks=0)
        {var result=repository.SaveNew(name,Data(ticks));Assert.That(result.Success,Is.True,result.ErrorCode);return result.Slot;}
        private static SaveEnvelope ReadEnvelope(string path)
        {using(var stream=File.OpenRead(path))return (SaveEnvelope)new DataContractJsonSerializer(typeof(SaveEnvelope)).ReadObject(stream);}
        private static void WriteEnvelope(string path,SaveEnvelope value)
        {using(var stream=File.Create(path))new DataContractJsonSerializer(typeof(SaveEnvelope)).WriteObject(stream,value);}

        [Test] public void NamesUseSupportedCharactersAndNeverBecomePaths()
        {
            Assert.That(SaveRepository.TryNormalizeName(" 한글 ㄱㅏ Save 01 -_()[]/! ",out var name,out _),Is.True);
            Assert.That(name,Is.EqualTo("한글 ㄱㅏ Save 01 -_()[]/!"));
            Assert.That(SaveRepository.TryNormalizeName(new string('가',40),out _,out _),Is.True);
            foreach(var bad in new[]{"", "   ",new string('가',41),"별😀","e\u0301","a\nb"})
                Assert.That(repository.SaveNew(bad,Data()).Success,Is.False,bad);
            var slot=Saved("../../outside");
            Assert.That(File.Exists(SlotPath(slot.SlotId)),Is.True);
            Assert.That(SaveRepository.IsSlotId(slot.SlotId),Is.True);
            Assert.That(repository.ListSlots().Slots.Count,Is.EqualTo(1));
            Assert.That(repository.Load("../outside").ErrorCode,Is.EqualTo("SaveInvalidSlot"));
        }

        [Test] public void SameNamedSlotsRemainIndependentAcrossOverwriteAndRepositoryRestart()
        {
            var a=Saved("동일",3);var b=Saved("동일",9);var bBefore=File.ReadAllBytes(SlotPath(b.SlotId));
            Assert.That(repository.Overwrite(a.SlotId,"변경",Data(12)).Success,Is.True);
            var reopened=new SaveRepository(directory);
            Assert.That(reopened.Load(a.SlotId).Data.World.Tick,Is.EqualTo(12));
            Assert.That(reopened.Load(b.SlotId).Data.World.Tick,Is.EqualTo(9));
            CollectionAssert.AreEqual(bBefore,File.ReadAllBytes(SlotPath(b.SlotId)));
            Assert.That(reopened.ListSlots().Slots.Count,Is.EqualTo(2));
            Assert.That(Directory.GetFiles(directory,"*.tmp").Length,Is.Zero);
        }

        [Test] public void SlotListUsesTimestampThenOrdinalIdAndIgnoresTemporaryFiles()
        {
            var a=Saved("A");var b=Saved("B");
            foreach(var slot in new[]{a,b}){var value=ReadEnvelope(SlotPath(slot.SlotId));value.SavedAtUtc="2026-11-17T00:00:00.0000000Z";WriteEnvelope(SlotPath(slot.SlotId),value);}
            File.WriteAllText(Path.Combine(directory,"abandoned.save.json.tmp"),"broken");
            var list=repository.ListSlots();Assert.That(list.Success,Is.True);
            CollectionAssert.AreEqual(new[]{a.SlotId,b.SlotId}.OrderBy(x=>x,StringComparer.Ordinal),list.Slots.Select(x=>x.SlotId));
        }

        [Test] public void FrozenLegacyFixtureLoadsWithoutChangingOriginalAndCanSaveAsNew()
        {
            var fixture=Path.Combine(UnityEngine.Application.dataPath,"Game","Tests","Fixtures","legacy-v1.json");
            Assert.That(File.Exists(fixture),Is.True,"Root-provided pre-change Player fixture is required.");
            var original=File.ReadAllBytes(fixture);
            using(var hash=SHA256.Create())Assert.That(BitConverter.ToString(hash.ComputeHash(original)).Replace("-",""),
                Is.EqualTo("ABE5ED857AB68284650D14EDE1B96C2CACB2F4C7FD4F5B7282B39D81E4FDE6F1"));
            var legacy=Path.Combine(directory,"settlement-v1.json");File.WriteAllBytes(legacy,original);
            var loaded=repository.Load(SaveRepository.LegacySlotId);Assert.That(loaded.Success,Is.True,loaded.ErrorCode);
            Assert.That(loaded.Slot.IsLegacy,Is.True);Assert.That(SimulationSession.TryRestoreSave(loaded.Data,out var restored,out var error),Is.True,error);
            var saved=repository.SaveNew("기존 진행 복사",loaded.Data);Assert.That(saved.Success,Is.True,saved.ErrorCode);
            var roundTrip=repository.Load(saved.Slot.SlotId);Assert.That(roundTrip.Success,Is.True,roundTrip.ErrorCode);
            Assert.That(SimulationSession.TryRestoreSave(roundTrip.Data,out var restoredAgain,out error),Is.True,error);
            restored.Step(30);restoredAgain.Step(30);
            Assert.That(restoredAgain.World.Tick,Is.EqualTo(restored.World.Tick));
            foreach(var kind in new[]{ResourceKind.Wood,ResourceKind.Ingredients,ResourceKind.Meal})
            {
                Assert.That(restoredAgain.World.ResourceTotal(kind,false),Is.EqualTo(restored.World.ResourceTotal(kind,false)));
                Assert.That(restoredAgain.World.ResourceTotal(kind,true),Is.EqualTo(restored.World.ResourceTotal(kind,true)));
                Assert.That(restoredAgain.World.CargoTotal(kind),Is.EqualTo(restored.World.CargoTotal(kind)));
            }
            Assert.That(repository.Overwrite(SaveRepository.LegacySlotId,"금지",loaded.Data).Success,Is.False);
            CollectionAssert.AreEqual(original,File.ReadAllBytes(legacy));CollectionAssert.AreEqual(original,File.ReadAllBytes(fixture));
        }

        [Test] public void EnvelopeMismatchVersionSummaryAndDuplicateIdsRejectLoadAndOverwrite()
        {
            var slot=Saved();var path=SlotPath(slot.SlotId);var original=File.ReadAllBytes(path);
            foreach(var mutate in new Action<SaveEnvelope>[] {x=>x.FormatVersion=99,x=>x.Tick++,x=>x.ColonistCount++,x=>x.SlotId=Guid.NewGuid().ToString("N"),x=>x.SavedAtUtc="not-date"})
            {
                File.WriteAllBytes(path,original);var value=ReadEnvelope(path);mutate(value);WriteEnvelope(path,value);var before=File.ReadAllBytes(path);
                Assert.That(repository.Load(slot.SlotId).Success,Is.False);
                Assert.That(repository.Overwrite(slot.SlotId,"수정",Data()).Success,Is.False);
                CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            }
            File.WriteAllBytes(path,original);File.Copy(path,SlotPath(Guid.NewGuid().ToString("N")));
            Assert.That(repository.ListSlots().Slots.All(x=>x.ErrorCode=="SaveDuplicateSlot"),Is.True);
            Assert.That(repository.Load(slot.SlotId).ErrorCode,Is.EqualTo("SaveDuplicateSlot"));
        }

        [Test] public void CorruptExtraRootAndOversizeEnvelopesAreListedAsErrors()
        {
            var slot=Saved();var path=SlotPath(slot.SlotId);var valid=File.ReadAllText(path);
            foreach(var bad in new[]{"{broken",valid+"{}",valid.Replace("\"payload\":","\"missingPayload\":" )})
            {File.WriteAllText(path,bad);Assert.That(repository.ListSlots().Slots.Single().IsValid,Is.False);Assert.That(repository.Load(slot.SlotId).Success,Is.False);}
            using(var stream=File.Create(path))stream.SetLength(SaveValidation.MaximumFileBytes+1L);
            Assert.That(repository.Load(slot.SlotId).ErrorCode,Is.EqualTo("SaveInvalidData"));
            Assert.That(repository.ListSlots().Slots.Count,Is.EqualTo(1));
        }

        [Test] public void InvalidPayloadCannotReplaceValidSlot()
        {
            var slot=Saved();var before=File.ReadAllBytes(SlotPath(slot.SlotId));var data=Data();data.World.Resources[0].Amount++;
            Assert.That(repository.Overwrite(slot.SlotId,"장부 불일치",data).Success,Is.False);
            CollectionAssert.AreEqual(before,File.ReadAllBytes(SlotPath(slot.SlotId)));
            Assert.That(repository.Load(slot.SlotId).Success,Is.True);
        }

        [Test] public void OversizedWritePreservesReadableSlot()
        {
            var slot=Saved();
            var path=SlotPath(slot.SlotId);
            var before=File.ReadAllBytes(path);
            var data=Data();
            data.World.Colonists[0].WorkReason=new string('x',SaveValidation.MaximumFileBytes);
            Assert.That(repository.Overwrite(slot.SlotId,"Size boundary",data).Success,Is.False);
            CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            Assert.That(repository.Load(slot.SlotId).Success,Is.True);
        }

        [Test] public void EveryAtomicFailurePreservesOriginalAndNoNewSlotLeaks()
        {
            var slot=Saved();var before=File.ReadAllBytes(SlotPath(slot.SlotId));
            foreach(var stage in new[]{"Write","ReadBack","Replace"})
            {
                files.BeforeAtomicStage=current=>{if(current==stage)throw new IOException("Injected "+stage);};
                Assert.That(repository.Overwrite(slot.SlotId,"실패",Data(10)).ErrorCode,Is.EqualTo("SaveIoError"));
                Assert.That(repository.SaveNew("실패 신규",Data()).Success,Is.False);
                CollectionAssert.AreEqual(before,File.ReadAllBytes(SlotPath(slot.SlotId)));
                Assert.That(Directory.GetFiles(directory,"*.tmp").Length,Is.Zero);
                Assert.That(repository.ListSlots().Slots.Count,Is.EqualTo(1));
            }
        }

        [Test] public void LockedDestinationPreservesOriginalEnvelope()
        {
            var slot=Saved();var path=SlotPath(slot.SlotId);var before=File.ReadAllBytes(path);
            using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                Assert.That(repository.Overwrite(slot.SlotId,"실패",Data(5)).ErrorCode,Is.EqualTo("SaveIoError"));
            CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }

        [Test] public void NestedFileRequestIsRejectedWhileFirstSaveFinishes()
        {
            SaveSlotWriteResult nested=null;
            files.BeforeAtomicStage=stage=>{if(stage=="Write")nested=repository.SaveNew("중복",Data(4));};
            var first=repository.SaveNew("정상",Data(2));Assert.That(first.Success,Is.True,first.ErrorCode);
            Assert.That(nested,Is.Not.Null);Assert.That(nested.ErrorCode,Is.EqualTo("SaveBusy"));
            Assert.That(repository.ListSlots().Slots.Count,Is.EqualTo(1));
        }

        [Test] public void SettingsReplacementRetriesOnlyIntactFileErrorAndStopsAfterThreeAttempts()
        {
            var path=Path.Combine(directory,"settings","user-settings.json");
            var io=new SaveFileService();var store=new UserSettingsStore(path,io);
            Assert.That(store.Save(new UserSettings()).Success,Is.True);
            var attempts=0;
            io.BeforeAtomicStage=stage=>{
                if(stage=="Replace" && ++attempts<3)throw new IOException("Transient replacement failure",unchecked((int)0x80070497));
            };
            var original = File.ReadAllBytes(path);
            var result = store.Save(new UserSettings {UiScalePercent=150});
            Assert.That(attempts, Is.EqualTo(3));
            if (result.Success)
            {
                Assert.That(store.Load().Settings.UiScalePercent, Is.EqualTo(150));
            }
            else
            {
                // The final real Windows replace can itself encounter the same transient error.
                Assert.That(result.ErrorCode, Is.EqualTo("SaveIoError"));
                Assert.That(io.LastFailureDiagnostic, Is.EqualTo("Replace:IOException:" + unchecked((int)0x80070497)));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                Assert.That(store.Load().Settings.UiScalePercent, Is.EqualTo(100));
                TestContext.WriteLine("Third replace also hit Win32 1175. The retry bound preserved the prior settings.");
            }
            foreach(var code in new[]{unchecked((int)0x80070497),unchecked((int)0x80070498),unchecked((int)0x80070020)})
            {
                attempts=0;var before=File.ReadAllBytes(path);
                io.BeforeAtomicStage=stage=>{if(stage=="Replace"){attempts++;throw new IOException("Persistent replacement failure",code);}};
                Assert.That(store.Save(new UserSettings {UiScalePercent=125}).Success,Is.False);
                Assert.That(attempts,Is.EqualTo(code==unchecked((int)0x80070497)?3:1));
                Assert.That(io.LastFailureDiagnostic,Does.StartWith("Replace:IOException:"));
                CollectionAssert.AreEqual(before,File.ReadAllBytes(path));Assert.That(File.Exists(path+".tmp"),Is.False);
            }
        }

        [Test] public void RepeatedSettingsReplacementRetainsLastSuccessfulFileOnIoFailure()
        {
            var path=Path.Combine(directory,"settings","user-settings.json");
            var settingsFiles=new SaveFileService();var store=new UserSettingsStore(path,settingsFiles);
            Assert.That(store.Save(new UserSettings()).Success,Is.True);
            var failures=0;
            for(var cycle=0;cycle<2000;cycle++)
            {
                var before=File.ReadAllBytes(path);
                var percent=new[]{100,125,150}[cycle%3];
                var saved=store.Save(new UserSettings {UiScalePercent=percent});
                var diagnostic=settingsFiles.LastFailureDiagnostic;
                if(!saved.Success)
                {
                    failures++;
                    TestContext.WriteLine("Settings cycle="+cycle+" code="+saved.ErrorCode+" diagnostic="+diagnostic);
                    Assert.That(saved.ErrorCode,Is.EqualTo("SaveIoError"),diagnostic);
                    CollectionAssert.AreEqual(before,File.ReadAllBytes(path),diagnostic);
                }
                var loaded=store.Load();
                Assert.That(loaded.Success,Is.True,settingsFiles.LastFailureDiagnostic);
                if(saved.Success)Assert.That(loaded.Settings.UiScalePercent,Is.EqualTo(percent));
                Assert.That(File.Exists(path+".tmp"),Is.False);
            }
            TestContext.WriteLine("Observed settings I/O failures="+failures);
        }

        [Test] public void SettingsRoundTripDefaultsAndCorruptionAreSeparateFromGameState()
        {
            var settingsPath=Path.Combine(directory,"settings","user-settings.json");var store=new UserSettingsStore(settingsPath);
            Assert.That(store.Load().Success,Is.True);Assert.That(store.Load().Settings.UiScalePercent,Is.EqualTo(100));
            var slot=Saved();var settings=new UserSettings {UiScalePercent=150,RecentSlotId=slot.SlotId};
            Assert.That(store.Save(settings).Success,Is.True);var loaded=new UserSettingsStore(settingsPath).Load();
            Assert.That(loaded.Success,Is.True);Assert.That(loaded.Settings.UiScalePercent,Is.EqualTo(150));Assert.That(loaded.Settings.RecentSlotId,Is.EqualTo(slot.SlotId));
            var before=File.ReadAllBytes(settingsPath);settings.UiScalePercent=200;
            Assert.That(store.Save(settings).Success,Is.False);CollectionAssert.AreEqual(before,File.ReadAllBytes(settingsPath));
            File.WriteAllText(settingsPath,"{broken");loaded=store.Load();Assert.That(loaded.Success,Is.False);Assert.That(loaded.Settings.UiScalePercent,Is.EqualTo(100));
            Assert.That(repository.Load(slot.SlotId).Success,Is.True);
        }

        [Test] public void SettingsWriteFailureKeepsOldRecentSlotWhileNewGameSaveSurvives()
        {
            var a=Saved("A");var settingsPath=Path.Combine(directory,"settings","user-settings.json");var settingsFiles=new SaveFileService();
            var store=new UserSettingsStore(settingsPath,settingsFiles);Assert.That(store.Save(new UserSettings {RecentSlotId=a.SlotId}).Success,Is.True);
            var b=Saved("B",7);settingsFiles.BeforeAtomicStage=stage=>{if(stage=="Replace")throw new IOException("Settings locked");};
            Assert.That(store.Save(new UserSettings {RecentSlotId=b.SlotId}).Success,Is.False);
            Assert.That(store.Load().Settings.RecentSlotId,Is.EqualTo(a.SlotId));
            Assert.That(repository.Load(b.SlotId).Data.World.Tick,Is.EqualTo(7));
            settingsFiles.BeforeAtomicStage=null;
        }
    }
}
