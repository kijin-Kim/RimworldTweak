using System;
using NUnit.Framework;
using RimworldTweak.Simulation;
namespace RimworldTweak.Tests.EditMode
{
    public sealed class PlayerControlTests
    {
        private static GridPosition P(int x,int y)=>new GridPosition(x,y);
        [Test] public void ManualMoveRequiresDraftAndUndraftClearsPendingMove()
        {
            var w=WorldState.Create(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),new[]{new ColonistSeed(1,P(1,1))},Array.Empty<ResourceSeed>()));
            Assert.That(w.QueueMove(1,P(3,1)),Is.False);Assert.That(w.LastCommandError,Is.EqualTo("NotDrafted"));
            Assert.That(w.PendingCommandCount,Is.Zero);Assert.That(w.Colonists[0].HasPendingCommand,Is.False);
            w.SetDrafted(1,true);Assert.That(w.QueueMove(1,P(3,1)),Is.True);
            w.SetDrafted(1,false);Assert.That(w.PendingCommandCount,Is.Zero);
            w.Step(20);Assert.That(w.Colonists[0].Position,Is.EqualTo(P(1,1)));
            Assert.That(w.QueueMove(1,P(3,1)),Is.False);
            w.SetDrafted(1,true);Assert.That(w.QueueMove(1,P(3,1)),Is.True);w.Step(10);
            Assert.That(w.Colonists[0].Position,Is.EqualTo(P(3,1)));
        }
        [Test] public void RejectedManualMovePreservesAutomaticConstructionClaim()
        {
            var w=WorldState.Create(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),new[]{new ColonistSeed(1,P(1,1))},new[]{new ResourceSeed(101,ResourceKind.Wood,12,P(2,1))},needsEnabled:false));
            w.PlaceBlueprint(BuildingKind.Bed,P(5,4));w.Step(1);
            var pawn=w.Colonists[0];var action=pawn.ActionId;var target=pawn.WorkTargetId;
            Assert.That(w.QueueMove(1,P(6,6)),Is.False);Assert.That(pawn.ActionId,Is.EqualTo(action));
            Assert.That(pawn.WorkTargetId,Is.EqualTo(target));Assert.That(w.Resources[0].OwnerId,Is.EqualTo(1));
            Assert.That(w.PendingCommandCount,Is.Zero);
        }
    }
}
