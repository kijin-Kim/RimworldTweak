namespace RimworldTweak.Simulation
{
    public enum ResourceKind
    {
        Wood,
        Ingredients,
        Meal
    }

    public sealed class ColonistSeed
    {
        public ColonistSeed(int id, GridPosition position)
        {
            Id = id;
            Position = position;
        }

        public int Id { get; }
        public GridPosition Position { get; }
    }

    public sealed class ResourceSeed
    {
        public ResourceSeed(int id, ResourceKind kind, int amount, GridPosition position)
        {
            Id = id;
            Kind = kind;
            Amount = amount;
            Position = position;
        }

        public int Id { get; }
        public ResourceKind Kind { get; }
        public int Amount { get; }
        public GridPosition Position { get; }
    }
}
