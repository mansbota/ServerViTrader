using ServerViTrader.Exceptions;
using System.Xml.Serialization;

namespace ServerViTrader.DTOs
{
    [XmlRoot(ElementName = "position")]
    public class PositionDTO : IValid
    {
        [XmlElement(ElementName ="id")]
        public int id { get; set; }

        [XmlElement(ElementName = "userId")]
        public int userId { get; set; }

        [XmlElement(ElementName = "cryptoId")]
        public int cryptoId { get; set; }

        [XmlElement(ElementName = "amount")]
        public decimal amount { get; set; }

        public void Validate()
        {
            if (id is not 0 and < 1)
                throw new BadRequestException("Id must be positive.");

            if (userId is not 0 and < 1)
                throw new BadRequestException("User id must be positive.");

            if (cryptoId is not 0 and < 1)
                throw new BadRequestException("Crypto id must be positive.");

            if (amount < 0)
                throw new BadRequestException("Amount can't be negative.");

            if (amount > 10000)
                throw new BadRequestException("Amount can't be greater than 100000.");
        }
    }
}
