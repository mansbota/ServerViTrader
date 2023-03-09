using System.Xml.Serialization;

namespace ServerViTrader.DTOs
{
    [XmlRoot(ElementName = "trigger")]
    public class TriggerDTO
    {
        [XmlElement(ElementName = "id")]
        public int id { get; set; }

        [XmlElement(ElementName = "strategyId")]
        public int strategyId { get; set; }

        [XmlElement(ElementName = "indicatorId")]
        public int indicatorId { get; set; }

        [XmlElement(ElementName = "indicatorValue")]
        public decimal indicatorValue { get; set; }

        [XmlElement(ElementName = "triggerTypeId")]
        public int triggerTypeId { get; set; }
    }
}
