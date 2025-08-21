using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace AddressBook.Application.DTO
{
    public class ContactDto
    {
        public Guid? Id { get; set; }
        [Required, StringLength(200)] public string FirstName { get; set; } = string.Empty;
        [Required, StringLength(200)] public string LastName { get; set; } = string.Empty;
        [Required, Phone] public string PhoneNumber { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        public IEnumerable<Guid> GroupIds { get; set; } = new List<Guid>();
    }
}
