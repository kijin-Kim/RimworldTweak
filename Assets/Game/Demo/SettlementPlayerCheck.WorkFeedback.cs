using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimworldTweak.Simulation;
using UnityEngine;

namespace RimworldTweak.Demo
{
    public sealed partial class SettlementPlayerCheck
    {
        [Serializable]
        private sealed class Measurement
        {
            public string operation;
            public double mainThreadMs, frameMs;
            public long allocatedBytes, managedHeapDeltaBytes, frameAllocatedBytes;
        }

        [Serializable]
        private sealed class SettlementMeasurements
        {
            public int size, zoneSide;
            public long slotBytes;
            public List<Measurement> samples = new List<Measurement>();
        }

        private static void RequireSample(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }

        private IEnumerator MeasureOperation(SettlementMeasurements report, string operation, Action action)
        {
            yield return null;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var heapBefore = GC.GetTotalMemory(false);
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            using (var frameAlloc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory,"GC Allocated In Frame"))
            {
                if (!Check(action)) yield break;
                var elapsed = watch.Elapsed.TotalMilliseconds;
                var threadDelta = GC.GetAllocatedBytesForCurrentThread() - allocated;
                var sample = new Measurement {
                    operation = operation, mainThreadMs = elapsed, allocatedBytes = threadDelta > 0 ? threadDelta : -1,
                    managedHeapDeltaBytes = GC.GetTotalMemory(false) - heapBefore
                };
                yield return null;
                sample.frameMs = watch.Elapsed.TotalMilliseconds;
                sample.frameAllocatedBytes = frameAlloc.Valid && frameAlloc.Count > 0 ? frameAlloc.LastValue : -1;
                report.samples.Add(sample);
            }
        }

