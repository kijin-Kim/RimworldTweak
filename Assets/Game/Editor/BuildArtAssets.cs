using System;
using System.Collections.Generic;
using System.IO;
using RimworldTweak.Presentation;
using UnityEditor;
using UnityEngine;

namespace RimworldTweak.Editor
{
    // root runs this once before source/test freeze. It does not modify game state or imported originals.
    public static class BuildArtAssets
    {
        private const string Art = "Assets/Game/Content/Art/";
        private const string Generated = Art + "Generated/";
        private const string Structural = Art + "Structural/";
        private static readonly Color Ink = new Color(.15f, .19f, .19f, 1);
        private static readonly Color Wood = new Color(.55f, .37f, .22f, 1);
        private static readonly Color Teal = new Color(.30f, .57f, .53f, 1);

        [MenuItem("RimworldTweak/Build visual assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Structural);
            Directory.CreateDirectory("Assets/Game/Content/Visuals");
            var entries = new List<VisualDefinition>();
            AddGenerated(entries, "terrain.soil", "soil", Vector2.one, new Vector2(.5f,.5f));
            AddGenerated(entries, "tree", "tree", new Vector2(1.32f,1.32f), new Vector2(.5f,132f/1254f));
            AddGenerated(entries, "pawn.front", "pawn-front", new Vector2(.95f,.95f), new Vector2(.5f,.05f));
            AddGenerated(entries, "pawn.back", "pawn-back", new Vector2(.95f,.95f), new Vector2(.5f,.05f));
            AddGenerated(entries, "pawn.side", "pawn-side", new Vector2(.95f,.95f), new Vector2(.5f,.05f));
            AddGenerated(entries, "building.bed", "bed", new Vector2(1.08f,1.08f), new Vector2(.5f,.10f));

            for (var mask = 0; mask < 16; mask++)
            {
                var value = mask;
                AddStructural(entries, "building.wall." + mask, "wall-" + mask, c => Wall(c,value), true);
            }
            entries.Add(new VisualDefinition("building.wall", entries.Find(e => e.Key == "building.wall.0").Sprite,
                Vector2.one, new Vector2(.5f,.5f),Color.white,true,"Original procedural connected wall / BuildArtAssets"));
            AddStructural(entries,"terrain.blocked","blocked",c => { Fill(c,0,0,128,128,Ink); Fill(c,7,7,114,114,new Color(.37f,.39f,.36f)); },true);
            AddStructural(entries,"ui.panel","panel",c => { Fill(c,0,0,128,128,new Color(.08f,.12f,.13f,.97f)); Border(c,0,0,128,128,4,new Color(.37f,.48f,.46f)); },true);
            AddStructural(entries,"overlay.selection","selection",c => Border(c,4,4,120,120,5,new Color(1,.82f,.35f)),true);
            AddStructural(entries,"overlay.storage","storage",c => { Fill(c,0,0,128,128,new Color(.3f,.65f,.8f,.14f)); Border(c,0,0,128,128,3,new Color(.4f,.75f,.9f,.6f)); },true);
            AddStructural(entries,"overlay.growing","growing",c => { Fill(c,0,0,128,128,new Color(.45f,.65f,.25f,.17f)); Border(c,0,0,128,128,3,new Color(.6f,.8f,.35f,.6f)); },true);
            AddStructural(entries,"overlay.blueprint","blueprint",c => { Fill(c,8,8,112,112,new Color(.3f,.7f,1,.18f)); Border(c,8,8,112,112,4,new Color(.3f,.7f,1,.85f)); },true);
            AddStructural(entries,"building.door","door",c => { Fill(c,20,4,88,120,Ink); Fill(c,27,10,74,108,Wood); Fill(c,60,10,4,108,Ink); Disk(c,87,64,5,new Color(.9f,.75f,.4f)); },true);
            AddGenerated(entries, "building.stove", "stove", new Vector2(1.08f,1.08f), new Vector2(.5f,.5f));
            AddGenerated(entries, "plant.gather", "gather", new Vector2(1f,1f), new Vector2(.5f,.18f));
            AddGenerated(entries, "crop.young", "crop-young", Vector2.one, new Vector2(.5f,.1396f));
            AddGenerated(entries, "crop.growing", "crop-growing", Vector2.one, new Vector2(.5f,.1619f));
            AddGenerated(entries, "crop.mature", "crop-mature", Vector2.one, new Vector2(.5f,.1348f));
            AddGenerated(entries, "item.wood", "wood", new Vector2(.9f,.9f), new Vector2(.5f,.5f));
            AddGenerated(entries, "item.ingredients", "ingredients", new Vector2(.9f,.9f), new Vector2(.5f,.5f));
            AddGenerated(entries, "item.meal", "meal", new Vector2(.9f,.9f), new Vector2(.5f,.5f));
            AddStructural(entries,"icon.draft","draft",c => { Line(c,30,25,98,104,8,Ink); Line(c,30,104,98,25,8,Ink); Line(c,30,25,98,104,4,new Color(.96f,.75f,.3f)); Line(c,30,104,98,25,4,new Color(.96f,.75f,.3f)); },true);
            AddStructural(entries,"icon.sleep","sleep",c => { Line(c,28,90,95,90,7,Teal); Line(c,95,90,28,35,7,Teal); Line(c,28,35,95,35,7,Teal); },true);
            AddStructural(entries,"icon.work","work",c => { Line(c,38,23,83,89,9,Wood); Fill(c,51,77,48,25,Ink); Fill(c,55,81,40,17,new Color(.75f,.77f,.70f)); },true);

            AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(VisualCatalog.DefaultAssetPath);
            if (catalog == null)
            { catalog = ScriptableObject.CreateInstance<VisualCatalog>(); AssetDatabase.CreateAsset(catalog,VisualCatalog.DefaultAssetPath); }
            catalog.SetEntries(entries); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log("Visual catalog built: " + entries.Count + " entries. Generated samples await actual in-game visual acceptance.");
        }

        private static void AddGenerated(List<VisualDefinition> entries,string key,string file,Vector2 size,Vector2 anchor)
        {
            var path=Generated+file+".png";
            if (!File.Exists(path)) throw new FileNotFoundException("Generated sample must exist before art build.",path);
            var sprite=Import(path,false);
            entries.Add(new VisualDefinition(key,sprite,size,anchor,Color.white,false,"Built-in imagegen original sample 2026-11-17: "+file));
        }

        private static void AddStructural(List<VisualDefinition> entries,string key,string file,Action<Color[]> draw,bool final)
        {
            var colors=new Color[128*128]; draw(colors);
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false); texture.SetPixels(colors); texture.Apply();
            var path=Structural+file+".png"; File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            var sprite=Import(path,key=="ui.panel");
            entries.Add(new VisualDefinition(key,sprite,Vector2.one,new Vector2(.5f,.5f),Color.white,final,
                final?"Original code-native geometry / BuildArtAssets":"Explicit temporary code placeholder / BuildArtAssets"));
        }

