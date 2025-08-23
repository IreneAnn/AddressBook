using AddressBook.Application.DTO;
using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Domain.Entities;
using Dapper;
using System.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Infrastructure.Repositories
{
    public class ContactRepository : IContactRepository
    {
        private readonly DapperContext _context;

        public ContactRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<UpsertStatus> UpsertContactAsync(Contact newContact)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            if (newContact.Id == Guid.Empty)
            {
                newContact.Id = Guid.NewGuid();
            }

            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Contacts WHERE Id = @Id COLLATE NOCASE",
                new { Id = newContact.Id.ToString() }, transaction: transaction);

            UpsertStatus upsertStatus;
            if (exists == 0)
            {
                const string insertSql = @"INSERT INTO Contacts (Id, FirstName, LastName, Email, PhoneNumber)
                                            VALUES (@Id, @FirstName, @LastName, @Email, @PhoneNumber);";
                await connection.ExecuteAsync(insertSql, newContact, transaction);
                upsertStatus = UpsertStatus.Created;
            }
            else
            {
                const string updateSql = @"UPDATE Contacts
                                            SET FirstName = @FirstName,
                                                LastName = @LastName,
                                                Email = @Email,
                                                PhoneNumber = @PhoneNumber
                                            WHERE Id = @Id;";
                await connection.ExecuteAsync(updateSql, newContact, transaction);
                upsertStatus = UpsertStatus.Updated;
            }

            // Sync groups mapping
            const string deleteMappings = "DELETE FROM ContactGroups WHERE ContactsId = @Id COLLATE NOCASE";
            await connection.ExecuteAsync(deleteMappings, new { Id = newContact.Id.ToString() }, transaction);

            if (newContact.Groups != null && newContact.Groups.Any())
            {
                const string insertMapping = "INSERT INTO ContactGroups (ContactsId, GroupsId) VALUES (@ContactsId, @GroupsId)";
                var mappingParams = newContact.Groups
                    .Select(g => g.Id)
                    .Distinct()
                    .Select(id => new { ContactsId = newContact.Id, GroupsId = id });
                await connection.ExecuteAsync(insertMapping, mappingParams, transaction);
            }

            transaction.Commit();
            return upsertStatus;
        }

        public async Task<Contact?> GetContactByIdAsync(Guid id)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            const string contactSql = @"SELECT Id, FirstName, LastName, Email, PhoneNumber
                                         FROM Contacts WHERE Id = @Id COLLATE NOCASE";
            var contact = await connection.QuerySingleOrDefaultAsync<Contact>(contactSql, new { Id = id.ToString() });
            if (contact == null)
                return null;

            const string groupsSql = @"SELECT g.Id, g.Name
                                       FROM Groups g
                                       INNER JOIN ContactGroups cg ON g.Id = cg.GroupsId
                                       WHERE cg.ContactsId = @Id COLLATE NOCASE";
            var groups = await connection.QueryAsync<Group>(groupsSql, new { Id = id.ToString() });
            contact.Groups = groups.ToList();
            return contact;
        }

        public async Task<(IEnumerable<Contact> Items, int Total)> GetContactListAsync(int page, int pageSize)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            var total = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Contacts");

            var limit = pageSize;
            var offset = (page - 1) * pageSize;
            var items = (await connection.QueryAsync<Contact>(
                "SELECT Id, FirstName, LastName, Email, PhoneNumber FROM Contacts ORDER BY FirstName LIMIT @limit OFFSET @offset",
                new { limit, offset })).ToList();

            // Load groups for each contact (simple and clear; acceptable for small page sizes)
            const string groupsSql = @"SELECT g.Id, g.Name
                                       FROM Groups g
                                       INNER JOIN ContactGroups cg ON g.Id = cg.GroupsId
                                       WHERE cg.ContactsId = @Id";
            foreach (var c in items)
            {
                var groups = await connection.QueryAsync<Group>(groupsSql, new { Id = c.Id.ToString() });
                c.Groups = groups.ToList();
            }

            return (items, total);
        }
    }
   
}
