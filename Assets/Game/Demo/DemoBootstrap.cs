using System;
using System.Collections;
using System.IO;
using System.Linq;
using RimworldTweak.Runtime;
using RimworldTweak.Presentation;
using RimworldTweak.Simulation;
using UnityEngine;

namespace RimworldTweak.Demo
{
    public sealed class DemoBootstrap : MonoBehaviour
    {
        public SimulationRunner Runner;
        public SettlementView View;
        private string output;
        private readonly SmokeReport report = new SmokeReport();

        [Serializable]
        private sealed class SmokeReport
        {
            public bool passed;
            public string error;
            public long automaticTicks;
            public long exactTicks;
            public int resets;
            public int markers;
            public int objectsBefore;
            public int objectsAfter;
            public string screenshot;
            public bool movementPassed, speedEquivalent, blockedResumed, resetStateCleared;
            public long arrivalTick, renderTick;
            public bool draftPassed, occupancyReassigned;
            public bool livingPassed, livingSpeedEquivalent, foodReservationObserved, bedOccupancyObserved;
            public int[] mealsPerColonist, sleepsPerColonist;
            public long starvationTicks;
            public int consumedMeals, livingMarkers, livingObjectsBefore, livingObjectsAfter;
        }

        private void Start()
        {
            View.Bind(Runner);
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "--smoke-output");
            if (index < 0) return;
            if (index + 1 >= args.Length) { Application.Quit(2); return; }
            Runner.Initialize(SandboxScenario.Create());
            View.Bind(Runner);
            output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(output);
            StartCoroutine(Smoke());
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private bool Check(Action action)
        {
            try { action(); return true; }
            catch (Exception exception)
            {
                report.error = exception.ToString();
                Finish(false);
                return false;
            }
        }

        private void CheckMarkers()
        {
            var world = Runner.Session.World;
            Require(View.Markers.Count == world.Width * world.Height + world.Colonists.Count + world.Resources.Count + world.Beds.Count, "Marker count");
            foreach (var colonist in world.Colonists)
                Require(View.Markers.Count(m => m.Kind == ViewKind.Colonist && m.EntityId == colonist.Id && m.Position == colonist.Position) == 1, "Colonist data");
            foreach (var resource in world.Resources)
                Require(View.Markers.Count(m => m.Kind == ViewKind.Resource && m.EntityId == resource.Id && m.Position == resource.Position && m.Amount == resource.Amount) == 1, "Resource data");
            Require(View.Markers.Count(m => m.Kind == ViewKind.Tile && m.IsBlocked) == world.BlockedTiles.Count, "Blocked tiles");
        }

        private IEnumerator Smoke()
        {
            yield return new WaitForSecondsRealtime(0.35f);
            if (!Check(() => {
                report.automaticTicks = Runner.Session.World.Tick;
                Require(report.automaticTicks > 0, "Automatic tick did not advance");
                Runner.SetPaused(true);
                Runner.ResetWorld();
                Runner.Session.AdvanceTime(10m);
                report.exactTicks = Runner.Session.World.Tick;
                Require(report.exactTicks == 100, "Ten seconds must be exactly 100 ticks");
                Runner.ResetWorld();
                View.RefreshNow();
                CheckMarkers();
                report.objectsBefore = View.TickText.canvas.GetComponentsInChildren<Transform>(true).Length;
            })) yield break;

            for (var i = 0; i < 10; i++)
            {
                if (!Check(() => {
                    var old = Runner.Session.World;
                    View.Bind(Runner);
                    View.StepButton.onClick.Invoke();
                    Require(old.Tick == 1, "Step listener duplicated");
                    var count = Runner.Session.ResetCount;
                    View.ResetButton.onClick.Invoke();
                    Require(Runner.Session.ResetCount == count + 1, "Reset listener duplicated");
                    Require(Runner.Session.World != old && old.Tick == 1 && Runner.Session.World.Tick == 0, "Reset world isolation");
                    Require(Runner.Session.PendingSeconds == 0m, "Reset remainder");
                    View.RefreshNow();
                    CheckMarkers();
                })) yield break;
                yield return null;
                if (!Check(() => Require(View.TickText.canvas.GetComponentsInChildren<Transform>(true).Length == report.objectsBefore, "Reset duplicated objects"))) yield break;
            }

            if (!Check(CheckMovement)) yield break;
            yield return null;
            if (!Check(() => {
                report.resets = Runner.Session.ResetCount;
                report.markers = View.Markers.Count;
                report.objectsAfter = View.TickText.canvas.GetComponentsInChildren<Transform>(true).Length;
                View.PauseButton.onClick.Invoke();
                Require(!Runner.IsPaused, "Pause listener duplicated");
                View.PauseButton.onClick.Invoke();
                Require(Runner.IsPaused, "Resume listener duplicated");
                View.SelectColonist(1);
                Require(View.MoveSelected(new GridPosition(5, 1)), "Final movement command");
                Runner.Session.Step(10);
                View.RefreshNow();
                CheckMarkers();
                report.renderTick = Runner.Session.World.Tick;
                report.screenshot = Path.Combine(output, "demo.png");
            })) yield break;
            if (!Check(CheckLiving)) yield break;
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!Check(() => ScreenCapture.CaptureScreenshot(report.screenshot))) yield break;
            var deadline = Time.realtimeSinceStartup + 10f;
            while (!File.Exists(report.screenshot) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!Check(() => Require(File.Exists(report.screenshot) && new FileInfo(report.screenshot).Length > 0, "Screenshot missing"))) yield break;
            Finish(true);
        }

