using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Domain.Entities;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Infrastructure.Repositories
{
    public class GroupRepository : IGroupRepository
    {
        private readonly DapperContext _context;
        public GroupRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<UpsertStatus> UpsertGroupAsync(Group newGroup)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            if (newGroup.Id == Guid.Empty)
            {
                newGroup.Id = Guid.NewGuid();
            }

            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Groups WHERE Id = @Id COLLATE NOCASE",
                new { Id = newGroup.Id.ToString() }, transaction: transaction);

            UpsertStatus upsertStatus;
            if (exists == 0)
            {
                const string insertSql = "INSERT INTO Groups (Id, Name) VALUES (@Id, @Name)";
                await connection.ExecuteAsync(insertSql, newGroup, transaction);
                upsertStatus = UpsertStatus.Created;
            }
            else
            {
                const string updateSql = "UPDATE Groups SET Name = @Name WHERE Id = @Id";
                await connection.ExecuteAsync(updateSql, newGroup, transaction);
                upsertStatus = UpsertStatus.Updated;
            }

            transaction.Commit();
            return upsertStatus;
        }

        public async Task<Group?> GetGroupByIdAsync(Guid id)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            const string groupSql = "SELECT Id, Name FROM Groups WHERE Id = @Id COLLATE NOCASE";
            var group = await connection.QuerySingleOrDefaultAsync<Group>(groupSql, new { Id = id.ToString() });
            if (group == null) return null;

            const string contactsSql = @"SELECT c.Id, c.FirstName, c.LastName, c.Email, c.PhoneNumber
                                         FROM Contacts c
                                         INNER JOIN ContactGroups cg ON c.Id = cg.ContactsId
                                         WHERE cg.GroupsId = @Id COLLATE NOCASE";
            var contacts = await connection.QueryAsync<Contact>(contactsSql, new { Id = id.ToString() });
            group.Contacts = contacts.ToList();
            return group;
        }

        public async Task<(IEnumerable<Group> Items, int Total)> GetGroupListAsync(int page, int pageSize)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            var total = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Groups");
            var limit = pageSize;
            var offset = (page - 1) * pageSize;
            var items = (await connection.QueryAsync<Group>(
                "SELECT Id, Name FROM Groups ORDER BY Name LIMIT @limit OFFSET @offset",
                new { limit, offset })).ToList();

            const string contactsSql = @"SELECT c.Id, c.FirstName, c.LastName, c.Email, c.PhoneNumber
                                         FROM Contacts c
                                         INNER JOIN ContactGroups cg ON c.Id = cg.ContactsId
                                         WHERE cg.GroupsId = @Id COLLATE NOCASE";
            foreach (var g in items)
            {
                var contacts = await connection.QueryAsync<Contact>(contactsSql, new { Id = g.Id.ToString() });
                g.Contacts = contacts.ToList();
            }
            return (items, total);
        }

        public async Task<List<Group>> GetGroupsByIdsAsync(List<Guid> ids)
        {
            if (ids == null || ids.Count == 0) return new List<Group>();
            using var connection = _context.CreateConnection();
            var stringIds = ids.Select(x => x.ToString().ToUpper()).ToList();
            var groups = await connection.QueryAsync<Group>("SELECT Id, Name FROM Groups WHERE Id IN @Ids", new { Ids = stringIds });
            return groups.ToList();
        }
    }
}
