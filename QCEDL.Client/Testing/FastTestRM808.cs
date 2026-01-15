using Qualcomm.EmergencyDownload.Layers.PBL.Streaming;
using Qualcomm.EmergencyDownload.Layers.APSS.Fast;
using Qualcomm.EmergencyDownload.Transport;

namespace QCEDL.Client.Testing
{
    internal class FastTestRM808
    {
        public static void TestFastProtocolOnRM808(string DevicePath, byte[] FASTPRG)
        {
            // Send and start programmer
            QualcommSerial Serial = new(DevicePath);
            QualcommDownload Download = new(Serial);

            if (Download.IsAlive())
            {
                int Attempt = 1;
                bool Result = false;

                Console.WriteLine("Attempt " + Attempt.ToString());

                try
                {
                    // TODO: Dynamically figure out or allow changing this boot address,
                    // this is MSM8x55 specific to begin with here
                    // Newer MSMs have different boot addresses
                    Download.SendToPhoneMemory(0x80000000, FASTPRG);
                    Download.StartBootloader(0x80000000);
                    Result = true;
                    Console.WriteLine("Loader sent successfully");
                }
                catch { }

                Attempt++;

                Serial.Close();

                if (!Result)
                {
                    Console.WriteLine("Loader failed");
                }
            }
            else
            {
                Console.WriteLine("Failed to communicate to Qualcomm Emergency Download mode");
                throw new BadConnectionException();
            }

            Console.WriteLine("Waiting for arrival");

            // TODO: Implement wait for arrival of different FAST USB Interface right here

        flash:
            Console.WriteLine("Device Arrived");

            // Flash bootloader
            QualcommSerial Serial2 = new(DevicePath);
            Serial2.EncodeCommands = true;

            Console.WriteLine("Flasher");

            QualcommFlasher Flasher = new(Serial2);

            Console.WriteLine("Hello");

            Flasher.Hello();

            Console.WriteLine("Security Mode");

            Flasher.SetSecurityMode(0);

            Console.WriteLine("Open Partition");

            Flasher.OpenPartition(0x21);

            Console.WriteLine("Partition Opened");

            Flasher.ClosePartition();

            Console.WriteLine("Partition closed. Rebooting.");

            Flasher.Reboot();

            Flasher.CloseSerial();
        }
    }
}
