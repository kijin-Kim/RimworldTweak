using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class RaidRecoveryTests
    {
        private static void Until(WorldState world, Func<bool> condition, int limit)
        {
            for (var i = 0; i < limit && !condition(); i++) world.Step(1);
            Assert.That(condition(), Is.True, "Condition did not arrive by tick " + world.Tick);
        }

        private static SimulationSession RoundTrip(SimulationSession session)
        {
            var folder = Path.Combine("TestResults", "RaidSave", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(folder, "settlement-v2.json");
            var file = new SaveFileService();
            var captured = session.CaptureSaveData();
            Assert.That(captured.Success, Is.True);
            var save = file.Save(path, captured.Data);
            Assert.That(save.Success, Is.True, save.ErrorCode);
            var loaded = file.Load(path);
            Assert.That(loaded.Success, Is.True, loaded.ErrorCode);
            Assert.That(SimulationSession.TryRestoreSave(loaded.Data, out var restored, out var error), Is.True, error);
            return restored;
        }


        private static SimulationSession NormalTrial(int chance)
        {
            var scenario = new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(), new[] {
                new ColonistSeed(1, new GridPosition(5, 3)),
                new ColonistSeed(2, new GridPosition(6, 4)),
                new ColonistSeed(3, new GridPosition(1, 3))
            }, Array.Empty<ResourceSeed>());
            var session = new SimulationSession(scenario);
            Assert.That(session.World.StartNormalRaid(chance), Is.True);
            return session;
        }

        [Test]
        public void ImmediateTargetsPersistUntilEligibleDamage()
        {
            foreach (var chance in new[] { 0, 100 })
            {
                var world = NormalTrial(chance).World;
                var first = world.Raiders[0];
                Assert.That(first.Attack.TargetId, Is.EqualTo(1)); // Equal distance, lower ID.
                Assert.That(world.Raiders[1].Attack.TargetId, Is.EqualTo(2));
                Assert.That(world.StartNormalRaid(), Is.False);
                Assert.That(world.Raiders.Count, Is.EqualTo(2));
                first.Position = new GridPosition(1, 4);
                world.Step(1);
                Assert.That(first.Attack.TargetId, Is.EqualTo(1));
                first.Attack.Preparation = 9;
                var seed = world.RetargetState;
                var hits = new SortedDictionary<int, int>();
                WorldState.AddDamage(hits, first.Id, 5);
                WorldState.AddDamage(hits, first.Id, 5);
                world.ResolveCombatDamage(hits);
                Assert.That(first.Health, Is.EqualTo(40));
                Assert.That(first.Attack.TargetId, Is.EqualTo(chance == 100 ? 3 : 1));
                Assert.That(first.Attack.Preparation, Is.EqualTo(chance == 100 ? 0 : 9));
                Assert.That(world.RetargetState, Is.EqualTo(NextState(seed)));
                var forced = first.Attack.TargetId.Value;
                seed = world.RetargetState;
                world.ResolveCombatDamage(new SortedDictionary<int, int> { {forced, 100}, {first.Id, 1} });
                Assert.That(first.Attack.TargetId, Is.Not.EqualTo(forced));
                Assert.That(world.RetargetState, Is.EqualTo(seed));
            }
        }

        private static uint NextState(uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            return state ^ (state << 5);
        }

        [Test]
        public void ProbabilityBoundaryAndPostDrawFileContinuation()
        {
            foreach (var roll in new[] { 29, 30 })
            {
                var session = NormalTrial(30);
                var data = session.CaptureSaveData().Data;
                uint seed = 1;
                while ((NextState(seed) * 100UL >> 32) != (uint)roll ||
                    (NextState(NextState(seed)) * 100UL >> 32) < 30 == (roll < 30)) seed++;
                data.World.Raid.RetargetState = seed;
                Assert.That(SimulationSession.TryRestoreSave(data, out session, out var error), Is.True, error);
                var world = session.World;
                var first = world.Raiders[0];
                first.Position = new GridPosition(1, 4);
                first.Attack.Cooldown = 7;
                var hits = new SortedDictionary<int, int> { {first.Id, 5} };
                world.ResolveCombatDamage(hits);
                Assert.That(first.Attack.TargetId, Is.EqualTo(roll < 30 ? 3 : 1));
                Assert.That(first.Attack.Cooldown, Is.EqualTo(7));
                var copy = RoundTrip(session).World;
                foreach (var compared in new[] {world, copy})
                {
                    compared.ResolveCombatDamage(hits);
                    Assert.That(compared.Raiders[0].Attack.TargetId, Is.EqualTo(3));
                    Assert.That(compared.RetargetState, Is.EqualTo(NextState(NextState(seed))));
                }
                for (var tick = 0; tick < 1500 && world.RaidPhase == RaidPhase.Active; tick++)
                {
                    world.Step(1);
                    copy.Step(1);
                    Assert.That(copy.Raiders.Select(r => (r.Health,r.Attack.TargetId,r.Attack.Preparation,r.Attack.Cooldown,r.Position)),
                        Is.EqualTo(world.Raiders.Select(r => (r.Health,r.Attack.TargetId,r.Attack.Preparation,r.Attack.Cooldown,r.Position))));
                }
                Assert.That(world.RaidOutcome, Is.Not.EqualTo(RaidOutcome.None));
                Assert.That(copy.RaidOutcome, Is.EqualTo(world.RaidOutcome));
            }
        }

        [Test]
        public void RealShotsDrawOnlyForSurvivingHitRaider()
        {
            foreach (var shooterCount in new[] { 1, 2 })
            {
                var world = NormalTrial(100).World;
                var first = world.Raiders[0];
                // Both defenders have clear shots from their original tiles.
                first.Position = new GridPosition(6, 5);
                for (var id = 1; id <= shooterCount; id++)
                {
                    Assert.That(world.SetDrafted(id, true) && world.OrderAttack(id, first.Id), Is.True);
                    world.Colonists[id-1].Attack.Preparation = 9;
                }
                var seed = world.RetargetState;
                world.Step(1);
                Assert.That(first.Health, Is.EqualTo(50 - shooterCount * 25));
                Assert.That(world.RetargetState, Is.EqualTo(shooterCount == 1 ? NextState(seed) : seed));
                Assert.That(world.Raiders.Count, Is.EqualTo(shooterCount == 1 ? 2 : 1));
                if (shooterCount == 1) Assert.That(first.Attack.TargetId, Is.EqualTo(2));
            }
        }

        [Test]
        public void DebugRequestPreservesWorkAndRejectsInvalidStarts()
        {
            var world = WorldState.Create(WorkScenario.Create());
            var node = world.Gatherables.First();
            Assert.That(world.QueueDesignateGather(node.Id, true), Is.True);
            Assert.That(world.QueueCreateStorage(new GridPosition(0,4), new GridPosition(1,4)), Is.True);
            Until(world, () => world.Colonists.Any(p => p.CargoAmount > 0), 3000);
            var carrier = world.Colonists.First(p => p.CargoAmount > 0);
            var cargo = carrier.CargoAmount;
            var action = carrier.ActionId;
            var position = carrier.Position;
            var tick = world.Tick;
            Assert.That(world.RaidPhase, Is.EqualTo(RaidPhase.Idle));
            Assert.That(world.StartNormalRaid(), Is.True);
            Assert.That((world.Tick, carrier.Position, carrier.CargoAmount, carrier.ActionId),
                Is.EqualTo((tick, position, cargo, action)));

            world = WorldState.Create(SandboxScenario.Create());
            Assert.That(world.QueueSetBlocked(world.RaidEntrance(0), true), Is.True);
            Assert.That(world.StartNormalRaid(), Is.False);
            world.Step(1);
            Assert.That(world.StartNormalRaid(), Is.False);
            Assert.That(world.QueueSetBlocked(world.RaidEntrance(0), false), Is.True);
            world.Step(1);
            foreach (var pawn in world.Colonists) world.ApplyCombatDamage(pawn.Id, 100);
            Assert.That(world.StartNormalRaid(), Is.False);
            Assert.That(world.RaidPhase, Is.EqualTo(RaidPhase.Idle));
            Assert.That(world.Raiders, Is.Empty);
            world.Colonists[1].Health = 1;
            Assert.That(world.StartNormalRaid(), Is.True);
            Assert.That(world.Raiders.All(r => r.Attack.TargetId == world.Colonists[1].Id), Is.True);
        }

        [Test]
        public void CompletedStrikeIsAppliedOnceBeforeRetarget()
        {
            var world = NormalTrial(100).World;
            var first = world.Raiders[0];
            var original = world.Colonists[0];
            var nearer = world.Colonists[1];
            // The original target is adjacent; a lower-priority candidate now occupies the attacker's tile.
            first.Position = nearer.Position;
            Assert.That(world.SetDrafted(original.Id, true), Is.True);
            Assert.That(world.QueueMove(original.Id, new GridPosition(5,4)), Is.True);
            world.Step(5);
            first.Position = nearer.Position;
            first.Attack.Preparation = 9;
            Assert.That(world.SetDrafted(nearer.Id,true) && world.OrderAttack(nearer.Id,first.Id), Is.True);
            nearer.Attack.Preparation = 9;
            var hp = original.Health;
            var otherHp = nearer.Health;
            world.Step(1);
            Assert.That(original.Health, Is.EqualTo(hp-10));
            Assert.That(nearer.Health, Is.EqualTo(otherHp));
            Assert.That(first.Health, Is.EqualTo(25));
            Assert.That(first.Attack.TargetId, Is.EqualTo(nearer.Id));
            Assert.That(first.Attack.Cooldown, Is.EqualTo(10));
            Assert.That(first.Attack.Preparation, Is.Zero);
            world.Step(1);
            Assert.That(first.Attack.Cooldown, Is.EqualTo(9));
        }

        [Test]
        public void GeneralRaidRejectsDamagedSaveAndLoadsHistoricalFiles()
        {
            var session = NormalTrial(30);
            foreach (var corrupt in new Action<RaidSaveData>[] { r => r.Kind = null, r => r.RetargetPercent = null,
                r => r.RetargetPercent = 101, r => r.RetargetState = 0, r => r.ObjectiveId = 123 })
            {
                var data = session.CaptureSaveData().Data;
                corrupt(data.World.Raid);
                Assert.That(SimulationSession.TryRestoreSave(data, out _, out _), Is.False);
            }
            foreach (var phase in new[] { RaidPhase.Idle, RaidPhase.Warning, RaidPhase.Active, RaidPhase.Resolved })
            {
                var file = new SaveFileService().Load(Path.Combine("Tools","TestFixtures","RaidV2",phase + ".json"));
                Assert.That(file.Success, Is.True, file.ErrorCode);
                Assert.That(SimulationSession.TryRestoreSave(file.Data, out var legacy, out var error), Is.True, error);
                Assert.That(legacy.World.RaidPhase, Is.EqualTo(phase));
                Assert.That(legacy.World.NormalRaid, Is.False);
                legacy.Step(1);
                Assert.That(legacy.World.NormalRaid, Is.False);
            }
        }

        [Test]
        public void WarningSpawnsOnceAndPausedCommandsDoNotDamage()
        {
            var session = new SimulationSession(RaidScenario.Create());
            var world = session.World;
            Assert.That(world.StartRaid(world.Buildings.Single(b => b.Kind == BuildingKind.Stove).Id), Is.True);
            Assert.That(world.RaidRemaining, Is.EqualTo(WorldState.RaidWarningTicks));
            Assert.That(world.Raiders, Is.Empty);
            Assert.That(world.SetDrafted(1, true), Is.True);
            Assert.That(world.Tick, Is.Zero);
            Assert.That(world.QueueSetBlocked(world.RaidEntrance(0), true), Is.False);
            Assert.That(RoundTrip(session).World.RaidRemaining, Is.EqualTo(WorldState.RaidWarningTicks));
            world.Step(WorldState.RaidWarningTicks - 1);
            Assert.That(world.Raiders, Is.Empty);
            world.Step(1);
            Assert.That(world.Raiders.Count, Is.EqualTo(2));
            Assert.That(world.StartRaid(world.Buildings.Single(b => b.Kind == BuildingKind.Stove).Id), Is.False);
            Assert.That(world.OrderAttack(1, world.Raiders[0].Id), Is.True);
            Assert.That(world.Raiders[0].Health, Is.EqualTo(50));
            var restored = RoundTrip(session);
            for (var i = 0; i < 80; i++)
            {
                session.Step(1);
                restored.Step(1);
                Assert.That(restored.World.Raiders.Select(x => (x.Id, x.Health, x.Position, x.Attack.Preparation)),
                    Is.EqualTo(world.Raiders.Select(x => (x.Id, x.Health, x.Position, x.Attack.Preparation))));
                Assert.That(restored.World.Colonists.Select(x => (x.Health, x.Attack.Preparation, x.Attack.Cooldown)),
                    Is.EqualTo(world.Colonists.Select(x => (x.Health, x.Attack.Preparation, x.Attack.Cooldown))));
            }
        }

        [Test]
        public void RealMeleeDownsColonistAndMedicineRestoresWorkEligibility()
        {
            var session = new SimulationSession(RaidScenario.Create());
            var world = session.World;
            var first = world.Colonists[0];
            Assert.That(world.SetDrafted(first.Id, true), Is.True);
            Assert.That(world.QueueMove(first.Id, new GridPosition(13, 5)), Is.True);
            Assert.That(world.StartRaid(world.Buildings.Single(b => b.Kind == BuildingKind.Stove).Id), Is.True);
            Until(world, () => first.IsDowned, 360);
            Assert.That(world.ToggleDraft(first.Id), Is.False);
            Assert.That(world.QueueMove(first.Id, new GridPosition(12, 5)), Is.False);
            Assert.That(world.RaidDownedCount, Is.GreaterThanOrEqualTo(1));
            // Help remaining colonists retreat before asking a worker to approach.
            foreach (var pawn in world.Colonists.Where(p => !p.IsDowned))
                Assert.That(world.SetDrafted(pawn.Id, true), Is.True);
            var remaining = world.Colonists.Where(p => !p.IsDowned).ToArray();
            foreach (var pawn in remaining)
                Assert.That(world.QueueMove(pawn.Id, new GridPosition(11, 7)), Is.True);
            Until(world, () => world.Raiders.Any() && remaining.Any(p => p.Attack.TargetId == null) &&
                remaining.All(p => p.Position == new GridPosition(11, 7) || p.IsDowned), 55);
            for (var tick = 0; tick < 130 && world.RaidPhase == RaidPhase.Active; tick++)
            {
                var target = world.Raiders.FirstOrDefault();
                if (target == null) break;
                foreach (var pawn in remaining.Where(p => !p.IsDowned)) world.OrderAttack(pawn.Id, target.Id);
                world.Step(1);
            }
            Assert.That(world.RaidOutcome, Is.EqualTo(RaidOutcome.Defended));
            foreach (var pawn in remaining.Where(p => p.IsDrafted)) world.SetDrafted(pawn.Id, false);
            Until(world, () => world.Colonists.Any(p => p.CareProgress == 39 && p.WorkTargetId == first.Id), 600);
            var restored = RoundTrip(session);
            var cancelled = RoundTrip(session);
            var caregiver = cancelled.World.Colonists.Single(p => p.CareProgress == 39 && p.WorkTargetId == first.Id);
            var medicineBeforeCancel = cancelled.World.MedicineConsumed;
            Assert.That(cancelled.World.SetDrafted(caregiver.Id, true), Is.True);
            Assert.That(cancelled.World.Colonists[0].TreatedBy, Is.Null);
            cancelled.Step(1);
            Assert.That(cancelled.World.Colonists[0].Health, Is.Zero);
            Assert.That(cancelled.World.MedicineConsumed, Is.EqualTo(medicineBeforeCancel));
            Assert.That(cancelled.World.Resources.Where(r => r.Exists && r.Kind == ResourceKind.Medicine).Sum(r => r.Amount)
                + cancelled.World.CargoTotal(ResourceKind.Medicine) + cancelled.World.MedicineConsumed, Is.EqualTo(12));
            world.Step(1);
            restored.Step(1);
            Assert.That(first.Health, Is.EqualTo(40));
            Assert.That(restored.World.Colonists[0].Health, Is.EqualTo(first.Health));
            Assert.That(restored.World.MedicineConsumed, Is.EqualTo(world.MedicineConsumed));
            Assert.That(world.MedicineConsumed, Is.GreaterThan(0));
            Assert.That(world.RaidDownedCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void ActualObjectiveDamageAndRepairPreserveStoveIdentity()
        {
            var scenario = new ScenarioDefinition(16, 12,
                Enumerable.Range(0, 12).Select(y => new GridPosition(7, y)),
                new[] { new ColonistSeed(1, new GridPosition(5, 4)) },
                new[] { new ResourceSeed(2, ResourceKind.Wood, 12, new GridPosition(5, 5)) },
                initialRaidStove: new GridPosition(10, 5));
            var session = new SimulationSession(scenario);
            var world = session.World;
            var stove = world.Buildings.Single(b => b.Kind == BuildingKind.Stove);
            Assert.That(world.StartRaid(stove.Id), Is.True);
            Until(world, () => world.RaidPhase == RaidPhase.Resolved, 450);
            Assert.That(world.RaidOutcome, Is.EqualTo(RaidOutcome.ObjectiveLost));
            Assert.That(stove.Durability, Is.Zero);
            Assert.That(world.RaidFacilityDamage, Is.EqualTo(100));
            Assert.That(world.SetRepair(stove.Id, true), Is.True);
            world.Step(40);
            Assert.That(stove.RepairProgress, Is.Zero);
            Assert.That(world.QueueSetBlocked(new GridPosition(7, 4), false), Is.True);
            Until(world, () => stove.RepairProgress == 39, 250);
            var restored = RoundTrip(session);
            restored.Step(1);
            Assert.That(restored.World.Buildings.Single(b => b.Kind == BuildingKind.Stove).Durability, Is.EqualTo(40));
            Assert.That(restored.World.RepairWoodConsumed, Is.EqualTo(2));
            Until(world, () => stove.Durability == 100, 400);
            Assert.That(world.Buildings.Single(b => b.Kind == BuildingKind.Stove), Is.SameAs(stove));
            Assert.That(world.RepairWoodConsumed, Is.EqualTo(6));
            Assert.That(world.ResourceTotal(ResourceKind.Wood, false), Is.EqualTo(6));
            Assert.That(world.RaidOutcome, Is.EqualTo(RaidOutcome.ObjectiveLost));
            Assert.That(world.StartRaid(stove.Id), Is.True);
            Assert.That(world.Raiders, Is.Empty);
        }

        [Test]
        public void InitialBedDamageReleasesSleeperAndRepairRestoresSameBed()
        {
            var world = WorldState.Create(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(),
                new[] { new ColonistSeed(1, new GridPosition(2, 1), 10000, 500),
                    new ColonistSeed(2, new GridPosition(3, 3)) },
                new[] { new ResourceSeed(10, ResourceKind.Wood, 12, new GridPosition(1, 2)) },
                new[] { new BedSeed(20, new GridPosition(2, 2)) }, true));
            var bed = world.Beds.Single();
            Until(world, () => bed.IsOccupied, 25);
            world.ApplyCombatDamage(bed.Id, 100);
            Assert.That(bed.Exists, Is.False);
            Assert.That(bed.OwnerId, Is.Null);
            Assert.That(world.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.Idle));
            Assert.That(world.SetDemolition(bed.Id, true), Is.False);
            Assert.That(world.SetRepair(bed.Id, true), Is.True);
            Until(world, () => bed.Exists, 130);
            Assert.That(world.Beds.Single(), Is.SameAs(bed));
            Assert.That(world.RepairWoodConsumed, Is.EqualTo(2));
        }

        [Test]
        public void FailedDefenseFreezesTickAndResetRestoresScenario()
        {
            var session = new SimulationSession(new ScenarioDefinition(16, 12, Array.Empty<GridPosition>(),
                new[] { new ColonistSeed(1, new GridPosition(13, 5)) }, Array.Empty<ResourceSeed>(),
                initialRaidStove: new GridPosition(5, 5)));
            var world = session.World;
            Assert.That(world.StartRaid(world.Buildings.Single().Id), Is.True);
            Until(world, () => world.RaidOutcome == RaidOutcome.AllDown, 360);
            var tick = world.Tick;
            world.Step(100);
            Assert.That(world.Tick, Is.EqualTo(tick));
            Assert.That(world.Raiders, Is.Empty);
            Assert.That(RoundTrip(session).World.RaidDefeatStopsTime, Is.True);
            session.Reset();
            Assert.That(session.World.RaidPhase, Is.EqualTo(RaidPhase.Idle));
            Assert.That(session.World.Colonists[0].Health, Is.EqualTo(100));
        }

        [Test]
        public void TrappedRaidersTimeOutWithoutLeavingAttackTargets()
        {
            var world = WorldState.Create(new ScenarioDefinition(16, 12,
                Enumerable.Range(0, 12).Select(y => new GridPosition(13, y)),
                new[] { new ColonistSeed(1, new GridPosition(5, 5)) }, Array.Empty<ResourceSeed>(),
                initialRaidStove: new GridPosition(4, 5)));
            Assert.That(world.StartRaid(world.Buildings.Single().Id), Is.True);
            world.Step(WorldState.RaidWarningTicks + WorldState.RaidCombatTicks);
            Assert.That(world.RaidOutcome, Is.EqualTo(RaidOutcome.TimeLimit));
            Assert.That(world.RaidPhase, Is.EqualTo(RaidPhase.Retreating));
            Assert.That(world.Raiders.All(enemy => enemy.Attack.TargetId == null), Is.True);
            world.Step(WorldState.RaidRetreatTicks);
            Assert.That(world.RaidPhase, Is.EqualTo(RaidPhase.Resolved));
            Assert.That(world.Raiders, Is.Empty);
            Assert.That(world.Colonists[0].Health, Is.EqualTo(100));
        }

        [Test]
        public void ClosingLineOfFireAtPreparationBoundaryCancelsShot()
        {
            var gap = new GridPosition(13, 5);
            var world = WorldState.Create(new ScenarioDefinition(16, 12,
                Enumerable.Range(0, 12).Where(y => y != 5).Select(y => new GridPosition(13, y)),
                new[] { new ColonistSeed(1, new GridPosition(11, 5)) }, Array.Empty<ResourceSeed>(),
                initialRaidStove: new GridPosition(5, 5)));
            Assert.That(world.StartRaid(world.Buildings.Single().Id), Is.True);
            Assert.That(world.SetDrafted(1, true), Is.True);
            world.Step(WorldState.RaidWarningTicks);
            var enemy = world.Raiders[0];
            var actor = world.Colonists[0];
            Assert.That(world.OrderAttack(actor.Id, enemy.Id), Is.True);
            world.Step(9);
            Assert.That(actor.Attack.Preparation, Is.EqualTo(9));
            Assert.That(world.QueueSetBlocked(gap, true), Is.True);
            world.Step(1);
            Assert.That(enemy.Health, Is.EqualTo(50));
            Assert.That(actor.Attack.Preparation, Is.Zero);
            Assert.That(actor.Attack.BlockedReason, Is.EqualTo("AttackBlocked"));
            Assert.That(world.QueueSetBlocked(gap, false), Is.True);
            world.Step(10);
            Assert.That(enemy.Health, Is.EqualTo(25));
            Assert.That(actor.Attack.Cooldown, Is.EqualTo(10));
            Assert.That(world.OrderAttack(actor.Id, enemy.Id), Is.True);
            world.Step(10);
            Assert.That(enemy.Health, Is.EqualTo(25));
        }

        [Test]
        public void BrokenWallWaitsForClearTileBeforeConsumingRepairWood()
        {
            var site = new GridPosition(4, 4);
            var world = WorldState.Create(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(),
                new[] { new ColonistSeed(1, new GridPosition(2, 4)), new ColonistSeed(2, new GridPosition(1, 1)) },
                new[] { new ResourceSeed(10, ResourceKind.Wood, 12, new GridPosition(2, 3)) }));
            Assert.That(world.SetDrafted(1, true), Is.True);
            Assert.That(world.PlaceBlueprint(BuildingKind.Wall, site), Is.True);
            Until(world, () => world.Buildings.Count == 1, 180);
            var wall = world.Buildings[0];
            world.ApplyCombatDamage(wall.Id, 100);
            Assert.That(world.BlockedTiles.Contains(site), Is.False);
            Assert.That(world.QueueMove(1, site), Is.True);
            Until(world, () => world.Colonists[0].Position == site, 25);
            Assert.That(world.SetRepair(wall.Id, true), Is.True);
            Until(world, () => world.GetRepairDiagnostic(wall.Id) == WorkReasonCode.AwaitingClearance, 150);
            Assert.That(wall.Durability, Is.Zero);
            Assert.That(world.RepairWoodConsumed, Is.Zero);
            Assert.That(wall.RepairWood, Is.EqualTo(2));
            Assert.That(world.QueueMove(1, new GridPosition(5, 4)), Is.True);
            Until(world, () => wall.Durability > 0, 15);
            Assert.That(world.BlockedTiles.Contains(site), Is.True);
            Assert.That(world.RepairWoodConsumed, Is.EqualTo(2));
        }
    }
}
