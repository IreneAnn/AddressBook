using AddressBook.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Application.Interfaces.Repositories
{
    public interface IContactRepository
    {
        Task<UpsertStatus> UpsertContactAsync(Contact contact);
        Task<Contact?> GetContactByIdAsync(Guid id);
        Task<(IEnumerable<Contact> Items, int Total)> GetContactListAsync(int page, int pageSize);
    }
}
