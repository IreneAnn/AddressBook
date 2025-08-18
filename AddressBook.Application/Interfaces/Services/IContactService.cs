using AddressBook.Application.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Application.Interfaces.Services
{
    public interface IContactService
    {
               
        Task<UpsertContactResult?> UpsertContactAsync(ContactDto dto);
        Task<ContactDto?> GetContactByIdAsync(Guid id);
        Task<(IEnumerable<ContactDto> Items, int Total)> GetContactListAsync(int page, int pageSize);
    }
}
