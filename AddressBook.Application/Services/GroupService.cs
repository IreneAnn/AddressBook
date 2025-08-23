using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
        private readonly IMemoryCache _cache;
        private readonly ILogger<GroupService> _logger;
        public GroupService(IGroupRepository groupRepository, IMemoryCache cache, ILogger<GroupService> logger)
        {
            _groupRepository = groupRepository;
            _cache = cache;
            _logger = logger;
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
                    _logger.LogInformation("Retrieved existing group for Id: {GroupId}", groupDto.Id);
                }

                group.Name = groupDto.Name;
                var upsertStatus = await _groupRepository.UpsertGroupAsync(group);
                _logger.LogInformation("Group upsert completed successfully for Id: {GroupId}, Status: {Status}", group.Id, upsertStatus);

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
                _logger.LogError(ex, "Error in {MethodName} for Group Id: {GroupId}", nameof(UpsertGroupAsync), groupDto.Id);
                return null;
            }           
        }

        public async Task<GroupDto?> GetGroupByIdAsync(Guid id)
        {            
            try
            {
                var result = await _groupRepository.GetGroupByIdAsync(id);
                if (result == null) return null;

                _logger.LogInformation("Group with Id {GroupId} retrieved successfully", id);
                return new GroupDto { Id = result.Id, Name = result.Name, ContactIds = result.Contacts.Select(x => x.Id) };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {MethodName} for Group Id: {GroupId}", nameof(GetGroupByIdAsync), id);
                return null;
            }
        }

        public async Task<(IEnumerable<GroupDto> Items, int Total)> GetGroupListAsync(int page, int pageSize)
        {
            try
            {
                var cacheKey = $"groups_{page}_{pageSize}";
                if (!_cache.TryGetValue(cacheKey, out (IEnumerable<GroupDto> Items, int Total) cached))
                {
                    var (groupList, total) = await _groupRepository.GetGroupListAsync(page, pageSize);
                    var dtos = groupList.Select(g => new GroupDto
                    {
                        Id = g.Id,
                        Name = g.Name,
                        ContactIds = g.Contacts.Select(x => x.Id)
                    }).ToList();

                    cached = (dtos, total);
                    _cache.Set(cacheKey, cached, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                    });
                    _logger.LogInformation("Cache MISS for {CacheKey}. Stored {Count} items of {Total}", cacheKey, dtos.Count, total);
                }
                else
                {
                    _logger.LogInformation("Cache HIT for {CacheKey}", cacheKey);
                }
                _logger.LogInformation("Retrieved {Count} groups out of total {Total}", cached.Items.Count(), cached.Total);
                return cached;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {MethodName} while fetching group list", nameof(GetGroupListAsync));
                return (Enumerable.Empty<GroupDto>(), 0);
            }
        }
    }
}
