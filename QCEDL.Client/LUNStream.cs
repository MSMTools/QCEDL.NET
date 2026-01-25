namespace QCEDL.Client
{
    public class LUNStream(SectorBasedReader sectorBasedReader) : Stream
    {
        private readonly SectorBasedReader sectorBasedReader = sectorBasedReader;

        private long currentPosition = 0;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override long Length => (long)(sectorBasedReader.GetMaxSectors() * sectorBasedReader.GetSectorSize());

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

            long blockSize = (long)sectorBasedReader.GetSectorSize();

            long start = Position;
            long end = start + readBytes;
            long startRemains = start % blockSize;
            long endRemains = end % blockSize;

            long firstSector = (start - startRemains) / blockSize;
            long offsetIntoTheEnd = endRemains == 0 ? 0 : (blockSize - endRemains);
            long lastSector = (end + offsetIntoTheEnd) / blockSize;

            byte[] blocks = sectorBasedReader.ReadSectors((uint)firstSector, (uint)lastSector);

            Array.Copy(blocks, startRemains, buffer, offset, readBytes);
            
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
