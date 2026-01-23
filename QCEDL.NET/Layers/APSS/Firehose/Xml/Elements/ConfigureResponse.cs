using System.Xml.Serialization;

namespace Qualcomm.EmergencyDownload.Layers.APSS.Firehose.Xml.Elements
{
    public class ConfigureResponse
    {
        private StorageType? storageType;

        [XmlAttribute(AttributeName = "storage_type")]
        public StorageType StorageType
        {
            get => storageType ?? StorageType.UFS; set => storageType = value;
        }

        public bool ShouldSerializeStorageType()
        {
            return storageType.HasValue;
        }

        private int? minVersionSupported;

        [XmlAttribute(AttributeName = "MinVersionSupported")]
        public int MinVersionSupported
        {
            get => minVersionSupported ?? 0; set => minVersionSupported = value;
        }

        public bool ShouldSerializeMinVersionSupported()
        {
            return minVersionSupported.HasValue;
        }

        private int? version;

        [XmlAttribute(AttributeName = "Version")]
        public int Version
        {
            get => version ?? 0; set => version = value;
        }

        public bool ShouldSerializeVersion()
        {
            return version.HasValue;
        }

        private int? maxPayloadSizeToTargetInBytes;

        [XmlAttribute(AttributeName = "MaxPayloadSizeToTargetInBytes")]
        public int MaxPayloadSizeToTargetInBytes
        {
            get => maxPayloadSizeToTargetInBytes ?? 0; set => maxPayloadSizeToTargetInBytes = value;
        }

        public bool ShouldSerializeMaxPayloadSizeToTargetInBytes()
        {
            return maxPayloadSizeToTargetInBytes.HasValue;
        }

        private int? maxPayloadSizeToTargetInBytesSupported;

        [XmlAttribute(AttributeName = "MaxPayloadSizeToTargetInBytesSupported")]
        public int MaxPayloadSizeToTargetInBytesSupported
        {
            get => maxPayloadSizeToTargetInBytesSupported ?? 0; set => maxPayloadSizeToTargetInBytesSupported = value;
        }

        public bool ShouldSerializeMaxPayloadSizeToTargetInBytesSupported()
        {
            return maxPayloadSizeToTargetInBytesSupported.HasValue;
        }

        private int? maxXMLSizeInBytes;

        [XmlAttribute(AttributeName = "MaxXMLSizeInBytes")]
        public int MaxXMLSizeInBytes
        {
            get => maxXMLSizeInBytes ?? 0; set => maxXMLSizeInBytes = value;
        }

        public bool ShouldSerializeMaxXMLSizeInBytes()
        {
            return maxXMLSizeInBytes.HasValue;
        }

        [XmlAttribute(AttributeName = "DateTime")]
        public string? DateTime
        {
            get; set;
        }
    }
}
