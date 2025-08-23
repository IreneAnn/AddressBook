using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Infrastructure
{
    public class DapperContext
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private static bool _handlersConfigured;

        public DapperContext(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=addressbook.db"; ;

            // Register Dapper type handlers once
            if (!_handlersConfigured)
            {
                SqlMapper.AddTypeHandler(new GuidTypeHandler());
                // Ensure Dapper doesn't try its own mapping first
                SqlMapper.RemoveTypeMap(typeof(Guid));
                SqlMapper.RemoveTypeMap(typeof(Guid?));
                _handlersConfigured = true;
            }
        }

        public IDbConnection CreateConnection() => new SqliteConnection(_connectionString);
    }
}