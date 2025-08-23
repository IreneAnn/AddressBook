using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Application.Services
{
    /// <summary>
    /// ContactService class provides methods to manage contacts in the address book.
    /// </summary>
    public class ContactService : IContactService
    {
        private readonly IContactRepository _contactRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly ILogger<ContactService> _logger;
        public ContactService(IContactRepository contactRepository,IGroupRepository groupRepository, ILogger<ContactService> logger)
        {
            _contactRepository = contactRepository;
            _groupRepository = groupRepository;
            _logger = logger;
        }

        public async Task<UpsertContactResult?> UpsertContactAsync(ContactDto contactDto)
        {
            try
            {
                Contact contact= new();
                if (contactDto.Id.HasValue)
                {
                    contact = await _contactRepository.GetContactByIdAsync(contactDto.Id.Value) ?? new Contact();
                    _logger.LogInformation("Fetching contact with Id: {ContactId}", contactDto.Id);
                }

                contact.FirstName = contactDto.FirstName;
                contact.LastName = contactDto.LastName;
                contact.PhoneNumber = contactDto.PhoneNumber;
                contact.Email = contactDto.Email;

                // Load groups by id
                var groups = new List<Group>();
                if (contactDto.GroupIds.Any())
                {
                    groups = await _groupRepository.GetGroupsByIdsAsync([.. contactDto.GroupIds]);
                    if (groups != null && groups.Any())
                    {
                        foreach (var group in groups)
                        {
                            contact.Groups.Add(group); // Add new groups to the existing list of groups in a contact
                            _logger.LogDebug("Added group {GroupId} to contact {ContactId}", group.Id, contact.Id);
                        }
                        //contact.Groups = groups; // Rewrite the old groups with the new groups
                    }
                }

                var upsertStatus = await _contactRepository.UpsertContactAsync(contact);
                _logger.LogInformation("Contact upsert completed. Status: {Status}, ContactId: {ContactId}", upsertStatus, contact.Id);

                return new UpsertContactResult
                {
                    ContactDto = new ContactDto
                    {
                        Id = contact.Id,
                        FirstName = contact.FirstName,
                        LastName = contact.LastName,
                        PhoneNumber = contact.PhoneNumber,
                        Email = contact.Email,
                        GroupIds = contact.Groups.Select(g => g.Id)
                    },
                    Status = upsertStatus
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {MethodName} for ContactDto: {@ContactDto}", nameof(UpsertContactAsync), contactDto);
                throw;
            }           
            
        }

        public async Task<ContactDto?> GetContactByIdAsync(Guid id)
        {
            try
            {
                var contact = await _contactRepository.GetContactByIdAsync(id);
                if (contact == null) return null;
                return new ContactDto { Id = contact.Id, FirstName = contact.FirstName, LastName = contact.LastName, PhoneNumber = contact.PhoneNumber, Email = contact.Email, GroupIds = contact.Groups.Select(g => g.Id) };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {MethodName} for ContactId: {ContactId}", nameof(GetContactByIdAsync), id);
                return null;
            }
        }

        public async Task<(IEnumerable<ContactDto> Items, int Total)> GetContactListAsync(int page, int pageSize)
        {

            try
            {
                var (items, contactListCount) = await _contactRepository.GetContactListAsync(page, pageSize);

                var dtos = items.Select(c => new ContactDto
                {
                    Id = c.Id,
                    FirstName = c.FirstName,
                    LastName = c.LastName,
                    PhoneNumber = c.PhoneNumber,
                    Email = c.Email,
                    GroupIds = c.Groups.Select(g => g.Id)
                });

                _logger.LogInformation("Fetched {Count} contacts out of total {TotalCount}", dtos.Count(), contactListCount);
                return (dtos, contactListCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {MethodName} while fetching contacts. Page: {Page}, PageSize: {PageSize}", nameof(GetContactListAsync), page, pageSize);
                return (Enumerable.Empty<ContactDto>(), 0);
            }
            
        }

    }
}
