using DiscUtils;
using DiscUtils.Containers;
using DiscUtils.Streams;
using QCEDL.NET.PartitionTable;
using Qualcomm.EmergencyDownload.ChipInfo;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;
using Qualcomm.EmergencyDownload.Layers.PBL.Sahara;
using Qualcomm.EmergencyDownload.Transport;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace QCEDL.Client
{
    internal class FirehoseTasks
    {
        private static byte[]? ReadGPTBuffer(QualcommFirehose Firehose, uint sectorSize, StorageType storageType, uint physicalPartition, bool Verbose)
        {
            // Read 6 sectors
            return Firehose.Read(storageType, physicalPartition, sectorSize, 0, 5, Verbose);
        }

        private static GPT? ReadGPT(QualcommFirehose Firehose, uint sectorSize, StorageType storageType, uint physicalPartition, bool Verbose)
        {
            byte[]? GPTLUN = ReadGPTBuffer(Firehose, sectorSize, storageType, physicalPartition, Verbose);

            if (GPTLUN == null)
            {
                return null;
            }

            using MemoryStream stream = new(GPTLUN);
            return GPT.ReadFromStream(stream, (int)sectorSize);
        }

        private static void ReadGPTs(QualcommFirehose Firehose, StorageType storageType, bool Verbose)
        {
            List<Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root> luStorageInfos = [];

            // Figure out the number of LUNs first.
            Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root? mainInfo = Firehose.GetStorageInfo(Verbose, storageType);
            if (mainInfo != null)
            {
                luStorageInfos.Add(mainInfo);

                int totalLuns = mainInfo.storage_info.num_physical;

                // Now figure out the size of each lun
                for (int i = 1; i < totalLuns; i++)
                {
                    Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root? luInfo = Firehose.GetStorageInfo(Verbose, storageType, (uint)i) ?? throw new Exception($"Error in reading LUN {i} for storage info!");
                    luStorageInfos.Add(luInfo);
                }
            }

            for (int i = 0; i < luStorageInfos.Count; i++)
            {
                Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root storageInfo = luStorageInfos[i];

                Logging.Log();
                Logging.Log($"LUN[{i}] Name: {storageInfo.storage_info.prod_name}");
                Logging.Log($"LUN[{i}] Total Blocks: {storageInfo.storage_info.total_blocks}");
                Logging.Log($"LUN[{i}] Block Size: {storageInfo.storage_info.block_size}");
                Logging.Log();

                GPT? GPT = null;

                try
                {
                    GPT = ReadGPT(Firehose, (uint)storageInfo.storage_info.block_size, storageType, (uint)i, Verbose);
                }
                catch (Exception e)
                {
                    Logging.Log(e.ToString());
                }

                if (GPT == null)
                {
                    Logging.Log($"LUN {i}: No GPT found");
                    continue;
                }

                Logging.Log($"LUN {i}:");
                PrintGPTPartitions(GPT);
            }
        }

        private static string ConvertCharArrayToASCIIString(char[] carr)
        {
            return Encoding.ASCII.GetString([.. carr.Select(x => (byte)x)]).Replace("\0", "");
        }

        private static void PrintGPTPartitions(GPT GPT)
        {
            List<(GPTPartition, string)> partitions = [];
            foreach (GPTPartition partition in GPT.Partitions)
            {
                partitions.Add((partition, ConvertCharArrayToASCIIString(partition.Name)));
            }

            int maxLength = partitions.Count > 0 ? partitions.MaxBy(t => t.Item2.Length).Item2.Length : 0;

            foreach ((GPTPartition partition, string name) in partitions)
            {
                string paddedName = name;
                if (paddedName.Length < maxLength)
                {
                    paddedName += new string(' ', maxLength - name.Length);
                }

                Logging.Log($"Name: {paddedName}, Type: {partition.TypeGUID}, ID: {partition.UID}, StartLBA: 0x{partition.FirstLBA:X16}, EndLBA: 0x{partition.LastLBA:X16}, Attributes: 0x{partition.Attributes:X16}, SizeLBA: 0x{(partition.LastLBA - partition.FirstLBA + 1):X16}");
            }
        }

        private static async Task<(QualcommSerial, QualcommFirehose?)> CommonFirehoseLoad(string DevicePath, string ProgrammerPath, bool Verbose)
        {
            Logging.Log();
            Logging.Log("Starting Firehose BootUp");
            Logging.Log();

            // Send and start programmer
            QualcommSerial Serial = new(DevicePath);

            bool PassedHandShake = false;
            bool PassedRKH = false;

            try
            {
                QualcommSahara Sahara = new(Serial);

                Sahara.CommandHandshake();

                PassedHandShake = true;

                byte[][] RKHs = Sahara.GetRKHs();
                PassedRKH = true;

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

                    Logging.Log($"RKH[{i}]: {RKHAsString} ({FriendlyName})");
                }

                byte[] HWID = Sahara.GetHWID();
                HardwareID.ParseHWID(HWID);

                Logging.Log($"Serial Number: {Convert.ToHexString(SN)}");

                Logging.Log();

                using FileStream FileStream = new(ProgrammerPath, FileMode.Open, FileAccess.Read);

                Sahara.SwitchMode(QualcommSaharaMode.ImageTXPending);

                if (!await Sahara.LoadProgrammer(FileStream))
                {
                    Logging.Log("Emergency programmer test failed");
                    return (Serial, null);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            Logging.Log();

            QualcommFirehose Firehose = new(Serial);

            if (PassedHandShake && !PassedRKH)
            {
                Logging.Log("Device successfully performed sahara handshake but failed retrieving RKH information. Assuming device is already booted into a programmer.");
                return (Serial, Firehose);
            }
            else if (!PassedHandShake)
            {
                Logging.Log("Device failed to perform sahara handshake.");
                return (Serial, null);
            }

            if (PassedHandShake && PassedRKH)
            {
                //bool RawMode = false;
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
                                if (Verbose)
                                {
                                    Logging.Log("DEVPRG LOG: " + data.Log.Value);
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

                                Logging.Log(sww.ToString());
                            }
                        }
                    }
                }
                catch (BadConnectionException) { }
            }

            return (Serial, Firehose);
        }

        internal static async Task FirehoseLoad(string DevicePath, string ProgrammerPath, bool Verbose)
        {
            Logging.Log("START FirehoseLoad");

            try
            {
                (QualcommSerial Serial, QualcommFirehose? Firehose) = await CommonFirehoseLoad(DevicePath, ProgrammerPath, Verbose);

                if (Firehose == null)
                {
                    Logging.Log("Loading firehose failed.");
                }
            }
            catch (Exception Ex)
            {
                Logging.Log(Ex.ToString());
            }
            finally
            {
                Logging.Log();
                Logging.Log("END FirehoseLoad");
            }
        }

        internal static async Task FirehoseReset(string DevicePath, string ProgrammerPath, bool Verbose, PowerValue powerValue)
        {
            Logging.Log("START FirehoseReset");

            try
            {
                (QualcommSerial Serial, QualcommFirehose? Firehose) = await CommonFirehoseLoad(DevicePath, ProgrammerPath, Verbose);

                if (Firehose == null)
                {
                    Logging.Log("Loading firehose failed.");
                }
                else
                {
                    if (Firehose.Reset(Verbose, powerValue))
                    {
                        Logging.Log();
                        Logging.Log("Emergency programmer test succeeded");
                    }
                    else
                    {
                        Logging.Log();
                        Logging.Log("Emergency programmer test failed");
                    }
                }
            }
            catch (Exception Ex)
            {
                Logging.Log(Ex.ToString());
            }
            finally
            {
                Logging.Log();
                Logging.Log("END FirehoseReset");
            }
        }

        internal static async Task FirehoseReadStorageInfo(string DevicePath, string ProgrammerPath, StorageType storageType, bool Verbose)
        {
            Logging.Log("START FirehoseReadStorageInfo");

            try
            {
                (QualcommSerial Serial, QualcommFirehose? Firehose) = await CommonFirehoseLoad(DevicePath, ProgrammerPath, Verbose);

                if (Firehose == null)
                {
                    Logging.Log("Loading firehose failed.");
                }
                else
                {
                    Firehose.Configure(storageType, Verbose);

                    ReadGPTs(Firehose, storageType, Verbose);
                }
            }
            catch (Exception Ex)
            {
                Logging.Log(Ex.ToString());
            }
            finally
            {
                Logging.Log();
                Logging.Log("END FirehoseReadStorageInfo");
            }
        }

        internal static async Task FirehoseDumpStorage(string DevicePath, string ProgrammerPath, string VhdxOutputPath, StorageType storageType, bool Verbose)
        {
            Logging.Log("START FirehoseDumpStorage");

            try
            {
                (QualcommSerial Serial, QualcommFirehose? Firehose) = await CommonFirehoseLoad(DevicePath, ProgrammerPath, Verbose);

                if (Firehose == null)
                {
                    Logging.Log("Loading firehose failed.");
                }
                else
                {
                    Firehose.Configure(storageType, Verbose);

                    switch (storageType)
                    {
                        case StorageType.UFS:
                        case StorageType.SPINOR:
                            {
                                DumpUFSDevice(Firehose, VhdxOutputPath, storageType, Verbose);
                                break;
                            }
                        default:
                            {
                                throw new NotImplementedException();
                            }
                    }
                }
            }
            catch (Exception Ex)
            {
                Logging.Log(Ex.ToString());
            }
            finally
            {
                Logging.Log();
                Logging.Log("END FirehoseDumpStorage");
            }
        }

        private static void DumpUFSDevice(QualcommFirehose Firehose, string VhdxOutputPath, StorageType storageType, bool Verbose)
        {
            List<Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root> luStorageInfos = [];

            // Figure out the number of LUNs first.
            Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root? mainInfo = Firehose.GetStorageInfo(Verbose, storageType);
            if (mainInfo != null)
            {
                luStorageInfos.Add(mainInfo);

                int totalLuns = mainInfo.storage_info.num_physical;

                // Now figure out the size of each lun
                for (int i = 1; i < totalLuns; i++)
                {
                    Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root? luInfo = Firehose.GetStorageInfo(Verbose, storageType, (uint)i) ?? throw new Exception($"Error in reading LUN {i} for storage info!");
                    luStorageInfos.Add(luInfo);
                }
            }

            for (int i = 0; i < luStorageInfos.Count; i++)
            {
                Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root storageInfo = luStorageInfos[i];

                Logging.Log();
                Logging.Log($"LUN[{i}] Name: {storageInfo.storage_info.prod_name}");
                Logging.Log($"LUN[{i}] Total Blocks: {storageInfo.storage_info.total_blocks}");
                Logging.Log($"LUN[{i}] Block Size: {storageInfo.storage_info.block_size}");
                Logging.Log();

                LUNStream test = new(Firehose, i, storageType, Verbose);
                ConvertDD2VHD(Path.Combine(VhdxOutputPath, $"LUN{i}.vhdx"), (uint)storageInfo.storage_info.block_size, test, Verbose);
                Logging.Log();
            }
        }

        /// <summary>
        ///     Coverts a raw DD image into a VHD file suitable for FFU imaging.
        /// </summary>
        /// <param name="ddfile">The path to the DD file.</param>
        /// <param name="vhdfile">The path to the output VHD file.</param>
        /// <returns></returns>
        private static void ConvertDD2VHD(string vhdfile, uint SectorSize, Stream inputStream, bool Verbose)
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
            pump.ProgressEvent += (o, e) => { ShowProgress((ulong)e.BytesRead, (ulong)totalBytes, now, Verbose); };

            Logging.Log("Converting RAW to VHDX");
            pump.Run();
            Logging.Log();
        }

        private static void ShowProgress(ulong readBytes, ulong totalBytes, DateTime startTime, bool Verbose)
        {
            DateTime now = DateTime.Now;
            TimeSpan timeSoFar = now - startTime;

            TimeSpan remaining =
                TimeSpan.FromMilliseconds(timeSoFar.TotalMilliseconds / readBytes * (totalBytes - readBytes));

            double speed = Math.Round(readBytes / 1024L / 1024L / timeSoFar.TotalSeconds);

            Logging.Log(
                $"{Logging.GetDISMLikeProgressBar((uint)(readBytes * 100 / totalBytes))} {speed}MB/s {remaining:hh\\:mm\\:ss\\.f}",
                returnLine: Verbose);
        }

        internal static async Task FirehoseDumpStorageLun(string DevicePath, string ProgrammerPath, string VhdxOutputPath, StorageType storageType, bool Verbose, int Lun)
        {
            Logging.Log("START FirehoseDumpStorageLun");

            try
            {
                (QualcommSerial Serial, QualcommFirehose? Firehose) = await CommonFirehoseLoad(DevicePath, ProgrammerPath, Verbose);

                if (Firehose == null)
                {
                    Logging.Log("Loading firehose failed.");
                }
                else
                {
                    Firehose.Configure(storageType, Verbose);

                    switch (storageType)
                    {
                        case StorageType.UFS:
                        case StorageType.SPINOR:
                            {
                                DumpUFSDeviceLun(Firehose, VhdxOutputPath, storageType, Verbose, Lun);
                                break;
                            }
                        default:
                            {
                                throw new NotImplementedException();
                            }
                    }
                }
            }
            catch (Exception Ex)
            {
                Logging.Log(Ex.ToString());
            }
            finally
            {
                Logging.Log();
                Logging.Log("END FirehoseDumpStorageLun");
            }
        }

        private static void DumpUFSDeviceLun(QualcommFirehose Firehose, string VhdxOutputPath, StorageType storageType, bool Verbose, int Lun)
        {
            List<Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root> luStorageInfos = [];

            // Figure out the number of LUNs first.
            Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root? mainInfo = Firehose.GetStorageInfo(Verbose, storageType);
            if (mainInfo != null)
            {
                luStorageInfos.Add(mainInfo);

                int totalLuns = mainInfo.storage_info.num_physical;

                // Now figure out the size of each lun
                for (int i = 1; i < totalLuns; i++)
                {
                    Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root? luInfo = Firehose.GetStorageInfo(Verbose, storageType, (uint)i) ?? throw new Exception($"Error in reading LUN {i} for storage info!");
                    luStorageInfos.Add(luInfo);
                }
            }

            if (luStorageInfos.Count <= Lun)
            {
                Logging.Log("Lun not found.");
                return;
            }

            Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root storageInfo = luStorageInfos[Lun];

            Logging.Log();
            Logging.Log($"LUN[{Lun}] Name: {storageInfo.storage_info.prod_name}");
            Logging.Log($"LUN[{Lun}] Total Blocks: {storageInfo.storage_info.total_blocks}");
            Logging.Log($"LUN[{Lun}] Block Size: {storageInfo.storage_info.block_size}");
            Logging.Log();

            LUNStream test = new(Firehose, Lun, storageType, Verbose);
            ConvertDD2VHD(Path.Combine(VhdxOutputPath, $"LUN{Lun}.vhdx"), (uint)storageInfo.storage_info.block_size, test, Verbose);
            Logging.Log();
        }

    }
}
