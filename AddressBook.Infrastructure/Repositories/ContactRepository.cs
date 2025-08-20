using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Infrastructure.Repositories
{
    public class ContactRepository : IContactRepository
    {
        private readonly AddressBookDbContext _addressBookDbContext;

        public ContactRepository(AddressBookDbContext addressBookDbContext)
        {
            _addressBookDbContext = addressBookDbContext;
        }

        public async Task<UpsertStatus> UpsertContactAsync(Contact newContact)
        {
            UpsertStatus upsertStatus = default;
            var existingContact = await _addressBookDbContext.Contacts.Include(c => c.Groups).FirstOrDefaultAsync(c => c.Id == newContact.Id);
            if (existingContact == null)
            {
                _addressBookDbContext.Contacts.Add(newContact);
                upsertStatus = UpsertStatus.Created;     
            }
            else
            {
                // update fields
                existingContact.FirstName = newContact.FirstName;
                existingContact.LastName = newContact.LastName;
                existingContact.PhoneNumber = newContact.PhoneNumber;
                existingContact.Email = newContact.Email;

                
                // Sync groups
                var newGroupIds = newContact.Groups.Select(x => x.Id);  // get the group ids of the new contact
                var groupsToAssign = await _addressBookDbContext.Groups
                    .Where(g => newGroupIds.Contains(g.Id)) // get their corresponding groups
                    .ToListAsync();
               
                existingContact.Groups.Clear();
                    foreach (var group in groupsToAssign)
                    existingContact.Groups.Add(group);
                upsertStatus = UpsertStatus.Updated;
            }
            await _addressBookDbContext.SaveChangesAsync();
            return upsertStatus;
        }

        public async Task<Contact?> GetContactByIdAsync(Guid id)
        {
            return await _addressBookDbContext.Contacts.Include(c => c.Groups).FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<(IEnumerable<Contact> Items, int Total)> GetContactListAsync(int page, int pageSize)
        {
            var contactList = _addressBookDbContext.Contacts.Include(c => c.Groups).OrderBy(c => c.FirstName);
            var contactListCount = await contactList.CountAsync();
            var list = await contactList.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (list, contactListCount);
        }
    }
   
}
