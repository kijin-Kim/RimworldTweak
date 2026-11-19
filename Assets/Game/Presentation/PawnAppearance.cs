using System;

namespace RimworldTweak.Presentation
{
    public sealed class PawnAppearance
    {
        private static readonly PawnAppearance Slim = new PawnAppearance("slim", "short", true);
        private static readonly PawnAppearance Broad = new PawnAppearance("broad", "bob", false);
        private static readonly PawnAppearance Round = new PawnAppearance("round", "bun", true);
        private static readonly PawnAppearance BroadWithJacket = new PawnAppearance("broad", "bob", true);

        public string Body { get; }
        public string Hair { get; }
        public bool HasJacket { get; }

        private PawnAppearance(string body, string hair, bool hasJacket)
        {
            Body = body;
            Hair = hair;
            HasJacket = hasJacket;
        }

        // Saved pawn IDs keep the same appearance when new catalog entries are introduced.
        public static PawnAppearance ForId(int id)
        {
            switch (id)
            {
                case 2: return Broad;
                case 3: return Round;
                case 4: return BroadWithJacket;
                default: return Slim;
            }
        }

        public static PawnAppearance ForRaider(int id) => (id & 1) == 0 ? BroadWithJacket : Round;

        public PawnAppearance WithJacket(bool hasJacket) =>
            HasJacket == hasJacket ? this : new PawnAppearance(Body, Hair, hasJacket);

        public PawnAppearance WithHair(string hair)
        {
            if (string.IsNullOrWhiteSpace(hair)) throw new ArgumentException("Missing hair key", nameof(hair));
            return Hair == hair ? this : new PawnAppearance(Body, hair, HasJacket);
        }

        public bool TryResolve(VisualCatalog catalog, string direction, out VisualDefinition[] layers)
        {
            layers = Array.Empty<VisualDefinition>();
            if (catalog == null || (direction != "front" && direction != "side" && direction != "back"))
                return false;

            if (!catalog.TryGet($"pawn.body.{Body}.{direction}", out var body) ||
                !catalog.TryGet($"pawn.shirt.{Body}.{direction}", out var shirt) ||
                !catalog.TryGet($"pawn.face.{direction}", out var face) ||
                !catalog.TryGet($"pawn.hair.{Hair}.{direction}", out var hair))
                return false;

            if (HasJacket)
            {
                if (!catalog.TryGet($"pawn.jacket.{Body}.{direction}", out var jacket)) return false;
                layers = new[] { body, shirt, jacket, face, hair };
            }
            else layers = new[] { body, shirt, face, hair };
            return true;
        }
    }
}
