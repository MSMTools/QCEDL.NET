using System.Text;
using System.Xml.Serialization;
using System.Xml;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;
using System.Diagnostics;

namespace Qualcomm.EmergencyDownload.Layers.APSS.Firehose
{
    public static class QualcommFirehoseCommands
    {
        public static Response Configure(this QualcommFirehose Firehose, StorageType storageType, bool Verbose)
        {
            ulong payloadSize = ulong.MaxValue;

            Response response = Configure2(Firehose, storageType, Verbose, payloadSize);

            if (response.Value == ResponseValue.ACK)
            {
                return response;
            }

            payloadSize = (ulong)response.MaxPayloadSizeToTargetInBytesSupported;

            return Configure2(Firehose, storageType, Verbose, payloadSize);
        }

        public static Response Configure2(this QualcommFirehose Firehose, StorageType storageType, bool Verbose, ulong maxPayloadSizeToTargetInBytes)
        {
            Console.WriteLine("Configuring");

            string Command03 = QualcommFirehoseXml.BuildCommandPacket([
                QualcommFirehoseXmlPackets.GetConfigurePacket(storageType, Verbose, maxPayloadSizeToTargetInBytes, false, 8192, true, false)
            ]);

            Firehose.Serial.SendData(Encoding.UTF8.GetBytes(Command03));

            while (true)
            {
                Data[] datas = Firehose.GetFirehoseResponseDataPayloads();

                foreach (Data data in datas)
                {
                    if (data.Log != null)
                    {
                        if (Verbose)
                        {
                            Console.WriteLine("DEVPRG LOG: " + data.Log.Value);
                        }
                        else
                        {
                            Debug.WriteLine("DEVPRG LOG: " + data.Log.Value);
                        }
                    }
                    else if (data.Response != null)
                    {
                        return data.Response;
                    }
                    else
                    {
                        XmlSerializer xmlSerializer = new(typeof(Data));

                        using StringWriter sww = new();
                        using XmlWriter writer = XmlWriter.Create(sww);

                        xmlSerializer.Serialize(writer, data);

                        Console.WriteLine(sww.ToString());
                    }
                }
            }
        }

        public static byte[]? Read(this QualcommFirehose Firehose, StorageType storageType, uint LUNi, uint sectorSize, uint FirstSector, uint LastSector, bool Verbose, int MaxPayloadSizeToTargetInBytes)
        {
            if (LastSector < FirstSector)
            {
                throw new InvalidDataException();
            }

            using MemoryStream memoryStream = new((int)((LastSector - FirstSector + 1) * sectorSize));

            bool result = Read(Firehose, storageType, LUNi, sectorSize, FirstSector, LastSector, Verbose, MaxPayloadSizeToTargetInBytes, memoryStream);
            if (!result)
            {
                return null;
            }

            return memoryStream.ToArray();
        }

        public static bool Read(this QualcommFirehose Firehose, StorageType storageType, uint LUNi, uint sectorSize, uint FirstSector, uint LastSector, bool Verbose, int MaxPayloadSizeToTargetInBytes, Stream outputStream)
        {
            if (LastSector < FirstSector)
            {
                throw new InvalidDataException();
            }

            Debug.WriteLine($"READ: FirstSector: {FirstSector} - LastSector: {LastSector} - SectorSize: {sectorSize}");
            //Console.WriteLine("Read");

            string Command03 = QualcommFirehoseXml.BuildCommandPacket([
                QualcommFirehoseXmlPackets.GetReadPacket(storageType, LUNi, sectorSize, FirstSector, LastSector)
            ]);

            Firehose.Serial.SendData(Encoding.UTF8.GetBytes(Command03));

            bool RawMode = false;
            bool GotResponse = false;

            while (!GotResponse)
            {
                Data[] datas = Firehose.GetFirehoseResponseDataPayloads(true);

                foreach (Data data in datas)
                {
                    if (data.Log != null)
                    {
                        if (Verbose)
                        {
                            Console.WriteLine("DEVPRG LOG: " + data.Log.Value);
                        }
                        else
                        {
                            Debug.WriteLine("DEVPRG LOG: " + data.Log.Value);
                        }
                    }
                    else if (data.Response != null)
                    {
                        if (data.Response.RawMode)
                        {
                            RawMode = true;
                        }

                        GotResponse = true;
                    }
                    else
                    {
                        XmlSerializer xmlSerializer = new(typeof(Data));

                        using StringWriter sww = new();
                        using XmlWriter writer = XmlWriter.Create(sww);

                        xmlSerializer.Serialize(writer, data);

                        Console.WriteLine(sww.ToString());
                    }
                }
            }

            if (!RawMode)
            {
                Console.WriteLine("Error: Raw mode not enabled");
                return false;
            }

            {
                ulong sectorsRemaining = LastSector - FirstSector + 1;
                ulong readBufferSize = sectorsRemaining * sectorSize;

                while (sectorsRemaining != 0)
                {
                    if (readBufferSize > (ulong)MaxPayloadSizeToTargetInBytes)
                    {
                        readBufferSize = ((ulong)MaxPayloadSizeToTargetInBytes / sectorSize) * sectorSize;
                    }

                    ulong readCount = 0;
                    while (readCount != readBufferSize)
                    {
                        byte[] readData = Firehose.Serial.GetResponse(null, Length: (uint)(readBufferSize - readCount));
                        outputStream.Write(readData);
                        readCount += (ulong)readData.LongLength;
                    }

                    ulong sectorCount = readBufferSize / sectorSize;
                    sectorsRemaining -= sectorCount;
                    readBufferSize = sectorsRemaining * sectorSize;
                }
            }

            GotResponse = false;

            while (!GotResponse)
            {
                Data[] datas = Firehose.GetFirehoseResponseDataPayloads();

                foreach (Data data in datas)
                {
                    if (data.Log != null)
                    {
                        if (Verbose)
                        {
                            Console.WriteLine("DEVPRG LOG: " + data.Log.Value);
                        }
                        else
                        {
                            Debug.WriteLine("DEVPRG LOG: " + data.Log.Value);
                        }
                    }
                    else if (data.Response != null)
                    {
                        GotResponse = true;
                    }
                    else
                    {
                        XmlSerializer xmlSerializer = new(typeof(Data));

                        using StringWriter sww = new();
                        using XmlWriter writer = XmlWriter.Create(sww);

                        xmlSerializer.Serialize(writer, data);

                        Console.WriteLine(sww.ToString());
                    }
                }
            }

            return true;
        }

