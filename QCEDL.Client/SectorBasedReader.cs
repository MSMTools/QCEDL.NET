namespace QCEDL.Client
{
    internal interface SectorBasedReader
    {
        public ulong GetSectorSize();

        public ulong GetMaxSectors();

        public byte[] ReadSectors(ulong FirstSector, ulong LastSector);
    }
}
