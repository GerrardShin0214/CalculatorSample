using System;
using System.Collections.Generic;

namespace KitchenDefense.Core
{
    /// <summary>엔진에 의존하지 않는 2D 벡터. Core 어셈블리는 UnityEngine을 참조하지 않는다.</summary>
    [Serializable]
    public struct Vec2
    {
        public float x;
        public float y;

        public Vec2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.x + b.x, a.y + b.y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.x - b.x, a.y - b.y); }
        public static Vec2 operator *(Vec2 a, float s) { return new Vec2(a.x * s, a.y * s); }

        public float Length { get { return (float)Math.Sqrt(x * x + y * y); } }
        public float SqrLength { get { return x * x + y * y; } }

        public static float Distance(Vec2 a, Vec2 b) { return (a - b).Length; }
        public static float SqrDistance(Vec2 a, Vec2 b) { return (a - b).SqrLength; }

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
        {
            return new Vec2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        }

        public override string ToString() { return "(" + x + ", " + y + ")"; }
    }

    /// <summary>격자 칸 좌표. col은 왼쪽→오른쪽, row는 위→아래(0행이 화면 맨 위).</summary>
    [Serializable]
    public struct Cell : IEquatable<Cell>
    {
        public int col;
        public int row;

        public Cell(int col, int row)
        {
            this.col = col;
            this.row = row;
        }

        public bool Equals(Cell other) { return col == other.col && row == other.row; }
        public override bool Equals(object obj) { return obj is Cell && Equals((Cell)obj); }
        public override int GetHashCode() { return col * 397 ^ row; }
        public static bool operator ==(Cell a, Cell b) { return a.Equals(b); }
        public static bool operator !=(Cell a, Cell b) { return !a.Equals(b); }
        public override string ToString() { return "(" + col + "," + row + ")"; }
    }

    public static class MathUtil
    {
        public static float Clamp(float v, float min, float max) { return v < min ? min : (v > max ? max : v); }
        public static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }

        public static int Round(float v) { return (int)Math.Round(v, MidpointRounding.AwayFromZero); }

        /// <summary>value를 step 단위로 반올림한다. 예: RoundTo(214, 10) = 210</summary>
        public static int RoundTo(float value, int step)
        {
            if (step <= 1) return Round(value);
            return Round(value / step) * step;
        }

        public static float Pow(float b, int e)
        {
            float r = 1f;
            for (int i = 0; i < e; i++) r *= b;
            return r;
        }
    }

    /// <summary>
    /// 저장 가능한 결정론적 난수(xorshift32). 세이브에 상태가 그대로 들어가므로
    /// 이어하기를 해도 같은 결과가 나온다. UnityEngine.Random은 Core에서 쓰지 않는다.
    /// </summary>
    [Serializable]
    public class Rng
    {
        public int state;

        public Rng() : this(12345) { }

        public Rng(int seed)
        {
            state = seed != 0 ? seed : 0x2545F491;
        }

        public uint NextUInt()
        {
            unchecked
            {
                uint x = (uint)state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                if (x == 0) x = 0x9E3779B9u;
                state = (int)x;
                return x;
            }
        }

        /// <summary>[0, 1) 범위 실수.</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>[minInclusive, maxExclusive) 범위 정수.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
