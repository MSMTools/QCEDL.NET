// Copyright (c) 2018, Rene Lergner - @Heathcliff74xda
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;
using Qualcomm.EmergencyDownload.Transport;
using System.Text;

namespace Qualcomm.EmergencyDownload.Layers.APSS.Firehose
{
    public class QualcommFirehose
    {
        internal readonly QualcommSerial Serial;

        public QualcommFirehose(QualcommSerial Serial)
        {
            this.Serial = Serial;
        }

        public byte[] GetFirehoseXMLResponseBuffer(bool WaitTilFooter = false)
        {
            if (!WaitTilFooter)
            {
                return Serial.GetResponse(null);
            }

            List<byte> bufferList = [];

            do
            {
                bufferList.Add(Serial.GetResponse(null, Length: 1)[0]);
            } while (bufferList.Count < 10 || Encoding.UTF8.GetString([.. bufferList.TakeLast(10)]) != " /></data>");

            byte[] ResponseBuffer = [.. bufferList];

            return ResponseBuffer;
        }

        public Data[] GetFirehoseResponseDataPayloads(bool WaitTilFooter = false)
        {
            byte[] ResponseBuffer = GetFirehoseXMLResponseBuffer(WaitTilFooter);

            // Empty response...
            if (ResponseBuffer.All(t => t == 0x0))
            {
                return [];
            }

            string Incoming = Encoding.UTF8.GetString(ResponseBuffer);

            //Console.WriteLine(Incoming);

            try
            {
                Data[] datas = QualcommFirehoseXml.GetDataPayloads(Incoming);
                return datas;
            }
            catch
            {
                Console.WriteLine("UNEXPECTED PARSING FAILURE. ABOUT TO CRASH. PAYLOAD BYTE RAW AS FOLLOW:");
                Console.WriteLine(Convert.ToHexString(ResponseBuffer));
                throw;
            }
        }
    }
}