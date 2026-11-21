using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class LivingIntegrationTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private SimulationRunner Runner(ScenarioDefinition scenario = null)
        {
            var go = new GameObject("Living test runner"); owned.Add(go);
            var runner = go.AddComponent<SimulationRunner>();
            if (scenario != null) runner.Initialize(scenario);
            runner.enabled = false; runner.SetPaused(true); return runner;
        }
        private SettlementView View(SimulationRunner runner)
        {
            var go = new GameObject("Living test view"); owned.Add(go);
            var view = go.AddComponent<SettlementView>(); view.Bind(runner); view.RefreshNow(); return view;
        }
        private static ScenarioDefinition Fixture(bool sleeping = false, bool competition = false)
            => new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(),
                competition ? new[] { new ColonistSeed(1, P(1, 1), 3010, 9000), new ColonistSeed(2, P(1, 1), 3010, 9000) }
                : new[] { new ColonistSeed(1, P(1, 1), sleeping ? 9000 : 3010, sleeping ? 2005 : 9000) },
                new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(1, 1)) },
                new[] { new BedSeed(201, P(1, 1)) }, true);
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var go in owned) if (go != null) UnityEngine.Object.Destroy(go);
            owned.Clear(); yield return null;
        }

        [UnityTest]
        public IEnumerator DefaultRunnerShowsLivingNeedsStockAndTwoBeds()
        {
            var runner = Runner(); var view = View(runner); view.SelectColonist(1); view.RefreshNow();
            Assert.That(runner.Session.World.NeedsEnabled, Is.True);
            Assert.That(view.Markers.Count, Is.EqualTo(71));
            Assert.That(view.Markers.Count(m => m.Kind == ViewKind.Bed && m.gameObject.activeSelf), Is.EqualTo(2));
            Assert.That(view.NeedsText.text, Does.Contain("35.00").And.Contain("25.00"));
            Assert.That(view.ReservationText.text, Does.Contain("201").And.Contain("202"));
            foreach (var marker in view.Markers.Where(m => m.Kind == ViewKind.Resource))
            {
                var resource = runner.Session.World.Resources.Single(r => r.Id == marker.EntityId);
                Assert.That(marker.Amount, Is.EqualTo(resource.Amount));
                Assert.That(marker.GetComponentInChildren<Text>().text, Is.EqualTo(SettlementView.ResourceName(resource.Kind) + " ×" + resource.Amount));
            }
            runner.StepOnce(); view.RefreshNow();
            Assert.That(view.NeedsText.text, Does.Contain("34.90").And.Contain("24.95"));
            Assert.That(view.LifeText.text, Is.Not.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LastMealCompetitionShowsOneReservationThenHidesConsumedMarker()
        {
            var runner = Runner(Fixture(competition: true)); var view = View(runner); view.SelectColonist(1);
            var marker = view.Markers.Single(m => m.Kind == ViewKind.Resource); var initialCount = view.Markers.Count;
            runner.StepOnce(); view.RefreshNow();
            Assert.That(runner.Session.World.Resources[0].Reservations.Keys, Is.EqualTo(new[] { 1 }));
            Assert.That(view.ReservationText.text, Does.Contain("MEALS 1 / 1 / 0"));
            Assert.That(view.LifeText.text, Does.Contain("0 / 20"));
            runner.Session.Step(19); view.RefreshNow();
            Assert.That(marker.gameObject.activeSelf, Is.True); Assert.That(marker.Amount, Is.EqualTo(1));
            Assert.That(view.LifeText.text, Does.Contain("19 / 20"));
            runner.StepOnce(); view.RefreshNow();
            Assert.That(marker.Amount, Is.Zero); Assert.That(marker.gameObject.activeSelf, Is.False);
            Assert.That(view.Markers.Count, Is.EqualTo(initialCount));
            Assert.That(runner.Session.World.Colonists.Select(c => c.MealsEaten), Is.EqualTo(new[] { 1, 0 }));
            Assert.That(view.NeedsText.text, Does.Contain("88.00"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BedOccupancyAndDeletionRefreshActualMarkerAndOwnerText()
        {
            var runner = Runner(Fixture(sleeping: true)); var view = View(runner); view.SelectColonist(1);
            var marker = view.Markers.Single(m => m.Kind == ViewKind.Bed); var freeColor = marker.GetComponent<Image>().color;
            runner.StepOnce(); view.RefreshNow();
            Assert.That(view.ReservationText.text, Does.Contain("SLEEPING C1"));
            Assert.That(marker.GetComponent<Image>().color, Is.Not.EqualTo(freeColor));
            runner.Session.Step(140); view.RefreshNow();
            Assert.That(view.NeedsText.text, Does.Contain("90.00"));
            Assert.That(view.ReservationText.text, Does.Contain("FREE"));
            Assert.That(marker.GetComponent<Image>().color, Is.EqualTo(freeColor));
            runner.Session.World.QueueSetBedExists(201, false); runner.StepOnce(); view.RefreshNow();
            Assert.That(marker.gameObject.activeSelf, Is.False);
            runner.Session.World.QueueSetBedExists(201, true); runner.StepOnce(); view.RefreshNow();
            Assert.That(view.Markers.Single(m => m.Kind == ViewKind.Bed), Is.SameAs(marker));
            Assert.That(marker.gameObject.activeSelf, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActiveLifeRejectsMovementButSelectionAndTimeControlsRemainUsable()
        {
            var runner = Runner(Fixture()); var view = View(runner); view.SelectColonist(1);
            view.StepButton.onClick.Invoke(); view.RefreshNow();
            Assert.That(runner.Session.World.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.Eating));
            Assert.That(view.MoveSelected(P(6, 6)), Is.False);
            Assert.That(runner.Session.World.LastCommandError, Is.EqualTo("LifeBusy"));
            Assert.That(view.CommandFeedbackText.text, Is.Not.Empty);
            Assert.That(runner.Session.World.PendingCommandCount, Is.Zero);
            view.ClearSelection(); view.SelectColonist(1); Assert.That(view.SelectedColonistId, Is.EqualTo(1));
            view.Speed3Button.onClick.Invoke(); Assert.That(runner.SpeedMultiplier, Is.EqualTo(3));
            Assert.That(runner.IsPaused, Is.False); view.PauseButton.onClick.Invoke(); Assert.That(runner.IsPaused, Is.True);
            var progress = runner.Session.World.Colonists[0].ActionProgress;
            var nutrition = runner.Session.World.Colonists[0].Nutrition; var rest = runner.Session.World.Colonists[0].Rest;
            runner.AdvanceTime(1m); Assert.That(runner.Session.World.Colonists[0].ActionProgress, Is.EqualTo(progress));
            Assert.That(runner.Session.World.Colonists[0].Nutrition, Is.EqualTo(nutrition)); Assert.That(runner.Session.World.Colonists[0].Rest, Is.EqualTo(rest));
            view.StepButton.onClick.Invoke(); Assert.That(runner.Session.World.Colonists[0].ActionProgress, Is.EqualTo(progress + 1));
            view.PauseButton.onClick.Invoke(); runner.AdvanceTime(0.1m);
            Assert.That(runner.Session.World.Colonists[0].ActionProgress, Is.EqualTo(progress + 4));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LivingStateAtSameTickIsIdenticalAtOneAndThreeTimesSpeed()
        {
            var a = Runner(); var b = Runner(); a.SetPaused(false); b.SetPaused(false); b.SetSpeed(3);
            var previousTick = 0;
            foreach (var checkpoint in new[] { 66, 201, 300 })
            {
            // Include active movement/reservations, then reach the original 30s/10s endpoint.
            a.AdvanceTime((checkpoint - previousTick) / 10m); b.AdvanceTime((checkpoint - previousTick) / 30m);
            previousTick = checkpoint;
            Assert.That(a.Session.World.Tick, Is.EqualTo(checkpoint)); Assert.That(b.Session.World.Tick, Is.EqualTo(checkpoint));
            for (var i = 0; i < a.Session.World.Colonists.Count; i++)
            {
                var x = a.Session.World.Colonists[i]; var y = b.Session.World.Colonists[i];
                Assert.That((x.Position, x.Nutrition, x.Rest, x.LifeActivity, x.LifeTargetId, x.ActionProgress, x.MealsEaten, x.SleepsCompleted),
                    Is.EqualTo((y.Position, y.Nutrition, y.Rest, y.LifeActivity, y.LifeTargetId, y.ActionProgress, y.MealsEaten, y.SleepsCompleted)));
                Assert.That(x.Path, Is.EqualTo(y.Path));
                Assert.That((x.ActionId, x.HasSleepIntent), Is.EqualTo((y.ActionId, y.HasSleepIntent)));
                Assert.That((x.Destination, x.MovementProgress, x.MovementTicksPerTile), Is.EqualTo((y.Destination, y.MovementProgress, y.MovementTicksPerTile)));
            }
            Assert.That(a.Session.World.Resources.Select(r => (r.Amount, r.ReservedAmount)), Is.EqualTo(b.Session.World.Resources.Select(r => (r.Amount, r.ReservedAmount))));
            for (var i = 0; i < a.Session.World.Resources.Count; i++)
                Assert.That(a.Session.World.Resources[i].Reservations.OrderBy(pair => pair.Key), Is.EqualTo(b.Session.World.Resources[i].Reservations.OrderBy(pair => pair.Key)));
            Assert.That(a.Session.World.Beds.Select(r => (r.OwnerId, r.OwnerActionId, r.IsOccupied)), Is.EqualTo(b.Session.World.Beds.Select(r => (r.OwnerId, r.OwnerActionId, r.IsOccupied))));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TenResetsClearLifeStateWithoutGrowingUiOrMarkerObjects()
        {
            var runner = Runner(); var view = View(runner);
            var totalObjects = view.TickText.canvas.GetComponentsInChildren<Transform>(true).Length;
            for (var i = 0; i < 10; i++)
            {
                runner.Session.Step(i % 2 == 0 ? 65 : 200); view.SelectColonist(1); view.RefreshNow();
                Assert.That(runner.Session.World.Colonists.Any(c => c.LifeActivity != LifeActivity.Idle), Is.True);
                view.ResetButton.onClick.Invoke(); view.SelectColonist(1); view.RefreshNow();
                Assert.That(runner.Session.World.Tick, Is.Zero); Assert.That(view.Markers.Count, Is.EqualTo(71));
                Assert.That(view.TickText.canvas.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(totalObjects));
                Assert.That(view.NeedsText.text, Does.Contain("35.00").And.Contain("25.00"));
                Assert.That(runner.Session.World.Resources.Single(r => r.Kind == ResourceKind.Meal).Amount, Is.EqualTo(18));
                Assert.That(runner.Session.World.Resources.All(r => r.ReservedAmount == 0), Is.True);
                Assert.That(runner.Session.World.Beds.All(r => r.OwnerId == null && !r.IsOccupied), Is.True);
                Assert.That(view.Markers.All(m => m.gameObject.activeSelf), Is.True);
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator FoodAndBedFailureReasonsAreDistinctOnActualUi()
        {
            var messages = new[] { "먹을 수 있는 음식이 없습니다", "음식을 다른 정착민이 점유 중입니다", "음식까지 이동할 수 없습니다",
                "침대가 없어 바닥에서 쉽니다", "침대를 다른 정착민이 점유 중입니다", "침대까지 이동할 수 없습니다", "기아" };
            for (var kind = 0; kind < messages.Length; kind++)
            {
                var foodCase = kind < 3 || kind == 6;
                var competing = kind == 1 || kind == 4;
                var unreachable = kind == 2 || kind == 5;
                var people = new List<ColonistSeed> { new ColonistSeed(1, P(1, 1), kind == 6 ? 0 : foodCase ? 3010 : 9000, foodCase ? 9000 : 2005) };
                if (competing) people.Add(new ColonistSeed(2, P(1, 1), foodCase ? 3010 : 9000, foodCase ? 9000 : 2005));
                var food = foodCase && kind != 0 && kind != 6 ? new[] { new ResourceSeed(101, ResourceKind.Meal, 1, unreachable ? P(5, 1) : P(1, 1)) } : Array.Empty<ResourceSeed>();
                var beds = !foodCase && kind != 3 ? new[] { new BedSeed(201, unreachable ? P(5, 1) : P(1, 1)) } : Array.Empty<BedSeed>();
                var wall = unreachable ? Enumerable.Range(0, 8).Select(y => P(3, y)).ToArray() : Array.Empty<GridPosition>();
                var runner = Runner(new ScenarioDefinition(8, 8, wall, people, food, beds, true));
                var view = View(runner); view.SelectColonist(competing ? 2 : 1);
                runner.StepOnce(); view.RefreshNow();
                Assert.That(view.LifeText.text, Does.Contain(messages[kind]), "Failure reason case " + kind);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DraftWhilePausedReleasesReservationsAndAllowsPlayerMovement()
        {
            var runner = Runner(Fixture()); var view = View(runner); view.SelectColonist(1);
            runner.StepOnce(); var w = runner.Session.World;
            Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.Eating));
            view.DraftButton.onClick.Invoke();
            Assert.That(runner.IsPaused, Is.True); Assert.That(w.Tick, Is.EqualTo(1));
            Assert.That(w.Colonists[0].IsDrafted, Is.True); Assert.That(w.Resources[0].ReservedAmount, Is.Zero);
            Assert.That(view.LifeText.text, Does.Contain("소집")); Assert.That(view.DraftButton.interactable, Is.True);
            Assert.That(view.MoveSelected(P(2, 1)), Is.True); runner.Session.Step(5);
            Assert.That(w.Colonists[0].Position, Is.EqualTo(P(2, 1)));
            Assert.That(view.ProcessShortcut(UnityEngine.InputSystem.Key.X), Is.False);
            Assert.That(w.Colonists[0].IsDrafted, Is.True);
            Assert.That(view.ProcessShortcut(UnityEngine.InputSystem.Key.R), Is.True);
            Assert.That(w.Colonists[0].IsDrafted, Is.False); Assert.That(view.DraftButton.GetComponentInChildren<Text>().text, Is.EqualTo("소집 [R]"));
            runner.StepOnce(); Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.GoingToFood));
            view.DraftButton.onClick.Invoke(); view.ResetButton.onClick.Invoke();
            Assert.That(runner.Session.World.Colonists[0].IsDrafted, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RKeyboardPressAndHoldTogglesOnceAndXDoesNothing()
        {
            var runner = Runner(Fixture()); var view = View(runner); view.SelectColonist(1);
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var previousMode = UnityEngine.InputSystem.InputSystem.settings.updateMode;
            view.enabled = false;
            try
            {
                UnityEngine.InputSystem.InputSystem.settings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually;
                System.Action pump = () => { UnityEngine.InputSystem.InputSystem.Update(); keyboard.MakeCurrent(); view.SendMessage("Update"); };
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                pump();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.R));
                pump();
                Assert.That(keyboard.rKey.isPressed, Is.True);
                Assert.That(runner.Session.World.Colonists[0].IsDrafted, Is.True);
                pump(); pump();
                Assert.That(runner.Session.World.Colonists[0].IsDrafted, Is.True, "Holding R must not toggle repeatedly");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.X));
                pump(); Assert.That(keyboard.xKey.isPressed, Is.True);
                Assert.That(runner.Session.World.Colonists[0].IsDrafted, Is.True);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.R));
                pump();
                Assert.That(runner.Session.World.Colonists[0].IsDrafted, Is.False);
                Assert.That(view.DraftButton.GetComponentInChildren<Text>().text, Is.EqualTo("소집 [R]"));
                Assert.That(view.TickText.canvas.GetComponentsInChildren<Button>().Any(b => b.name.Contains("Stop") || b.name.Contains("Release")), Is.False);
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
                UnityEngine.InputSystem.InputSystem.settings.updateMode = previousMode;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator OwnershipUiDistinguishesApproachUseAndSpareStockFromAccess()
        {
            var runner = Runner(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(), new[] { new ColonistSeed(1, P(1, 1), 3010, 9000) }, new[] { new ResourceSeed(101, ResourceKind.Meal, 18, P(3, 1)) }, null, true));
            var view = View(runner); view.SelectColonist(1); runner.StepOnce(); view.RefreshNow();
            Assert.That(view.ReservationText.text, Does.Contain("MEALS 18 / 1 / 0").And.Contain("FOOD 101 APPROACH C1"));
            runner.Session.Step(9); view.RefreshNow();
            Assert.That(view.ReservationText.text, Does.Contain("FOOD 101 USING C1"));
            view.DraftButton.onClick.Invoke();
            Assert.That(view.ReservationText.text, Does.Contain("MEALS 18 / 0 / 18").And.Contain("FOOD 101 FREE"));
            var bedRunner = Runner(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(), new[] { new ColonistSeed(1, P(0, 0), 9000, 2005), new ColonistSeed(2, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(7, 7)) }, true));
            var bedView = View(bedRunner); bedRunner.Session.Step(41); bedView.SelectColonist(1);
            Assert.That(bedView.ReservationText.text, Does.Contain("BED 201  APPROACH C1"));
            bedView.DraftButton.onClick.Invoke(); Assert.That(bedView.ReservationText.text, Does.Contain("FREE"));
            bedRunner.StepOnce(); bedView.SelectColonist(2);
            Assert.That(bedView.ReservationText.text, Does.Contain("BED 201  APPROACH C2"));
            Assert.That(bedView.LifeText.text, Does.Contain("침대로 이동 중"));
            yield return null;
        }
    }
}
