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
    public sealed class ReadableUiTests
    {
        private GameObject root;
        private SimulationRunner runner;
        private SettlementView view;
        private static GridPosition P(int x,int y)=>new GridPosition(x,y);
        private void Setup()
        {
            root=new GameObject("Readable UI test"); runner=root.AddComponent<SimulationRunner>(); runner.enabled=false;
            runner.Initialize(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,P(7,7)),new ColonistSeed(2,P(7,7)),new ColonistSeed(3,P(7,7))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,10,P(1,1)),new ResourceSeed(102,ResourceKind.Meal,4,P(2,1)),new ResourceSeed(103,ResourceKind.Ingredients,4,P(3,1))},
                new[]{new BedSeed(201,P(4,1))},false,new[]{new GatherSeed(301,GatherKind.Tree,P(1,4)),new GatherSeed(302,GatherKind.EdiblePlant,P(3,4))}));
            runner.SetPaused(true); view=root.AddComponent<SettlementView>(); view.Bind(runner);
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(root!=null)UnityEngine.Object.Destroy(root); yield return null;}

        [UnityTest]
        public IEnumerator PawnNamesStayDistinctAtMapEdgeAndColorsSurviveDraftAndReset()
        {
            Setup(); yield return null;
            var names=view.BoardRect.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("Pawn name ")).ToArray();
            Assert.That(names.Length,Is.EqualTo(3)); Assert.That(names.Select(n=>n.rectTransform.anchoredPosition).Distinct().Count(),Is.EqualTo(3));
            Assert.That(names.Select(n=>n.color).Distinct().Count(),Is.EqualTo(3));
            foreach(var name in names)
            {
                Assert.That(name.text,Does.Contain("정착민"));
                Assert.That(name.rectTransform.anchoredPosition.x,Is.GreaterThanOrEqualTo(0));
                Assert.That(name.rectTransform.anchoredPosition.y+20,Is.LessThanOrEqualTo(view.BoardRect.rect.height));
            }
            view.SelectColonist(2); view.DraftButton.onClick.Invoke();
            Assert.That(names.Single(n=>n.name=="Pawn name 2").text,Does.Contain("[R]"));
            Assert.That(view.SelectionText.color,Is.EqualTo(SettlementView.PawnColor(2)));
            view.ResetButton.onClick.Invoke(); view.SelectColonist(2);
            Assert.That(view.SelectionText.text,Is.EqualTo("2 · 정착민"));
            Assert.That(view.SelectionText.color,Is.EqualTo(SettlementView.PawnColor(2)));
        }

        [UnityTest]
        public IEnumerator SevenObjectKindsHaveDifferentSymbolsAndReadableNames()
        {
            Setup(); yield return null;
            var symbols=view.BoardRect.GetComponentsInChildren<MapSymbolGraphic>();
            Assert.That(symbols.Select(s=>s.Symbol).Distinct().Count(),Is.EqualTo(7));
            foreach(var marker in view.Markers.Where(m=>m.Kind==ViewKind.Resource))
            {
                var label=marker.GetComponentInChildren<Text>(); Assert.That(label.text,Does.Contain("×"));
                Assert.That(label.gameObject.activeInHierarchy,Is.True);
            }
            view.SelectionText.font.RequestCharactersInTexture("정착민 목재 식재료 침대",16);
            Assert.That(view.SelectionText.font.HasCharacter('정'),Is.True,"Windows Korean font must be available");
        }

        [UnityTest]
        public IEnumerator StockZoneHasFloorAndOnlyOuterEdgesBelowObjects()
        {
            Setup(); runner.CreateStorage(P(5,1),P(6,2)); runner.StepOnce(); view.RefreshNow(); yield return null;
            var rects=view.BoardRect.GetComponentsInChildren<RectTransform>();
            var floors=rects.Where(r=>r.name=="Storage floor").ToArray(); var edges=rects.Where(r=>r.name=="Storage outer edge").ToArray();
            Assert.That(floors.Length,Is.EqualTo(4)); Assert.That(edges.Length,Is.EqualTo(8));
            var firstEntity=view.Markers.Where(m=>m.Kind!=ViewKind.Tile).Min(m=>m.transform.GetSiblingIndex());
            Assert.That(floors.Concat(edges).All(r=>r.GetSiblingIndex()<firstEntity),Is.True);
            Assert.That(floors.All(r=>r.GetComponent<Image>().color.a>0 && r.GetComponent<Image>().color.a<1),Is.True);
            runner.DeleteStorage(runner.Session.World.StorageZones[0].Id); runner.StepOnce(); view.RefreshNow();
            Assert.That(floors.Concat(edges).All(r=>!r.gameObject.activeSelf),Is.True);
        }

        [UnityTest]
        public IEnumerator BottomPanelShowsRealCargoAndClaimAndCanSelectTarget()
        {
            Setup(); runner.CreateStorage(P(0,1),P(0,1));
            for(var i=0;i<100 && !runner.Session.World.Colonists.Any(c=>c.CargoAmount>0);i++) runner.StepOnce();
            var pawn=runner.Session.World.Colonists.First(c=>c.CargoAmount>0); view.SelectColonist(pawn.Id);
            Assert.That(view.CargoText.text,Does.Contain("×"+pawn.CargoAmount));
            Assert.That(view.ClaimText.text,Does.Contain("저장 구역"));
            Assert.That(view.DestinationText.text,Does.Contain(pawn.Destination.Value.ToString()));
            Assert.That(view.SelectionText.transform.parent.name,Is.EqualTo("Selection inspector"));
            Assert.That(view.SelectionText.transform.parent.gameObject.activeInHierarchy,Is.True);
            view.SelectTargetButton.onClick.Invoke();
            Assert.That(view.SelectedObjectId,Is.EqualTo(runner.Session.World.StorageZones[0].Id));
            Assert.That(view.LifeText.text,Does.Contain("저장 구역"));
            view.SelectColonist(pawn.Id); view.DraftButton.onClick.Invoke();
            Assert.That(view.CargoText.text,Does.Contain("없음")); Assert.That(view.ClaimText.text,Does.Contain("없음"));
            Assert.That(view.SelectTargetButton.interactable,Is.False); yield return null;
        }
    }
}
