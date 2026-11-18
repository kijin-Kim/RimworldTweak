using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using NUnit.Framework;
using RimworldTweak.Presentation;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RimworldTweak.Tests.PlayMode
{
    public sealed class WorldMapViewTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private Mouse mouse;
        private Keyboard keyboard;
        private WorldMapView map;
        private WorldState world;
        private InputSettings.UpdateMode? previousInputMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        private WorldMapInput routedInput;
        private bool? pendingMouseDown;
        private Vector2 pendingMousePosition;
        private string pointerEvidence;
        private static GridPosition P(int x, int y) => new GridPosition(x, y);

        private void Setup()
        {
            world = WorldState.Create(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(),
                new[] { new ColonistSeed(1, P(3, 3)), new ColonistSeed(2, P(6, 6)), new ColonistSeed(3, P(7, 7)) },
                new[] { new ResourceSeed(101, ResourceKind.Wood, 10, P(1, 1)), new ResourceSeed(102, ResourceKind.Meal, 3, P(2, 1)) },
                new[] { new BedSeed(201, P(2, 2)) }, false,
                new[] { new GatherSeed(301, GatherKind.Tree, P(5, 5)), new GatherSeed(302, GatherKind.EdiblePlant, P(4, 5)) }));
            var go = Own(new GameObject("World map test"));
            map = go.AddComponent<WorldMapView>(); map.Initialize(); map.Refresh(world, null, null, true);
        }
        private GameObject Own(GameObject go) { objects.Add(go); return go; }
        private Mouse MouseDevice()
        {
            UseManualInput();
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent(); return mouse;
        }
        private void UseManualInput()
        {
            previousInputMode = InputSystem.settings.updateMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            // A batch runner has no focused Game View. Otherwise InputManager defers
            // pointer events to the editor even during an explicit InputSystem.Update.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        }
        private WorldMapInput CreateInput()
        {
            routedInput = map.gameObject.AddComponent<WorldMapInput>();
            // Each synthetic event is processed exactly once, independent of editor focus
            // and the batch runner's scheduling. The production Update/raycast path is retained.
            routedInput.enabled = false;
            return routedInput;
        }
        private void PumpInput()
        {
            InputSystem.Update();
            if (mouse != null) mouse.MakeCurrent();
            if (keyboard != null) keyboard.MakeCurrent();
            if (mouse != null)
            {
                var screen = mouse.position.ReadValue();
                var inside = map.TryScreenToGrid(screen, out var cell);
                pointerEvidence = $"current={Mouse.current == mouse}, enabled={mouse.enabled}, down={mouse.leftButton.isPressed}, press={mouse.leftButton.wasPressedThisFrame}, release={mouse.leftButton.wasReleasedThisFrame}, screen={screen}, viewport={map.MapCamera.pixelRect}, inside={inside}, cell={cell}, ui={routedInput.IsPointerOverUI(screen)}";
                if (pendingMouseDown.HasValue)
                {
                    Assert.That(mouse.leftButton.isPressed, Is.EqualTo(pendingMouseDown.Value), pointerEvidence);
                    Assert.That(screen, Is.EqualTo(pendingMousePosition), pointerEvidence);
                    pendingMouseDown = null;
                }
            }
            routedInput.SendMessage("Update");
        }
        private void MouseState(Vector2 position, bool down = false)
        {
            var state = new UnityEngine.InputSystem.LowLevel.MouseState { position = position };
            if (down) state = state.WithButton(MouseButton.Left);
            pendingMouseDown = down; pendingMousePosition = position;
            InputSystem.QueueStateEvent(mouse, state);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousInputMode.HasValue)
            {
                InputSystem.settings.updateMode = previousInputMode.Value;
                InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            }
            pendingMouseDown = null; pointerEvidence = null;
            previousInputMode = null; routedInput = null;
            foreach (var go in objects) if (go != null) UnityEngine.Object.Destroy(go);
            objects.Clear(); mouse = null; keyboard = null; yield return null;
        }

        [UnityTest] public IEnumerator GroundSpritesFitOneCellAcrossImportSizesAndCatalogReplacement()
        {
            Setup();
            var texture=new Texture2D(256,256);
            var catalog=ScriptableObject.CreateInstance<VisualCatalog>();
            var sprites=new List<Sprite>();
            try
            {
                foreach(var ppu in new[]{64f,128f,256f})
                {
                    var sprite=Sprite.Create(texture,new Rect(0,0,256,256),new Vector2(.5f,.5f),ppu);
                    sprites.Add(sprite);
                    catalog.SetEntries(new[]{
                        new VisualDefinition("terrain.soil",sprite,Vector2.one,new Vector2(.5f,.5f),Color.white,false,"test"),
                        new VisualDefinition("terrain.blocked",sprite,Vector2.one,new Vector2(.5f,.5f),Color.white,false,"test")});
                    map.SetCatalog(catalog);map.Refresh(world,null,null,true);
                    var tilemap=map.GetComponentInChildren<UnityEngine.Tilemaps.Tilemap>();
                    var tile=tilemap.GetTile<UnityEngine.Tilemaps.Tile>(Vector3Int.zero);
                    var displayedSize=tile.transform.MultiplyVector(sprite.bounds.size);
                    Assert.That(displayedSize.x,Is.EqualTo(1).Within(.0001f));
                    Assert.That(displayedSize.y,Is.EqualTo(1).Within(.0001f));
                    Assert.That(world.Tick,Is.Zero);
                }
            }
            finally
            {
                map.SetCatalog(null);
                foreach(var sprite in sprites)UnityEngine.Object.Destroy(sprite);
                UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(catalog);
            }
            yield return null;
        }

        private static byte[] SaveBytes(SimulationSession session)
        {
            var capture=session.CaptureSaveData(true,3);
            Assert.That(capture.Success,Is.True,capture.ErrorCode);
            using(var stream=new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(SaveData)).WriteObject(stream,capture.Data);
                return stream.ToArray();
            }
        }

        [UnityTest] public IEnumerator CatalogReplacementPreservesSaveStateRestorationAndProjectedGroundAnchors()
        {
#if UNITY_EDITOR
            Setup();
            var session=new SimulationSession(WorkScenario.Create());world=session.World;
            world.SetDrafted(1,true);Assert.That(world.QueueMove(1,P(4,1)),Is.True);session.Step(2);
            var before=SaveBytes(session);
            Assert.That(SimulationSession.TryRestoreSave(session.CaptureSaveData(true,3).Data,out var expected,out var error),Is.True,error);
            var imported=UnityEditor.AssetDatabase.LoadAssetAtPath<VisualCatalog>(VisualCatalog.DefaultAssetPath);
            Assert.That(imported,Is.Not.Null);
            var replacement=ScriptableObject.CreateInstance<VisualCatalog>();
            var texture=new Texture2D(64,128);
            var sprite=Sprite.Create(texture,new Rect(0,0,64,128),new Vector2(.2f,.8f),32);
            try
            {
                replacement.SetEntries(imported.Entries.Select(v=>new VisualDefinition(v.Key,sprite,
                    v.Key.StartsWith("terrain.")?Vector2.one:v.WorldSize*1.6f,
                    new Vector2(.35f,.25f),Color.white,false,"Test replacement only")));
                var count=-1;
                foreach(var catalog in new[]{(VisualCatalog)null,replacement,imported,replacement,imported})
                {
                    map.SetCatalog(catalog);map.Refresh(world,1,null,true);yield return null;
                    CollectionAssert.AreEqual(before,SaveBytes(session),"Art must not change any serialized gameplay field.");
                    if(count<0)count=map.MarkerObjectCount;
                    Assert.That(map.MarkerObjectCount,Is.EqualTo(count));
                    var centers=new[]{WorldMapView.PawnPosition(world.Colonists[0]),new Vector2(1.5f,5.5f),new Vector2(5.5f,5.5f)};
                    var identities=new[]{"pawn.1","gather.301","bed.201"};
                    var keys=new[]{"pawn.side","tree","building.bed"};
                    for(var i=0;i<identities.Length;i++)
                    {
                        var renderer=map.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.transform.parent.name==identities[i]);
                        var anchor=new Vector2(.5f,.5f);
                        if(catalog!=null)
                        {
                            Assert.That(catalog.TryGet(keys[i],out var definition),Is.True);
                            Assert.That(renderer.sprite,Is.SameAs(definition.Sprite));anchor=definition.GroundAnchorNormalized;
                        }
                        var actualSprite=renderer.sprite;
                        var local=(Vector2.Scale(anchor,actualSprite.rect.size)-actualSprite.pivot)/actualSprite.pixelsPerUnit;
                        var actual=map.MapCamera.WorldToScreenPoint(renderer.transform.TransformPoint(local));
                        var target=map.MapCamera.WorldToScreenPoint(centers[i]);
                        Assert.That(Vector2.Distance(actual,target),Is.LessThanOrEqualTo(1),identities[i]);
                        Assert.That(map.TryScreenToGrid(target,out var cell),Is.True);
                        Assert.That(cell,Is.EqualTo(P(Mathf.FloorToInt(centers[i].x),Mathf.FloorToInt(centers[i].y))));
                    }
                    Assert.That(SimulationSession.TryRestoreSave(session.CaptureSaveData(true,3).Data,out var restored,out error),Is.True,error);
                    CollectionAssert.AreEqual(before,SaveBytes(restored));
                    restored.Step(10);
                    var control=session.CaptureSaveData(true,3).Data;
                    Assert.That(SimulationSession.TryRestoreSave(control,out var baseline,out error),Is.True,error);
                    baseline.Step(10);CollectionAssert.AreEqual(SaveBytes(baseline),SaveBytes(restored));
                }
                expected.Step(10);session.Step(10);CollectionAssert.AreEqual(SaveBytes(expected),SaveBytes(session));
            }
            finally
            {
                map.SetCatalog(null);UnityEngine.Object.Destroy(replacement);
                UnityEngine.Object.Destroy(sprite);UnityEngine.Object.Destroy(texture);
            }
