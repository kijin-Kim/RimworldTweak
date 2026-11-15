using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Presentation;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace RimworldTweak.Tests.PlayMode
{
    public sealed class SaveIntegrationTests
    {
        private GameObject root;
        private SimulationRunner runner;
        private SettlementView view;
        private string directory;
        private static GridPosition P(int x,int y)=>new GridPosition(x,y);
        private void Setup()
        {
            root=new GameObject("Save integration");runner=root.AddComponent<SimulationRunner>();runner.enabled=false;
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),new[]{new ColonistSeed(1,P(1,1))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(2,1))},needsEnabled:false));
            runner.SetPaused(true);view=root.AddComponent<SettlementView>();view.Bind(runner);
            directory=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TestResults","save-integration-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);runner.SavePath=Path.Combine(directory,"settlement-v1.json");
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(root!=null)UnityEngine.Object.Destroy(root);
            if(directory!=null&&Directory.Exists(directory))
            {
                var expected=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TestResults"))+Path.DirectorySeparatorChar;
                Assert.That(directory.StartsWith(expected,StringComparison.OrdinalIgnoreCase),Is.True);
                Directory.Delete(directory,true);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveAndConfirmedLoadRestoreTimeStateAndClearSelectionToolsImmediately()
        {
            Setup();runner.SetSpeed(3);runner.AdvanceTime(.01m);runner.SetPaused(true);
            runner.ToggleDraft(1);Assert.That(runner.MoveColonist(1,P(6,1)),Is.True);
            var savedWorld=runner.Session.World;var tick=savedWorld.Tick;
            view.SaveLoadPanel.SaveButton.onClick.Invoke();Assert.That(File.Exists(runner.SavePath),Is.True);
            Assert.That(savedWorld.Tick,Is.EqualTo(tick));Assert.That(runner.IsPaused,Is.True);Assert.That(runner.SpeedMultiplier,Is.EqualTo(3));
            runner.Session.Step(8);view.SelectColonist(1);view.SetWorkTool(SettlementView.WorkTool.Storage);view.HandleMapClick(P(3,3),false);
            view.SaveLoadPanel.LoadButton.onClick.Invoke();Assert.That(runner.IsLoadConfirmationOpen,Is.True);
            view.SaveLoadPanel.ConfirmButton.onClick.Invoke();view.RefreshNow();
            Assert.That(runner.IsLoadConfirmationOpen,Is.False);Assert.That(runner.Session.World,Is.Not.SameAs(savedWorld));
            Assert.That(runner.Session.World.Tick,Is.EqualTo(tick));Assert.That(runner.Session.PendingSeconds,Is.EqualTo(.03m));
            Assert.That(runner.IsPaused,Is.True);Assert.That(runner.SpeedMultiplier,Is.EqualTo(3));
            Assert.That(runner.Session.World.PendingCommandCount,Is.EqualTo(1));Assert.That(runner.Session.World.Colonists[0].IsDrafted,Is.True);
            Assert.That(view.ActiveWorkTool,Is.EqualTo(SettlementView.WorkTool.None));Assert.That(view.StorageFirstCorner,Is.Null);
            Assert.That(view.SelectedColonistId,Is.Null);Assert.That(view.SelectedObjectId,Is.Null);
            runner.StepOnce();Assert.That(runner.Session.World.Colonists[0].Status,Is.EqualTo(MovementStatus.Moving));
            view.SelectColonist(1);view.ProcessShortcut(Key.R);
            Assert.That(runner.Session.World.Colonists[0].IsDrafted,Is.False);
            Assert.That(view.MoveSelected(P(6,1)),Is.False);view.RefreshNow();
            Assert.That(view.CommandFeedbackText.text,Does.Contain("R로 소집"));yield return null;
        }

        [UnityTest]
        public IEnumerator LoadConfirmationStopsTimeAndCancelKeepsLiveSessionAndSpeed()
        {
            Setup();Assert.That(runner.SaveNow(),Is.True);runner.SetSpeed(3);runner.AdvanceTime(.1m);view.SelectColonist(1);
            var original=runner.Session;var tick=original.World.Tick;var pending=original.PendingSeconds;
            Assert.That(runner.BeginLoad(),Is.True);Assert.That(runner.IsLoadConfirmationOpen,Is.True);
            runner.AdvanceTime(10m);Assert.That(original.World.Tick,Is.EqualTo(tick));
            view.SaveLoadPanel.CancelButton.onClick.Invoke();Assert.That(runner.IsLoadConfirmationOpen,Is.False);
            Assert.That(runner.Session,Is.SameAs(original));Assert.That(runner.IsPaused,Is.False);Assert.That(runner.SpeedMultiplier,Is.EqualTo(3));
            Assert.That(runner.Session.PendingSeconds,Is.EqualTo(pending));Assert.That(view.SelectedColonistId,Is.EqualTo(1));
            runner.AdvanceTime(.1m);Assert.That(original.World.Tick,Is.EqualTo(tick+3));yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidDtoAndCorruptFileKeepCurrentWorldClockAndSelection()
        {
            Setup();runner.SetSpeed(3);runner.AdvanceTime(.01m);runner.SetPaused(true);view.SelectColonist(1);
            var original=runner.Session;var world=original.World;var position=world.Colonists[0].Position;var pending=original.PendingSeconds;
            Assert.That(runner.TryApplySave(null),Is.False);Assert.That(runner.Session,Is.SameAs(original));
            var capture=original.CaptureSaveData(true,3);Assert.That(capture.Success,Is.True);capture.Data.SchemaVersion=999;
            Assert.That(runner.TryApplySave(capture.Data),Is.False);Assert.That(runner.Session,Is.SameAs(original));
            File.WriteAllText(runner.SavePath,"{invalid save file");
            var began=runner.BeginLoad();if(began)Assert.That(runner.ConfirmLoad(),Is.False);
            Assert.That(runner.Session,Is.SameAs(original));Assert.That(runner.Session.World,Is.SameAs(world));
            Assert.That(world.Tick,Is.Zero);Assert.That(world.Colonists[0].Position,Is.EqualTo(position));Assert.That(original.PendingSeconds,Is.EqualTo(pending));
            Assert.That(runner.IsPaused,Is.True);Assert.That(runner.SpeedMultiplier,Is.EqualTo(3));Assert.That(view.SelectedColonistId,Is.EqualTo(1));
            Assert.That(runner.SaveMessage,Is.Not.Empty);yield return null;
        }

        [UnityTest]
        public IEnumerator SaveSkipsOnlyFirstAutomaticFrameWithoutAccumulatingIoDelay()
        {
            Setup();runner.SetSpeed(3);runner.AdvanceTime(.01m);
            Assert.That(runner.SaveNow(),Is.True);runner.SetPaused(false);
            var tick=runner.Session.World.Tick;var pending=runner.Session.PendingSeconds;
            // The runner is disabled, so only these explicit Update messages advance automatic time.
            yield return null;
            Assert.That(Time.unscaledDeltaTime,Is.GreaterThan(0f));
            runner.SendMessage("Update");
            Assert.That(runner.Session.World.Tick,Is.EqualTo(tick));
            Assert.That(runner.Session.PendingSeconds,Is.EqualTo(pending));
            runner.SendMessage("Update");
            Assert.That(runner.Session.World.Tick>tick||runner.Session.PendingSeconds>pending,Is.True,
                "Only the first automatic frame after saving should be skipped.");
        }

        [UnityTest]
        public IEnumerator SaveFailureDoesNotReplaceExistingSlotOrAdvanceWorld()
        {
            Setup();Assert.That(runner.SaveNow(),Is.True);var bytes=File.ReadAllBytes(runner.SavePath);var tick=runner.Session.World.Tick;
            runner.Session.World.QueueSetBlocked(P(7,7),true);
            Assert.That(runner.SaveNow(),Is.False);Assert.That(File.ReadAllBytes(runner.SavePath),Is.EqualTo(bytes));
            Assert.That(runner.Session.World.Tick,Is.EqualTo(tick));Assert.That(runner.Session.World.PendingCommandCount,Is.EqualTo(1));
            Assert.That(runner.IsPaused,Is.True);yield return null;
        }
    }
}
