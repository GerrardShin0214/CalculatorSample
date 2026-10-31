using System;
using System.Collections.Generic;

namespace KitchenDefense.Core
{
    /// <summary>
    /// 주방 하나 = 맵 하나. 세로 화면 기준 가로 7칸 격자이고, 몬스터는 위쪽 뒷문으로 들어와
    /// 고정 경로를 따라 아래쪽 창고로 내려온다. 월드 좌표는 칸 하나가 1 단위이고 격자 중앙이 (0, 0)이다.
    /// </summary>
    public sealed class KitchenMap
    {
        public readonly int Cols;
        public readonly int Rows;
        public readonly Cell Entrance;
        public readonly Cell Storage;

        readonly Vec2[] points;
        readonly float[] cumulative;
        readonly bool[] pathMask;
        readonly List<Cell> pathCells = new List<Cell>();
        readonly float length;

        public KitchenMap(int cols, int rows, Cell[] waypoints)
        {
            if (waypoints == null || waypoints.Length < 2) throw new ArgumentException("경로 웨이포인트가 2개 이상 필요합니다.");
            Cols = cols;
            Rows = rows;
            Entrance = waypoints[0];
            Storage = waypoints[waypoints.Length - 1];
            pathMask = new bool[cols * rows];

            points = new Vec2[waypoints.Length];
            // 몬스터는 뒷문 칸의 위쪽 가장자리에서 나타난다.
            points[0] = CellCenter(Entrance.col, Entrance.row) + new Vec2(0f, 0.5f);
            for (int i = 1; i < waypoints.Length; i++) points[i] = CellCenter(waypoints[i].col, waypoints[i].row);

            cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
                cumulative[i] = cumulative[i - 1] + Vec2.Distance(points[i - 1], points[i]);
            length = cumulative[points.Length - 1];

            MarkPath(Entrance);
            for (int i = 1; i < waypoints.Length; i++)
            {
                Cell a = waypoints[i - 1];
                Cell b = waypoints[i];
                if (a.col != b.col && a.row != b.row) throw new ArgumentException("경로는 가로/세로 직선만 지원합니다: " + a + " -> " + b);
                int dc = Math.Sign(b.col - a.col);
                int dr = Math.Sign(b.row - a.row);
                Cell c = a;
                while (c != b)
                {
                    c = new Cell(c.col + dc, c.row + dr);
                    MarkPath(c);
                }
            }
        }

        /// <summary>기본 주방: 세 번 꺾이는 뱀 모양 경로(길이 23.5칸).</summary>
        public static KitchenMap CreateDefault()
        {
            return new KitchenMap(7, 10, new[]
            {
                new Cell(1, 0), new Cell(1, 2), new Cell(5, 2), new Cell(5, 4), new Cell(1, 4),
                new Cell(1, 6), new Cell(5, 6), new Cell(5, 8), new Cell(3, 8), new Cell(3, 9),
            });
        }

        public float Length { get { return length; } }
        public int SegmentCount { get { return points.Length - 1; } }
        public IList<Cell> PathCells { get { return pathCells.AsReadOnly(); } }
        public Vec2 PathPoint(int index) { return points[index]; }
        public int PathPointCount { get { return points.Length; } }

        void MarkPath(Cell c)
        {
            if (!InBounds(c.col, c.row)) throw new ArgumentException("경로가 격자를 벗어났습니다: " + c);
            int idx = c.row * Cols + c.col;
            if (pathMask[idx]) return;
            pathMask[idx] = true;
            pathCells.Add(c);
        }

        public bool InBounds(int col, int row) { return col >= 0 && col < Cols && row >= 0 && row < Rows; }

        public bool IsPath(int col, int row) { return InBounds(col, row) && pathMask[row * Cols + col]; }

        /// <summary>뒷문과 창고 칸에는 아무것도 놓을 수 없다.</summary>
        public bool IsReserved(int col, int row)
        {
            return (col == Entrance.col && row == Entrance.row) || (col == Storage.col && row == Storage.row);
        }

        public Vec2 CellCenter(int col, int row)
        {
            return new Vec2(col - (Cols - 1) * 0.5f, (Rows - 1) * 0.5f - row);
        }

        public bool TryGetCell(Vec2 world, out Cell cell)
        {
            int col = (int)Math.Floor(world.x + (Cols - 1) * 0.5f + 0.5f);
            int row = (int)Math.Floor((Rows - 1) * 0.5f - world.y + 0.5f);
            cell = new Cell(col, row);
            return InBounds(col, row);
        }

        public bool CellContains(int col, int row, Vec2 p)
        {
            Vec2 c = CellCenter(col, row);
            return Math.Abs(p.x - c.x) <= 0.5f && Math.Abs(p.y - c.y) <= 0.5f;
        }

        public int SegmentAt(float distance)
        {
            if (distance <= 0f) return 0;
            for (int i = 1; i < cumulative.Length; i++)
                if (distance < cumulative[i]) return i - 1;
            return SegmentCount - 1;
        }

        public Vec2 PositionAt(float distance)
        {
            if (distance <= 0f) return points[0];
            if (distance >= length) return points[points.Length - 1];
            int s = SegmentAt(distance);
            float segLen = cumulative[s + 1] - cumulative[s];
            float t = segLen > 0f ? (distance - cumulative[s]) / segLen : 0f;
            return Vec2.Lerp(points[s], points[s + 1], t);
        }

        public Vec2 DirectionAt(float distance)
        {
            int s = SegmentAt(distance);
            Vec2 d = points[s + 1] - points[s];
            float l = d.Length;
            return l > 0f ? d * (1f / l) : new Vec2(0f, -1f);
        }

        /// <summary>지금 있는 직선 구간이 끝나는(다음 모퉁이) 지점까지 남은 거리.</summary>
        public float DistanceToSegmentEnd(float distance)
        {
            int s = SegmentAt(distance);
            return cumulative[s + 1] - Math.Max(0f, distance);
        }
    }
}
