using Qualcomm.EmergencyDownload.Layers.APSS.Firehose;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;

namespace QCEDL.Client
{
    public class PartStream : Stream
    {
        private readonly QualcommFirehose Firehose;
        private readonly int physicalPartitionNumber;
        private readonly Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root storageInfo;
        private readonly StorageType storageType;
        private long currentPosition;
        private readonly bool Verbose;
        private readonly ulong firstSector;
        private readonly ulong lastSector;

        public PartStream(QualcommFirehose Firehose, int physicalPartitionNumber, StorageType storageType, bool Verbose, ulong firstSector, ulong lastSector)
        {
            this.Firehose = Firehose;
            this.physicalPartitionNumber = physicalPartitionNumber;
            this.storageType = storageType;

            Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root luInfo = Firehose.GetStorageInfo(Verbose, storageType, (uint)physicalPartitionNumber) ?? throw new Exception($"Error in reading LUN {physicalPartitionNumber} for storage info!");

            storageInfo = luInfo;
            currentPosition = 0;
            this.Verbose = Verbose;

            this.firstSector = firstSector;
            this.lastSector = lastSector;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override long Length => (long)(lastSector - firstSector + 1) * (long)storageInfo.storage_info.block_size;

        public override long Position
        {
            get => currentPosition;
            set
            {
                if (currentPosition < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                currentPosition = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (offset + count > buffer.Length)
            {
                throw new ArgumentException("The sum of offset and count is greater than the buffer length.");
            }

            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            // Workaround for malformed MBRs
            if (Position >= Length)
            {
                return count;
            }

            long readBytes = count;

            if (Position + readBytes > Length)
            {
                readBytes = (int)(Length - Position);
            }

            long blockSize = storageInfo.storage_info.block_size;

            // The number of bytes that do not line up with the size of blocks (blockSize) at the beginning
            long overflowBlockStartByteCount = Position % blockSize;

            // The number of bytes that do not line up with the size of blocks (blockSize) at the end
            long overflowBlockEndByteCount = (Position + readBytes) % blockSize;

            // The position to start reading from, aligned to the size of blocks (blockSize)
            long noOverflowBlockStartByteCount = Position - overflowBlockStartByteCount;

            // The number of extra bytes to read at the start
            long extraStartBytes = overflowBlockStartByteCount == 0 ? 0 : blockSize - overflowBlockStartByteCount;

            // The number of extra bytes to read at the end
            long extraEndBytes = overflowBlockEndByteCount == 0 ? 0 : blockSize - overflowBlockEndByteCount;

            // The position to end reading from, aligned to the size of blocks (blockSize) (excluding)
            long noOverflowBlockEndByteCount = Position + readBytes + extraEndBytes;

            // The first block we have to read
            long startBlockIndex = noOverflowBlockStartByteCount / blockSize;

            // The last block we have to read (excluding)
            long endBlockIndex = noOverflowBlockEndByteCount / blockSize;

            byte[]? blocksOnDevice = Firehose.Read(storageType, (uint)physicalPartitionNumber, (uint)blockSize, (uint)firstSector + (uint)startBlockIndex, (uint)firstSector + (uint)endBlockIndex - 1, Verbose);

            if (blocksOnDevice != null)
            {
                Array.Copy(blocksOnDevice, overflowBlockStartByteCount, buffer, offset, readBytes);
            }

            // Go through every block one by one
            /*for (long currentBlock = startBlockIndex; currentBlock < endBlockIndex; currentBlock++)
            {
                bool isFirstBlock = currentBlock == startBlockIndex;
                bool isLastBlock = currentBlock == endBlockIndex - 1;

                long bytesToRead = blockSize;
                long bufferDestination = extraStartBytes + (currentBlock - startBlockIndex - 1) * blockSize;

                if (isFirstBlock)
                {
                    bytesToRead = extraStartBytes;
                    bufferDestination = 0;
                }

                if (isLastBlock)
                {
                    bytesToRead -= extraEndBytes;
                }

                // Read one sector (sector currentBlock)
                byte[] blockOnDevice = Firehose.Read(storageType, (uint)physicalPartitionNumber, (uint)blockSize, (uint)currentBlock, (uint)currentBlock);

                // TODO: Check me!
                Array.Copy(blockOnDevice, overflowBlockStartByteCount, buffer, offset + (int)bufferDestination, (int)bytesToRead);
            }*/

            Position += readBytes;

            return (int)readBytes;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    {
                        Position = offset;
                        break;
                    }
                case SeekOrigin.Current:
                    {
                        Position += offset;
                        break;
                    }
                case SeekOrigin.End:
                    {
                        Position = Length + offset;
                        break;
                    }
                default:
                    {
                        throw new ArgumentException(null, nameof(origin));
                    }
            }

            return Position;
        }


        public override bool CanWrite => false;

        public override void Flush()
        {
            // Nothing to do here
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
