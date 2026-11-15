using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Presentation;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
namespace RimworldTweak.Tests.PlayMode
{
    public sealed class CookingIntegrationTests
    {
        private GameObject root;
        private SimulationRunner runner;
        private SettlementView view;
        private BuildingState stove;
        private static GridPosition P(int x,int y)=>new GridPosition(x,y);
        private void Setup()
        {
            root=new GameObject("Cooking integration");runner=root.AddComponent<SimulationRunner>();runner.enabled=false;
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),new[]{new ColonistSeed(1,P(1,1))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(1,1)),new ResourceSeed(102,ResourceKind.Ingredients,8,P(2,1))},null,false));
            runner.SetPaused(true);view=root.AddComponent<SettlementView>();view.Bind(runner);
            Assert.That(runner.PlaceBlueprint(BuildingKind.Stove,P(4,4)),Is.True);
            for(var i=0;i<600&&!runner.Session.World.Buildings.Any(b=>b.Exists);i++)runner.StepOnce();
            stove=runner.Session.World.Buildings.Single(b=>b.Exists);view.HandleMapClick(P(4,4),false);view.RefreshNow();
        }
        private CookingOrderState Order()=>runner.Session.World.CookingOrders.Single(o=>o.Exists&&o.StoveId==stove.Id);
        [UnityTearDown] public IEnumerator Cleanup(){if(root!=null)UnityEngine.Object.Destroy(root);yield return null;}
        [UnityTest] public IEnumerator PausedCookingControlsImmediatelyUpdateOrderAndDisplay()
        {
            Setup();var w=runner.Session.World;var tick=w.Tick;var o=Order();
            Assert.That(view.CookingPanel.DetailText.text,Does.Contain("목표 12"));
            view.CookingPanel.EnabledButton.onClick.Invoke();view.RefreshNow();
            Assert.That(o.Enabled,Is.True);Assert.That(view.CookingPanel.DetailText.text,Does.Contain("가동"));
            view.CookingPanel.TargetMinusButton.onClick.Invoke();view.RefreshNow();Assert.That(o.TargetMeals,Is.EqualTo(11));
            Assert.That(view.CookingPanel.DetailText.text,Does.Contain("목표 11"));
            view.CookingPanel.DeleteButton.onClick.Invoke();view.RefreshNow();Assert.That(o.Exists,Is.False);
            Assert.That(view.CookingPanel.DetailText.text,Does.Contain("주문 없음"));
            view.CookingPanel.CreateButton.onClick.Invoke();view.RefreshNow();
            Assert.That(Order().Enabled,Is.False);Assert.That(Order().TargetMeals,Is.EqualTo(12));Assert.That(w.Tick,Is.EqualTo(tick));
            yield return null;
        }
        [UnityTest] public IEnumerator PausedCookingStopShowsPreservedInputAndProgress()
        {
            Setup();var w=runner.Session.World;runner.SetCookingOrder(stove.Id,true,1);var o=Order();
            for(var i=0;i<300&&o.Progress<10;i++)runner.StepOnce();Assert.That(o.Progress,Is.EqualTo(10));
            view.HandleMapClick(P(4,4),false);view.RefreshNow();var tick=w.Tick;
            view.CookingPanel.EnabledButton.onClick.Invoke();view.RefreshNow();
            Assert.That(o.Enabled,Is.False);Assert.That(o.OwnerId,Is.Null);Assert.That(o.Progress,Is.EqualTo(10));
            Assert.That(view.CookingPanel.DetailText.text,Does.Contain("투입 2/2"));
            Assert.That(view.CookingPanel.DetailText.text,Does.Contain("진행 10/40"));Assert.That(w.Tick,Is.EqualTo(tick));
            yield return null;
        }
        [UnityTest] public IEnumerator ResetDropsOldCookingSelectionAndOrderReferences()
        {
            Setup();var old=runner.Session.World;runner.SetCookingOrder(stove.Id,true,2);var o=Order();
            for(var i=0;i<300&&o.Progress==0;i++)runner.StepOnce();Assert.That(o.Progress,Is.GreaterThan(0));
            runner.ResetWorld();view.RefreshNow();Assert.That(runner.Session.World,Is.Not.SameAs(old));
            Assert.That(runner.Session.World.CookingOrders.Count,Is.Zero);Assert.That(runner.Session.World.CookingOutputs.Count,Is.Zero);
            Assert.That(view.CookingPanel.EnabledButton.interactable,Is.False);Assert.That(view.CookingPanel.DetailText.text,Does.Contain("조리대를 선택"));
            yield return null;
        }
        [UnityTest] public IEnumerator LogisticsViewStaysOpenAfterClosingSelectedStovePanel()
        {
            Setup();Assert.That(view.CookingPanel.Root.gameObject.activeInHierarchy,Is.True);
            view.ProductionButton.onClick.Invoke();
            for(var i=0;i<5;i++)view.RefreshNow();
            Assert.That(view.CookingPanel.Root.gameObject.activeInHierarchy,Is.False);
            Assert.That(view.ProductionButton.GetComponentInChildren<UnityEngine.UI.Text>().text,Is.EqualTo("건설 / 계획"));
            yield return null;view.RefreshNow();
            Assert.That(view.CookingPanel.Root.gameObject.activeInHierarchy,Is.False);
            view.ProductionButton.onClick.Invoke();view.RefreshNow();
            Assert.That(view.CookingPanel.Root.gameObject.activeInHierarchy,Is.True);
        }
    }
}
