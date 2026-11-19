using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimworldTweak.Presentation;
using RimworldTweak.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RimworldTweak.Demo
{
    // Observes the normal two-scene startup only when an isolated Player check is requested.
    public sealed class TitlePlayerCheck : MonoBehaviour
    {
        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public int width, height, scale;
            public long restoredTick;
            public string error, slot, firstScene;
            public List<string> checks = new List<string>();
        }

        private readonly Report report = new Report();
        private string output, storage;
        private TitleMenu title => FindFirstObjectByType<TitleMenu>();
        private SettlementShell shell => FindFirstObjectByType<SettlementShell>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "--title-check");
            if (index < 0) return;
            var root = Array.IndexOf(args, "--storage-root");
            if (index + 1 >= args.Length || root < 0 || root + 1 >= args.Length)
                throw new ArgumentException("Title check requires output and explicit --storage-root.");
            var output = Path.GetFullPath(args[index + 1]);
            var storage = Path.GetFullPath(args[root + 1]);
            if (!string.Equals(Path.Combine(output, "storage"), storage, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Title check needs its own storage directory under the run output.");
            var observer = new GameObject("Title Player check").AddComponent<TitlePlayerCheck>();
            DontDestroyOnLoad(observer.gameObject);
            observer.output = output;
            observer.storage = storage;
        }

        private IEnumerator Start()
        {
            // Flatten nested enumerators so failures during a scene wait are reported to the host.
            var routines = new Stack<IEnumerator>();
            routines.Push(Run());
            while (routines.Count > 0)
            {
                object current;
                try
                {
                    var top = routines.Peek();
                    if (!top.MoveNext()) { routines.Pop(); continue; }
                    current = top.Current;
                    if (current is IEnumerator nested && !(current is CustomYieldInstruction))
                    {
                        routines.Push(nested);
                        continue;
                    }
                }
                catch (Exception e)
                {
                    report.error = e.ToString();
                    break;
                }
                yield return current;
            }
            report.passed = string.IsNullOrEmpty(report.error);
            File.WriteAllText(Path.Combine(output, "title-result.json"), JsonUtility.ToJson(report, true));
            if (!report.passed) Debug.LogError(report.error);
            Application.Quit(report.passed ? 0 : 1);
        }

        private IEnumerator Run()
        {
            report.firstScene = SceneManager.GetActiveScene().name;
            Verify(report.firstScene == "Title", "ordinary startup scene");
            SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
            yield return Until(() => title && title.GetComponentInChildren<Button>() && SplashScreen.isFinished, "first title");
            var settings = new UserSettingsStore(Path.Combine(storage, "Settings", "user-settings.json")).Load();
            Verify(settings.Success, "isolated settings");
            report.scale = settings.Settings.UiScalePercent;
            report.width = Screen.width;
            report.height = Screen.height;
            yield return null;
            VerifyTitle("first entry");
            Verify(EventSystem.current.currentSelectedGameObject == TitleButton("new").gameObject, "keyboard focus on new");
            Verify(!TitleButton("continue").IsInteractable(), "empty continue disabled");
            Capture("title");
            PressTitle("load.menu");
            yield return null;
            VerifyTitle("empty list");
            Verify(HasText("저장된 정착지가 없어"), "empty list notice");
            Capture("empty-list");
            PressTitle("cancel");
            PressTitle("settings");
            PressTitle("scale." + report.scale);
            yield return null;
            VerifyTitle("settings");
            PressTitle("cancel");
            var newGame = TitleButton("new");
            Submit(newGame);
            if (newGame) Submit(newGame);
            yield return Until(() => shell && shell.HasSettlement, "new settlement");
            var firstRunner = shell.Runner;
            Verify(Count<SimulationRunner>() == 1 && Count<SettlementShell>() == 1 && Count<WorldMapView>() == 1 &&
                Count<TitleMenu>() == 0 && firstRunner.Session.World.Width == 250 && firstRunner.Session.World.Height == 250 &&
                firstRunner.Session.World.Colonists.Count == 3, "single new settlement after double submit");
            firstRunner.SetPaused(true);
            firstRunner.Session.Step(7);
            var expectedTick = firstRunner.Session.World.Tick;
            var original = firstRunner.Session;
            shell.ShowPauseMenu();
            PressPlay("main");
            PressPlay("cancel");
            Verify(shell.Runner.Session == original && SceneManager.GetActiveScene().name == "SimulationSandbox", "return cancelled");
            PressPlay("main");
            PressPlay("transition.save");
            shell.SaveNameInput.text = "";
            PressPlay("save.new");
            Verify(shell.Runner.Session == original && shell.SaveNameInput && !string.IsNullOrEmpty(firstRunner.SaveMessage) &&
                firstRunner.ActiveSlotId == null, "failed save preserves session");
            shell.SaveNameInput.text = "타이틀 복원 검사";
            PressPlay("save.new");
            yield return Until(() => title && title.GetComponentInChildren<Button>(), "return after save");
            yield return null;
            VerifyTitle("return after save");
            Verify(!firstRunner, "original runner unloaded");
            var repository = new SaveRepository(Path.Combine(storage, "Saves"));
            var slot = repository.ListSlotSummaries().Slots.Single();
            Verify(slot.IsValid && slot.Tick == expectedTick, "save on return");
            report.slot = slot.SlotId;
            PressTitle("load.menu");
            yield return null;
            VerifyTitle("save selection");
            Capture("saved-list");
            var path = Path.Combine(repository.SaveDirectory, slot.SlotId + ".save.json");
            var savedBytes = File.ReadAllBytes(path);
            File.WriteAllText(path, "{");
            PressTitle("load." + slot.SlotId);
            yield return Until(() => title && title.GetComponentsInChildren<Button>().Any(b =>
                b.name == "load." + slot.SlotId && !b.IsInteractable()) && HasText("저장 데이터"), "corruption error list");
            yield return null;
            VerifyTitle("failed restore");
            Capture("failed-list");
            File.WriteAllBytes(path, savedBytes);
            PressTitle("cancel");
            PressTitle("load.menu");
            PressTitle("load." + slot.SlotId);
            yield return Until(() => shell && shell.HasSettlement, "restored settlement");
            var restored = shell.Runner;
            report.restoredTick = restored.Session.World.Tick;
            Verify(restored.IsPaused && restored.Session.World.Tick == expectedTick && restored.ActiveSlotId == slot.SlotId &&
                restored.Session != original, "restored slot and tick");
            shell.ShowPauseMenu();
            PressPlay("main");
            PressPlay("transition.discard");
            yield return Until(() => title && title.GetComponentInChildren<Button>(), "return after discard");
            yield return null;
            VerifyTitle("return after discard");
            Verify(!restored, "restored session unloaded");
            PressTitle("continue");
            yield return Until(() => shell && shell.HasSettlement, "continue");
            Verify(shell.Runner.ActiveSlotId == slot.SlotId && shell.Runner.Session.World.Tick == expectedTick, "continue recent");
            shell.ShowPauseMenu();
            PressPlay("main");
            PressPlay("transition.discard");
            yield return Until(() => title && title.GetComponentInChildren<Button>(), "return for fresh game");
            PressTitle("new");
            yield return Until(() => shell && shell.HasSettlement, "fresh reentry");
            Verify(shell.Runner.ActiveSlotId == null && !shell.Runner.IsPaused && shell.Runner.Session != original &&
                Count<SimulationRunner>() == 1 && Count<WorldMapInput>() == 1, "fresh second settlement");
        }

        private void VerifyTitle(string stage)
        {
            Verify(SceneManager.GetActiveScene().name == "Title" && Count<TitleMenu>() == 1 && Count<SimulationRunner>() == 0 &&
                Count<SettlementShell>() == 0 && Count<SettlementView>() == 0 && Count<WorldMapView>() == 0 &&
                Count<WorldMapInput>() == 0 && Count<EventSystem>() == 1, "isolated title: " + stage);
        }

        private static int Count<T>() where T : UnityEngine.Object =>
            FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        private bool HasText(string value) => title.GetComponentsInChildren<Text>().Any(t => t.text.Contains(value));
        private Button TitleButton(string name) => title.GetComponentsInChildren<Button>().Single(b => b.name == name);
        private void PressTitle(string name) => Submit(TitleButton(name));
        private void PressPlay(string name) => Submit(shell.Buttons[name]);
        private static void Submit(Button target) => ExecuteEvents.Execute(target.gameObject,
            new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        private void Verify(bool success, string label)
        {
            if (!success) throw new InvalidOperationException(label);
            report.checks.Add(label);
        }

        private static IEnumerator Until(Func<bool> condition, string label)
        {
            var deadline = Time.realtimeSinceStartup + 40;
            while (!condition())
            {
                if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException(label);
                yield return null;
            }
        }

        private static Rect ScreenBounds(RectTransform transform)
        {
            var corners = new Vector3[4];
            transform.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Fits(Rect parent, Rect child) =>
            child.width > 0 && child.height > 0 && child.xMin >= parent.xMin - 1 &&
            child.yMin >= parent.yMin - 1 && child.xMax <= parent.xMax + 1 && child.yMax <= parent.yMax + 1;

        private List<string> CheckLayout(string name)
        {
            var issues = new List<string>();
            var bounds = new Dictionary<Button, Rect>();
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            var card = title.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Title panel");
            var cardRect = ScreenBounds(card);
            var view = title.GetComponentInChildren<ScrollRect>().viewport;
            var viewRect = ScreenBounds(view);
            foreach (var button in title.GetComponentsInChildren<Button>())
            {
                var rect = ScreenBounds((RectTransform)button.transform);
                var clip = button.transform.IsChildOf(view) ? viewRect : cardRect;
                var overlaps = bounds.Where(pair => pair.Value.Overlaps(rect))
                    .Select(pair => pair.Key.name + " " + pair.Value).ToArray();
                var details = name + " button " + button.name + " " + rect + "; screen " + screen +
                    "; card " + cardRect + "; viewport " + viewRect + "; overlaps [" + string.Join(", ", overlaps) + "]";
                if (!Fits(screen, rect) || !Fits(clip, rect) || overlaps.Length > 0) issues.Add(details);
                else report.checks.Add(details);
                bounds.Add(button, rect);
            }
            foreach (var label in title.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrWhiteSpace(label.text)) continue;
                var rect = ScreenBounds(label.rectTransform);
                var clip = label.transform.IsChildOf(view) ? viewRect : cardRect;
                var details = name + " text " + label.name + " (" + label.text + ") " + rect +
                    "; allocated height " + label.rectTransform.rect.height + "; required " + label.preferredHeight +
                    "; viewport " + viewRect + "; card " + cardRect;
                if (label.rectTransform.rect.height + 2 < label.preferredHeight || !Fits(clip, rect)) issues.Add(details);
                else report.checks.Add(details);
            }
            return issues;
        }

        private void Capture(string name)
        {
            var canvas = title.GetComponentInChildren<Canvas>();
            var camera = Camera.allCameras.FirstOrDefault();
            GameObject cameraObject = null;
            var previousMask = camera ? camera.cullingMask : 0;
            var mode = canvas.renderMode;
            var worldCamera = canvas.worldCamera;
            var distance = canvas.planeDistance;
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                var issues = CheckLayout(name);
                if (!camera)
                {
                    cameraObject = new GameObject("Title capture camera");
                    camera = cameraObject.AddComponent<Camera>();
                }
                camera.cullingMask |= 1 << canvas.gameObject.layer;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
                if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues));
            }
            finally
            {
                canvas.renderMode = mode;
                canvas.worldCamera = worldCamera;
                canvas.planeDistance = distance;
                if (camera) camera.cullingMask = previousMask;
                if (cameraObject) Destroy(cameraObject);
                if (pixels) Destroy(pixels);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