        public static bool Reset(this QualcommFirehose Firehose, bool Verbose, PowerValue powerValue = PowerValue.Reset, uint delayInSeconds = 1)
        {
            Console.WriteLine("Rebooting phone");

            string Command03 = QualcommFirehoseXml.BuildCommandPacket([
                QualcommFirehoseXmlPackets.GetPowerPacket(powerValue, delayInSeconds)
            ]);

            Firehose.Serial.SendData(Encoding.UTF8.GetBytes(Command03));

            //bool RawMode = false;
            bool GotResponse = false;

            while (!GotResponse)
            {
                Data[] datas = Firehose.GetFirehoseResponseDataPayloads();

                foreach (Data data in datas)
                {
                    if (data.Log != null)
                    {
                        if (Verbose)
                        {
                            Console.WriteLine("DEVPRG LOG: " + data.Log.Value);
                        }
                        else
                        {
                            Debug.WriteLine("DEVPRG LOG: " + data.Log.Value);
                        }
                    }
                    else if (data.Response != null)
                    {
                        /*if (data.Response.RawMode)
                        {
                            RawMode = true;
                        }*/

                        GotResponse = true;
                    }
                    else
                    {
                        XmlSerializer xmlSerializer = new(typeof(Data));

                        using StringWriter sww = new();
                        using XmlWriter writer = XmlWriter.Create(sww);

                        xmlSerializer.Serialize(writer, data);

                        Console.WriteLine(sww.ToString());
                    }
                }
            }

            // Workaround for problem
            // SerialPort is sometimes not disposed correctly when the device is already removed.
            // So explicitly dispose here
            Firehose.Serial.Close();

            return true;
        }

        public static JSON.StorageInfo.Root? GetStorageInfo(this QualcommFirehose Firehose, bool Verbose, StorageType storageType = StorageType.UFS, uint PhysicalPartitionNumber = 0)
        {
            Console.WriteLine("Getting Storage Info");

            string Command03 = QualcommFirehoseXml.BuildCommandPacket([
                QualcommFirehoseXmlPackets.GetStorageInfoPacket(storageType, PhysicalPartitionNumber)
            ]);

            Firehose.Serial.SendData(Encoding.UTF8.GetBytes(Command03));

            bool GotResponse = false;

            string? storageInfoJson = null;

            while (!GotResponse)
            {
                Data[] datas = Firehose.GetFirehoseResponseDataPayloads();

                foreach (Data data in datas)
                {
                    if (data.Log != null)
                    {
                        if (data.Log.Value.StartsWith("INFO: {\"storage_info\": "))
                        {
                            storageInfoJson = data.Log.Value.Substring(6);
                        }
                        
                        if (Verbose)
                        {
                            Console.WriteLine("DEVPRG LOG: " + data.Log.Value);
                        }
                        else
                        {
                            Debug.WriteLine("DEVPRG LOG: " + data.Log.Value);
                        }
                    }
                    else if (data.Response != null)
                    {
                        GotResponse = true;
                    }
                    else
                    {
                        XmlSerializer xmlSerializer = new(typeof(Data));

                        using StringWriter sww = new();
                        using XmlWriter writer = XmlWriter.Create(sww);

                        xmlSerializer.Serialize(writer, data);

                        Console.WriteLine(sww.ToString());
                    }
                }
            }

            if (storageInfoJson == null)
            {
                return null;
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<JSON.StorageInfo.Root>(storageInfoJson);
            }
            catch
            {
                return null;
            }
        }
    }
}