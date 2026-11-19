using System;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class WeaponOrderTests
    {
        private static SimulationSession Settlement(int width = 20, GridPosition? wall = null)
        {
            var cells = wall.HasValue ? new[] { wall.Value } : Array.Empty<GridPosition>();
            return new SimulationSession(new ScenarioDefinition(width, 10, cells, new[] {
                new ColonistSeed(1,new GridPosition(5,4)), new ColonistSeed(2,new GridPosition(4,4)) },
                Array.Empty<ResourceSeed>(), startWithWeapons:true));
        }

        private static WeaponItemState Weapon(WorldState world, string id) => world.Weapons.Single(w => w.Definition.Id == id);

        private static SimulationSession Copy(SimulationSession session)
        {
            var capture = session.CaptureSaveData(true,1);
            Assert.That(capture.Success,Is.True,capture.ErrorCode);
            Assert.That(SimulationSession.TryRestoreSave(capture.Data,out var copy,out var error),Is.True,error);
            return copy;
        }

        [Test] public void PickupReservationSurvivesSaveAndSwapDropsExactlyOnePreviousWeapon()
        {
            var session=Settlement();var world=session.World;var rifle=Weapon(world,"rifle");var knife=Weapon(world,"knife");
            Assert.That(world.EquippedWeapon(1),Is.Null);
            Assert.That(world.OrderEquip(1,rifle.Id),Is.True);
            Assert.That(world.OrderEquip(2,rifle.Id),Is.False);
            Assert.That(world.LastCommandError,Is.EqualTo("WeaponReserved"));
            session.Step(4);Assert.That(rifle.HolderId,Is.Null);
            var restored=Copy(session);
            foreach(var trial in new[]{session,restored})
            {
                var w=trial.World;var gun=Weapon(w,"rifle");var blade=Weapon(w,"knife");
                Assert.That(gun.ReservedById,Is.EqualTo(1));
                trial.Step(1);
                Assert.That(w.EquippedWeapon(1),Is.SameAs(gun));
                Assert.That(w.OrderEquip(1,blade.Id),Is.True);
                Assert.That(w.CancelDirectOrder(1),Is.True);
                Assert.That(w.EquippedWeapon(1),Is.SameAs(gun));
                Assert.That(w.OrderEquip(2,blade.Id),Is.True);
                Assert.That(w.CancelDirectOrder(2),Is.True);
                Assert.That(w.OrderEquip(1,blade.Id),Is.True);
                trial.Step(10);
                var after=Copy(trial).World;
                Assert.That(after.Weapons.Count,Is.EqualTo(2));
                Assert.That(after.EquippedWeapon(1).Id,Is.EqualTo(knife.Id));
                Assert.That(Weapon(after,"rifle").HolderId,Is.Null);
                Assert.That(Weapon(after,"rifle").Position,Is.EqualTo(after.Colonists[0].Position));
            }
        }

        [Test] public void DowningAndNewObstacleReleasePickupAndUnarmedAttackExplainsRejection()
        {
            var session=Settlement();var w=session.World;var gun=Weapon(w,"rifle");
            Assert.That(w.OrderEquip(1,gun.Id),Is.True);
            w.QueueSetBlocked(gun.Position,true);session.Step(1);
            Assert.That(gun.ReservedById,Is.Null);Assert.That(w.EquippedWeapon(1),Is.Null);
            w.QueueSetBlocked(gun.Position,false);session.Step(1);
            w.SetDrafted(1,true);Assert.That(w.StartNormalRaid(),Is.True);
            Assert.That(w.OrderEquip(1,gun.Id),Is.True);
            Assert.That(w.OrderAttack(1,w.Raiders[0].Id),Is.False);Assert.That(w.LastCommandError,Is.EqualTo("WeaponRequired"));
            Assert.That(gun.ReservedById,Is.EqualTo(1));
            w.ApplyCombatDamage(1,100);Assert.That(gun.ReservedById,Is.Null);
            Assert.That(w.OrderEquip(1,gun.Id),Is.False);Assert.That(w.OrderEquip(2,gun.Id),Is.True);
        }

        [Test] public void RifleUsesRangeAimRecoveryDamageAndRestoresMidShot()
        {
            var session=Settlement();var w=session.World;w.SetDrafted(1,true);
            Assert.That(w.OrderEquip(1,Weapon(w,"rifle").Id),Is.True);session.Step(5);
            Assert.That(w.StartNormalRaid(),Is.True);
            var enemy=w.Raiders[0];Assert.That(w.OrderAttack(1,enemy.Id),Is.True);
            // From (6,4) the first raider appears at (19,4), then approaches by one cell.
            session.Step(1);Assert.That(w.Colonists[0].Attack.BlockedReason,Is.EqualTo("AttackOutOfRange"));
            while(w.Colonists[0].Attack.Preparation==0 && w.Tick<30)session.Step(1);
            Assert.That(enemy.Position,Is.EqualTo(new GridPosition(18,4)));
            Assert.That(w.Colonists[0].Attack.Preparation,Is.EqualTo(1));
            session.Step(17);Assert.That(w.Colonists[0].Attack.Preparation,Is.EqualTo(18));
            var copy=Copy(session);
            foreach(var trial in new[]{session,copy})
            {
                var state=trial.World;var target=state.Raiders[0];var pawn=state.Colonists[0];
                trial.Step(1);Assert.That(target.Health,Is.EqualTo(50));
                trial.Step(1);Assert.That(target.Health,Is.EqualTo(30));
                Assert.That(pawn.Attack.Cooldown,Is.EqualTo(30));
                Assert.That(Copy(trial).World.Colonists[0].Attack.Cooldown,Is.EqualTo(30));
                Assert.That(state.OrderEquip(1,Weapon(state,"knife").Id),Is.True);trial.Step(10);
                Assert.That(state.EquippedWeapon(1).Definition,Is.SameAs(PrototypeEquipment.Knife));
                Assert.That(pawn.Attack.TargetId,Is.Null);Assert.That(pawn.Attack.Preparation,Is.Zero);
            }
        }

        [Test] public void KnifeHitsOnlyAtContactAndWallBlocksRifle()
        {
            var session=Settlement(11,new GridPosition(8,4));var w=session.World;
            w.SetDrafted(1,true);w.OrderEquip(1,Weapon(w,"rifle").Id);session.Step(5);
            w.StartNormalRaid();var enemy=w.Raiders[0];w.OrderAttack(1,enemy.Id);session.Step(1);
            Assert.That(w.Colonists[0].Attack.BlockedReason,Is.EqualTo("AttackBlocked"));Assert.That(enemy.Health,Is.EqualTo(50));
            w.OrderEquip(1,Weapon(w,"knife").Id);session.Step(10);
            Assert.That(w.EquippedWeapon(1).Definition,Is.SameAs(PrototypeEquipment.Knife));
            w.OrderAttack(1,enemy.Id);
            var pawn=w.Colonists[0];
            enemy.Position=new GridPosition(pawn.Position.X+2,pawn.Position.Y);enemy.NextCell=null;enemy.MoveProgress=0;
            session.Step(1);Assert.That(enemy.Health,Is.EqualTo(50));Assert.That(pawn.Attack.BlockedReason,Is.EqualTo("AttackOutOfRange"));
            enemy.Position=new GridPosition(pawn.Position.X+1,pawn.Position.Y);enemy.NextCell=null;enemy.MoveProgress=0;
            session.Step(1);Assert.That(enemy.Health,Is.EqualTo(42));Assert.That(pawn.Attack.Cooldown,Is.EqualTo(10));
        }

        [Test] public void DuplicateItemOrMalformedOwnershipRejectsRestore()
        {
            var session=Settlement();
            foreach(var damage in new Action<EquipmentSaveData>[] {
                d=>d.Weapons[0].Id=d.Weapons[1].Id,
                d=>d.Weapons[0].Id=1,
                d=>d.Weapons[0].HolderId=1,
                d=>d.Weapons[0].ReservedById=1,
                d=>d.Weapons.RemoveAt(0)
            })
            {
                var data=session.CaptureSaveData().Data;damage(data.World.Equipment);
                Assert.That(SimulationSession.TryRestoreSave(data,out _,out _),Is.False);
            }
        }
    }
}
