namespace QdcmLoader
{
    internal class DisplayTargetQcom
    {
        public uint QdcmIndex { get; }

        public int QdcmCaps { get; }


        public bool SupportsLookupTable3D { get; }
        public bool SupportsPccMatrix { get; }

        public DisplayTargetQcom(uint qdcmIndex)
        {
            QdcmIndex = qdcmIndex;
            QdcmCaps = DisplayPlatformQcom.Instance.QueryCapabilities(qdcmIndex);
            if ((QdcmCaps & 0x200) != 0) SupportsLookupTable3D = true;
            if ((QdcmCaps & 0x2) != 0) SupportsPccMatrix = true;
        }
    }
}
