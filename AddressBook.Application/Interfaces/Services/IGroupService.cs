using AddressBook.Application.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Application.Interfaces.Services
{
    public interface IGroupService
    {       
       
        Task<UpsertGroupResult?> UpsertGroupAsync(GroupDto dto);
        Task<GroupDto?> GetGroupByIdAsync(Guid id);
        Task<(IEnumerable<GroupDto> Items, int Total)> GetGroupListAsync(int page, int pageSize);
    }
}
