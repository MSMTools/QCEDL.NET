using CommandLine;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;

namespace QCEDL.Client
{
    internal class CLIOptions
    {
        [Verb("firehose-load", HelpText = "Load firehose programmer onto the device")]
        public class FirehoseLoadOptions
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose
            {
                get; set;
            }

            [Option('f', "firehose", Required = true, HelpText = "Firehose programmer.")]
            public string Firehose
            {
                get; set;
            }
        }

        [Verb("firehose-reset", HelpText = "Reset from a loaded firehose programmer on the device")]
        public class FirehoseResetOptions
        {
            [Option('p', "power-value", Required = true, HelpText = "TODO", Default = PowerValue.Reset)]
            public PowerValue PowerValue
            {
                get; set;
            }

            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose
            {
                get; set;
            }

            [Option('f', "firehose", Required = true, HelpText = "Firehose programmer.")]
            public string Firehose
            {
                get; set;
            }
        }

        [Verb("firehose-readstorageinfo", HelpText = "TODO")]
        public class FirehoseReadStorageInfoOptions
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose
            {
                get; set;
            }

            [Option('s', "storage-type", Required = true, HelpText = "TODO")]
            public StorageType StorageType
            {
                get; set;
            }

            [Option('f', "firehose", Required = true, HelpText = "Firehose programmer.")]
            public string Firehose
            {
                get; set;
            }
        }

        [Verb("firehose-dumpstorage", HelpText = "TODO")]
        public class FirehoseDumpStorageOptions
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose
            {
                get; set;
            }

            [Option('o', "output-path", Required = true, HelpText = "TODO")]
            public string VhdxOutputPath
            {
                get; set;
            }

            [Option('s', "storage-type", Required = true, HelpText = "TODO")]
            public StorageType StorageType
            {
                get; set;
            }

            [Option('f', "firehose", Required = true, HelpText = "Firehose programmer.")]
            public string Firehose
            {
                get; set;
            }
        }

        [Verb("firehose-dumpstoragelun", HelpText = "TODO")]
        public class FirehoseDumpStorageLunOptions
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose
            {
                get; set;
            }

            [Option('o', "output-path", Required = true, HelpText = "TODO")]
            public string VhdxOutputPath
            {
                get; set;
            }

            [Option('s', "storage-type", Required = true, HelpText = "TODO")]
            public StorageType StorageType
            {
                get; set;
            }

            [Option('f', "firehose", Required = true, HelpText = "Firehose programmer.")]
            public string Firehose
            {
                get; set;
            }

            [Option('l', "lun", Required = true, HelpText = "TODO")]
            public int Lun
            {
                get; set;
            }
        }

        [Verb("firehose-dumpstorageuid", HelpText = "TODO")]
        public class FirehoseDumpStorageUIDOptions
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose
            {
                get; set;
            }

            [Option('o', "output-path", Required = true, HelpText = "TODO")]
            public string VhdxOutputPath
            {
                get; set;
            }

            [Option('s', "storage-type", Required = true, HelpText = "TODO")]
            public StorageType StorageType
            {
                get; set;
            }

            [Option('f', "firehose", Required = true, HelpText = "Firehose programmer.")]
            public string Firehose
            {
                get; set;
            }

            [Option('u', "uid", Required = true, HelpText = "TODO")]
            public Guid Uid
            {
                get; set;
            }
        }

        [Verb("firehose-dumpstoragelunname", HelpText = "TODO")]
        public class FirehoseDumpStorageLunNameOptions
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose
            {
                get; set;
            }

            [Option('o', "output-path", Required = true, HelpText = "TODO")]
            public string VhdxOutputPath
            {
                get; set;
            }

            [Option('s', "storage-type", Required = true, HelpText = "TODO")]
            public StorageType StorageType
            {
                get; set;
            }

            [Option('f', "firehose", Required = true, HelpText = "Firehose programmer.")]
            public string Firehose
            {
                get; set;
            }

            [Option('l', "lun", Required = true, HelpText = "TODO")]
            public int Lun
            {
                get; set;
            }

            [Option('n', "name", Required = true, HelpText = "TODO")]
            public string Name
            {
                get; set;
            }
        }
    }
}
