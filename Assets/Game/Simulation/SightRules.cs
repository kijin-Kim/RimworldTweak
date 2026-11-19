using System;

namespace RimworldTweak.Simulation
{
    public readonly struct SightCell
    {
        public SightCell(bool blocksShot, int coverPercent)
        {
            if (coverPercent < 0 || coverPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(coverPercent));
            BlocksShot = blocksShot;
            CoverPercent = coverPercent;
        }

        public bool BlocksShot { get; }
        public int CoverPercent { get; }
    }

    public enum SightFailure { None, SamePosition, OutOfRange, Blocked }

    public readonly struct SightResult
    {
        internal SightResult(SightFailure reason, int coverPercent = 0)
        {
            Reason = reason;
            CoverPercent = coverPercent;
        }

        public bool CanAttack => Reason == SightFailure.None;
        public SightFailure Reason { get; }
        public int CoverPercent { get; }
    }

    public static class SightRules
    {
        public static SightResult Evaluate(GridPosition source, GridPosition target, int maxRange,
            Func<GridPosition, SightCell> cellAt)
        {
            if (maxRange <= 0) throw new ArgumentOutOfRangeException(nameof(maxRange));
            if (cellAt == null) throw new ArgumentNullException(nameof(cellAt));
            if (source == target) return new SightResult(SightFailure.SamePosition);

            // Subtract in Int64 before taking absolute values. The axis check bounds each
            // squared component to Int32.MaxValue squared, so their sum also fits Int64.
            long dx = (long)target.X - source.X, dy = (long)target.Y - source.Y;
            long nx = Math.Abs(dx), ny = Math.Abs(dy);
            if (nx > maxRange || ny > maxRange || nx * nx + ny * ny > (long)maxRange * maxRange)
                return new SightResult(SightFailure.OutOfRange);

            int stepX = Math.Sign(dx), stepY = Math.Sign(dy);
            int x = source.X, y = source.Y, cover = 0;
            long ix = 0, iy = 0;
            while (ix < nx || iy < ny)
            {
                // Compare exact crossing times without floating-point rounding. When a
                // ray touches a corner, both side cells count, as well as the diagonal.
                long crossingX = (2 * ix + 1) * ny;
                long crossingY = (2 * iy + 1) * nx;
                if (crossingX == crossingY)
                {
                    if (Blocks(new GridPosition(x + stepX, y), source, target, cellAt, ref cover) ||
                        Blocks(new GridPosition(x, y + stepY), source, target, cellAt, ref cover))
                        return new SightResult(SightFailure.Blocked);
                    x += stepX; y += stepY; ix++; iy++;
                }
                else if (crossingX < crossingY) { x += stepX; ix++; }
                else { y += stepY; iy++; }

                if (Blocks(new GridPosition(x, y), source, target, cellAt, ref cover))
                    return new SightResult(SightFailure.Blocked);
            }
            return new SightResult(SightFailure.None, cover);
        }

        private static bool Blocks(GridPosition position, GridPosition source, GridPosition target,
            Func<GridPosition, SightCell> cellAt, ref int cover)
        {
            if (position == source || position == target) return false;
            var cell = cellAt(position);
            if (cell.BlocksShot) return true;
            if (Math.Abs((long)position.X - target.X) <= 1 && Math.Abs((long)position.Y - target.Y) <= 1)
                cover = Math.Max(cover, cell.CoverPercent);
            return false;
        }
    }
}
