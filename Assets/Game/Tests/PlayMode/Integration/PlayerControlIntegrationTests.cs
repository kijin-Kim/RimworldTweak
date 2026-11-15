using System;
using System.Collections;
using NUnit.Framework;
using RimworldTweak.Presentation;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
namespace RimworldTweak.Tests.PlayMode
{
    public sealed class PlayerControlIntegrationTests
    {
        private GameObject root; private SimulationRunner runner; private SettlementView view;
        private static GridPosition P(int x,int y)=>new GridPosition(x,y);
        private void Setup()
        {
            root=new GameObject("Player control inspection regression");runner=root.AddComponent<SimulationRunner>();runner.enabled=false;
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),new[]{new ColonistSeed(1,P(1,1))},new[]{new ResourceSeed(101,ResourceKind.Wood,12,P(2,1))},needsEnabled:false));
            runner.SetPaused(true);view=root.AddComponent<SettlementView>();view.Bind(runner);view.SelectColonist(1);
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(root!=null)UnityEngine.Object.Destroy(root);yield return null;}
        [UnityTest] public IEnumerator PausedRightClickRequiresExplicitDraftAndDoesNotQueueRejectedInput()
        {
            Setup();var w=runner.Session.World;
            Assert.That(view.HandleMapClick(P(4,1),true),Is.False);Assert.That(w.PendingCommandCount,Is.Zero);
            Assert.That(view.CommandFeedbackText.text,Does.Contain("R로 소집"));Assert.That(w.Tick,Is.Zero);
            view.ProcessShortcut(Key.R);Assert.That(view.HandleMapClick(P(4,1),true),Is.True);
            Assert.That(w.PendingCommandCount,Is.EqualTo(1));view.ProcessShortcut(Key.R);
            Assert.That(w.PendingCommandCount,Is.Zero);Assert.That(view.MoveSelected(P(4,1)),Is.False);
            runner.Session.Step(30);Assert.That(w.Colonists[0].Position,Is.EqualTo(P(1,1)));yield return null;
        }
        [UnityTest] public IEnumerator BuilderPanelCancelActsOnDisplayedSiteWithoutObjectSelection()
        {
            Setup();var w=runner.Session.World;runner.PlaceBlueprint(BuildingKind.Bed,P(5,4));
            for(var i=0;i<100&&w.Colonists[0].CargoAmount==0;i++)w.Step(1);
            Assert.That(w.Colonists[0].CargoAmount,Is.EqualTo(10));view.SelectColonist(1);
            Assert.That(view.ConstructionPanel.CancelButton.interactable,Is.True);
            var tick=w.Tick;view.ConstructionPanel.CancelButton.onClick.Invoke();
            Assert.That(w.Tick,Is.EqualTo(tick));Assert.That(w.Blueprints[0].Exists,Is.False);
            Assert.That(w.Colonists[0].CargoAmount,Is.Zero);
            Assert.That(w.ResourceTotal(ResourceKind.Wood,false),Is.EqualTo(12));
            Assert.That(view.ConstructionPanel.CancelButton.interactable,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator SelectingBuilderShowsActualSiteAcrossDeliveryAndConstructionWhilePaused()
        {
            Setup();var w=runner.Session.World;runner.PlaceBlueprint(BuildingKind.Bed,P(5,4));
            w.Step(1);view.RefreshNow();Assert.That(view.ConstructionPanel.Root.gameObject.activeInHierarchy,Is.True);
            foreach(var stage in new[]{WorkActivity.GoingToBuildPickup,WorkActivity.GoingToBuildDelivery,WorkActivity.WorkingFeature})
            {
                for(var i=0;i<400&&w.Colonists[0].WorkActivity!=stage;i++)w.Step(1);
                Assert.That(w.Colonists[0].WorkActivity,Is.EqualTo(stage));
                view.SelectColonist(1);var b=w.Blueprints[0];
                Assert.That(view.ConstructionPanel.Root.gameObject.activeInHierarchy,Is.True);
                Assert.That(view.ConstructionPanel.DetailText.text,Does.Contain("침대 설계도 #"+b.Id));
                Assert.That(view.ConstructionPanel.DetailText.text,Does.Contain("목재 "+b.DeliveredWood+"/10"));
                Assert.That(view.LifeText.text,Does.Contain("침대 현장 #"+b.Id));
            }
            w.Step(7);view.RefreshNow();Assert.That(view.LifeText.text,Does.Contain("시공 7/100"));
            Assert.That(view.ConstructionPanel.DetailText.text,Does.Contain("시공 7/100"));
            var tick=w.Tick;runner.AdvanceTime(10m);view.RefreshNow();Assert.That(w.Tick,Is.EqualTo(tick));
            view.ProcessShortcut(Key.R);Assert.That(view.LifeText.text,Does.Contain("소집"));
            Assert.That(view.ClaimText.text,Does.Contain("없음"));Assert.That(w.Blueprints[0].Progress,Is.EqualTo(7));yield return null;
        }
    }
}
