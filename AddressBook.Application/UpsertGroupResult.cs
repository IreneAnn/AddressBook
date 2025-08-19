using AddressBook.Application.DTO;
using AddressBook.Domain.Entities;

namespace AddressBook.Application
{
    public class UpsertGroupResult
    {
        public GroupDto GroupDto { get; set; } = default!;
        public UpsertStatus Status { get; set; }
    }
}
