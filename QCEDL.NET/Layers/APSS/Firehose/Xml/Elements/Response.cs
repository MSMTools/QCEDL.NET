using System.Xml;
using System.Xml.Serialization;

namespace Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements
{
    public class Response : ConfigureResponse
    {
        private ResponseValue? value;

        [XmlAttribute(AttributeName = "value")]
        public ResponseValue Value
        {
            get => value ?? ResponseValue.NAK; set => this.value = value;
        }

        public bool ShouldSerializeValue()
        {
            return value.HasValue;
        }

        [XmlAttribute(AttributeName = "rawmode")]
        public bool RawMode
        {
            get; set;
        }

        [XmlAttribute(AttributeName = "sha256")]
        public string? SHA256
        {
            get; set;
        }

        public bool ShouldSerializeSHA256()
        {
            return SHA256 != null;
        }
    }
}
