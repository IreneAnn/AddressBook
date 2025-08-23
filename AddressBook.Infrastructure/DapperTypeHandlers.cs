using Dapper;
using System;
using System.Data;

namespace AddressBook.Infrastructure
{
    public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
    {
        public override Guid Parse(object value)
        {
            if (value is Guid g) return g;
            if (value is string s && !string.IsNullOrWhiteSpace(s)) return Guid.Parse(s);
            if (value is byte[] b && b.Length == 16) return new Guid(b);
            if (value is DBNull) return Guid.Empty;
            return Guid.Parse(value?.ToString() ?? throw new InvalidCastException("Cannot convert to Guid."));
        }

        public override void SetValue(IDbDataParameter parameter, Guid value)
        {
            // Store as TEXT in SQLite
            parameter.Value = value.ToString();
        }
    }
}