        private IEnumerator MeasureSettlement(int size)
        {
            var report = new SettlementMeasurements {size = size, zoneSide = size == 32 ? 8 : 32};
            yield return MeasureOperation(report,"first-map",()=>{
                if(size==250) runner.StartNewSettlement();
                else runner.Initialize(WorkScenario.CreateTestMap());
                runner.SetPaused(true);
                shell.Map.Bind(runner.Session.World);
                RequireSample(runner.Session.World.Width==size,"Wrong performance scenario");
            });
            if(finished)yield break;
            var world=runner.Session.World;
            var corner=new GridPosition(size-1,size-1);
            var start=world.Colonists[0].Position;
            var wallCell=new GridPosition(start.X+4,start.Y+2);
            if(!Check(()=>{
                foreach(var colonist in world.Colonists)
                    foreach(WorkCategory category in Enum.GetValues(typeof(WorkCategory)))
                        world.SetWorkPriority(colonist.Id,category,category==WorkCategory.Build?1:0);
                RequireSample(world.PlaceBlueprint(BuildingKind.Wall,wallCell),"Could not place benchmark wall");
                var blueprint=world.Blueprints.Last();
                for(var tick=0;tick<600&&blueprint.Progress<blueprint.Duration-1;tick++)world.Step(1);
                RequireSample(blueprint.Progress==blueprint.Duration-1,"Wall construction not ready");
            }))yield break;
            yield return MeasureOperation(report,"complete-wall",()=>{
                world.Step(1);
                shell.Map.Refresh(world,null,null,true);
                RequireSample(world.BlockedTiles.Contains(wallCell),"Wall did not block movement");
            });
            if(finished)yield break;
            var wall=world.Buildings.Single(b=>b.Exists&&b.Position==wallCell);
            if(!Check(()=>{
                RequireSample(world.SetDemolition(wall.Id,true),"Demolition refused");
                for(var tick=0;tick<600&&wall.DemolitionProgress<ConstructionRules.DemolitionDuration-1;tick++)world.Step(1);
                RequireSample(wall.DemolitionProgress==ConstructionRules.DemolitionDuration-1,"Demolition not ready");
            }))yield break;
            yield return MeasureOperation(report,"complete-demolition",()=>{
                world.Step(1);
                shell.Map.Refresh(world,null,null,true);
                RequireSample(!world.BlockedTiles.Contains(wallCell),"Demolition did not reopen tile");
            });
            if(finished)yield break;
            yield return MeasureOperation(report,"short-path",()=>RequireSample(world.FindPath(start,new GridPosition(start.X+1,start.Y))!=null,"Short path missing"));
            yield return MeasureOperation(report,"corner-path",()=>RequireSample(world.FindPath(new GridPosition(0,0),corner)?.Count==size+1,"Corner path length"));
            yield return MeasureOperation(report,"group-edge-command",()=>{
                foreach(var pawn in world.Colonists)
                {
                    world.SetDrafted(pawn.Id,true);
                    RequireSample(world.QueueMove(pawn.Id,corner),"Edge command refused");
                }
                world.Step(1);
                RequireSample(world.Colonists.All(p=>p.Status==MovementStatus.Moving),"Group movement missing");
            });
            if(finished)yield break;
            var tile=new GridPosition(size-3,size-4);
            var fullBefore=shell.Map.FullTerrainUpdates;
            var changedBefore=shell.Map.ChangedTerrainCells;
            foreach(var blocked in new[]{true,false})
            {
                yield return MeasureOperation(report,blocked?"obstacle-add":"obstacle-remove",()=>{
                    RequireSample(world.QueueSetBlocked(tile,blocked),"Topology command rejected");
                    world.Step(1);
                    shell.Map.Refresh(world,null,null,true);
                });
                if(finished)yield break;
            }
            if(!Check(()=>RequireSample(shell.Map.FullTerrainUpdates==fullBefore&&shell.Map.ChangedTerrainCells==changedBefore+2,
                "Single tile changes triggered an unexpected terrain rebuild")))yield break;
            var pocket=new GridPosition(size-2,1);
            foreach(var cell in new[]{new GridPosition(size-3,1),new GridPosition(size-1,1),new GridPosition(size-2,0),new GridPosition(size-2,2)})
                world.QueueSetBlocked(cell,true);
            world.Step(1);
            yield return MeasureOperation(report,"isolated-open-target",()=>RequireSample(world.FindPath(start,pocket)==null,"Pocket reachable"));
            var first=new GridPosition(0,size-report.zoneSide);
            var last=new GridPosition(report.zoneSide-1,size-1);
            yield return MeasureOperation(report,"zone-preview",()=>RequireSample(world.CanCreateGrowingZone(first,last)&&world.CanCreateStorage(first,last),"Preview rejected"));
            yield return MeasureOperation(report,"zone-create",()=>RequireSample(world.CreateGrowingZone(first,last),"Zone commit rejected"));
            yield return MeasureOperation(report,"zone-render",()=>shell.Map.Refresh(world,null,null,true));
            yield return MeasureOperation(report,"farm-work-selection",()=>{
                var farmer=world.Colonists[0];
                world.SetWorkPriority(farmer.Id,WorkCategory.Farm,1);
                world.SetDrafted(farmer.Id,false);
                world.Step(1);
                RequireSample(farmer.FeatureStep==WorkStep.Sow,"Farmer did not select a field cell");
            });
            if(finished)yield break;
            yield return MeasureOperation(report,"zone-save",()=>{
                RequireSample(runner.SaveSlot("Map "+size),runner.SaveMessage);
                report.slotBytes=new FileInfo(Path.Combine(runner.Slots.SaveDirectory,runner.ActiveSlotId+".save.json")).Length;
            });
            if(finished)yield break;
            var raidWorld = WorldState.Create(size == 32 ? WorkScenario.CreateTestMap() : WorkScenario.CreateSettlement());
            RequireSample(raidWorld.StartNormalRaid(0), "Benchmark raid could not start");
            yield return MeasureOperation(report, "raid-pursuit-20-ticks", () => {
                raidWorld.Step(20);
                RequireSample(raidWorld.Tick == 20 && raidWorld.Raiders.Count == 2, "Raid benchmark diverged");
            });
            if (finished) yield break;
            File.WriteAllText(Path.Combine(output,"performance-"+size+".json"),JsonUtility.ToJson(report,true));
        }

