
using QdcmLoader;

internal interface ILookupTable : ICloneable
{
    int Size { get; }
    int Dimension { get; }

    RGBData GetTableEntry(int r, int g, int b);
    RGBData Apply(float r, float g, float b);
}