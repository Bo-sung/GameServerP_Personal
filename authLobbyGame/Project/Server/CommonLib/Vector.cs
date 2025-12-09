using System;

namespace CommonLib
{
    /// <summary>
    /// 2D 벡터 구조체
    /// </summary>
    public struct Vector2 : IEquatable<Vector2>
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 벡터의 크기
        /// </summary>
        public float Magnitude => (float)Math.Sqrt(X * X + Y * Y);

        /// <summary>
        /// 벡터의 크기의 제곱
        /// </summary>
        public float SqrMagnitude => X * X + Y * Y;

        /// <summary>
        /// 정규화된 벡터 (크기가 1인 벡터)
        /// </summary>
        public Vector2 Normalized
        {
            get
            {
                float mag = Magnitude;
                if (mag > 0)
                    return new Vector2(X / mag, Y / mag);
                return Zero;
            }
        }

        /// <summary>
        /// 벡터를 정규화
        /// </summary>
        public void Normalize()
        {
            float mag = Magnitude;
            if (mag > 0)
            {
                X /= mag;
                Y /= mag;
            }
        }

        /// <summary>
        /// 두 벡터 사이의 거리
        /// </summary>
        public static float Distance(Vector2 a, Vector2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 두 벡터의 내적
        /// </summary>
        public static float Dot(Vector2 a, Vector2 b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        /// <summary>
        /// 두 벡터 사이의 각도 (라디안)
        /// </summary>
        public static float Angle(Vector2 a, Vector2 b)
        {
            float dot = Dot(a, b);
            float mag = a.Magnitude * b.Magnitude;
            if (mag == 0)
                return 0;
            return (float)Math.Acos(Math.Clamp(dot / mag, -1.0f, 1.0f));
        }

        /// <summary>
        /// 두 벡터를 선형 보간
        /// </summary>
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new Vector2(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t
            );
        }

        // 정적 벡터
        public static Vector2 Zero => new Vector2(0, 0);
        public static Vector2 One => new Vector2(1, 1);
        public static Vector2 Up => new Vector2(0, 1);
        public static Vector2 Down => new Vector2(0, -1);
        public static Vector2 Left => new Vector2(-1, 0);
        public static Vector2 Right => new Vector2(1, 0);

        // 연산자 오버로딩
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.X, -a.Y);
        public static Vector2 operator *(Vector2 a, float scalar) => new Vector2(a.X * scalar, a.Y * scalar);
        public static Vector2 operator *(float scalar, Vector2 a) => new Vector2(a.X * scalar, a.Y * scalar);
        public static Vector2 operator /(Vector2 a, float scalar) => new Vector2(a.X / scalar, a.Y / scalar);

        public static bool operator ==(Vector2 a, Vector2 b) => a.Equals(b);
        public static bool operator !=(Vector2 a, Vector2 b) => !a.Equals(b);

        public bool Equals(Vector2 other)
        {
            return Math.Abs(X - other.X) < float.Epsilon && Math.Abs(Y - other.Y) < float.Epsilon;
        }

        public override bool Equals(object? obj)
        {
            return obj is Vector2 vector && Equals(vector);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }


}
