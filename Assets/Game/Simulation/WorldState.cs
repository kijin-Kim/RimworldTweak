using System;
using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public sealed class WorldState
    {
        private WorldState(
            int width,
            int height,
            List<GridPosition> blockedTiles,
            List<ColonistSeed> colonists,
            List<ResourceSeed> resources)
        {
            Width = width;
            Height = height;
            BlockedTiles = blockedTiles.AsReadOnly();
            Colonists = colonists.AsReadOnly();
            Resources = resources.AsReadOnly();
        }

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<GridPosition> BlockedTiles { get; }
        public IReadOnlyList<ColonistSeed> Colonists { get; }
        public IReadOnlyList<ResourceSeed> Resources { get; }

        public static WorldState Create(ScenarioDefinition scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (scenario.Width <= 0 || scenario.Height <= 0)
            {
                throw new ScenarioValidationException("InvalidDimensions", "Map width and height must be positive.");
            }

            var blockedTiles = new List<GridPosition>();
            var blockedSet = new HashSet<GridPosition>();
            foreach (var position in scenario.BlockedTiles)
            {
                ValidatePosition(position, scenario);
                if (!blockedSet.Add(position))
                {
                    throw new ScenarioValidationException("DuplicateBlockedTile", $"Blocked tile {position} is repeated.");
                }
                blockedTiles.Add(position);
            }

            var ids = new HashSet<int>();
            var colonists = new List<ColonistSeed>();
            foreach (var colonist in scenario.Colonists)
            {
                if (colonist == null)
                {
                    throw new ScenarioValidationException("NullEntity", "A colonist entry is null.");
                }
                ValidateEntity(colonist.Id, colonist.Position, scenario, ids, blockedSet);
                colonists.Add(new ColonistSeed(colonist.Id, colonist.Position));
            }

            var resources = new List<ResourceSeed>();
            foreach (var resource in scenario.Resources)
            {
                if (resource == null)
                {
                    throw new ScenarioValidationException("NullEntity", "A resource entry is null.");
                }
                ValidateEntity(resource.Id, resource.Position, scenario, ids, blockedSet);
                if (resource.Amount <= 0)
                {
                    throw new ScenarioValidationException("InvalidAmount", $"Resource {resource.Id} must have a positive amount.");
                }
                if (!Enum.IsDefined(typeof(ResourceKind), resource.Kind))
                {
                    throw new ScenarioValidationException("InvalidResourceKind", $"Resource {resource.Id} has an unknown kind.");
                }
                resources.Add(new ResourceSeed(resource.Id, resource.Kind, resource.Amount, resource.Position));
            }

            blockedTiles.Sort((left, right) =>
            {
                var byY = left.Y.CompareTo(right.Y);
                return byY != 0 ? byY : left.X.CompareTo(right.X);
            });
            colonists.Sort((left, right) => left.Id.CompareTo(right.Id));
            resources.Sort((left, right) => left.Id.CompareTo(right.Id));

            return new WorldState(scenario.Width, scenario.Height, blockedTiles, colonists, resources);
        }

        private static void ValidatePosition(GridPosition position, ScenarioDefinition scenario)
        {
            if (position.X < 0 || position.Y < 0 || position.X >= scenario.Width || position.Y >= scenario.Height)
            {
                throw new ScenarioValidationException("OutOfBounds", $"Position {position} is outside the map.");
            }
        }

        private static void ValidateEntity(
            int id,
            GridPosition position,
            ScenarioDefinition scenario,
            HashSet<int> ids,
            HashSet<GridPosition> blockedTiles)
        {
            if (id <= 0)
            {
                throw new ScenarioValidationException("InvalidId", "Entity IDs must be positive.");
            }
            if (!ids.Add(id))
            {
                throw new ScenarioValidationException("DuplicateId", $"Entity ID {id} is repeated.");
            }
            ValidatePosition(position, scenario);
            if (blockedTiles.Contains(position))
            {
                throw new ScenarioValidationException("BlockedPosition", $"Entity {id} occupies blocked tile {position}.");
            }
        }
    }
}
