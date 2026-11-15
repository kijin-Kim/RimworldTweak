using System;
using System.Collections;
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
    public sealed class FarmingIntegrationTests
    {
        private GameObject root;
        private SimulationRunner runner;
        private SettlementView view;
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private void Setup()
        {
            root = new GameObject("Farming integration"); runner = root.AddComponent<SimulationRunner>(); runner.enabled = false;
            runner.Initialize(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(), new[] { new ColonistSeed(1, P(1, 1)) },
                Array.Empty<ResourceSeed>(), needsEnabled: false));
            runner.SetPaused(true); view = root.AddComponent<SettlementView>(); view.Bind(runner);
        }
        [UnityTearDown]
        public IEnumerator Cleanup() { if (root != null) UnityEngine.Object.Destroy(root); yield return null; }

        [UnityTest]
        public IEnumerator GrowingToolIsSingleUseAndPausedSettingsRefreshImmediately()
        {
            Setup(); var panel = view.FarmingPanel; var w = runner.Session.World;
            panel.ZoneButton.onClick.Invoke(); Assert.That(view.ActiveWorkTool, Is.EqualTo(SettlementView.WorkTool.Growing));
            view.HandleMapClick(P(3, 1), false); view.HandleMapClick(P(4, 1), false);
            Assert.That(view.ActiveWorkTool, Is.EqualTo(SettlementView.WorkTool.None));
            var zone = w.GrowingZones.Single(); Assert.That(zone.Cells.Count, Is.EqualTo(2));
            view.HandleMapClick(P(5, 1), false); Assert.That(w.GrowingZones.Count, Is.EqualTo(1));
            view.HandleMapClick(P(3, 1), false); view.RefreshNow();
            Assert.That(panel.ToggleSowingButton.interactable, Is.True);
            panel.ToggleSowingButton.onClick.Invoke(); Assert.That(zone.SowingEnabled, Is.False);
            Assert.That(panel.InfoText.text, Does.Contain("중지")); Assert.That(w.Tick, Is.Zero);
            Assert.That(panel.InfoText.text, Does.Contain("미수확 작물 제거"));
            panel.DeleteZoneButton.onClick.Invoke(); Assert.That(zone.Exists, Is.False);
            Assert.That(panel.ToggleSowingButton.interactable, Is.False); Assert.That(w.Tick, Is.Zero);
            Assert.That(w.PendingCommandCount, Is.Zero); yield return null;
        }

        [UnityTest]
        public IEnumerator GrowingToolFailureAndEscapeNeverQueueMovement()
        {
            Setup(); var w = runner.Session.World;
            runner.CreateStorage(P(3, 1), P(3, 1));
            view.FarmingPanel.ZoneButton.onClick.Invoke(); view.HandleMapClick(P(3, 1), false); view.HandleMapClick(P(3, 1), false);
            Assert.That(w.GrowingZones.Count, Is.Zero); Assert.That(view.ActiveWorkTool, Is.EqualTo(SettlementView.WorkTool.Growing));
            Assert.That(view.FarmingPanel.InfoText.text, Does.Contain("재배할 수 없는 위치"));
            view.HandleMapClick(P(4, 1), false); view.HandleMapClick(P(4, 1), false);
            Assert.That(w.GrowingZones.Single().Cells.Count, Is.EqualTo(1));
            view.FarmingPanel.ZoneButton.onClick.Invoke(); view.HandleMapClick(P(5, 1), false);
            Assert.That(view.ProcessShortcut(Key.Escape), Is.True);
            Assert.That(view.ActiveWorkTool, Is.EqualTo(SettlementView.WorkTool.None));
            view.FarmingPanel.ZoneButton.onClick.Invoke(); view.HandleMapClick(P(6, 1), true);
            Assert.That(view.ActiveWorkTool, Is.EqualTo(SettlementView.WorkTool.None));
            Assert.That(w.PendingCommandCount, Is.Zero); Assert.That(w.Colonists[0].HasPendingCommand, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseDraftAndResetKeepCropUiBoundToCurrentWorld()
        {
            Setup(); var w = runner.Session.World;
            runner.CreateGrowingZone(P(1, 1), P(1, 1)); runner.Session.Step(8);
            var cell = w.Crops.Single(); Assert.That(cell.OwnerId, Is.EqualTo(1));
            runner.AdvanceTime(10m); Assert.That(w.Tick, Is.EqualTo(8)); Assert.That(cell.SowProgress, Is.EqualTo(7));
            view.SelectColonist(1); view.ProcessShortcut(Key.R);
            Assert.That(cell.OwnerId, Is.Null); Assert.That(w.Colonists[0].IsDrafted, Is.True);
            Assert.That(view.LifeText.text, Does.Contain("소집")); Assert.That(view.ClaimText.text, Does.Contain("없음"));
            view.HandleMapClick(P(1, 1), false); view.RefreshNow();
            Assert.That(view.FarmingPanel.InfoText.text, Does.Contain("담당 없음"));
            runner.ResetWorld(); view.RefreshNow(); Assert.That(runner.Session.World.Crops.Count, Is.Zero);
            Assert.That(runner.Session.World.GrowingZones.Count, Is.Zero);
            Assert.That(view.FarmingPanel.ToggleSowingButton.interactable, Is.False);
            Assert.That(view.FarmingPanel.InfoText.text, Does.Not.Contain("파종 7")); yield return null;
        }

        [UnityTest]
        public IEnumerator EqualGameTimeAtOneAndThreeSpeedProducesSameHarvest()
        {
            Setup(); runner.CreateGrowingZone(P(1, 1), P(1, 1)); runner.SetSpeed(1); runner.AdvanceTime(2.1m);
            runner.SetSowingEnabled(runner.Session.World.GrowingZones[0].Id, false); runner.AdvanceTime(243m);
            var one = runner.Session.World;
            var tick = one.Tick; var produced = one.ProducedAmount(ResourceKind.Ingredients);
            var stage = one.Crops.Single().Stage;
            Assert.That(produced, Is.EqualTo(6));
            runner.ResetWorld(); runner.CreateGrowingZone(P(1, 1), P(1, 1)); runner.SetSpeed(3); runner.AdvanceTime(.7m);
            runner.SetSowingEnabled(runner.Session.World.GrowingZones[0].Id, false); runner.AdvanceTime(81m);
            var three = runner.Session.World;
            Assert.That(three.Tick, Is.EqualTo(tick)); Assert.That(three.ProducedAmount(ResourceKind.Ingredients), Is.EqualTo(produced));
            Assert.That(three.Crops.Single().Stage, Is.EqualTo(stage));
            Assert.That(three.ResourceTotal(ResourceKind.Ingredients, false), Is.EqualTo(one.ResourceTotal(ResourceKind.Ingredients, false)));
            runner.SetPaused(true); var before = three.Tick; runner.AdvanceTime(100m); Assert.That(three.Tick, Is.EqualTo(before));
            yield return null;
        }
    }
}
