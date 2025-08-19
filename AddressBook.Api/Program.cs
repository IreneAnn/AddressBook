using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Application.Services;
using AddressBook.Infrastructure;
using AddressBook.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------
// Add services
// ---------------------------

// SQLite DB
var conn = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=addressbook.db";
builder.Services.AddDbContext<AddressBookDbContext>(options =>
{
    options.UseSqlite(conn)
           .UseOpenIddict(); // Required for OpenIddict entities
});

// OpenIddict configuration
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<AddressBookDbContext>();
    })
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token");
        options.AllowClientCredentialsFlow();
        options.AcceptAnonymousClients();
        // Register the signing and encryption credentials.
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();
        options.UseAspNetCore()
              .EnableTokenEndpointPassthrough();

    });
    

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "";
        options.Audience = "addressbook.api";
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

// DI for services
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------------------------
// Middleware
// ---------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

foreach (var endpoint in app.Services.GetRequiredService<EndpointDataSource>().Endpoints)
{
    Console.WriteLine(endpoint.DisplayName);
}

// In your startup scope:
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AddressBookDbContext>();
    db.Database.Migrate();

    var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    await SeedOpenIddictClients(manager);

    var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
    await SeedOpenIddictScopes(scopeManager);
}

app.Run();

static async Task SeedOpenIddictScopes(IOpenIddictScopeManager scopeManager)
{
    if (await scopeManager.FindByNameAsync("addressbook.api") == null)
    {
        await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = "addressbook.api",
            DisplayName = "Address Book API access"
        });
    }
}


static async Task SeedOpenIddictClients(IOpenIddictApplicationManager manager)
{
    if (await manager.FindByClientIdAsync("addressbook.client") == null)
    {
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "addressbook.client",
            ClientSecret = "secret",
            DisplayName = "AddressBook Test Client",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + "addressbook.api"
            }
        });
    }
}

