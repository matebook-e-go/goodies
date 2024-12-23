using System.Buffers.Binary;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Runtime.InteropServices;

namespace QdcmLoader
{
    enum Preset
    {
        sRGB,
        DisplayP3
    }

    internal class Program
    {
        static int Main(string[] args)
        {
            var presetoption = new Option<Preset?>("--preset", "Load factory calibration");
            var resetoption = new Option<bool>("--reset", "Reset display color");
            var igcoption = new Option<string?>("--igc", "Load custom input shaper (3x1D LUT applied before 3D LUT)") { ArgumentHelpName = "file.cube" };
            igcoption.AddValidator(result =>
            {
                var val = result.GetValueForOption(igcoption);
                if (val != null && !File.Exists(val))
                {
                    result.ErrorMessage = "file not exists: " + val;
                }
            });
            var lut3doption = new Option<string?>("--3dlut", "Load custom 3D LUT") { ArgumentHelpName = "file.cube" };
            lut3doption.AddValidator(result =>
            {
                var val = result.GetValueForOption(lut3doption);
                if (val != null && !File.Exists(val))
                {
                    result.ErrorMessage = "file not exists: " + val;
                }
            });
            var rootCommand = new RootCommand("QDCM loader for Gaokun") { presetoption, resetoption, igcoption, lut3doption };
            rootCommand.SetHandler(HandleParsedCommand, presetoption, resetoption, igcoption, lut3doption);
            rootCommand.AddValidator(result =>
            {
                if (result.Children.Count == 0)
                {
                    result.ErrorMessage = "missing action";
                }
                else if (result.Children.Count > 1)
                {
                    if (result.Children.Any(x => x.Symbol == resetoption))
                    {
                        result.ErrorMessage = "cannot use other option with --reset";
                    }
                    else if (result.Children.Any(x => x.Symbol == presetoption))
                    {
                        result.ErrorMessage = "cannot use other option with --preset";
                    }
                }
            });
            var parser = new CommandLineBuilder(rootCommand)
                .UseExceptionHandler()
                .UseParseErrorReporting()
                .CancelOnProcessTermination()
                .UseHelp(ctx =>
                {
                    ctx.HelpBuilder.CustomizeLayout(
                        _ =>
                            HelpBuilder.Default
                            .GetLayout()
                            .Append(x =>
                            {
                                ctx.Output.WriteLine("Remarks:");
                                ctx.Output.WriteLine("  Both IRIDAS and Resolve .cube format are supported.");
                            })
                        );
                })
                .Build();
            return parser.Invoke(args);
        }

