using System;
using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    internal sealed class WalkingRoute : System.Collections.ObjectModel.ReadOnlyCollection<GridPosition>
    {
        internal WalkingRoute(List<GridPosition> cells, int cost) : base(cells) { Cost = cost; }
        internal int Cost { get; }
    }

    internal static class Walking
    {
        internal const int Straight = 5;
        internal const int Diagonal = 7;

        internal static int StepCost(GridPosition from, GridPosition to) =>
            from.X != to.X && from.Y != to.Y ? Diagonal : Straight;

        internal static long Estimate(int x, int y, int goalX, int goalY)
        {
            long dx = Math.Abs((long)x - goalX), dy = Math.Abs((long)y - goalY);
            return Straight * Math.Max(dx, dy) + (Diagonal - Straight) * Math.Min(dx, dy);
        }

        internal static int RouteCost(GridPosition from, IReadOnlyList<GridPosition> path)
        {
            if (path is WalkingRoute route) return route.Cost;
            int cost = 0;
            foreach (var cell in path)
            {
                cost += StepCost(from, cell);
                from = cell;
            }
            return cost;
        }
    }

    public sealed partial class WorldState
    {
        // A step adds at most 7 to g and at most 7 to the consistent octile
        // heuristic. Sixteen cyclic FIFO buckets cover the pending priorities.
        private sealed class CostQueue<T>
        {
            private readonly Queue<T>[] buckets = new Queue<T>[16];
            private int count;
            private long priority;
            public CostQueue()
            {
                for (int i = 0; i < buckets.Length; i++) buckets[i] = new Queue<T>();
            }
            public void Reset(T origin, long initialPriority)
            {
                foreach (var bucket in buckets) bucket.Clear();
                count = 0;
                priority = initialPriority;
                Add(origin, priority);
            }
            public void Add(T cell, long score)
            {
                buckets[score & 15].Enqueue(cell);
                count++;
            }
            public bool TryPop(out T cell)
            {
                if (count == 0) { cell = default; return false; }
                while (buckets[priority & 15].Count == 0) priority++;
                cell = buckets[priority & 15].Dequeue();
                count--;
                return true;
            }
        }

        private sealed class GridPathSearch
        {
            private readonly int stride;
            private readonly int[] parents, distances, steps;
            private readonly bool[] blocked, settled;
            private readonly int[] touched;
            private readonly CostQueue<int> frontier = new CostQueue<int>();
            private int origin, touchedCount;
            private long mapRevision = -1;

            private readonly bool directed;
            private int target, targetX, targetY;
            public GridPathSearch(int width, int height, bool directed)
            {
                this.directed = directed;
                stride = width + 2;
                int length = stride * (height + 2);
                parents = new int[length];
                distances = new int[length];
                steps = new int[length];
                blocked = new bool[length];
                settled = new bool[length];
                touched = new int[width * height];
            }

            private int Index(GridPosition p) => (p.Y + 1) * stride + p.X + 1;
            public int Cost(GridPosition p) => distances[Index(p)];
            public int StepsTo(GridPosition p) => steps[Index(p)];

            public bool Reaches(GridPosition start, GridPosition destination, long revision, List<GridPosition> obstacles)
            {
                if (revision != mapRevision)
                {
                    Array.Clear(blocked, 0, blocked.Length);
                    int last = blocked.Length - stride;
                    for (int x = 0; x < stride; x++) blocked[x] = blocked[last + x] = true;
                    for (int row = stride; row < last; row += stride)
                        blocked[row] = blocked[row + stride - 1] = true;
                    foreach (var cell in obstacles) blocked[Index(cell)] = true;
                    origin = 0;
                    mapRevision = revision;
                }
                int first = Index(start);
                int goal = Index(destination);
                if (origin != first || (directed && target != goal))
                {
                    for (int i = 0; i < touchedCount; i++)
                    {
                        parents[touched[i]] = 0;
                        settled[touched[i]] = false;
                    }
                    origin = first;
                    parents[first] = first;
                    distances[first] = 0;
                    steps[first] = 0;
                    touched[0] = first;
                    touchedCount = 1;
                    target = goal;
                    targetX = destination.X + 1;
                    targetY = destination.Y + 1;
                    frontier.Reset(first, directed ? Walking.Estimate(start.X,start.Y,destination.X,destination.Y) : 0);
                }

                while (!settled[goal] && frontier.TryPop(out int cell))
                {
                    if (settled[cell]) continue;
                    settled[cell] = true;
                    int east = cell + 1, north = cell + stride, west = cell - 1, south = cell - stride;
                    int straightCost = distances[cell] + Walking.Straight;
                    int diagonalCost = distances[cell] + Walking.Diagonal;
                    if (!blocked[east]) Visit(east, cell, straightCost);
                    if (!blocked[north]) Visit(north, cell, straightCost);
                    if (!blocked[west]) Visit(west, cell, straightCost);
                    if (!blocked[south]) Visit(south, cell, straightCost);
                    if (!blocked[north] && !blocked[east] && !blocked[north + 1]) Visit(north + 1, cell, diagonalCost);
                    if (!blocked[north] && !blocked[west] && !blocked[north - 1]) Visit(north - 1, cell, diagonalCost);
                    if (!blocked[south] && !blocked[west] && !blocked[south - 1]) Visit(south - 1, cell, diagonalCost);
                    if (!blocked[south] && !blocked[east] && !blocked[south + 1]) Visit(south + 1, cell, diagonalCost);
                }
                return settled[goal];
            }

            private void Visit(int cell, int parent, int cost)
            {
                if (parents[cell] == 0) touched[touchedCount++] = cell;
                else if (distances[cell] <= cost) return;
                distances[cell] = cost;
                steps[cell] = steps[parent] + 1;
                parents[cell] = parent;
                frontier.Add(cell, directed ? cost + Walking.Estimate(cell % stride, cell / stride, targetX, targetY) : cost);
            }

            public IReadOnlyList<GridPosition> BuildRoute(GridPosition destination)
            {
                var route = new List<GridPosition>();
                for (int cell = Index(destination); cell != origin; cell = parents[cell])
                    route.Add(new GridPosition(cell % stride - 1, cell / stride - 1));
                route.Reverse();
                return new WalkingRoute(route, Cost(destination));
            }
        }

        private sealed class SparsePathSearch
        {
            private readonly WorldState world;
            private readonly CostQueue<GridPosition> frontier = new CostQueue<GridPosition>();
            private readonly Dictionary<GridPosition, int> costs = new Dictionary<GridPosition, int>();
            private readonly Dictionary<GridPosition, GridPosition> parents = new Dictionary<GridPosition, GridPosition>();
            private readonly HashSet<GridPosition> settled = new HashSet<GridPosition>();
            private static readonly GridPosition[] Steps = {
                new GridPosition(1,0), new GridPosition(0,1), new GridPosition(-1,0), new GridPosition(0,-1),
                new GridPosition(1,1), new GridPosition(-1,1), new GridPosition(-1,-1), new GridPosition(1,-1)
            };
            private GridPosition origin;
            private long revision = -1;
            private readonly bool directed;
            private GridPosition target;
            public SparsePathSearch(WorldState world, bool directed) { this.world = world; this.directed = directed; }
            public int Cost(GridPosition cell) => costs[cell];
            public int StepsTo(GridPosition cell)
            {
                int count = 0;
                while (cell != origin) { cell = parents[cell]; count++; }
                return count;
            }

            public bool Reaches(GridPosition start, GridPosition destination)
            {
                if (start != origin || revision != world.MapRevision || (directed && target != destination))
                {
                    origin = start;
                    revision = world.MapRevision;
                    settled.Clear();
                    parents.Clear();
                    costs.Clear();
                    costs[start] = 0;
                    target = destination;
                    frontier.Reset(start, directed ? Walking.Estimate(start.X,start.Y,target.X,target.Y) : 0);
                }
                while (!settled.Contains(destination) && frontier.TryPop(out var cell))
                {
                    if (!settled.Add(cell)) continue;
                    foreach (var step in Steps)
                    {
                        var next = new GridPosition(cell.X + step.X, cell.Y + step.Y);
                        if (!world.CanWalkStep(cell, next)) continue;
                        int cost = costs[cell] + Walking.StepCost(cell, next);
                        if (costs.TryGetValue(next, out int previous) && cost >= previous) continue;
                        costs[next] = cost;
                        parents[next] = cell;
                        frontier.Add(next, directed ? cost + Walking.Estimate(next.X,next.Y,target.X,target.Y) : cost);
                    }
                }
                return settled.Contains(destination);
            }
            public IReadOnlyList<GridPosition> BuildRoute(GridPosition destination)
            {
                var route = new List<GridPosition>();
                for (var cell = destination; cell != origin; cell = parents[cell]) route.Add(cell);
                route.Reverse();
                return new WalkingRoute(route, Cost(destination));
            }
        }
    }
}
