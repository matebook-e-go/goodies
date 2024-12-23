namespace QdcmLoader;

class LookupTable3x1D : ILookupTable
{
    public int Dimension => 1;
    public int Size { get; }
    private RGBData[] table;

    public LookupTable3x1D(int size)
    {
        Size = size;
        if (size < 2 || size > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }
        table = new RGBData[size];
    }

    public LookupTable3x1D(RGBData[] table)
    {
        Size = table.Length;
        if (Size < 2 || Size > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(table));
        }
        this.table = table;
    }

    public RGBData GetTableEntry(int r, int g, int b)
    {
        var rval = table[r].Red;
        var gval = table[g].Green;
        var bval = table[b].Blue;
        return new RGBData(rval, gval, bval);
    }

    public RGBData Apply(float r, float g, float b)
    {
        // linear interpolation
        var rpos = r * (Size - 1);
        var gpos = g * (Size - 1);
        var bpos = b * (Size - 1);
        var rlower = (int)Math.Floor(rpos);
        var rupper = (int)Math.Ceiling(rpos);
        var rdelta = rpos - rlower;
        var glower = (int)Math.Floor(gpos);
        var gupper = (int)Math.Ceiling(gpos);
        var gdelta = gpos - glower;
        var blower = (int)Math.Floor(bpos);
        var bupper = (int)Math.Ceiling(bpos);
        var bdelta = bpos - blower;
        var rout = table[rlower].Red * (1 - rdelta) + table[rupper].Red * rdelta;
        var gout = table[glower].Green * (1 - gdelta) + table[gupper].Green * gdelta;
        var bout = table[blower].Blue * (1 - bdelta) + table[bupper].Blue * bdelta;
        return new RGBData(rout, gout, bout);
    }

    public LookupTable3x1D Resize(int newSize)
    {
        if (newSize < 2 || newSize > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(newSize));
        }
        if (newSize == Size)
        {
            return Clone();
        }
        var newTable = new RGBData[newSize];
        for (var i = 0; i < newSize; i++)
        {
            var x = (float)i / (newSize - 1);
            newTable[i] = Apply(x, x, x);
        }
        return new LookupTable3x1D(newTable);
    }

    public LookupTable3x1D Clone()
    {
        return new LookupTable3x1D((RGBData[])table.Clone());
    }

    object ICloneable.Clone() => Clone();
}