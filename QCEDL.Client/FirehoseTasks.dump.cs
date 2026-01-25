using DiscUtils;
using DiscUtils.Containers;
using DiscUtils.Streams;
using QCEDL.NET.PartitionTable;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;
using Qualcomm.EmergencyDownload.Transport;

namespace QCEDL.Client
{
    internal partial class FirehoseTasks
    {
        internal static List<Root> GetStorageInfos(QualcommFirehose Firehose, StorageType storageType, bool Verbose, int MaxPayloadSizeToTargetInBytes)
        {
            List<Root> luStorageInfos = [];

            // Figure out the number of LUNs first.
            Root? mainInfo = Firehose.GetStorageInfo(Verbose, storageType);

            if (mainInfo != null)
            {
                luStorageInfos.Add(mainInfo);

                int totalLuns = mainInfo.storage_info.num_physical;

                // Now figure out the size of each lun
                for (int i = 1; i < totalLuns; i++)
                {
                    Root? luInfo = Firehose.GetStorageInfo(Verbose, storageType, (uint)i) ?? throw new Exception($"Error in reading LUN {i} for storage info!");
                    luStorageInfos.Add(luInfo);
                }
            }

            // Two possibilities, we are facing a programmer that locks down getting storage info on secure boot fused devices (MSFT Andromeda), or the device doesnt exist.
            // As a fallback, try to automatically build up this information
            if (mainInfo == null)
            {
                List<Root> tempLuStorageInfos = [];

                uint sectorSize = 0;

                // We hardcode a generous maximum of 10 luns
                for (int i = 0; i < 10; i++)
                {
                    // For each of these, read the first part

                    GPT? GPT = null;

                    try
                    {
                        if (sectorSize != 0)
                        {
                            GPT = ReadGPT(Firehose, sectorSize, storageType, (uint)i, Verbose, MaxPayloadSizeToTargetInBytes);
                        }
                        else
                        {
                            // We hardcode a sector size of 4096 for this one
                            GPT = ReadGPT(Firehose, 4096, storageType, (uint)i, Verbose, MaxPayloadSizeToTargetInBytes);

                            if (GPT != null)
                            {
                                // Confirm it
                                sectorSize = 4096;
                            }
                            else
                            {
                                // Backoff with 512 instead
                                GPT = ReadGPT(Firehose, 512, storageType, (uint)i, Verbose, MaxPayloadSizeToTargetInBytes);

                                if (GPT != null)
                                {
                                    // Confirm it
                                    sectorSize = 512;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.Log(e.ToString());

                        if (sectorSize == 0)
                        {
                            // Backoff with 512 instead
                            try
                            {
                                GPT = ReadGPT(Firehose, 512, storageType, (uint)i, Verbose, MaxPayloadSizeToTargetInBytes);

                                if (GPT != null)
                                {
                                    // Confirm it
                                    sectorSize = 512;
                                }
                            }
                            catch (Exception e2)
                            {
                                Logging.Log(e2.ToString());
                            }
                        }
                    }

                    if (GPT == null)
                    {
                        Logging.Log($"LUN {i}: No GPT found");
                        continue;
                    }
                    else
                    {
                        // We temporarily use num_physical as a storage location for which index this lun was.
                        tempLuStorageInfos.Add(new()
                        {
                            storage_info = new()
                            {
                                block_size = GPT.SectorSize,
                                total_blocks = (int)(GPT.Header.LastUsableLBA + 1),
                                num_physical = i
                            }
                        });
                    }
                }

                if (tempLuStorageInfos.Count > 0)
                {
                    // Now that we iterated through everything, reformat the elements in the list
                    // First, grab the maximum valid id we obtained

                    int maxValid = tempLuStorageInfos.MaxBy(t => t.storage_info.num_physical)!.storage_info.num_physical;

                    for (int i = 0; i < maxValid + 1; i++)
                    {
                        Root? storageInfo = tempLuStorageInfos.FirstOrDefault(t => t!.storage_info.num_physical == i, null);
                        if (storageInfo != null)
                        {
                            // Rectify the num physical property to reflect the correct amount of physical luns
                            storageInfo.storage_info.num_physical = maxValid + 1;
                            luStorageInfos.Add(storageInfo);
                        }
                        else
                        {
                            // This is one of those luns we couldn't read at all.
                            // Lets be a minimum "generous" and create a dummy storage info structure with barely any available block to spare

                            // We hardcode a sector size of 4096 for this one
                            // TODO: Dynamically infer this as well
                            luStorageInfos.Add(new()
                            {
                                storage_info = new()
                                {
                                    block_size = (int)sectorSize,
                                    total_blocks = 1,
                                    num_physical = maxValid + 1
                                }
                            });
                        }
                    }
                }
            }

            return luStorageInfos;
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
                    ConfigureResponse response = Firehose.Configure(storageType, Verbose);

                    switch (storageType)
                    {
                        case StorageType.UFS:
                        case StorageType.SPINOR:
                            {
                                List<Root> luStorageInfos = GetStorageInfos(Firehose, storageType, Verbose, response.MaxPayloadSizeToTargetInBytes);

                                for (int i = 0; i < luStorageInfos.Count; i++)
                                {
                                    Root storageInfo = luStorageInfos[i];

                                    Logging.Log();
                                    Logging.Log($"LUN[{i}] Name: {storageInfo.storage_info.prod_name}");
                                    Logging.Log($"LUN[{i}] Total Blocks: {storageInfo.storage_info.total_blocks}");
                                    Logging.Log($"LUN[{i}] Block Size: {storageInfo.storage_info.block_size}");
                                    Logging.Log();

                                    SetupHelper.SetupContainers();

                                    long diskCapacity = storageInfo.storage_info.block_size * (long)storageInfo.storage_info.total_blocks;
                                    using Stream fs = new FileStream(Path.Combine(VhdxOutputPath, $"LUN{i}.vhdx"), FileMode.CreateNew, FileAccess.ReadWrite);
                                    using DiscUtils.Vhdx.Disk outDisk = DiscUtils.Vhdx.Disk.InitializeDynamic(fs, Ownership.None, diskCapacity, Geometry.FromCapacity(diskCapacity, storageInfo.storage_info.block_size));

                                    outDisk.Content.Seek(0, SeekOrigin.Begin);

                                    Logging.Log("Converting RAW to VHDX");
                                    Firehose.Read(storageType, (uint)i, (uint)storageInfo.storage_info.block_size, 0, (uint)(storageInfo.storage_info.total_blocks - 1), Verbose, response.MaxPayloadSizeToTargetInBytes, outDisk.Content, (int percentage, TimeSpan? remaining) =>
                                    {
                                        Logging.Log(
                                            $"{Logging.GetDISMLikeProgressBar((uint)percentage)} {remaining:hh\\:mm\\:ss\\.f}",
                                            returnLine: Verbose);
                                    }, null);
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
                                List<Root> luStorageInfos = GetStorageInfos(Firehose, storageType, Verbose, response.MaxPayloadSizeToTargetInBytes);

                                if (luStorageInfos.Count <= Lun)
                                {
                                    Logging.Log("Lun not found.");
                                    return;
                                }

                                Root storageInfo = luStorageInfos[Lun];

                                Logging.Log();
                                Logging.Log($"LUN[{Lun}] Name: {storageInfo.storage_info.prod_name}");
                                Logging.Log($"LUN[{Lun}] Total Blocks: {storageInfo.storage_info.total_blocks}");
                                Logging.Log($"LUN[{Lun}] Block Size: {storageInfo.storage_info.block_size}");
                                Logging.Log();

                                SetupHelper.SetupContainers();

                                long diskCapacity = storageInfo.storage_info.block_size * (long)storageInfo.storage_info.total_blocks;
                                using Stream fs = new FileStream(Path.Combine(VhdxOutputPath, $"LUN{Lun}.vhdx"), FileMode.CreateNew, FileAccess.ReadWrite);
                                using DiscUtils.Vhdx.Disk outDisk = DiscUtils.Vhdx.Disk.InitializeDynamic(fs, Ownership.None, diskCapacity, Geometry.FromCapacity(diskCapacity, storageInfo.storage_info.block_size));

                                outDisk.Content.Seek(0, SeekOrigin.Begin);

                                Logging.Log("Converting RAW to VHDX");
                                Firehose.Read(storageType, (uint)Lun, (uint)storageInfo.storage_info.block_size, 0, (uint)(storageInfo.storage_info.total_blocks - 1), Verbose, response.MaxPayloadSizeToTargetInBytes, outDisk.Content, (int percentage, TimeSpan? remaining) =>
                                {
                                    Logging.Log(
                                        $"{Logging.GetDISMLikeProgressBar((uint)percentage)} {remaining:hh\\:mm\\:ss\\.f}",
                                        returnLine: Verbose);
                                }, null);
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
                                List<Root> luStorageInfos = GetStorageInfos(Firehose, storageType, Verbose, response.MaxPayloadSizeToTargetInBytes);

                                bool PartitionFound = false;

                                for (int i = 0; i < luStorageInfos.Count; i++)
                                {
                                    Root storageInfo = luStorageInfos[i];

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

                                            using FileStream fileStream = File.Create(VhdxOutputPath);

                                            SectorBasedReader sectorBasedReader = new EDLSectorReader(Firehose, i, storageType, Verbose, response.MaxPayloadSizeToTargetInBytes, storageInfo);
                                            using PartStream partStream = new(sectorBasedReader, partition.FirstLBA, partition.LastLBA);

                                            long diskCapacity = partStream.Length;
                                            fileStream.SetLength(diskCapacity);


                                            StreamPump pump = new()
                                            {
                                                InputStream = partStream,
                                                OutputStream = fileStream,
                                                SparseCopy = true,
                                                SparseChunkSize = storageInfo.storage_info.block_size,
                                                BufferSize = response.MaxPayloadSizeToTargetInBytes
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
                                List<Root> luStorageInfos = GetStorageInfos(Firehose, storageType, Verbose, response.MaxPayloadSizeToTargetInBytes);

                                bool PartitionFound = false;

                                // Test workaround for Duo 2 programmer
                                //luStorageInfos.Add(new Root() { storage_info = new StorageInfo() { block_size = 4096, total_blocks = 10000000, num_physical = 0 } });

                                if (luStorageInfos.Count <= Lun)
                                {
                                    Logging.Log("Lun not found.");
                                    return;
                                }

                                Root storageInfo = luStorageInfos[Lun];

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

                                        using FileStream fileStream = File.Create(VhdxOutputPath);

                                        SectorBasedReader sectorBasedReader = new EDLSectorReader(Firehose, Lun, storageType, Verbose, response.MaxPayloadSizeToTargetInBytes, storageInfo);
                                        using PartStream partStream = new(sectorBasedReader, partition.FirstLBA, partition.LastLBA);

                                        long diskCapacity = partStream.Length;
                                        fileStream.SetLength(diskCapacity);

                                        StreamPump pump = new()
                                        {
                                            InputStream = partStream,
                                            OutputStream = fileStream,
                                            SparseCopy = true,
                                            SparseChunkSize = storageInfo.storage_info.block_size,
                                            BufferSize = response.MaxPayloadSizeToTargetInBytes
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
