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
    public class GroupRepository : IGroupRepository
    {
        private readonly AddressBookDbContext _addressBookDbContext;
        public GroupRepository(AddressBookDbContext addressBookDbContext)
        {
            _addressBookDbContext = addressBookDbContext;
        }

        public async Task<UpsertStatus> UpsertGroupAsync(Group newGroup)
        {
            UpsertStatus upsertStatus = default;
            var existingGroup = await _addressBookDbContext.Groups.Include(g => g.Contacts).FirstOrDefaultAsync(g => g.Id == newGroup.Id);
            if (existingGroup == null)
            {
                _addressBookDbContext.Groups.Add(newGroup);
                upsertStatus = UpsertStatus.Created;
            }
            else
            {
                existingGroup.Name = newGroup.Name;
                // Sync contacts if needed

                /*existingGroup.Contacts.Clear();

                foreach (var contact in newGroup.Contacts)
                {
                    var contactsToAssign = await _addressBookDbContext.Contacts
                        .FirstOrDefaultAsync(c => c.Id == contact.Id);

                    if (contactsToAssign != null)
                        existingGroup.Contacts.Add(contactsToAssign);
                    else
                        existingGroup.Contacts.Add(contact); // new contact
                }*/

                upsertStatus = UpsertStatus.Updated;
            }
            await _addressBookDbContext.SaveChangesAsync();
            return upsertStatus;
        }

        public async Task<Group?> GetGroupByIdAsync(Guid id)
        {
            return await _addressBookDbContext.Groups.Include(g => g.Contacts).FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task<(IEnumerable<Group> Items, int Total)> GetGroupListAsync(int page, int pageSize)
        {
            var groupList = _addressBookDbContext.Groups.Include(g => g.Contacts).OrderBy(g => g.Name);
            var groupListCount = await groupList.CountAsync();
            var items = await groupList.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, groupListCount);
        }

        public async Task<List<Group>> GetGroupsByIdsAsync(List<Guid> ids)
        {
            return await _addressBookDbContext.Groups.Where(g => ids.Contains(g.Id)).ToListAsync();
        }
    }
}
