using System.Xml.Serialization;

namespace ServerViTrader.DTOs
{
    [XmlRoot(ElementName = "crypto")]
    public class CryptoDTO
    {
        [XmlElement(ElementName = "id")]
        public int id { get; set; }

        [XmlElement(ElementName = "ticker")]
        public string ticker { get; set; }

        [XmlElement(ElementName = "name")]
        public string name { get; set; }
    }
}
