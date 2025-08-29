using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AddressBook.Domain.Entities;
using AddressBook.Infrastructure;
using AddressBook.Infrastructure.Repositories;
using Dapper;
using Moq;
using Moq.Dapper;
using Xunit;

namespace AddressBook.Tests.RepositoryTests
{
    public class GroupRepositoryTests
    {
        private readonly Mock<DapperContext> _mockContext;
        private readonly Mock<IDbConnection> _mockConn;
        private readonly Mock<IDbTransaction> _mockTx;
        private readonly GroupRepository _repo;

        public GroupRepositoryTests()
        {
            _mockContext = new Mock<DapperContext>(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
            _mockConn = new Mock<IDbConnection>(MockBehavior.Strict);
            _mockTx = new Mock<IDbTransaction>(MockBehavior.Strict);

            // Dapper inspects ConnectionString and State internally; provide values for strict mocks
            _mockConn.SetupProperty(c => c.ConnectionString, "Data Source=:memory:");
            _mockConn.SetupGet(c => c.State).Returns(ConnectionState.Open);
            _mockConn.Setup(c => c.Open());
            _mockConn.Setup(c => c.Dispose());

            _mockContext.Setup(c => c.CreateConnection()).Returns(_mockConn.Object);
            _repo = new GroupRepository(_mockContext.Object);
        }
                   
        [Fact]
        public async Task GetGroupByIdAsync_Returns_Null_When_Not_Found()
        {
            _mockConn.SetupDapperAsync(c => c.QuerySingleOrDefaultAsync<Group>(
                    It.Is<string>(s => s.Contains("FROM Groups")),
                    It.IsAny<object>(), null, null, null))
                .ReturnsAsync((Group?)null);

            var got = await _repo.GetGroupByIdAsync(Guid.NewGuid());
            Assert.Null(got);
        }

        [Fact]
        public async Task GetGroupByIdAsync_Returns_Group_With_Contacts()
        {
            var id = Guid.NewGuid();
            var group = new Group { Id = id, Name = "G" };
            var contacts = new List<Contact> { new Contact { Id = Guid.NewGuid(), FirstName = "A" } } as IEnumerable<Contact>;

            _mockConn.SetupDapperAsync(c => c.QuerySingleOrDefaultAsync<Group>(
                    It.Is<string>(s => s.Contains("FROM Groups")),
                    It.IsAny<object>(), null, null, null))
                .ReturnsAsync(group);
            // Also cover CommandDefinition overload
            _mockConn.SetupDapperAsync(c => c.QuerySingleOrDefaultAsync<Group>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync(group);

            // contacts per group: repository uses string overload here, so cover both
            _mockConn.SetupDapperAsync(c => c.QueryAsync<Contact>(
                    It.Is<string>(s => s.Contains("FROM Contacts")),
                    It.IsAny<object>(), null, null, null))
                .ReturnsAsync(contacts);
            // contacts per group via CommandDefinition
            _mockConn.SetupDapperAsync(c => c.QueryAsync<Contact>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync(contacts);
            _mockConn.SetupDapperAsync(c => c.QueryAsync<Contact>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync(contacts);

            var got = await _repo.GetGroupByIdAsync(id);
            Assert.NotNull(got);
            Assert.NotEqual(Guid.Empty, got!.Id);
            Assert.Single(got.Contacts);
        }
               

        [Fact]
        public async Task GetGroupsByIdsAsync_Returns_Filtered_List()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var groups = new List<Group> { new Group { Id = id1, Name = "A" } } as IEnumerable<Group>;

            _mockConn.SetupDapperAsync(c => c.QueryAsync<Group>(
                    It.Is<string>(s => s.Contains("WHERE Id IN")),
                    It.IsAny<object>(), null, null, null))
                .ReturnsAsync(groups);

            var result = await _repo.GetGroupsByIdsAsync(new List<Guid> { id1, id2 });
            Assert.Single(result);
            Assert.Equal(id1, result[0].Id);

            var empty = await _repo.GetGroupsByIdsAsync(new List<Guid>());
            Assert.Empty(empty);
        }
    }
}