        private static Sprite Import(string path,bool panel)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=128; importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
            importer.filterMode=FilterMode.Bilinear; importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=512;
            importer.isReadable=false; importer.spriteBorder=panel?new Vector4(4,4,4,4):Vector4.zero;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Center; settings.spritePivot=new Vector2(.5f,.5f);
            settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Wall(Color[] c,int mask)
        {
            Fill(c,28,28,72,72,Ink); Fill(c,34,34,60,60,Wood);
            if((mask&1)!=0){Fill(c,28,64,72,64,Ink);Fill(c,34,64,60,64,Wood);}
            if((mask&2)!=0){Fill(c,64,28,64,72,Ink);Fill(c,64,34,64,60,Wood);}
            if((mask&4)!=0){Fill(c,28,0,72,64,Ink);Fill(c,34,0,60,64,Wood);}
            if((mask&8)!=0){Fill(c,0,28,64,72,Ink);Fill(c,0,34,64,60,Wood);}
            Line(c,40,48,87,48,2,new Color(.36f,.24f,.14f));
            Line(c,40,77,87,77,2,new Color(.66f,.47f,.29f));
        }
        private static void Fill(Color[] c,int x,int y,int width,int height,Color color)
        {for(var py=Math.Max(0,y);py<Math.Min(128,y+height);py++)for(var px=Math.Max(0,x);px<Math.Min(128,x+width);px++)c[py*128+px]=color;}
        private static void Border(Color[] c,int x,int y,int w,int h,int t,Color color)
        {Fill(c,x,y,w,t,color);Fill(c,x,y+h-t,w,t,color);Fill(c,x,y,t,h,color);Fill(c,x+w-t,y,t,h,color);}
        private static void Disk(Color[] c,int x,int y,int radius,Color color)
        {for(var py=Math.Max(0,y-radius);py<Math.Min(128,y+radius+1);py++)for(var px=Math.Max(0,x-radius);px<Math.Min(128,x+radius+1);px++)if((px-x)*(px-x)+(py-y)*(py-y)<=radius*radius)c[py*128+px]=color;}
        private static void Line(Color[] c,int x0,int y0,int x1,int y1,int radius,Color color)
        {var steps=Math.Max(Math.Abs(x1-x0),Math.Abs(y1-y0));for(var i=0;i<=steps;i++){var t=steps==0?0f:i/(float)steps;Disk(c,Mathf.RoundToInt(Mathf.Lerp(x0,x1,t)),Mathf.RoundToInt(Mathf.Lerp(y0,y1,t)),radius,color);}}
    }
}
