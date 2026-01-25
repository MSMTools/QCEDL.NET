using QCEDL.NET.PartitionTable;
using Qualcomm.EmergencyDownload.ChipInfo;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.JSON.StorageInfo;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;
using Qualcomm.EmergencyDownload.Layers.PBL.Sahara;
using Qualcomm.EmergencyDownload.Transport;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace QCEDL.Client
{
    internal partial class FirehoseTasks
    {
        private static byte[]? ReadGPTBuffer(QualcommFirehose Firehose, uint sectorSize, StorageType storageType, uint physicalPartition, bool Verbose, int MaxPayloadSizeToTargetInBytes)
        {
            // Read 6 sectors
            return Firehose.Read(storageType, physicalPartition, sectorSize, 0, 5, Verbose, MaxPayloadSizeToTargetInBytes);
        }

        private static GPT? ReadGPT(QualcommFirehose Firehose, uint sectorSize, StorageType storageType, uint physicalPartition, bool Verbose, int MaxPayloadSizeToTargetInBytes)
        {
            byte[]? GPTLUN = ReadGPTBuffer(Firehose, sectorSize, storageType, physicalPartition, Verbose, MaxPayloadSizeToTargetInBytes);

            if (GPTLUN == null)
            {
                return null;
            }

            using MemoryStream stream = new(GPTLUN);
            return GPT.ReadFromStream(stream, (int)sectorSize);
        }

        private static void ReadGPTs(QualcommFirehose Firehose, StorageType storageType, bool Verbose, int MaxPayloadSizeToTargetInBytes)
        {
            List<Root> luStorageInfos = GetStorageInfos(Firehose, storageType, Verbose, MaxPayloadSizeToTargetInBytes);

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
                    GPT = ReadGPT(Firehose, (uint)storageInfo.storage_info.block_size, storageType, (uint)i, Verbose, MaxPayloadSizeToTargetInBytes);
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
                    ConfigureResponse response = Firehose.Configure(storageType, Verbose);

                    ReadGPTs(Firehose, storageType, Verbose, response.MaxPayloadSizeToTargetInBytes);
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
    }
}
