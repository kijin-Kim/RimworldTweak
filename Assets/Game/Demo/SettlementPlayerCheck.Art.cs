using System;
using System.Collections;
using System.Linq;
using RimworldTweak.Presentation;
using RimworldTweak.Simulation;
using UnityEngine;

namespace RimworldTweak.Demo
{
    public sealed partial class SettlementPlayerCheck
    {
        private IEnumerator CaptureSettlementArt()
        {
            if (!Check(() =>
            {
                shell.Buttons["new"].onClick.Invoke();
                runner.Initialize(new ScenarioDefinition(WorkScenario.SettlementSize, WorkScenario.SettlementSize, Array.Empty<GridPosition>(),
                    new[] {
                        new ColonistSeed(1, new GridPosition(13,16)),
                        new ColonistSeed(4, new GridPosition(15,16)),
                        new ColonistSeed(3, new GridPosition(17,16)),
                        new ColonistSeed(2, new GridPosition(18,18), 9000, 2000)
                    }, Array.Empty<ResourceSeed>(),
                    new[] { new BedSeed(201, new GridPosition(18,18)), new BedSeed(202, new GridPosition(16,18)) },
                    true, new[] { new GatherSeed(301, GatherKind.Tree, new GridPosition(13,18)) }));
                runner.SetPaused(true);
                var world = runner.Session.World;
                foreach (var id in new[] { 1, 4, 3 }) world.SetDrafted(id, true);
                for (int i = 0; i < 60 && world.Colonists.Single(p => p.Id == 2).LifeActivity != LifeActivity.SleepingInBed; i++)
                    runner.Session.Step(1);
                RequireSample(world.Colonists.Single(p => p.Id == 2).LifeActivity == LifeActivity.SleepingInBed, "Occupied bed required");
                shell.Map.Bind(world);
                shell.Map.Focus(new GridPosition(15,16));
            })) yield break;
            yield return null;
            var before = Hash();
            var samples = new GameObject("Art check ground items");
            if (!Check(() =>
            {
                var catalog = GetComponent<DemoBootstrap>().Visuals;
                foreach (var pawn in runner.Session.World.Colonists)
                {
                    var portrait = shell.Buttons["pawn." + pawn.Id].transform.Find("Portrait");
                    if (!PawnAppearance.ForId(pawn.Id).TryResolve(catalog, "front", out var expected))
                        throw new InvalidOperationException("Missing pawn appearance " + pawn.Id);
                    RequireSample(portrait != null && portrait.childCount == expected.Length, "Incomplete portrait " + pawn.Id);
                    for (var i = 0; i < expected.Length; i++)
                        RequireSample(portrait.GetChild(i).GetComponent<UnityEngine.UI.Image>().sprite == expected[i].Sprite, "Portrait layer mismatch");
                }
                PlaceGroundSample(samples.transform, catalog, "preview.rifle", new GridPosition(14,14));
                PlaceGroundSample(samples.transform, catalog, "preview.knife", new GridPosition(16,14));
            })) { Destroy(samples); yield break; }
            yield return Capture("settlement-default");
            shell.Map.MapCamera.orthographicSize = 3.5f;
            yield return Capture("settlement-details");
            shell.Map.MapCamera.orthographicSize = 16f;
            yield return Capture("settlement-distant");
            samples.SetActive(false);
            Destroy(samples);
            if (!Check(() => RequireSample(before == Hash(), "Art preview changed the simulation"))) yield break;
            if (!Check(PrepareCookingScreen)) yield break;
            var order = runner.Session.World.CookingOrders.First(o => o.Exists);
            if (!Check(() =>
            {
                RequireSample(runner.SetCookingOrder(order.StoveId, true, 2), "Cooking order missing");
                for (int i = 0; i < 400 && order.Progress < 20; i++) runner.Session.Step(1);
                RequireSample(order.Progress == 20, "Expected half-complete cooking work");
                shell.Map.Bind(runner.Session.World);
                shell.Map.Focus(new GridPosition(4,4));
                shell.Map.MapCamera.orthographicSize = 3.5f;
            })) yield break;
            yield return Capture("settlement-cooking");
            if (!Check(PrepareArtScreen)) yield break;
            shell.Map.Focus(new GridPosition(4,4));
            yield return Capture("settlement-harvest");
        }

        private static void PlaceGroundSample(Transform parent, VisualCatalog catalog, string key, GridPosition cell)
        {
            if (!catalog.TryGet(key, out var visual)) throw new InvalidOperationException("Missing art sample " + key);
            var item = new GameObject(key, typeof(SpriteRenderer));
            item.layer = WorldMapView.MapLayer;
            item.transform.SetParent(parent, false);
            item.transform.position = WorldMapView.CellCenter(cell) + visual.PivotOffset;
            item.transform.localScale = visual.Scale;
            var renderer = item.GetComponent<SpriteRenderer>();
            renderer.sprite = visual.Sprite;
            renderer.sortingOrder = -20000;
        }
    }
}
