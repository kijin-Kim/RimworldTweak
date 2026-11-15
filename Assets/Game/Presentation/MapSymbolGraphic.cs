using UnityEngine;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public enum MapSymbol { Person, Tree, Plant, Meal, Ingredients, Wood, Bed, Wall, Door, Stove, Crop }

    /// <summary>Resolution independent, distinct silhouettes; no downloaded art or font icons.</summary>
    public sealed class MapSymbolGraphic : MaskableGraphic
    {
        public MapSymbol Symbol { get; private set; }
        public void SetSymbol(MapSymbol symbol, Color tint)
        {
            if (Symbol == symbol && color == tint) return;
            Symbol = symbol; color = tint; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var dark = Color.Lerp(color, Color.black, 0.45f);
            var light = Color.Lerp(color, Color.white, 0.4f);
            switch (Symbol)
            {
                case MapSymbol.Wall:
                    Quad(mesh,.05f,.05f,.95f,.95f,dark);Quad(mesh,.10f,.55f,.48f,.90f,color);Quad(mesh,.52f,.55f,.90f,.90f,light);
                    Quad(mesh,.10f,.10f,.30f,.50f,light);Quad(mesh,.34f,.10f,.70f,.50f,color);Quad(mesh,.74f,.10f,.90f,.50f,light);break;
                case MapSymbol.Door:
                    Quad(mesh,.15f,.02f,.85f,.98f,dark);Quad(mesh,.25f,.05f,.75f,.90f,color);Ellipse(mesh,.66f,.48f,.04f,.04f,light);break;
                case MapSymbol.Stove:
                    Quad(mesh,.08f,.08f,.92f,.85f,dark);Quad(mesh,.08f,.65f,.92f,.88f,color);
                    Ellipse(mesh,.32f,.78f,.16f,.09f,light);Ellipse(mesh,.69f,.78f,.13f,.09f,light);Quad(mesh,.22f,.22f,.78f,.53f,color);break;
                case MapSymbol.Crop:
                    Quad(mesh,.46f,.05f,.54f,.94f,dark);Ellipse(mesh,.30f,.36f,.20f,.10f,color);Ellipse(mesh,.70f,.55f,.20f,.10f,color);
                    Ellipse(mesh,.40f,.74f,.13f,.16f,light);Ellipse(mesh,.65f,.87f,.13f,.10f,light);break;
                case MapSymbol.Person:
                    Ellipse(mesh, .5f, .75f, .16f, .16f, light);
                    Quad(mesh, .30f, .22f, .70f, .57f, color);
                    Triangle(mesh, new Vector2(.30f,.55f),new Vector2(.15f,.24f),new Vector2(.42f,.28f),color);
                    Triangle(mesh, new Vector2(.70f,.55f),new Vector2(.58f,.28f),new Vector2(.85f,.24f),color);
                    Quad(mesh,.32f,.04f,.46f,.25f,dark); Quad(mesh,.54f,.04f,.68f,.25f,dark); break;
                case MapSymbol.Tree:
                    Quad(mesh,.43f,.03f,.57f,.44f,new Color(.48f,.30f,.16f));
                    Triangle(mesh,new Vector2(.08f,.30f),new Vector2(.92f,.30f),new Vector2(.5f,.94f),color);
                    Triangle(mesh,new Vector2(.22f,.56f),new Vector2(.78f,.56f),new Vector2(.5f,.98f),light); break;
                case MapSymbol.Plant:
                    Quad(mesh,.47f,.08f,.53f,.65f,dark);
                    Ellipse(mesh,.28f,.42f,.23f,.11f,color); Ellipse(mesh,.72f,.56f,.23f,.12f,light);
                    Ellipse(mesh,.37f,.72f,.13f,.21f,color); Ellipse(mesh,.68f,.26f,.20f,.10f,color); break;
                case MapSymbol.Meal:
                    Ellipse(mesh,.50f,.47f,.46f,.30f,light); Ellipse(mesh,.50f,.50f,.36f,.21f,dark);
                    Ellipse(mesh,.42f,.53f,.19f,.14f,color); Ellipse(mesh,.66f,.48f,.10f,.09f,new Color(.8f,.45f,.23f)); break;
                case MapSymbol.Ingredients:
                    Ellipse(mesh,.28f,.63f,.15f,.16f,new Color(.8f,.4f,.3f));
                    Ellipse(mesh,.55f,.72f,.17f,.17f,color); Ellipse(mesh,.76f,.61f,.14f,.15f,light);
                    Quad(mesh,.12f,.18f,.88f,.53f,new Color(.62f,.43f,.23f));
                    Quad(mesh,.10f,.48f,.90f,.57f,new Color(.82f,.64f,.38f));
                    Quad(mesh,.30f,.19f,.34f,.48f,dark); Quad(mesh,.63f,.19f,.67f,.48f,dark); break;
                case MapSymbol.Wood:
                    Log(mesh,.14f,.22f,.85f,.40f,color,light,dark);
                    Log(mesh,.18f,.44f,.89f,.62f,color,light,dark);
                    Log(mesh,.10f,.66f,.81f,.84f,color,light,dark); break;
                case MapSymbol.Bed:
                    Quad(mesh,.12f,.08f,.88f,.92f,dark); Quad(mesh,.19f,.16f,.81f,.82f,color);
                    Quad(mesh,.25f,.66f,.75f,.83f,light); Quad(mesh,.22f,.18f,.78f,.58f,Color.Lerp(color,Color.white,.15f));
                    Quad(mesh,.10f,.02f,.22f,.12f,dark); Quad(mesh,.78f,.02f,.90f,.12f,dark); break;
            }
        }
        private void Log(VertexHelper mesh,float x,float y,float right,float top,Color tint,Color light,Color dark)
        {
            Quad(mesh,x,y,right,top,tint); Ellipse(mesh,right,(y+top)/2,.08f,(top-y)/2,light);
            Ellipse(mesh,right,(y+top)/2,.035f,(top-y)*.22f,dark);
        }
        private Vector2 Position(Vector2 normalized)
        {
            var rect = rectTransform.rect;
            return new Vector2(rect.xMin+rect.width*normalized.x,rect.yMin+rect.height*normalized.y);
        }
        private void Triangle(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Color tint)
        {
            var start=mesh.currentVertCount;
            mesh.AddVert(Position(a),tint,Vector2.zero); mesh.AddVert(Position(b),tint,Vector2.zero); mesh.AddVert(Position(c),tint,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);
        }
        private void Quad(VertexHelper mesh,float x,float y,float right,float top,Color tint)
        {
            Triangle(mesh,new Vector2(x,y),new Vector2(right,y),new Vector2(right,top),tint);
            Triangle(mesh,new Vector2(x,y),new Vector2(right,top),new Vector2(x,top),tint);
        }
        private void Ellipse(VertexHelper mesh,float x,float y,float radiusX,float radiusY,Color tint)
        {
            const int segments=24;
            for(var i=0;i<segments;i++)
            {
                var a=i*Mathf.PI*2/segments; var b=(i+1)*Mathf.PI*2/segments;
                Triangle(mesh,new Vector2(x,y),new Vector2(x+Mathf.Cos(a)*radiusX,y+Mathf.Sin(a)*radiusY),
                    new Vector2(x+Mathf.Cos(b)*radiusX,y+Mathf.Sin(b)*radiusY),tint);
            }
        }
    }
}
