using AddressBook.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AddressBook.Infrastructure
{
    public class AddressBookDbContext : DbContext
    {
        public AddressBookDbContext(DbContextOptions<AddressBookDbContext> options)
            : base(options)
        {
        }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Group> Groups { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseOpenIddict(); // adds OpenIddict models to the EF Core model

            modelBuilder.Entity<Contact>(b =>
            {
                b.HasKey(c => c.Id);
                b.Property(c => c.FirstName).HasMaxLength(200);
                b.Property(c => c.LastName).HasMaxLength(200);
                b.HasMany(c => c.Groups).WithMany(g => g.Contacts).UsingEntity(j => j.ToTable("ContactGroups")); // EF core automatically creates ContactGroups
            });

            modelBuilder.Entity<Group>(b =>
            {
                b.HasKey(g => g.Id);
                b.Property(g => g.Name).HasMaxLength(200);
            });
          
        }
    }   
    
}
