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
            IEnumerable<ResourceSeed> resources,
            IEnumerable<BedSeed> beds = null, bool needsEnabled = false)
        {
            if (blockedTiles == null) throw new ArgumentNullException(nameof(blockedTiles));
            if (colonists == null) throw new ArgumentNullException(nameof(colonists));
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            NeedsEnabled = needsEnabled;
            var bedCopies = new List<BedSeed>();
            if (beds != null) foreach (var bed in beds) bedCopies.Add(bed == null ? null : new BedSeed(bed.Id, bed.Position));
            Beds = bedCopies.AsReadOnly();
            Width = width;
            Height = height;
            BlockedTiles = new List<GridPosition>(blockedTiles).AsReadOnly();

            var colonistCopies = new List<ColonistSeed>();
            foreach (var colonist in colonists)
            {
                colonistCopies.Add(colonist == null
                    ? null
                    : new ColonistSeed(colonist.Id, colonist.Position, colonist.Nutrition, colonist.Rest));
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

        public bool NeedsEnabled { get; }
        public IReadOnlyList<BedSeed> Beds { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<GridPosition> BlockedTiles { get; }
        public IReadOnlyList<ColonistSeed> Colonists { get; }
        public IReadOnlyList<ResourceSeed> Resources { get; }
    }
}
