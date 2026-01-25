using QCEDL.NET;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

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

            return MessageLoop(Firehose, Verbose);
        }

        public static byte[]? Read(this QualcommFirehose Firehose, StorageType storageType, uint LUNi, uint sectorSize, uint FirstSector, uint LastSector, bool Verbose, int MaxPayloadSizeToTargetInBytes, Action<int, TimeSpan?>? ProgressUpdateCallback, ProgressUpdater? UpdaterPerSector)
        {
            if (LastSector < FirstSector)
            {
                throw new InvalidDataException();
            }

            using MemoryStream memoryStream = new((int)((LastSector - FirstSector + 1) * sectorSize));

            bool result = Read(Firehose, storageType, LUNi, sectorSize, FirstSector, LastSector, Verbose, MaxPayloadSizeToTargetInBytes, memoryStream, ProgressUpdateCallback, UpdaterPerSector);
            if (!result)
            {
                return null;
            }

            return memoryStream.ToArray();
        }

        public static Response MessageLoop(this QualcommFirehose Firehose, bool Verbose, Action<string>? logCallback = null)
        {
            while (true)
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

                        logCallback?.Invoke(data.Log.Value);
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

        public static bool Read(this QualcommFirehose Firehose, StorageType StorageType, uint LunIndex, uint SectorSize, uint FirstSector, uint LastSector, bool Verbose, int MaxPayloadSizeToTargetInBytes, Stream OutputStream, Action<int, TimeSpan?>? ProgressUpdateCallback, ProgressUpdater? UpdaterPerSector)
        {
            if (LastSector < FirstSector)
            {
                throw new InvalidDataException();
            }

            Debug.WriteLine($"READ: FirstSector: {FirstSector} - LastSector: {LastSector} - SectorSize: {SectorSize}");
            //Console.WriteLine("Read");

            string Command03 = QualcommFirehoseXml.BuildCommandPacket([
                QualcommFirehoseXmlPackets.GetReadPacket(StorageType, LunIndex, SectorSize, FirstSector, LastSector)
            ]);

            Firehose.Serial.SendData(Encoding.UTF8.GetBytes(Command03));

            Response response = MessageLoop(Firehose, Verbose);

            if (!response.RawMode)
            {
                Console.WriteLine("Error: Raw mode not enabled");
                return false;
            }

            {
                ulong sectorsRemaining = LastSector - FirstSector + 1;
                ulong readBufferSize = sectorsRemaining * SectorSize;

                ProgressUpdater? Progress = UpdaterPerSector;
                if (Progress == null && ProgressUpdateCallback != null)
                {
                    Progress = new ProgressUpdater(sectorsRemaining, ProgressUpdateCallback);
                }

                while (sectorsRemaining != 0)
                {
                    if (readBufferSize > (ulong)MaxPayloadSizeToTargetInBytes)
                    {
                        readBufferSize = ((ulong)MaxPayloadSizeToTargetInBytes / SectorSize) * SectorSize;
                    }

                    ulong readCount = 0;
                    while (readCount != readBufferSize)
                    {
                        byte[] readData = Firehose.Serial.GetResponse(null, Length: (uint)(readBufferSize - readCount));
                        OutputStream.Write(readData);
                        readCount += (ulong)readData.LongLength;
                    }

                    ulong sectorCount = readBufferSize / SectorSize;

                    Progress?.IncreaseProgress(sectorCount);

                    sectorsRemaining -= sectorCount;
                    readBufferSize = sectorsRemaining * SectorSize;
                }
            }

            MessageLoop(Firehose, Verbose);

            return true;
        }

        public static bool Reset(this QualcommFirehose Firehose, bool Verbose, PowerValue powerValue = PowerValue.Reset, uint delayInSeconds = 1)
        {
            Console.WriteLine("Rebooting phone");

            string Command03 = QualcommFirehoseXml.BuildCommandPacket([
                QualcommFirehoseXmlPackets.GetPowerPacket(powerValue, delayInSeconds)
            ]);

            Firehose.Serial.SendData(Encoding.UTF8.GetBytes(Command03));

            MessageLoop(Firehose, Verbose);

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

            string? storageInfoJson = null;

            MessageLoop(Firehose, Verbose, (dataLogValue) =>
            {
                if (dataLogValue.StartsWith("INFO: {\"storage_info\": "))
                {
                    storageInfoJson = dataLogValue.Substring(6);
                }
            });

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