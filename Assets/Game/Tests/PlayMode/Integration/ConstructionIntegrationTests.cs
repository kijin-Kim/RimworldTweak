using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Presentation;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
namespace RimworldTweak.Tests.PlayMode
{
    public sealed class ConstructionIntegrationTests
    {
        private GameObject root;
        private SimulationRunner runner;
        private SettlementView view;
        private static GridPosition P(int x,int y)=>new GridPosition(x,y);
        private void Setup()
        {
            root=new GameObject("Construction integration");runner=root.AddComponent<SimulationRunner>();runner.enabled=false;
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),new[]{new ColonistSeed(1,P(1,1))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(1,1))},null,false));
            runner.SetPaused(true);view=root.AddComponent<SettlementView>();view.Bind(runner);
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(root!=null)UnityEngine.Object.Destroy(root);yield return null;}
        [UnityTest] public IEnumerator PausedBlueprintPlacementIsOneShotAndCancelUpdatesImmediately()
        {
            Setup();var w=runner.Session.World;
            view.ConstructionPanel.BuildButtons[(int)BuildingKind.Wall].onClick.Invoke();
            Assert.That(view.ActiveWorkTool,Is.EqualTo(SettlementView.WorkTool.Building));
            Assert.That(view.HandleMapClick(P(4,4),false),Is.True);
            Assert.That(view.ActiveWorkTool,Is.EqualTo(SettlementView.WorkTool.None));
            Assert.That(w.Blueprints.Count(b=>b.Exists),Is.EqualTo(1));Assert.That(w.Tick,Is.Zero);
            view.HandleMapClick(P(4,4),false);view.RefreshNow();
            Assert.That(view.ConstructionPanel.DetailText.text,Does.Contain("설계도"));
            Assert.That(view.ConstructionPanel.DetailText.text,Does.Contain("0/2"));
            view.ConstructionPanel.CancelButton.onClick.Invoke();view.RefreshNow();
            Assert.That(w.Blueprints.Any(b=>b.Exists),Is.False);Assert.That(w.Tick,Is.Zero);
            Assert.That(view.ConstructionPanel.CancelButton.interactable,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator FailedPlacementKeepsToolAndRightClickOrEscapeOnlyCancelsTool()
        {
            Setup();var w=runner.Session.World;view.BeginBlueprint(BuildingKind.Wall);
            Assert.That(view.HandleMapClick(P(1,1),false),Is.False);
            Assert.That(view.ActiveWorkTool,Is.EqualTo(SettlementView.WorkTool.Building));
            view.HandleMapClick(P(3,3),true);Assert.That(view.ActiveWorkTool,Is.EqualTo(SettlementView.WorkTool.None));
            Assert.That(w.PendingCommandCount,Is.Zero);view.BeginBlueprint(BuildingKind.Stove);
            view.ProcessShortcut(Key.Escape);Assert.That(view.ActiveWorkTool,Is.EqualTo(SettlementView.WorkTool.None));
            Assert.That(w.PendingCommandCount,Is.Zero);Assert.That(w.Tick,Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator ResetRebindsConstructionPanelAndClearsFacilitiesAndTools()
        {
            Setup();var old=runner.Session.World;runner.PlaceBlueprint(BuildingKind.Door,P(4,4));
            for(var i=0;i<400&&!old.Buildings.Any(b=>b.Exists);i++)runner.StepOnce();
            Assert.That(old.Buildings.Any(b=>b.Exists),Is.True);view.BeginBlueprint(BuildingKind.Bed);
            runner.ResetWorld();view.RefreshNow();
            Assert.That(runner.Session.World,Is.Not.SameAs(old));Assert.That(runner.Session.World.Buildings.Any(b=>b.Exists),Is.False);
            Assert.That(runner.Session.World.Blueprints.Any(b=>b.Exists),Is.False);
            Assert.That(view.ActiveWorkTool,Is.EqualTo(SettlementView.WorkTool.None));
            Assert.That(view.ConstructionPanel.CancelButton.interactable,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator StockTotalsIncludeWoodDeliveredToUnfinishedSite()
        {
            Setup();var w=runner.Session.World;
            Assert.That(runner.PlaceBlueprint(BuildingKind.Wall,P(4,4)),Is.True);
            var blueprint=w.Blueprints.Single(b=>b.Exists);
            for(var i=0;i<300&&blueprint.DeliveredWood==0;i++)runner.StepOnce();
            Assert.That(blueprint.DeliveredWood,Is.EqualTo(2));Assert.That(blueprint.Progress,Is.LessThan(blueprint.Duration));
            view.RefreshNow();
            Assert.That(view.StockTotalsText.text,Does.Contain("현장 2"));
            Assert.That(view.StockTotalsText.text,Does.Contain("총량 20"));
            yield return null;
        }
    }
}