        static Task<int> HandleParsedCommand(Preset? preset, bool reset, string? igcfile, string? lut3dfile)
        {
            //Console.WriteLine("igc: {0}", igcfile);
            //Console.WriteLine("pcc: {0}", pcc);
            //Console.WriteLine("3dlut: {0}", lut3dfile);
            //Console.WriteLine("vcgt: {0}", vcgtfile);


            DisplayPlatformQcom plat;
            DisplayTargetQcom disp;
            try
            {
                plat = DisplayPlatformQcom.Instance;
                var disps = plat.EnumDisplays().Where(x => x.SupportsLookupTable3D).ToArray();
                if (disps.Length != 1)
                {
                    Console.Error.WriteLine("No supported display found");
                    return Task.FromResult(1);
                }
                disp = disps[0];
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to load QDCM: {0}", ex);
                return Task.FromResult(1);
            }

            if (reset)
            {
                plat.SetDegammaShaper(disp, null);
                plat.SetLookupTable3D(disp, null);
                plat.SetMatrix(disp, new[] {
                    1f, 0f, 0f,
                    0f, 1f, 0f,
                    0f, 0f, 1f });
                return Task.FromResult(0);
            }

            LookupTable3x1D? igc = null;
            LookupTable3D? lut3d = null;

            if (preset.HasValue)
            {
                lut3d = LoadFactoryCalibration(preset.Value);
                if (lut3d == null)
                {
                    Console.Error.WriteLine("Failed to load factory calibration");
                    return Task.FromResult(1);
                }
                plat.SetLookupTable3D(disp, lut3d);
                return Task.FromResult(0);
            }


            //float[]? pccarr = null;
            //LookupTable3x1D? vcgt = null;

            if (igcfile != null)
            {
                using var fs = File.OpenRead(igcfile);
                var reader = new CubeReader(fs);
                try
                {
                    igc = (LookupTable3x1D)reader.Read();
                }
                catch (InvalidCastException)
                {
                    Console.Error.WriteLine($"{igcfile}: Expected 1D LUT but get a 3D LUT.");
                    return Task.FromResult(1);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"failed to load {igcfile}: {ex.Message}");
                    return Task.FromResult(1);
                }
            }
            if (lut3dfile != null)
            {
                using var fs = File.OpenRead(lut3dfile);
                var reader = new CubeReader(fs);
                try
                {
                    lut3d = (LookupTable3D)reader.Read();
                }
                catch (InvalidCastException)
                {
                    Console.Error.WriteLine($"{lut3dfile}: Expected 3D LUT but get a 1D LUT.");
                    return Task.FromResult(1);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"failed to load {lut3dfile}: {ex.Message}");
                    return Task.FromResult(1);
                }
            }
            //if (pcc != null)
            //{
            //    pccarr = new float[9];
            //    var tokens = pcc.Split(',', 9);
            //    if (tokens.Length != 9)
            //    {
            //        Console.Error.WriteLine($"Bad PCC matrix: {pcc}");
            //        Environment.ExitCode = 1;
            //        return;
            //    }
            //    for (int i = 0; i < 9; i++)
            //    {
            //        try
            //        {
            //            pccarr[i] = float.Parse(tokens[i]);
            //        }
            //        catch
            //        {
            //            Console.Error.WriteLine($"Bad PCC value: {tokens[i]}");
            //            Environment.ExitCode = 1;
            //            return;
            //        }
            //    }
            //}
            //if (vcgtfile != null)
            //{
            //    using var fs = File.OpenRead(vcgtfile.FullName);
            //    var reader = new CubeReader(fs);
            //    try
            //    {
            //        vcgt = (LookupTable3x1D)reader.Read();
            //    }
            //    catch (InvalidCastException)
            //    {
            //        Console.Error.WriteLine($"{vcgtfile}: Expected 1D LUT but get a 3D LUT.");
            //        Environment.ExitCode = 1;
            //        return;
            //    }
            //}


            plat.SetDegammaShaper(disp, igc);
            plat.SetLookupTable3D(disp, lut3d);
            return Task.FromResult(0);
        }

        private static unsafe LookupTable3D? LoadFactoryCalibration(Preset preset)
        {
            const uint sigACPI = 0x41435049;
            const uint sigDLUT = 0x54554c44;

            var len = GetSystemFirmwareTable(sigACPI, sigDLUT, null, 0);
            var buf = new byte[len];
            fixed (byte* ptr = buf)
            {
                GetSystemFirmwareTable(sigACPI, sigDLUT, ptr, len);
            }

            const int size = 0x6400;
            var offset = preset switch
            {
                Preset.sRGB => 0x6444,
                Preset.DisplayP3 => 0x44,
                _ => throw new ArgumentOutOfRangeException(nameof(preset))
            };
            Span<byte> table;

            try
            {

                table = buf.AsSpan(offset, size);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }

            var result = new LookupTable3D(17);

            var low32s = table.Slice(128);
            var high8s = table.Slice(19780);

            int i = 0;

            for (int r = 0; r < 17; r++)
                for (int g = 0; g < 17; g++)
                    for (int b = 0; b < 17; b++)
                    {
                        var low32 = BinaryPrimitives.ReadUInt32BigEndian(low32s.Slice(i * 4));
                        var high8 = high8s[i];

                        ref var entry = ref result.GetTableEntry(r, g, b);
                        entry.Blue = (low32 & 0xFFF) / 4095.0f;
                        entry.Green = ((low32 >> 12) & 0xFFF) / 4095.0f;
                        entry.Red = (((low32 >> 24)) | ((uint)high8 << 8)) / 4095.0f;

                        i++;
                    }

            return result;

            [DllImport("kernel32")]
            static extern uint GetSystemFirmwareTable(uint FirmwareTableProviderSignature, uint FirmwareTableID, void* pFirmwareTableBuffer, uint BufferSize);
        }
    }
}
