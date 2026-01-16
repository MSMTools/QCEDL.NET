using Qualcomm.EmergencyDownload.Transport;

namespace Qualcomm.EmergencyDownload.Programmer
{
    internal class ProgrammerConverter
    {
        internal static byte[]? ParseAsHexFile(string FilePath)
        {
            byte[]? Result = null;

            try
            {
                string[] Lines = File.ReadAllLines(FilePath);
                byte[]? Buffer = null;
                int BufferSize = 0;

                foreach (string Line in Lines)
                {
                    if (string.IsNullOrEmpty(Line))
                    {
                        continue;
                    }

                    if (Line[0] != ':')
                    {
                        throw new BadImageFormatException();
                    }

                    byte[] LineBytes = Converter.ConvertStringToHex(Line[1..]);

                    if ((LineBytes[0] + 5) != LineBytes.Length)
                    {
                        throw new BadImageFormatException();
                    }

                    Buffer ??= new byte[0x40000];

                    if (LineBytes[3] == 0) // This is mem data
                    {
                        System.Buffer.BlockCopy(LineBytes, 4, Buffer, BufferSize, LineBytes[0]);
                        BufferSize += LineBytes[0];
                    }
                }

                if (BufferSize != 0)
                {
                    Result = new byte[BufferSize];
                    System.Buffer.BlockCopy(Buffer!, 0, Result, 0, BufferSize);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return Result;
        }
    }
}
