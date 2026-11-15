using RimworldTweak.Simulation;
using UnityEngine;

namespace RimworldTweak.Presentation
{
    public enum ViewKind { Tile, Colonist, Resource, Bed }

    public sealed class WorldMarker : MonoBehaviour
    {
        public ViewKind Kind { get; private set; }
        public int EntityId { get; private set; }
        public GridPosition Position { get; private set; }
        public int Amount { get; private set; }
        public bool IsBlocked { get; private set; }

        internal void SetState(ViewKind kind, int id, GridPosition position, int amount, bool blocked)
        {
            Kind = kind;
            EntityId = id;
            Position = position;
            Amount = amount;
            IsBlocked = blocked;
        }
    }
}
