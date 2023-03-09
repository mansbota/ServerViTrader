using System.Xml.Serialization;

namespace ServerViTrader.DTOs
{
    [XmlRoot(ElementName = "triggerType")]
    public class TriggerTypeDTO
    {
        [XmlElement(ElementName = "id")]
        public int id { get; set; }

        [XmlElement(ElementName = "name")]
        public string name { get; set; }
    }
}