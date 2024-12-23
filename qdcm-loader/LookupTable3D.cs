using System.Numerics;

namespace QdcmLoader
{
    internal sealed class LookupTable3D : ILookupTable
    {
        public int Dimension => 3;
        public int Size { get; }
        private RGBData[,,] table;

        public LookupTable3D(int size)
        {
            Size = size;
            if (size < 2 || size > 257)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }
            table = new RGBData[size, size, size];
        }

        public LookupTable3D(int size, RGBData[,,] values)
        {
            Size = size;
            if (size < 2 || size > 257)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }
            if (values.Rank != 3 || values.GetLength(0) != size || values.GetLength(1) != size || values.GetLength(2) != size)
            {
                throw new ArgumentException("wrong size", nameof(values));
            }
            this.table = values;
        }

        public ref RGBData GetTableEntry(int redIndex, int greenIndex, int blueIndex) => ref table[redIndex, greenIndex, blueIndex];
        RGBData ILookupTable.GetTableEntry(int redIndex, int greenIndex, int blueIndex) => table[redIndex, greenIndex, blueIndex];

        private Vector3 V(int redIndex, int greenIndex, int blueIndex)
        {
            ref var entry = ref table[redIndex, greenIndex, blueIndex];
            return new Vector3(entry.Red, entry.Green, entry.Blue);
        }


        private static (int lower, int upper, float delta) GetBoundsAndDelta(float value, int steps)
        {
            var scaled = value * (steps - 1);
            var lower = (int)Math.Floor(scaled);
            var upper = (int)Math.Ceiling(scaled);
            if (lower == upper)
            {
                if (upper == steps - 1)
                {
                    lower = upper - 1;
                }
                else
                {
                    upper = lower + 1;
                }
            }
            var delta = (scaled - lower) / (upper - lower);
            if (float.IsNaN(delta))
            {
                throw new Exception();
            }
            return (lower, upper, delta);
        }
        public RGBData Apply(float red, float green, float blue)
        {
            // trilinear interpolation from https://community.acescentral.com/t/3d-lut-interpolation-pseudo-code/2160

            var (R0, R1, dr) = GetBoundsAndDelta(red, Size);
            var (G0, G1, dg) = GetBoundsAndDelta(green, Size);
            var (B0, B1, db) = GetBoundsAndDelta(blue, Size);

            var c0 = V(R0, G0, B0);
            var c1 = V(R0, G0, B1) - V(R0, G0, B0);
            var c2 = V(R1, G0, B0) - V(R0, G0, B0);
            var c3 = V(R0, G1, B0) - V(R0, G0, B0);
            var c4 = V(R1, G0, B1) - V(R1, G0, B0) - V(R0, G0, B1) + V(R0, G0, B0);
            var c5 = V(R1, G1, B0) - V(R0, G1, B0) - V(R1, G0, B0) + V(R0, G0, B0);
            var c6 = V(R0, G1, B1) - V(R0, G1, B0) - V(R0, G0, B1) + V(R0, G0, B0);
            var c7 = V(R1, G1, B1) - V(R1, G1, B0) - V(R0, G1, B1) - V(R1, G0, B1) + V(R0, G0, B1) + V(R0, G1, B0) + V(R1, G0, B0) - V(R0, G0, B0);

            var result = c0 + c1 * db + c2 * dr + c3 * dg + c4 * db * dr + c5 * dr * dg + c6 * dg * db + c7 * dr * dg * db;
            return new RGBData { Red = result.X, Green = result.Y, Blue = result.Z };
        }

        public RGBData Apply2(float red, float green, float blue)
        {
            // tetrahedral interpolation
            var (R0, R1, x) = GetBoundsAndDelta(red, Size);
            var (G0, G1, y) = GetBoundsAndDelta(green, Size);
            var (B0, B1, z) = GetBoundsAndDelta(blue, Size);

            var V000 = V(R0, G0, B0);
            var V001 = V(R0, G0, B1);
            var V010 = V(R0, G1, B0);
            var V011 = V(R0, G1, B1);
            var V100 = V(R1, G0, B0);
            var V101 = V(R1, G0, B1);
            var V110 = V(R1, G1, B0);
            var V111 = V(R1, G1, B1);

            Vector3 result = default;

            if (x > y && y > z)
            {
                result = (1 - x) * V000 + (x - y) * V100 + (y - z) * V110 + z * V111;
            }
            else if (x > y && x > z)
            {
                result = (1 - x) * V000 + (x - z) * V100 + (z - y) * V101 + y * V111;
            }
            else if (x > y && y <= z && x <= z)
            {
                result = (1 - z) * V000 + (z - x) * V001 + (x - y) * V101 + y * V111;
            }
            else if (x <= y && z > y)
            {
                result = (1 - z) * V000 + (z - y) * V001 + (y - x) * V011 + x * V111;
            }
            else if (x <= y && z > x)
            {
                result = (1 - y) * V000 + (y - z) * V010 + (z - x) * V011 + x * V111;
            }
            else if (x <= y && z <= y && z <= x)
            {
                result = (1 - y) * V000 + (y - x) * V010 + (x - z) * V110 + z * V111;
            }

            return new RGBData(result.X, result.Y, result.Z);
        }

        public LookupTable3D Resample(int newChannelSize)
        {
            if (newChannelSize == Size) return Clone();
            var result = new LookupTable3D(newChannelSize);
            var scale = newChannelSize - 1.0f;
            for (int r = 0; r < newChannelSize; r++)
                for (int g = 0; g < newChannelSize; g++)
                    for (int b = 0; b < newChannelSize; b++)
                    {
                        result.table[r, g, b] = Apply2(r / scale, g / scale, b / scale);
                    }
            return result;
        }

        public LookupTable3D Clone()
        {
            return new LookupTable3D(Size, (RGBData[,,])table.Clone());
        }

        object ICloneable.Clone()
        {
            return this.Clone();
        }
    }
}
