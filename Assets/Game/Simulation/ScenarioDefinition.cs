using System;
using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public sealed class ScenarioDefinition
    {
        public ScenarioDefinition(
            int width,
            int height,
            IEnumerable<GridPosition> blockedTiles,
            IEnumerable<ColonistSeed> colonists,
            IEnumerable<ResourceSeed> resources)
        {
            if (blockedTiles == null) throw new ArgumentNullException(nameof(blockedTiles));
            if (colonists == null) throw new ArgumentNullException(nameof(colonists));
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            Width = width;
            Height = height;
            BlockedTiles = new List<GridPosition>(blockedTiles).AsReadOnly();

            var colonistCopies = new List<ColonistSeed>();
            foreach (var colonist in colonists)
            {
                colonistCopies.Add(colonist == null
                    ? null
                    : new ColonistSeed(colonist.Id, colonist.Position));
            }
            Colonists = colonistCopies.AsReadOnly();

            var resourceCopies = new List<ResourceSeed>();
            foreach (var resource in resources)
            {
                resourceCopies.Add(resource == null
                    ? null
                    : new ResourceSeed(resource.Id, resource.Kind, resource.Amount, resource.Position));
            }
            Resources = resourceCopies.AsReadOnly();
        }

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<GridPosition> BlockedTiles { get; }
        public IReadOnlyList<ColonistSeed> Colonists { get; }
        public IReadOnlyList<ResourceSeed> Resources { get; }
    }
}
