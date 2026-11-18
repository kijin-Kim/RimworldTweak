using System;
using NUnit.Framework;
using RimworldTweak.Presentation;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEditor;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class VisualCatalogTests
    {
        private Texture2D texture;
        private Sprite sprite;
        private VisualCatalog catalog;
        [SetUp] public void Setup()
        {
            texture=new Texture2D(16,32); sprite=Sprite.Create(texture,new Rect(0,0,16,32),new Vector2(.5f,.5f),16);
            catalog=ScriptableObject.CreateInstance<VisualCatalog>();
        }
        [TearDown] public void Cleanup()
        { UnityEngine.Object.DestroyImmediate(catalog);UnityEngine.Object.DestroyImmediate(sprite);UnityEngine.Object.DestroyImmediate(texture); }

        [Test] public void DuplicateOrMissingEntryDoesNotReplaceValidCatalog()
        {
            var valid=new VisualDefinition("pawn.front",sprite,Vector2.one,new Vector2(.5f,.1f),Color.white,false,"test fixture");
            catalog.SetEntries(new[]{valid});
            Assert.Throws<ArgumentException>(()=>catalog.SetEntries(new[]{valid,valid}));
            Assert.That(catalog.TryGet("pawn.front",out var result),Is.True);Assert.That(result,Is.SameAs(valid));
            Assert.That(catalog.TryGet("missing",out _),Is.False);
            Assert.Throws<ArgumentException>(()=>new VisualDefinition("missing",null,Vector2.one,Vector2.zero,Color.white,false,"test"));
        }

        [Test] public void DifferentSpritePivotAndSizeKeepAuthoredGroundPoint()
        {
            var first=new VisualDefinition("a",sprite,new Vector2(1,2),new Vector2(.5f,.1f),Color.white,false,"test");
            var other=Sprite.Create(texture,new Rect(0,0,16,32),new Vector2(.2f,.8f),8);
            try
            {
                var second=new VisualDefinition("b",other,new Vector2(1.5f,3),new Vector2(.4f,.2f),Color.white,false,"test");
                foreach(var definition in new[]{first,second})
                {
                    var pivot=definition.Sprite.pivot;
                    var localAnchor=new Vector2((definition.GroundAnchorNormalized.x*definition.Sprite.rect.width-pivot.x)/definition.Sprite.pixelsPerUnit,
                        (definition.GroundAnchorNormalized.y*definition.Sprite.rect.height-pivot.y)/definition.Sprite.pixelsPerUnit);
                    var anchor=definition.PivotOffset+Vector2.Scale(localAnchor,(Vector2)definition.Scale);
                    Assert.That(anchor.x,Is.EqualTo(0).Within(.00001f));Assert.That(anchor.y,Is.EqualTo(0).Within(.00001f));
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(other);}
        }

        [Test] public void CropDisplayBoundariesDoNotInventSimulationStages()
        {
            Assert.That(VisualCatalog.CropKey(CropStage.Empty,0),Is.Null);
            Assert.That(VisualCatalog.CropKey(CropStage.Growing,0),Is.EqualTo("crop.young"));
            Assert.That(VisualCatalog.CropKey(CropStage.Growing,1199),Is.EqualTo("crop.young"));
            Assert.That(VisualCatalog.CropKey(CropStage.Growing,1200),Is.EqualTo("crop.growing"));
            Assert.That(VisualCatalog.CropKey(CropStage.Growing,2399),Is.EqualTo("crop.growing"));
            Assert.That(VisualCatalog.CropKey(CropStage.Mature,2400),Is.EqualTo("crop.mature"));
        }

        [Test] public void BuildingResourceAndDirectionKeysCoverCurrentKinds()
        {
            foreach(BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))Assert.That(VisualCatalog.BuildingKey(kind),Does.StartWith("building."));
            foreach(ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))Assert.That(VisualCatalog.ResourceKey(kind),Does.StartWith("item."));
            for(var i=0;i<16;i++)Assert.That(VisualCatalog.WallKey(i),Is.EqualTo("building.wall."+i));
            Assert.Throws<ArgumentOutOfRangeException>(()=>VisualCatalog.WallKey(16));
            Assert.That(VisualCatalog.PawnKey(Vector2.up),Is.EqualTo("pawn.back"));
            Assert.That(VisualCatalog.PawnKey(Vector2.right),Is.EqualTo("pawn.side"));
            Assert.That(VisualCatalog.PawnKey(Vector2.zero),Is.EqualTo("pawn.front"));
        }

        [Test] public void MissingRequiredLayerFallsBackAsAWholeAndShirtOnlyNeedsNoJacket()
        {
            var appearance = PawnAppearance.ForId(1);
            var keys = new[] { "pawn.body.slim.front", "pawn.shirt.slim.front", "pawn.face.front", "pawn.hair.short.front" };
            var entries = Array.ConvertAll(keys, key => new VisualDefinition(key, sprite, Vector2.one, Vector2.zero, Color.white, false, "test"));
            catalog.SetEntries(entries);
            Assert.That(appearance.TryResolve(catalog, "front", out var incomplete), Is.False);
            Assert.That(incomplete, Is.Empty);

            var shirtOnly = appearance.WithJacket(false);
            Assert.That(shirtOnly.TryResolve(catalog, "front", out var visible), Is.True);
            Assert.That(visible, Is.EqualTo(entries));
            Array.Reverse(entries);
            catalog.SetEntries(entries);
            Assert.That(shirtOnly.TryResolve(catalog, "front", out var reordered), Is.True);
            Assert.That(Array.ConvertAll(reordered, x => x.Key), Is.EqualTo(keys));
            Assert.That(shirtOnly.TryResolve(catalog, "back", out _), Is.False);
            Assert.That(PawnAppearance.ForId(200), Is.SameAs(appearance));
        }

        [Test] public void AllPawnCombinationsUseAlignedFullCanvasSpritesAndStableIds()
        {
            var built = AssetDatabase.LoadAssetAtPath<VisualCatalog>(VisualCatalog.DefaultAssetPath);
            var bodies = new[] { "slim", "broad", "round" };
            var hairstyles = new[] { "short", "bob", "bun" };
            Vector2? canvas = null;
            Assert.That(PawnAppearance.ForId(4).Body, Is.EqualTo("broad"));
            Assert.That(PawnAppearance.ForId(4).HasJacket, Is.True);
            for (var id = 1; id <= 3; id++)
            {
                var appearance = PawnAppearance.ForId(id);
                Assert.That(appearance.Body, Is.EqualTo(bodies[id - 1]));
                Assert.That(appearance.Hair, Is.EqualTo(hairstyles[id - 1]));
                Assert.That(appearance.HasJacket, Is.EqualTo(id != 2));
                foreach (var hairstyle in hairstyles)
                foreach (var direction in new[] { "front", "side", "back" })
                foreach (var jacket in new[] { false, true })
                {
                    Assert.That(appearance.WithHair(hairstyle).WithJacket(jacket).TryResolve(built, direction, out var layers), Is.True, $"pawn {id} / {direction} / {hairstyle} / jacket {jacket}");
                    Assert.That(layers.Length, Is.EqualTo(jacket ? 5 : 4));
                    Assert.That(layers[0].Key, Is.EqualTo($"pawn.body.{appearance.Body}.{direction}"));
                    Assert.That(layers[1].Key, Is.EqualTo($"pawn.shirt.{appearance.Body}.{direction}"));
                    if (jacket) Assert.That(layers[2].Key, Is.EqualTo($"pawn.jacket.{appearance.Body}.{direction}"));
                    Assert.That(layers[layers.Length - 2].Key, Is.EqualTo($"pawn.face.{direction}"));
                    Assert.That(layers[layers.Length - 1].Key, Is.EqualTo($"pawn.hair.{hairstyle}.{direction}"));
                    foreach (var layer in layers)
                    {
                        var dimensions = layer.Sprite.rect.size;
                        canvas = canvas ?? dimensions;
                        Assert.That(dimensions, Is.EqualTo(canvas.Value), layer.Key);
                        Assert.That(dimensions.x, Is.EqualTo(dimensions.y), layer.Key);
                        Assert.That(layer.WorldSize, Is.EqualTo(new Vector2(.95f, .95f)), layer.Key);
                        Assert.That(layer.GroundAnchorNormalized, Is.EqualTo(new Vector2(.5f,.05f)), layer.Key);
                    }
                }
            }
        }

        [Test] public void BuiltCatalogConnectsEveryCurrentVisualWithoutMissingSprites()
        {
            var built=AssetDatabase.LoadAssetAtPath<VisualCatalog>(VisualCatalog.DefaultAssetPath);
            Assert.That(built,Is.Not.Null,"Run BuildArtAssets.Build before validating the content build.");
            var required=new[]{"terrain.soil","terrain.blocked","pawn.front","pawn.back","pawn.side","tree","plant.gather",
                "crop.young","crop.growing","crop.mature","item.wood","item.ingredients","item.meal",
                "building.wall","building.door","building.bed","building.stove","ui.panel","overlay.selection",
                "overlay.storage","overlay.growing","overlay.blueprint","icon.draft","icon.sleep","icon.work","preview.rifle","preview.knife"};
            foreach(var key in required)
            {
                Assert.That(built.TryGet(key,out var visual),Is.True,key);
                Assert.DoesNotThrow(()=>visual.Validate(),key);
                Assert.That(visual.Sprite.texture,Is.Not.Null,key);
            }
            for(var mask=0;mask<16;mask++)Assert.That(built.TryGet(VisualCatalog.WallKey(mask),out _),Is.True);
            built.TryGet("terrain.soil",out var soil);
            Assert.That(soil.WorldSize,Is.EqualTo(Vector2.one));Assert.That(soil.GroundAnchorNormalized,Is.EqualTo(new Vector2(.5f,.5f)));
        }
    }
}
