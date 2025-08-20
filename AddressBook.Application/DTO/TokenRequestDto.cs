using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Application.DTO
{
    public class TokenRequestDto
    {
        [FromForm(Name = "grant_type")]
        public string GrantType { get; set; } = "client_credentials";

        [FromForm(Name = "client_id")]
        public string ClientId { get; set; }

        [FromForm(Name = "client_secret")]
        public string ClientSecret { get; set; }

        [FromForm(Name = "scope")]
        public string Scope { get; set; }
    }
}
