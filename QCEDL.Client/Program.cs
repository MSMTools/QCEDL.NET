using CommandLine;
using QCEDL.Client.USB;

namespace QCEDL.Client
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("QCEDL Client");
            Console.WriteLine("Copyright (c) 2026 Gustave Monce");
            Console.WriteLine();

            return Parser.Default.ParseArguments<
               CLIOptions.FirehoseLoadOptions,
               CLIOptions.FirehoseResetOptions,
               CLIOptions.FirehoseReadStorageInfoOptions,
               CLIOptions.FirehoseDumpStorageOptions,
               CLIOptions.FirehoseDumpStorageLunOptions>(args)
                 .MapResult(
                   (CLIOptions.FirehoseLoadOptions opts) => RunLoadFirehoseAndReturnExitCode(opts),
                   (CLIOptions.FirehoseResetOptions opts) => RunResetFromFirehoseAndReturnExitCode(opts),
                   (CLIOptions.FirehoseReadStorageInfoOptions opts) => RunFirehoseReadStorageInfoAndReturnExitCode(opts),
                   (CLIOptions.FirehoseDumpStorageOptions opts) => RunFirehoseDumpStorageAndReturnExitCode(opts),
                   (CLIOptions.FirehoseDumpStorageLunOptions opts) => RunFirehoseDumpStorageLunAndReturnExitCode(opts),
                   errs => 1);
        }

        private static int RunLoadFirehoseAndReturnExitCode(CLIOptions.FirehoseLoadOptions opts)
        {
            USBNotifier usbNotifier = new();

            usbNotifier.OnQualcommEmergencyDownloadDeviceDetected += DevicePath =>
            {
                FirehoseTasks.FirehoseLoad(DevicePath, opts.Firehose, opts.Verbose).Wait();
            };

            usbNotifier.FindEDLDevices();

            return 0;
        }

        private static int RunResetFromFirehoseAndReturnExitCode(CLIOptions.FirehoseResetOptions opts)
        {
            USBNotifier usbNotifier = new();

            usbNotifier.OnQualcommEmergencyDownloadDeviceDetected += DevicePath =>
            {
                FirehoseTasks.FirehoseReset(DevicePath, opts.Firehose, opts.Verbose, opts.PowerValue).Wait();
            };

            usbNotifier.FindEDLDevices();

            return 0;
        }

        private static int RunFirehoseReadStorageInfoAndReturnExitCode(CLIOptions.FirehoseReadStorageInfoOptions opts)
        {
            USBNotifier usbNotifier = new();

            usbNotifier.OnQualcommEmergencyDownloadDeviceDetected += DevicePath =>
            {
                FirehoseTasks.FirehoseReadStorageInfo(DevicePath, opts.Firehose, opts.StorageType, opts.Verbose).Wait();
            };

            usbNotifier.FindEDLDevices();

            return 0;
        }

        private static int RunFirehoseDumpStorageAndReturnExitCode(CLIOptions.FirehoseDumpStorageOptions opts)
        {
            USBNotifier usbNotifier = new();

            usbNotifier.OnQualcommEmergencyDownloadDeviceDetected += DevicePath =>
            {
                FirehoseTasks.FirehoseDumpStorage(DevicePath, opts.Firehose, opts.VhdxOutputPath, opts.StorageType, opts.Verbose).Wait();
            };

            usbNotifier.FindEDLDevices();

            return 0;
        }

        private static int RunFirehoseDumpStorageLunAndReturnExitCode(CLIOptions.FirehoseDumpStorageLunOptions opts)
        {
            USBNotifier usbNotifier = new();

            usbNotifier.OnQualcommEmergencyDownloadDeviceDetected += DevicePath =>
            {
                FirehoseTasks.FirehoseDumpStorageLun(DevicePath, opts.Firehose, opts.VhdxOutputPath, opts.StorageType, opts.Verbose, opts.Lun).Wait();
            };

            usbNotifier.FindEDLDevices();

            return 0;
        }
    }
}
