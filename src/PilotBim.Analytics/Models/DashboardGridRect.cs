using System;

namespace PilotBim.Analytics.Models
{
    /// <summary>
    /// Logical dashboard grid rectangle. Integer grid units, never pixels.
    /// </summary>
    internal struct DashboardGridRect : IEquatable<DashboardGridRect>
    {
        public DashboardGridRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public int Right
        {
            get { return X + Width; }
        }

        public int Bottom
        {
            get { return Y + Height; }
        }

        public bool Equals(DashboardGridRect other)
        {
            return X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            return obj is DashboardGridRect && Equals((DashboardGridRect)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Width;
                hash = (hash * 397) ^ Height;
                return hash;
            }
        }

        public override string ToString()
        {
            return X + "," + Y + " " + Width + "x" + Height;
        }
    }
}