        private IEnumerator CheckDiagonalConstruction(bool restore)
        {
            var site = new GridPosition(125,125);
            if (!Check(() => {
                shell.Buttons["new"].onClick.Invoke();
                if (restore)
                {
                    string id = File.ReadAllText(Path.Combine(storage,"diagonal-slot.txt"));
                    RequireSample(runner.LoadSlot(id), runner.SaveMessage);
                    RequireSample(Hash() == File.ReadAllText(Path.Combine(storage,"waiting-hash.txt")), "Restored waiting state differs");
                }
                else
                {
                    runner.Initialize(new ScenarioDefinition(250,250,Array.Empty<GridPosition>(),
                        new[]{new ColonistSeed(1,new GridPosition(124,125)),new ColonistSeed(2,new GridPosition(122,122))},
                        new[]{new ResourceSeed(101,ResourceKind.Wood,2,new GridPosition(124,125))}, needsEnabled:false));
                    runner.SetPaused(true);
                    var world = runner.Session.World;
                    world.SetDrafted(2,true);
                    RequireSample(world.PlaceBlueprint(BuildingKind.Wall,site),"Blueprint refused");
                    var b = world.Blueprints.Single();
                    for (int i=0; i<100 && b.Progress<2; i++) world.Step(1);
                    RequireSample(b.Progress==2 && world.QueueMove(2,site),"Could not start diagonal approach");
                    world.Step(4);
                    var visitor = world.Colonists[1];
                    RequireSample(visitor.MovementProgress==4 && visitor.MovementTicksPerTile==7,"Wrong diagonal progress");
                }
            })) yield break;
            yield return null;
            var w = runner.Session.World;
            var blueprint = w.Blueprints.Single(b=>b.Exists);
            shell.Map.Focus(site);
            if (!restore)
            {
                shell.SelectPawn(2);
                yield return Capture("diagonal-walk");
                if (finished) yield break;
                if (!Check(() => {
                    w.Step(17);
                    RequireSample(w.Colonists[1].Position==site,"Visitor did not arrive at tick 21");
                    int progress=blueprint.Progress;
                    w.Step(10);
                    RequireSample(blueprint.Progress==progress && blueprint.DeliveredWood==2 &&
                        w.GetWorkDiagnostic(WorkCategory.Build,blueprint.Id)==WorkReasonCode.AwaitingClearance,"Construction did not wait");
                    RequireSample(runner.SaveSlot("대각 이동과 공사 대기"),runner.SaveMessage);
                    File.WriteAllText(Path.Combine(storage,"diagonal-slot.txt"),runner.ActiveSlotId);
                    File.WriteAllText(Path.Combine(storage,"waiting-hash.txt"),Hash());
                })) yield break;
            }
            shell.SelectObject(blueprint.Id);
            yield return Capture(restore ? "restored-wait" : "occupied-site");
            if (finished) yield break;
            if (!Check(() => {
                RequireSample(w.QueueMove(2,new GridPosition(127,127)),"Visitor cannot leave site");
                w.Step(14);
                for (int i=0; i<50 && blueprint.Exists; i++) w.Step(1);
                RequireSample(!blueprint.Exists && w.ConstructionConsumedWood==2 && w.BlockedTiles.Contains(site),"Construction resume failed");
                var from = new GridPosition(124,125);
                var to = new GridPosition(125,126);
                var around = w.FindPath(from,to);
                RequireSample(around!=null && around.Count==2 && around[0]==new GridPosition(124,126),"Wall corner crossed");
                string end = Path.Combine(storage,"finished-hash.txt");
                if (restore) RequireSample(Hash()==File.ReadAllText(end),"Continued simulation differs after restart");
                else File.WriteAllText(end,Hash());
            })) yield break;
            shell.SelectObject(w.Buildings.Single(b=>b.Exists).Id);
            yield return Capture(restore ? "restored-complete" : "completed-wall");
        }

        private void PrepareConstructionRoute()
        {
            var site=new GridPosition(125,125);
            runner.Initialize(new ScenarioDefinition(250,250,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,new GridPosition(124,125)),new ColonistSeed(2,new GridPosition(122,124))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,2,new GridPosition(124,125))},needsEnabled:false));
            runner.SetPaused(true);
            var world=runner.Session.World;
            world.SetDrafted(2,true);
            RequireSample(world.PlaceBlueprint(BuildingKind.Wall,site),"Wall fixture refused");
            var blueprint=world.Blueprints.Single();
            for(var i=0;i<100&&blueprint.Progress<15;i++)world.Step(1);
            RequireSample(blueprint.Progress==15&&world.QueueMove(2,new GridPosition(129,124)),"Wall/route fixture missing");
            world.Step(1);
        }

        private void PrepareAdjacentWork()
        {
            runner.Initialize(new ScenarioDefinition(250,250,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,new GridPosition(125,125)),new ColonistSeed(2,new GridPosition(126,125))},
                Array.Empty<ResourceSeed>(),Array.Empty<BedSeed>(),false,
                new[]{new GatherSeed(301,GatherKind.Tree,new GridPosition(125,125)),new GatherSeed(302,GatherKind.EdiblePlant,new GridPosition(126,125))}));
            runner.SetPaused(true);
            var world=runner.Session.World;
            world.QueueDesignateGather(301,true);
            world.QueueDesignateGather(302,true);
            for(var i=0;i<20&&!world.Gatherables.All(n=>n.Progress>=3);i++)world.Step(1);
            RequireSample(world.Gatherables.All(n=>n.Progress>=3),"Two active jobs required");
        }
    }
}
