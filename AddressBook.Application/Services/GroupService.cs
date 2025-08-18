using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Application.Services
{
    public class GroupService:IGroupService
    {
        private readonly IGroupRepository _groupRepository;
        public GroupService(IGroupRepository groupRepository)
        {
            _groupRepository = groupRepository;
        }

        // Replace all instances of '_repo' with '_groupRepository' to match the declared field name.

        public async Task<UpsertGroupResult?> UpsertGroupAsync(GroupDto groupDto)
        {
            try
            {
                Group group = new();
                if (groupDto.Id.HasValue)
                {
                    group = await _groupRepository.GetGroupByIdAsync(groupDto.Id.Value) ?? new Group();
                }

                group.Name = groupDto.Name;
                var upsertStatus = await _groupRepository.UpsertGroupAsync(group);
                return new UpsertGroupResult
                {
                    GroupDto = new GroupDto
                    {
                        Id = group.Id,
                        Name = group.Name
                    },
                    Status = upsertStatus
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in {nameof(UpsertGroupAsync)}: {ex}");
                return null;
            }           
        }

        public async Task<GroupDto?> GetGroupByIdAsync(Guid id)
        {            
            try
            {
                var result = await _groupRepository.GetGroupByIdAsync(id);
                if (result == null) return null;
                return new GroupDto { Id = result.Id, Name = result.Name, ContactIds = result.Contacts.Select(x => x.Id) };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in {nameof(GetGroupByIdAsync)}: {ex}");
                return null;
            }
        }

        public async Task<(IEnumerable<GroupDto> Items, int Total)> GetGroupListAsync(int page, int pageSize)
        {
            try
            {
                var (groupList, groupListCount) = await _groupRepository.GetGroupListAsync(page, pageSize);

                var dtos = groupList.Select(g => new GroupDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    ContactIds = g.Contacts.Select(x => x.Id)
                });

                return (dtos, groupListCount);
            }
            catch (Exception ex)
            {
                return (Enumerable.Empty<GroupDto>(), 0);
            }
        }
    }
}
