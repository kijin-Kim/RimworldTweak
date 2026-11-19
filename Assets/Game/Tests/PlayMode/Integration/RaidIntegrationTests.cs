using System.Collections;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Presentation;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace RimworldTweak.Tests.PlayMode
{
    public sealed class RaidIntegrationTests
    {
        [UnityTest]
        public IEnumerator DeveloperRaidStartsInPlaceAndAcceptsPausedOrders()
        {
            var root = new GameObject("Raid integration");
            try
            {
                var runner = root.AddComponent<SimulationRunner>();
                runner.enabled = false;
                var view = root.AddComponent<SettlementView>();
                view.Bind(runner);
                runner.SetPaused(true);
                var world = runner.Session.World;
                view.RefreshNow();
                Assert.That(view.RaidButton.gameObject.activeSelf, Is.False);
                Assert.That(runner.StartDebugRaid(), Is.False);
                view.DeveloperButton.onClick.Invoke();
                Assert.That(view.RaidButton.interactable, Is.True);
                view.RaidButton.onClick.Invoke();
                view.RaidButton.onClick.Invoke();
                Assert.That(runner.Session.World, Is.SameAs(world));
                Assert.That(world.Raiders.Count, Is.EqualTo(2));
                Assert.That(world.Raiders.All(r => r.Attack.TargetId.HasValue), Is.True);
                Assert.That(view.RaidStatusText.text, Does.Contain("일반 습격"));
                Assert.That(world.Tick, Is.Zero);
                view.DeveloperButton.onClick.Invoke();
                Assert.That(view.RaidButton.gameObject.activeSelf, Is.False);
                view.SelectColonist(1);
                Assert.That(view.ToggleDraftSelected(), Is.True);
                view.RefreshNow();
                Assert.That(view.AttackButton.interactable, Is.True);
                view.AttackButton.onClick.Invoke();
                var enemy = world.Raiders[0];
                Assert.That(view.HandleMapClick(enemy.Position, false), Is.True);
                Assert.That(world.Colonists[0].Attack.TargetId, Is.EqualTo(enemy.Id));
                Assert.That(enemy.Health, Is.EqualTo(50));
                Assert.That(world.Tick, Is.Zero);
                view.ResetButton.onClick.Invoke();
                Assert.That(runner.Session.World.RaidPhase, Is.EqualTo(RaidPhase.Idle));
                Assert.That(runner.Session.World.Raiders, Is.Empty);
                Assert.That(view.SelectedColonistId, Is.Null);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
