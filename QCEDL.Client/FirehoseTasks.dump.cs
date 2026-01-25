using DiscUtils;
using DiscUtils.Containers;
using DiscUtils.Streams;
using QCEDL.NET.PartitionTable;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;
using Qualcomm.EmergencyDownload.Transport;

namespace QCEDL.Client
{
    internal partial class FirehoseTasks
    {
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
                    ConfigureResponse response = Firehose.Configure(storageType, Verbose);

                    switch (storageType)
                    {
                        case StorageType.UFS:
                        case StorageType.SPINOR:
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

                                    LUNStream test = new(Firehose, i, storageType, Verbose, response.MaxPayloadSizeToTargetInBytes, storageInfo);
                                    ConvertDD2VHD(Path.Combine(VhdxOutputPath, $"LUN{i}.vhdx"), (uint)storageInfo.storage_info.block_size, test, Verbose);
                                    Logging.Log();
                                }
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
                    ConfigureResponse response = Firehose.Configure(storageType, Verbose);

                    switch (storageType)
                    {
                        case StorageType.UFS:
                        case StorageType.SPINOR:
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

                                LUNStream test = new(Firehose, Lun, storageType, Verbose, response.MaxPayloadSizeToTargetInBytes, storageInfo);
                                ConvertDD2VHD(Path.Combine(VhdxOutputPath, $"LUN{Lun}.vhdx"), (uint)storageInfo.storage_info.block_size, test, Verbose);
                                Logging.Log();
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

        internal static async Task FirehoseDumpStoragePartitionByUID(string DevicePath, string ProgrammerPath, string VhdxOutputPath, StorageType storageType, bool Verbose, Guid Uid)
        {
            Logging.Log("START FirehoseDumpStoragePartitionByUID");

            try
            {
                (QualcommSerial Serial, QualcommFirehose? Firehose) = await CommonFirehoseLoad(DevicePath, ProgrammerPath, Verbose);

                if (Firehose == null)
                {
                    Logging.Log("Loading firehose failed.");
                }
                else
                {
                    ConfigureResponse response = Firehose.Configure(storageType, Verbose);

                    switch (storageType)
                    {
                        case StorageType.UFS:
                        case StorageType.SPINOR:
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

                                bool PartitionFound = false;

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
                                        GPT = ReadGPT(Firehose, (uint)storageInfo.storage_info.block_size, storageType, (uint)i, Verbose, response.MaxPayloadSizeToTargetInBytes);
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

                                    foreach (GPTPartition partition in GPT.Partitions)
                                    {
                                        if (partition.UID == Uid)
                                        {
                                            if (File.Exists(VhdxOutputPath))
                                            {
                                                throw new Exception("File already exists");
                                            }

                                            FileStream fileStream = File.Create(VhdxOutputPath);

                                            PartStream partStream = new(Firehose, i, storageType, Verbose, partition.FirstLBA, partition.LastLBA, response.MaxPayloadSizeToTargetInBytes, storageInfo);

                                            long diskCapacity = partStream.Length;
                                            fileStream.SetLength(diskCapacity);


                                            StreamPump pump = new()
                                            {
                                                InputStream = partStream,
                                                OutputStream = fileStream,
                                                SparseCopy = true,
                                                SparseChunkSize = storageInfo.storage_info.block_size,
                                                BufferSize = storageInfo.storage_info.block_size * 256 // Max 24 sectors at a time
                                            };

                                            long totalBytes = partStream.Length;

                                            DateTime now = DateTime.Now;
                                            pump.ProgressEvent += (o, e) => { ShowProgress((ulong)e.BytesRead, (ulong)totalBytes, now, Verbose); };

                                            Logging.Log("Converting RAW to RAW");
                                            pump.Run();
                                            Logging.Log();

                                            PartitionFound = true;
                                            break;
                                        }
                                    }

                                    if (PartitionFound)
                                    {
                                        break;
                                    }
                                }

                                if (!PartitionFound)
                                {
                                    Logging.Log("Partition UID not found.");
                                }
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
                Logging.Log("END FirehoseDumpStoragePartitionByUID");
            }
        }

        internal static async Task FirehoseDumpStoragePartitionByNameAndLUN(string DevicePath, string ProgrammerPath, string VhdxOutputPath, StorageType storageType, bool Verbose, string Name, int Lun)
        {
            Logging.Log("START FirehoseDumpStoragePartitionByNameAndLUN");

            try
            {
                (QualcommSerial Serial, QualcommFirehose? Firehose) = await CommonFirehoseLoad(DevicePath, ProgrammerPath, Verbose);

                if (Firehose == null)
                {
                    Logging.Log("Loading firehose failed.");
                }
                else
                {
                    ConfigureResponse response = Firehose.Configure(storageType, Verbose);

                    switch (storageType)
                    {
                        case StorageType.UFS:
                        case StorageType.SPINOR:
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

                                bool PartitionFound = false;

                                // Test workaround for Duo 2 programmer
                                //luStorageInfos.Add(new Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.Root() { storage_info = new Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo.StorageInfo() { block_size = 4096, total_blocks = 10000000, num_physical = 0 } });

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

                                GPT? GPT = null;

                                try
                                {
                                    GPT = ReadGPT(Firehose, (uint)storageInfo.storage_info.block_size, storageType, (uint)Lun, Verbose, response.MaxPayloadSizeToTargetInBytes);
                                }
                                catch (Exception e)
                                {
                                    Logging.Log(e.ToString());
                                }

                                if (GPT == null)
                                {
                                    Logging.Log($"LUN {Lun}: No GPT found");
                                    break;
                                }

                                foreach (GPTPartition partition in GPT.Partitions)
                                {
                                    if (ConvertCharArrayToASCIIString(partition.Name) == Name)
                                    {
                                        if (File.Exists(VhdxOutputPath))
                                        {
                                            throw new Exception("File already exists");
                                        }

                                        FileStream fileStream = File.Create(VhdxOutputPath);

                                        PartStream partStream = new(Firehose, Lun, storageType, Verbose, partition.FirstLBA, partition.LastLBA, response.MaxPayloadSizeToTargetInBytes, storageInfo);

                                        long diskCapacity = partStream.Length;
                                        fileStream.SetLength(diskCapacity);


                                        StreamPump pump = new()
                                        {
                                            InputStream = partStream,
                                            OutputStream = fileStream,
                                            SparseCopy = true,
                                            SparseChunkSize = storageInfo.storage_info.block_size,
                                            BufferSize = response.MaxPayloadSizeToTargetInBytes//storageInfo.storage_info.block_size * 256 // Max 24 sectors at a time
                                        };

                                        long totalBytes = partStream.Length;

                                        DateTime now = DateTime.Now;
                                        pump.ProgressEvent += (o, e) => { ShowProgress((ulong)e.BytesRead, (ulong)totalBytes, now, Verbose); };

                                        Logging.Log("Converting RAW to RAW");
                                        pump.Run();
                                        Logging.Log();

                                        PartitionFound = true;
                                        break;
                                    }
                                }

                                if (PartitionFound)
                                {
                                    break;
                                }

                                if (!PartitionFound)
                                {
                                    Logging.Log("Partition Name not found.");
                                }
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
                Logging.Log("END FirehoseDumpStoragePartitionByNameAndLUN");
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
    }
}