#else
            Assert.Ignore("Imported catalog inspection requires the Editor asset database.");
            yield break;
#endif
        }

        [UnityTest] public IEnumerator CrowdedPawnLabelsKeepScreenSizeAndSeparateAfterZoom()
        {
            Setup();
            world=WorldState.Create(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,P(3,3)),new ColonistSeed(2,P(3,3)),new ColonistSeed(3,P(3,3))},
                Array.Empty<ResourceSeed>()));
            map.Refresh(world,null,null,true);
            foreach(var steps in new[]{0f,20f,-40f})
            {
                map.Zoom(steps,map.GridToScreen(P(3,3)));map.Refresh(world,null,null,true);
                yield return null;
                var positions=new List<Vector2>();
                foreach(var label in map.GetComponentsInChildren<TextMesh>())
                {
                    if(label.name!="Label"||!label.gameObject.activeSelf||!label.text.StartsWith("정착민"))continue;
                    var pixels=label.characterSize*label.fontSize/10f*map.MapCamera.pixelHeight/(2*map.ZoomSize);
                    Assert.That(pixels,Is.EqualTo(16).Within(.01f));
                    var shadow = label.transform.Find("Label shadow").GetComponent<TextMesh>();
                    Assert.That(shadow.text, Is.EqualTo(label.text));
                    var shadowOffset = (Vector2)(map.MapCamera.WorldToScreenPoint(shadow.transform.position) -
                        map.MapCamera.WorldToScreenPoint(label.transform.position));
                    Assert.That(shadowOffset.x, Is.EqualTo(1).Within(.01f));
                    Assert.That(shadowOffset.y, Is.EqualTo(-1).Within(.01f));
                    var position=(Vector2)map.MapCamera.WorldToScreenPoint(label.transform.position);
                    foreach(var previous in positions)Assert.That(Mathf.Abs(position.y-previous.y),Is.GreaterThanOrEqualTo(20));
                    positions.Add(position);
                }
                Assert.That(positions.Count,Is.EqualTo(3));
                Assert.That(world.Tick,Is.Zero);
            }
        }

        [UnityTest] public IEnumerator ScreenGridRoundTripsAfterZoomPanAndViewportChanges()
        {
            Setup(); map.SetViewport(new Rect(.12f, .2f, .78f, .7f)); yield return null;
            foreach (var cell in new[] { P(0, 0), P(7, 0), P(0, 7), P(7, 7), P(4, 4) })
            {
                map.Focus(cell);
                foreach (var zoom in new[] { 20f, -40f, 3f })
                {
                    map.Zoom(zoom, map.GridToScreen(cell)); map.Focus(cell);
                    var screen = map.GridToScreen(cell);
                    Assert.That(map.TryScreenToGrid(screen, out var actual), Is.True, "cell=" + cell + " screen=" + screen);
                    Assert.That(actual, Is.EqualTo(cell));
                }
            }
            Assert.That(map.TryScreenToGrid(Vector2.zero, out _), Is.False, "Outside camera viewport must not hit map");
            map.Pan(new Vector2(1000, -1000));
            Assert.That(map.MapCamera.transform.position.x, Is.InRange(0, 8));
            Assert.That(map.MapCamera.transform.position.y, Is.InRange(0, 8));
        }

        [UnityTest] public IEnumerator RefreshReadsWorldWithoutAdvancingMovementAndRebindClearsOldViews()
        {
            Setup(); world.SetDrafted(1, true); Assert.That(world.QueueMove(1, P(4, 3)), Is.True); world.Step(2);
            var pawn = world.Colonists[0]; var before = WorldMapView.PawnPosition(pawn);
            var tick = world.Tick; var searches = world.PathSearchCount;
            for (var i = 0; i < 10; i++) map.Refresh(world, 1, null, true);
            Assert.That(WorldMapView.PawnPosition(pawn), Is.EqualTo(before));
            Assert.That(world.Tick, Is.EqualTo(tick)); Assert.That(world.PathSearchCount, Is.EqualTo(searches));
            var count = map.MarkerObjectCount;
            map.Refresh(world, 1, null, true); Assert.That(map.MarkerObjectCount, Is.EqualTo(count));
            var replacement = WorldState.Create(new ScenarioDefinition(4, 4, Array.Empty<GridPosition>(),
                new[] { new ColonistSeed(1, P(1, 1)) }, Array.Empty<ResourceSeed>(), Array.Empty<BedSeed>(), false));
            map.Refresh(replacement, null, null, true); yield return null;
            Assert.That(map.World, Is.SameAs(replacement)); Assert.That(map.MarkerObjectCount, Is.EqualTo(1));
            Assert.That(map.VisibleEntityCount, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator SameCellPawnRemainsVisibleAboveBedResourceAndTree()
        {
            Setup();
            var session = new SimulationSession(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,P(3,3),9000,2000)},
                new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(3,3))},
                new[]{new BedSeed(201,P(3,3))},true,
                new[]{new GatherSeed(301,GatherKind.Tree,P(3,3))}));
            session.Step(2); world=session.World;
            Assert.That(world.Colonists[0].LifeActivity,Is.EqualTo(LifeActivity.SleepingInBed));
            var before=SaveBytes(session);
            map.Refresh(world,1,null,true); yield return null;
            var sprites=map.GetComponentsInChildren<SpriteRenderer>();
            var pawn=sprites.Single(r=>r.transform.parent.name=="pawn.1");
            foreach(var id in new[]{"bed.201","resource.101","gather.301"})
                Assert.That(pawn.sortingOrder,Is.GreaterThan(sprites.Single(r=>r.transform.parent.name==id).sortingOrder),id);
            Assert.That(Quaternion.Angle(pawn.transform.localRotation,Quaternion.identity),Is.LessThan(.01f));
            CollectionAssert.AreEqual(before,SaveBytes(session));
            world.SetDrafted(1,true); world.QueueMove(1,P(3,4)); session.Step(5);
            map.Refresh(world,1,null,true);
            Assert.That(pawn.sortingOrder,Is.LessThan(sprites.Single(r=>r.transform.parent.name=="gather.301").sortingOrder),
                "Moving north must still pass behind the tree.");
        }

        [UnityTest] public IEnumerator MovementVisualUsesOnlyCurrentSimulationCellProgress()
        {
            Setup(); var pawn = world.Colonists[0]; world.SetDrafted(1, true); world.QueueMove(1, P(4, 3));
            var origin = WorldMapView.PawnPosition(pawn); world.Step(2); var halfway = WorldMapView.PawnPosition(pawn);
            Assert.That(halfway.x, Is.GreaterThan(origin.x).And.LessThan(origin.x + 1));
            map.Refresh(world, 1, null, true); yield return null; yield return null;
            Assert.That(WorldMapView.PawnPosition(pawn), Is.EqualTo(halfway));
            world.Step(3); Assert.That(WorldMapView.PawnPosition(pawn), Is.EqualTo(new Vector2(4.5f, 3.5f)));
        }

        [UnityTest] public IEnumerator NormalizedWheelZoomsNoticeablyAndModalBlocksIt()
        {
            Setup(); MouseDevice();
            var blocked=false;var input=CreateInput();
            input.Configure(map,()=>blocked,()=>false,()=>false,(p,r)=>{},(a,b)=>{},()=>{});
            var previous=InputSystem.settings.scrollDeltaBehavior;
            try
            {
                InputSystem.settings.scrollDeltaBehavior=InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
                var screen=map.GridToScreen(P(3,3));
                MouseState(screen);PumpInput();yield return null;
                var before=map.ZoomSize;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen,scroll=new Vector2(0,1)});
                PumpInput();yield return null;
                Assert.That(map.ZoomSize,Is.EqualTo(before*Mathf.Pow(.85f,1.5f)).Within(.001f));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen,scroll=new Vector2(0,-1)});
                PumpInput();yield return null;
                Assert.That(map.ZoomSize,Is.EqualTo(before).Within(.001f));
                blocked=true;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen,scroll=new Vector2(0,1)});
                PumpInput();yield return null;
                Assert.That(map.ZoomSize,Is.EqualTo(before).Within(.001f));
            }
            finally {InputSystem.settings.scrollDeltaBehavior=previous;}
        }

        [UnityTest] public IEnumerator ActualMousePressReleaseSelectsOnceAndRectanglePreservesEndpoints()
        {
            Setup(); MouseDevice(); var clicks = 0; var rectangles = 0; var isRectangle = false;
            GridPosition first = default, last = default;
            var input = CreateInput();
            input.Configure(map, () => false, () => isRectangle, () => isRectangle,
                (p, right) => { clicks++; last = p; }, (a, b) => { rectangles++; first = a; last = b; }, () => { });
            var target = map.GridToScreen(P(3, 3)); MouseState(target); PumpInput(); yield return null;
            MouseState(target, true); PumpInput(); yield return null; MouseState(target); PumpInput(); yield return null;
            Assert.That(clicks, Is.EqualTo(1), pointerEvidence); Assert.That(last, Is.EqualTo(P(3, 3)));
            isRectangle = true;
            MouseState(map.GridToScreen(P(5, 5)), true); PumpInput(); yield return null;
            MouseState(map.GridToScreen(P(2, 2))); PumpInput(); yield return null;
            Assert.That(rectangles, Is.EqualTo(1)); Assert.That(first, Is.EqualTo(P(5, 5))); Assert.That(last, Is.EqualTo(P(2, 2)));
        }

        [UnityTest] public IEnumerator ModalCancelsCapturedGestureAndStaleReleaseCannotPlace()
        {
            Setup(); MouseDevice(); var blocked = false; var rectangles = 0;
            var input = CreateInput();
            input.Configure(map, () => blocked, () => true, () => true, (p, r) => { }, (a, b) => rectangles++, () => { });
            var target = map.GridToScreen(P(3, 3)); MouseState(target); PumpInput(); yield return null;
            MouseState(target, true); PumpInput(); yield return null; Assert.That(input.IsCapturing, Is.True, pointerEvidence);
            blocked = true; PumpInput(); yield return null; Assert.That(input.IsCapturing, Is.False);
            blocked = false; MouseState(target); PumpInput(); yield return null; Assert.That(rectangles, Is.Zero);
            MouseState(target, true); PumpInput(); yield return null; MouseState(target); PumpInput(); yield return null;
            Assert.That(rectangles, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator GraphicRaycasterPreventsMapInputUnderUiAndReleaseOutsideCancels()
        {
            Setup(); MouseDevice();
            if (EventSystem.current == null) Own(new GameObject("World input events", typeof(EventSystem)));
            var canvas = Own(new GameObject("UI blocker", typeof(Canvas), typeof(GraphicRaycaster))).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = new GameObject("Blocker", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(canvas.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var clicks = 0; var input = CreateInput();
            input.Configure(map, () => false, () => false, () => false, (p, r) => clicks++, (a, b) => clicks++, () => { });
            PumpInput(); yield return null;
            var target = map.GridToScreen(P(3, 3)); Assert.That(input.IsPointerOverUI(target), Is.True);
            MouseState(target, true); PumpInput(); yield return null; MouseState(target); PumpInput(); yield return null; Assert.That(clicks, Is.Zero);
            panel.SetActive(false); PumpInput(); yield return null;
            MouseState(target, true); PumpInput(); yield return null; Assert.That(input.IsCapturing, Is.True, pointerEvidence);
            MouseState(new Vector2(-10, -10)); PumpInput(); yield return null; Assert.That(clicks, Is.Zero);
            MouseState(target, true); PumpInput(); yield return null; MouseState(target); PumpInput(); yield return null; Assert.That(clicks, Is.EqualTo(1), pointerEvidence);
        }

        [UnityTest] public IEnumerator HeldCameraKeyDoesNotResumeUntilReleasedAfterBlock()
        {
            Setup(); UseManualInput(); keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            var blocked = false; var input = CreateInput();
            input.Configure(map, () => blocked, () => false, () => false, (p, r) => { }, (a, b) => { }, () => { });
            map.Zoom(20, map.GridToScreen(P(3, 3))); map.Focus(P(3, 3));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); PumpInput(); yield return null;
            blocked = true; PumpInput(); yield return null; var position = map.MapCamera.transform.position;
            blocked = false; PumpInput(); yield return null; PumpInput(); yield return null;
            Assert.That(map.MapCamera.transform.position, Is.EqualTo(position));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); PumpInput(); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); PumpInput(); yield return null;
            Assert.That(map.MapCamera.transform.position.y, Is.GreaterThan(position.y));
        }
    }
}
