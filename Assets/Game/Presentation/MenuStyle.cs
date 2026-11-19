using UnityEngine;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    internal static class MenuStyle
    {
        internal static readonly Color Panel = new Color(.22f, .22f, .21f, .98f);
        internal static readonly Color Control = new Color(.30f, .30f, .28f);
        internal static readonly Color Active = new Color(.48f, .53f, .36f);
        internal static readonly Color Text = new Color(.96f, .94f, .88f);
        internal static readonly Color Accent = new Color(.73f, .80f, .56f);

        internal static void ApplySurface(Image image, VisualCatalog visuals)
        {
            if (visuals == null || !visuals.TryGet("ui.panel", out var surface)) return;
            image.sprite = surface.Sprite;
            image.type = Image.Type.Sliced;
        }

        internal static void ApplyButtonColors(Button button)
        {
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, .93f);
            colors.selectedColor = new Color(.78f, .90f, 1f);
            colors.pressedColor = new Color(.72f, .74f, .68f);
            colors.disabledColor = new Color(.68f, .68f, .68f);
            button.colors = colors;
        }
    }
}
