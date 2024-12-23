using System.Text;

namespace QdcmLoader
{
    internal class CubeReader
    {
        private Stream stream;
        private RGBData[]? table;
        private int lineno;
        private int size1;
        private int size2;
        private int size3;
        private int rowcount;
        private int dimension;
        private bool intable;

        public CubeReader(Stream s)
        {
            this.stream = s;
        }

        public ILookupTable Read()
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            lineno = 0;
            rowcount = 0;
            intable = false;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null) break;

                lineno++;

                if (!ParseLine(line)) break;
            }

            if (table == null)
            {
                throw new IOException("no valid data in stream");
            }


            if (dimension == 1)
            {
                if (rowcount != size1)
                {
                    throw new IOException($"incomplete 3DLUT: expect {size1} entries, got {rowcount} entries");
                }
                return new LookupTable3x1D(table);
            }
            else if (dimension == 3)
            {
                if (rowcount != size3)
                {
                    throw new IOException($"incomplete 3DLUT: expect {size3} entries, got {rowcount} entries");
                }
                var array3d = new RGBData[size1, size1, size1];
                for (int b = 0; b < size1; b++)
                {
                    for (int g = 0; g < size1; g++)
                    {
                        for (int r = 0; r < size1; r++)
                        {
                            array3d[r, g, b] = table[b * size1 * size1 + g * size1 + r];
                        }
                    }
                }
                return new LookupTable3D(size1, array3d);
            }
            throw new IOException("something went wrong");
        }

        private bool ParseLine(string line)
        {
            // empty line
            if (line.Length == 0) return true;

            // comment
            if (line[0] == '#') return true;

            var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            // whitespace-only line
            if (tokens.Length == 0) return true;

            if (!intable && ParseHeader(tokens)) return true;

            ParseTableRow(tokens);

            if (rowcount == size3)
            {
                return false;
            }

            return true;
        }

        private bool ParseHeader(string[] tokens)
        {
            // IRIDAS title
            if (tokens[0] == "TITLE")
            {

                if (tokens.Length < 2 || tokens[1][0] != '"' || tokens[^1][^1] != '"') throw new IOException($"line {lineno}: malformed TITLE line");
                // title is ignored
                return true;
            }

            // IRIDAS domain definition
            if (tokens[0] == "DOMAIN_MIN")
            {
                if (tokens.Length != 4) throw new IOException($"line {lineno}: malformed DOMAIN_MIN line");
                double rmin, gmin, bmin;
                try
                {
                    rmin = double.Parse(tokens[1]);
                    gmin = double.Parse(tokens[2]);
                    bmin = double.Parse(tokens[3]);
                }
                catch (Exception e)
                {
                    throw new IOException($"line {lineno}: malformed DOMAIN_MIN line", e);
                }
                if (rmin != 0.0 || gmin != 0.0 || bmin != 0.0) throw new IOException($"line {lineno}: non-zero DOMAIN_MIN is not supported");
                return true;
            }

            if (tokens[0] == "DOMAIN_MAX")
            {
                if (tokens.Length != 4) throw new IOException($"line {lineno}: malformed DOMAIN_MAX line");
                double rmax, gmax, bmax;
                try
                {
                    rmax = double.Parse(tokens[1]);
                    gmax = double.Parse(tokens[2]);
                    bmax = double.Parse(tokens[3]);
                }
                catch (Exception e)
                {
                    throw new IOException($"line {lineno}: malformed DOMAIN_MAX line", e);
                }
                if (rmax != 1.0 || gmax != 1.0 || bmax != 1.0) throw new IOException($"line {lineno}: DOMAIN_MAX other than 1.0 is not supported");
                return true;
            }

            // Resolve domain definition
            if (tokens[0] == "LUT_3D_INPUT_RANGE")
            {
                if (tokens.Length != 3) throw new IOException($"line {lineno}: malformed LUT_3D_INPUT_RANGE line");
                double min, max;
                try
                {
                    min = double.Parse(tokens[1]);
                    max = double.Parse(tokens[2]);
                }
                catch (Exception e)
                {
                    throw new IOException($"line {lineno}: malformed LUT_3D_INPUT_RANGE line", e);
                }
                if (min != 0.0 || max != 1.0) throw new IOException($"line {lineno}: LUT_3D_INPUT_RANGE other than 0.0 to 1.0 is not supported");
                dimension = 3;
                return true;
            }

            if (tokens[0] == "LUT_1D_INPUT_RANGE")
            {
                if (tokens.Length != 3) throw new IOException($"line {lineno}: malformed LUT_1D_INPUT_RANGE line");
                double min, max;
                try
                {
                    min = double.Parse(tokens[1]);
                    max = double.Parse(tokens[2]);
                }
                catch (Exception e)
                {
                    throw new IOException($"line {lineno}: malformed LUT_1D_INPUT_RANGE line", e);
                }
                dimension = 1;
                return true;
            }


            if (tokens[0] == "LUT_3D_SIZE")
            {
                if (tokens.Length != 2) throw new IOException($"line {lineno}: malformed LUT_3D_SIZE line");
                try
                {
                    size1 = (int)uint.Parse(tokens[1]);
                    if (size1 < 2 || size1 > 256) throw new IOException($"unsupported size {size1}");
                }
                catch (Exception e)
                {
                    throw new IOException($"line {lineno}: malformed LUT_3D_SIZE line", e);
                }
                if (table != null)
                {
                    throw new IOException($"line {lineno}: duplicate LUT_3D_SIZE");
                }
                if (dimension == 1)
                {
                    throw new IOException($"line {lineno}: dimension mismatch");
                }
                dimension = 3;
                size2 = size1 * size1;
                size3 = size1 * size1 * size1;
                table = new RGBData[size3];
                return true;
            }

            if (tokens[0] == "LUT_1D_SIZE")
            {
                if (tokens.Length != 2) throw new IOException($"line {lineno}: malformed LUT_1D_SIZE line");
                try
                {
                    size1 = (int)uint.Parse(tokens[1]);
                    if (size1 < 2 || size1 > 65536) throw new IOException($"unsupported size {size1}");
                }
                catch (Exception e)
                {
                    throw new IOException($"line {lineno}: malformed LUT_1D_SIZE line", e);
                }
                if (dimension == 3)
                {
                    throw new IOException($"line {lineno}: dimension mismatch");
                }
                dimension = 1;
                table = new RGBData[size1];

                return true;
            }

            if (tokens.Length == 3 && double.TryParse(tokens[0], out _))
            {
                intable = true;
                return false;
            }

            throw new IOException($"line {lineno}: unrecognized token {tokens[0]}");

        }

        private void ParseTableRow(string[] tokens)
        {
            if (tokens.Length != 3)
            {
                throw new IOException($"line {lineno}: malformed table row {tokens[0]}");
            }
            if (table == null)
            {
                throw new IOException($"line {lineno}: table data appeared before table size");
            }

            float r, g, b;
            try
            {
                r = float.Parse(tokens[0]);
                g = float.Parse(tokens[1]);
                b = float.Parse(tokens[2]);
            }
            catch (Exception e)
            {
                throw new IOException($"line {lineno}: malformed table line", e);
            }

            if (dimension == 1)
            {
                ref var entry = ref table[rowcount];
                entry.Red = r;
                entry.Green = g;
                entry.Blue = b;
            }
            else if (dimension == 3)
            {
                var bindex = rowcount / size2;
                var gindex = (rowcount % size2) / size1;
                var rindex = rowcount % size1;

                ref var entry = ref table[rowcount];
                entry.Red = r;
                entry.Green = g;
                entry.Blue = b;
            }
            rowcount++;
        }
    }
}
