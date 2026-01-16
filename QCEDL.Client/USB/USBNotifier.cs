using QCEDL.NET.USB;

namespace QCEDL.Client.USB
{
    internal class USBNotifier
    {
        private static readonly Guid COMPortGuid = new("{86E0D1E0-8089-11D0-9CE4-08003E301F73}");
        private static readonly Guid WinUSBGuid = new("{71DE994D-8B7C-43DB-A27E-2AE7CD579A0C}");

        public void FindEDLDevices()
        {
            foreach ((string?, string?) deviceInfo in USBExtensions.GetDeviceInfos(COMPortGuid))
            {
                GetEmergencyPathType(COMPortGuid, deviceInfo);
            }

            foreach ((string?, string?) deviceInfo in USBExtensions.GetDeviceInfos(WinUSBGuid))
            {
                GetEmergencyPathType(WinUSBGuid, deviceInfo);
            }
        }

        public void GetEmergencyPathType(Guid Guid, (string?, string?) deviceInfo)
        {
            string? DevicePath = deviceInfo.Item1;
            string? BusName = deviceInfo.Item2;

            if (DevicePath!.Contains("VID_05C6&", StringComparison.OrdinalIgnoreCase)) // Qualcomm device
            {
                if (DevicePath.Contains("&PID_9008", StringComparison.OrdinalIgnoreCase))
                {
                    if ((BusName == "QHSUSB_DLOAD") || (BusName == "QHSUSB__BULK") || (BusName!.StartsWith("QUSB_BULK")))
                    {
                        Console.WriteLine($"Found device on interface: {Guid}");
                        Console.WriteLine($"Device path: {DevicePath}");
                        Console.WriteLine($"Bus Name: {BusName}");

                        if (BusName?.Length == 0)
                        {
                            Console.WriteLine("Driver does not show busname, assume mode: Qualcomm Emergency Download 9008");
                        }
                        else
                        {
                            Console.WriteLine("Mode: Qualcomm Emergency Download 9008");
                        }

                        InternalOnQualcommEmergencyDownloadDeviceDetected(DevicePath);
                    }
                    else if (BusName == "QHSUSB_ARMPRG")
                    {
                        Console.WriteLine($"Found device on interface: {Guid}");
                        Console.WriteLine($"Device path: {DevicePath}");
                        Console.WriteLine($"Bus Name: {BusName}");

                        if (BusName?.Length == 0)
                        {
                            Console.WriteLine("Driver does not show busname, assume mode: Qualcomm Emergency Flash 9008");
                        }
                        else
                        {
                            Console.WriteLine("Mode: Qualcomm Emergency Flash 9008");
                        }

                        InternalOnQualcommEmergencyFlashDeviceDetected(DevicePath);
                    }
                }
            }
        }

        private void InternalOnQualcommEmergencyFlashDeviceDetected(string DevicePath)
        {
            Console.WriteLine("Qualcomm Emergency Flash 9008 device detected");

            OnQualcommEmergencyFlashDeviceDetected?.Invoke(DevicePath);
        }

        private void InternalOnQualcommEmergencyDownloadDeviceDetected(string DevicePath)
        {
            Console.WriteLine("Qualcomm Emergency Download 9008 device detected");

            OnQualcommEmergencyDownloadDeviceDetected?.Invoke(DevicePath);
        }

        public delegate void DeviceFound(string DevicePath);

        public event DeviceFound? OnQualcommEmergencyDownloadDeviceDetected;
        public event DeviceFound? OnQualcommEmergencyFlashDeviceDetected;
    }
}
