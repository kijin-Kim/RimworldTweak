using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public enum CropStage { Empty, Growing, Mature }

    // A cell keeps its identity across harvest/replant cycles; stage describes its current crop.
    public sealed partial class CropState
    {
        internal CropState(int id, int zoneId, GridPosition position, long requestOrder)
        { Id = id; ZoneId = zoneId; Position = position; RequestOrder = requestOrder; }
        internal readonly InteractionClaim Claim = new InteractionClaim();
        public int Id { get; }
        public int ZoneId { get; }
        public GridPosition Position { get; }
        public bool Exists { get; internal set; } = true;
        public CropStage Stage { get; internal set; }
        public int SowProgress { get; internal set; }
        public int HarvestProgress { get; internal set; }
        public int GrowthTicks { get; internal set; }
        public long PlantedTick { get; internal set; } = -1;
        public long RequestOrder { get; internal set; }
        public WorkReasonCode Reason { get; internal set; }
        public int? OwnerId => Claim.OwnerId;
        public long OwnerActionId => Claim.ActionId;
    }

    public sealed partial class GrowingZoneState
    {
        internal GrowingZoneState(int id, List<CropState> cells)
        { Id = id; Cells = new List<CropState>(cells).AsReadOnly(); }
        public int Id { get; }
        public bool Exists { get; internal set; } = true;
        public bool SowingEnabled { get; internal set; } = true;
        public IReadOnlyList<CropState> Cells { get; }
    }
}
