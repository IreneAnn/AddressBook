using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Application.Services;
using AddressBook.Domain.Entities;
using Castle.Core.Logging;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Tests.ServiceTests
{
    public class ContactServiceTests
    {
        private readonly Mock<IContactRepository> _mockContactRepo;
        private readonly Mock<IGroupRepository> _mockGroupRepo;
        private readonly Mock<ILogger<ContactService>> _mockLogger;
        private readonly ContactService _service;

        public ContactServiceTests()
        {
            _mockContactRepo = new Mock<IContactRepository>();
            _mockGroupRepo = new Mock<IGroupRepository>();
            _mockLogger = new Mock<ILogger<ContactService>>();
            _service = new ContactService(_mockContactRepo.Object, _mockGroupRepo.Object, _mockLogger.Object);
        }

        #region UpsertContactAsync Tests

        [Fact]
        public async Task UpsertContactAsync_ShouldCreateNewContact_WhenIdIsNull()
        {
            // Arrange
            var contactDto = new ContactDto
            {
                FirstName = "John",
                LastName = "Doe",
                PhoneNumber = "123456",
                Email = "john@example.com",
                GroupIds = new List<Guid>()
            };

            _mockContactRepo.Setup(r => r.UpsertContactAsync(It.IsAny<Contact>()))
                .ReturnsAsync(UpsertStatus.Created);

            // Act
            var result = await _service.UpsertContactAsync(contactDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Created",result.Status.ToString());
            Assert.Equal("John", result.ContactDto.FirstName);
        }

        [Fact]
        public async Task UpsertContactAsync_ShouldUpdateExistingContact_WhenIdIsProvided()
        {
            // Arrange
            var contactId = Guid.NewGuid();
            var existingContact = new Contact { Id = contactId, FirstName = "Timothy" };
            var contactDto = new ContactDto { Id = contactId, FirstName = "Tom", GroupIds = new List<Guid>() };

            _mockContactRepo.Setup(r => r.GetContactByIdAsync(contactId)).ReturnsAsync(existingContact);
            _mockContactRepo.Setup(r => r.UpsertContactAsync(It.IsAny<Contact>())).ReturnsAsync(UpsertStatus.Updated);

            // Act
            var result = await _service.UpsertContactAsync(contactDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Tom", result.ContactDto.FirstName);
        }

        [Fact]
        public async Task UpsertContactAsync_ShouldAddGroups_WhenGroupIdsProvided()
        {
            // Arrange
            var contactDto = new ContactDto
            {
                FirstName = "Alice",
                LastName = "Smith",
                GroupIds = new List<Guid> { Guid.NewGuid() }
            };

            _mockGroupRepo.Setup(r => r.GetGroupsByIdsAsync(contactDto.GroupIds.ToList()))
                          .ReturnsAsync(new List<Group> { new Group { Id = contactDto.GroupIds.First(), Name = "Friends" } });

            _mockContactRepo.Setup(r => r.UpsertContactAsync(It.IsAny<Contact>())).ReturnsAsync(UpsertStatus.Created);

            // Act
            var result = await _service.UpsertContactAsync(contactDto);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.ContactDto.GroupIds);
            Assert.Equal(UpsertStatus.Created,result.Status);
        }

        #endregion

        #region GetContactByIdAsync Tests

        [Fact]
        public async Task GetContactByIdAsync_ShouldReturnContactDto_WhenContactExists()
        {
            // Arrange
            var id = Guid.NewGuid();
            var contact = new Contact
            {
                Id = id,
                FirstName = "Jane",
                LastName = "Doe",
                Groups = new List<Group>()
            };
            _mockContactRepo.Setup(r => r.GetContactByIdAsync(id)).ReturnsAsync(contact);

            // Act
            var result = await _service.GetContactByIdAsync(id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Jane", result.FirstName);
        }

        [Fact]
        public async Task GetContactByIdAsync_ShouldReturnNull_WhenContactDoesNotExist()
        {
            // Arrange
            _mockContactRepo.Setup(r => r.GetContactByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Contact?)null);

            // Act
            var result = await _service.GetContactByIdAsync(Guid.NewGuid());

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetContactListAsync Tests

        [Fact]
        public async Task GetContactListAsync_ShouldReturnListOfContactDtos()
        {
            // Arrange
            var contacts = new List<Contact>
            {
                new Contact { Id = Guid.NewGuid(), FirstName = "A", Groups = new List<Group>() },
                new Contact { Id = Guid.NewGuid(), FirstName = "B", Groups = new List<Group>() }
            };

            _mockContactRepo.Setup(r => r.GetContactListAsync(1, 10)).ReturnsAsync((contacts, contacts.Count));

            // Act
            var (items, total) = await _service.GetContactListAsync(1, 10);

            // Assert
            Assert.Equal(2, total);
            Assert.Equal(2, items.Count());
        }

        #endregion
    }
}