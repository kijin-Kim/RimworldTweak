using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class LivingTests
    {
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private static ScenarioDefinition Scenario(ColonistSeed[] people = null, ResourceSeed[] resources = null,
            BedSeed[] beds = null, GridPosition[] blocked = null)
        {
            return new ScenarioDefinition(8, 8, blocked ?? Array.Empty<GridPosition>(),
                people ?? new[] { new ColonistSeed(1, P(1, 1), 3010, 10000) },
                resources ?? new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(1, 1)) }, beds, true);
        }
        private static WorldState World(ColonistSeed[] people = null, ResourceSeed[] resources = null,
            BedSeed[] beds = null, GridPosition[] blocked = null) => WorldState.Create(Scenario(people, resources, beds, blocked));
        private static void Conserve(ResourceState r, int initial)
        {
            Assert.That(r.Amount, Is.EqualTo(initial + r.AddedAmount - r.ConsumedAmount - r.RemovedAmount));
            Assert.That(r.Amount, Is.GreaterThanOrEqualTo(0));
            Assert.That(r.ReservedAmount, Is.InRange(0, r.Amount));
            Assert.That(r.AvailableAmount, Is.EqualTo(r.Amount - r.ReservedAmount));
            Assert.That(r.Reservations.Values.Sum(), Is.EqualTo(r.ReservedAmount));
            Assert.That(r.Reservations.Count, Is.LessThanOrEqualTo(1));
            Assert.That(r.AccessibleAmount, Is.EqualTo(r.Exists && !r.OwnerId.HasValue ? r.Amount : 0));
        }
        private static void Until(WorldState world, Func<bool> condition, int ticks = 100)
        {
            for (var i = 0; i < ticks && !condition(); i++) world.Step(1);
            Assert.That(condition(), Is.True, "Expected state not reached within bounded ticks");
        }

        [Test]
        public void InitialNeedsBedsAndValidationAreIndependentAndReadOnly()
        {
            var scenario = LivingScenario.Create(); var a = WorldState.Create(scenario); var b = WorldState.Create(scenario);
            Assert.That(a.NeedsEnabled, Is.True); Assert.That(a.Beds.Count, Is.EqualTo(2));
            Assert.That(a.Colonists.Select(c => c.Nutrition), Is.EqualTo(new[] { 3500, 3500, 3500 }));
            Assert.That(a.Colonists.Select(c => c.Rest), Is.EqualTo(new[] { 2500, 2500, 2500 }));
            Assert.That(a.Resources[0], Is.Not.SameAs(b.Resources[0])); Assert.That(a.Beds[0], Is.Not.SameAs(b.Beds[0]));
            a.Step(100);
            Assert.That(b.Colonists[0].Nutrition, Is.EqualTo(3500)); Assert.That(scenario.Colonists[0].Nutrition, Is.EqualTo(3500));
            Assert.That(b.Beds.All(x => x.OwnerId == null), Is.True); Assert.That(b.Resources.All(x => x.ReservedAmount == 0), Is.True);
            foreach (var type in new[] { typeof(ResourceState), typeof(BedState) })
                foreach (var property in type.GetProperties()) Assert.That(property.GetSetMethod(false), Is.Null, type.Name + "." + property.Name);
            if (a.Beds is IList<BedState> list) Assert.Throws<NotSupportedException>(() => list.Clear());
            if (a.Resources[0].Reservations is IDictionary<int, int> reservations)
                Assert.Throws<NotSupportedException>(() => reservations.Add(999, 1));
            foreach (var invalid in new[] { -1, 10001 })
            {
                var error = Assert.Throws<ScenarioValidationException>(() => WorldState.Create(Scenario(new[] { new ColonistSeed(1, P(1, 1), invalid, 9000) })));
                Assert.That(error.Code, Is.EqualTo("InvalidNeeds"));
                Assert.Throws<ScenarioValidationException>(() => WorldState.Create(Scenario(new[] { new ColonistSeed(1, P(1, 1), 9000, invalid) })));
            }
            Assert.Throws<ScenarioValidationException>(() => WorldState.Create(Scenario(beds: new[] { new BedSeed(1, P(2, 2)) })));
            Assert.Throws<ScenarioValidationException>(() => WorldState.Create(Scenario(beds: new[] { new BedSeed(201, P(8, 2)) })));
            Assert.Throws<ScenarioValidationException>(() => WorldState.Create(Scenario(beds: new[] { new BedSeed(201, P(3, 3)) }, blocked: new[] { P(3, 3) })));
        }

        [Test]
        public void NeedBoundaryUsesFixedPointAndNeverFallsBelowZero()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 3010, 2005) }, Array.Empty<ResourceSeed>());
            w.Step(1); Assert.That(w.Colonists[0].Nutrition, Is.EqualTo(3000)); Assert.That(w.Colonists[0].Rest, Is.EqualTo(2000));
            Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor));
            w.Step(1000); Assert.That(w.Colonists[0].Nutrition, Is.Zero); Assert.That(w.Colonists[0].Rest, Is.InRange(0, 10000));
            Assert.That(w.Colonists[0].StarvationTicks, Is.GreaterThan(0));
        }

        [Test]
        public void MealConsumesOnlyOnTwentiethActionTickAndRestoresSixty()
        {
            var w = World(); var c = w.Colonists[0]; var r = w.Resources[0];
            w.Step(1); Assert.That(c.LifeActivity, Is.EqualTo(LifeActivity.Eating)); Assert.That(c.Nutrition, Is.EqualTo(3000));
            Assert.That(c.ActionProgress, Is.Zero); Assert.That(c.ActionDuration, Is.EqualTo(20));
            Assert.That(r.Amount, Is.EqualTo(1)); Assert.That(r.ReservedAmount, Is.EqualTo(1)); Assert.That(r.AvailableAmount, Is.Zero);
            w.Step(19); Assert.That(r.Amount, Is.EqualTo(1)); Assert.That(r.ConsumedAmount, Is.Zero); Assert.That(c.Nutrition, Is.EqualTo(2810));
            w.Step(1); Assert.That(r.Amount, Is.Zero); Assert.That(r.ReservedAmount, Is.Zero); Assert.That(c.Nutrition, Is.EqualTo(8800));
            Assert.That(c.MealsEaten, Is.EqualTo(1)); w.Step(10); Assert.That(c.MealsEaten, Is.EqualTo(1)); Conserve(r, 1);
        }

        [Test]
        public void FoodArrivalPrecedesEatingAndConsumptionAtTickThirty()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 3000, 9000) }, new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(3, 1)) });
            w.Step(9); Assert.That(w.Resources[0].ConsumedAmount, Is.Zero); Assert.That(w.Colonists[0].Position, Is.Not.EqualTo(P(3, 1)));
            w.Step(1); Assert.That(w.Colonists[0].Position, Is.EqualTo(P(3, 1))); Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.Eating));
            Assert.That(w.Colonists[0].ActionProgress, Is.Zero); w.Step(19); Assert.That(w.Resources[0].Amount, Is.EqualTo(1));
            w.Step(1); Assert.That(w.Resources[0].Amount, Is.Zero); Assert.That(w.Colonists[0].Nutrition, Is.EqualTo(8700));
        }

        [Test]
        public void LastMealReservationUsesHungerThenIdWithoutDoubleConsumption()
        {
            foreach (var hungrierSecond in new[] { false, true })
            {
                var w = World(new[] { new ColonistSeed(1, P(1, 1), 3010, 9000), new ColonistSeed(2, P(1, 1), hungrierSecond ? 2010 : 3010, 9000) });
                w.Step(1); var winner = hungrierSecond ? 2 : 1;
                Assert.That(w.Resources[0].Reservations.Keys, Is.EquivalentTo(new[] { winner }));
                Assert.That(w.Resources[0].ReservedAmount, Is.EqualTo(1)); w.Step(20);
                Assert.That(w.Colonists.Single(c => c.Id == winner).MealsEaten, Is.EqualTo(1));
                Assert.That(w.Colonists.Single(c => c.Id != winner).MealsEaten, Is.Zero); Conserve(w.Resources[0], 1);
            }
        }

        [Test]
        public void RawFoodRequiresFourAndFortyActionTicks()
        {
            var w = World(resources: new[] { new ResourceSeed(101, ResourceKind.Ingredients, 4, P(1, 1)) });
            w.Step(1); Assert.That(w.Resources[0].ReservedAmount, Is.EqualTo(4)); Assert.That(w.Colonists[0].ActionDuration, Is.EqualTo(40));
            w.Step(39); Assert.That(w.Resources[0].Amount, Is.EqualTo(4)); w.Step(1);
            Assert.That(w.Resources[0].Amount, Is.Zero); Assert.That(w.Colonists[0].Nutrition, Is.EqualTo(8600));
            Assert.That(w.Colonists[0].RawMealsEaten, Is.EqualTo(1)); Conserve(w.Resources[0], 4);
            var insufficient = World(resources: new[] { new ResourceSeed(101, ResourceKind.Ingredients, 3, P(1, 1)) });
            insufficient.Step(50); Assert.That(insufficient.Resources[0].Amount, Is.EqualTo(3)); Assert.That(insufficient.Resources[0].ReservedAmount, Is.Zero);
            Assert.That(insufficient.Colonists[0].RawMealsEaten, Is.Zero);
        }

        [Test]
        public void MealPreferredOverCloserRawFoodAndEqualMealsTieById()
        {
            var w = World(resources: new[] { new ResourceSeed(103, ResourceKind.Ingredients, 4, P(1, 1)), new ResourceSeed(102, ResourceKind.Meal, 1, P(2, 1)), new ResourceSeed(101, ResourceKind.Meal, 1, P(1, 2)) });
            w.Step(1); Assert.That(w.Colonists[0].LifeTargetId, Is.EqualTo(101));
            Assert.That(w.Resources.Single(r => r.Id == 103).ReservedAmount, Is.Zero);
        }

        [Test]
        public void CancelledOrDeletedFoodReleasesReservationWithoutConsumption()
        {
            var w = World(); w.Step(10); Assert.That(w.Resources[0].ReservedAmount, Is.EqualTo(1));
            Assert.That(w.QueueCancelLife(1), Is.True);
            Assert.That(w.Resources[0].Amount, Is.EqualTo(1)); Assert.That(w.Resources[0].ReservedAmount, Is.Zero); Assert.That(w.Colonists[0].ActionProgress, Is.Zero);
            Assert.That(w.Colonists[0].MealsEaten, Is.Zero); w.Step(1); Assert.That(w.Resources[0].ReservedAmount, Is.EqualTo(1));
            Assert.That(w.QueueRemoveResource(101), Is.True); w.Step(1);
            Assert.That(w.Resources[0].Exists, Is.False); Assert.That(w.Resources[0].ReservedAmount, Is.Zero);
            Assert.That(w.Resources[0].ConsumedAmount, Is.Zero); Assert.That(w.Colonists[0].MealsEaten, Is.Zero); Conserve(w.Resources[0], 1);
            Assert.That(w.QueueSetResourceAmount(101, 1), Is.True); Until(w, () => w.Colonists[0].MealsEaten == 1, 40); Conserve(w.Resources[0], 1);
        }

        [Test]
        public void StockReductionInvalidatesExcessReservationsAndConservesAmounts()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 3010, 9000), new ColonistSeed(2, P(1, 1), 3010, 9000) }, new[] { new ResourceSeed(101, ResourceKind.Meal, 2, P(1, 1)) });
            w.Step(1); Assert.That(w.Resources[0].ReservedAmount, Is.EqualTo(1));
            w.QueueSetResourceAmount(101, 1); w.Step(1); Conserve(w.Resources[0], 2);
            Assert.That(w.Resources[0].ReservedAmount, Is.LessThanOrEqualTo(1)); w.Step(30);
            Assert.That(w.Colonists.Sum(c => c.MealsEaten), Is.EqualTo(1)); Conserve(w.Resources[0], 2);
            Assert.That(w.QueueSetResourceAmount(101, -1), Is.False); Assert.That(w.QueueRemoveResource(999), Is.False);
        }

        [Test]
        public void BlockedFoodPathReleasesReservationAndReopeningRecovers()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 3000, 9000) }, new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(5, 1)) });
            w.Step(1); Assert.That(w.Resources[0].ReservedAmount, Is.EqualTo(1));
            for (var y = 0; y < 8; y++) w.QueueSetBlocked(P(3, y), true);
            w.Step(1); Assert.That(w.Resources[0].ReservedAmount, Is.Zero); Assert.That(w.Colonists[0].MealsEaten, Is.Zero);
            var searches = w.PathSearchCount; w.Step(5); Assert.That(w.PathSearchCount - searches, Is.LessThan(5), "Do not search every tick while blocked");
            w.QueueSetBlocked(P(3, 1), false); Until(w, () => w.Resources[0].ReservedAmount == 1, 10);
            Until(w, () => w.Colonists[0].MealsEaten == 1, 60); Conserve(w.Resources[0], 1);
        }

        [Test]
        public void BedAndFloorReachNinetyAfterDifferentRecoveryDurations()
        {
            foreach (var bed in new[] { true, false })
            {
                var w = World(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>(), bed ? new[] { new BedSeed(201, P(1, 1)) } : null);
                w.Step(1); Assert.That(w.Colonists[0].Rest, Is.EqualTo(2000));
                Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(bed ? LifeActivity.SleepingInBed : LifeActivity.SleepingOnFloor));
                var duration = bed ? 140 : 280; w.Step(duration - 1);
                Assert.That(w.Colonists[0].Rest, Is.EqualTo(bed ? 8950 : 8975)); w.Step(1);
                Assert.That(w.Colonists[0].Rest, Is.EqualTo(9000)); Assert.That(w.Colonists[0].SleepsCompleted, Is.EqualTo(1));
                Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.Idle));
                if (bed) { Assert.That(w.Beds[0].OwnerId, Is.Null); Assert.That(w.Beds[0].IsOccupied, Is.False); }
            }
        }

        [Test]
        public void OneBedHasOneOwnerAndFloorSleeperTakesReleasedBed()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005), new ColonistSeed(2, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(1, 1)) });
            w.Step(1); Assert.That(w.Beds[0].OwnerId, Is.EqualTo(1)); Assert.That(w.Beds[0].IsOccupied, Is.True);
            Assert.That(w.Colonists[1].LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor));
            w.Step(140); Assert.That(w.Beds[0].OwnerId, Is.EqualTo(2)); Assert.That(w.Colonists[1].LifeActivity, Is.EqualTo(LifeActivity.SleepingInBed));
            w.Step(140); Assert.That(w.Colonists[1].SleepsCompleted, Is.EqualTo(1));
        }

        [Test]
        public void DeletedOrUnreachableBedFallsBackWithoutStaleOwner()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(1, 1)) });
            w.Step(2); Assert.That(w.Colonists[0].Rest, Is.EqualTo(2050));
            w.QueueSetBedExists(201, false); w.Step(1);
            Assert.That(w.Beds[0].Exists, Is.False); Assert.That(w.Beds[0].OwnerId, Is.Null); Assert.That(w.Beds[0].IsOccupied, Is.False);
            Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor));
            var restAfterDeletion = w.Colonists[0].Rest; Assert.That(restAfterDeletion, Is.EqualTo(2045));
            w.QueueSetBedExists(201, true); w.Step(1); Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.SleepingInBed));
            Assert.That(w.Colonists[0].Rest, Is.EqualTo(restAfterDeletion + 25));
            var blocked = World(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(5, 1)) }, Enumerable.Range(0, 8).Select(y => P(3, y)).ToArray());
            blocked.Step(1); Assert.That(blocked.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor)); Assert.That(blocked.Beds[0].OwnerId, Is.Null);
        }

        [Test]
        public void BothNeedsPreferFoodThenSleepAndNoFoodDoesNotOscillate()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 3010, 2005) });
            w.Step(1); Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.Eating));
            w.Step(20); Assert.That(w.Colonists[0].MealsEaten, Is.EqualTo(1)); Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor));
            var noFood = World(new[] { new ColonistSeed(1, P(1, 1), 3010, 2005) });
            noFood.QueueSetResourceAmount(101, 0); noFood.Step(1);
            for (var i = 0; i < 20; i++) { noFood.Step(1); Assert.That(noFood.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor)); }
            Assert.That(noFood.Colonists[0].Rest, Is.EqualTo(2500));
            noFood.QueueSetResourceAmount(101, 1); Until(noFood, () => noFood.Colonists[0].LifeActivity == LifeActivity.Eating, 10);
            Assert.That(noFood.Resources[0].ReservedAmount, Is.EqualTo(1));
        }

        [Test]
        public void LifeInterruptsManualOrderRejectsManualCommandsAndNeverResumesOldTarget()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 3030, 9000) });
            Assert.That(w.QueueMove(1, P(7, 7)), Is.False); w.Step(3);
            Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.Eating)); Assert.That(w.Colonists[0].HasPendingCommand, Is.False);
            Assert.That(w.QueueMove(1, P(7, 7)), Is.False); Assert.That(w.LastCommandError, Is.EqualTo("LifeBusy"));
            w.Step(20); Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.Idle));
            Assert.That(w.Colonists[0].Destination, Is.Null); w.Step(20); Assert.That(w.Colonists[0].Position, Is.EqualTo(P(1, 1)));
            var noFood = World(resources: Array.Empty<ResourceSeed>()); noFood.Step(1);
            Assert.That(noFood.QueueMove(1, P(2, 1)), Is.False);
            Assert.That(noFood.LastCommandError, Is.EqualTo("NotDrafted"));
        }

        [Test]
        public void StarvationSlowsOnlyTheNextTileAndDoesNotStopMovement()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 30, 9000) }, Array.Empty<ResourceSeed>());
            w.SetDrafted(1,true); w.QueueMove(1, P(3, 1)); w.Step(4); Assert.That(w.Colonists[0].Position, Is.EqualTo(P(1, 1)));
            w.Step(1); Assert.That(w.Colonists[0].Position, Is.EqualTo(P(2, 1)));
            w.Step(9); Assert.That(w.Colonists[0].Position, Is.EqualTo(P(2, 1))); w.Step(1);
            Assert.That(w.Colonists[0].Position, Is.EqualTo(P(3, 1))); Assert.That(w.Colonists[0].Nutrition, Is.Zero);
            var hungry = World(new[] { new ColonistSeed(1, P(1, 1), 0, 9000) }, Array.Empty<ResourceSeed>());
            hungry.SetDrafted(1,true); hungry.QueueMove(1, P(2, 1)); hungry.Step(9); Assert.That(hungry.Colonists[0].Position, Is.EqualTo(P(1, 1)));
            hungry.Step(1); Assert.That(hungry.Colonists[0].Position, Is.EqualTo(P(2, 1)));
        }

        [Test]
        public void TwoDayAutonomyHasNoStarvationDoubleOwnershipOrLostResources()
        {
            var w = WorldState.Create(LivingScenario.Create());
            for (var tick = 0; tick < 2400; tick++)
            {
                w.Step(1);
                foreach (var resource in w.Resources) Conserve(resource, resource.Kind == ResourceKind.Wood ? 40 : 18);
                Assert.That(w.Colonists.All(c => c.Nutrition > 0), Is.True, "Starvation at tick " + w.Tick);
                foreach (var bed in w.Beds.Where(b => b.OwnerId.HasValue))
                {
                    var owner = w.Colonists.Single(c => c.Id == bed.OwnerId);
                    Assert.That(owner.LifeTargetId, Is.EqualTo(bed.Id));
                    Assert.That(owner.ActionId, Is.EqualTo(bed.OwnerActionId));
                    Assert.That(owner.LifeActivity, Is.EqualTo(bed.IsOccupied ? LifeActivity.SleepingInBed : LifeActivity.GoingToBed));
                }
            }
            Assert.That(w.Colonists.All(c => c.MealsEaten >= 1 && c.SleepsCompleted >= 1 && c.StarvationTicks == 0), Is.True);
            Assert.That(w.Resources.Single(r => r.Kind == ResourceKind.Meal).ConsumedAmount, Is.EqualTo(w.Colonists.Sum(c => c.MealsEaten)));
        }

        [Test]
        public void ResetFromEveryLifePhaseRestoresNeedsStockBedsAndReservations()
        {
            var fixtures = new[] {
                Scenario(resources: new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(5, 1)) }),
                Scenario(),
                Scenario(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(1, 1)) }),
                Scenario(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>()),
                Scenario(resources: Array.Empty<ResourceSeed>())
            };
            var expectedPhases = new[] { LifeActivity.GoingToFood, LifeActivity.Eating, LifeActivity.SleepingInBed, LifeActivity.SleepingOnFloor, LifeActivity.Idle };
            var fixtureIndex = 0;
            foreach (var fixture in fixtures)
            {
                var session = new SimulationSession(fixture);
                for (var repeat = 0; repeat < 10; repeat++)
                {
                    session.AdvanceTime(0.15m); var old = session.World; var oldTick = old.Tick; var oldNutrition = old.Colonists[0].Nutrition;
                    Assert.That(old.Colonists[0].LifeActivity, Is.EqualTo(expectedPhases[fixtureIndex]));
                    session.Reset(); var fresh = session.World;
                    Assert.That(fresh, Is.Not.SameAs(old)); Assert.That(fresh.Tick, Is.Zero); Assert.That(session.PendingSeconds, Is.Zero);
                    Assert.That(fresh.Colonists[0].Nutrition, Is.EqualTo(fixture.Colonists[0].Nutrition)); Assert.That(fresh.Colonists[0].Rest, Is.EqualTo(fixture.Colonists[0].Rest));
                    Assert.That(fresh.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.Idle)); Assert.That(fresh.Colonists[0].LifeTargetId, Is.Null);
                    Assert.That(fresh.Colonists[0].HasSleepIntent, Is.False); Assert.That(fresh.Colonists[0].ActionId, Is.Zero);
                    Assert.That(fresh.Resources.All(r => r.ReservedAmount == 0), Is.True); Assert.That(fresh.Beds.All(b => b.OwnerId == null && !b.IsOccupied), Is.True);
                    Assert.That(fresh.Resources.Select(r => r.Amount), Is.EqualTo(fixture.Resources.Select(r => r.Amount))); Assert.That(fresh.PendingCommandCount, Is.Zero);
                    Assert.That(old.Tick, Is.EqualTo(oldTick)); Assert.That(old.Colonists[0].Nutrition, Is.EqualTo(oldNutrition));
                }
                fixtureIndex++;
            }
        }
        [Test]
        public void FoodChoiceUsesReachablePathLengthAndDetourDelaysConsumption()
        {
            var wall = Enumerable.Range(0, 4).Select(y => P(2, y)).ToArray();
            var w = World(resources: new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(3, 1)), new ResourceSeed(102, ResourceKind.Meal, 1, P(1, 4)) }, blocked: wall);
            w.Step(1); Assert.That(w.Colonists[0].LifeTargetId, Is.EqualTo(102), "Three-step route beats the geometrically closer target behind the wall");
            var detour = World(resources: new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(3, 1)) }, blocked: wall);
            Assert.That(detour.FindPath(P(1, 1), P(3, 1)).Count, Is.EqualTo(8));
            detour.Step(39); Assert.That(detour.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.GoingToFood));
            Assert.That(detour.Resources[0].ConsumedAmount, Is.Zero);
            detour.Step(1); Assert.That(detour.Colonists[0].Position, Is.EqualTo(P(3, 1)));
            Assert.That(detour.Colonists[0].ActionProgress, Is.Zero);
            detour.Step(19); Assert.That(detour.Resources[0].Amount, Is.EqualTo(1));
            detour.Step(1); Assert.That(detour.Resources[0].ConsumedAmount, Is.EqualTo(1));
        }

        [Test]
        public void ReservedOrUnreachableMealAllowsRawFoodFallback()
        {
            var competing = World(new[] { new ColonistSeed(1, P(1, 1), 3010, 9000), new ColonistSeed(2, P(1, 1), 3000, 9000) },
                new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(1, 1)), new ResourceSeed(102, ResourceKind.Ingredients, 4, P(1, 1)) });
            competing.Step(1);
            Assert.That(competing.Resources[0].Reservations.Keys, Is.EqualTo(new[] { 2 }));
            Assert.That(competing.Resources[1].Reservations.Keys, Is.EqualTo(new[] { 1 }));
            Assert.That(competing.Resources[1].ReservedAmount, Is.EqualTo(4));
            var blocked = World(resources: new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(5, 1)), new ResourceSeed(102, ResourceKind.Ingredients, 4, P(1, 1)) },
                blocked: Enumerable.Range(0, 8).Select(y => P(3, y)).ToArray());
            blocked.Step(1); Assert.That(blocked.Colonists[0].LifeTargetId, Is.EqualTo(102));
            Assert.That(blocked.Resources[0].ReservedAmount, Is.Zero); Assert.That(blocked.Resources[1].ReservedAmount, Is.EqualTo(4));
        }

        [Test]
        public void LowerRestWinsBedAndReservationBecomesOccupancyOnlyOnArrival()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005), new ColonistSeed(2, P(1, 1), 9000, 1000) },
                Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(3, 1)) });
            w.Step(1); Assert.That(w.Beds[0].OwnerId, Is.EqualTo(2)); Assert.That(w.Beds[0].IsOccupied, Is.False);
            Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor));
            Assert.That(w.Colonists[1].LifeActivity, Is.EqualTo(LifeActivity.GoingToBed));
            w.Step(8); Assert.That(w.Beds[0].OwnerId, Is.EqualTo(2)); Assert.That(w.Beds[0].IsOccupied, Is.False);
            w.Step(1); Assert.That(w.Beds[0].IsOccupied, Is.True); Assert.That(w.Colonists[1].Position, Is.EqualTo(P(3, 1)));
        }

        [Test]
        public void HungerDuringBedSleepReservesFoodBeforeReleasingBed()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 3030, 2005) },
                new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(3, 1)) }, new[] { new BedSeed(201, P(1, 1)) });
            w.Step(2); Assert.That(w.Beds[0].IsOccupied, Is.True); Assert.That(w.Resources[0].ReservedAmount, Is.Zero);
            w.Step(1); Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.GoingToFood));
            Assert.That(w.Resources[0].Reservations.Keys, Is.EqualTo(new[] { 1 }));
            Assert.That(w.Beds[0].OwnerId, Is.Null); Assert.That(w.Beds[0].IsOccupied, Is.False);
        }

        [Test]
        public void ReachableDetourKeepsFoodReservationAndPendingManualOrderIsCancelled()
        {
            var w = World(resources: new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(5, 1)) });
            w.QueueMove(1, P(7, 7)); w.Step(1);
            Assert.That(w.Colonists[0].LifeTargetId, Is.EqualTo(101)); Assert.That(w.PendingCommandCount, Is.Zero);
            Assert.That(w.Colonists[0].HasPendingCommand, Is.False);
            w.QueueSetBlocked(P(3, 1), true); w.Step(1);
            Assert.That(w.Resources[0].Reservations.Keys, Is.EqualTo(new[] { 1 }));
            Assert.That(w.Colonists[0].LifeTargetId, Is.EqualTo(101));
            Assert.That(w.Colonists[0].Path, Has.No.Member(P(3, 1)));
            Until(w, () => w.Colonists[0].MealsEaten == 1, 80);
            Assert.That(w.Colonists[0].Destination, Is.Null);
        }

        [Test]
        public void DraftImmediatelyCancelsEveryLifePhaseAndKeepsPlayerControl()
        {
            var cases = new[] {
                Scenario(resources: new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(5, 1)) }),
                Scenario(),
                Scenario(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(5, 1)) }),
                Scenario(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(1, 1)) }),
                Scenario(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>())
            };
            var phases = new[] { LifeActivity.GoingToFood, LifeActivity.Eating, LifeActivity.GoingToBed, LifeActivity.SleepingInBed, LifeActivity.SleepingOnFloor };
            for (var index = 0; index < cases.Length; index++)
            {
                var w = WorldState.Create(cases[index]); w.Step(1); var c = w.Colonists[0];
                Assert.That(c.LifeActivity, Is.EqualTo(phases[index]));
                var tick = w.Tick; var nutrition = c.Nutrition; var rest = c.Rest;
                Assert.That(w.QueueStop(1), Is.True);
                Assert.That(w.Tick, Is.EqualTo(tick)); Assert.That(c.Nutrition, Is.EqualTo(nutrition)); Assert.That(c.Rest, Is.EqualTo(rest));
                Assert.That(c.IsDrafted, Is.True); Assert.That(c.LifeActivity, Is.EqualTo(LifeActivity.Idle));
                Assert.That(c.ActionProgress, Is.Zero); Assert.That(c.Destination, Is.Null);
                Assert.That(w.Resources.All(r => r.ReservedAmount == 0 && r.ConsumedAmount == 0), Is.True);
                Assert.That(w.Beds.All(b => b.OwnerId == null && !b.IsOccupied), Is.True);
                w.Step(20); Assert.That(c.IsDrafted, Is.True); Assert.That(c.LifeActivity, Is.EqualTo(LifeActivity.Idle));
                Assert.That(c.Rest, Is.EqualTo(rest - 100));
                Assert.That(w.QueueMove(1, P(2, 1)), Is.True); w.Step(5);
                Assert.That(c.Position, Is.EqualTo(P(2, 1)));
                w.QueueMove(1, P(7, 7)); w.QueueStop(1);
                Assert.That(w.PendingCommandCount, Is.Zero); w.Step(10);
                Assert.That(c.Position, Is.EqualTo(P(2, 1))); Assert.That(c.IsDrafted, Is.True);
                Assert.That(w.SetDrafted(1, false), Is.True); w.Step(1);
                Assert.That(c.IsDrafted, Is.False); Assert.That(c.LifeActivity, Is.Not.EqualTo(LifeActivity.Idle));
            }
        }

        [Test]
        public void DraftBeforeMealCompletionPreventsConsumptionAndResetClearsDraft()
        {
            var session = new SimulationSession(Scenario()); var w = session.World;
            w.Step(20); Assert.That(w.Colonists[0].ActionProgress, Is.EqualTo(19));
            w.QueueStop(1); w.Step(1);
            Assert.That(w.Resources[0].Amount, Is.EqualTo(1)); Assert.That(w.Resources[0].ConsumedAmount, Is.Zero);
            Assert.That(w.Colonists[0].MealsEaten, Is.Zero);
            w.SetDrafted(1, false); w.Step(21);
            Assert.That(w.Resources[0].ConsumedAmount, Is.EqualTo(1));
            w.QueueStop(1); w.QueueStop(1); Assert.That(w.Resources[0].ConsumedAmount, Is.EqualTo(1));
            session.Reset(); Assert.That(session.World.Colonists[0].IsDrafted, Is.False);
            Assert.That(session.World.Resources[0].Amount, Is.EqualTo(1));
            Assert.That(session.World.SetDrafted(99, true), Is.False);
            Assert.That(session.World.LastCommandError, Is.EqualTo("InvalidColonist"));
            var starving = World(new[] { new ColonistSeed(1, P(1, 1), 0, 0) });
            starving.QueueStop(1); starving.QueueMove(1, P(2, 1)); starving.Step(10);
            Assert.That(starving.Colonists[0].Position, Is.EqualTo(P(2, 1)));
            Assert.That(starving.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.Idle));
            Assert.That(starving.Resources[0].ReservedAmount, Is.Zero);
        }

        [Test]
        public void FoodStackIsExclusiveEvenWithSpareQuantityAndSeparateStacksStayIndependent()
        {
            var people = new[] { new ColonistSeed(1, P(1, 1), 3010, 9000), new ColonistSeed(2, P(1, 1), 3010, 9000) };
            var w = World(people, new[] { new ResourceSeed(101, ResourceKind.Meal, 18, P(3, 1)) });
            w.Step(1); var r = w.Resources[0];
            Assert.That(r.OwnerId, Is.EqualTo(1)); Assert.That(r.OwnerActionId, Is.EqualTo(w.Colonists[0].ActionId));
            Assert.That(r.Reservations.Count, Is.EqualTo(1)); Assert.That(r.ReservedAmount, Is.EqualTo(1));
            Assert.That(r.AvailableAmount, Is.EqualTo(17)); Assert.That(r.AccessibleAmount, Is.Zero);
            Assert.That(r.IsInUse, Is.False); Assert.That(w.Colonists[1].FoodReason, Is.EqualTo("FoodReserved"));
            w.ToggleDraft(1); Assert.That(r.OwnerId, Is.Null); Assert.That(r.Amount, Is.EqualTo(18));
            w.Step(1); Assert.That(r.OwnerId, Is.EqualTo(2));
            Until(w, () => r.ConsumedAmount == 1, 40); Assert.That(r.Amount, Is.EqualTo(17));
            var separate = World(people, new[] { new ResourceSeed(101, ResourceKind.Meal, 18, P(3, 1)), new ResourceSeed(102, ResourceKind.Meal, 18, P(3, 2)) });
            separate.Step(1); Assert.That(separate.Resources.Select(x => x.OwnerId), Is.EqualTo(new int?[] { 1, 2 }));
        }

        [Test]
        public void DraftedBedApproacherReleasesToFloorSleeperAboveStartThreshold()
        {
            var w = World(new[] { new ColonistSeed(1, P(0, 0), 9000, 2005), new ColonistSeed(2, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(7, 7)) });
            w.Step(61); var a = w.Colonists[0]; var b = w.Colonists[1]; var bed = w.Beds[0];
            Assert.That(a.LifeActivity, Is.EqualTo(LifeActivity.GoingToBed)); Assert.That(b.Rest, Is.EqualTo(3500));
            Assert.That(b.LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor));
            var tick = w.Tick; w.ToggleDraft(1);
            Assert.That(w.Tick, Is.EqualTo(tick)); Assert.That(bed.OwnerId, Is.Null); Assert.That(a.HasSleepIntent, Is.False);
            Assert.That(a.Path.Count, Is.Zero); Assert.That(a.HasPendingCommand, Is.False);
            w.Step(1); Assert.That(bed.OwnerId, Is.EqualTo(2)); Assert.That(bed.IsOccupied, Is.False);
            Assert.That(b.LifeActivity, Is.EqualTo(LifeActivity.GoingToBed)); Assert.That(b.Rest, Is.EqualTo(3525));
            w.Step(9); Assert.That(b.Rest, Is.EqualTo(3480)); Assert.That(b.SleepsCompleted, Is.Zero);
            Until(w, () => b.SleepsCompleted == 1, 230);
            Assert.That(b.HasSleepIntent, Is.False); Assert.That(bed.OwnerId, Is.Null);
            Assert.That(a.IsDrafted, Is.True);
        }

        [Test]
        public void FloorCompetitionRetriesBlockedBedAndExcludesDraftedOrFinishedSleepers()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005), new ColonistSeed(2, P(1, 1), 9000, 1005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(5, 1)) }, Enumerable.Range(0, 8).Select(y => P(3, y)).ToArray());
            w.Step(1); var searches = w.PathSearchCount; w.Step(5);
            Assert.That(w.PathSearchCount - searches, Is.LessThan(5)); Assert.That(w.Beds[0].OwnerId, Is.Null);
            Assert.That(w.Colonists.All(c => c.LifeActivity == LifeActivity.SleepingOnFloor), Is.True);
            w.QueueSetBlocked(P(3, 1), false); w.Step(1);
            Assert.That(w.Beds[0].OwnerId, Is.EqualTo(2)); Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor));
            w.ToggleDraft(2); w.ToggleDraft(1); w.Step(1); Assert.That(w.Beds[0].OwnerId, Is.Null);
            var done = World(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(1, 1)) });
            done.QueueSetBedExists(201, false); done.Step(281);
            Assert.That(done.Colonists[0].SleepsCompleted, Is.EqualTo(1));
            done.QueueSetBedExists(201, true); done.Step(1);
            Assert.That(done.Beds[0].OwnerId, Is.Null); Assert.That(done.Colonists[0].HasSleepIntent, Is.False);
        }

        [Test]
        public void StaleActionCannotCancelReleaseOrConsumeANewerOwnership()
        {
            var w = World(resources: new[] { new ResourceSeed(101, ResourceKind.Meal, 18, P(1, 1)) });
            w.Step(1); var c = w.Colonists[0]; var r = w.Resources[0]; var old = c.ActionId;
            w.ToggleDraft(1); w.ToggleDraft(1); w.Step(1); var fresh = c.ActionId;
            Assert.That(fresh, Is.GreaterThan(old)); Assert.That(r.OwnerActionId, Is.EqualTo(fresh));
            w.QueueCancelLife(1, old); w.Step(1);
            Assert.That(c.ActionId, Is.EqualTo(fresh)); Assert.That(r.OwnerActionId, Is.EqualTo(fresh));
            // Exercise internal authority checks as if a delayed old action tried to finish.
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ResourceState).GetMethod("Release", flags).Invoke(r, new object[] { 1, old });
            var consumed = (bool)typeof(ResourceState).GetMethod("Consume", flags).Invoke(r, new object[] { 1, old, 1 });
            Assert.That(consumed, Is.False); Assert.That(r.Amount, Is.EqualTo(18)); Assert.That(r.OwnerActionId, Is.EqualTo(fresh));
            var beds = World(new[] { new ColonistSeed(1, P(1, 1), 9000, 2005), new ColonistSeed(2, P(1, 1), 9000, 2005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(1, 1)) });
            beds.Step(1); var oldBedAction = beds.Colonists[0].ActionId;
            beds.ToggleDraft(1); beds.Step(1); Assert.That(beds.Beds[0].OwnerId, Is.EqualTo(2));
            typeof(BedState).GetMethod("Release", flags).Invoke(beds.Beds[0], new object[] { 1, oldBedAction });
            Assert.That(beds.Beds[0].OwnerId, Is.EqualTo(2)); Assert.That(beds.Beds[0].IsOccupied, Is.True);
        }

        [Test]
        public void InterruptedSleepIntentSurvivesFoodAndBedRemovalUntilNinety()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 4000, 2005) }, new[] { new ResourceSeed(101, ResourceKind.Meal, 1, P(1, 1)) }, new[] { new BedSeed(201, P(1, 1)) });
            w.Step(100); var c = w.Colonists[0];
            Assert.That(c.LifeActivity, Is.EqualTo(LifeActivity.Eating)); Assert.That(c.HasSleepIntent, Is.True);
            Assert.That(c.Rest, Is.GreaterThan(2000)); Assert.That(w.Beds[0].OwnerId, Is.Null);
            w.Step(20); Assert.That(c.Rest, Is.GreaterThan(2000)); Assert.That(c.LifeActivity, Is.EqualTo(LifeActivity.SleepingInBed));
            w.Step(10); w.QueueSetBedExists(201, false); w.Step(1);
            Assert.That(c.LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor)); Assert.That(c.HasSleepIntent, Is.True);
            Assert.That(c.SleepsCompleted, Is.Zero);
            Until(w, () => c.SleepsCompleted == 1, 300); Assert.That(c.HasSleepIntent, Is.False);
        }

        [Test]
        public void ReleasedBedChoosesLowestRestThenIdAmongFloorSleepers()
        {
            foreach (var thirdHungrier in new[] { false, true })
            {
                var w = World(new[] { new ColonistSeed(1, P(1, 1), 9000, 1005), new ColonistSeed(2, P(1, 1), 9000, 2005), new ColonistSeed(3, P(1, 1), 9000, thirdHungrier ? 1505 : 2005) }, Array.Empty<ResourceSeed>(), new[] { new BedSeed(201, P(3, 1)) });
                w.Step(3); Assert.That(w.Beds[0].OwnerId, Is.EqualTo(1));
                Assert.That(w.Colonists.Skip(1).All(c => c.LifeActivity == LifeActivity.SleepingOnFloor), Is.True);
                w.ToggleDraft(1); w.Step(1);
                Assert.That(w.Beds[0].OwnerId, Is.EqualTo(thirdHungrier ? 3 : 2));
                Assert.That(w.Colonists.Single(c => c.Id == (thirdHungrier ? 2 : 3)).LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor));
            }
        }
    }
}
