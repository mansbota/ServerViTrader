using ServerViTrader.Exceptions;
using System;
using System.Xml.Serialization;

namespace ServerViTrader.DTOs
{
    [XmlRoot(ElementName = "trade")]
    public class TradeDTO : IValid
    {
        [XmlElement(ElementName = "id")]
        public int id { get; set; }

        [XmlElement(ElementName = "userId")]
        public int userId { get; set; }

        [XmlElement(ElementName = "cryptoId")]
        public int cryptoId { get; set; }

        [XmlElement(ElementName = "tradeTime")]
        public DateTime tradeTime { get; set; }

        [XmlElement(ElementName = "amount")]
        public decimal amount { get; set; }

        [XmlElement(ElementName = "tradeTypeId")]
        public int tradeTypeId { get; set; }

        public void Validate()
        {
            if (id is not 0 and < 1)
                throw new BadRequestException("Id must be positive.");

            if (userId is not 0 and < 1)
                throw new BadRequestException("User id must be positive.");

            if (cryptoId is not 0 and < 1)
                throw new BadRequestException("Crypto id must be positive.");

            if (amount is < 10 or > 100000)
                throw new BadRequestException("Trade USD value must be between 10 and 100000.");

            if (tradeTypeId is not 0 and < 1)
                throw new BadRequestException("Trade type id must be positive.");
        }
    }
}
