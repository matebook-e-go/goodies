using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CS0649  // field never assigned to
#pragma warning disable CS0169  // field never used
#pragma warning disable IDE0051 // unused privates


namespace QdcmLoader
{

    unsafe readonly struct IQDCM
    {
        public readonly struct IQDCMVftable
        {
            /// <summary>
            /// int QueryCaps(IQDCM*, uint32_t index, uint32_t* result);
            /// </summary>
            public readonly delegate* unmanaged[Thiscall]<IQDCM*, uint, int*, int> QueryCaps;

            /// <summary>
            /// int GetValidDisplays(IQDCM*, uint32_t *ids, uint32_t* count);
            /// </summary>
            public readonly delegate* unmanaged[Thiscall]<IQDCM*, uint*, int*, int> GetValidDisplays;

            private readonly IntPtr SetSharpness;
            private readonly IntPtr SetWarmness;
            private readonly IntPtr SetHue;
            private readonly IntPtr SetSaturation;
            private readonly IntPtr SetIntensity;
            private readonly IntPtr SetContrast;

            /// <summary>
            /// int SetPcc(IQDCM*, uint32_t index, float[] matrix3x3);
            /// </summary>
            public readonly delegate* unmanaged[Thiscall]<IQDCM*, uint, float*, int> SetPcc;

        }
        public readonly IQDCMVftable* Vftable;
    }


    public unsafe struct IGCData
    {
        public int Enable;
        public uint* Channel1;
        public uint* Channel2;
        public uint* Channel3;
        public int NumEntries;
    }

    public unsafe struct QDCM3DLUTData
    {
        public int Enable;
        public uint* Channel1;
        public uint* Channel2;
        public uint* Channel3;
        public int NumFlattenEntries;
    }

    unsafe readonly struct IQDCM2
    {
        public readonly struct IQDCM2Vftable
        {
            /// <summary>
            /// int SetIGC(IQDCM2*, uint32_t index, IGCData* igc);
            /// </summary>
            public readonly delegate* unmanaged[Thiscall]<IQDCM2*, uint, IGCData*, int> SetIGC;

            /// <summary>
            /// int Set3DLUT(IQDCM2*, uint32_t index, IGCData* igc);
            /// </summary>
            public readonly delegate* unmanaged[Thiscall]<IQDCM2*, uint, QDCM3DLUTData*, int> Set3DLUT;

            private readonly IntPtr ScreenCapture;
            private readonly IntPtr CaptureRead;
        }
        public readonly IQDCM2Vftable* Vftable;
    }
}
