using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Application.Services;
using AddressBook.Infrastructure;
using AddressBook.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle


// SQLite
var conn = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=addressbook.db";
builder.Services.AddDbContext<AddressBookDbContext>(options =>
    options.UseSqlite(conn));

//DI
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure DB + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AddressBookDbContext>();
    db.Database.EnsureCreated();
    //await SeedData.InitializeAsync(db, scope.ServiceProvider); //DELETE*****
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
