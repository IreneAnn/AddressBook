using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Application.Services;
using AddressBook.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace AddressBook.Tests.ServiceTests
{
    public class GroupServiceTests
    {
        private readonly Mock<IGroupRepository> _mockRepo;
        private readonly Mock<ILogger<GroupService>> _mockLogger;
        private readonly GroupService _service;

        public GroupServiceTests()
        {
            _mockRepo = new Mock<IGroupRepository>();
            _mockLogger = new Mock<ILogger<GroupService>>();
            _service = new GroupService(_mockRepo.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task UpsertGroupAsync_Should_CreateNewGroup_When_IdIsNull()
        {
            // Arrange
            var groupDto = new GroupDto { Name = "Test Group" };
            _mockRepo.Setup(r => r.UpsertGroupAsync(It.IsAny<Group>()))
                     .ReturnsAsync(UpsertStatus.Created);

            // Act
            var result = await _service.UpsertGroupAsync(groupDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Group", result!.GroupDto.Name);
            Assert.Equal("Created", result.Status.ToString());
            _mockRepo.Verify(r => r.UpsertGroupAsync(It.IsAny<Group>()), Times.Once);
        }

        [Fact]
        public async Task UpsertGroupAsync_Should_UpdateExistingGroup_When_IdIsSet()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            var groupDto = new GroupDto { Id = groupId, Name = "Updated Group" };
            var existingGroup = new Group { Id = groupId, Name = "Old Name" };

            _mockRepo.Setup(r => r.GetGroupByIdAsync(groupId)).ReturnsAsync(existingGroup);
            _mockRepo.Setup(r => r.UpsertGroupAsync(It.IsAny<Group>())).ReturnsAsync(UpsertStatus.Updated);

            // Act
            var result = await _service.UpsertGroupAsync(groupDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated Group", result!.GroupDto.Name);
            _mockRepo.Verify(r => r.GetGroupByIdAsync(groupId), Times.Once);
            _mockRepo.Verify(r => r.UpsertGroupAsync(It.IsAny<Group>()), Times.Once);
        }

        [Fact]
        public async Task GetGroupByIdAsync_Should_ReturnGroupDto_WhenGroupExists()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            var group = new Group
            {
                Id = groupId,
                Name = "Sample Group",
                Contacts = new List<Contact> { new Contact { Id = Guid.NewGuid() } }
            };

            _mockRepo.Setup(r => r.GetGroupByIdAsync(groupId)).ReturnsAsync(group);

            // Act
            var result = await _service.GetGroupByIdAsync(groupId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(groupId, result!.Id);
            Assert.Equal("Sample Group", result.Name);
            Assert.Single(result.ContactIds);
        }

        [Fact]
        public async Task GetGroupByIdAsync_Should_ReturnNull_WhenGroupDoesNotExist()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            _mockRepo.Setup(r => r.GetGroupByIdAsync(groupId)).ReturnsAsync((Group?)null);

            // Act
            var result = await _service.GetGroupByIdAsync(groupId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetGroupListAsync_Should_ReturnGroupDtosAndCount()
        {
            // Arrange
            var groups = new List<Group>
        {
            new Group { Id = Guid.NewGuid(), Name = "Group1", Contacts = new List<Contact>() },
            new Group { Id = Guid.NewGuid(), Name = "Group2", Contacts = new List<Contact>() }
        };
            int total = 2;

            _mockRepo.Setup(r => r.GetGroupListAsync(1, 10))
                     .ReturnsAsync((groups, total));

            // Act
            var (items, count) = await _service.GetGroupListAsync(1, 10);

            // Assert
            Assert.Equal(total, count);
            Assert.Equal(2, items.Count());
            Assert.Contains(items, g => g.Name == "Group1");
            Assert.Contains(items, g => g.Name == "Group2");
        }
    }
}