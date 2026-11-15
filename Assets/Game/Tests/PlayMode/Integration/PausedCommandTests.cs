using System;
using System.Collections;
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
    public sealed class PausedCommandTests
    {
        private GameObject root;
        private SimulationRunner runner;
        private SettlementView view;
        private static GridPosition P(int x,int y)=>new GridPosition(x,y);
        private void Setup(bool sleep=false, bool food=false)
        {
            root=new GameObject("Paused commands"); runner=root.AddComponent<SimulationRunner>(); runner.enabled=false;
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                sleep?new[]{new ColonistSeed(1,P(1,1),9000,2005),new ColonistSeed(2,P(1,1),9000,2005)}:
                    new[]{new ColonistSeed(1,P(1,1),food?3010:9000,9000)},
                new[]{new ResourceSeed(101,food?ResourceKind.Meal:ResourceKind.Wood,10,P(1,1))},
                new[]{new BedSeed(201,P(1,1))},true,new[]{new GatherSeed(301,GatherKind.Tree,P(2,1))}));
            runner.SetPaused(true); view=root.AddComponent<SettlementView>(); view.Bind(runner);
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(root!=null)UnityEngine.Object.Destroy(root);yield return null;}
        private static string ClockState(WorldState w)=>w.Tick+"|"+string.Join(";",w.Colonists.Select(c=>c.Nutrition+":"+c.Rest))+"|"+string.Join(";",w.Gatherables.Select(n=>n.Progress));

        [UnityTest]
        public IEnumerator PickupClaimIsReleasedImmediatelyWhenLastStorageDisappears()
        {
            Setup(); var w=runner.Session.World; runner.CreateStorage(P(6,1),P(6,1));
            runner.StepOnce(); Assert.That(w.Resources[0].OwnerId,Is.EqualTo(1));
            Assert.That(w.Colonists[0].CargoAmount,Is.Zero); var before=ClockState(w);
            runner.SetStorageFilter(w.StorageZones[0].Id,ResourceKind.Wood,false); view.RefreshNow();
            Assert.That(w.Resources[0].OwnerId,Is.Null); Assert.That(w.Colonists[0].IsWorking,Is.False);
            Assert.That(ClockState(w),Is.EqualTo(before));
            runner.SetStorageFilter(w.StorageZones[0].Id,ResourceKind.Wood,true); runner.StepOnce();
            Assert.That(w.Resources[0].OwnerId,Is.EqualTo(1)); before=ClockState(w);
            w.QueueDeleteStorage(w.StorageZones[0].Id); view.RefreshNow();
            Assert.That(w.Resources[0].OwnerId,Is.Null); Assert.That(w.Colonists[0].IsWorking,Is.False);
            Assert.That(ClockState(w),Is.EqualTo(before)); Assert.That(w.ResourceTotal(ResourceKind.Wood,false),Is.EqualTo(10));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitCargoCancellationDropsOnceWithoutAdvancingClock()
        {
            Setup(); var w=runner.Session.World; runner.CreateStorage(P(6,1),P(6,1));
            for(var i=0;i<50&&w.Colonists[0].CargoAmount==0;i++)runner.StepOnce();
            Assert.That(w.Colonists[0].CargoAmount,Is.EqualTo(10)); view.SelectColonist(1);
            var before=ClockState(w); var action=w.Colonists[0].ActionId;
            w.QueueCancelLife(1,action); view.RefreshNow();
            Assert.That(w.Colonists[0].CargoAmount,Is.Zero); Assert.That(w.StorageZones[0].Slots[0].OwnerId,Is.Null);
            Assert.That(w.ResourceTotal(ResourceKind.Wood,false),Is.EqualTo(10));
            Assert.That(view.CargoText.text,Does.Contain("없음")); Assert.That(view.ClaimText.text,Does.Contain("없음"));
            var count=w.Resources.Count; w.QueueCancelLife(1,action); view.RefreshNow();
            Assert.That(w.Resources.Count,Is.EqualTo(count)); Assert.That(ClockState(w),Is.EqualTo(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SleepingDraftUpdatesAllViewsWithoutTickAndBedReassignsOnlyOnStep()
        {
            Setup(sleep:true); runner.StepOnce(); view.SelectColonist(1);
            var w=runner.Session.World; Assert.That(w.Colonists[0].LifeActivity,Is.EqualTo(LifeActivity.SleepingInBed));
            var before=ClockState(w); view.DraftButton.onClick.Invoke();
            Assert.That(ClockState(w),Is.EqualTo(before)); Assert.That(runner.IsPaused,Is.True);
            Assert.That(w.Beds[0].OwnerId,Is.Null); Assert.That(w.Colonists[0].HasSleepIntent,Is.False);
            Assert.That(w.Colonists[0].LifeActivity,Is.EqualTo(LifeActivity.Idle));
            Assert.That(view.LifeText.text,Does.Contain("소집")); Assert.That(view.ClaimText.text,Does.Contain("없음"));
            Assert.That(view.SelectTargetButton.interactable,Is.False);
            Assert.That(view.BoardRect.GetComponentsInChildren<Text>().Single(t=>t.name=="Pawn name 1").text,Does.Contain("[R]"));
            view.HandleMapClick(P(1,1),false); while(view.SelectedObjectId!=201) view.NextObjectButton.onClick.Invoke();
            Assert.That(view.WorkInfoText.text,Does.Contain("비어 있음"));
            yield return null;
            Assert.That(ClockState(w),Is.EqualTo(before)); Assert.That(w.Beds[0].OwnerId,Is.Null);
            runner.StepOnce(); Assert.That(w.Beds[0].OwnerId,Is.EqualTo(2));
            view.SelectColonist(1); var paused=ClockState(w); view.DraftButton.onClick.Invoke();
            Assert.That(w.Colonists[0].LifeActivity,Is.EqualTo(LifeActivity.Idle)); Assert.That(ClockState(w),Is.EqualTo(paused));
        }

        [UnityTest]
        public IEnumerator StorageToolIsOneShotAndFiltersApplyWithoutAdvancingClock()
        {
            Setup(); var w=runner.Session.World; var before=ClockState(w);
            view.StorageButton.onClick.Invoke(); view.HandleMapClick(P(4,1),false); view.HandleMapClick(P(4,2),false);
            Assert.That(w.StorageZones.Count,Is.EqualTo(1)); Assert.That(view.ActiveWorkTool,Is.EqualTo(SettlementView.WorkTool.None));
            Assert.That(view.StorageFirstCorner,Is.Null); Assert.That(w.PendingCommandCount,Is.Zero);
            view.HandleMapClick(P(6,1),false); view.HandleMapClick(P(6,2),false); Assert.That(w.StorageZones.Count,Is.EqualTo(1));
            view.StorageButton.onClick.Invoke(); view.HandleMapClick(P(6,1),false); view.HandleMapClick(P(6,2),false);
            Assert.That(w.StorageZones.Count,Is.EqualTo(2)); Assert.That(ClockState(w),Is.EqualTo(before));
            view.HandleMapClick(P(4,1),false); view.FilterButtons[0].onClick.Invoke();
            Assert.That(w.StorageZones[0].Allows(ResourceKind.Wood),Is.False);
            Assert.That(view.FilterButtons[0].GetComponentInChildren<Text>().text,Is.EqualTo("[ ] 목재"));
            view.FilterButtons[0].onClick.Invoke(); Assert.That(w.StorageZones[0].Allows(ResourceKind.Wood),Is.True);
            Assert.That(ClockState(w),Is.EqualTo(before));
            view.DeleteStorageButton.onClick.Invoke(); Assert.That(w.StorageZones[0].Exists,Is.False);
            Assert.That(ClockState(w),Is.EqualTo(before)); Assert.That(w.Colonists[0].IsWorking,Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FilterCancelsCarriedGoodsImmediatelyAndPreservesMaterialOnce()
        {
            Setup(); var w=runner.Session.World; runner.CreateStorage(P(6,1),P(6,1));
            for(var i=0;i<50&&w.Colonists[0].CargoAmount==0;i++)runner.StepOnce();
            Assert.That(w.Colonists[0].CargoAmount,Is.EqualTo(10)); view.SelectColonist(1);
            view.HandleMapClick(P(6,1),false); view.SelectColonist(1); var before=ClockState(w);
            view.FilterButtons[0].onClick.Invoke();
            Assert.That(ClockState(w),Is.EqualTo(before)); Assert.That(w.Colonists[0].CargoAmount,Is.Zero);
            Assert.That(w.StorageZones[0].Slots[0].OwnerId,Is.Null); Assert.That(w.ResourceTotal(ResourceKind.Wood,false),Is.EqualTo(10));
            Assert.That(view.CargoText.text,Does.Contain("없음")); Assert.That(view.ClaimText.text,Does.Contain("없음"));
            var count=w.Resources.Count; runner.SetStorageFilter(w.StorageZones[0].Id,ResourceKind.Wood,false);
            Assert.That(w.Resources.Count,Is.EqualTo(count)); Assert.That(ClockState(w),Is.EqualTo(before));
            view.FilterButtons[0].onClick.Invoke(); runner.StepOnce(); Assert.That(w.Colonists[0].IsWorking,Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GatheringAndEatingCancellationAreImmediateAndStaleActionIsHarmless()
        {
            Setup(); var w=runner.Session.World;
            view.GatherButton.onClick.Invoke(); view.HandleMapClick(P(2,1),false);
            Assert.That(w.Gatherables[0].Designated,Is.True); Assert.That(w.Tick,Is.Zero);
            for(var i=0;i<12;i++)runner.StepOnce(); var before=ClockState(w);
            view.CancelGatherButton.onClick.Invoke();
            Assert.That(w.Gatherables[0].Designated,Is.False); Assert.That(w.Gatherables[0].OwnerId,Is.Null);
            Assert.That(w.Colonists[0].IsWorking,Is.False); Assert.That(ClockState(w),Is.EqualTo(before));
            UnityEngine.Object.Destroy(root); yield return null; Setup(food:true);
            w=runner.Session.World; runner.StepOnce(); view.SelectColonist(1); before=ClockState(w); var old=w.Colonists[0].ActionId;
            w.QueueCancelLife(1); view.RefreshNow(); Assert.That(w.Resources[0].OwnerId,Is.Null);
            Assert.That(w.Colonists[0].LifeActivity,Is.EqualTo(LifeActivity.Idle)); Assert.That(ClockState(w),Is.EqualTo(before));
            Assert.That(view.ClaimText.text,Does.Contain("없음")); Assert.That(w.Resources[0].ConsumedAmount,Is.Zero);
            runner.StepOnce(); var action=w.Colonists[0].ActionId; Assert.That(w.Resources[0].OwnerId,Is.EqualTo(1));
            w.QueueCancelLife(1,old); Assert.That(w.Colonists[0].ActionId,Is.EqualTo(action)); Assert.That(w.Resources[0].OwnerId,Is.EqualTo(1));
        }
    }
}
