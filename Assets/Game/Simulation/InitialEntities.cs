namespace RimworldTweak.Simulation
{
    public enum ResourceKind
    {
        Wood,
        Ingredients,
        Meal,
        Medicine
    }

    public sealed class ColonistSeed
    {
        public ColonistSeed(int id, GridPosition position, int nutrition = 10000, int rest = 10000)
        {
            Id = id;
            Position = position;
            Nutrition = nutrition;
            Rest = rest;
        }

        public int Nutrition { get; }
        public int Rest { get; }
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
