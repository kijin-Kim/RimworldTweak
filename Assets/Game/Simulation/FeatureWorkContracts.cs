using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("RimworldTweak.Tests.EditMode")]
namespace RimworldTweak.Simulation
{
    public enum WorkCategory { Gather, Haul, Build, Farm, Cook }
    public enum WorkStep { Gather, HaulToStorage, DeliverBuild, Construct, Demolish, Sow, Harvest, DeliverCook, Cook }
    public enum WorkReasonCode { None, Disabled, Drafted, NeedsPriority, NoDesignation, MissingMaterials, NoWorker,
        Unreachable, TargetReserved, OutputFull, TargetReached, AwaitingClearance, InvalidTarget,
        NoAllowedStorage, DestinationReserved, StorageFull }
    public enum WorkCancelReason { Draft, Life, Disabled, Explicit, TargetRemoved, Unreachable, RestoreInvalid }

    // Immutable values only: no live entity or mutable route is exposed to feature rules.
    public sealed class WorkCandidate
    {
        internal WorkCandidate(WorkReadContext context, WorkCategory category, WorkStep step, int targetId,
            int? sourceId, GridPosition cell, long order, IReadOnlyList<GridPosition> route)
            : this(context, category, step, targetId, sourceId, cell, order, route.Count)
        {
            Route = new List<GridPosition>(route).AsReadOnly();
        }

        internal WorkCandidate(WorkReadContext context, WorkCategory category, WorkStep step, int targetId,
            int? sourceId, GridPosition cell, long order, int pathLength)
        {
            Context = context; Category = category; Step = step; TargetId = targetId; SourceId = sourceId;
            WorkCell = cell; RequestOrder = order; PathLength = pathLength;
        }
        internal WorkReadContext Context { get; }
        internal IReadOnlyList<GridPosition> Route { get; }
        public WorkCategory Category { get; }
        public WorkStep Step { get; }
        public int TargetId { get; }
        public int? SourceId { get; }
        public GridPosition WorkCell { get; }
        public long RequestOrder { get; }
        public int PathLength { get; }
        public WorkReasonCode Reason => WorkReasonCode.None;
    }

    public sealed class FeatureWorkOption
    {
        internal FeatureWorkOption(WorkCandidate candidate, WorkReasonCode reason) { Candidate = candidate; Reason = reason; }
        public WorkCandidate Candidate { get; }
        public WorkReasonCode Reason { get; }
    }
    public sealed class FeatureWorkSnapshot
    {
        internal FeatureWorkSnapshot(int pawnId, long actionId, int targetId, WorkStep step, int progress,
            int duration, int requiredInput, WorkReasonCode reason)
        { PawnId=pawnId; ActionId=actionId; TargetId=targetId; Step=step; Progress=progress;
            Duration=duration; RequiredInput=requiredInput; Reason=reason; }
        public int PawnId { get; }
        public long ActionId { get; }
        public int TargetId { get; }
        public WorkStep Step { get; }
        public int Progress { get; }
        public int Duration { get; }
        public int RequiredInput { get; }
        public WorkReasonCode Reason { get; }
    }
    public sealed class FeatureWorkResult
    {
        public FeatureWorkResult(FeatureWorkSnapshot source,int progress,bool completed,ResourceKind consumeKind,
            int consumeAmount,ResourceKind produceKind,int produceAmount,WorkReasonCode reason)
        { Source=source; Progress=progress; Completed=completed; ConsumeKind=consumeKind; ConsumeAmount=consumeAmount;
            ProduceKind=produceKind; ProduceAmount=produceAmount; Reason=reason; }
        public FeatureWorkSnapshot Source { get; }
        public int Progress { get; }
        public bool Completed { get; }
        public ResourceKind ConsumeKind { get; }
        public int ConsumeAmount { get; }
        public ResourceKind ProduceKind { get; }
        public int ProduceAmount { get; }
        public WorkReasonCode Reason { get; }
    }

    public sealed class WorkReadContext
    {
        internal WorkReadContext(object worldToken, long tick, long revision, long mapRevision, ColonistState pawn)
        { WorldToken = worldToken; Tick = tick; Revision = revision; MapRevision = mapRevision;
            PawnId = pawn.Id; ActionId = pawn.ActionId; Position = pawn.Position; }
        internal object WorldToken { get; }
        internal long Revision { get; }
        internal long MapRevision { get; }
        public long Tick { get; }
        public int PawnId { get; }
        public long ActionId { get; }
        public GridPosition Position { get; }
        public IReadOnlyList<FeatureWorkOption> Options { get; private set; } = Array.AsReadOnly(Array.Empty<FeatureWorkOption>());
        public FeatureWorkSnapshot ActiveWork { get; private set; }
        public WorkPolicySnapshot Policy { get; private set; }
        internal void SetPolicy(WorkPolicySnapshot policy)
        { if (Candidates != null) throw new InvalidOperationException("Context already sealed."); Policy = policy; }
        internal void SetFeatureInputs(List<FeatureWorkOption> options, FeatureWorkSnapshot active = null)
        { if (Candidates != null) throw new InvalidOperationException("Context already sealed.");
            Options = new List<FeatureWorkOption>(options).AsReadOnly(); ActiveWork = active; }
        public IReadOnlyList<WorkCandidate> Candidates { get; private set; }
        public WorkReasonCode Reason { get; private set; }
        internal void Seal(List<WorkCandidate> candidates, WorkReasonCode reason)
        { if (Candidates != null) throw new InvalidOperationException("Context already sealed.");
            Candidates = new List<WorkCandidate>(candidates).AsReadOnly(); Reason = reason; }
    }
}
