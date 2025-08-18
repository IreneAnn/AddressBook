using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressBook.Application.DTO
{
    public class GroupDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public IEnumerable<Guid> ContactIds { get; set; } = new List<Guid>();
    }
}
