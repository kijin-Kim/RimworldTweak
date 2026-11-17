using System;
using System.IO;
using System.Runtime.Serialization.Json;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class PlacementPreviewTests
    {
        private static GridPosition P(int x,int y)=>new GridPosition(x,y);
        private static SimulationSession Session()=>new SimulationSession(new ScenarioDefinition(6,6,new[]{P(0,0)},
            new[]{new ColonistSeed(1,P(1,1))},new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(2,2))},
            new[]{new BedSeed(201,P(3,3))},false,new[]{new GatherSeed(301,GatherKind.Tree,P(4,4))}));
        private static byte[] State(SimulationSession session)
        {
            var data=session.CaptureSaveData();Assert.That(data.Success,Is.True,data.ErrorCode);
            // Rejected commits intentionally report an error; compare persistent game state separately.
            data.Data.World.LastCommandError=string.Empty;
            using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(SaveData)).WriteObject(stream,data.Data);return stream.ToArray();}
        }
        [Test] public void BlueprintPreviewLeavesStateUntouchedAndMatchesCommitAtEveryCell()
        {
            foreach(BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
                for(var y=-1;y<=6;y++)for(var x=-1;x<=6;x++)
                {
                    var s=Session();var before=State(s);var error=s.World.LastCommandError;
                    var allowed=s.World.CanPlaceBlueprint(kind,P(x,y),out var reason);
                    CollectionAssert.AreEqual(before,State(s));Assert.That(s.World.LastCommandError,Is.EqualTo(error));
                    Assert.That(s.World.PlaceBlueprint(kind,P(x,y)),Is.EqualTo(allowed),kind+" "+P(x,y));
                    if(!allowed){CollectionAssert.AreEqual(before,State(s));Assert.That(s.World.LastCommandError,Is.EqualTo(reason));}
                }
        }
        [Test] public void RectanglePreviewPreservesStateForReverseSingleOccupiedAndOutOfBoundsAreas()
        {
            var pairs=new[]{P(1,4),P(2,5),P(2,2),P(2,2),P(5,5),P(5,5),P(-1,0),P(1,1),P(0,0),P(5,5)};
            for(var i=0;i<pairs.Length;i+=2)foreach(var growing in new[]{false,true})foreach(var reverse in new[]{false,true})
            {
                var s=Session();var a=pairs[i+(reverse?1:0)];var b=pairs[i+(reverse?0:1)];var before=State(s);
                var allowed=growing?s.World.CanCreateGrowingZone(a,b):s.World.CanCreateStorage(a,b);
                CollectionAssert.AreEqual(before,State(s));Assert.That(s.World.LastCommandError,Is.Empty);
                Assert.That(growing?s.World.CreateGrowingZone(a,b):s.World.QueueCreateStorage(a,b),Is.EqualTo(allowed));
                if(!allowed)CollectionAssert.AreEqual(before,State(s));
            }
        }
    }
}
