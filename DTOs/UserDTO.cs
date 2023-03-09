using ServerViTrader.Exceptions;
using System;
using System.Xml.Serialization;

namespace ServerViTrader.DTOs
{
    [XmlRoot(ElementName = "user")]
    public class UserDTO : IValid
    {
        [XmlElement(ElementName = "id")]
        public int id { get; set; }

        [XmlElement(ElementName = "username")]
        public string username { get; set; }

        [XmlElement(ElementName = "password")]
        public string password { get; set; }

        [XmlElement(ElementName = "email")]
        public string email { get; set; }

        [XmlElement(ElementName = "dateCreated")]
        public DateTime dateCreated { get; set; }

        [XmlElement(ElementName = "salt")]
        public string salt { get; set; }

        [XmlElement(ElementName = "statusId")]
        public int statusId { get; set; }

        public void Validate()
        {
            if (id is not 0 and < 0)
                throw new BadRequestException("Id must be positive.");

            if (username is not null && username.Length is < 5 or > 15)
                throw new BadRequestException("Username must be between 5 and 15 characters.");

            if (password is not null && password.Length is < 7 or > 25)
                throw new BadRequestException("Password must be between 7 and 25 characters.");

            if (email is not null && email.Length is < 10 or > 30)
                throw new BadRequestException("Email must be between 10 and 30 characters.");

            if (statusId is not 0 and < 1)
                throw new BadRequestException("Status id must be greater than 0.");
        }
    }
}
