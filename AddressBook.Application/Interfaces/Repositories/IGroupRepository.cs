using AddressBook.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Application.Interfaces.Repositories
{
    public interface IGroupRepository
    {
        Task<UpsertStatus> UpsertGroupAsync(Group group);
        Task<Group?> GetGroupByIdAsync(Guid id);
        Task<(IEnumerable<Group> Items, int Total)> GetGroupListAsync(int page, int pageSize);
        Task<List<Group>> GetGroupsByIdsAsync(List<Guid> ids);
    }
}
