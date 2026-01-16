using System.Xml.Serialization;
using System.Xml;
using Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements;

namespace Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml
{
    internal class QualcommFirehoseXml
    {
        public static string BuildCommandPacket(Data[] dataPayloads)
        {
            XmlSerializer xmlSerializer = new(typeof(Data), new XmlRootAttribute("data"));

            XmlWriterSettings settings = new()
            {
                Indent = false,
                OmitXmlDeclaration = true
            };

            XmlSerializerNamespaces ns = new([XmlQualifiedName.Empty]);

            List<string> dataStrElements = [];

            foreach (Data data in dataPayloads)
            {
                using StringWriter sww = new();
                using XmlWriter writer = XmlWriter.Create(sww, settings);

                xmlSerializer.Serialize(writer, data, ns);

                dataStrElements.Add(sww.ToString());
            }

            return "<?xml version=\"1.0\" ?>" + string.Join("", dataStrElements);
        }

        public static Data[] GetDataPayloads(string commandPacket)
        {
            /*ConsoleColor original = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(commandPacket);
            Console.ForegroundColor = original;*/

            string newCommandPacket = commandPacket.Replace("<?xml version=\"1.0\" encoding=\"UTF-8\" ?>", "");
            newCommandPacket = $"<dataArray>{newCommandPacket}</dataArray>";
            newCommandPacket = newCommandPacket.Replace((char)0x14, ' ');

            XmlSerializer xmlSerializer = new(typeof(DataArray), new XmlRootAttribute("dataArray"));

            XmlReaderSettings settings = new()
            {
                CheckCharacters = false
            };

            using XmlReader reader = XmlReader.Create(new StringReader(newCommandPacket), settings);

            try
            {
                DataArray data = (xmlSerializer.Deserialize(reader) as DataArray)!;
                return data.Data;
            }
            catch
            {
                Console.WriteLine("UNEXPECTED PARSING FAILURE. ABOUT TO CRASH. PAYLOAD STR RAW AS FOLLOW:");
                Console.WriteLine(commandPacket);
                throw;
            }
        }
    }
}
