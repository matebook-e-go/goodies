using System.Runtime.InteropServices;

namespace QdcmLoader
{
    unsafe class DisplayPlatformQcom
    {
        [DllImport("qdcmlib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IQDCM* Create_QDCMLibrary();

        [DllImport("qdcmlib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IQDCM2* Create_QDCMLibrary2();

        [DllImport("qdcmlib", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Destroy_QDCMLibrary(IQDCM* obj);

        [DllImport("qdcmlib", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Destroy_QDCMLibrary2(IQDCM2* obj);

        private static Lazy<DisplayPlatformQcom> _instanceHolder = new(() => new DisplayPlatformQcom());

        public static DisplayPlatformQcom Instance => _instanceHolder.Value;

        public int MaxLookupTableSize => 17;

        private IQDCM* factory1;
        private IQDCM2* factory2;

        private DisplayPlatformQcom()
        {
            factory1 = Create_QDCMLibrary();
            factory2 = Create_QDCMLibrary2();
            if (factory1 == null || factory2 == null)
            {
                throw new COMException("failed to create QDCM factory");
            }
        }

        ~DisplayPlatformQcom()
        {
            if (factory1 != null)
            {
                Destroy_QDCMLibrary(factory1);
            }
            if (factory2 != null)
            {
                Destroy_QDCMLibrary2(factory2);
            }
        }

        public IEnumerable<DisplayTargetQcom> EnumDisplays()
        {
            var buf = new uint[16];
            int count = 0;
            fixed (uint* ptr = buf)
            {
                factory1->Vftable->GetValidDisplays(factory1, ptr, &count);
                if (count >= buf.Length)
                {
                    buf = new uint[count];
                }
                factory1->Vftable->GetValidDisplays(factory1, ptr, &count);
            }
            return Enumerable.Range(0, count).Select(x => new DisplayTargetQcom(buf[x]));
        }

        internal unsafe int QueryCapabilities(uint index)
        {
            int caps = 0;
            var result = factory1->Vftable->QueryCaps(factory1, index, &caps);
            return caps;
        }

        public bool SetMatrix(DisplayTargetQcom id, float[] mat)
        {
            if (mat.Length != 9) throw new ArgumentException("matrix size", nameof(mat));
            if (id is DisplayTargetQcom qid)
            {
                fixed (float* ptr = &mat[0])
                {
                    var result = factory1->Vftable->SetPcc(factory1, qid.QdcmIndex, ptr);
                    return result != 0;
                }
            }
            return false;
        }

        public bool SetDegammaShaper(DisplayTargetQcom id, LookupTable3x1D? degamma)
        {
            if (id is not DisplayTargetQcom qid)
            {
                return false;
            }
            if (degamma == null)
            {
                var data = new IGCData();
                data.Enable = 0;
                var result = factory2->Vftable->SetIGC(factory2, qid.QdcmIndex, &data);
                return result != 0;
            }

            if (degamma.Size != 257)
            {
                degamma = degamma.Resize(257);
            }

            var buf1 = new uint[257];
            var buf2 = new uint[257];
            var buf3 = new uint[257];
            for (int i = 0; i < 257; i++)
            {
                var rgb = degamma.GetTableEntry(i, i, i);
                buf1[i] = (uint)Math.Round(rgb.Red * 4095.0f);
                buf2[i] = (uint)Math.Round(rgb.Green * 4095.0f);
                buf3[i] = (uint)Math.Round(rgb.Blue * 4095.0f);
            }
            //buf1[256] = 4095;
            //buf2[256] = 4095;
            //buf3[256] = 4095;
            fixed (uint* ptr1 = buf1, ptr2 = buf2, ptr3 = buf3)
            {
                var data = new IGCData();
                data.Enable = 1;
                data.Channel1 = ptr1;
                data.Channel2 = ptr2;
                data.Channel3 = ptr3;
                data.NumEntries = 257;
                var result = factory2->Vftable->SetIGC(factory2, qid.QdcmIndex, &data);
                return result != 0;
            }
        }

        public bool SetLookupTable3D(DisplayTargetQcom id, LookupTable3D? lut3d)
        {
            if (id is not DisplayTargetQcom qid)
            {
                return false;
            }
            if (lut3d == null)
            {
                QDCM3DLUTData data = new QDCM3DLUTData
                {
                    Enable = 0
                };

                var result = factory2->Vftable->Set3DLUT(factory2, qid.QdcmIndex, &data);
                return result != 0;
            }

            if (lut3d.Size != MaxLookupTableSize)
            {
                lut3d = lut3d.Resample(MaxLookupTableSize);
            }

            var size1 = lut3d.Size;
            var size2 = size1 * size1;
            var size3 = size2 * size1;
            var channelRed = new uint[size3];
            var channelGreen = new uint[size3];
            var channelBlue = new uint[size3];

            for (var redin = 0; redin < size1; redin++)
                for (var greenin = 0; greenin < size1; greenin++)
                    for (var bluein = 0; bluein < size1; bluein++)
                    {
                        var entry = lut3d.GetTableEntry(redin, greenin, bluein);
                        var index = bluein * size2 + greenin * size1 + redin;
                        channelRed[index] = (uint)Math.Clamp(Math.Round(entry.Red * 4095.0), 0, 4095);
                        channelGreen[index] = (uint)Math.Clamp(Math.Round(entry.Green * 4095.0), 0, 4095);
                        channelBlue[index] = (uint)Math.Clamp(Math.Round(entry.Blue * 4095.0), 0, 4095);
                    }

            fixed (uint* rptr = channelRed, gptr = channelGreen, bptr = channelBlue)
            {
                QDCM3DLUTData data = new QDCM3DLUTData
                {
                    Enable = 1,
                    Channel1 = rptr,
                    Channel2 = gptr,
                    Channel3 = bptr,
                    NumFlattenEntries = size3
                };

                var result = factory2->Vftable->Set3DLUT(factory2, qid.QdcmIndex, &data);
                return result != 0;
            }
        }
    }
}
