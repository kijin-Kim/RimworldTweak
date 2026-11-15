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
    public sealed class WorkIntegrationTests
    {
        private GameObject root;
        private SimulationRunner runner;
        private SettlementView view;
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private void Setup()
        {
            root = new GameObject("Work integration"); runner = root.AddComponent<SimulationRunner>();
            runner.Initialize(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(), new[] { new ColonistSeed(1, P(1, 1)) },
                Array.Empty<ResourceSeed>(), needsEnabled: true, gatherables: new[] { new GatherSeed(101, GatherKind.Tree, P(1, 1)) }));
            runner.enabled = false; runner.SetPaused(true); view = root.AddComponent<SettlementView>(); view.Bind(runner);
        }
        [UnityTearDown]
        public IEnumerator Cleanup() { if (root != null) UnityEngine.Object.Destroy(root); yield return null; }

        [UnityTest]
        public IEnumerator ToolsQueueDesignationAndRectangleThenShowDynamicMaterials()
        {
            Setup(); view.GatherButton.onClick.Invoke(); Assert.That(view.ActiveWorkTool, Is.EqualTo(SettlementView.WorkTool.Gather));
            view.HandleMapClick(P(1, 1), false);
            Assert.That(runner.Session.World.Gatherables[0].Designated, Is.True);
            Assert.That(view.WorkInfoText.text, Does.Contain("설정 즉시 적용"));
            view.StorageButton.onClick.Invoke(); view.HandleMapClick(P(3, 1), false);
            Assert.That(runner.Session.World.PendingCommandCount, Is.Zero);
            view.HandleMapHover(P(4, 1)); view.HandleMapClick(P(4, 1), false);
            runner.StepOnce(); view.RefreshNow();
            Assert.That(runner.Session.World.StorageZones[0].Slots.Count, Is.EqualTo(2));
            foreach (var marker in view.BoardRect.GetComponentsInChildren<RectTransform>().Where(r => r.name == "Work marker"))
            {
                Assert.That(marker.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(marker.anchoredPosition.x - marker.sizeDelta.x / 2, Is.GreaterThanOrEqualTo(0));
                Assert.That(marker.anchoredPosition.y + marker.sizeDelta.y / 2, Is.LessThanOrEqualTo(view.BoardRect.rect.height));
            }
            runner.Session.Step(100); view.RefreshNow();
            Assert.That(runner.Session.World.ResourceTotal(ResourceKind.Wood, true), Is.EqualTo(10));
            Assert.That(view.StockTotalsText.text, Does.Contain("보관 10 / 총량 10"));
            Assert.That(view.Markers.Count(m => m.Kind == ViewKind.Resource && m.gameObject.activeSelf && m.Amount == 10), Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EscapeAndRightClickCancelToolWithoutMovingAndFiltersPreserveStock()
        {
            Setup(); view.SelectColonist(1); view.StorageButton.onClick.Invoke(); view.HandleMapClick(P(3, 1), false);
            Assert.That(view.ProcessShortcut(Key.Escape), Is.True); Assert.That(view.StorageFirstCorner, Is.Null);
            Assert.That(runner.Session.World.PendingCommandCount, Is.Zero);
            view.GatherButton.onClick.Invoke(); view.HandleMapClick(P(4, 1), true);
            Assert.That(runner.Session.World.Colonists[0].HasPendingCommand, Is.False);
            runner.CreateStorage(P(3, 1), P(3, 1)); runner.StepOnce(); view.RefreshNow(); view.HandleMapClick(P(3, 1), false);
            Assert.That(view.SelectedObjectId, Is.EqualTo(runner.Session.World.StorageZones[0].Id));
            view.FilterButtons[0].onClick.Invoke(); runner.StepOnce(); view.RefreshNow();
            Assert.That(runner.Session.World.StorageZones[0].Allows(ResourceKind.Wood), Is.False);
            view.DeleteStorageButton.onClick.Invoke(); runner.StepOnce(); view.RefreshNow();
            Assert.That(runner.Session.World.StorageZones[0].Exists, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DraftDuringWorkAndRepeatedResetClearOrdersAndUiState()
        {
            Setup(); view.GatherButton.onClick.Invoke(); view.HandleMapClick(P(1, 1), false); runner.StepOnce();
            view.SelectColonist(1); view.ProcessShortcut(Key.R);
            Assert.That(runner.Session.World.Gatherables[0].OwnerId, Is.Null);
            Assert.That(runner.Session.World.Gatherables[0].Designated, Is.True);
            var objects = root.GetComponentsInChildren<Transform>(true).Length;
            for (var i = 0; i < 10; i++)
            {
                view.StorageButton.onClick.Invoke(); view.HandleMapClick(P(3, 1), false); view.ResetButton.onClick.Invoke();
                Assert.That(view.ActiveWorkTool, Is.EqualTo(SettlementView.WorkTool.None));
                Assert.That(view.StorageFirstCorner, Is.Null); Assert.That(view.SelectedObjectId, Is.Null);
                Assert.That(runner.Session.World.PendingCommandCount, Is.Zero);
                Assert.That(runner.Session.World.Gatherables[0].Designated, Is.False);
            }
            yield return null;
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
        }
    }
}
