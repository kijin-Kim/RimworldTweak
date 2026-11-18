using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Presentation;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RimworldTweak.Tests.PlayMode
{
    // Button callback tests verify shell wiring and state, not physical mouse hit testing.
    // The keyboard test sends Input System state through the real Shell.Update path.
    public sealed class SettlementShellIntegrationTests
    {
        private GameObject root, eventRoot;
        private SimulationRunner runner;
        private SettlementShell shell;
        private string directory;
        private Keyboard keyboard;
        private static GridPosition P(int x,int y)=>new GridPosition(x,y);
        [UnityTest]
        public IEnumerator NewSettlementUsesLargeMapAndPreservesDimensionsThroughSave()
        {
            Setup();runner.StartNewSettlement();
            yield return null;
            var w=runner.Session.World;
            Assert.That(w.Width,Is.EqualTo(32));Assert.That(w.Height,Is.EqualTo(32));
            Assert.That(w.Gatherables.Any(x=>x.Position.X>20&&x.Position.Y>20),Is.True);
            var captured=runner.Session.CaptureSaveData(true,1);
            Assert.That(captured.Success,Is.True);
            Assert.That(SimulationSession.TryRestoreSave(captured.Data,out var restored,out var error),Is.True,error);
            Assert.That(restored.World.Width,Is.EqualTo(32));
            Assert.That(restored.World.Gatherables.Count,Is.EqualTo(w.Gatherables.Count));
        }
        private void Setup(bool developmentUi = false)
        {
            directory=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TestResults","ShellIntegration",Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            eventRoot=new GameObject("Shell test events",typeof(EventSystem));
            root=new GameObject("Shell integration");
            runner=root.AddComponent<SimulationRunner>();runner.enabled=false;
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,P(2,2))},new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(3,2))},needsEnabled:false));
            shell=root.AddComponent<SettlementShell>();shell.SuppressProcessExit=true;
            shell.Initialize(runner,false,directory,enableDevelopmentUi:developmentUi);
        }
        private void Click(string key)
        {
            Assert.That(shell.Buttons.ContainsKey(key),Is.True,"Missing shell command: "+key);
            var button=shell.Buttons[key];Assert.That(button,Is.Not.Null,key);
            Assert.That(button.gameObject.activeInHierarchy,Is.True,key);
            Assert.That(button.interactable,Is.True,key);
            button.onClick.Invoke();
        }
        private string SlotPath(string id)=>Path.Combine(directory,"Saves",id+".save.json");
        private string SaveDirect(string name,int advance=0)
        {
            runner.Session.Step(advance);Assert.That(runner.SaveSlot(name),Is.True,runner.SaveMessage);
            Assert.That(runner.SettingsWarning,Is.Empty,"Fixture requires a persisted recent slot: "+name);
            Assert.That(runner.Settings.RecentSlotId,Is.EqualTo(runner.ActiveSlotId));
            return runner.ActiveSlotId;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            keyboard=null;
            if(root!=null)UnityEngine.Object.Destroy(root);
            if(eventRoot!=null)UnityEngine.Object.Destroy(eventRoot);
            yield return null;
            var allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TestResults","ShellIntegration"))+Path.DirectorySeparatorChar;
            if(directory!=null&&directory.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(directory))Directory.Delete(directory,true);
        }

        [UnityTest] public IEnumerator OverlayPanelsPreserveCameraAndOnlyVisiblePanelsBlockPointer()
        {
            Setup();runner.SetPaused(true);yield return null;
            var canvas=shell.GetComponentInChildren<Canvas>();
            var inspector=(RectTransform)canvas.transform.Find("Selection summary");
            var panel=(RectTransform)canvas.transform.Find("Command panel");
            Assert.That(inspector.gameObject.activeSelf,Is.False);
            Assert.That(panel.gameObject.activeSelf,Is.False);
            var hud = (RectTransform)canvas.transform;
            Vector2 ScreenCenter(string name)
            {
                var rect = (RectTransform)hud.Find(name);
                return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            }
            Assert.That(ScreenCenter("Resources").x, Is.LessThan(Screen.width * .3f));
            Assert.That(ScreenCenter("Portrait viewport").x, Is.EqualTo(Screen.width * .5f).Within(1));
            Assert.That(ScreenCenter("Time controls").x, Is.GreaterThan(Screen.width * .65f));
            Assert.That(ScreenCenter("Time controls").y, Is.LessThan(Screen.height * .3f));
            Assert.That(ScreenCenter("Work notices").y, Is.GreaterThan(ScreenCenter("Time controls").y));
            Assert.That(shell.MapInput.IsPointerOverUI(ScreenCenter("Resources")), Is.False);
            Assert.That(shell.MapInput.IsPointerOverUI(ScreenCenter("Tabs")), Is.False);
            Assert.That(shell.MapInput.IsPointerOverUI(ScreenCenter("Time controls")), Is.True);
            Assert.That(shell.Map.Viewport,Is.EqualTo(new Rect(0,0,1,1)));
            var center=shell.Map.MapCamera.transform.position;
            var zoom=shell.Map.ZoomSize;
            var cell=shell.Map.GridToScreen(P(3,3));
            shell.SelectPawn(1);Click("tab.work");yield return null;
            Assert.That(inspector.gameObject.activeSelf,Is.False);
            Assert.That(shell.SelectedPawnId,Is.EqualTo(1));
            Assert.That(panel.gameObject.activeSelf,Is.True);
            var point=RectTransformUtility.WorldToScreenPoint(null,panel.TransformPoint(panel.rect.center));
            var pointer=new PointerEventData(EventSystem.current){position=point};
            var hits=new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits.Any(h=>h.gameObject.transform.IsChildOf(panel)||h.gameObject==panel.gameObject),Is.True);
            Click("tab.work");
            Assert.That(inspector.gameObject.activeSelf,Is.True);
            Click("tab.build");
            foreach(var pair in new[]{("structure","build.Wall"),("furniture","build.Bed"),("production","build.Stove"),("zones","tool.storage"),("orders","tool.gather")})
            {
                Click("architect."+pair.Item1);
                Assert.That(shell.Buttons[pair.Item2].gameObject.activeInHierarchy,Is.True);
            }
            Click("tool.gather");
            Assert.That(shell.ProcessShortcut(Key.Escape),Is.True);
            Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));
            Assert.That(panel.gameObject.activeSelf,Is.True);
            shell.SelectPawn(1);
            Assert.That(panel.gameObject.activeSelf,Is.False);
            shell.ClearSelection();yield return null;
            hits.Clear();EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits.Any(h=>h.gameObject.transform.IsChildOf(panel)||h.gameObject==panel.gameObject),Is.False);
            Assert.That(shell.Map.MapCamera.transform.position,Is.EqualTo(center));
            Assert.That(shell.Map.ZoomSize,Is.EqualTo(zoom));
            Assert.That(Vector2.Distance(shell.Map.GridToScreen(P(3,3)),cell),Is.LessThan(.01f));
            Assert.That(runner.Session.World.Tick,Is.Zero);
        }

        [UnityTest] public IEnumerator WheelScrollReachesLastWorkRowWithoutZoomAndPointerClickChangesOnlyThatPriority()
        {
            Setup();runner.Initialize(WorkScenario.Create());runner.SetPaused(true);
            yield return null;yield return null;shell.OpenTab("work");
            var canvas=shell.GetComponentInChildren<Canvas>();
            var panel=(RectTransform)canvas.transform.Find("Command panel");
            // Exercise both overflow axes independently of the batch Game View size.
            panel.sizeDelta = new Vector2(450, 150);
            shell.OpenTab("work");
            yield return null;yield return null;
            Canvas.ForceUpdateCanvases();
            var scroll=panel.GetComponentInChildren<ScrollRect>();scroll.inertia=false;
            Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.horizontal, Is.True);
            Assert.That(scroll.content.rect.width, Is.EqualTo(612).Within(.1f));
            var headings = scroll.content.GetChild(1);
            Assert.That(((RectTransform)headings.GetChild(0)).rect.width, Is.GreaterThanOrEqualTo(122));
            Assert.That(((RectTransform)headings.GetChild(5)).rect.width, Is.GreaterThanOrEqualTo(92));
            var target=shell.Buttons["priority.3.Cook"];
            scroll.horizontalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.content.anchoredPosition.x, Is.LessThan(0));
            var before=runner.Session.World.WorkPolicies.Single(p=>p.PawnId==3).GetPriority(WorkCategory.Cook);
            var other=runner.Session.World.WorkPolicies.Single(p=>p.PawnId==1).GetPriority(WorkCategory.Cook);
            var zoom=shell.Map.ZoomSize;var cameraPosition=shell.Map.MapCamera.transform.position;
            var previousMode=InputSystem.settings.updateMode;
            var previousBackground=InputSystem.settings.backgroundBehavior;
            var previousEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            var module=eventRoot.AddComponent<InputSystemUIInputModule>();
            Mouse mouse=null;shell.enabled=false;shell.MapInput.enabled=false;
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
                mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();
                Assert.That(mouse.enabled,Is.True);
                Action<MouseState> pump=state=>{
                    InputSystem.QueueStateEvent(mouse,state);InputSystem.Update();mouse.MakeCurrent();
                    module.Process();shell.MapInput.SendMessage("Update");Canvas.ForceUpdateCanvases();
                };
                var point=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(scroll.viewport.rect.center));
                pump(new MouseState{position=point});
                Assert.That(shell.MapInput.IsPointerOverUI(point),Is.True,
                    "point="+point+" screen="+Screen.width+"x"+Screen.height+" panel="+panel.gameObject.activeInHierarchy
                    +" viewport="+scroll.viewport.rect+" EventSystem="+(EventSystem.current==eventRoot.GetComponent<EventSystem>()));
                var start=scroll.content.anchoredPosition.y;
                for(var i=0;i<8;i++)pump(new MouseState{position=point,scroll=new Vector2(0,-120)});
                Assert.That(scroll.content.anchoredPosition.y,Is.GreaterThan(start));
                Assert.That(scroll.verticalNormalizedPosition,Is.LessThanOrEqualTo(.01f));
                point=RectTransformUtility.WorldToScreenPoint(null,target.transform.TransformPoint(((RectTransform)target.transform).rect.center));
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,point),Is.True,"Last row and column must become visible.");
                pump(new MouseState{position=point});
                pump(new MouseState{position=point}.WithButton(MouseButton.Left));
                pump(new MouseState{position=point});
                Assert.That(runner.Session.World.WorkPolicies.Single(p=>p.PawnId==3).GetPriority(WorkCategory.Cook),Is.EqualTo((before+1)%4));
                Assert.That(runner.Session.World.WorkPolicies.Single(p=>p.PawnId==1).GetPriority(WorkCategory.Cook),Is.EqualTo(other));
                Assert.That(shell.Map.ZoomSize,Is.EqualTo(zoom));
                Assert.That(shell.Map.MapCamera.transform.position,Is.EqualTo(cameraPosition));
                Assert.That(runner.Session.World.Tick,Is.Zero);
                shell.OpenTab(string.Empty);
                point=shell.Map.GridToScreen(P(3,3));
                Assert.That(shell.MapInput.IsPointerOverUI(point),Is.False);
                pump(new MouseState{position=point});
                pump(new MouseState{position=point,scroll=new Vector2(0,120)});
                Assert.That(shell.Map.ZoomSize,Is.LessThan(zoom),"The same wheel event must zoom when it reaches the map.");
            }
            finally
            {
                module.enabled=false;UnityEngine.Object.Destroy(module);
                if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
                InputSystem.settings.updateMode=previousMode;
                InputSystem.settings.backgroundBehavior=previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode=previousEditor;
                shell.enabled=true;shell.MapInput.enabled=true;
            }
            yield return null;
        }

        [UnityTest] public IEnumerator WorkNoticeNamesTargetDeduplicatesAndClearsWhenCauseIsResolved()
        {
            Setup();runner.SetPaused(true);
            Assert.That(runner.SetWorkPriority(1,WorkCategory.Build,0),Is.True);
            Assert.That(runner.PlaceBlueprint(BuildingKind.Wall,P(5,5)),Is.True);
            var world=runner.Session.World;var blueprint=world.Blueprints.Single(b=>b.Exists);
            var key="alert."+blueprint.Id+"."+WorkReasonCode.NoWorker;
            for(var frame=0;frame<5;frame++)yield return null;
            Assert.That(shell.Buttons.ContainsKey(key),Is.True);
            Assert.That(shell.GetComponentsInChildren<Button>().Count(b=>b.name==key),Is.EqualTo(1));
            var caption=shell.Buttons[key].GetComponentInChildren<Text>().text;
            Assert.That(caption,Does.Contain("벽 설계").And.Contain(P(5,5).ToString()).And.Contain(WorkDiagnostics.GetReasonText(WorkReasonCode.NoWorker)));
            var action=world.Colonists[0].ActionId;
            Click(key);Assert.That(shell.SelectedObjectId,Is.EqualTo(blueprint.Id));
            Assert.That(world.Colonists[0].ActionId,Is.EqualTo(action));
            Assert.That(blueprint.Exists,Is.True);Assert.That(blueprint.OwnerId,Is.Null);
            Assert.That(runner.SetWorkPriority(1,WorkCategory.Build,1),Is.True);
            yield return null;yield return null;
            Assert.That(shell.Buttons.ContainsKey(key),Is.False);
            Assert.That(world.Tick,Is.Zero);
            var summary=shell.GetComponentsInChildren<Text>().Single(t=>t.name=="Resource total Wood").text;
            Assert.That(summary,Is.EqualTo("목재 20"));
        }

        [UnityTest] public IEnumerator ConstructionNoticeTracksMissingMaterialsAndBlockedAccess()
        {
            Setup();runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,P(1,1))},Array.Empty<ResourceSeed>()));
            runner.SetPaused(true);Assert.That(runner.PlaceBlueprint(BuildingKind.Wall,P(5,5)),Is.True);
            var world=runner.Session.World;var site=world.Blueprints.Single(b=>b.Exists);
            yield return null;yield return null;
            var missing="alert."+site.Id+"."+WorkReasonCode.MissingMaterials;
            Assert.That(shell.Buttons[missing].GetComponentInChildren<Text>().text,
                Does.Contain(WorkDiagnostics.GetReasonText(WorkReasonCode.MissingMaterials)));
            for(var y=0;y<8;y++)world.QueueSetBlocked(P(3,y),true);
            runner.Session.Step(1);yield return null;yield return null;
            var unreachable="alert."+site.Id+"."+WorkReasonCode.Unreachable;
            Assert.That(shell.Buttons.ContainsKey(missing),Is.False);
            Assert.That(shell.Buttons[unreachable].GetComponentInChildren<Text>().text,
                Does.Contain(WorkDiagnostics.GetReasonText(WorkReasonCode.Unreachable)));
            var tick=world.Tick;Click(unreachable);
            Assert.That(shell.SelectedObjectId,Is.EqualTo(site.Id));Assert.That(world.Tick,Is.EqualTo(tick));
            for(var y=0;y<8;y++)world.QueueSetBlocked(P(3,y),false);
            runner.Session.Step(1);yield return null;yield return null;
            Assert.That(shell.Buttons.ContainsKey(unreachable),Is.False);
            Assert.That(shell.Buttons.ContainsKey(missing),Is.True);
        }

        [UnityTest] public IEnumerator CookingNoticeDistinguishesFullOutputFromNormalTargetAndCountsInputsOnce()
        {
            Setup();runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,P(1,1))},new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(1,1)),
                    new ResourceSeed(102,ResourceKind.Ingredients,120,P(2,1))}));
            runner.SetPaused(true);Assert.That(runner.PlaceBlueprint(BuildingKind.Stove,P(4,4)),Is.True);
            for(var tick=0;tick<600&&!runner.Session.World.Buildings.Any(b=>b.Exists);tick++)runner.Session.Step(1);
            var world=runner.Session.World;var stove=world.Buildings.Single(b=>b.Exists);
            foreach(WorkCategory category in Enum.GetValues(typeof(WorkCategory)))
                Assert.That(runner.SetWorkPriority(1,category,category==WorkCategory.Cook?1:0),Is.True);
            Assert.That(runner.SetCookingOrder(stove.Id,true,99),Is.True);
            var output=world.CookingOutputs.Single(o=>o.Exists);
            // Same bounded path as CookingTests: 50 meals at 40 ticks each plus delivery/movement.
            for(var tick=0;tick<5000&&output.Amount<50;tick++)runner.Session.Step(1);
            Assert.That(output.Amount,Is.EqualTo(50));
            yield return null;yield return null;
            var full="alert."+stove.Id+"."+WorkReasonCode.OutputFull;
            var reached="alert."+stove.Id+"."+WorkReasonCode.TargetReached;
            Assert.That(shell.Buttons[full].GetComponentInChildren<Text>().text,
                Does.StartWith("! 조리대").And.Contain(WorkDiagnostics.GetReasonText(WorkReasonCode.OutputFull)));
            var tickBefore=world.Tick;Assert.That(runner.SetCookingOrder(stove.Id,true,50),Is.True);
            yield return null;yield return null;
            Assert.That(shell.Buttons.ContainsKey(full),Is.False);
            Assert.That(shell.Buttons[reached].GetComponentInChildren<Text>().text,Does.StartWith("정상 · 조리대"));
            Assert.That(world.Tick,Is.EqualTo(tickBefore));
            Assert.That(runner.SetCookingOrder(stove.Id,true,51),Is.True);
            yield return null;yield return null;
            Assert.That(shell.Buttons.ContainsKey(reached),Is.False);Assert.That(shell.Buttons.ContainsKey(full),Is.True);
            world.QueueSetResourceAmount(output.Stack.Id,49);runner.Session.Step(1);
            var order=world.CookingOrders.Single(o=>o.Exists);
            var checkedCargo=false;
            for(var tick=0;tick<300&&order.InputIngredients<2;tick++)
            {
                runner.Session.Step(1);
                if(!checkedCargo&&world.CargoTotal(ResourceKind.Ingredients)==2)
                {
                    yield return null;yield return null;
                    var carryingSummary=shell.GetComponentsInChildren<Text>().Single(t=>t.name=="Resource total Ingredients").text;
                    Assert.That(world.ResourceTotal(ResourceKind.Ingredients,false),Is.EqualTo(18));
                    Assert.That(carryingSummary,Does.Contain("식재료 20"),"18 on the ground + 2 carried must be counted once.");
                    checkedCargo=true;
                }
            }
            Assert.That(checkedCargo,Is.True,"Fixture must observe delivery, not skip straight to processing.");
            Assert.That(order.InputIngredients,Is.EqualTo(2));
            yield return null;yield return null;
            Assert.That(shell.Buttons.ContainsKey(full),Is.False);
            var summary=shell.GetComponentsInChildren<Text>().Single(t=>t.name=="Resource total Ingredients").text;
            Assert.That(world.ResourceTotal(ResourceKind.Ingredients,false)+world.CargoTotal(ResourceKind.Ingredients),Is.EqualTo(18));
            Assert.That(summary,Does.Contain("식재료 20"),"18 outside + 2 in production must be counted once.");
        }

        [UnityTest] public IEnumerator MissingRecentRecordOffersLatestSlotAndRequiresConfirmation()
        {
            Setup();
            shell.ShowPauseMenu();Click("main");Click("transition.discard");
            Assert.That(shell.Buttons["continue"].interactable,Is.False);
            SaveDirect("이전 저장",2);
            var latest=SaveDirect("최근 저장",3);
            runner.Settings.RecentSlotId=null;
            var original=runner.Session;
            shell.ShowMainMenu();Click("continue");
            Assert.That(runner.Session,Is.SameAs(original));
            Assert.That(shell.Buttons["load."+latest].GetComponentInChildren<Text>().text,Does.StartWith("[최근 저장 후보]"));
            Click("load."+latest);
            Assert.That(runner.Session,Is.SameAs(original));
            Click("cancel");
            Assert.That(runner.Session,Is.SameAs(original));
            shell.ShowMainMenu();Click("continue");Click("load."+latest);Click("load.confirm");
            Assert.That(runner.ActiveSlotId,Is.EqualTo(latest));
            Assert.That(runner.Session,Is.Not.SameAs(original));
            Assert.That(runner.Session.World.Tick,Is.EqualTo(5));
            yield return null;
        }

        [UnityTest] public IEnumerator MissingOrCorruptRecentSlotShowsListWithoutLoadingAnotherSave()
        {
            Setup();
            var other=SaveDirect("다른 저장",2);
            var recent=SaveDirect("최근 저장",3);
            var validBytes=File.ReadAllBytes(SlotPath(recent));
            var original=runner.Session;
            shell.ShowPauseMenu();Click("main");Click("transition.discard");
            File.Delete(SlotPath(recent));
            Assert.That(runner.Settings.RecentSlotId,Is.EqualTo(recent),"Deleted recent slot must remain the explicit continue target.");
            Assert.That(File.Exists(SlotPath(recent)),Is.False);
            shell.ShowMainMenu();Click("continue");
            Assert.That(runner.Session,Is.SameAs(original));
            Assert.That(shell.HasSettlement,Is.False);
            Assert.That(shell.Feedback,Does.Contain("최근 저장을 읽을 수 없어"));
            Assert.That(shell.Buttons["load."+other].interactable,Is.True);
            File.WriteAllText(SlotPath(recent),"invalid json");
            shell.ShowMainMenu();Click("continue");
            Assert.That(runner.Session,Is.SameAs(original));
            Assert.That(shell.Buttons["load."+recent].interactable,Is.False);
            File.WriteAllBytes(SlotPath(recent),validBytes);
            shell.ShowMainMenu();Click("continue");
            Assert.That(shell.HasSettlement,Is.True);
            Assert.That(runner.ActiveSlotId,Is.EqualTo(recent));
            Assert.That(runner.Session.World.Tick,Is.EqualTo(5));
            yield return null;
        }

        [UnityTest] public IEnumerator SettingsFailureAfterSaveKeepsNewSlotAndWaitsForAcknowledgementBeforeExit()
        {
            Setup();
            var a=SaveDirect("A",2);
            var aBytes=File.ReadAllBytes(SlotPath(a));
            var settingsPath=Path.Combine(directory,"Settings","user-settings.json");
            runner.Session.Step(3);
            shell.ShowPauseMenu();Click("exit");Click("transition.save");
            shell.SaveNameInput.text="B";
            using(var locked=new FileStream(settingsPath,FileMode.Open,FileAccess.Read,FileShare.Read))
                Click("save.new");
            var b=runner.ActiveSlotId;
            Assert.That(b,Is.Not.EqualTo(a));
            Assert.That(runner.Slots.Load(b).Data.World.Tick,Is.EqualTo(5));
            CollectionAssert.AreEqual(aBytes,File.ReadAllBytes(SlotPath(a)));
            Assert.That(new UserSettingsStore(settingsPath).Load().Settings.RecentSlotId,Is.EqualTo(a));
            Assert.That(runner.SettingsWarning,Does.Contain("'B'"));
            Assert.That(shell.ExitRequested,Is.False);
            Click("warning.ok");
            Assert.That(shell.ExitRequested,Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator UiScalePersistsAcrossStorageReloadAndGameLoadAndReportsSettingsFailure()
        {
            Setup();
            var slot=SaveDirect("기존 상태",2);
            shell.ShowPauseMenu();Click("settings");Click("scale.150");
            Assert.That(runner.Settings.UiScalePercent,Is.EqualTo(150),"Scale button must apply before disk reload.");
            Assert.That(runner.SettingsWarning,Is.Empty,"Initial scale persistence failed before ConfigureStorage: "+runner.SettingsWarning);
            var persistedScale=new UserSettingsStore(Path.Combine(directory,"Settings","user-settings.json")).Load();
            Assert.That(persistedScale.Success,Is.True,"Initial settings read: "+persistedScale.ErrorCode);
            Assert.That(persistedScale.Settings.UiScalePercent,Is.EqualTo(150),"Readback before ConfigureStorage.");
            runner.ConfigureStorage(directory);
            Assert.That(runner.Settings.UiScalePercent,Is.EqualTo(150),runner.SettingsWarning);
            Assert.That(runner.LoadSlot(slot),Is.True);
            Assert.That(runner.Settings.UiScalePercent,Is.EqualTo(150));
            var settingsPath=Path.Combine(directory,"Settings","user-settings.json");
            shell.ShowPauseMenu();Click("settings");
            using(var locked=new FileStream(settingsPath,FileMode.Open,FileAccess.Read,FileShare.Read))
                Click("scale.125");
            Assert.That(runner.Settings.UiScalePercent,Is.EqualTo(125));
            Assert.That(new UserSettingsStore(settingsPath).Load().Settings.UiScalePercent,Is.EqualTo(150));
            Assert.That(shell.GetComponentsInChildren<Text>().Any(t=>t.gameObject.activeInHierarchy&&t.text==runner.SettingsWarning),Is.True);
            Assert.That(runner.SettingsWarning,Is.Not.Empty);
            File.WriteAllText(settingsPath,"invalid json");
            runner.ConfigureStorage(directory);
            shell.ShowPauseMenu();Click("settings");
            Assert.That(runner.Settings.UiScalePercent,Is.EqualTo(100));
            Assert.That(shell.GetComponentsInChildren<Text>().Any(t=>t.gameObject.activeInHierarchy&&t.text==runner.SettingsWarning),Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator PawnInspectorCanSelectAnOverlappingResourceWithoutChangingOrders()
        {
            Setup();
            shell.SelectPawn(1);
            var details = shell.GetComponentInChildren<Canvas>().transform.Find("Inspector").gameObject;
            Assert.That(details.activeSelf, Is.False);
            Click("selection.detail");
            Assert.That(details.activeSelf, Is.True);
            Assert.That(shell.ProcessShortcut(Key.Escape), Is.True);
            Assert.That(details.activeSelf, Is.False);
            Assert.That(shell.SelectedPawnId, Is.EqualTo(1));
            Click("selection.detail");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var focusPoint = shell.Map.GridToScreen(P(2,2));
            var detailRect = (RectTransform)details.transform;
            var panelCenter = RectTransformUtility.WorldToScreenPoint(null, detailRect.TransformPoint(detailRect.rect.center));
            detailRect.position += (Vector3)(focusPoint - panelCenter);
            Canvas.ForceUpdateCanvases();
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(detailRect, focusPoint), Is.True);
            Assert.That(shell.MapInput.IsPointerOverUI(focusPoint), Is.True);
            var beforeFocus = CurrentSaveBytes();
            var zoom = shell.Map.ZoomSize;
            Click("locate");
            Assert.That(details.activeSelf, Is.False);
            Assert.That(shell.MapInput.IsPointerOverUI(shell.Map.GridToScreen(P(2,2))), Is.False);
            Assert.That(shell.Map.ZoomSize, Is.EqualTo(zoom));
            CollectionAssert.AreEqual(beforeFocus, CurrentSaveBytes());
            Assert.That(shell.Buttons["next.object"].interactable,Is.False);
            Click("draft");
            Assert.That(runner.MoveColonist(1,P(3,2)),Is.True);
            runner.Session.Step(5);
            yield return null;yield return null;
            var pawn=runner.Session.World.Colonists[0];
            var tick=runner.Session.World.Tick;
            var action=pawn.ActionId;
            Click("next.object");
            Assert.That(shell.SelectedObjectId,Is.EqualTo(101));
            Assert.That(shell.SelectedPawnId,Is.Null);
            Assert.That(pawn.IsDrafted,Is.True);
            Assert.That(pawn.ActionId,Is.EqualTo(action));
            Assert.That(runner.Session.World.Tick,Is.EqualTo(tick));
            Click("next.object");
            Assert.That(shell.SelectedPawnId, Is.EqualTo(1), "The last object must cycle back to the pawn.");
            Assert.That(pawn.ActionId, Is.EqualTo(action));
            shell.HandleMapClick(P(3,2),false);
            Assert.That(shell.SelectedPawnId,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator RepeatedLoadAndResetCyclesRebindViewsAndButtonsOnlyToCurrentWorld()
        {
            Setup();
            runner.SetPaused(true);
            var slot=SaveDirect("반복 복원",2);
            yield return null;yield return null;
            var markerCount=shell.Map.MarkerObjectCount;
            var acknowledgedSettingsWarnings=0;
            for(var cycle=0;cycle<1000;cycle++)
            {
                shell.SelectPawn(1);shell.BeginTool(SettlementShell.Tool.Storage);
                var previous=runner.Session.World;
                shell.ShowLoadMenu();Click("load."+slot);Click("load.confirm");
                yield return null;yield return null;
                Assert.That(shell.Map.World,Is.SameAs(runner.Session.World));
                Assert.That(shell.Map.World,Is.Not.SameAs(previous));
                Assert.That(shell.SelectedPawnId,Is.Null);
                Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));
                Assert.That(shell.Map.MarkerObjectCount,Is.EqualTo(markerCount));
                if(!string.IsNullOrEmpty(runner.SettingsWarning))
                {
                    Assert.That(runner.SettingsWarning,Does.Contain("'반복 복원'"));
                    Assert.That(shell.IsMenuOpen,Is.True);Assert.That(runner.IsUiBlocked,Is.True);
                    Assert.That(runner.ActiveSlotId,Is.EqualTo(slot));Assert.That(shell.ExitRequested,Is.False);
                    var restoredState=CurrentSaveBytes();
                    Click("warning.ok");
                    CollectionAssert.AreEqual(restoredState,CurrentSaveBytes(),"Acknowledging a settings warning must not change the restored settlement.");
                    acknowledgedSettingsWarnings++;
                }
                Assert.That(runner.IsUiBlocked,Is.False,"Load left UI blocked at cycle "+cycle
                    +"; settings warning="+runner.SettingsWarning+"; save message="+runner.SaveMessage
                    +"; warning button="+shell.Buttons.ContainsKey("warning.ok")+"; menu="+shell.IsMenuOpen);
                Assert.That(runner.Session.World.Colonists[0].IsDrafted,Is.False,"Loaded pawn already drafted at cycle "+cycle);
                Click("pawn.1");Click("draft");
                Assert.That(runner.Session.World.Colonists[0].IsDrafted,Is.True,"Duplicate toggle at cycle "+cycle);
                Assert.That(previous.Colonists[0].IsDrafted,Is.False,"Old world changed");
                Click("draft");
                shell.BeginTool(SettlementShell.Tool.Growing);
                previous=runner.Session.World;
                runner.ResetWorld();
                yield return null;yield return null;
                Assert.That(shell.Map.World,Is.SameAs(runner.Session.World));
                Assert.That(shell.Map.World,Is.Not.SameAs(previous));
                Assert.That(shell.SelectedPawnId,Is.Null);
                Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));
                Assert.That(runner.Session.World.Tick,Is.Zero);
                Assert.That(shell.Map.MarkerObjectCount,Is.EqualTo(markerCount));
                Assert.That(shell.Feedback,Is.Empty,"Reset must discard the previous world's tool message.");
                Click("pawn.1");Click("draft");Click("draft");
                Assert.That(runner.Session.World.Colonists[0].IsDrafted,Is.False);
                Assert.That(previous.Colonists[0].IsDrafted,Is.False);
                Assert.That(shell.GetComponentsInChildren<Button>().Count(x=>x.name=="pawn.1"),Is.EqualTo(1));
            }
            TestContext.WriteLine("Completed 1000 Load/Reset cycles; acknowledged settings warnings="+acknowledgedSettingsWarnings);
        }

        [UnityTest] public IEnumerator WorkTableChangesPriorityWithoutRestartingCookingAndOffReleasesWorker()
        {
            Setup();
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,P(1,1))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(1,1)),new ResourceSeed(102,ResourceKind.Ingredients,8,P(2,1))},null,false));
            runner.SetPaused(true);
            Assert.That(runner.PlaceBlueprint(BuildingKind.Stove,P(4,4)),Is.True);
            for(var tick=0;tick<600&&!runner.Session.World.Buildings.Any(b=>b.Exists);tick++)runner.Session.Step(1);
            var stove=runner.Session.World.Buildings.Single(b=>b.Exists);
            yield return null;yield return null;
            shell.SelectObject(stove.Id);Click("selection.detail");Click("cook.toggle");
            var order=runner.Session.World.CookingOrders.Single(o=>o.Exists);
            for(var tick=0;tick<300&&order.Progress<10;tick++)runner.Session.Step(1);
            Assert.That(order.Progress,Is.EqualTo(10));
            Assert.That(order.InputIngredients,Is.EqualTo(2));
            Assert.That(runner.SetWorkPriority(1,WorkCategory.Cook,1),Is.True);
            var savedTick=runner.Session.World.Tick;
            var owner=order.OwnerId;
            var action=runner.Session.World.Colonists[0].ActionId;
            shell.OpenTab("work");
            var key="priority.1.Cook";
            Click(key);Click(key);
            Assert.That(order.OwnerId,Is.EqualTo(owner));
            Assert.That(runner.Session.World.Colonists[0].ActionId,Is.EqualTo(action));
            Assert.That(order.Progress,Is.EqualTo(10));
            Click(key);
            Assert.That(runner.Session.World.WorkPolicies[0].GetPriority(WorkCategory.Cook),Is.Zero);
            Assert.That(order.OwnerId,Is.Null);
            Assert.That(order.Progress,Is.EqualTo(10));
            Assert.That(order.InputIngredients,Is.EqualTo(2));
            Assert.That(runner.Session.World.Tick,Is.EqualTo(savedTick));
            yield return null;yield return null;
            Assert.That(shell.Buttons[key].GetComponentInChildren<Text>().text,Is.EqualTo("끔"));
            Click(key);
            runner.Session.Step(1);
            Assert.That(order.OwnerId,Is.EqualTo(1),"Activity="+runner.Session.World.Colonists[0].WorkActivity
                +" Step="+runner.Session.World.Colonists[0].FeatureStep+" Reason="+runner.Session.World.Colonists[0].WorkReason
                +" Order="+order.Reason+" Priority="+runner.Session.World.WorkPolicies[0].GetPriority(WorkCategory.Cook));
            Assert.That(order.Progress,Is.GreaterThanOrEqualTo(10));
            Click("tab.work");
            Click("cook.toggle");
            Assert.That(order.Enabled,Is.False);
            Assert.That(order.OwnerId,Is.Null);
            var progress=order.Progress;
            runner.Session.Step(5);
            Assert.That(order.Progress,Is.EqualTo(progress));
            savedTick=runner.Session.World.Tick;
            var world=runner.Session.World;
            var ingredientTotal=world.ResourceTotal(ResourceKind.Ingredients,false)+world.ResourceTotal(ResourceKind.Ingredients,true)
                +world.CargoTotal(ResourceKind.Ingredients)+world.CookingInputTotal;
            Click("cook.delete");
            Assert.That(order.Exists,Is.False);Assert.That(order.OwnerId,Is.Null);
            Assert.That(order.InputIngredients,Is.Zero);Assert.That(order.Progress,Is.Zero);
            Assert.That(world.ResourceTotal(ResourceKind.Ingredients,false)+world.ResourceTotal(ResourceKind.Ingredients,true)
                +world.CargoTotal(ResourceKind.Ingredients)+world.CookingInputTotal,Is.EqualTo(ingredientTotal));
            yield return null;
            Assert.That(shell.Buttons["cook.toggle"].GetComponentInChildren<Text>().text,Is.EqualTo("주문 재생성"));
            Click("cook.toggle");
            var replacement=world.CookingOrders.Single(o=>o.Exists);
            Assert.That(replacement.Id,Is.Not.EqualTo(order.Id));Assert.That(replacement.Enabled,Is.False);
            Assert.That(replacement.Progress,Is.Zero);Assert.That(replacement.InputIngredients,Is.Zero);
            Click("cook.more");Assert.That(replacement.TargetMeals,Is.EqualTo(13));
            Click("cook.less");Assert.That(replacement.TargetMeals,Is.EqualTo(12));
            Assert.That(world.Tick,Is.EqualTo(savedTick));
        }

        [UnityTest] public IEnumerator PointerDragCreatesExactZonesAndCancelsAcrossUiAndWorldTransitions()
        {
            Setup();runner.SetPaused(true);yield return null;yield return null;
            var savedSlot=SaveDirect("입력 복원");
            var world=runner.Session.World;
            var previousMode=InputSystem.settings.updateMode;
            var previousBackground=InputSystem.settings.backgroundBehavior;
            var previousEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            var module=eventRoot.AddComponent<InputSystemUIInputModule>();
            Mouse mouse=null;shell.enabled=false;shell.MapInput.enabled=false;
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
                mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();
                Action<Vector2,bool> pump=(point,down)=>{
                    var input=new MouseState{position=point};if(down)input=input.WithButton(MouseButton.Left);
                    InputSystem.QueueStateEvent(mouse,input);InputSystem.Update();mouse.MakeCurrent();
                    module.Process();shell.MapInput.SendMessage("Update");Canvas.ForceUpdateCanvases();
                };
                var first=P(3,3);var last=P(4,4);
                var firstScreen=shell.Map.GridToScreen(first);var lastScreen=shell.Map.GridToScreen(last);
                Assert.That(shell.MapInput.IsPointerOverUI(firstScreen),Is.False);
                Assert.That(shell.MapInput.IsPointerOverUI(lastScreen),Is.False);
                var menu=shell.Buttons["tab.menu"];
                var ui=RectTransformUtility.WorldToScreenPoint(null,menu.transform.TransformPoint(((RectTransform)menu.transform).rect.center));
                Assert.That(shell.MapInput.IsPointerOverUI(ui),Is.True);
                foreach(var tool in new[]{SettlementShell.Tool.Storage,SettlementShell.Tool.Growing})
                {
                Func<int> count=()=>tool==SettlementShell.Tool.Storage?world.StorageZones.Count(z=>z.Exists):world.GrowingZones.Count(z=>z.Exists);
                Func<GridPosition[]> cells=()=>tool==SettlementShell.Tool.Storage
                    ?world.StorageZones.Single(z=>z.Exists).Slots.Select(s=>s.Position).ToArray()
                    :world.GrowingZones.Single(z=>z.Exists).Cells.Where(c=>c.Exists).Select(c=>c.Position).ToArray();
                Action delete=()=>{
                    var removed=tool==SettlementShell.Tool.Storage
                        ?runner.DeleteStorage(world.StorageZones.Single(z=>z.Exists).Id)
                        :runner.DeleteGrowingZone(world.GrowingZones.Single(z=>z.Exists).Id);
                    Assert.That(removed,Is.True);
                };
                foreach(var endpoint in new[]{ui,new Vector2(-100,-100)})
                {
                    pump(firstScreen,false);shell.BeginTool(tool);
                    var before=CurrentSaveBytes();
                    pump(firstScreen,true);Assert.That(shell.MapInput.IsCapturing,Is.True);
                    pump(endpoint,true);pump(endpoint,false);
                    Assert.That(shell.MapInput.IsCapturing,Is.False);
                    Assert.That(shell.IsMenuOpen,Is.False,"A drag entering the menu button must not click it.");
                    CollectionAssert.AreEqual(before,CurrentSaveBytes());
                    Assert.That(count(),Is.Zero);
                }
                foreach(var reverse in new[]{false,true})
                {
                    pump(firstScreen,false);shell.BeginTool(tool);
                    var before=CurrentSaveBytes();
                    pump(reverse?lastScreen:firstScreen,true);
                    pump(reverse?firstScreen:lastScreen,true);
                    CollectionAssert.AreEqual(before,CurrentSaveBytes(),"Dragging preview must not change game state.");
                    pump(reverse?firstScreen:lastScreen,false);
                    CollectionAssert.AreEquivalent(new[]{P(3,3),P(4,3),P(3,4),P(4,4)},cells());
                    Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));
                    pump(lastScreen,false);Assert.That(count(),Is.EqualTo(1));
                    delete();
                }
                pump(firstScreen,false);shell.BeginTool(tool);
                pump(firstScreen,true);pump(firstScreen,false);
                CollectionAssert.AreEqual(new[]{first},cells());
                Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));
                delete();
                foreach(var interruption in new[]{"menu","focus","reset","load"})
                {
                    pump(firstScreen,false);shell.BeginTool(tool);pump(firstScreen,true);
                    Assert.That(shell.MapInput.IsCapturing,Is.True,interruption+" "+tool);
                    switch(interruption)
                    {
                        case "menu": shell.ShowPauseMenu();shell.CloseMenu();break;
                        case "focus":
                            shell.SendMessage("OnApplicationFocus",false);shell.MapInput.SendMessage("OnApplicationFocus",false);
                            shell.SendMessage("OnApplicationFocus",true);shell.MapInput.SendMessage("OnApplicationFocus",true);break;
                        case "reset": runner.ResetWorld();shell.SendMessage("LateUpdate");break;
                        case "load":
                            shell.ShowLoadMenu();Click("load."+savedSlot);Click("load.confirm");break;
                    }
                    world=runner.Session.World;
                    Assert.That(shell.MapInput.IsCapturing,Is.False,interruption+" "+tool);
                    var afterTransition=CurrentSaveBytes();
                    // Re-arm while the old press is still held: the old release must stay suppressed.
                    shell.BeginTool(tool);pump(lastScreen,false);
                    CollectionAssert.AreEqual(afterTransition,CurrentSaveBytes(),interruption+" stale release "+tool);
                    Assert.That(count(),Is.Zero);
                    pump(firstScreen,true);pump(firstScreen,false);
                    Assert.That(count(),Is.EqualTo(1),interruption+" fresh press "+tool);
                    CollectionAssert.AreEqual(new[]{first},cells());delete();
                }
                }
                Assert.That(world.Tick,Is.Zero);Assert.That(world.PendingCommandCount,Is.Zero);
                Assert.That(world.Resources.Single(r=>r.Id==101).Amount,Is.EqualTo(20));
            }
            finally
            {
                module.enabled=false;UnityEngine.Object.Destroy(module);
                if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
                InputSystem.settings.updateMode=previousMode;
                InputSystem.settings.backgroundBehavior=previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode=previousEditor;
                shell.enabled=true;shell.MapInput.enabled=true;
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ZoneAndGatherCommandsUpdateImmediatelyWithoutAdvancingPausedWorld()
        {
            Setup();
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,P(1,1))},new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(3,2))},
                gatherables:new[]{new GatherSeed(301,GatherKind.Tree,P(1,5))}));
            runner.SetPaused(true);yield return null;yield return null;
            var world=runner.Session.World;
            Click("tab.build");Click("architect.orders");Click("tool.gather");shell.HandleMapClick(P(1,5),false);
            Assert.That(world.Gatherables[0].Designated,Is.True);
            shell.CancelTool();shell.SelectObject(301);Click("gather.toggle");
            Assert.That(world.Gatherables[0].Designated,Is.False);
            Assert.That(world.Gatherables[0].Progress,Is.Zero);
            Click("tab.build");Click("architect.zones");Click("tool.storage");shell.HandleRectangle(P(4,3),P(3,2));
            var storage=world.StorageZones.Single(z=>z.Exists);
            Assert.That(storage.Slots.Count,Is.EqualTo(4));
            Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));
            shell.SelectObject(storage.Id);Click("selection.detail");
            foreach(ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
            {
                var previous=storage.Allows(kind);Click("filter."+kind);
                Assert.That(storage.Allows(kind),Is.EqualTo(!previous));
                yield return null;
                Assert.That(shell.Buttons["filter."+kind].GetComponentInChildren<Text>().text,
                    Does.EndWith(previous?"차단":"허용"));
                Click("filter."+kind);Assert.That(storage.Allows(kind),Is.EqualTo(previous));
            }
            Click("delete.storage");Assert.That(storage.Exists,Is.False);
            Assert.That(world.Resources.Single(r=>r.Id==101).Amount,Is.EqualTo(20));
            Click("tab.build");Click("architect.zones");Click("tool.growing");shell.HandleRectangle(P(6,5),P(5,4));
            var growing=world.GrowingZones.Single(z=>z.Exists);
            Assert.That(growing.Cells.Count,Is.EqualTo(4));
            Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));
            shell.SelectObject(growing.Id);Click("sowing");
            Assert.That(growing.SowingEnabled,Is.False);yield return null;
            Assert.That(shell.Buttons["sowing"].GetComponentInChildren<Text>().text,Does.StartWith("파종 중지"));
            Click("sowing");Assert.That(growing.SowingEnabled,Is.True);
            Click("delete.growing");Assert.That(growing.Exists,Is.False);
            Assert.That(growing.Cells.All(c=>!c.Exists),Is.True);
            Assert.That(world.Resources.Single(r=>r.Id==101).Amount,Is.EqualTo(20));
            Assert.That(world.Tick,Is.Zero);
        }

        [UnityTest] public IEnumerator ConstructionButtonsCancelDeliveredMaterialsAndToggleDemolitionWithoutTick()
        {
            Setup();runner.SetPaused(true);yield return null;
            shell.OpenTab("build");Click("build.Wall");shell.HandleMapClick(P(4,4),false);
            var world=runner.Session.World;var blueprint=world.Blueprints.Single(b=>b.Exists);
            Assert.That(shell.SelectedObjectId,Is.EqualTo(blueprint.Id));
            Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));
            for(var tick=0;tick<300&&blueprint.DeliveredWood==0;tick++)runner.Session.Step(1);
            Assert.That(blueprint.Exists,Is.True);Assert.That(blueprint.DeliveredWood,Is.EqualTo(2));
            var savedTick=world.Tick;Click("cancel.blueprint");
            Assert.That(blueprint.Exists,Is.False);Assert.That(blueprint.OwnerId,Is.Null);
            Assert.That(world.ResourceTotal(ResourceKind.Wood,false)+world.ResourceTotal(ResourceKind.Wood,true)+world.CargoTotal(ResourceKind.Wood),Is.EqualTo(20));
            Assert.That(world.Tick,Is.EqualTo(savedTick));
            Click("tab.build");Click("architect.structure");Click("build.Wall");shell.HandleMapClick(P(5,4),false);
            Assert.That(world.Blueprints.Count(b=>b.Exists),Is.EqualTo(1),"New site must avoid returned materials.");
            for(var tick=0;tick<300&&!world.Buildings.Any(b=>b.Exists);tick++)runner.Session.Step(1);
            var building=world.Buildings.Single(b=>b.Exists);savedTick=world.Tick;
            shell.SelectObject(building.Id);Click("demolish");
            Assert.That(building.DemolitionDesignated,Is.True);yield return null;
            Assert.That(shell.Buttons["demolish"].GetComponentInChildren<Text>().text,Is.EqualTo("철거 취소"));
            Click("demolish");Assert.That(building.DemolitionDesignated,Is.False);
            Assert.That(building.Exists,Is.True);Assert.That(world.Tick,Is.EqualTo(savedTick));
        }

        [UnityTest] public IEnumerator CommandFeedbackExpiresWhilePausedButActiveToolKeepsInstructions()
        {
            Setup();
            runner.SetPaused(true);
            yield return null;
            shell.SelectPawn(1);
            shell.HandleMapClick(P(5,5), true);
            Assert.That(shell.Feedback, Does.Contain("소집"));
            yield return new WaitForSecondsRealtime(3.3f);
            Assert.That(shell.Feedback, Is.Empty);
            Assert.That(runner.Session.World.Tick, Is.Zero);

            shell.BeginTool(SettlementShell.Tool.Building);
            yield return new WaitForSecondsRealtime(3.3f);
            Assert.That(shell.ActiveTool, Is.EqualTo(SettlementShell.Tool.Building));
            Assert.That(shell.Feedback, Does.Contain("우클릭 취소"));
            shell.CancelTool();
            Assert.That(shell.Feedback, Is.Empty);
        }

        [UnityTest] public IEnumerator DevelopmentPanelStepsOnlyWhilePausedAndResetsThroughTheCurrentShell()
        {
            Setup(developmentUi: true);
            yield return null;
            shell.SelectPawn(1);
            Assert.That(shell.ProcessShortcut(Key.F3), Is.True);
            Assert.That(runner.IsPaused, Is.False);
            Assert.That(shell.Buttons["dev.step"].interactable, Is.False);
            Click("dev.pause");
            yield return null;
            runner.AdvanceTime(1m);
            Assert.That(runner.Session.World.Tick, Is.Zero);
            Click("dev.step");
            Assert.That(runner.Session.World.Tick, Is.EqualTo(1));
            Assert.That(shell.ProcessShortcut(Key.Escape), Is.True);
            Assert.That(shell.SelectedPawnId, Is.EqualTo(1));
            Assert.That(runner.IsPaused, Is.True);
            var oldWorld = runner.Session.World;
            Assert.That(shell.ProcessShortcut(Key.F3), Is.True);
            Click("dev.reset");
            Assert.That(runner.Session.World, Is.Not.SameAs(oldWorld));
            Assert.That(shell.Map.World, Is.SameAs(runner.Session.World));
            Assert.That(runner.Session.World.Tick, Is.Zero);
            Assert.That(runner.Session.ResetCount, Is.EqualTo(1));
            Assert.That(shell.SelectedPawnId, Is.Null);
            Assert.That(shell.Buttons.ContainsKey("dev.step"), Is.False);
        }

        [UnityTest] public IEnumerator MenusBlockPositiveTimeAndRestoreUserPauseAndSpeed()
        {
            Setup();yield return null;
            Assert.That(shell.ProcessShortcut(Key.F3), Is.False);
            foreach(var speed in new[]{1,3})foreach(var paused in new[]{false,true})
            {
                runner.SetSpeed(speed);runner.SetPaused(paused);
                var tick=runner.Session.World.Tick;var remainder=runner.Session.PendingSeconds;
                shell.ShowPauseMenu();Assert.That(runner.IsUiBlocked,Is.True);
                runner.AdvanceTime(1m);
                shell.ShowSaveMenu();runner.AdvanceTime(1m);
                Assert.That(runner.Session.World.Tick,Is.EqualTo(tick));Assert.That(runner.Session.PendingSeconds,Is.EqualTo(remainder));
                Click("cancel");Assert.That(shell.IsMenuOpen,Is.True);runner.AdvanceTime(1m);
                Click("resume");Assert.That(runner.IsUiBlocked,Is.False);
                Assert.That(runner.IsPaused,Is.EqualTo(paused));Assert.That(runner.SpeedMultiplier,Is.EqualTo(speed));
                runner.AdvanceTime(1m);
                Assert.That(runner.Session.World.Tick,Is.EqualTo(tick+(paused?0:10*speed)),"Positive time control after menu close");
            }
        }

        [UnityTest] public IEnumerator LoadConfirmationCancelPreservesSessionTimeSelectionAndActiveSlot()
        {
            Setup();var a=SaveDirect("A",3);var b=SaveDirect("B",9);Assert.That(runner.LoadSlot(a),Is.True);
            yield return null;
            runner.SetSpeed(3);runner.SetPaused(true);shell.SelectPawn(1);
            var original=runner.Session;var pending=original.PendingSeconds;var tick=original.World.Tick;
            shell.ShowLoadMenu();Click("load."+b);runner.AdvanceTime(2m);
            Assert.That(runner.Session,Is.SameAs(original));Assert.That(runner.ActiveSlotId,Is.EqualTo(a));
            Click("cancel");
            Assert.That(runner.Session,Is.SameAs(original));Assert.That(original.World.Tick,Is.EqualTo(tick));
            Assert.That(original.PendingSeconds,Is.EqualTo(pending));Assert.That(runner.IsPaused,Is.True);
            Assert.That(runner.SpeedMultiplier,Is.EqualTo(3));Assert.That(shell.SelectedPawnId,Is.EqualTo(1));
            Assert.That(runner.ActiveSlotId,Is.EqualTo(a));Assert.That(shell.IsMenuOpen,Is.True);
        }

        [UnityTest] public IEnumerator CorruptionAfterOpeningLoadConfirmationDoesNotReplaceLiveSettlement()
        {
            Setup();var a=SaveDirect("A",2);var b=SaveDirect("B",7);Assert.That(runner.LoadSlot(a),Is.True);
            yield return null;
            shell.SelectPawn(1);runner.SetSpeed(3);runner.SetPaused(true);
            shell.Map.Focus(P(5,5));var cameraPosition=shell.Map.MapCamera.transform.position;
            var original=runner.Session;var tick=original.World.Tick;var pending=original.PendingSeconds;
            shell.ShowLoadMenu();Click("load."+b);
            File.WriteAllText(SlotPath(b),"{broken");Click("load.confirm");
            Assert.That(runner.Session,Is.SameAs(original));Assert.That(original.World.Tick,Is.EqualTo(tick));
            Assert.That(original.PendingSeconds,Is.EqualTo(pending));Assert.That(runner.IsPaused,Is.True);
            Assert.That(runner.SpeedMultiplier,Is.EqualTo(3));Assert.That(shell.SelectedPawnId,Is.EqualTo(1));
            Assert.That(shell.Map.MapCamera.transform.position,Is.EqualTo(cameraPosition));
            Assert.That(runner.ActiveSlotId,Is.EqualTo(a));Assert.That(shell.Feedback,Is.Not.Empty);
            Assert.That(shell.IsMenuOpen,Is.True);Assert.That(runner.IsUiBlocked,Is.True);
        }

        [UnityTest] public IEnumerator ConfirmedLoadRestoresSlotStateAndClearsOldSelectionAndTool()
        {
            Setup();var a=SaveDirect("A",2);runner.SetSpeed(3);runner.SetPaused(true);var b=SaveDirect("B",8);
            Assert.That(runner.LoadSlot(a),Is.True);yield return null;
            shell.SelectPawn(1);shell.OpenTab("zone");shell.BeginTool(SettlementShell.Tool.Storage);var previous=runner.Session;
            shell.ShowLoadMenu();Click("load."+b);Click("load.confirm");
            Assert.That(runner.Session,Is.Not.SameAs(previous));Assert.That(runner.Session.World.Tick,Is.EqualTo(10));
            Assert.That(runner.ActiveSlotId,Is.EqualTo(b));Assert.That(runner.IsPaused,Is.True);Assert.That(runner.SpeedMultiplier,Is.EqualTo(3));
            Assert.That(shell.SelectedPawnId,Is.Null);Assert.That(shell.SelectedObjectId,Is.Null);
            Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));Assert.That(shell.IsMenuOpen,Is.False);
            Assert.That(runner.IsUiBlocked,Is.False);Assert.That(shell.Map.World,Is.SameAs(runner.Session.World));
            Assert.That(shell.GetComponentInChildren<Canvas>().transform.Find("Command panel").gameObject.activeSelf,Is.False);
            Assert.That(shell.Feedback,Is.EqualTo(runner.SaveMessage).And.Not.Empty);
            yield return null;
            Assert.That(shell.Feedback,Is.EqualTo(runner.SaveMessage),"Load success survives the following UI refresh.");
        }

        [UnityTest] public IEnumerator NewSettlementFirstSaveCreatesNewSlotWithoutOverwritingPreviousGame()
        {
            Setup();var a=SaveDirect("A",4);var originalBytes=File.ReadAllBytes(SlotPath(a));
            shell.BeginTool(SettlementShell.Tool.Building);
            Assert.That(shell.Feedback,Is.Not.Empty);
            shell.ShowPauseMenu();Click("new");Click("transition.discard");
            Assert.That(runner.ActiveSlotId,Is.Null);Assert.That(runner.Session.World.Tick,Is.Zero);Assert.That(shell.IsMenuOpen,Is.False);
            Assert.That(shell.Feedback,Is.Empty);
            shell.ShowSaveMenu();shell.SaveNameInput.text="새 정착지";Click("save.new");
            var b=runner.ActiveSlotId;Assert.That(b,Is.Not.Null.And.Not.EqualTo(a));
            CollectionAssert.AreEqual(originalBytes,File.ReadAllBytes(SlotPath(a)));
            Assert.That(runner.Slots.ListSlots().Slots.Count,Is.EqualTo(2));yield return null;
        }

        [UnityTest] public IEnumerator LegacyLoadThenSaveAndExitCreatesNamedSlotWithoutChangingOriginal()
        {
            Setup();var existing=SaveDirect("다른 저장",2);
            var existingBytes=File.ReadAllBytes(SlotPath(existing));
            var fixture=Path.Combine(Application.dataPath,"Game","Tests","Fixtures","legacy-v1.json");
            var legacy=Path.Combine(directory,"Saves","settlement-v1.json");
            var legacyBytes=File.ReadAllBytes(fixture);File.WriteAllBytes(legacy,legacyBytes);
            shell.ShowLoadMenu();Click("load."+SaveRepository.LegacySlotId);Click("load.confirm");
            Assert.That(runner.ActiveSlotId,Is.Null);
            Assert.That(runner.Settings.RecentSlotId,Is.EqualTo(SaveRepository.LegacySlotId));
            var state=CurrentSaveBytes();var tick=runner.Session.World.Tick;
            shell.ShowPauseMenu();Click("exit");Click("transition.save");
            Assert.That(shell.Buttons.ContainsKey("overwrite."+SaveRepository.LegacySlotId),Is.False);
            Assert.That(shell.SaveNameInput.text,Is.EqualTo("우리 정착지"));
            shell.SaveNameInput.text="기존 정착지 이어서";Click("save.new");
            var id=runner.ActiveSlotId;
            Assert.That(SaveRepository.IsSlotId(id),Is.True);Assert.That(id,Is.Not.EqualTo(existing));
            Assert.That(shell.ExitRequested,Is.True);Assert.That(runner.Settings.RecentSlotId,Is.EqualTo(id));
            CollectionAssert.AreEqual(state,CurrentSaveBytes());
            CollectionAssert.AreEqual(legacyBytes,File.ReadAllBytes(legacy));
            CollectionAssert.AreEqual(legacyBytes,File.ReadAllBytes(fixture));
            CollectionAssert.AreEqual(existingBytes,File.ReadAllBytes(SlotPath(existing)));
            var reopened=new SaveRepository(Path.Combine(directory,"Saves"));
            Assert.That(reopened.ListSlots().Slots.Count,Is.EqualTo(3));
            var loaded=reopened.Load(id);Assert.That(loaded.Success,Is.True,loaded.ErrorCode);
            Assert.That(loaded.Slot.DisplayName,Is.EqualTo("기존 정착지 이어서"));Assert.That(loaded.Slot.Tick,Is.EqualTo(tick));
            yield return null;
        }

        [UnityTest] public IEnumerator TransitionCancelAndOverwriteCancelPreserveProgressBeforeExplicitDiscard()
        {
            Setup();var a=SaveDirect("전환 기준",4);var original=File.ReadAllBytes(SlotPath(a));
            foreach(var transition in new[]{"new","main","exit"})
            {
                if(transition!="new")
                {
                    var needsConfirmation=shell.HasSettlement;
                    shell.ShowLoadMenu();Click("load."+a);if(needsConfirmation)Click("load.confirm");
                }
                runner.SetSpeed(3);runner.SetPaused(true);shell.SelectPawn(1);
                var session=runner.Session;var state=CurrentSaveBytes();
                shell.ShowPauseMenu();Click(transition);Click("cancel");Click("resume");
                Assert.That(shell.IsMenuOpen,Is.False);Assert.That(shell.HasSettlement,Is.True);
                Assert.That(runner.Session,Is.SameAs(session));Assert.That(runner.ActiveSlotId,Is.EqualTo(a));
                Assert.That(shell.SelectedPawnId,Is.EqualTo(1));CollectionAssert.AreEqual(state,CurrentSaveBytes());
                shell.ShowPauseMenu();Click(transition);Click("transition.save");Click("overwrite."+a);
                Click("cancel");Click("cancel");Click("resume");
                Assert.That(shell.IsMenuOpen,Is.False);Assert.That(shell.ExitRequested,Is.False);
                Assert.That(runner.Session,Is.SameAs(session));Assert.That(runner.ActiveSlotId,Is.EqualTo(a));
                Assert.That(shell.SelectedPawnId,Is.EqualTo(1));CollectionAssert.AreEqual(state,CurrentSaveBytes());
                CollectionAssert.AreEqual(original,File.ReadAllBytes(SlotPath(a)));
                shell.ShowPauseMenu();Click(transition);Click("transition.discard");
                if(transition=="new")
                {
                    Assert.That(runner.Session,Is.Not.SameAs(session));Assert.That(runner.ActiveSlotId,Is.Null);
                    Assert.That(runner.Session.World.Tick,Is.Zero);Assert.That(shell.HasSettlement,Is.True);
                }
                else if(transition=="main")
                {
                    Assert.That(shell.HasSettlement,Is.False);Assert.That(shell.IsMenuOpen,Is.True);
                    Assert.That(runner.IsUiBlocked,Is.True);Assert.That(shell.ExitRequested,Is.False);
                }
                else Assert.That(shell.ExitRequested,Is.True);
                CollectionAssert.AreEqual(original,File.ReadAllBytes(SlotPath(a)));
                Assert.That(runner.Slots.ListSlots().Slots.Count,Is.EqualTo(1));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator SaveNameRemainsLiteralTextInInputAndSlotCaptions()
        {
            Setup();const string name="<b>A</b> <size=0>B";
            shell.ShowSaveMenu();shell.SaveNameInput.text=name;
            Assert.That(shell.SaveNameInput.textComponent.supportRichText,Is.False);
            Click("save.new");var id=runner.ActiveSlotId;Assert.That(id,Is.Not.Null);
            shell.ShowLoadMenu();var label=shell.Buttons["load."+id].GetComponentInChildren<Text>();
            Assert.That(label.text,Does.Contain(name));Assert.That(label.supportRichText,Is.False);
            Assert.That(runner.Slots.Load(id).Slot.DisplayName,Is.EqualTo(name));yield return null;
        }

        [UnityTest] public IEnumerator BuildingPointerPreviewRejectsOccupiedCellAndCancelNeverMovesDraftedPawn()
        {
            Setup();runner.SetPaused(true);yield return null;yield return null;
            runner.ToggleDraft(1);shell.SelectPawn(1);
            var mode=InputSystem.settings.updateMode;var background=InputSystem.settings.backgroundBehavior;
            var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            Mouse mouse=null;shell.enabled=false;shell.MapInput.enabled=false;
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
                mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
                Action<Vector2,int,bool> pump=(point,button,escape)=>{
                    var state=new MouseState{position=point};
                    if(button==1)state=state.WithButton(MouseButton.Left);
                    if(button==2)state=state.WithButton(MouseButton.Right);
                    InputSystem.QueueStateEvent(mouse,state);
                    InputSystem.QueueStateEvent(keyboard,escape?new KeyboardState(Key.Escape):new KeyboardState());
                    InputSystem.Update();mouse.MakeCurrent();keyboard.MakeCurrent();
                    shell.SendMessage("Update");shell.MapInput.SendMessage("Update");shell.SendMessage("LateUpdate");
                };
                foreach(var kind in new[]{BuildingKind.Wall,BuildingKind.Door,BuildingKind.Bed,BuildingKind.Stove})
                {
                    var valid=P(4,4);var occupied=P(3,2);
                    var point=shell.Map.GridToScreen(valid);var blocked=shell.Map.GridToScreen(occupied);
                    Assert.That(shell.MapInput.IsPointerOverUI(point),Is.False);
                    Assert.That(shell.MapInput.IsPointerOverUI(blocked),Is.False);
                    pump(point,0,false);shell.BeginTool(SettlementShell.Tool.Building,kind);
                    var before=CurrentSaveBytes(ignoreCommandError:true);pump(point,0,false);
                    Assert.That(shell.Feedback,Does.StartWith("지정 가능"),kind.ToString());
                    CollectionAssert.AreEqual(before,CurrentSaveBytes(ignoreCommandError:true),"Valid hover "+kind);
                    pump(blocked,0,false);Assert.That(shell.Feedback,Does.StartWith("지정 불가"));
                    CollectionAssert.AreEqual(before,CurrentSaveBytes(ignoreCommandError:true),"Invalid hover "+kind);
                    pump(blocked,1,false);pump(blocked,0,false);
                    Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.Building));
                    Assert.That(runner.Session.World.LastCommandError,Is.Not.Empty);
                    CollectionAssert.AreEqual(before,CurrentSaveBytes(ignoreCommandError:true),"Rejected placement "+kind);
                    pump(point,2,false);
                    Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));
                    CollectionAssert.AreEqual(before,CurrentSaveBytes(ignoreCommandError:true),"Right cancel must not move drafted pawn "+kind);
                    pump(point,0,false);shell.BeginTool(SettlementShell.Tool.Building,kind);pump(point,0,true);
                    Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));
                    Assert.That(shell.IsMenuOpen,Is.False);
                    CollectionAssert.AreEqual(before,CurrentSaveBytes(ignoreCommandError:true),"Escape cancel "+kind);
                    pump(point,0,false);shell.BeginTool(SettlementShell.Tool.Building,kind);
                    pump(point,1,false);pump(point,0,false);pump(point,0,false);
                    var world=runner.Session.World;var blueprint=world.Blueprints.Single(b=>b.Exists);
                    Assert.That(blueprint.Kind,Is.EqualTo(kind));Assert.That(blueprint.Position,Is.EqualTo(valid));
                    Assert.That(shell.ActiveTool,Is.EqualTo(SettlementShell.Tool.None));
                    Assert.That(world.Tick,Is.Zero);Assert.That(world.PendingCommandCount,Is.Zero);
                    Assert.That(world.Resources.Single(r=>r.Id==101).Amount,Is.EqualTo(20));
                    Assert.That(runner.CancelBlueprint(blueprint.Id),Is.True);shell.SelectPawn(1);
                }
            }
            finally
            {
                if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
                if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);keyboard=null;
                InputSystem.settings.updateMode=mode;InputSystem.settings.backgroundBehavior=background;
                InputSystem.settings.editorInputBehaviorInPlayMode=editor;shell.enabled=true;shell.MapInput.enabled=true;
            }
            yield return null;
        }

        [UnityTest] public IEnumerator HeldKeysRequireFreshPressAfterMenuFocusResetAndLoad()
        {
            Setup();runner.SetPaused(true);yield return null;yield return null;
            var slot=SaveDirect("키 입력 복원");
            var mode=InputSystem.settings.updateMode;
            var background=InputSystem.settings.backgroundBehavior;
            var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            shell.enabled=false;shell.MapInput.enabled=false;
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
                keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
                Action<KeyboardState> pump=state=>{
                    InputSystem.QueueStateEvent(keyboard,state);InputSystem.Update();keyboard.MakeCurrent();
                    shell.SendMessage("Update");shell.MapInput.SendMessage("Update");
                };
                foreach(var interruption in new[]{"menu","focus","reset","load"})
                {
                    pump(new KeyboardState());shell.SelectPawn(1);
                    shell.Map.Zoom(20,shell.Map.GridToScreen(P(3,3)));shell.Map.Focus(P(3,3));
                    var initial=shell.Map.MapCamera.transform.position;
                    var drafted=runner.Session.World.Colonists[0].IsDrafted;
                    pump(new KeyboardState(Key.W,Key.R));
                    Assert.That(shell.Map.MapCamera.transform.position.y,Is.GreaterThan(initial.y),interruption+" initial W");
                    Assert.That(runner.Session.World.Colonists[0].IsDrafted,Is.EqualTo(!drafted),interruption+" initial R");
                    switch(interruption)
                    {
                        case "menu":shell.ShowPauseMenu();shell.CloseMenu();break;
                        case "focus":shell.SendMessage("OnApplicationFocus",false);shell.MapInput.SendMessage("OnApplicationFocus",false);
                            shell.SendMessage("OnApplicationFocus",true);shell.MapInput.SendMessage("OnApplicationFocus",true);break;
                        case "reset":runner.ResetWorld();shell.SendMessage("LateUpdate");break;
                        case "load":shell.ShowLoadMenu();Click("load."+slot);Click("load.confirm");break;
                    }
                    if(!string.IsNullOrEmpty(runner.SettingsWarning)&&shell.Buttons.ContainsKey("warning.ok"))Click("warning.ok");
                    shell.SelectPawn(1);shell.Map.Zoom(20,shell.Map.GridToScreen(P(3,3)));shell.Map.Focus(P(3,3));
                    var position=shell.Map.MapCamera.transform.position;var before=CurrentSaveBytes();
                    pump(new KeyboardState(Key.W,Key.R));pump(new KeyboardState(Key.W,Key.R));
                    Assert.That(shell.Map.MapCamera.transform.position,Is.EqualTo(position),interruption+" stale hold");
                    CollectionAssert.AreEqual(before,CurrentSaveBytes(),interruption+" stale hold");
                    pump(new KeyboardState());
                    Assert.That(shell.Map.MapCamera.transform.position,Is.EqualTo(position),interruption+" release");
                    CollectionAssert.AreEqual(before,CurrentSaveBytes(),interruption+" release");
                    drafted=runner.Session.World.Colonists[0].IsDrafted;
                    pump(new KeyboardState(Key.W,Key.R));
                    Assert.That(shell.Map.MapCamera.transform.position.y,Is.GreaterThan(position.y),interruption+" fresh W");
                    Assert.That(runner.Session.World.Colonists[0].IsDrafted,Is.EqualTo(!drafted),interruption+" fresh R");
                    pump(new KeyboardState(Key.W,Key.R));
                    Assert.That(runner.Session.World.Colonists[0].IsDrafted,Is.EqualTo(!drafted),interruption+" no repeat R");
                }
                pump(new KeyboardState());
            }
            finally
            {
                if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);keyboard=null;
                InputSystem.settings.updateMode=mode;InputSystem.settings.backgroundBehavior=background;
                InputSystem.settings.editorInputBehaviorInPlayMode=editor;shell.enabled=true;shell.MapInput.enabled=true;
            }
            yield return null;
        }

        [UnityTest] public IEnumerator FocusedNameFieldBlocksKeyboardShortcutsAndMapCameraMovement()
        {
            Setup();shell.SelectPawn(1);
            runner.SetSpeed(3);runner.SetPaused(true);shell.ShowSaveMenu();
            EventSystem.current.SetSelectedGameObject(shell.SaveNameInput.gameObject);
            Assert.That(shell.IsInputBlocked,Is.True);
            var position=shell.Map.MapCamera.transform.position;var tick=runner.Session.World.Tick;
            var previousMode=InputSystem.settings.updateMode;
            var previousBackground=InputSystem.settings.backgroundBehavior;
            var previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            var shellEnabled=shell.enabled;var mapInputEnabled=shell.MapInput.enabled;
            shell.enabled=false;shell.MapInput.enabled=false;
            try
            {
                // Batch tests do not have a focused Game View. Route synthetic events
                // to the game explicitly, then restore all global settings below.
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
                // Device creation must happen after the focus policy is configured:
                // a device added while the batch editor is unfocused can start disabled.
                keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
                Assert.That(keyboard.enabled,Is.True,"Synthetic keyboard must be enabled under the test-only focus policy.");
                Action pump=()=>{InputSystem.Update();keyboard.MakeCurrent();shell.SendMessage("Update");shell.MapInput.SendMessage("Update");};
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());pump();
                var inputField=shell.SaveNameInput;var committed=inputField.text;
                var composition=string.Empty;var compositionEvents=0;
                keyboard.onIMECompositionChange+=value=>{composition=value.ToString();compositionEvents++;};
                foreach(var key in new[]{Key.Escape,Key.Enter})
                {
                    var composing=IMECompositionEvent.Create(keyboard.deviceId,"한",InputState.currentTime);
                    InputSystem.QueueEvent(ref composing);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));pump();
                    Assert.That(composition,Is.EqualTo("한"));Assert.That(keyboard[key].wasPressedThisFrame,Is.True);
                    Assert.That(shell.IsMenuOpen,Is.True);Assert.That(shell.SaveNameInput,Is.SameAs(inputField));
                    Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(inputField.gameObject));
                    Assert.That(inputField.text,Is.EqualTo(committed),"Device composition does not mutate committed text in this routing-only test.");
                    Assert.That(runner.Slots.ListSlots().Slots.Count,Is.Zero);
                    Assert.That(shell.ExitRequested,Is.False);
                    var ended=IMECompositionEvent.Create(keyboard.deviceId,string.Empty,InputState.currentTime);
                    InputSystem.QueueEvent(ref ended);InputSystem.QueueStateEvent(keyboard,new KeyboardState());pump();
                    Assert.That(composition,Is.Empty);
                }
                Assert.That(compositionEvents,Is.EqualTo(4),"Both composition start/end events must actually reach the keyboard.");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R,Key.Space,Key.Digit1,Key.Digit3,Key.W));pump();
                foreach(var key in new[]{Key.R,Key.Space,Key.Digit1,Key.Digit3,Key.W})
                    Assert.That(keyboard[key].isPressed,Is.True,"Input must be delivered before checking blocked command: "+key+
                        "; enabled="+keyboard.enabled+"; current="+(Keyboard.current==keyboard)+"; background="+InputSystem.settings.backgroundBehavior+
                        "; editor="+InputSystem.settings.editorInputBehaviorInPlayMode);
                Assert.That(keyboard.spaceKey.wasPressedThisFrame,Is.True);
                Assert.That(runner.Session.World.Colonists[0].IsDrafted,Is.False);Assert.That(runner.IsPaused,Is.True);
                Assert.That(runner.SpeedMultiplier,Is.EqualTo(3));Assert.That(runner.Session.World.Tick,Is.EqualTo(tick));
                Assert.That(shell.Map.MapCamera.transform.position,Is.EqualTo(position));Assert.That(shell.IsMenuOpen,Is.True);
                Assert.That(shell.ProcessShortcut(Key.Space),Is.False,"Focused name field rejects command dispatch too.");

                InputSystem.QueueStateEvent(keyboard,new KeyboardState());pump();
                shell.CloseMenu();Assert.That(shell.IsInputBlocked,Is.False);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R,Key.Space));pump();
                Assert.That(keyboard.rKey.isPressed,Is.True);Assert.That(keyboard.spaceKey.wasPressedThisFrame,Is.True);
                Assert.That(runner.Session.World.Colonists[0].IsDrafted,Is.True,"The same real Update route must work after input focus closes.");
                Assert.That(runner.IsPaused,Is.False,"Space must toggle through the delivered keyboard event after menu closes.");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());pump();
            }
            finally
            {
                if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
                keyboard=null;
                InputSystem.settings.updateMode=previousMode;
                InputSystem.settings.backgroundBehavior=previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
                shell.enabled=shellEnabled;shell.MapInput.enabled=mapInputEnabled;
            }
            yield return null;
        }

        private SaveFileService SlotFileService()
        {
            // Reach the existing fault seam without exposing test injection on the runtime API.
            var field=typeof(SaveRepository).GetField("files",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(field,Is.Not.Null);
            return (SaveFileService)field.GetValue(runner.Slots);
        }
        private byte[] CurrentSaveBytes(bool ignoreCommandError=false)
        {
            var capture=runner.Session.CaptureSaveData(runner.IsPaused,runner.SpeedMultiplier);
            Assert.That(capture.Success,Is.True,capture.ErrorCode);
            if(ignoreCommandError)capture.Data.World.LastCommandError=string.Empty;
            using(var stream=new MemoryStream())
            {
                new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(SaveData)).WriteObject(stream,capture.Data);
                return stream.ToArray();
            }
        }

        [UnityTest] public IEnumerator AtomicStageFailuresPreserveLiveStateAndDoNotExitOrLeakSlots()
        {
            Setup();var a=SaveDirect("기존",2);var original=File.ReadAllBytes(SlotPath(a));
            runner.Session.Step(3);runner.SetSpeed(3);shell.SelectPawn(1);
            var session=runner.Session;var world=session.World;
            var files=SlotFileService();
            foreach(var paused in new[]{false,true})
            foreach(var stage in new[]{"Write","ReadBack","Replace"})
            {
                runner.SetPaused(paused);var state=CurrentSaveBytes();var hits=0;
                files.BeforeAtomicStage=current=>{if(current==stage){hits++;throw new IOException("Injected "+stage);}};
                try
                {
                    shell.ShowPauseMenu();Click("exit");Click("transition.save");Click("overwrite."+a);Click("overwrite.confirm");
                    Assert.That(hits,Is.EqualTo(1),stage);
                    Assert.That(shell.ExitRequested,Is.False);Assert.That(shell.IsMenuOpen,Is.True);
                    Assert.That(shell.SelectedPawnId,Is.EqualTo(1));
                    Assert.That(runner.Session,Is.SameAs(session));Assert.That(runner.Session.World,Is.SameAs(world));
                    Assert.That(runner.ActiveSlotId,Is.EqualTo(a));Assert.That(runner.SaveBusy,Is.False);
                    CollectionAssert.AreEqual(state,CurrentSaveBytes());
                    Assert.That(runner.SaveSlot("실패 신규"),Is.False);
                    Assert.That(hits,Is.EqualTo(2),stage);
                    CollectionAssert.AreEqual(state,CurrentSaveBytes());
                    CollectionAssert.AreEqual(original,File.ReadAllBytes(SlotPath(a)));
                    Assert.That(runner.Slots.ListSlots().Slots.Count,Is.EqualTo(1));
                    Assert.That(Directory.GetFiles(Path.Combine(directory,"Saves"),"*.tmp"),Is.Empty);
                }
                finally { files.BeforeAtomicStage=null; }
            }
            yield return null;
        }

        [UnityTest] public IEnumerator NestedRequestsAtEverySaveStageCannotReplaceWorldSaveAgainOrExit()
        {
            Setup();var a=SaveDirect("기존",2);var original=File.ReadAllBytes(SlotPath(a));
            runner.Session.Step(3);runner.SetSpeed(3);shell.SelectPawn(1);
            var session=runner.Session;var world=session.World;var state=CurrentSaveBytes();
            shell.ShowPauseMenu();Click("exit");
            var exitButton=shell.Buttons["transition.discard"];
            var files=SlotFileService();var stages=new System.Collections.Generic.List<string>();
            files.BeforeAtomicStage=stage=>{
                stages.Add(stage);Assert.That(runner.SaveBusy,Is.True);
                Assert.That(runner.SaveSlot("중복"),Is.False);
                Assert.That(runner.LoadSlot(a),Is.False);
                runner.ResetWorld();runner.StartNewSettlement();runner.AdvanceTime(10m);
                Click("transition.discard");Click("transition.save");
                Assert.That(shell.ExitRequested,Is.False);
                Assert.That(shell.Buttons["transition.discard"],Is.SameAs(exitButton));
                Assert.That(shell.ProcessShortcut(Key.Escape),Is.False);
                Assert.That(runner.Session,Is.SameAs(session));Assert.That(runner.Session.World,Is.SameAs(world));
                Assert.That(shell.SelectedPawnId,Is.EqualTo(1));
                CollectionAssert.AreEqual(state,CurrentSaveBytes());
            };
            try { Assert.That(runner.SaveSlot("첫 요청"),Is.True,runner.SaveMessage); }
            finally { files.BeforeAtomicStage=null; }
            CollectionAssert.AreEqual(new[]{"Write","ReadBack","Replace"},stages);
            Assert.That(runner.SaveBusy,Is.False);Assert.That(shell.ExitRequested,Is.False);
            Assert.That(runner.Slots.ListSlots().Slots.Count,Is.EqualTo(2));
            CollectionAssert.AreEqual(original,File.ReadAllBytes(SlotPath(a)));
            var saved=runner.Slots.Load(runner.ActiveSlotId);
            Assert.That(saved.Success,Is.True,saved.ErrorCode);Assert.That(saved.Slot.Tick,Is.EqualTo(5));
            Click("transition.discard");Assert.That(shell.ExitRequested,Is.True,"The same button works after the file gate releases.");
            yield return null;
        }

        [UnityTest] public IEnumerator NestedRequestsDuringLoadCannotSaveReplaceRestoredWorldOrExit()
        {
            Setup();var a=SaveDirect("A",2);var b=SaveDirect("B",3);
            var aBytes=File.ReadAllBytes(SlotPath(a));var bBytes=File.ReadAllBytes(SlotPath(b));
            Assert.That(runner.LoadSlot(a),Is.True);shell.SendMessage("LateUpdate");
            var original=runner.Session;
            shell.ShowPauseMenu();Click("exit");
            var discard=shell.Buttons["transition.discard"];
            var field=typeof(UserSettingsStore).GetField("files",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(field,Is.Not.Null);var settingsFiles=(SaveFileService)field.GetValue(runner.SettingsStore);
            var stages=new System.Collections.Generic.List<string>();
            SimulationSession loadedSession=null;byte[] loadedState=null;
            settingsFiles.BeforeAtomicStage=stage=>{
                stages.Add(stage);Assert.That(runner.SaveBusy,Is.True);
                Assert.That(runner.Session,Is.Not.SameAs(original));Assert.That(runner.Session.World.Tick,Is.EqualTo(5));
                Assert.That(runner.ActiveSlotId,Is.EqualTo(b));
                if(loadedSession==null){loadedSession=runner.Session;loadedState=CurrentSaveBytes();}
                Assert.That(runner.SaveSlot("중복 신규"),Is.False);
                Assert.That(runner.SaveSlot("중복 덮어쓰기",a),Is.False);
                Assert.That(runner.LoadSlot(a),Is.False);
                runner.StartNewSettlement();runner.ResetWorld();runner.AdvanceTime(10m);
                Click("transition.discard");Click("transition.save");
                Assert.That(shell.ExitRequested,Is.False);Assert.That(shell.Buttons["transition.discard"],Is.SameAs(discard));
                Assert.That(shell.ProcessShortcut(Key.Escape),Is.False);
                Assert.That(runner.Session,Is.SameAs(loadedSession));CollectionAssert.AreEqual(loadedState,CurrentSaveBytes());
            };
            try { Assert.That(runner.LoadSlot(b),Is.True,runner.SaveMessage); }
            finally { settingsFiles.BeforeAtomicStage=null; }
            CollectionAssert.AreEqual(new[]{"Write","ReadBack","Replace"},stages);
            Assert.That(runner.SaveBusy,Is.False);Assert.That(runner.SettingsWarning,Is.Empty);
            Assert.That(runner.Settings.RecentSlotId,Is.EqualTo(b));Assert.That(runner.Slots.ListSlots().Slots.Count,Is.EqualTo(2));
            CollectionAssert.AreEqual(aBytes,File.ReadAllBytes(SlotPath(a)));CollectionAssert.AreEqual(bBytes,File.ReadAllBytes(SlotPath(b)));
            Assert.That(shell.ExitRequested,Is.False);
            Click("cancel");Click("resume");shell.SendMessage("LateUpdate");
            Assert.That(shell.IsMenuOpen,Is.False);Assert.That(runner.IsUiBlocked,Is.False);
            Assert.That(shell.Map.World,Is.SameAs(loadedSession.World));CollectionAssert.AreEqual(loadedState,CurrentSaveBytes());
            yield return null;
        }

        [UnityTest] public IEnumerator LockedSlotPreservesLiveStateAndSelectionUntilSaveThenExitRetrySucceeds()
        {
            Setup();var a=SaveDirect("잠금 검사",2);var path=SlotPath(a);
            var original=File.ReadAllBytes(path);
            runner.Session.Step(3);runner.SetSpeed(3);runner.SetPaused(true);
            shell.SelectPawn(1);
            var session=runner.Session;var world=session.World;
            var camera=shell.Map.MapCamera.transform.position;var zoom=shell.Map.ZoomSize;
            Func<byte[]> snapshot=()=>{
                var capture=session.CaptureSaveData(runner.IsPaused,runner.SpeedMultiplier);
                Assert.That(capture.Success,Is.True,capture.ErrorCode);
                using(var stream=new MemoryStream())
                {
                    new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(SaveData)).WriteObject(stream,capture.Data);
                    return stream.ToArray();
                }
            };
            var state=snapshot();
            shell.ShowPauseMenu();Click("exit");Click("transition.save");Click("overwrite."+a);
            // Reading remains possible for the list, but Windows cannot replace this open file.
            using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                Click("overwrite.confirm");
                Assert.That(shell.ExitRequested,Is.False);
                Assert.That(shell.IsMenuOpen,Is.True);
                Assert.That(runner.SaveBusy,Is.False,"Failure must release the request gate.");
                Assert.That(runner.Session,Is.SameAs(session));
                Assert.That(runner.Session.World,Is.SameAs(world));
                Assert.That(runner.ActiveSlotId,Is.EqualTo(a));
                Assert.That(shell.SelectedPawnId,Is.EqualTo(1));
                Assert.That(shell.Map.MapCamera.transform.position,Is.EqualTo(camera));
                Assert.That(shell.Map.ZoomSize,Is.EqualTo(zoom));
                CollectionAssert.AreEqual(state,snapshot());
                CollectionAssert.AreEqual(original,File.ReadAllBytes(path));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(path),"*.tmp"),Is.Empty);
                Assert.That(runner.Slots.ListSlots().Slots.Count,Is.EqualTo(1));
                Assert.That(shell.Buttons.ContainsKey("warning.ok"),Is.True);
            }
            Click("warning.ok");Click("overwrite."+a);Click("overwrite.confirm");
            Assert.That(shell.ExitRequested,Is.True,"Unlocked retry must complete the requested transition.");
            Assert.That(runner.SaveBusy,Is.False);
            Assert.That(runner.Slots.ListSlots().Slots.Count,Is.EqualTo(1));
            var restored=new SaveRepository(Path.GetDirectoryName(path)).Load(a);
            Assert.That(restored.Success,Is.True,restored.ErrorCode);
            Assert.That(restored.Slot.Tick,Is.EqualTo(world.Tick));
            Assert.That(restored.Slot.Tick,Is.EqualTo(5));
            CollectionAssert.AreEqual(state,snapshot());
            yield return null;
        }

        [UnityTest] public IEnumerator PendingCommandSaveFailurePreservesSlotAndDoesNotExit()
        {
            Setup();var a=SaveDirect("A");var before=File.ReadAllBytes(SlotPath(a));
            runner.SetPaused(true);runner.Session.World.QueueSetBlocked(P(7,7),true);
            var world=runner.Session.World;var tick=world.Tick;var pending=world.PendingCommandCount;
            shell.ShowPauseMenu();Click("exit");Click("transition.save");
            Click("overwrite."+a);Click("overwrite.confirm");
            Assert.That(shell.ExitRequested,Is.False);Assert.That(shell.IsMenuOpen,Is.True);
            Assert.That(runner.Session.World,Is.SameAs(world));Assert.That(world.Tick,Is.EqualTo(tick));
            Assert.That(world.PendingCommandCount,Is.EqualTo(pending));Assert.That(runner.IsPaused,Is.True);
            CollectionAssert.AreEqual(before,File.ReadAllBytes(SlotPath(a)));
            Assert.That(runner.SaveMessage,Does.Contain("재개"));
            Click("warning.ok");Click("cancel");Click("resume");
            Assert.That(shell.IsMenuOpen,Is.False);Assert.That(runner.IsPaused,Is.True);
            runner.AdvanceTime(1m);
            Assert.That(world.Tick,Is.EqualTo(tick));Assert.That(world.PendingCommandCount,Is.EqualTo(pending));
            Assert.That(shell.ProcessShortcut(Key.Space),Is.True);runner.AdvanceTime(.1m);
            Assert.That(world.Tick,Is.EqualTo(tick+1));Assert.That(world.PendingCommandCount,Is.Zero);
            shell.ShowPauseMenu();Click("exit");Click("transition.save");Click("overwrite."+a);Click("overwrite.confirm");
            Assert.That(shell.ExitRequested,Is.True);
            var saved=runner.Slots.Load(a);Assert.That(saved.Success,Is.True,saved.ErrorCode);
            Assert.That(saved.Slot.Tick,Is.EqualTo(tick+1));
            Assert.That(runner.Slots.ListSlots().Slots.Count,Is.EqualTo(1));
            yield return null;
        }
    }
}
