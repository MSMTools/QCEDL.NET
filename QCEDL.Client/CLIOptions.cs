using CommandLine;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;

namespace QCEDL.Client
{
    internal class CLIOptions
    {
        [Verb("firehose-load", HelpText = "Load firehose programmer onto the device")]
        public class FirehoseLoadOptions
        {
            /*[Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose
            {
                get; set;
            }*/

            [Option('f', "firehose", Required = true, HelpText = "Firehose programmer.")]
            public string Firehose
            {
                get; set;
            }
        }

        [Verb("firehose-reset", HelpText = "Reset from a loaded firehose programmer on the device")]
        public class FirehoseResetOptions
        {
            [Option('f', "firehose", Required = true, HelpText = "Firehose programmer.")]
            public string Firehose
            {
                get; set;
            }
        }

        [Verb("firehose-readstorageinfo", HelpText = "TODO")]
        public class FirehoseReadStorageInfoOptions
        {
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
    }
}
