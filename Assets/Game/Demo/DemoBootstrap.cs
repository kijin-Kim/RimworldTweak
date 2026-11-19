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
        public VisualCatalog Visuals;
        private string output;
        private readonly SmokeReport report = new SmokeReport();

        [Serializable]
        private sealed class SmokeReport
        {
            public bool passed;
            public bool playerControlGuardPassed, constructionInspectionPassed;
            public bool constructionPassed, farmingPassed;
            public bool cookingPassed, workPolicyPassed, saveLoadPassed;
            public bool normalRaidPassed;
            public bool raidDefensePassed, raidRecoveryPassed;
            public int raidMedicineConsumed, raidRepairWoodConsumed, raidRestoredMeals;
            public string defaultSavePath;
            public long restoredSaveTick;
            public int cookedMeals, cookingIngredientsConsumed;
            public int builtFacilities, constructionWoodConsumed, harvestedIngredients, storedHarvest;
            public bool workPassed, workDraftDropPassed, workFilterRecoveryPassed, workSpeedEquivalent;
            public int producedWood, producedIngredients, storedWood, storedIngredients, workConsumedIngredients;
            public long workStarvationTicks;
            public long workTicks;
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
            var args = Environment.GetCommandLineArgs();
            var uxIndex=Array.IndexOf(args,"--ux-output");
            if(uxIndex>=0)
            {
                if(uxIndex+1>=args.Length){Application.Quit(2);return;}
                View.gameObject.SetActive(false);Runner.enabled=false;
                gameObject.AddComponent<SettlementPlayerCheck>().Begin(Runner,Visuals,args);
                return;
            }
            var index = Array.IndexOf(args, "--smoke-output");
            if (index < 0)
            {
                try { StartInteractive(args); }
                catch(Exception exception) { Debug.LogError("Interactive startup failed: "+exception.Message);Application.Quit(2); }
                return;
            }
            View.Bind(Runner);
            if (index + 1 >= args.Length) { Application.Quit(2); return; }
            Runner.Initialize(SandboxScenario.Create());
            View.Bind(Runner);
            output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(output);
            StartCoroutine(Smoke());
        }

        public void StartInteractive(string[] args)
        {
            var storageIndex=Array.IndexOf(args,"--storage-root");
            string storageRoot=null;
            if(storageIndex>=0)
            {
                if(storageIndex+1>=args.Length||string.IsNullOrWhiteSpace(args[storageIndex+1])||args[storageIndex+1].StartsWith("--",StringComparison.Ordinal))
                    throw new ArgumentException("--storage-root requires a directory path.");
                storageRoot=Path.GetFullPath(args[storageIndex+1]);
            }
            View.gameObject.SetActive(false);
            var shell=gameObject.AddComponent<SettlementShell>();
            shell.Initialize(Runner, true, storageRoot, Visuals, Array.IndexOf(args, "--development-ui") >= 0);
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
                Runner.Session.World.SetDrafted(1,true);
                Require(View.MoveSelected(new GridPosition(5, 1)), "Final movement command");
                Runner.Session.Step(10);
                View.RefreshNow();
                CheckMarkers();
                report.renderTick = Runner.Session.World.Tick;
                report.screenshot = Path.Combine(output, "demo.png");
            })) yield break;
            if (!Check(CheckLiving)) yield break;
            if (!Check(CheckWork)) yield break;
            if (!Check(CheckProduction)) yield break;
            if (!Check(CheckPlayerControlsAndInspection)) yield break;
            if (!Check(StartGeneralRaid)) yield break;
            yield return new WaitForEndOfFrame();
            var normalStart = Path.Combine(output, "normal-raid-start.png");
            ScreenCapture.CaptureScreenshot(normalStart);
            yield return null;
            if (!Check(FinishGeneralRaid)) yield break;
            yield return new WaitForEndOfFrame();
            var normalResult = Path.Combine(output, "normal-raid-result.png");
            ScreenCapture.CaptureScreenshot(normalResult);
            var normalDeadline = Time.realtimeSinceStartup + 10f;
            while ((!File.Exists(normalStart) || !File.Exists(normalResult)) && Time.realtimeSinceStartup < normalDeadline) yield return null;
            if (!Check(() => Require(File.Exists(normalStart) && File.Exists(normalResult), "Normal raid screenshots missing"))) yield break;
            if (!Check(PrepareRaidDefense)) yield break;
            yield return null;
            yield return new WaitForEndOfFrame();
            var warningImage = Path.Combine(output, "raid-warning.png");
            if (!Check(() => ScreenCapture.CaptureScreenshot(warningImage))) yield break;
            var warningDeadline = Time.realtimeSinceStartup + 10f;
            while (!File.Exists(warningImage) && Time.realtimeSinceStartup < warningDeadline) yield return null;
            if (!Check(() => Require(File.Exists(warningImage), "Raid warning image missing"))) yield break;
            if (!Check(CheckRaidDefense)) yield break;
            yield return null;
            yield return new WaitForEndOfFrame();
            var raidImage = Path.Combine(output, "raid.png");
            if (!Check(() => ScreenCapture.CaptureScreenshot(raidImage))) yield break;
            var raidDeadline = Time.realtimeSinceStartup + 10f;
            while (!File.Exists(raidImage) && Time.realtimeSinceStartup < raidDeadline) yield return null;
            if (!Check(() => Require(File.Exists(raidImage), "Raid image missing"))) yield break;
            if (!Check(CheckRaidRepair)) yield break;
            if (!Check(CheckCookingAndPolicy)) yield break;
            if (!Check(CheckSaveLoad)) yield break;
            if(!Check(()=>{Require(Runner.BeginLoad(),"Render load confirmation");View.RefreshNow();}))yield break;
            yield return null;
            yield return new WaitForEndOfFrame();
            var confirmationImage=Path.Combine(output,"save-confirmation.png");
            if(!Check(()=>ScreenCapture.CaptureScreenshot(confirmationImage)))yield break;
            var confirmationDeadline=Time.realtimeSinceStartup+10f;
            while(!File.Exists(confirmationImage)&&Time.realtimeSinceStartup<confirmationDeadline)yield return null;
            if(!Check(()=>{Require(File.Exists(confirmationImage),"Confirmation image missing");Runner.CancelLoad();Require(Runner.BeginLoad()&&Runner.ConfirmLoad(),"Return to loaded display");View.RefreshNow();View.SelectColonist(1);View.RefreshNow();}))yield break;
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!Check(() => ScreenCapture.CaptureScreenshot(report.screenshot))) yield break;
            var deadline = Time.realtimeSinceStartup + 10f;
            while (!File.Exists(report.screenshot) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!Check(() => Require(File.Exists(report.screenshot) && new FileInfo(report.screenshot).Length > 0, "Screenshot missing"))) yield break;
            Finish(true);
        }

        private static void StepUntil(WorldState world, Func<bool> condition, int limit, string message)
        {
            for (var index = 0; index < limit && !condition(); index++) world.Step(1);
            Require(condition(), message + " at tick " + world.Tick);
        }

        private void StartGeneralRaid()
        {
            Runner.Initialize(WorkScenario.Create());
            Runner.SetPaused(true);
            View.RefreshNow();
            var world = Runner.Session.World;
            View.DeveloperButton.onClick.Invoke();
            View.RaidButton.onClick.Invoke();
            Require(Runner.Session.World == world && world.Tick == 0 && world.Raiders.Count == 2 &&
                world.Raiders.All(r => r.Attack.TargetId.HasValue), "Immediate normal raid");
        }

        private void FinishGeneralRaid()
        {
            var world = Runner.Session.World;
            View.DeveloperButton.onClick.Invoke();
            foreach (var pawn in world.Colonists)
            {
                View.SelectColonist(pawn.Id);
                Require(View.ToggleDraftSelected(), "Draft defender");
            }
            Runner.SetPaused(false);
            for (var tick = 0; tick < 180 && world.RaidPhase == RaidPhase.Active; tick++)
            {
                foreach (var pawn in world.Colonists.Where(p => !p.IsDowned && !p.Attack.TargetId.HasValue))
                {
                    View.SelectColonist(pawn.Id);
                    View.AttackButton.onClick.Invoke();
                    View.HandleMapClick(world.Raiders[0].Position, false);
                    Require(pawn.Attack.TargetId.HasValue, "Normal raid attack selection");
                }
                Runner.AdvanceTime(0.1m);
                View.RefreshNow();
            }
            Runner.SetPaused(true);
            Require(world.RaidOutcome == RaidOutcome.Defended && world.Raiders.Count == 0, "Normal raid combat outcome");
            report.normalRaidPassed = true;
        }

        private void PrepareRaidDefense()
        {
            Runner.Initialize(RaidScenario.Create());
            Runner.SetPaused(true);
            var world = Runner.Session.World;
            var objective = world.Buildings.Single(b => b.Kind == BuildingKind.Stove);
            Require(Runner.StartRaid(objective.Id), "Legacy raid regression");
            View.RefreshNow();
            Require(world.RaidPhase == RaidPhase.Warning && world.Tick == 0, "Paused raid warning");
        }

        private void CheckRaidDefense()
        {
            var world = Runner.Session.World;
            View.SelectColonist(1);
            Require(View.ToggleDraftSelected() && View.MoveSelected(new GridPosition(13, 5)), "Raid defender move");
            var first = world.Colonists[0];
            StepUntil(world, () => first.IsDowned, 360, "Actual melee downed the defender");
            var remaining = world.Colonists.Where(pawn => !pawn.IsDowned).ToArray();
            foreach (var pawn in remaining)
            {
                View.SelectColonist(pawn.Id);
                Require(View.ToggleDraftSelected() && View.MoveSelected(new GridPosition(11, 7)), "Defense reposition");
            }
            StepUntil(world, () => remaining.All(p => p.Position == new GridPosition(11, 7) || p.IsDowned), 55, "Defenders in position");
            for (var tick = 0; tick < 130 && world.RaidPhase == RaidPhase.Active; tick++)
            {
                var enemy = world.Raiders.FirstOrDefault();
                foreach (var pawn in remaining.Where(p => !p.IsDowned && p.Attack.TargetId != enemy.Id))
                {
                    View.SelectColonist(pawn.Id);
                    View.AttackButton.onClick.Invoke();
                    Require(View.HandleMapClick(enemy.Position, false) && pawn.Attack.TargetId.HasValue, "Attack target input");
                }
                world.Step(1);
            }
            Require(world.RaidOutcome == RaidOutcome.Defended && world.RaidDownedCount >= 1, "Defense outcome");
            foreach (var pawn in remaining.Where(p => p.IsDrafted)) Runner.ToggleDraft(pawn.Id);
            StepUntil(world, () => world.Colonists.Any(p => p.CareProgress == 39 && p.WorkTargetId == first.Id), 650, "Actual care in progress");
            var previousSavePath = Runner.SavePath;
            Runner.SavePath = Path.Combine(output, "raid-save", "settlement-v2.json");
            Require(Runner.SaveNow() && Runner.BeginLoad() && Runner.ConfirmLoad(), "Treatment save and restore");
            Runner.SavePath = previousSavePath;
            world = Runner.Session.World;
            var consumed = world.MedicineConsumed;
            world.Step(1);
            Require(world.Colonists[0].Health == 40 && world.MedicineConsumed == consumed + 1, "Restored medicine applied once");
            View.RefreshNow();
            View.SelectColonist(1);
            Require(View.NeedsText.text.Contains("40/100") && View.RaidStatusText.text.Contains("방어 성공"), "Recovery HUD");
            report.raidMedicineConsumed = world.MedicineConsumed;
            report.raidDefensePassed = true;
        }

        private void CheckRaidRepair()
        {
            Runner.Initialize(new ScenarioDefinition(16, 12,
                Enumerable.Range(0, 12).Where(y => y != 4).Select(y => new GridPosition(7, y)),
                new[] { new ColonistSeed(1, new GridPosition(6, 4)) },
                new[] { new ResourceSeed(2, ResourceKind.Wood, 12, new GridPosition(5, 5)),
                    new ResourceSeed(3, ResourceKind.Ingredients, 2, new GridPosition(9, 4)) },
                initialRaidStove: new GridPosition(10, 5)));
            Runner.SetPaused(true);
            var world = Runner.Session.World;
            var stove = world.Buildings.Single(b => b.Kind == BuildingKind.Stove);
            var order = world.CookingOrders.Single();
            Require(Runner.SetCookingOrder(stove.Id, true, 1), "Cooking before the raid");
            StepUntil(world, () => order.Progress == 10, 180, "Stove had work in progress");
            Require(Runner.ToggleDraft(1) && Runner.MoveColonist(1, new GridPosition(6, 4)), "Protect the remaining worker");
            StepUntil(world, () => world.Colonists[0].Position == new GridPosition(6, 4), 100, "Worker behind the divider");
            Require(world.QueueSetBlocked(new GridPosition(7, 4), true), "Close the divider");
            world.Step(1);
            Require(Runner.StartRaid(stove.Id), "Start objective attack");
            StepUntil(world, () => world.RaidPhase == RaidPhase.Resolved, 450, "Failed raid resolves");
            Require(world.RaidOutcome == RaidOutcome.ObjectiveLost && stove.Durability == 0 &&
                order.Progress == 10 && order.InputIngredients == 2, "Actual damage preserved cooking work");
            Require(world.QueueSetBlocked(new GridPosition(7, 4), false) && Runner.ToggleDraft(1), "Open recovery access");
            View.RefreshNow();
            View.HandleMapClick(stove.Position, false);
            View.RepairButton.onClick.Invoke();
            Require(stove.RepairDesignated, "Repair UI command");
            StepUntil(world, () => stove.Durability == 100 && world.CookingProducedMeals == 1, 600, "Repair and resume cooking");
            Require(world.RepairWoodConsumed == 6 && world.CookingConsumedIngredients == 2, "Repair and cooking ledger");
            Require(Runner.StartRaid(stove.Id) && world.Raiders.Count == 0 && world.RaidPhase == RaidPhase.Warning, "Second raid resets");
            report.raidRepairWoodConsumed = world.RepairWoodConsumed;
            report.raidRestoredMeals = world.CookingProducedMeals;
            report.raidRecoveryPassed = true;
        }

        private void CheckSaveLoad()
        {
            var original=Runner.Session;var world=original.World;var order=world.CookingOrders.Single(o=>o.Exists);
            Require(order.Progress==10&&order.InputIngredients==2,"Save cooking phase");
            var tick=world.Tick;var action=world.Colonists[0].ActionId;
            report.defaultSavePath=Runner.SavePath;
            Runner.SavePath=Path.Combine(output,"save-smoke","settlement-v1.json");
            Require(Runner.SaveNow(),"Player save file: "+Runner.SaveMessage);
            world.Step(8);Require(order.Progress==18,"Save original progression");
            Require(Runner.BeginLoad(),"Player load prompt");Runner.AdvanceTime(100m);
            Require(world.Tick==tick+8,"Load prompt freeze");
            Require(Runner.ConfirmLoad(),"Player load file: "+Runner.SaveMessage);
            Require(!ReferenceEquals(original,Runner.Session)&&Runner.Session.World.Tick==tick,"Restored world replacement");
            world=Runner.Session.World;order=world.CookingOrders.Single(o=>o.Exists);
            Require(order.Progress==10&&order.InputIngredients==2&&order.OwnerId==1&&world.Colonists[0].ActionId==action,"Restored active cooking claim");
            Require(world.WorkPolicies[0].GetPriority(WorkCategory.Cook)==1,"Restored personal priority");
            Require(Runner.IsPaused&&world.CookingProducedMeals==2&&world.CookingConsumedIngredients==4,"Restored clock and material ledger");
            world.Step(30);Require(world.CookingProducedMeals==3&&world.CookingConsumedIngredients==6,"Restored completion occurs once");
            world.Step(10);Require(world.CookingProducedMeals==3,"No duplicate restored production");
            Require(Runner.BeginLoad()&&Runner.ConfirmLoad(),"Reload captured display phase");
            View.RefreshNow();Require(!View.SelectedColonistId.HasValue&&View.ActiveWorkTool==SettlementView.WorkTool.None,"Restore selection reset");
            View.SelectColonist(1);View.RefreshNow();
            report.saveLoadPassed=true;report.restoredSaveTick=Runner.Session.World.Tick;report.renderTick=Runner.Session.World.Tick;
        }

        private void CheckCookingAndPolicy()
        {
            Runner.Initialize(new ScenarioDefinition(10,10,Array.Empty<GridPosition>(),new[]{new ColonistSeed(1,new GridPosition(1,1))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,20,new GridPosition(1,1))},needsEnabled:false));
            Runner.SetPaused(true);View.RefreshNow();var w=Runner.Session.World;
            Require(Runner.PlaceBlueprint(BuildingKind.Stove,new GridPosition(4,4)),"Cooking stove placement");
            for(var i=0;i<600&&!w.Buildings.Any();i++)w.Step(1);
            Require(w.Buildings.Count==1,"Actual cooking stove construction");var stove=w.Buildings[0];
            Require(Runner.SetCookingOrder(stove.Id,true,2),"Enable cooking");
            Require(Runner.CreateGrowingZone(new GridPosition(6,2),new GridPosition(6,2)),"Cooking crop source");
            for(var i=0;i<600&&w.Crops[0].Stage!=CropStage.Growing;i++)w.Step(1);
            Runner.SetSowingEnabled(w.GrowingZones[0].Id,false);
            for(var i=0;i<3000&&w.CookingOrders[0].Progress<10;i++)w.Step(1);
            var order=w.CookingOrders[0];Require(order.Progress==10&&order.InputIngredients==2,"Harvest to cooking");
            var tick=w.Tick;Runner.SetWorkPriority(1,WorkCategory.Cook,0);View.SelectColonist(1);View.RefreshNow();
            Require(w.Tick==tick&&!order.OwnerId.HasValue&&order.Progress==10&&order.InputIngredients==2,"Paused cook off conservation");
            Require(w.GetWorkDiagnostic(WorkCategory.Cook,stove.Id)==WorkReasonCode.NoWorker,"No cook diagnostic");
            Runner.SetWorkPriority(1,WorkCategory.Cook,1);
            for(var i=0;i<600&&w.CookingProducedMeals<2;i++)w.Step(1);
            Require(w.CookingProducedMeals==2&&w.CookingConsumedIngredients==4,"Cooking output ledger");
            Require(w.ResourceTotal(ResourceKind.Ingredients,false)+w.ResourceTotal(ResourceKind.Ingredients,true)+w.CargoTotal(ResourceKind.Ingredients)+w.CookingInputTotal==2,"Harvest input conservation");
            report.cookedMeals=w.CookingProducedMeals;report.cookingIngredientsConsumed=w.CookingConsumedIngredients;
            report.cookingPassed=report.workPolicyPassed=true;
            Runner.SetCookingOrder(stove.Id,true,3);
            for(var i=0;i<200&&order.Progress<10;i++)w.Step(1);
            View.HandleMapClick(stove.Position,false);View.SelectColonist(1);View.RefreshNow();
            Require(View.CookingPanel.Root.gameObject.activeInHierarchy&&View.CookingPanel.DetailText.text.Contains("진행 10/40"),"Cooking panel rendered state");
            Require(View.WorkPolicyPanel.PriorityButtons[4].GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("1"),"Policy displayed state");
            report.renderTick=w.Tick;
        }

        private void CheckPlayerControlsAndInspection()
        {
            Runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),new[]{new ColonistSeed(1,new GridPosition(1,1))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,12,new GridPosition(2,1))},needsEnabled:false));
            Runner.SetPaused(true);View.RefreshNow();View.SelectColonist(1);var w=Runner.Session.World;
            Require(!View.MoveSelected(new GridPosition(4,1))&&w.PendingCommandCount==0,"Undrafted player move accepted");
            Require(View.CommandFeedbackText.text.Contains("R로 소집"),"Missing draft guidance");
            Runner.ToggleDraft(1);Require(View.MoveSelected(new GridPosition(4,1)),"Drafted move rejected");
            Runner.ToggleDraft(1);Require(w.PendingCommandCount==0,"Undraft retained move");report.playerControlGuardPassed=true;
            Require(Runner.PlaceBlueprint(BuildingKind.Bed,new GridPosition(5,4)),"Inspection blueprint");
            for(var i=0;i<400&&w.Blueprints[0].Progress<7;i++)w.Step(1);
            Require(w.Blueprints[0].Progress==7,"Construction inspection stage missing");
            View.SelectColonist(1);View.RefreshNow();
            Require(View.ConstructionPanel.Root.gameObject.activeInHierarchy&&View.ConstructionPanel.DetailText.text.Contains("시공 7/100"),"Builder panel missing progress");
            Require(View.LifeText.text.Contains("침대 현장")&&View.ClaimText.text.Contains("설계도"),"Builder action/claim missing");
            report.constructionInspectionPassed=true;report.renderTick=w.Tick;
        }

        private void CheckProduction()
        {
            Runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[] { new ColonistSeed(1,new GridPosition(1,1)),new ColonistSeed(2,new GridPosition(2,1)),new ColonistSeed(3,new GridPosition(3,1)) },
                new[] { new ResourceSeed(101,ResourceKind.Wood,80,new GridPosition(2,1)) }, needsEnabled:false));
            Runner.SetPaused(true);View.RefreshNow();var w=Runner.Session.World;
            Require(Runner.CreateStorage(new GridPosition(0,0),new GridPosition(0,1)),"Production storage");
            Require(Runner.PlaceBlueprint(BuildingKind.Wall,new GridPosition(5,5)),"Wall blueprint");
            Require(Runner.PlaceBlueprint(BuildingKind.Door,new GridPosition(6,5)),"Door blueprint");
            Require(Runner.PlaceBlueprint(BuildingKind.Bed,new GridPosition(5,3)),"Bed blueprint");
            Require(Runner.PlaceBlueprint(BuildingKind.Stove,new GridPosition(6,3)),"Stove blueprint");
            Require(Runner.CreateGrowingZone(new GridPosition(1,4),new GridPosition(2,4)),"Growing area");
            for(var i=0;i<4000 && (w.Buildings.Count(b=>b.Exists)<4 || w.ResourceTotal(ResourceKind.Ingredients,true)<12);i++) w.Step(1);
            report.builtFacilities=w.Buildings.Count(b=>b.Exists);report.constructionWoodConsumed=w.ConstructionConsumedWood;
            report.harvestedIngredients=w.ProducedAmount(ResourceKind.Ingredients);report.storedHarvest=w.ResourceTotal(ResourceKind.Ingredients,true);
            Require(report.builtFacilities==4 && report.constructionWoodConsumed==31,"Real material construction");
            Require(w.Beds.Any(b=>b.Exists && w.Buildings.Any(f=>f.Id==b.Id)),"Constructed bed registration");
            Require(w.ResourceTotal(ResourceKind.Wood,false)+w.ResourceTotal(ResourceKind.Wood,true)+w.CargoTotal(ResourceKind.Wood)+w.ConstructionWood==49,"Construction wood ledger");
            Require(report.harvestedIngredients==12 && report.storedHarvest==0,"Harvest waits while storage contains wood");
            Require(w.StorageZones[0].Slots.All(slot=>slot.Amount>0 && slot.Stack.Kind==ResourceKind.Wood),"Existing storage slots contain wood");
            Require(Runner.CreateStorage(new GridPosition(0,2),new GridPosition(0,2)),"Add crop storage without advancing time");
            for(var i=0;i<300 && w.ResourceTotal(ResourceKind.Ingredients,true)<12;i++) w.Step(1);
            report.storedHarvest=w.ResourceTotal(ResourceKind.Ingredients,true);
            Require(report.storedHarvest==12 && w.ProducedAmount(ResourceKind.Ingredients)==12,"Actual crop harvest resumes into new storage");
            report.constructionPassed=report.farmingPassed=true;
            View.RefreshNow(); View.HandleMapClick(new GridPosition(1,4),false);View.RefreshNow();
            Require(View.FarmingPanel.InfoText.text.Contains("재배 구역"),"Farming UI");
            var tick=w.Tick;Runner.AdvanceTime(1m);Require(w.Tick==tick,"Production pause");
            report.renderTick=w.Tick;
        }

        private string MovementSignature()
        {
            var world = Runner.Session.World;
            return world.Tick + ":" + Runner.Session.PendingSeconds + ":" + string.Join(";", world.Colonists.Select(c =>
                c.Id + ":" + c.Position + ":" + c.MovementProgress + ":" + c.Status + ":" + c.Destination + ":" + string.Join(",", c.Path)));
        }

        private void CheckMovement()
        {
            Runner.ResetWorld(); Runner.Session.World.SetDrafted(1,true);
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

            Runner.ResetWorld(); Runner.Session.World.SetDrafted(1,true);
            Runner.MoveColonist(1, new GridPosition(5, 1));
            View.Speed1Button.onClick.Invoke();
            Runner.AdvanceTime(0.6m);
            var partial = MovementSignature();
            Runner.AdvanceTime(2.4m);
            var whole = MovementSignature();
            Runner.ResetWorld(); Runner.Session.World.SetDrafted(1,true);
            Runner.MoveColonist(1, new GridPosition(5, 1));
            View.Speed3Button.onClick.Invoke();
            Runner.AdvanceTime(0.2m);
            Require(MovementSignature() == partial, "Partial speed mismatch");
            Runner.AdvanceTime(0.8m);
            Require(MovementSignature() == whole, "Arrival speed mismatch");
            report.speedEquivalent = true;
            Runner.SetPaused(true);

            Runner.ResetWorld(); Runner.Session.World.SetDrafted(1,true);
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
                Runner.ResetWorld(); Runner.Session.World.SetDrafted(1,true);
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

        private void ConfigureWork()
        {
            Runner.Initialize(WorkScenario.Create()); Runner.SetPaused(true); View.RefreshNow();
            View.StorageButton.onClick.Invoke();
            Require(View.HandleMapClick(new GridPosition(0, 4), false), "Work first corner");
            Require(View.HandleMapClick(new GridPosition(0, 7), false), "Work second corner");
            Require(Runner.Session.World.Tick == 0 && Runner.Session.World.StorageZones.Count == 1 &&
                View.ActiveWorkTool == SettlementView.WorkTool.None, "Paused immediate storage and one-shot tool");
            View.HandleMapClick(new GridPosition(1, 4), false);
            View.HandleMapClick(new GridPosition(1, 5), false);
            Require(Runner.Session.World.StorageZones.Count == 1, "Normal clicks created extra storage");
            View.GatherButton.onClick.Invoke();
            foreach (var node in Runner.Session.World.Gatherables) Require(View.HandleMapClick(node.Position, false), "Gather designation input");
            Require(Runner.Session.World.Tick == 0 && Runner.Session.World.Gatherables.All(n => n.Designated),
                "Paused immediate gathering designation");
            View.SetWorkTool(SettlementView.WorkTool.None);
        }
        private void CheckWorkConservation()
        {
            var world = Runner.Session.World;
            foreach (var kind in new[] { ResourceKind.Wood, ResourceKind.Ingredients, ResourceKind.Meal })
            {
                var initial = kind == ResourceKind.Wood ? 40 : kind == ResourceKind.Meal ? 18 : 0;
                var expected = initial + world.ProducedAmount(kind) + world.Resources.Where(r => r.Kind == kind).Sum(r => r.AddedAmount - r.RemovedAmount - r.ConsumedAmount);
                Require(expected == world.ResourceTotal(kind, false) + world.ResourceTotal(kind, true) + world.CargoTotal(kind), "Work resource conservation: " + kind);
            }
            Require(world.Colonists.All(c => c.CargoAmount >= 0 && c.CargoAmount <= 10 && (!c.IsWorking || c.LifeActivity == LifeActivity.Idle)), "Work/life exclusion and capacity");
        }
        private string WorkSignature()
        {
            var w = Runner.Session.World;
            return LivingSignature() + "|" + string.Join(";", w.Resources.Select(r => r.Id + ":" + r.Amount + ":" + r.StorageSlotId)) +
                "|" + string.Join(";", w.Colonists.Select(c => c.WorkActivity + ":" + c.CargoAmount + ":" + c.ActionId));
        }
        private void CheckWork()
        {
            ConfigureWork();
            var world = Runner.Session.World;
            for (var i = 0; i < 150 && !world.Colonists.Any(c => c.CargoAmount > 0); i++) { Runner.Session.Step(1); CheckWorkConservation(); }
            var carrier = world.Colonists.FirstOrDefault(c => c.CargoAmount > 0);
            Require(carrier != null, "No work carrier reached");
            View.SelectColonist(carrier.Id); View.DraftButton.onClick.Invoke();
            Require(carrier.CargoAmount == 0 && carrier.IsDrafted && world.Resources.All(r => r.OwnerId != carrier.Id) &&
                world.StorageZones.SelectMany(z => z.Slots).All(slot => slot.OwnerId != carrier.Id), "Work draft drop and release");
            CheckWorkConservation(); report.workDraftDropPassed = true; View.DraftButton.onClick.Invoke();
            while (world.Tick < 2400) { Runner.Session.Step(1); CheckWorkConservation(); }
            report.workTicks = world.Tick; report.producedWood = world.ProducedAmount(ResourceKind.Wood); report.producedIngredients = world.ProducedAmount(ResourceKind.Ingredients);
            report.storedWood = world.ResourceTotal(ResourceKind.Wood, true); report.storedIngredients = world.ResourceTotal(ResourceKind.Ingredients, true);
            report.workConsumedIngredients = world.Resources.Where(r => r.Kind == ResourceKind.Ingredients).Sum(r => r.ConsumedAmount);
            report.workStarvationTicks = world.Colonists.Sum(c => c.StarvationTicks);
            Require(report.producedWood == 20 && report.producedIngredients == 8 && report.storedWood == 60 &&
                report.storedIngredients + report.workConsumedIngredients == 8 && world.ResourceTotal(ResourceKind.Ingredients, false) == 0 &&
                world.CargoTotal(ResourceKind.Ingredients) == 0, "Work finite production, storage and raw food consumption");
            Require(world.Colonists.All(c => c.MealsEaten > 0 && c.SleepsCompleted > 0 && c.StarvationTicks == 0), "Work living continuity");
            var zone = world.StorageZones[0]; var filterTick = world.Tick; world.QueueStorageFilter(zone.Id, ResourceKind.Wood, false);
            Require(world.Tick == filterTick, "Filter advanced time");
            Require(world.ResourceTotal(ResourceKind.Wood, true) == 0 && world.ResourceTotal(ResourceKind.Wood, false) == 60, "Filter must preserve stored material");
            world.QueueStorageFilter(zone.Id, ResourceKind.Wood, true);
            for (var i = 0; i < 600 && world.ResourceTotal(ResourceKind.Wood, true) != 60; i++) { Runner.Session.Step(1); CheckWorkConservation(); }
            Require(world.ResourceTotal(ResourceKind.Wood, true) == 60, "Filter recovery"); report.workFilterRecoveryPassed = true;
            ConfigureWork(); Runner.SetSpeed(1); Runner.AdvanceTime(30m); var normal = WorkSignature();
            ConfigureWork(); Runner.SetSpeed(3); Runner.AdvanceTime(10m); Require(WorkSignature() == normal, "Work speed equivalence"); report.workSpeedEquivalent = true;
            ConfigureWork(); Runner.Session.Step(40); View.RefreshNow(); View.HandleMapClick(new GridPosition(0, 4), false);
            View.SelectColonist(Runner.Session.World.Colonists.First(c => c.CargoAmount > 0).Id);
            report.renderTick = Runner.Session.World.Tick; CheckMarkers(); report.workPassed = true;
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
