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
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RimworldTweak.Tests.PlayMode
{
    public sealed class TitleMenuTests
    {
        private string directory;
        private TitleMenu menu;
        private GameObject events;
        private List<string> starts;
        private SaveRepository slots;
        private UserSettingsStore settings;

        [SetUp]
        public void Before()
        {
            directory = Path.Combine(Application.temporaryCachePath, "RimworldTweak-Title-" + Guid.NewGuid().ToString("N"));
            slots = new SaveRepository(Path.Combine(directory, "Saves"));
            settings = new UserSettingsStore(Path.Combine(directory, "Settings", "user-settings.json"));
            starts = new List<string>();
            if (EventSystem.current == null) events = new GameObject("Test events", typeof(EventSystem));
            menu = new GameObject("Test title").AddComponent<TitleMenu>();
        }

        [UnityTearDown]
        public IEnumerator After()
        {
            if (menu != null) UnityEngine.Object.Destroy(menu.gameObject);
            if (events != null) UnityEngine.Object.Destroy(events);
            yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private void Open() => menu.Initialize(directory, null, starts.Add, () => Assert.Fail("Unexpected exit"));
        private Button Button(string key) => menu.GetComponentsInChildren<Button>().Single(b => b.name == key);
        private void Submit(string key) => ExecuteEvents.Execute(Button(key).gameObject,
            new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        private string SaveSlot(string name)
        {
            var session = new SimulationSession(SandboxScenario.Create());
            var result = slots.SaveNew(name, session.CaptureSaveData(true, 1).Data);
            Assert.That(result.Success, Is.True, result.ErrorCode);
            return result.Slot.SlotId;
        }

        [UnityTest]
        public IEnumerator EmptyTitleKeepsNavigationInMenuAndStartsOnlyOnce()
        {
            Open();
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("new").gameObject));
            Assert.That(Button("continue").IsInteractable(), Is.False);
            Submit("continue");
            Submit("load.menu");
            Assert.That(menu.GetComponentsInChildren<Text>().Any(t => t.text.Contains("저장") && t.text.Contains("없")), Is.True);
            Submit("cancel");
            Submit("settings");
            Submit("scale.150");
            Assert.That(settings.Load().Settings.UiScalePercent, Is.EqualTo(150));
            Submit("cancel");
            Assert.That(starts, Is.Empty);
            var first = Button("new");
            for (var i = 0; i < 2; i++)
                ExecuteEvents.Execute(first.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(starts.Count, Is.EqualTo(1));
            Assert.That(starts[0], Is.Null);
        }

        [UnityTest]
        public IEnumerator ContinueWithoutRecentRequiresConfirmationAndMissingRecentOffersManualChoice()
        {
            var candidate = SaveSlot("후보 저장");
            Open();
            Submit("continue");
            Submit("load." + candidate);
            Assert.That(starts, Is.Empty);
            Submit("cancel");
            Assert.That(starts, Is.Empty);
            Submit("cancel");
            Submit("continue");
            Submit("load." + candidate);
            Submit("load.confirm");
            Assert.That(starts, Is.EqualTo(new[] { candidate }));
            UnityEngine.Object.Destroy(menu.gameObject);
            yield return null;
            menu = new GameObject("Second test title").AddComponent<TitleMenu>();
            var missing = Guid.NewGuid().ToString("N");
            Assert.That(settings.Save(new UserSettings { RecentSlotId = missing }).Success, Is.True);
            Open();
            Submit("continue");
            Assert.That(starts.Count, Is.EqualTo(1));
            Assert.That(Button("load." + candidate).IsInteractable(), Is.True);
        }

        [UnityTest]
        public IEnumerator CorruptSlotAndFailedRestoreLeaveAnActionableList()
        {
            var good = SaveSlot("정상 저장");
            var bad = Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(slots.SaveDirectory, bad + ".save.json"), "{");
            Assert.That(settings.Save(new UserSettings { RecentSlotId = bad }).Success, Is.True);
            Open();
            Submit("continue");
            Assert.That(Button("load." + bad).IsInteractable(), Is.False);
            Submit("load." + bad);
            Assert.That(starts, Is.Empty);
            Submit("load." + good);
            Assert.That(starts, Is.EqualTo(new[] { good }));
            menu.ShowLoadError("복원 검사 오류");
            Assert.That(menu.GetComponentsInChildren<Text>().Any(t => t.text.Contains("복원 검사 오류")), Is.True);
            Assert.That(Button("load." + good).IsInteractable(), Is.True);
            Submit("cancel");
            Submit("new");
            Assert.That(starts.Count, Is.EqualTo(2));
            yield return null;
        }
    }
}
