using System;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class RescueCommandTests
    {
        private static GridPosition Cell(int x, int y) => new GridPosition(x,y);

        private static SimulationSession Settlement(bool bed = true, bool medicine = false)
        {
            var session = new SimulationSession(new ScenarioDefinition(10,8,Array.Empty<GridPosition>(),
                new[] { new ColonistSeed(1,Cell(1,2)), new ColonistSeed(2,Cell(3,2)), new ColonistSeed(3,Cell(1,4)) },
                medicine ? new[] { new ResourceSeed(10,ResourceKind.Medicine,2,Cell(2,4)) } : Array.Empty<ResourceSeed>(),
                bed ? new[] { new BedSeed(20,Cell(8,2)) } : Array.Empty<BedSeed>()));
            session.World.ApplyCombatDamage(2,100);
            return session;
        }

        private static void Until(WorldState world, Func<bool> reached, int limit = 300)
        {
            for (var i=0;i<limit && !reached();i++) world.Step(1);
            Assert.That(reached(), Is.True, "condition not reached by tick "+world.Tick);
        }

        private static SimulationSession Restore(SimulationSession session)
        {
            var capture = session.CaptureSaveData(true);
            Assert.That(capture.Success, Is.True,capture.ErrorCode);
            Assert.That(SimulationSession.TryRestoreSave(capture.Data,out var restored,out var error),Is.True,error);
            return restored;
        }

        [Test] public void MissingOrUnreachableBedAndReservedPatientRejectWithoutMovingThePatient()
        {
            var world=Settlement(false).World;
            Assert.That(world.OrderRescue(1,2), Is.False);
            Assert.That(world.LastCommandError,Is.EqualTo("NoRescueBed"));
            Assert.That(world.Colonists[1].Position,Is.EqualTo(Cell(3,2)));
            world=Settlement().World;
            Assert.That(world.SetDrafted(1,true),Is.True);
            Assert.That(world.OrderRescue(1,2),Is.False);
            world.SetDrafted(1,false);
            foreach(var p in new[] {Cell(7,2),Cell(9,2),Cell(8,1),Cell(8,3)})
                Assert.That(world.QueueSetBlocked(p,true),Is.True);
            world.Step(1);
            Assert.That(world.OrderRescue(1,2),Is.False);
            Assert.That(world.LastCommandError,Is.EqualTo("Unreachable"));
            Assert.That(world.Beds[0].OwnerId,Is.Null);
            world.QueueSetBlocked(Cell(7,2),false);world.Step(1);
            Assert.That(world.OrderRescue(1,2), Is.True,world.LastCommandError);
            Assert.That(world.OrderRescue(3,2), Is.False);
            Assert.That(world.LastCommandError,Is.EqualTo("PatientReserved"));
            Assert.That(world.Beds[0].OwnerId,Is.EqualTo(1));
        }

        [Test] public void RescueSurvivesPickupCarryAndHandoffWithoutHealing()
        {
            var session=Settlement(true,true);var world=session.World;
            Assert.That(world.OrderRescue(1,2),Is.True);
            session=Restore(session);world=session.World;
            Assert.That(world.GetCareDiagnostic(2),Is.EqualTo(WorkReasonCode.TargetReserved));
            Until(world,()=>world.RescueFor(1)?.Carrying==true && world.Colonists[0].Position==Cell(5,2));
            Assert.That(world.Colonists[1].Position,Is.EqualTo(Cell(5,2)));
            var restored=Restore(session).World;
            Until(restored,()=>restored.RescuedBedFor(2)==20);
            Until(world,()=>world.RescuedBedFor(2)==20);
            Assert.That(world.Tick,Is.EqualTo(restored.Tick));
            Assert.That(restored.Colonists[1].Position, Is.EqualTo(restored.Beds[0].Position));
            Assert.That(restored.Colonists[1].Health,Is.Zero);
            Assert.That(restored.MedicineConsumed,Is.Zero);
            Assert.That(restored.Beds[0].IsOccupied,Is.True);
            session=Restore(session);world=session.World;
            Until(world,()=>world.Colonists[1].TreatedBy.HasValue);
            session=Restore(session);world=session.World;
            Until(world,()=>world.Colonists[1].Health==40);
            Assert.That(world.MedicineConsumed,Is.EqualTo(1));
            Assert.That(world.RescuedBedFor(2),Is.Null);
            Assert.That(world.Beds[0].IsOccupied,Is.False);
        }

        [Test] public void DraftOrCancelDuringTransportDropsPatientAndReleasesBothReservations()
        {
            var session=Settlement();var world=session.World;
            world.OrderRescue(1,2);
            Until(world,()=>world.RescueFor(1)?.Carrying==true && world.Colonists[0].Position==Cell(5,2));
            var restored=Restore(session).World;
            Assert.That(world.SetDrafted(1,true),Is.True);
            Assert.That(restored.CancelRescue(1),Is.True);
            foreach(var branch in new[] {world,restored})
            {
                Assert.That(branch.Beds[0].OwnerId,Is.Null);
                Assert.That(branch.Colonists[1].Position,Is.EqualTo(Cell(5,2)));
                Assert.That(branch.RescuerFor(2),Is.Null);
                Assert.That(branch.OrderRescue(3,2),Is.True);
            }
        }

        [Test] public void RescuerDownAndDestroyedBedReleasePatientAndAllowRecovery()
        {
            var world=Settlement().World;
            world.OrderRescue(1,2);Until(world,()=>world.RescueFor(1)?.Carrying==true);
            world.ApplyCombatDamage(1,100);
            Assert.That(world.RescuerFor(2),Is.Null);Assert.That(world.Beds[0].OwnerId,Is.Null);
            Assert.That(world.OrderRescue(3,2),Is.True);
            world.ApplyCombatDamage(20,100);
            Assert.That(world.RescueFor(3),Is.Null);Assert.That(world.Beds[0].Exists,Is.False);
            Assert.That(world.Colonists[1].Health,Is.Zero);
            world=Settlement().World;
            world.OrderRescue(1,2);Until(world,()=>world.RescuedBedFor(2)==20);
            world.QueueSetBedExists(20,false);world.Step(1);
            Assert.That(world.RescuedBedFor(2),Is.Null);Assert.That(world.Colonists[1].Position,Is.EqualTo(Cell(8,2)));
        }

        [Test] public void FieldCareWithoutBedStillWorksAndActiveTreatmentRejectsRescue()
        {
            var world=Settlement(false,true).World;
            Until(world,()=>world.Colonists[1].TreatedBy.HasValue);
            Assert.That(world.OrderRescue(3,2),Is.False);
            Until(world,()=>world.Colonists[1].Health==40);
            Assert.That(world.MedicineConsumed,Is.EqualTo(1));
        }

        [Test] public void InconsistentTransportSaveIsRejectedWithoutTouchingLivePatient()
        {
            var session=Settlement();var world=session.World;world.OrderRescue(1,2);
            var captured=session.CaptureSaveData(true).Data;
            captured.World.Rescues[0].Carrying=true;
            Assert.That(SimulationSession.TryRestoreSave(captured,out _,out _),Is.False);
            Assert.That(world.RescueFor(1).Carrying,Is.False);
            Assert.That(world.Beds[0].OwnerId,Is.EqualTo(1));
        }
    }
}
