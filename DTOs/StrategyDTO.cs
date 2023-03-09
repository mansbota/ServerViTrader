using ServerViTrader.Exceptions;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace ServerViTrader.DTOs
{
    [XmlRoot(ElementName = "strategy")]
    public class StrategyDTO : IValid
    {
        [XmlElement(ElementName = "id")]
        public int id { get; set; }

        [XmlElement(ElementName = "name")]
        public string name { get; set; }

        [XmlElement(ElementName = "userId")]
        public int userId { get; set; }

        [XmlElement(ElementName = "triggers")]
        public List<TriggerDTO> triggers { get; set; }

        public void Validate()
        {
            if (id is not 0 and < 1)
                throw new BadRequestException("Id must be positive.");

            if (name is not null && name.Length is < 5 or > 20)
                throw new BadRequestException("Name must be between 5 and 20 chatacters.");

            if (userId is not 0 and < 1)
                throw new BadRequestException("User id must be positive.");
        }
    }
}
