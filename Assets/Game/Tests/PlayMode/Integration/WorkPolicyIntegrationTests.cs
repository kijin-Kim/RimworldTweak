using System;
using System.Collections;
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
    public sealed class WorkPolicyIntegrationTests
    {
        private GameObject root;
        private SimulationRunner runner;
        private SettlementView view;
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private void Setup()
        {
            root = new GameObject("Work policy integration"); runner = root.AddComponent<SimulationRunner>(); runner.enabled = false;
            runner.Initialize(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(), new[] { new ColonistSeed(1, P(1, 1)), new ColonistSeed(2, P(1, 2)) },
                new[] { new ResourceSeed(101, ResourceKind.Wood, 20, P(1, 1)) }, needsEnabled: false,
                gatherables: new[] { new GatherSeed(201, GatherKind.Tree, P(2, 1)) }));
            runner.SetPaused(true); view = root.AddComponent<SettlementView>(); view.Bind(runner); view.SelectColonist(1);
        }
        [UnityTearDown]
        public IEnumerator Cleanup() { if (root != null) UnityEngine.Object.Destroy(root); yield return null; }
        private static string Label(Button button) => button.GetComponentInChildren<Text>().text;

        [UnityTest]
        public IEnumerator PausedPriorityButtonsCycleAllCategoriesAndRemainPawnSpecific()
        {
            Setup(); var w = runner.Session.World; var panel = view.WorkPolicyPanel;
            Assert.That(panel.PriorityButtons.Count, Is.EqualTo(5));
            foreach (WorkCategory category in Enum.GetValues(typeof(WorkCategory)))
            {
                var button = panel.PriorityButtons[(int)category]; var policy = w.WorkPolicies.Single(p => p.PawnId == 1);
                var initial = policy.GetPriority(category);
                for (var count = 0; count < 4; count++)
                {
                    var next = (policy.GetPriority(category) + 1) % 4; button.onClick.Invoke();
                    Assert.That(policy.GetPriority(category), Is.EqualTo(next));
                    Assert.That(Label(button), Does.Contain(next == 0 ? "끔" : next.ToString()));
                    Assert.That(w.Tick, Is.Zero);
                }
                Assert.That(policy.GetPriority(category), Is.EqualTo(initial));
            }
            panel.PriorityButtons[(int)WorkCategory.Farm].onClick.Invoke();
            Assert.That(Label(panel.PriorityButtons[(int)WorkCategory.Farm]), Does.Contain("3"));
            view.SelectColonist(2);
            Assert.That(Label(panel.PriorityButtons[(int)WorkCategory.Farm]), Does.Contain("2"));
            Assert.That(panel.InfoText.text, Does.Contain("#2")); Assert.That(w.Tick, Is.Zero); yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingSelectedWorkersCategoryUpdatesClaimAndUiWhilePaused()
        {
            Setup(); var w = runner.Session.World;
            runner.SetWorkPriority(2, WorkCategory.Gather, 0); runner.DesignateGather(201, true); runner.Session.Step(8); view.RefreshNow();
            var node = w.Gatherables[0]; Assert.That(node.OwnerId, Is.EqualTo(1)); var progress = node.Progress; var action = w.Colonists[0].ActionId;
            var button = view.WorkPolicyPanel.PriorityButtons[(int)WorkCategory.Gather];
            button.onClick.Invoke(); Assert.That(w.Colonists[0].ActionId, Is.EqualTo(action)); Assert.That(Label(button), Does.Contain("3"));
            button.onClick.Invoke(); Assert.That(Label(button), Does.Contain("끔"));
            Assert.That(node.OwnerId, Is.Null); Assert.That(node.Progress, Is.EqualTo(progress)); Assert.That(w.Colonists[0].IsWorking, Is.False);
            Assert.That(view.ClaimText.text, Does.Contain("없음")); Assert.That(w.Tick, Is.EqualTo(8));
            runner.AdvanceTime(20m); Assert.That(w.Tick, Is.EqualTo(8)); yield return null;
        }

        [UnityTest]
        public IEnumerator ReenablingPausedWorkClearsDisabledReasonBeforeAnyNewAction()
        {
            Setup(); var w = runner.Session.World;
            runner.SetWorkPriority(2, WorkCategory.Gather, 0); runner.DesignateGather(201, true); runner.Session.Step(8); view.RefreshNow();
            var tick = w.Tick; var node = w.Gatherables[0]; Assert.That(node.OwnerId, Is.EqualTo(1));
            var button = view.WorkPolicyPanel.PriorityButtons[(int)WorkCategory.Gather];
            button.onClick.Invoke(); button.onClick.Invoke();
            Assert.That(Label(button), Does.Contain("끔")); Assert.That(node.OwnerId, Is.Null);
            button.onClick.Invoke();
            Assert.That(Label(button), Does.Contain("1"));
            Assert.That(view.WorkPolicyPanel.InfoText.text, Does.Not.Contain("개인 작업 끔"));
            Assert.That(w.Colonists[0].WorkReason, Is.Not.EqualTo(WorkReasonCode.Disabled.ToString()));
            Assert.That(w.Tick, Is.EqualTo(tick)); Assert.That(node.OwnerId, Is.Null); Assert.That(w.Colonists[0].IsWorking, Is.False);
            runner.StepOnce(); Assert.That(node.OwnerId, Is.EqualTo(1)); yield return null;
        }

        [UnityTest]
        public IEnumerator ResetRestoresDefaultPrioritiesAndPanelBindsNewPolicy()
        {
            Setup(); var w = runner.Session.World;
            foreach (WorkCategory category in Enum.GetValues(typeof(WorkCategory))) runner.SetWorkPriority(1, category, 0);
            view.RefreshNow(); Assert.That(Label(view.WorkPolicyPanel.PriorityButtons[0]), Does.Contain("끔"));
            runner.ResetWorld(); view.RefreshNow(); view.SelectColonist(1);
            Assert.That(runner.Session.World, Is.Not.SameAs(w));
            foreach (WorkCategory category in Enum.GetValues(typeof(WorkCategory)))
            {
                var expected = category == WorkCategory.Haul ? 1 : 2;
                Assert.That(runner.Session.World.WorkPolicies.Single(p => p.PawnId == 1).GetPriority(category), Is.EqualTo(expected));
                Assert.That(Label(view.WorkPolicyPanel.PriorityButtons[(int)category]), Does.Contain(expected.ToString()));
            }
            view.WorkPolicyPanel.PriorityButtons[(int)WorkCategory.Cook].onClick.Invoke();
            Assert.That(runner.Session.World.WorkPolicies.Single(p => p.PawnId == 1).GetPriority(WorkCategory.Cook), Is.EqualTo(3));
            Assert.That(w.WorkPolicies.Single(p => p.PawnId == 1).GetPriority(WorkCategory.Cook), Is.Zero); yield return null;
        }
    }
}