        private string MovementSignature()
        {
            var world = Runner.Session.World;
            return world.Tick + ":" + Runner.Session.PendingSeconds + ":" + string.Join(";", world.Colonists.Select(c =>
                c.Id + ":" + c.Position + ":" + c.MovementProgress + ":" + c.Status + ":" + c.Destination + ":" + string.Join(",", c.Path)));
        }

        private void CheckMovement()
        {
            Runner.ResetWorld();
            View.RefreshNow();
            View.SelectColonist(1);
            Require(View.MoveSelected(new GridPosition(5, 1)), "Move command rejected");
            Require(Runner.Session.World.Colonists[0].HasPendingCommand, "Missing pending command");
            Runner.AdvanceTime(10m);
            Require(Runner.Session.World.Tick == 0, "Pause accumulated time");
            Runner.Session.Step(19);
            Require(Runner.Session.World.Colonists[0].Position != new GridPosition(5, 1), "Early arrival");
            Runner.StepOnce();
            Require(Runner.Session.World.Colonists[0].Position == new GridPosition(5, 1), "Expected arrival at tick20");
            report.arrivalTick = Runner.Session.World.Tick;
            View.RefreshNow();
            CheckMarkers();

            Runner.ResetWorld();
            Runner.MoveColonist(1, new GridPosition(5, 1));
            View.Speed1Button.onClick.Invoke();
            Runner.AdvanceTime(0.6m);
            var partial = MovementSignature();
            Runner.AdvanceTime(2.4m);
            var whole = MovementSignature();
            Runner.ResetWorld();
            Runner.MoveColonist(1, new GridPosition(5, 1));
            View.Speed3Button.onClick.Invoke();
            Runner.AdvanceTime(0.2m);
            Require(MovementSignature() == partial, "Partial speed mismatch");
            Runner.AdvanceTime(0.8m);
            Require(MovementSignature() == whole, "Arrival speed mismatch");
            report.speedEquivalent = true;
            Runner.SetPaused(true);

            Runner.ResetWorld();
            Runner.MoveColonist(1, new GridPosition(5, 1));
            Runner.Session.Step(2);
            for (var y = 0; y < 8; y++) Require(Runner.Session.World.QueueSetBlocked(new GridPosition(3, y), true), "Wall change rejected");
            Runner.StepOnce();
            Require(Runner.Session.World.Colonists[0].Status == MovementStatus.Blocked, "Did not block");
            var searches = Runner.Session.World.PathSearchCount;
            Runner.Session.Step(10);
            Require(Runner.Session.World.PathSearchCount == searches, "Repeated failed path searches");
            Require(Runner.Session.World.QueueSetBlocked(new GridPosition(3, 1), false), "Open corridor rejected");
            Runner.Session.Step(30);
            Require(Runner.Session.World.Colonists[0].Position == new GridPosition(5, 1), "Blocked move did not resume");
            report.blockedResumed = true;

            for (var repeat = 0; repeat < 10; repeat++)
            {
                Runner.ResetWorld();
                View.RefreshNow();
                View.SelectColonist(1);
                Require(View.MoveSelected(new GridPosition(5, 1)), "Reset cycle move");
                if (repeat % 3 != 0) Runner.StepOnce();
                if (repeat % 3 == 2)
                {
                    for (var y = 0; y < 8; y++) Runner.Session.World.QueueSetBlocked(new GridPosition(3, y), true);
                    Runner.StepOnce();
                    Require(Runner.Session.World.Colonists[0].Status == MovementStatus.Blocked, "Reset blocked setup");
                }
                View.RefreshNow();
                var previous = Runner.Session.World;
                var previousTick = previous.Tick;
                var resetCount = Runner.Session.ResetCount;
                View.ResetButton.onClick.Invoke();
                View.RefreshNow();
                Require(Runner.Session.ResetCount == resetCount + 1, "Reset listener duplicated after movement");
                Require(Runner.Session.World != previous && previous.Tick == previousTick, "Movement reset isolation");
                Require(Runner.IsPaused && Runner.SpeedMultiplier == 3, "Reset pause/speed policy");
                Require(Runner.Session.World.Tick == 0 && Runner.Session.PendingSeconds == 0m, "Reset clock");
                Require(Runner.Session.World.PendingCommandCount == 0 && View.SelectedColonistId == null, "Reset command/selection leak");
                Require(Runner.Session.World.Colonists.All(c => c.Path.Count == 0 && c.MovementProgress == 0 && !c.HasPendingCommand && c.Destination == null), "Reset movement residue");
                Require(View.TickText.canvas.GetComponentsInChildren<Transform>(true).Length == report.objectsBefore, "Movement reset duplicated UI objects");
                CheckMarkers();
            }
            report.resetStateCleared = true;
            report.movementPassed = true;
        }

