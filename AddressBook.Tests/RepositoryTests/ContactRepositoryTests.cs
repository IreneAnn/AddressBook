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
    public class ContactRepositoryTests
    {
        private readonly Mock<DapperContext> _mockContext;
        private readonly Mock<IDbConnection> _mockConn;
        private readonly Mock<IDbTransaction> _mockTx;
        private readonly ContactRepository _repo;

        public ContactRepositoryTests()
        {
            _mockContext = new Mock<DapperContext>(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
            _mockConn = new Mock<IDbConnection>(MockBehavior.Strict);
            _mockTx = new Mock<IDbTransaction>(MockBehavior.Strict);

            // Common connection behaviors
            // Dapper inspects ConnectionString internally; provide a value for strict mocks
            _mockConn.SetupProperty(c => c.ConnectionString, "Data Source=:memory:");
            _mockConn.SetupGet(c => c.State).Returns(ConnectionState.Open);
            _mockConn.Setup(c => c.Open());
            _mockConn.Setup(c => c.Dispose());

            _mockContext.Setup(c => c.CreateConnection()).Returns(_mockConn.Object);
            _repo = new ContactRepository(_mockContext.Object);
        }

        [Fact]
        public async Task GetContactByIdAsync_Returns_Null_When_Not_Found()
        {
            _mockConn.SetupDapperAsync(c => c.QuerySingleOrDefaultAsync<Contact>(
                    It.Is<string>(s => s.Contains("FROM Contacts")),
                    It.IsAny<object>(), null, null, null))
                .ReturnsAsync((Contact?)null);

            var result = await _repo.GetContactByIdAsync(Guid.NewGuid());

            Assert.Null(result);
            _mockContext.Verify(c => c.CreateConnection(), Times.Once);
        }

        [Fact]
        public async Task GetContactByIdAsync_Returns_Contact_With_Groups()
        {
            var id = Guid.NewGuid();
            var contact = new Contact { Id = id, FirstName = "John" };
            var groups = new List<Group> { new Group { Id = Guid.NewGuid(), Name = "Friends" } } as IEnumerable<Group>;

            _mockConn.SetupDapperAsync(c => c.QuerySingleOrDefaultAsync<Contact>(
                    It.Is<string>(s => s.Contains("FROM Contacts")),
                    It.IsAny<object>(), null, null, null))
                .ReturnsAsync(contact);
            // Also cover CommandDefinition overload
            _mockConn.SetupDapperAsync(c => c.QuerySingleOrDefaultAsync<Contact>(
                    It.IsAny<CommandDefinition>()))
                .ReturnsAsync(contact);

            _mockConn.SetupDapperAsync(c => c.QueryAsync<Group>(
                    It.IsAny<string>(),
                    It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>(), It.IsAny<CommandType?>()))
                .ReturnsAsync(groups);
            _mockConn.SetupDapperAsync(c => c.QueryAsync<Group>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync(groups);
            _mockConn.SetupDapperAsync(c => c.QueryAsync<Group>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync(groups);

            var result = await _repo.GetContactByIdAsync(id);

            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result!.Id);
            Assert.Single(result.Groups);
        }            
             
    }
}
