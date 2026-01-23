using System.Xml.Serialization;

namespace Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements
{
    public enum ResponseValue
    {
        [XmlEnum(Name = "ACK")]
        ACK,
        [XmlEnum(Name = "NAK")]
        NAK
    }
}