        private string LivingSignature()
        {
            var world = Runner.Session.World;
            return MovementSignature() + ":" + string.Join(";", world.Colonists.Select(c =>
                c.ActionId + ":" + c.HasSleepIntent + ":" + c.Nutrition + ":" + c.Rest + ":" + c.LifeActivity + ":" + c.LifeTargetId + ":" + c.ActionProgress + ":" + c.MealsEaten + ":" + c.SleepsCompleted)) +
                ":" + string.Join(";", world.Resources.Select(r => r.OwnerActionId + ":" + r.IsInUse + ":" + r.Id + ":" + r.Amount + ":" + r.ReservedAmount + ":" + r.ConsumedAmount + ":" + string.Join(",", r.Reservations.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value)))) +
                ":" + string.Join(";", world.Beds.Select(b => b.OwnerActionId + ":" + b.Id + ":" + b.OwnerId + ":" + b.IsOccupied));
        }

        private void CheckLiving()
        {
            Runner.Initialize(LivingScenario.Create());
            Runner.SetPaused(true);
            View.RefreshNow();
            report.livingObjectsBefore = View.TickText.canvas.GetComponentsInChildren<Transform>(true).Length;
            Runner.Session.Step(65);
            report.foodReservationObserved = Runner.Session.World.Resources.Any(r => r.ReservedAmount > 0);
            Require(report.foodReservationObserved, "No food reservation during automatic life");
            Runner.Session.Step(135);
            report.bedOccupancyObserved = Runner.Session.World.Beds.Any(b => b.IsOccupied);
            Require(report.bedOccupancyObserved, "No automatic bed occupancy");
            Runner.Session.Step(2200);
            var world = Runner.Session.World;
            report.mealsPerColonist = world.Colonists.Select(c => c.MealsEaten).ToArray();
            report.sleepsPerColonist = world.Colonists.Select(c => c.SleepsCompleted).ToArray();
            report.starvationTicks = world.Colonists.Sum(c => c.StarvationTicks);
            report.consumedMeals = world.Resources.Where(r => r.Kind == ResourceKind.Meal).Sum(r => r.ConsumedAmount);
            Require(world.Tick == 2400, "Living integration duration");
            Require(report.mealsPerColonist.All(n => n > 0) && report.sleepsPerColonist.All(n => n > 0), "Every colonist must eat and finish sleeping");
            Require(report.starvationTicks == 0, "Supported living scenario starved");
            Require(report.consumedMeals == report.mealsPerColonist.Sum(), "Meal consumption duplicated");
            Require(world.Resources.Single(r => r.Kind == ResourceKind.Meal).Amount == 18 - report.consumedMeals, "Food conservation");
            Require(world.Resources.All(r => r.Amount >= r.ReservedAmount && r.ReservedAmount >= 0 && r.AvailableAmount == r.Amount - r.ReservedAmount), "Reservation bounds");
            Require(world.Beds.Where(b => b.OwnerId.HasValue).Select(b => b.OwnerId.Value).Distinct().Count() == world.Beds.Count(b => b.OwnerId.HasValue), "Bed owner duplicated");

            Runner.ResetWorld();
            Runner.SetSpeed(1);
            Runner.SetPaused(false);
            Runner.AdvanceTime(30m);
            Require(Runner.Session.World.Tick == 300, "Living normal speed must advance 300 ticks");
            var normal = LivingSignature();
            Runner.ResetWorld();
            Runner.SetSpeed(3);
            Runner.AdvanceTime(10m);
            Require(Runner.Session.World.Tick == 300 && LivingSignature() == normal, "Living speed mismatch");
            report.livingSpeedEquivalent = true;
            Runner.SetPaused(true);
            var checkpoints = new[] { 50L, 65L, 200L, 400L };
            for (var repeat = 0; repeat < 10; repeat++)
            {
                Runner.ResetWorld();
                Runner.Session.Step(checkpoints[repeat % checkpoints.Length]);
                View.RefreshNow();
                View.SelectColonist(1);
                var previous = Runner.Session.World;
                var oldTick = previous.Tick;
                var count = Runner.Session.ResetCount;
                View.ResetButton.onClick.Invoke();
                View.RefreshNow();
                Require(Runner.Session.ResetCount == count + 1 && Runner.Session.World != previous && previous.Tick == oldTick, "Living reset listener or isolation");
                Require(Runner.IsPaused && Runner.SpeedMultiplier == 3 && Runner.Session.PendingSeconds == 0m, "Living reset time policy");
                Require(Runner.Session.World.Tick == 0 && Runner.Session.World.PendingCommandCount == 0 && View.SelectedColonistId == null, "Living reset residues");
                Require(Runner.Session.World.Colonists.All(c => c.Nutrition == 3500 && c.Rest == 2500 && c.LifeActivity == LifeActivity.Idle && c.ActionProgress == 0 && !c.HasSleepIntent && c.ActionId == 0), "Reset needs/state");
                Require(Runner.Session.World.Resources.All(r => r.ReservedAmount == 0 && r.ConsumedAmount == 0) && Runner.Session.World.Beds.All(b => b.OwnerId == null && !b.IsOccupied), "Reset reservations");
                Require(Runner.Session.World.Resources.Single(r => r.Kind == ResourceKind.Meal).Amount == 18, "Reset food amount");
                Require(View.TickText.canvas.GetComponentsInChildren<Transform>(true).Length == report.livingObjectsBefore, "Living reset duplicated UI");
            }
            Runner.Session.Step(200);
            View.RefreshNow();
            View.SelectColonist(1);
            CheckMarkers();
            foreach (var bed in Runner.Session.World.Beds)
                Require(View.Markers.Count(m => m.Kind == ViewKind.Bed && m.EntityId == bed.Id && m.Position == bed.Position) == 1, "Bed marker data");
            Require(Runner.Session.World.Colonists[0].LifeActivity == LifeActivity.SleepingInBed, "Draft fixture must be sleeping");
            var releasedBed = Runner.Session.World.Beds.Single(b => b.OwnerId == 1);
            var floorSleeper = Runner.Session.World.Colonists.Single(c => c.LifeActivity == LifeActivity.SleepingOnFloor);
            View.DraftButton.onClick.Invoke();
            Require(Runner.IsPaused && Runner.Session.World.Colonists[0].IsDrafted && Runner.Session.World.Beds.All(b => b.OwnerId != 1), "Draft must immediately release bed while paused");
            Require(View.MoveSelected(new GridPosition(5, 4)), "Drafted move refused");
            Runner.Session.Step(5);
            Require(releasedBed.OwnerId == floorSleeper.Id && floorSleeper.HasSleepIntent, "Freed bed was not acquired by floor sleeper");
            report.occupancyReassigned = true;
            Require(Runner.Session.World.Colonists[0].Position == new GridPosition(5, 4) && Runner.Session.World.Colonists[0].LifeActivity == LifeActivity.Idle, "Automatic life overrode player");
            View.DraftButton.onClick.Invoke();
            Require(!Runner.Session.World.Colonists[0].IsDrafted, "Release failed");
            report.draftPassed = true;
            Runner.ResetWorld(); Runner.Session.Step(200); View.RefreshNow(); View.SelectColonist(1);
            View.DraftButton.onClick.Invoke();
            Runner.Session.Step(1); View.RefreshNow(); View.SelectColonist(3);
            report.renderTick = Runner.Session.World.Tick;
            report.livingMarkers = View.Markers.Count;
            report.livingObjectsAfter = View.TickText.canvas.GetComponentsInChildren<Transform>(true).Length;
            Require(report.livingObjectsAfter == report.livingObjectsBefore, "Living UI count after progress");
            report.livingPassed = true;
        }

        private void Finish(bool passed)
        {
            report.passed = passed;
            File.WriteAllText(Path.Combine(output, "smoke.json"), JsonUtility.ToJson(report, true));
            Debug.Log("Demo smoke passed=" + passed);
            Application.Quit(passed ? 0 : 1);
        }
    }
}
