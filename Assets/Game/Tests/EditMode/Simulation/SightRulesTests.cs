using System;
using System.Collections.Generic;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class SightRulesTests
    {
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private static SightCell Open(GridPosition _) => default;

        [Test] public void RangeIncludesExactEuclideanBoundaryAndRejectsBeyond()
        {
            Assert.That(SightRules.Evaluate(P(0,0),P(3,4),5,Open).CanAttack,Is.True);
            Assert.That(SightRules.Evaluate(P(0,0),P(0,5),5,Open).CanAttack,Is.True);
            Assert.That(SightRules.Evaluate(P(0,0),P(3,4),4,Open).Reason,Is.EqualTo(SightFailure.OutOfRange));
            Assert.That(SightRules.Evaluate(P(0,0),P(4,4),5,Open).Reason,Is.EqualTo(SightFailure.OutOfRange));
            Assert.That(SightRules.Evaluate(P(-4,-4),P(-1,0),5,Open).CanAttack,Is.True);
        }

        [Test] public void RejectedGeometryDoesNotQueryTheMap()
        {
            SightCell Fail(GridPosition _) => throw new InvalidOperationException("Map must not be queried");
            Assert.That(SightRules.Evaluate(P(1,1),P(1,1),5,Fail).Reason,Is.EqualTo(SightFailure.SamePosition));
            Assert.That(SightRules.Evaluate(P(0,0),P(9,9),5,Fail).Reason,Is.EqualTo(SightFailure.OutOfRange));
        }

        [Test] public void InvalidArgumentsAndCoverValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>SightRules.Evaluate(P(0,0),P(1,0),0,Open));
            Assert.Throws<ArgumentOutOfRangeException>(()=>SightRules.Evaluate(P(0,0),P(1,0),-1,Open));
            Assert.Throws<ArgumentNullException>(()=>SightRules.Evaluate(P(0,0),P(1,0),1,null));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new SightCell(false,-1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new SightCell(false,101));
            Assert.That(new SightCell(false,100).CoverPercent,Is.EqualTo(100));
        }

        [Test] public void ExtremeCoordinatesRejectBeforeOverflowOrTraversal()
        {
            SightCell Fail(GridPosition _) => throw new InvalidOperationException("Out of range");
            Assert.That(SightRules.Evaluate(P(int.MinValue,0),P(int.MaxValue,0),int.MaxValue,Fail).Reason,
                Is.EqualTo(SightFailure.OutOfRange));
            Assert.That(SightRules.Evaluate(P(0,0),P(int.MaxValue,int.MaxValue),int.MaxValue,Fail).Reason,
                Is.EqualTo(SightFailure.OutOfRange));
            Assert.That(SightRules.Evaluate(P(int.MaxValue-1,int.MinValue),P(int.MaxValue,int.MinValue+1),2,Open).CanAttack,Is.True);
            Assert.That(SightRules.Evaluate(P(int.MinValue+1,int.MaxValue),P(int.MinValue,int.MaxValue-1),2,Open).CanAttack,Is.True);
        }

        [Test] public void AxisAndDiagonalWallsBlockInBothDirections()
        {
            foreach(var target in new[]{P(4,0),P(0,4),P(4,4),P(-4,-4)})
            {
                var wall=P(target.X/2,target.Y/2);
                SightCell Map(GridPosition p)=>new SightCell(p==wall,0);
                Assert.That(SightRules.Evaluate(P(0,0),target,8,Map).Reason,Is.EqualTo(SightFailure.Blocked));
                Assert.That(SightRules.Evaluate(target,P(0,0),8,Map).Reason,Is.EqualTo(SightFailure.Blocked));
            }
        }

        [Test] public void CornerTouchingSideCellsBlockEvenAdjacentDiagonalShots()
        {
            foreach(var wall in new[]{P(1,0),P(0,1)})
            {
                SightCell Map(GridPosition p)=>new SightCell(p==wall,0);
                Assert.That(SightRules.Evaluate(P(0,0),P(1,1),2,Map).Reason,Is.EqualTo(SightFailure.Blocked));
                Assert.That(SightRules.Evaluate(P(1,1),P(0,0),2,Map).Reason,Is.EqualTo(SightFailure.Blocked));
            }
        }

        [Test] public void EndpointCellsAreNeverQueriedOrUsedAsCover()
        {
            var source=P(0,0); var target=P(4,0);
            SightCell Map(GridPosition p)
            {
                Assert.That(p!=source&&p!=target,Is.True);
                return default;
            }
            var result=SightRules.Evaluate(source,target,4,Map);
            Assert.That(result.CanAttack,Is.True);
            Assert.That(result.CoverPercent,Is.Zero);
        }

        [Test] public void TargetFrontCoverProtectsButSideAndDistantCoverDoNot()
        {
            SightCell Map(GridPosition p)=>new SightCell(false,p==P(3,0)?60:0);
            Assert.That(SightRules.Evaluate(P(0,0),P(4,0),6,Map).CoverPercent,Is.EqualTo(60));
            Assert.That(SightRules.Evaluate(P(4,4),P(4,0),6,Map).CoverPercent,Is.Zero);
            Assert.That(SightRules.Evaluate(P(0,0),P(6,0),6,Map).CoverPercent,Is.Zero);
            Assert.That(SightRules.Evaluate(P(4,0),P(0,0),6,Map).CoverPercent,Is.Zero);
        }

        [Test] public void MultipleCoverCellsUseMaximumWithoutAddingAndBlockClearsCover()
        {
            SightCell Map(GridPosition p)=>new SightCell(false,p==P(2,1)?40:p==P(1,2)?70:0);
            var result=SightRules.Evaluate(P(0,0),P(2,2),3,Map);
            Assert.That(result.CoverPercent,Is.EqualTo(70));
            SightCell BlockMap(GridPosition p)=>p==P(1,2)?new SightCell(true,70):Map(p);
            result=SightRules.Evaluate(P(0,0),P(2,2),3,BlockMap);
            Assert.That(result.Reason,Is.EqualTo(SightFailure.Blocked));
            Assert.That(result.CoverPercent,Is.Zero);
        }

        [Test] public void TraversalMatchesIndependentSegmentRectangleIntersection()
        {
            // Exhaustive small-board oracle uses slab intersections, not the production
            // grid stepping algorithm. Boundary contact deliberately counts as blocked.
            var source=P(0,0);
            for(int tx=-4;tx<=4;tx++) for(int ty=-4;ty<=4;ty++)
            {
                var target=P(tx,ty); if(target==source)continue;
                var visited=new HashSet<GridPosition>();
                SightRules.Evaluate(source,target,6,p=>{Assert.That(visited.Add(p),Is.True);return default;});
                for(int x=-5;x<=5;x++) for(int y=-5;y<=5;y++)
                {
                    var cell=P(x,y); if(cell==source||cell==target)continue;
                    double enter=0,exit=1;
                    bool expected=IntersectAxis(tx,x-.5,x+.5,ref enter,ref exit)&&
                        IntersectAxis(ty,y-.5,y+.5,ref enter,ref exit);
                    Assert.That(visited.Contains(cell),Is.EqualTo(expected),$"Ray {target}, cell {cell}");
                }
                var reverse=new HashSet<GridPosition>();
                SightRules.Evaluate(target,source,6,p=>{reverse.Add(p);return default;});
                Assert.That(reverse.SetEquals(visited),Is.True,$"Reverse ray {target}");
            }
        }

        private static bool IntersectAxis(double direction,double low,double high,ref double enter,ref double exit)
        {
            if(direction==0)return low<=0&&high>=0;
            double a=low/direction,b=high/direction;
            enter=Math.Max(enter,Math.Min(a,b));exit=Math.Min(exit,Math.Max(a,b));
            return enter<=exit;
        }

        [Test] public void RepeatedQueriesAreDeterministicAndDoNotChangeMap()
        {
            var cells=new Dictionary<GridPosition,SightCell>{{P(3,0),new SightCell(false,50)}};
            SightCell Map(GridPosition p)=>cells.TryGetValue(p,out var cell)?cell:default;
            for(int i=0;i<20;i++)
            {
                var result=SightRules.Evaluate(P(0,0),P(4,0),4,Map);
                Assert.That(result.CanAttack,Is.True);Assert.That(result.Reason,Is.EqualTo(SightFailure.None));
                Assert.That(result.CoverPercent,Is.EqualTo(50));
            }
            Assert.That(cells.Count,Is.EqualTo(1));Assert.That(cells[P(3,0)].CoverPercent,Is.EqualTo(50));
        }
    }
}
