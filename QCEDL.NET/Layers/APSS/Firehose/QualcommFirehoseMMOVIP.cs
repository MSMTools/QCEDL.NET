using Qualcomm.EmergencyDownload.Transport;
using System.Xml.Serialization;
using System.Xml;
using Qualcomm.EmergencyDownload.Layers.PBL.Sahara;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;
using System.Diagnostics;

namespace Qualcomm.EmergencyDownload.Layers.APSS.Firehose
{
    internal class QualcommFirehoseMMOVIP
    {
        public static bool ConnectToProgrammer(QualcommSerial Serial, QualcommFirehose Firehose, byte[] PacketFromPcToProgrammer, bool Verbose)
        {
            // Behaviour of old firehose:
            // Takes about 20 ms to be started.
            // Then PC has to start talking to the phone.
            // Behaviour of new firehose:
            // After 2000 ms the firehose starts talking to the PC
            //
            // For the duration of 2.5 seconds we will send Hello packages
            // And also wait for incoming messages
            // An incoming message can be a response to our outgoing Hello packet (read incoming until "response value")
            // Or it can be an incoming Hello-packet from the programmer (always 2 packets, starting with "Chip serial num")
            // Sending the hello-packet can succeed immediately, or it can timeout.
            // When sending succeeds, an answer should be incoming immediately to complete the handshake.
            // When an incoming Hello was received, the phone still expects to receive another Hello.

            int HelloSendCount = 0;
            bool HandshakeCompleted = false;
            do
            {
                Serial.SetTimeOut(200);
                HelloSendCount++;
                try
                {
                    Console.WriteLine($"Send Hello to programmer ({HelloSendCount})");
                    Serial.SendData(PacketFromPcToProgrammer);
                    Console.WriteLine("Hello packet accepted");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An unexpected error happened");
                    Console.WriteLine(ex.GetType().ToString());
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);

                    Console.WriteLine("Hello packet not accepted");
                }

                try
                {
                    Serial.SetTimeOut(500);
                    Data[] datas = Firehose.GetFirehoseResponseDataPayloads();

                    foreach (Data data in datas)
                    {
                        if (data.Log != null)
                        {
                            if (Verbose)
                            {
                                Console.WriteLine("DEVPRG LOG: " + data.Log.Value);
                            }
                            else
                            {
                                Debug.WriteLine("DEVPRG LOG: " + data.Log.Value);
                            }
                        }
                        else if (data.Response != null)
                        {

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

                    Serial.SetTimeOut(200);
                    if (datas.Any(x => x.Log?.Value?.Contains("Chip serial num") == true))
                    {
                        datas = Firehose.GetFirehoseResponseDataPayloads();

                        foreach (Data data in datas)
                        {
                            if (data.Log != null)
                            {
                                if (Verbose)
                                {
                                    Console.WriteLine("DEVPRG LOG: " + data.Log.Value);
                                }
                                else
                                {
                                    Debug.WriteLine("DEVPRG LOG: " + data.Log.Value);
                                }
                            }
                            else if (data.Response != null)
                            {

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

                        Console.WriteLine("Incoming Hello-packets received");
                    }

                    while (!datas.Any(x => x.Response != null))
                    {
                        datas = Firehose.GetFirehoseResponseDataPayloads();

                        foreach (Data data in datas)
                        {
                            if (data.Log != null)
                            {
                                if (Verbose)
                                {
                                    Console.WriteLine("DEVPRG LOG: " + data.Log.Value);
                                }
                                else
                                {
                                    Debug.WriteLine("DEVPRG LOG: " + data.Log.Value);
                                }
                            }
                            else if (data.Response != null)
                            {

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

                    Console.WriteLine("Incoming Hello-response received");

                    if (!datas.Any(x => x.Log?.Value?.Contains("Failed to authenticate Digital Signature.") == true))
                    {
                        HandshakeCompleted = true;
                    }
                    else
                    {
                        Console.WriteLine("Programmer failed to authenticate Digital Signature");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            while (!HandshakeCompleted && HelloSendCount < 6);

            return HandshakeCompleted;
        }

        public static bool ConnectToProgrammerInTestMode(QualcommSerial Serial, QualcommFirehose Firehose, bool Verbose)
        {
            byte[] HelloPacketFromPcToProgrammer = new byte[0x20C];
            ByteOperations.WriteUInt32(HelloPacketFromPcToProgrammer, 0, 0x57503730);     // WP70
            ByteOperations.WriteUInt32(HelloPacketFromPcToProgrammer, 0x28, 0x57503730);  // WP70
            ByteOperations.WriteUInt32(HelloPacketFromPcToProgrammer, 0x208, 0x57503730); // WP70
            ByteOperations.WriteUInt16(HelloPacketFromPcToProgrammer, 0x48, 0x4445);      // DE

            bool HandshakeCompleted = ConnectToProgrammer(Serial, Firehose, HelloPacketFromPcToProgrammer, Verbose);

            if (HandshakeCompleted)
            {
                Console.WriteLine("Handshake completed with programmer in testmode");
            }
            else
            {
                Console.WriteLine("Handshake with programmer failed");
            }

            return HandshakeCompleted;
        }
    }
}
