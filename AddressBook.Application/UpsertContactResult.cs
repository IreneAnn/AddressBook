using AddressBook.Application.DTO;
using AddressBook.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Application
{
    public class UpsertContactResult
    {
        public ContactDto ContactDto { get; set; } = default!;
        public UpsertStatus Status { get; set; }
    }
}
