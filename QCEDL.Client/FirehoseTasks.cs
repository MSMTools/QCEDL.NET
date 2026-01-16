using DiscUtils;
using DiscUtils.Containers;
using DiscUtils.Streams;
using QCEDL.NET.PartitionTable;
using Qualcomm.EmergencyDownload.ChipInfo;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;
using Qualcomm.EmergencyDownload.Layers.PBL.Sahara;
using Qualcomm.EmergencyDownload.Transport;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace QCEDL.Client
{
    internal class FirehoseTasks
    {
        private static byte[] ReadGPTBuffer(QualcommFirehose Firehose, uint sectorSize, StorageType storageType, uint physicalPartition)
        {
            // Read 6 sectors
            return Firehose.Read(storageType, physicalPartition, sectorSize, 0, 5);
        }

        private static GPT ReadGPT(QualcommFirehose Firehose, uint sectorSize, StorageType storageType, uint physicalPartition)
        {
            byte[] GPTLUN = ReadGPTBuffer(Firehose, sectorSize, storageType, physicalPartition);

            if (GPTLUN == null)
            {
                return null;
            }

            using MemoryStream stream = new(GPTLUN);
            return GPT.ReadFromStream(stream, (int)sectorSize);
        }

        private static void ReadGPTs(QualcommFirehose Firehose, StorageType storageType)
        {
            List<Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root> luStorageInfos = [];

            // Figure out the number of LUNs first.
            Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root? mainInfo = Firehose.GetStorageInfo(storageType);
            if (mainInfo != null)
            {
                int totalLuns = mainInfo.storage_info.num_physical;
                if (totalLuns == 0)
                {
                    totalLuns = 1;
                }

                // Now figure out the size of each lun
                for (int i = 0; i < totalLuns; i++)
                {
                    Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root? luInfo = Firehose.GetStorageInfo(storageType, (uint)i) ?? throw new Exception($"Error in reading LUN {i} for storage info!");
                    luStorageInfos.Add(luInfo);
                }
            }

            for (int i = 0; i < luStorageInfos.Count; i++)
            {
                Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root storageInfo = luStorageInfos[i];

                Console.WriteLine($"LUN[{i}] Name: {storageInfo.storage_info.prod_name}");
                Console.WriteLine($"LUN[{i}] Total Blocks: {storageInfo.storage_info.total_blocks}");
                Console.WriteLine($"LUN[{i}] Block Size: {storageInfo.storage_info.block_size}");
                Console.WriteLine();

                GPT GPT = null;

                try
                {
                    GPT = ReadGPT(Firehose, (uint)storageInfo.storage_info.block_size, storageType, (uint)i);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                if (GPT == null)
                {
                    Console.WriteLine($"LUN {i}: No GPT found");
                    continue;
                }

                Console.WriteLine($"LUN {i}:");
                PrintGPTPartitions(GPT);
            }
        }

        private static void PrintGPTPartitions(GPT GPT)
        {
            foreach (GPTPartition partition in GPT.Partitions)
            {
                Console.WriteLine($"Name: {Encoding.ASCII.GetString([.. partition.Name.Select(x => (byte)x)])}, Type: {partition.TypeGUID}, ID: {partition.UID}, StartLBA: {partition.FirstLBA}, EndLBA: {partition.LastLBA}");
            }
        }

        private static async Task<(QualcommSerial, QualcommFirehose?)> CommonFirehoseLoad(string DevicePath, string ProgrammerPath)
        {
            Console.WriteLine();
            Console.WriteLine("Starting Firehose BootUp");
            Console.WriteLine();

            // Send and start programmer
            QualcommSerial Serial = new(DevicePath);

            try
            {
                QualcommSahara Sahara = new(Serial);

                Sahara.CommandHandshake();

                byte[][] RKHs = Sahara.GetRKHs();
                byte[] SN = Sahara.GetSerialNumber();

                for (int i = 0; i < RKHs.Length; i++)
                {
                    byte[] RKH = RKHs[i];
                    string RKHAsString = Convert.ToHexString(RKH);
                    string FriendlyName = "Unknown";

                    foreach (KeyValuePair<string, string> element in KnownPKData.KnownOEMPKHashes)
                    {
                        if (element.Value == RKHAsString)
                        {
                            FriendlyName = element.Key;
                            break;
                        }
                    }

                    Console.WriteLine($"RKH[{i}]: {RKHAsString} ({FriendlyName})");
                }

                byte[] HWID = Sahara.GetHWID();
                HardwareID.ParseHWID(HWID);

                Console.WriteLine($"Serial Number: {Convert.ToHexString(SN)}");

                Sahara.SwitchMode(QualcommSaharaMode.ImageTXPending);

                Console.WriteLine();

                if (!await Sahara.LoadProgrammer(ProgrammerPath))
                {
                    Console.WriteLine("Emergency programmer test failed");
                    return (Serial, null);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.WriteLine();

            QualcommFirehose Firehose = new(Serial);

            bool RawMode = false;
            bool GotResponse = false;

            try
            {
                while (!GotResponse)
                {
                    Data[] datas = Firehose.GetFirehoseResponseDataPayloads();

                    foreach (Data data in datas)
                    {
                        if (data.Log != null)
                        {
                            Debug.WriteLine("DEVPRG LOG: " + data.Log.Value);
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
            }
            catch (BadConnectionException) { }

            return (Serial, Firehose);
        }

        internal static async Task FirehoseLoad(string DevicePath, string ProgrammerPath)
        {
            Console.WriteLine("START FirehoseLoad");

            try
            {
                await CommonFirehoseLoad(DevicePath, ProgrammerPath);
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("END FirehoseLoad");
            }
        }

        internal static async Task FirehoseReset(string DevicePath, string ProgrammerPath)
        {
            Console.WriteLine("START FirehoseReset");

            try
            {
                (QualcommSerial Serial, QualcommFirehose Firehose) = await CommonFirehoseLoad(DevicePath, ProgrammerPath);

                if (Firehose.Reset())
                {
                    Console.WriteLine();
                    Console.WriteLine("Emergency programmer test succeeded");
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Emergency programmer test failed");
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("END FirehoseReset");
            }
        }

        internal static async Task FirehoseReadStorageInfo(string DevicePath, string ProgrammerPath, StorageType storageType)
        {
            Console.WriteLine("START FirehoseReadStorageInfo");

            try
            {
                (QualcommSerial Serial, QualcommFirehose Firehose) = await CommonFirehoseLoad(DevicePath, ProgrammerPath);

                Firehose.Configure(storageType);
                Firehose.GetStorageInfo(storageType);

                ReadGPTs(Firehose, storageType);
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("END FirehoseReadStorageInfo");
            }
        }

        internal static async Task FirehoseDumpStorage(string DevicePath, string ProgrammerPath, string VhdxOutputPath, StorageType storageType)
        {
            Console.WriteLine("START FirehoseReadStorageInfo");

            try
            {
                (QualcommSerial Serial, QualcommFirehose Firehose) = await CommonFirehoseLoad(DevicePath, ProgrammerPath);

                Firehose.Configure(storageType);
                Firehose.GetStorageInfo(storageType);

                switch (storageType)
                {
                    case StorageType.UFS:
                    case StorageType.SPINOR:
                        {
                            DumpUFSDevice(Firehose, VhdxOutputPath, storageType);
                            break;
                        }
                    default:
                        {
                            throw new NotImplementedException();
                        }
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("END FirehoseReadStorageInfo");
            }
        }

        private static void DumpUFSDevice(QualcommFirehose Firehose, string VhdxOutputPath, StorageType storageType)
        {
            List<Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root> luStorageInfos = [];

            // Figure out the number of LUNs first.
            Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root? mainInfo = Firehose.GetStorageInfo(storageType);
            if (mainInfo != null)
            {
                int totalLuns = mainInfo.storage_info.num_physical;
                if (totalLuns == 0)
                {
                    totalLuns = 1;
                }

                // Now figure out the size of each lun
                for (int i = 0; i < totalLuns; i++)
                {
                    Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root? luInfo = Firehose.GetStorageInfo(storageType, (uint)i) ?? throw new Exception($"Error in reading LUN {i} for storage info!");
                    luStorageInfos.Add(luInfo);
                }
            }

            for (int i = 0; i < luStorageInfos.Count; i++)
            {
                Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root storageInfo = luStorageInfos[i];

                Console.WriteLine($"LUN[{i}] Name: {storageInfo.storage_info.prod_name}");
                Console.WriteLine($"LUN[{i}] Total Blocks: {storageInfo.storage_info.total_blocks}");
                Console.WriteLine($"LUN[{i}] Block Size: {storageInfo.storage_info.block_size}");
                Console.WriteLine();

                LUNStream test = new(Firehose, i, storageType);
                ConvertDD2VHD(Path.Combine(VhdxOutputPath, $"LUN{i}.vhdx"), (uint)storageInfo.storage_info.block_size, test);
                Console.WriteLine();
            }
        }

        /// <summary>
        ///     Coverts a raw DD image into a VHD file suitable for FFU imaging.
        /// </summary>
        /// <param name="ddfile">The path to the DD file.</param>
        /// <param name="vhdfile">The path to the output VHD file.</param>
        /// <returns></returns>
        private static void ConvertDD2VHD(string vhdfile, uint SectorSize, Stream inputStream)
        {
            SetupHelper.SetupContainers();

            using DiscUtils.Raw.Disk inDisk = new(inputStream, Ownership.Dispose);

            long diskCapacity = inputStream.Length;
            using Stream fs = new FileStream(vhdfile, FileMode.CreateNew, FileAccess.ReadWrite);
            using DiscUtils.Vhdx.Disk outDisk = DiscUtils.Vhdx.Disk.InitializeDynamic(fs, Ownership.None, diskCapacity, Geometry.FromCapacity(diskCapacity, (int)SectorSize));
            SparseStream contentStream = inDisk.Content;

            StreamPump pump = new()
            {
                InputStream = contentStream,
                OutputStream = outDisk.Content,
                SparseCopy = true,
                SparseChunkSize = (int)SectorSize,
                BufferSize = (int)SectorSize * 256 // Max 24 sectors at a time
            };

            long totalBytes = contentStream.Length;

            DateTime now = DateTime.Now;
            pump.ProgressEvent += (o, e) => { ShowProgress((ulong)e.BytesRead, (ulong)totalBytes, now); };

            Logging.Log("Converting RAW to VHDX");
            pump.Run();
            Console.WriteLine();
        }

        private static void ShowProgress(ulong readBytes, ulong totalBytes, DateTime startTime)
        {
            DateTime now = DateTime.Now;
            TimeSpan timeSoFar = now - startTime;

            TimeSpan remaining =
                TimeSpan.FromMilliseconds(timeSoFar.TotalMilliseconds / readBytes * (totalBytes - readBytes));

            double speed = Math.Round(readBytes / 1024L / 1024L / timeSoFar.TotalSeconds);

            Logging.Log(
                $"{Logging.GetDISMLikeProgressBar((uint)(readBytes * 100 / totalBytes))} {speed}MB/s {remaining:hh\\:mm\\:ss\\.f}",
                returnLine: false);
        }
    }
}
