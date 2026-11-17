using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Presentation;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RimworldTweak.Tests.PlayMode
{
    public sealed class DemoIntegrationTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private string manualDirectory;

        private SimulationRunner Runner(ScenarioDefinition scenario = null)
        {
            var go = new GameObject("TestRunner");
            owned.Add(go);
            var runner = go.AddComponent<SimulationRunner>();
            runner.Initialize(scenario ?? SandboxScenario.Create());
            runner.SetPaused(true);
            return runner;
        }

        private SettlementView View(SimulationRunner runner)
        {
            var go = new GameObject("TestView");
            owned.Add(go);
            var view = go.AddComponent<SettlementView>();
            view.Bind(runner);
            view.RefreshNow();
            return view;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var go in owned) if (go != null) UnityEngine.Object.Destroy(go);
            owned.Clear();
            yield return null;
            var allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TestResults","ManualStartup"))+Path.DirectorySeparatorChar;
            if(manualDirectory!=null&&manualDirectory.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(manualDirectory))Directory.Delete(manualDirectory,true);
            manualDirectory=null;
        }

        [UnityTest]
        public IEnumerator InteractiveStartupUsesExplicitSaveRootAndRejectsMissingPath()
        {
            var runner=Runner();runner.enabled=false;
            var view=View(runner);
            var go=new GameObject("Manual startup test");owned.Add(go);
            var bootstrap=go.AddComponent<RimworldTweak.Demo.DemoBootstrap>();bootstrap.enabled=false;
            bootstrap.Runner=runner;bootstrap.View=view;
            foreach(var args in new[]{new[]{"--storage-root"},new[]{"--storage-root"," "},new[]{"--storage-root","--other-option"}})
            {
                Assert.Throws<ArgumentException>(()=>bootstrap.StartInteractive(args));
                Assert.That(runner.Slots,Is.Null,"Invalid explicit storage must not fall back to personal saves.");
                Assert.That(go.GetComponent<SettlementShell>(),Is.Null);
            }
            manualDirectory=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TestResults","ManualStartup","경로 공백 "+Guid.NewGuid().ToString("N")));
            bootstrap.StartInteractive(new[]{"game.exe","--storage-root",manualDirectory});
            var shell=go.GetComponent<SettlementShell>();Assert.That(shell,Is.Not.Null);
            Assert.That(view.gameObject.activeSelf,Is.False);Assert.That(shell.IsMenuOpen,Is.True);
            Assert.That(runner.Slots.SaveDirectory,Is.EqualTo(Path.Combine(manualDirectory,"Saves")));
            shell.Buttons["new"].onClick.Invoke();runner.SetPaused(true);runner.Session.Step(3);
            shell.ShowSaveMenu();shell.SaveNameInput.text="수동 확인";shell.Buttons["save.new"].onClick.Invoke();
            Assert.That(runner.ActiveSlotId,Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(manualDirectory,"Saves",runner.ActiveSlotId+".save.json")),Is.True);
            var settings=new UserSettingsStore(Path.Combine(manualDirectory,"Settings","user-settings.json")).Load();
            Assert.That(settings.Success,Is.True);Assert.That(settings.Settings.RecentSlotId,Is.EqualTo(runner.ActiveSlotId));
            Assert.That(new SaveRepository(Path.Combine(manualDirectory,"Saves")).Load(runner.ActiveSlotId).Slot.Tick,Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PausedRunnerStepsExactlyOneHundredAndResetPreservesPause()
        {
            var runner = Runner();
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(runner.Session.World.Tick, Is.Zero);
            for (var i = 0; i < 100; i++) runner.StepOnce();
            Assert.That(runner.Session.World.Tick, Is.EqualTo(100L));
            var previous = runner.Session.World;
            runner.ResetWorld();
            Assert.That(runner.IsPaused, Is.True);
            Assert.That(runner.Session.ResetCount, Is.EqualTo(1));
            Assert.That(runner.Session.World.Tick, Is.Zero);
            Assert.That(runner.Session.World, Is.Not.SameAs(previous));
            Assert.That(previous.Tick, Is.EqualTo(100L));
            yield return null;
            Assert.That(runner.Session.World.Tick, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AutomaticRunnerAdvancesAndPauseStopsFurtherProgress()
        {
            var runner = Runner();
            runner.SetPaused(false);
            runner.StepOnce();
            Assert.That(runner.Session.World.Tick, Is.Zero, "Manual step only applies while paused.");
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(runner.Session.World.Tick, Is.GreaterThan(0L));
            runner.SetPaused(true);
            var stopped = runner.Session.World.Tick;
            var pending = runner.Session.PendingSeconds;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(runner.Session.World.Tick, Is.EqualTo(stopped));
            Assert.That(runner.Session.PendingSeconds, Is.EqualTo(pending));
        }

        [UnityTest]
        public IEnumerator ViewObjectsMatchWorldCoordinatesBlockedTilesAndAmounts()
        {
            var runner = Runner();
            var view = View(runner);
            yield return null;
            AssertMarkers(view, runner.Session.World);
            runner.Session.Step(100);
            view.RefreshNow();
            Assert.That(view.TickText.text, Does.Contain("100"));
        }

        [UnityTest]
        public IEnumerator RepeatedBindAndResetKeepObjectCountsAndSingleButtonActions()
        {
            var runner = Runner();
            var view = View(runner);
            yield return null;
            var canvas = view.TickText.canvas;
            var objectCount = canvas.GetComponentsInChildren<Transform>(true).Length;
            var markerCount = canvas.GetComponentsInChildren<WorldMarker>(true).Length;
            for (var i = 0; i < 5; i++)
            {
                view.Bind(runner);
                view.StepButton.onClick.Invoke();
                Assert.That(runner.Session.World.Tick, Is.EqualTo(1L));
                view.ResetButton.onClick.Invoke();
                view.RefreshNow();
                yield return null;
                Assert.That(runner.Session.ResetCount, Is.EqualTo(i + 1));
                Assert.That(runner.Session.World.Tick, Is.Zero);
                Assert.That(canvas.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
                Assert.That(canvas.GetComponentsInChildren<WorldMarker>(true).Length, Is.EqualTo(markerCount));
                AssertMarkers(view, runner.Session.World);
            }
            view.PauseButton.onClick.Invoke();
            Assert.That(runner.IsPaused, Is.False);
            view.PauseButton.onClick.Invoke();
            Assert.That(runner.IsPaused, Is.True);
        }

        [UnityTest]
        public IEnumerator BindingAnotherRunnerReplacesDataAndButtonsTargetCurrentRunner()
        {
            var first = Runner();
            var second = Runner(new ScenarioDefinition(3, 2,
                new[] { new GridPosition(2, 1) },
                new[] { new ColonistSeed(71, new GridPosition(0, 1)) },
                new[] { new ResourceSeed(82, ResourceKind.Meal, 7, new GridPosition(1, 0)) }));
            var view = View(first);
            view.Bind(second);
            view.Bind(second);
            view.RefreshNow();
            yield return null;
            Assert.That(view.Runner, Is.SameAs(second));
            AssertMarkers(view, second.Session.World);
            Assert.That(view.TickText.canvas.GetComponentsInChildren<WorldMarker>(true).Length, Is.EqualTo(8));
            view.StepButton.onClick.Invoke();
            Assert.That(second.Session.World.Tick, Is.EqualTo(1L));
            Assert.That(first.Session.World.Tick, Is.Zero);
            view.ResetButton.onClick.Invoke();
            Assert.That(second.Session.ResetCount, Is.EqualTo(1));
            Assert.That(first.Session.ResetCount, Is.Zero);
            view.PauseButton.onClick.Invoke();
            Assert.That(second.IsPaused, Is.False);
            Assert.That(first.IsPaused, Is.True);
            second.SetPaused(true);
        }

        private static void AssertMarkers(SettlementView view, WorldState world)
        {
            Assert.That(view.Markers.Count, Is.EqualTo(world.Width * world.Height + world.Colonists.Count + world.Resources.Count));
            var tiles = view.Markers.Where(m => m.Kind == ViewKind.Tile).ToArray();
            Assert.That(tiles.Length, Is.EqualTo(world.Width * world.Height));
            Assert.That(tiles.Select(m => m.Position).Distinct().Count(), Is.EqualTo(tiles.Length));
            var origin = tiles.Single(m => m.Position == new GridPosition(0, 0));
            var cell = ((RectTransform)origin.transform).anchoredPosition * 2f;
            Assert.That(cell.x, Is.GreaterThan(0));
            Assert.That(cell.y, Is.GreaterThan(0));
            foreach (var marker in view.Markers)
            {
                var image = marker.GetComponent<Image>();
                Assert.That(image, Is.Not.Null);
                if(marker.Kind==ViewKind.Tile) Assert.That(image.enabled, Is.True);
                else { var symbol=marker.GetComponentInChildren<MapSymbolGraphic>(); Assert.That(symbol, Is.Not.Null); Assert.That(symbol.enabled, Is.True); }
                Assert.That(marker.gameObject.activeInHierarchy, Is.True);
                Assert.That(image.color.a, Is.GreaterThan(0f));
                var rect = marker.GetComponent<RectTransform>();
                Assert.That(rect, Is.Not.Null);
                Assert.That(rect.sizeDelta.x, Is.GreaterThan(0f));
                Assert.That(rect.sizeDelta.y, Is.GreaterThan(0f));
                Assert.That(rect.anchoredPosition.x, Is.EqualTo((marker.Position.X + 0.5f) * cell.x).Within(0.01f));
                Assert.That(rect.anchoredPosition.y, Is.EqualTo((marker.Position.Y + 0.5f) * cell.y).Within(0.01f));
                if (marker.Kind == ViewKind.Tile)
                    Assert.That(marker.IsBlocked, Is.EqualTo(world.BlockedTiles.Contains(marker.Position)));
                else if (marker.Kind == ViewKind.Colonist)
                {
                    var colonist = world.Colonists.Single(c => c.Id == marker.EntityId);
                    Assert.That(marker.Position, Is.EqualTo(colonist.Position));
                    Assert.That(image.sprite, Is.Not.Null);
                }
                else
                {
                    var resource = world.Resources.Single(r => r.Id == marker.EntityId);
                    Assert.That(marker.Position, Is.EqualTo(resource.Position));
                    Assert.That(marker.Amount, Is.EqualTo(resource.Amount));
                    var label = marker.GetComponentInChildren<Text>();
                    Assert.That(label, Is.Not.Null);
                    Assert.That(label.text, Is.EqualTo(SettlementView.ResourceName(resource.Kind) + " ×" + resource.Amount));
                }
            }
            Assert.That(view.Markers.Where(m => m.Kind == ViewKind.Colonist).Select(m => m.EntityId),
                Is.EquivalentTo(world.Colonists.Select(c => c.Id)));
            Assert.That(view.Markers.Where(m => m.Kind == ViewKind.Resource).Select(m => m.EntityId),
                Is.EquivalentTo(world.Resources.Select(r => r.Id)));
            var blocked = tiles.FirstOrDefault(m => m.IsBlocked);
            var open = tiles.FirstOrDefault(m => !m.IsBlocked);
            if (blocked != null && open != null)
                Assert.That(blocked.GetComponent<Image>().color, Is.Not.EqualTo(open.GetComponent<Image>().color));
        }
    }
}

