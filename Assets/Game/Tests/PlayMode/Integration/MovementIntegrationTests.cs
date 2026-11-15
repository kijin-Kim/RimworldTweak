using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Presentation;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RimworldTweak.Tests.PlayMode
{
    public sealed class MovementIntegrationTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private SimulationRunner Runner()
        {
            var go = new GameObject("Movement test runner"); owned.Add(go);
            var runner = go.AddComponent<SimulationRunner>(); runner.Initialize(SandboxScenario.Create()); runner.enabled = false; runner.SetPaused(true); DraftMovementPawns(runner); return runner;
        }
        private static void DraftMovementPawns(SimulationRunner runner)
        { foreach(var pawn in runner.Session.World.Colonists) runner.Session.World.SetDrafted(pawn.Id,true); }
        private SettlementView View(SimulationRunner runner)
        {
            var go = new GameObject("Movement test view"); owned.Add(go);
            var view = go.AddComponent<SettlementView>(); view.Bind(runner); view.RefreshNow(); return view;
        }
        private EventSystem Events()
        {
            if (EventSystem.current != null) return EventSystem.current;
            var go = new GameObject("Movement test events"); owned.Add(go); return go.AddComponent<EventSystem>();
        }
        private static Vector2 ScreenPoint(RectTransform rect, Vector2 local)
            => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local));
        private static Vector2 CellPoint(SettlementView view, GridPosition p)
        {
            var rect = view.BoardRect.rect; var world = view.Runner.Session.World;
            return ScreenPoint(view.BoardRect, new Vector2(rect.xMin + rect.width * (p.X + 0.5f) / world.Width,
                rect.yMin + rect.height * (p.Y + 0.5f) / world.Height));
        }
        private static GameObject Click(EventSystem events, Vector2 point, PointerEventData.InputButton button)
        {
            var data = new PointerEventData(events) { position = point, button = button };
            var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "No actual GraphicRaycaster hit");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
            return hits[0].gameObject;
        }
        private static void SameMovement(SimulationRunner a, SimulationRunner b)
        {
            Assert.That(a.Session.World.Tick, Is.EqualTo(b.Session.World.Tick));
            Assert.That(a.Session.PendingSeconds, Is.EqualTo(b.Session.PendingSeconds));
            for (var i = 0; i < a.Session.World.Colonists.Count; i++)
            {
                var x = a.Session.World.Colonists[i]; var y = b.Session.World.Colonists[i];
                Assert.That((x.Position, x.Destination, x.MovementProgress, x.Status, x.HasPendingCommand),
                    Is.EqualTo((y.Position, y.Destination, y.MovementProgress, y.Status, y.HasPendingCommand)));
                Assert.That(x.Path, Is.EqualTo(y.Path));
            }
            Assert.That(a.Session.World.Resources.Select(r => r.Amount), Is.EqualTo(b.Session.World.Resources.Select(r => r.Amount)));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var go in owned) if (go != null) UnityEngine.Object.Destroy(go);
            owned.Clear(); yield return null;
        }

        [Test]
        public void OneAndThreeSpeedMatchAtIntermediateAndFinalGameTimes()
        {
            var a = Runner(); var b = Runner();
            a.MoveColonist(1, P(7, 7)); b.MoveColonist(1, P(7, 7)); a.SetSpeed(1); b.SetSpeed(3);
            a.AdvanceTime(0.9m); b.AdvanceTime(0.3m); SameMovement(a, b);
            Assert.That(a.Session.World.Tick, Is.EqualTo(9));
            a.AdvanceTime(2.1m); b.AdvanceTime(0.7m); SameMovement(a, b);
            Assert.That(a.Session.World.Tick, Is.EqualTo(30));
            Assert.That(a.Session.World.Colonists[0].Position, Is.Not.EqualTo(P(1, 1)));
        }

        [Test]
        public void PauseSpeedSwitchAndSingleStepPreserveFractionalGameTime()
        {
            var runner = Runner(); runner.MoveColonist(1, P(5, 1)); runner.SetSpeed(1);
            runner.AdvanceTime(0.05m); runner.SetPaused(true); runner.AdvanceTime(10m);
            Assert.That(runner.Session.World.Tick, Is.Zero); Assert.That(runner.Session.PendingSeconds, Is.EqualTo(0.05m));
            Assert.That(runner.Session.World.Colonists[0].HasPendingCommand, Is.True);
            runner.SetSpeed(3); runner.AdvanceTime(0.05m);
            Assert.That(runner.Session.World.Tick, Is.EqualTo(2)); Assert.That(runner.Session.PendingSeconds, Is.Zero);
            runner.AdvanceTime(0.01m); runner.SetPaused(true); runner.StepOnce();
            Assert.That(runner.Session.World.Tick, Is.EqualTo(3)); Assert.That(runner.Session.PendingSeconds, Is.EqualTo(0.03m));
            Assert.That(runner.SpeedMultiplier, Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.SetSpeed(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.AdvanceTime(-1m));
            runner.ResetWorld(); DraftMovementPawns(runner);
            Assert.That(runner.IsPaused, Is.True); Assert.That(runner.SpeedMultiplier, Is.EqualTo(3));
            Assert.That(runner.Session.World.Tick, Is.Zero); Assert.That(runner.Session.PendingSeconds, Is.Zero);
            Assert.That(runner.Session.World.PendingCommandCount, Is.Zero);
            runner.SetPaused(false); Assert.That(runner.SpeedMultiplier, Is.EqualTo(3));
            runner.StepOnce(); Assert.That(runner.Session.World.Tick, Is.Zero);
            runner.Initialize(SandboxScenario.Create()); Assert.That(runner.SpeedMultiplier, Is.EqualTo(1)); Assert.That(runner.IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator MapPointerSelectionAndMoveUpdateActualMarkerInSameWorld()
        {
            var runner = Runner(); var view = View(runner); var events = Events();
            yield return null; Canvas.ForceUpdateCanvases();
            var world = runner.Session.World; var marker = view.Markers.Single(m => m.Kind == ViewKind.Colonist && m.EntityId == 1);
            var initialPosition = ((RectTransform)marker.transform).anchoredPosition;
            Click(events, CellPoint(view, P(1, 1)), PointerEventData.InputButton.Left);
            Assert.That(view.SelectedColonistId, Is.EqualTo(1));
            Click(events, CellPoint(view, P(5, 1)), PointerEventData.InputButton.Right);
            Assert.That(world.Colonists[0].HasPendingCommand, Is.True); Assert.That(world.PendingCommandCount, Is.EqualTo(1));
            Assert.That(world.Colonists[0].Position, Is.EqualTo(P(1, 1)));
            for (var i = 0; i < 5; i++) view.StepButton.onClick.Invoke();
            view.RefreshNow(); yield return null;
            Assert.That(runner.Session.World, Is.SameAs(world)); Assert.That(marker.Position, Is.EqualTo(P(2, 1)));
            Assert.That(((RectTransform)marker.transform).anchoredPosition, Is.Not.EqualTo(initialPosition));
            Assert.That(marker.GetComponentInChildren<MapSymbolGraphic>().enabled, Is.True);
            Assert.That(view.Markers.Count, Is.EqualTo(69));
            Assert.That(view.SelectionText.text, Does.Contain("1"));
        }

        [UnityTest]
        public IEnumerator UiRightClickAndOutsideMapNeverQueueMovement()
        {
            var runner = Runner(); var view = View(runner); var events = Events();
            yield return null; Canvas.ForceUpdateCanvases(); view.SelectColonist(1);
            var buttonRect = (RectTransform)view.Speed3Button.transform;
            var hit = Click(events, ScreenPoint(buttonRect, buttonRect.rect.center), PointerEventData.InputButton.Right);
            Assert.That(hit.transform.IsChildOf(view.BoardRect), Is.False);
            Assert.That(runner.Session.World.PendingCommandCount, Is.Zero);
            Assert.That(view.HandleMapClick(P(-1, 0), true), Is.False);
            Assert.That(view.HandleMapClick(P(8, 0), true), Is.False);
            view.ClearSelection(); Assert.That(view.MoveSelected(P(5, 1)), Is.False);
            Assert.That(runner.Session.World.PendingCommandCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ScreenCoordinatesMapToCellsAcrossCanvasScalesAndRejectBounds()
        {
            var view = View(Runner()); var scaler = view.TickText.canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            foreach (var scale in new[] { 0.75f, 1.25f })
            {
                scaler.scaleFactor = scale; yield return null; Canvas.ForceUpdateCanvases();
                foreach (var point in new[] { P(0, 0), P(3, 4), P(7, 7) })
                {
                    Assert.That(view.TryScreenToGrid(CellPoint(view, point), out var actual), Is.True);
                    Assert.That(actual, Is.EqualTo(point));
                }
                var r = view.BoardRect.rect;
                Assert.That(view.TryScreenToGrid(ScreenPoint(view.BoardRect, new Vector2(r.xMin - 1, r.center.y)), out _), Is.False);
                Assert.That(view.TryScreenToGrid(ScreenPoint(view.BoardRect, new Vector2(r.xMax + 1, r.center.y)), out _), Is.False);
                Assert.That(view.TryScreenToGrid(ScreenPoint(view.BoardRect, new Vector2(r.center.x, r.yMax + 1)), out _), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ListSelectionShortcutsAndTextFocusUseOneCommandPath()
        {
            var runner = Runner(); var view = View(runner); var events = Events();
            yield return null;
            Assert.That(view.ColonistButtons.Count, Is.EqualTo(3));
            view.ColonistButtons[1].onClick.Invoke(); Assert.That(view.SelectedColonistId, Is.EqualTo(2));
            view.MoveSelected(P(6, 2)); Assert.That(runner.Session.World.PendingCommandCount, Is.EqualTo(1));
            Assert.That(view.ProcessShortcut(Key.Escape), Is.True); Assert.That(view.SelectedColonistId, Is.Null);
            Assert.That(runner.Session.World.PendingCommandCount, Is.EqualTo(1), "Deselection does not cancel movement");
            view.ProcessShortcut(Key.Digit3); Assert.That(runner.SpeedMultiplier, Is.EqualTo(3)); Assert.That(runner.IsPaused, Is.False);
            view.ProcessShortcut(Key.Space); Assert.That(runner.IsPaused, Is.True);
            view.ProcessShortcut(Key.Period); Assert.That(runner.Session.World.Tick, Is.EqualTo(1));
            view.SelectColonist(2); Assert.That(view.ProcessShortcut(Key.X), Is.False); view.ProcessShortcut(Key.R); runner.StepOnce();
            Assert.That(runner.Session.World.Colonists[1].IsDrafted, Is.False);
            Assert.That(runner.Session.World.PendingCommandCount, Is.Zero);
            view.ProcessShortcut(Key.R); Assert.That(runner.Session.World.Colonists[1].IsDrafted, Is.True);
            Assert.That(runner.Session.World.Colonists[1].Status, Is.EqualTo(MovementStatus.Idle));
            view.ProcessShortcut(Key.Digit1); Assert.That(runner.SpeedMultiplier, Is.EqualTo(1)); Assert.That(runner.IsPaused, Is.False);
            var inputGo = new GameObject("Text focus", typeof(RectTransform), typeof(InputField)); owned.Add(inputGo);
            events.SetSelectedGameObject(inputGo);
            var beforeTick = runner.Session.World.Tick;
            view.ProcessShortcut(Key.Space); view.ProcessShortcut(Key.Digit3); view.ProcessShortcut(Key.Period);
            Assert.That(runner.IsPaused, Is.False); Assert.That(runner.SpeedMultiplier, Is.EqualTo(1)); Assert.That(runner.Session.World.Tick, Is.EqualTo(beforeTick));
            events.SetSelectedGameObject(null);
        }

        [UnityTest]
        public IEnumerator RepeatedMovementResetClearsSelectionAndKeepsStableObjectsAndListeners()
        {
            var runner = Runner(); var view = View(runner); yield return null;
            var canvas = view.TickText.canvas;
            var initialCount = canvas.GetComponentsInChildren<Transform>(true).Length;
            for (var i = 0; i < 10; i++)
            {
                view.Bind(runner); view.SelectColonist(1); view.MoveSelected(P(5, 1));
                for (var j = 0; j < 7; j++) view.StepButton.onClick.Invoke();
                Assert.That(runner.Session.World.Tick, Is.EqualTo(7));
                view.ResetButton.onClick.Invoke(); view.RefreshNow(); yield return null;
                Assert.That(runner.Session.ResetCount, Is.EqualTo(i + 1)); Assert.That(view.SelectedColonistId, Is.Null);
                Assert.That(runner.Session.World.PendingCommandCount, Is.Zero); Assert.That(runner.Session.World.Colonists[0].Position, Is.EqualTo(P(1, 1)));
                Assert.That(canvas.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(initialCount));
                Assert.That(view.Markers.Count, Is.EqualTo(69));
            }
            view.Speed3Button.onClick.Invoke(); Assert.That(runner.SpeedMultiplier, Is.EqualTo(3));
            view.PauseButton.onClick.Invoke(); Assert.That(runner.IsPaused, Is.True);
            view.StepButton.onClick.Invoke(); Assert.That(runner.Session.World.Tick, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OverlapSelectsLowestIdAndListCanSelectOtherColonist()
        {
            var runner = Runner(); runner.MoveColonist(1, P(2, 1)); runner.Session.Step(5);
            var view = View(runner); yield return null;
            view.HandleMapClick(P(2, 1), false); Assert.That(view.SelectedColonistId, Is.EqualTo(1));
            view.ColonistButtons[1].onClick.Invoke(); Assert.That(view.SelectedColonistId, Is.EqualTo(2));
            view.MoveSelected(P(3, 3)); view.RefreshNow();
            Assert.That(runner.Session.World.LastCommandError, Is.EqualTo("BlockedDestination"));
            Assert.That(view.CommandFeedbackText.text, Does.Contain("Target is blocked"));
            var other = Runner(); view.Bind(other); Assert.That(view.SelectedColonistId, Is.Null);
        }
    }
}

