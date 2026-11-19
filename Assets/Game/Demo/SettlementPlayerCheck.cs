using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using RimworldTweak.Presentation;
using RimworldTweak.Runtime;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using RimworldTweak.Simulation;

namespace RimworldTweak.Demo
{
    // A real Player probe. Commands are scripted: screenshots do not certify physical input.
    public sealed partial class SettlementPlayerCheck : MonoBehaviour
    {
        private SettlementShell shell;
        private SimulationRunner runner;
        private string output,storage,mode;
        private bool finished;
        private Mouse captureMouse;
        private InputSettings.BackgroundBehavior? previousCaptureBackgroundBehavior;
        private readonly Result result=new Result();
        [Serializable] private sealed class LayoutReport
        {
            public string screen;
            public int width, height, scale, checkedButtons, checkedTexts;
            public List<string> issues = new List<string>();
        }
        [Serializable] private sealed class Result
        {public bool passed;public string error,mode,slot,stateHash,continuedStateHash;public long tick;public int width,height,scale;public bool movementCompleted;}
        [Serializable] private sealed class PoseReport
        {public string state,hash;public long tick;public int cargo,renderers;public string life,work;public bool pauseStable,hoverChanged,resumeAdvanced;}
        private static string Arg(string[] args,string key,string fallback)
        {var i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        public void Begin(SimulationRunner runtime,VisualCatalog visuals,string[] args)
        {
            runner=runtime;output=Path.GetFullPath(Arg(args,"--ux-output",""));
            storage=Path.GetFullPath(Arg(args,"--ux-storage",Path.Combine(output,"storage")));
            mode=Arg(args,"--ux-mode","capture");Directory.CreateDirectory(output);
            try
            {
                shell=gameObject.AddComponent<SettlementShell>();shell.Initialize(runner,true,storage,visuals,mode=="raid-integration");
                var scale=int.Parse(Arg(args,"--ux-scale","100"));runner.SetUiScale(scale);
                result.scale=scale;result.mode=mode;StartCoroutine(Run());
            }
            catch(Exception e){Fail(e);}
        }
        private string Hash()
        {
            var capture=runner.Session.CaptureSaveData(runner.IsPaused,runner.SpeedMultiplier);
            if(!capture.Success)throw new InvalidOperationException(capture.ErrorCode);
            using(var data=new MemoryStream())using(var hash=SHA256.Create())
            {new DataContractJsonSerializer(typeof(Simulation.SaveData)).WriteObject(data,capture.Data);return BitConverter.ToString(hash.ComputeHash(data.ToArray())).Replace("-","");}
        }
        private bool Check(Action action){try{action();return true;}catch(Exception e){Fail(e);return false;}}
        private void Fail(Exception e){result.error=e.ToString();Finish(false);}
        private IEnumerator Run()
        {
            // A hidden/batch Player may never advance its splash presentation.
            // Only this explicit verification mode skips it; normal startup is unchanged.
            UnityEngine.Rendering.SplashScreen.Stop(UnityEngine.Rendering.SplashScreen.StopBehavior.StopImmediate);
            var splashDeadline = Time.realtimeSinceStartup + 15;
            while (!UnityEngine.Rendering.SplashScreen.isFinished && Time.realtimeSinceStartup < splashDeadline)
                yield return null;
            if (!UnityEngine.Rendering.SplashScreen.isFinished)
            {
                Fail(new TimeoutException("Splash presentation did not finish."));
                yield break;
            }
            yield return new WaitForSecondsRealtime(2);
            if(mode=="capture")
            {
                yield return Capture("main");
                if(!Check(()=>shell.Buttons["new"].onClick.Invoke()))yield break;
                runner.SetPaused(true);yield return null;
                yield return Capture("default");
                shell.SelectPawn(runner.Session.World.Colonists[0].Id);yield return Capture("selected");
                if(!Check(()=>{
                    var directions=new[]{new GridPosition(0,1),new GridPosition(1,0),new GridPosition(0,-1)};
                    for(var index=0;index<3;index++)
                    {
                        var pawn=runner.Session.World.Colonists[index];
                        runner.ToggleDraft(pawn.Id);
                        if(!runner.MoveColonist(pawn.Id,new GridPosition(pawn.Position.X+directions[index].X,pawn.Position.Y+directions[index].Y)))throw new InvalidOperationException("Direction sample move rejected.");
                    }
                    runner.Session.Step(1);
                }))yield break;
                yield return Capture("pawn-directions");
                shell.OpenTab("work");yield return Capture("work");
                shell.OpenTab("build");shell.BeginTool(SettlementShell.Tool.Building);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var previewCell = new GridPosition(5,5);
                if(!Check(()=>{
                    var world=runner.Session.World;
                    var origin=world.Colonists[0].Position;
                    var left=Math.Max(0,origin.X-14);
                    var bottom=Math.Max(0,origin.Y-14);
                    var candidates=from y in Enumerable.Range(bottom,Math.Min(world.Height,origin.Y+15)-bottom)
                                   from x in Enumerable.Range(left,Math.Min(world.Width,origin.X+15)-left)
                                   let cell=new GridPosition(x,y)
                                   let point=shell.Map.GridToScreen(cell)
                                   where point.x>0&&point.x<Screen.width&&point.y>0&&point.y<Screen.height
                                       &&!shell.MapInput.IsPointerOverUI(point)
                                       &&world.CanPlaceBlueprint(BuildingKind.Wall,cell,out _)
                                   orderby Math.Abs(x-origin.X-4)+Math.Abs(y-origin.Y-4)
                                   select cell;
                    previewCell=candidates.First();
                    previousCaptureBackgroundBehavior=InputSystem.settings.backgroundBehavior;
                    InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                    captureMouse=InputSystem.AddDevice<Mouse>();
                    captureMouse.MakeCurrent();
                    InputSystem.QueueStateEvent(captureMouse,new MouseState {position=shell.Map.GridToScreen(previewCell)});
                    InputSystem.Update();
                    captureMouse.MakeCurrent();
                }))yield break;
                yield return Capture("building");
                if(!Check(()=>{
                    if(shell.MapInput.HoverPosition!=previewCell)
                        throw new InvalidOperationException("Building preview hover "+shell.MapInput.HoverPosition+
                            " expected "+previewCell+"; pointer "+captureMouse.position.ReadValue()+
                            "; expected screen "+shell.Map.GridToScreen(previewCell)+
                            "; enabled "+captureMouse.enabled+"; current "+(Mouse.current==captureMouse)+
                            "; over UI "+shell.MapInput.IsPointerOverUI(captureMouse.position.ReadValue()));
                }))yield break;
                shell.CancelTool();
                if(!Check(PrepareCookingScreen))yield break;
                yield return null;yield return null;
                shell.SelectObject(runner.Session.World.Buildings.Single(b=>b.Exists).Id);
                shell.Buttons["selection.detail"].onClick.Invoke();
                yield return Capture("stove");
                var stoveId=runner.Session.World.Buildings.Single(b=>b.Exists).Id;
                runner.AddCookingBill(stoveId);runner.AddCookingBill(stoveId);
                shell.SelectObject(stoveId);shell.Buttons["selection.detail"].onClick.Invoke();
                yield return Capture("bills-three");
                var firstBill=runner.Session.World.CookingOrders.First(b=>b.Exists&&b.StoveId==stoveId);
                shell.Buttons["cook.rowdetails."+firstBill.Id].onClick.Invoke();
                yield return Capture("bill-conditions");
                shell.ProcessShortcut(Key.Escape);
                shell.HandleMapClick(runner.Session.World.Buildings.Single(b=>b.Exists).Position,true);
                yield return Capture("context-menu");
                shell.ProcessShortcut(Key.Escape);
                var ingredientTotal = shell.GetComponentsInChildren<Text>().Single(t => t.name == "Resource total Ingredients");
                var resourcePoint = RectTransformUtility.WorldToScreenPoint(null,
                    ingredientTotal.rectTransform.TransformPoint(ingredientTotal.rectTransform.rect.center));
                var resourceHash = Hash();
                InputSystem.QueueStateEvent(captureMouse, new MouseState { position = resourcePoint });
                InputSystem.Update();
                captureMouse.MakeCurrent();
                yield return Capture("resource-breakdown");
                if (!Check(() =>
                {
                    var breakdown = shell.GetComponentsInChildren<Text>().Single(t => t.name == "Resource breakdown text");
                    if (breakdown.text != "식재료 합계 8\n바닥 8 · 보관 0\n운반 0 · 작업 투입 0")
                        throw new InvalidOperationException("Unexpected ingredient breakdown: " + breakdown.text);
                    if (shell.MapInput.IsPointerOverUI(resourcePoint) || Hash() != resourceHash)
                        throw new InvalidOperationException("Read-only resource hover blocked the map or changed world state.");
                })) yield break;
                InputSystem.QueueStateEvent(captureMouse, new MouseState { position = Vector2.zero });
                InputSystem.Update();
                captureMouse.MakeCurrent();
                shell.ShowPauseMenu();
                yield return Capture("pause-menu");
                if(!Check(()=>{
                    for (var index = 1; index <= 8; index++)
                        if (!runner.SaveSlot("스크롤 확인 " + index)) throw new InvalidOperationException(runner.SaveMessage);
                    if(!runner.SaveSlot(new string('가',40)))throw new InvalidOperationException(runner.SaveMessage);
                }))yield break;
                shell.ShowSaveMenu();yield return Capture("save");
                var scroll = shell.SaveNameInput.GetComponentInParent<ScrollRect>();
                var cancel = shell.Buttons["cancel"].transform;
                var save = shell.Buttons["save.new"].transform;
                var cancelPosition = cancel.position;
                var savePosition = save.position;
                var startScroll = scroll.content.anchoredPosition;
                scroll.verticalNormalizedPosition = 0;
                yield return Capture("save-scrolled");
                if(!Check(()=>{
                    if (scroll.content.anchoredPosition.y <= startScroll.y ||
                        cancel.position != cancelPosition || save.position != savePosition)
                        throw new InvalidOperationException("Save list must scroll independently of footer actions.");
                }))yield break;
                shell.Buttons["overwrite."+runner.ActiveSlotId].onClick.Invoke();
                yield return Capture("confirmation");
                shell.Buttons["cancel"].onClick.Invoke();
                shell.SaveNameInput.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0;
                shell.SaveNameInput.text=new string('가',41);
                shell.Buttons["save.new"].onClick.Invoke();
                yield return Capture("error");
                shell.CloseMenu();shell.OpenTab(string.Empty);
                if(!Check(PrepareArtScreen))yield break;
                yield return null;yield return null;
                yield return Capture("art-default");
                var artHash=Hash();
                shell.Map.MapCamera.orthographicSize=shell.Map.MinimumZoom;
                yield return Capture("art-near");
                shell.Map.MapCamera.orthographicSize=shell.Map.MaximumZoom;
                yield return Capture("art-far");
                if(!Check(()=>{if(Hash()!=artHash)throw new InvalidOperationException("Art captures changed simulation state.");}))yield break;
                if(!Check(PrepareWallScreen))yield break;
                yield return null;yield return null;
                yield return Capture("walls-sixteen");
                if(!Check(PrepareCropBoundaryScreen))yield break;
                yield return null;yield return null;
                yield return Capture("crop-boundaries");
                foreach(var pose in new[]{"move","carry","sleep","work"})
                {
                    if(!Check(()=>PreparePawnPose(pose)))yield break;
                    yield return null;yield return null;
                    shell.SelectPawn(1);
                    yield return Capture("pose-"+pose+"-before");
                    var stateHash=Hash();var tick=runner.Session.World.Tick;
                    var sprites=shell.Map.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.activeInHierarchy).ToArray();
                    var positions=sprites.Select(r=>r.transform.position).ToArray();
                    var rotations=sprites.Select(r=>r.transform.rotation).ToArray();
                    var pictures=sprites.Select(r=>r.sprite).ToArray();
                    var pause=shell.Buttons["pause"];var normal=pause.targetGraphic.canvasRenderer.GetColor();
                    // Exercise the actual Selectable hover handler while simulation time is paused.
                    ExecuteEvents.Execute(pause.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                    runner.enabled=true;
                    yield return new WaitForSecondsRealtime(.25f);
                    yield return Capture("pose-"+pose+"-paused");
                    runner.enabled=false;
                    if(!Check(()=>{
                        if(Hash()!=stateHash||runner.Session.World.Tick!=tick)throw new InvalidOperationException("Paused pose advanced: "+pose);
                        for(var i=0;i<sprites.Length;i++)
                            if(sprites[i]==null||sprites[i].transform.position!=positions[i]||sprites[i].transform.rotation!=rotations[i]||sprites[i].sprite!=pictures[i])
                                throw new InvalidOperationException("Paused renderer changed: "+pose);
                        var hovered=pause.targetGraphic.canvasRenderer.GetColor();
                        if(hovered==normal)throw new InvalidOperationException("Paused UI hover did not respond: "+pose);
                        var pawn=runner.Session.World.Colonists[0];
                        var report=new PoseReport {state=pose,hash=stateHash,tick=tick,cargo=pawn.CargoAmount,life=pawn.LifeActivity.ToString(),
                            work=pawn.WorkActivity.ToString(),renderers=sprites.Length,pauseStable=true,hoverChanged=true};
                        ExecuteEvents.Execute(pause.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
                        runner.SetPaused(false);runner.AdvanceTime(.1m);runner.SetPaused(true);
                        report.resumeAdvanced=runner.Session.World.Tick==tick+1;
                        if(!report.resumeAdvanced)throw new InvalidOperationException("Resume control did not advance: "+pose);
                        File.WriteAllText(Path.Combine(output,"pose-"+pose+"-state.json"),JsonUtility.ToJson(report,true));
                    }))yield break;
                }
                if(!Check(()=>{
                    runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                        new[]{new ColonistSeed(1,new GridPosition(2,3))},Array.Empty<ResourceSeed>(),
                        new[]{new BedSeed(201,new GridPosition(6,3))},false,
                        new[]{new GatherSeed(301,GatherKind.Tree,new GridPosition(4,3))}));
                    runner.SetPaused(true);
                }))yield break;
                yield return null;yield return null;
                shell.SelectPawn(1);shell.Map.Focus(new GridPosition(4,3));
                var sampleHash=Hash();
                yield return Capture("sample-five");
                if(!Check(()=>{
                    if(Hash()!=sampleHash)throw new InvalidOperationException("Sample capture changed game state.");
                    foreach(var identity in new[]{"pawn.1","bed.201","gather.301"})
                    {
                        var mark = shell.Map.transform.Find("World map content/" + identity);
                        if (mark == null) throw new InvalidOperationException("Missing sample " + identity);
                        var parts = mark.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled && r.sprite != null).ToArray();
                        if (parts.Length == 0) throw new InvalidOperationException("Empty sample " + identity);
                        var bounds = parts[0].bounds;
                        for (var i = 1; i < parts.Length; i++) bounds.Encapsulate(parts[i].bounds);
                        foreach(var corner in new[]{bounds.min,bounds.max,new Vector3(bounds.min.x,bounds.max.y),new Vector3(bounds.max.x,bounds.min.y),bounds.center})
                        {
                            var screen=(Vector2)shell.Map.MapCamera.WorldToScreenPoint(corner);
                            if(screen.x<0||screen.y<0||screen.x>Screen.width||screen.y>Screen.height||shell.MapInput.IsPointerOverUI(screen))
                                throw new InvalidOperationException("Sample hidden by edge or UI: "+identity+" "+screen);
                        }
                    }
                    File.WriteAllText(Path.Combine(output,"sample-five-state.json"),"{\"stateUnchanged\":true,\"spriteBoundsClearOfUi\":true}");
                }))yield break;
                runner.Initialize(WorkScenario.CreateTestMap());runner.SetPaused(true);
                yield return null;yield return null;
                shell.HandleRectangle(new GridPosition(0,0),new GridPosition(2,2));
                shell.Buttons["locate"].onClick.Invoke();
                yield return Capture("group-selection");
                const int longNameId = 1234567890;
                runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                    new[]{new ColonistSeed(longNameId,new GridPosition(3,3))},Array.Empty<ResourceSeed>(), needsEnabled:false));
                runner.SetPaused(true);
                yield return null; yield return null;
                var portrait = (RectTransform)shell.Buttons["pawn." + longNameId].transform;
                var namePoint = RectTransformUtility.WorldToScreenPoint(null, portrait.TransformPoint(portrait.rect.center));
                InputSystem.QueueStateEvent(captureMouse, new MouseState { position = namePoint });
                InputSystem.Update();
                captureMouse.MakeCurrent();
                yield return Capture("portrait-long-name");
                if (!Check(() =>
                {
                    var label = portrait.GetComponentInChildren<Text>();
                    var fullName = shell.GetComponentsInChildren<Text>().Single(text => text.name == "Full name");
                    if (!label.text.EndsWith("…", StringComparison.Ordinal) || !fullName.text.StartsWith("정착민 " + longNameId+"\n", StringComparison.Ordinal))
                        throw new InvalidOperationException("Long portrait name did not truncate and disclose the full value.");
                })) yield break;
            }
            else if(mode=="raid-integration")
            {
                if(!Check(()=>{
                    shell.Buttons["new"].onClick.Invoke();runner.SetPaused(true);
                    var world=runner.Session.World;
                    if(world.Width!=250||world.Height!=250||world.RaidPhase!=RaidPhase.Idle)throw new InvalidOperationException("New settlement baseline");
                    shell.OpenTab("development");shell.Buttons["dev.raid"].onClick.Invoke();
                    if(world.Raiders.Count!=2||world.Raiders.Any(r=>!r.Attack.TargetId.HasValue))throw new InvalidOperationException("Immediate raid targets");
                    shell.HandleMapClick(world.Raiders[0].Position,false);
                    if(shell.SelectedObjectId!=world.Raiders[0].Id)throw new InvalidOperationException("Raider map selection");
                }))yield break;
                yield return Capture("raid-start");
                if(!Check(()=>{
                    if(shell.SelectedObjectId!=runner.Session.World.Raiders[0].Id)throw new InvalidOperationException("Raider selection lost after HUD refresh");
                    if(!runner.SaveSlot("일반 습격 통합 검사"))throw new InvalidOperationException(runner.SaveMessage);
                    var saved=Hash();
                    if(!runner.LoadSlot(runner.ActiveSlotId)||Hash()!=saved)throw new InvalidOperationException("Active raid restore");
                    var world=runner.Session.World;
                    var pawn=world.Colonists[0];shell.SelectPawn(pawn.Id);runner.ToggleDraft(pawn.Id);
                    shell.HandleMapClick(world.Raiders[0].Position,true);
                    if(!pawn.Attack.TargetId.HasValue)throw new InvalidOperationException("Map attack order");
                    for(var i=0;i<1600&&world.RaidPhase!=RaidPhase.Resolved;i++)runner.Session.Step(1);
                    if(world.RaidPhase!=RaidPhase.Resolved)throw new InvalidOperationException("Raid outcome");
                    shell.Map.Focus(pawn.Position);
                }))yield break;
                yield return Capture("raid-result");
            }
            else if(mode=="settlement-art")
            {
                yield return CaptureSettlementArt();
                if(finished)yield break;
            }
            else if(mode=="map-performance")
            {
                if(!Check(()=>shell.Buttons["new"].onClick.Invoke()))yield break;
                foreach(var size in new[]{32,250})
                {
                    yield return MeasureSettlement(size);
                    if(finished)yield break;
                }
            }
            else if(mode=="work-feedback")
            {
                if(!Check(()=>shell.Buttons["new"].onClick.Invoke()))yield break;
                if(!Check(PrepareCookingScreen))yield break;
                var world=runner.Session.World;
                var order=world.CookingOrders.First(o=>o.Exists);
                if(!Check(()=>runner.SetCookingOrder(order.StoveId,true,2)))yield break;
                for(var i=0;i<300&&order.Progress==0;i++)runner.Session.Step(1);
                if(!Check(()=>RequireSample(order.Progress==1,"First cooking tick missing")))yield break;
                yield return Capture("cooking-start");
                runner.Session.Step(19);
                if(!Check(()=>RequireSample(order.Progress==20,"Half meal was not reached")))yield break;
                yield return Capture("cooking-half");
                runner.Session.Step(20);
                if(!Check(()=>RequireSample(order.CompletedMeals==1,"Cooking completion missing")))yield break;
                yield return Capture("cooking-done");
                if(!Check(PrepareAdjacentWork))yield break;
                yield return Capture("neighbor-work");
                shell.SelectPawn(2);
                shell.Map.Zoom(-20,shell.Map.MapCamera.pixelRect.center);
                yield return Capture("distant-selected-work");
                if(!Check(()=>RequireSample(shell.Map.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.name=="Work bar 2"),
                    "Selected distant work bar missing")))yield break;
                if(!Check(PrepareConstructionRoute))yield break;
                yield return null;yield return null;
                shell.SelectPawn(2);
                shell.Map.Focus(new GridPosition(125,130));
                yield return Capture("construction-route");
            }
            else if(mode=="legacy-map")
            {
                if(!Check(()=>{
                    var slot=runner.Slots.ListSlots().Slots.Single(s=>s.IsValid&&s.SlotId!=SaveRepository.LegacySlotId);
                    if(!runner.LoadSlot(slot.SlotId)||runner.Session.World.Width!=32)throw new InvalidOperationException("32-cell slot restore failed");
                    if(!runner.LoadSlot(SaveRepository.LegacySlotId)||runner.Session.World.Width!=10)throw new InvalidOperationException("Legacy map restore failed");
                }))yield break;
            }
            else if(mode=="save")
            {
                if(!Check(()=>{
                    shell.Buttons["new"].onClick.Invoke();
                    var world=runner.Session.World;
                    if(world.Width!=250||world.Height!=250)throw new InvalidOperationException("Expected actual new settlement");
                    var node=world.Gatherables.First(n=>n.Kind==GatherKind.Tree);
                    world.QueueSetBlocked(new GridPosition(249,248),true);
                    runner.DesignateGather(node.Id,true);
                    runner.ToggleDraft(1);
                    if(!runner.MoveColonist(1,new GridPosition(249,249)))throw new InvalidOperationException("Edge move rejected");
                    for(var i=0;i<400&&node.Progress<3;i++)runner.Session.Step(1);
                    if(node.Progress<3||node.Depleted)throw new InvalidOperationException("Expected an active gathering job");
                    runner.SetPaused(true);
                    if(!runner.SaveSlot("재실행 확인"))throw new InvalidOperationException(runner.SaveMessage);
                    File.WriteAllText(Path.Combine(storage,"expected-state.txt"),Hash());
                    File.WriteAllText(Path.Combine(storage,"expected-slot.txt"),runner.ActiveSlotId);
                    CompleteMovement();
                    File.WriteAllText(Path.Combine(storage,"expected-continued-state.txt"),result.continuedStateHash);
                    if(!runner.LoadSlot(runner.ActiveSlotId))throw new InvalidOperationException(runner.SaveMessage);
                }))yield break;
                yield return Capture("saved");
            }
            else if(mode=="load")
            {
                if(!Check(()=>{
                    var id=File.ReadAllText(Path.Combine(storage,"expected-slot.txt"));
                    if(runner.Settings.RecentSlotId!=id)throw new InvalidOperationException("Recent slot mismatch");
                    shell.Buttons["continue"].onClick.Invoke();
                    if(runner.ActiveSlotId!=id||Hash()!=File.ReadAllText(Path.Combine(storage,"expected-state.txt")))throw new InvalidOperationException("Restored state mismatch");
                    CompleteMovement();
                    if(result.continuedStateHash!=File.ReadAllText(Path.Combine(storage,"expected-continued-state.txt")))
                        throw new InvalidOperationException("Post-restart movement differs from uninterrupted progression.");
                }))yield break;
                yield return Capture("restored");
            }
            else {Fail(new ArgumentException("Unknown ux-mode"));yield break;}
            if(!Check(()=>{result.tick=runner.Session.World.Tick;result.stateHash=Hash();result.slot=runner.ActiveSlotId;}))yield break;
            Finish(true);
        }
        private IEnumerator Capture(string name)
        {
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>WriteLayoutReport(name)))yield break;
            if(!Check(()=>CaptureOffscreen(name)))yield break;
            var path=Path.Combine(output,name+".png");ScreenCapture.CaptureScreenshot(path);
            var deadline=Time.realtimeSinceStartup+10;
            while(!File.Exists(path)&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!File.Exists(path))Fail(new IOException("Screenshot missing: "+path));
        }
        private void CaptureOffscreen(string name)
        {
            var canvas=shell.GetComponentInChildren<Canvas>();
            var mapCamera=shell.Map.MapCamera;
            var backgroundCamera=Camera.allCameras.First(c=>c!=mapCamera);
            var viewport=mapCamera.rect;
            var mapTexture=RenderTexture.GetTemporary(Mathf.RoundToInt(Screen.width*viewport.width),
                Mathf.RoundToInt(Screen.height*viewport.height),24,RenderTextureFormat.ARGB32);
            var frame=RenderTexture.GetTemporary(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32);
            var previousActive=RenderTexture.active;
            var previousMode=canvas.renderMode;
            var previousCamera=canvas.worldCamera;
            var previousDistance=canvas.planeDistance;
            GameObject mapImage=null;
            Texture2D pixels=null;
            try
            {
                // Diagnostic rendering uses the same map camera and HUD, without a window backbuffer.
                mapCamera.rect=new Rect(0,0,1,1);
                RenderPipeline.SubmitRenderRequest(mapCamera,new UniversalRenderPipeline.SingleCameraRequest {destination=mapTexture});
                mapCamera.rect=viewport;
                mapImage=new GameObject("Offscreen map composition",typeof(RectTransform),typeof(RawImage));
                var rect=(RectTransform)mapImage.transform;
                rect.SetParent(canvas.transform,false);rect.SetAsFirstSibling();
                rect.anchorMin=viewport.min;rect.anchorMax=viewport.max;rect.offsetMin=rect.offsetMax=Vector2.zero;
                var image=mapImage.GetComponent<RawImage>();image.texture=mapTexture;image.raycastTarget=false;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=backgroundCamera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(backgroundCamera,new UniversalRenderPipeline.SingleCameraRequest {destination=frame});
                RenderTexture.active=frame;
                pixels=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(output,name+"-offscreen.png"),pixels.EncodeToPNG());
            }
            finally
            {
                mapCamera.rect=viewport;
                canvas.renderMode=previousMode;canvas.worldCamera=previousCamera;canvas.planeDistance=previousDistance;
                if(mapImage!=null){mapImage.SetActive(false);Destroy(mapImage);}
                if(pixels!=null)Destroy(pixels);
                RenderTexture.active=previousActive;
                RenderTexture.ReleaseTemporary(frame);RenderTexture.ReleaseTemporary(mapTexture);
                Canvas.ForceUpdateCanvases();
            }
        }
        private void PreparePawnPose(string pose)
        {
            var cell=new GridPosition(3,3);
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,cell,9000,pose=="sleep"?2000:9000)},
                new[]{new ResourceSeed(101,ResourceKind.Wood,20,cell)},
                pose=="sleep"?new[]{new BedSeed(201,cell)}:Array.Empty<BedSeed>(),pose=="sleep",
                new[]{new GatherSeed(301,GatherKind.Tree,new GridPosition(4,3))}));
            runner.SetPaused(true);
            var world=runner.Session.World;
            foreach(WorkCategory category in Enum.GetValues(typeof(WorkCategory)))
                world.SetWorkPriority(1,category,(pose=="carry"&&category==WorkCategory.Haul)||(pose=="work"&&category==WorkCategory.Gather)?1:0);
            if(pose=="move"){world.SetDrafted(1,true);world.QueueMove(1,new GridPosition(6,3));}
            if(pose=="carry")world.QueueCreateStorage(new GridPosition(6,3),new GridPosition(6,3));
            if(pose=="work")world.QueueDesignateGather(301,true);
            Func<bool> ready=()=>{
                var pawn=world.Colonists[0];
                return pose=="move"?pawn.Status==MovementStatus.Moving&&pawn.MovementProgress>0
                    :pose=="carry"?pawn.CargoAmount>0&&pawn.Status==MovementStatus.Moving
                    :pose=="sleep"?pawn.LifeActivity==LifeActivity.SleepingInBed&&pawn.ActionProgress>0
                    :pawn.WorkActivity==WorkActivity.Gathering&&world.Gatherables[0].Progress>0;
            };
            for(var tick=0;tick<300&&!ready();tick++)runner.Session.Step(1);
            if(!ready())throw new InvalidOperationException("Pose fixture did not reach state: "+pose);
        }

        private void PrepareCookingScreen()
        {
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,new GridPosition(1,1))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,20,new GridPosition(1,1)),
                    new ResourceSeed(102,ResourceKind.Ingredients,8,new GridPosition(2,1))}));
            runner.SetPaused(true);
            if(!runner.PlaceBlueprint(BuildingKind.Stove,new GridPosition(4,4)))
                throw new InvalidOperationException("Capture stove placement failed.");
            for(var tick=0;tick<600&&!runner.Session.World.Buildings.Any(b=>b.Exists);tick++)
                runner.Session.Step(1);
            if(!runner.Session.World.Buildings.Any(b=>b.Exists))
                throw new InvalidOperationException("Capture stove construction did not finish.");
        }

        private void PrepareArtScreen()
        {
            // Authored display fixture, restored through normal validation. It is not evidence of growth progression.
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,new GridPosition(2,4))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,10,new GridPosition(2,3)),
                    new ResourceSeed(102,ResourceKind.Ingredients,10,new GridPosition(3,3)),
                    new ResourceSeed(103,ResourceKind.Meal,10,new GridPosition(4,3))},
                new[]{new BedSeed(201,new GridPosition(5,3))},false,
                new[]{new GatherSeed(301,GatherKind.Tree,new GridPosition(2,5)),
                    new GatherSeed(302,GatherKind.EdiblePlant,new GridPosition(5,5))}));
            runner.SetPaused(true);
            if(!runner.CreateGrowingZone(new GridPosition(3,4),new GridPosition(5,4)))
                throw new InvalidOperationException("Art crop fixture placement failed.");
            if(!runner.CreateStorage(new GridPosition(2,2),new GridPosition(4,2)))
                throw new InvalidOperationException("Art stockpile fixture placement failed.");
            var capture=runner.Session.CaptureSaveData(true,1);
            if(!capture.Success)throw new InvalidOperationException(capture.ErrorCode);
            capture.Data.World.Tick=2400;
            var crops=capture.Data.World.Crops.OrderBy(c=>c.Position.X).ToArray();
            for(var i=0;i<crops.Length;i++)
            {
                crops[i].Stage=i==2?"Mature":"Growing";
                crops[i].GrowthTicks=i*1200;
                crops[i].PlantedTick=2400-i*1200;
                crops[i].SowProgress=20;
            }
            if(!runner.TryApplySave(capture.Data))throw new InvalidOperationException(runner.SaveMessage);
            shell.ClearSelection();
        }

        private void PrepareWallScreen()
        {
            runner.Initialize(new ScenarioDefinition(18,18,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,new GridPosition(0,0))},Array.Empty<ResourceSeed>()));
            runner.SetPaused(true);
            var capture=runner.Session.CaptureSaveData(true,1);
            if(!capture.Success)throw new InvalidOperationException(capture.ErrorCode);
            var data=capture.Data;
            var nextId=100;
            Action<GridPosition> add=position=>{
                data.World.Buildings.Add(new BuildingSaveData {Id=nextId++,Kind="Wall",Position=PositionSaveData.From(position),
                    Exists=true,Reason="None",Claim=new ClaimSaveData()});
                data.World.BlockedTiles.Add(PositionSaveData.From(position));
            };
            var offsets=new[]{new GridPosition(0,1),new GridPosition(1,0),new GridPosition(0,-1),new GridPosition(-1,0)};
            for(var mask=0;mask<16;mask++)
            {
                var center=new GridPosition(2+(mask%4)*4,14-(mask/4)*4);
                add(center);
                for(var side=0;side<4;side++)if((mask&(1<<side))!=0)
                    add(new GridPosition(center.X+offsets[side].X,center.Y+offsets[side].Y));
            }
            data.World.NextEntityId=nextId;
            if(!runner.TryApplySave(data))throw new InvalidOperationException(runner.SaveMessage);
            for(var mask=0;mask<16;mask++)
                if(WorldMapView.WallMask(runner.Session.World,new GridPosition(2+(mask%4)*4,14-(mask/4)*4))!=mask)
                    throw new InvalidOperationException("Wall fixture mask mismatch: "+mask);
            shell.ClearSelection();
        }

        private void PrepareCropBoundaryScreen()
        {
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,new GridPosition(1,1))},Array.Empty<ResourceSeed>()));
            runner.SetPaused(true);
            if(!runner.CreateGrowingZone(new GridPosition(1,4),new GridPosition(5,4)))
                throw new InvalidOperationException("Crop boundary fixture placement failed.");
            var data=runner.Session.CaptureSaveData(true,1).Data;
            data.World.Tick=2400;
            var crops=data.World.Crops.OrderBy(c=>c.Position.X).ToArray();
            var growth=new[]{0,1199,1200,2399,2400};
            for(var i=0;i<crops.Length;i++)
            {
                crops[i].Stage=i==4?"Mature":"Growing";
                crops[i].GrowthTicks=growth[i];crops[i].PlantedTick=2400-growth[i];crops[i].SowProgress=20;
            }
            if(!runner.TryApplySave(data))throw new InvalidOperationException(runner.SaveMessage);
            File.WriteAllText(Path.Combine(output,"crop-boundaries-order.txt"),"Left to right: Growing 0, Growing 1199, Growing 1200, Growing 2399, Mature 2400. Authored display fixture, not simulated growth.");
            shell.ClearSelection();
        }

        private void WriteLayoutReport(string name)
        {
            Canvas.ForceUpdateCanvases();
            var report=new LayoutReport { screen=name,width=Screen.width,height=Screen.height,scale=result.scale };
            var canvas=shell.GetComponentInChildren<Canvas>();
            var canvasRect=(RectTransform)canvas.transform;
            var priorityButton=canvas.GetComponentsInChildren<Button>()
                .FirstOrDefault(button=>button.name.StartsWith("priority.",StringComparison.Ordinal));
            var workRows=priorityButton==null?Array.Empty<HorizontalLayoutGroup>():
                priorityButton.transform.parent.parent.GetComponentsInChildren<HorizontalLayoutGroup>()
                    .Where(row=>row.name=="Work row").ToArray();
            if(workRows.Length>1)
            {
                var heading=workRows[0].transform;
                foreach(var row in workRows.Skip(1))
                    for(var column=0;column<heading.childCount;column++)
                    {
                        var headerBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect,heading.GetChild(column));
                        var cellBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect,row.transform.GetChild(column));
                        if(Mathf.Abs(headerBounds.center.x-cellBounds.center.x)>1)
                            report.issues.Add("Work column misaligned: "+column);
                    }
            }
            foreach(var button in canvas.GetComponentsInChildren<Button>())
            {
                if(!button.gameObject.activeInHierarchy)continue;
                report.checkedButtons++;
                var rect=(RectTransform)button.transform;
                var scroll=button.GetComponentInParent<ScrollRect>();
                var container=scroll==null?canvasRect:scroll.content;
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(container,rect);
                var available=container.rect;
                if(bounds.min.x<available.xMin-1||bounds.max.x>available.xMax+1
                    ||bounds.min.y<available.yMin-1||bounds.max.y>available.yMax+1)
                    report.issues.Add("Control outside reachable container: "+button.name);
                if(scroll!=null&&rect.rect.width>scroll.viewport.rect.width+1)
                    report.issues.Add("Control wider than scroll viewport: "+button.name);
            }
            foreach(var label in canvas.GetComponentsInChildren<Text>())
            {
                if(!label.gameObject.activeInHierarchy||string.IsNullOrEmpty(label.text))continue;
                report.checkedTexts++;
                if(label.preferredHeight>label.rectTransform.rect.height+2)
                    report.issues.Add("Text clipped: "+label.transform.parent.name+" / "+label.text);
                label.font.RequestCharactersInTexture(label.text,label.fontSize,label.fontStyle);
                foreach(var character in label.text.Distinct())
                    if(!char.IsWhiteSpace(character)&&!label.font.HasCharacter(character))
                        report.issues.Add("Missing glyph: "+character);
            }
            File.WriteAllText(Path.Combine(output,name+"-layout.json"),JsonUtility.ToJson(report,true));
        }
        private void CompleteMovement()
        {
            var world=runner.Session.World;
            var node=world.Gatherables.First(n=>n.Kind==GatherKind.Tree);
            if(world.Width!=250||!world.BlockedTiles.Contains(new GridPosition(249,248))||node.Progress==0)
                throw new InvalidOperationException("250-cell active work fixture missing");
            for(var i=0;i<1600&&!node.Depleted;i++)runner.Session.Step(1);
            var pawn=world.Colonists[0];
            for(var i=0;i<2500&&pawn.Status==MovementStatus.Moving;i++)runner.Session.Step(1);
            result.movementCompleted=node.Depleted&&pawn.Position.Equals(new GridPosition(249,249))&&pawn.Status==MovementStatus.Arrived;
            if(!result.movementCompleted)throw new InvalidOperationException("Gather or edge movement did not complete.");
            result.continuedStateHash=Hash();
        }
        private void Finish(bool passed)
        {
            if(finished)return;finished=true;
            if(captureMouse!=null&&captureMouse.added)InputSystem.RemoveDevice(captureMouse);
            if(previousCaptureBackgroundBehavior.HasValue)InputSystem.settings.backgroundBehavior=previousCaptureBackgroundBehavior.Value;
            result.passed=passed;result.width=Screen.width;result.height=Screen.height;
            File.WriteAllText(Path.Combine(output,"ux-result.json"),JsonUtility.ToJson(result,true));
            Application.Quit(passed?0:2);
        }
    }
}
