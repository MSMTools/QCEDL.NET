namespace QCEDL.Client
{
    internal interface SectorBasedReader
    {
        public ulong GetSectorSize();

        public ulong GetMaxSectors();

        public byte[] ReadSectors(ulong FirstSector, ulong LastSector);

        public bool ReadSectors(ulong FirstSector, ulong LastSector, Stream outputStream);
    }
}
