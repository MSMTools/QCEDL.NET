using Qualcomm.EmergencyDownload.Layers.APSS.Firehose;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;

namespace QCEDL.Client
{
    internal class EDLSectorReader : SectorBasedReader
    {
        private readonly QualcommFirehose Firehose;
        private readonly int physicalPartitionNumber;
        private readonly Root storageInfo;
        private readonly StorageType storageType;
        private readonly bool Verbose;
        private readonly int MaxPayloadSizeToTargetInBytes;

        public EDLSectorReader(QualcommFirehose Firehose, int physicalPartitionNumber, StorageType storageType, bool Verbose, int MaxPayloadSizeToTargetInBytes, Root luInfo)
        {
            this.Firehose = Firehose;
            this.physicalPartitionNumber = physicalPartitionNumber;
            this.storageType = storageType;

            storageInfo = luInfo;
            this.Verbose = Verbose;
            this.MaxPayloadSizeToTargetInBytes = MaxPayloadSizeToTargetInBytes;
        }

        public EDLSectorReader(QualcommFirehose Firehose, int physicalPartitionNumber, StorageType storageType, bool Verbose, int MaxPayloadSizeToTargetInBytes)
        {
            this.Firehose = Firehose;
            this.physicalPartitionNumber = physicalPartitionNumber;
            this.storageType = storageType;

            Root luInfo = Firehose.GetStorageInfo(Verbose, storageType, (uint)physicalPartitionNumber) ?? throw new Exception($"Error in reading LUN {physicalPartitionNumber} for storage info!");

            storageInfo = luInfo;
            this.Verbose = Verbose;
            this.MaxPayloadSizeToTargetInBytes = MaxPayloadSizeToTargetInBytes;
        }

        public ulong GetSectorSize()
        {
            return (ulong)storageInfo.storage_info.block_size;
        }

        public ulong GetMaxSectors()
        {
            return (ulong)storageInfo.storage_info.total_blocks;
        }

        public byte[] ReadSectors(ulong FirstSector, ulong LastSector)
        {
            byte[]? readBuffer = Firehose.Read(storageType, (uint)physicalPartitionNumber, (uint)storageInfo.storage_info.block_size, (uint)FirstSector, (uint)LastSector, Verbose, MaxPayloadSizeToTargetInBytes);
            return readBuffer ?? throw new Exception();
        }

        public bool ReadSectors(ulong FirstSector, ulong LastSector, Stream outputStream)
        {
            return Firehose.Read(storageType, (uint)physicalPartitionNumber, (uint)storageInfo.storage_info.block_size, (uint)FirstSector, (uint)LastSector, Verbose, MaxPayloadSizeToTargetInBytes, outputStream);
        }
    }
}
