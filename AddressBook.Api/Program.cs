using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Application.Services;
using AddressBook.Infrastructure;
using AddressBook.Infrastructure.Repositories;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using System;
using System.Linq;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Ensure Kestrel server is registered explicitly (safety on some hosts)
builder.WebHost.UseKestrel();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080); // HTTP only for local/dev

});

// ---------------------------
// Add services
// ---------------------------

//Dapper
builder.Services.AddSingleton<DapperContext>();
// Register Guid handler for Dapper
SqlMapper.AddTypeHandler(new AddressBook.Infrastructure.GuidTypeHandler());

// OpenTelemetry + Azure Monitor exporter (reads APPLICATIONINSIGHTS_CONNECTION_STRING)
builder.Services.AddOpenTelemetry()
    .UseAzureMonitor();

// SQLite DB
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(conn))
{
    // Decide base directory based on hosting environment
    var onAzure = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME"));
    var inContainer = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
    string baseDir;
    if (onAzure)
    {
        // Azure App Service persistent mount
        baseDir = "/home/data";
    }
    else if (inContainer)
    {
        // Local/container run (Docker)
        baseDir = "/app/data";
    }
    else
    {
        // Local "dotnet run" scenario -> next to binaries
        baseDir = Path.Combine(AppContext.BaseDirectory, "data");
    }

    try { Directory.CreateDirectory(baseDir); } catch { /* ignore */ }
    var dbPath = Path.Combine(baseDir, "addressbook.db").Replace("\\", "/");
    conn = $"Data Source={dbPath}";
}
else
{
    // Best effort: ensure directory exists for provided Data Source path
    try
    {
        var marker = "Data Source=";
        var idx = conn.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var after = conn.Substring(idx + marker.Length);
            var end = after.IndexOf(';');
            var path = end >= 0 ? after.Substring(0, end) : after;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            }
        }
    }
    catch { /* ignore */ }
}

builder.Services.AddDbContext<AddressBookDbContext>(options =>
{
    options.UseSqlite(conn)
           .UseOpenIddict(); // Required for OpenIddict entities
});

builder.Services.AddMemoryCache();

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

        // Register signing/encryption credentials without requiring dev certificates.
        // Ephemeral keys are fine for local/dev and avoid certificate setup.

        options.AddEphemeralEncryptionKey()
                        .AddEphemeralSigningKey()
                        .DisableAccessTokenEncryption();


        // If you want to allow HTTP (disable HTTPS requirement) for development, use:
        options.UseAspNetCore()
            .EnableTokenEndpointPassthrough()
            .DisableTransportSecurityRequirement();


    })
    .AddValidation(options =>
    {
        // Validate tokens issued by this local server.
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReadScope", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.HasClaim(c =>
                c.Type == "scope" && c.Value.Split(' ').Contains("addressbook.read")));
    });

    options.AddPolicy("WriteScope", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            var scopesClaim = context.User.FindFirst(c => c.Type == "scope")?.Value ?? "";
            var scopes = scopesClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return scopes.Contains("addressbook.read") && scopes.Contains("addressbook.write");
        });
    });
});

builder.Services.AddControllers();

// DI for services
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AddressBook API", Version = "v1" });

    // Determine an HTTP base URL for the token endpoint (helps when running without HTTPS).
    // Prefer explicit override via env/config; fall back to ASPNETCORE_URLS; normalize '+' host to 'localhost'.
    var overrideBase = Environment.GetEnvironmentVariable("SWAGGER_BASE_URL")
                       ?? builder.Configuration["Swagger:BaseUrl"];
    string baseForSwagger;
    if (!string.IsNullOrWhiteSpace(overrideBase))
    {
        baseForSwagger = overrideBase!;
    }
    else
    {
        var urls = (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidate = urls.FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                       ?? "http://localhost:8080";
        // Replace wildcard host '+' with 'localhost' to make it a valid absolute URI.
        baseForSwagger = candidate.Replace("http://+:", "http://localhost:", StringComparison.OrdinalIgnoreCase)
                                  .Replace("https://+:", "https://localhost:", StringComparison.OrdinalIgnoreCase);
    }
    var tokenUrl = new Uri(new Uri(baseForSwagger), "/connect/token");

    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = tokenUrl,
                Scopes = new Dictionary<string, string>
            {
                { "addressbook.read", "Read access to Address Book API" },
                { "addressbook.write", "Write access to Address Book API" }
            }
            }
        }
    });


    // Apply to all endpoints
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
           new[] { "addressbook.read", "addressbook.write" } // both required for WriteScope endpoints
        }
    });
});

var app = builder.Build();

// Log app version when the application has fully started
app.Lifetime.ApplicationStarted.Register(() =>
{
    var version = "1.0.1";
    app.Logger.LogInformation(
        "AddressBook API started. Version:{Version}",
        version
    );
});

//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AddressBook API v1");

    // OAuth2 Client Credentials setup
    c.OAuthClientId(builder.Configuration["Auth:ClientId"] ?? "addressbook.client");
    c.OAuthClientSecret(builder.Configuration["Auth:ClientSecret"] ?? "secret");
    c.OAuthScopes("addressbook.read", "addressbook.write");
});
//}

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
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var clientId = config["Auth:ClientId"] ?? "addressbook.client";
    var clientSecret = config["Auth:ClientSecret"] ?? "secret";
    await SeedOpenIddictClients(manager, clientId, clientSecret);

    var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
    await SeedOpenIddictScopes(scopeManager);

    // Seed app data using Dapper, not EF
    var dapper = scope.ServiceProvider.GetRequiredService<DapperContext>();
    await SeedAppData(dapper);
}

app.Run();


static async Task SeedOpenIddictScopes(IOpenIddictScopeManager scopeManager)
{

    // Read scope
    if (await scopeManager.FindByNameAsync("addressbook.read") == null)
    {
        await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = "addressbook.read",
            DisplayName = "Read access to Address Book API"
        });
    }

    // Write scope
    if (await scopeManager.FindByNameAsync("addressbook.write") == null)
    {
        await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = "addressbook.write",
            DisplayName = "Write access to Address Book API"
        });
    }
}


static async Task SeedOpenIddictClients(IOpenIddictApplicationManager manager, string clientId, string clientSecret)
{
    if (await manager.FindByClientIdAsync(clientId) == null)
    {
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = "AddressBook Test Client",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + "addressbook.read",
                OpenIddictConstants.Permissions.Prefixes.Scope + "addressbook.write"
            }
        });
    }
}

static async Task SeedAppData(DapperContext context)
{
    using var connection = context.CreateConnection();
    connection.Open();

    // Seed 3 groups if none exist
    var groupCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Groups");
    List<Guid> groupIds = new List<Guid>();
    if (groupCount == 0)
    {
        const string insertGroup = "INSERT INTO Groups (Id, Name) VALUES (@Id, @Name)";
        var g1 = new { Id = Guid.NewGuid(), Name = "Family" };
        var g2 = new { Id = Guid.NewGuid(), Name = "Friends" };
        var g3 = new { Id = Guid.NewGuid(), Name = "Work" };
        await connection.ExecuteAsync(insertGroup, g1);
        await connection.ExecuteAsync(insertGroup, g2);
        await connection.ExecuteAsync(insertGroup, g3);
        groupIds.AddRange(new[] { g1.Id, g2.Id, g3.Id });
    }
    else
    {
        var ids = await connection.QueryAsync<Guid>("SELECT Id FROM Groups LIMIT 3");
        groupIds.AddRange(ids);
    }

    // Seed 5 contacts if none exist, and map them to the groups (round-robin)
    var contactCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Contacts");
    if (contactCount == 0)
    {
        var contacts = new[]
        {
            new { Id = Guid.NewGuid(), FirstName = "Helen", LastName = "George", Email = "helengeorge.com", PhoneNumber = "0256437829" },
            new { Id = Guid.NewGuid(), FirstName = "John", LastName = "Albert", Email = "johnalbert@gmail.com", PhoneNumber = "+64-555-01000" },
            new { Id = Guid.NewGuid(), FirstName = "Mary", LastName = "Smith", Email = "marysmith@gmail.com", PhoneNumber = "+1-555-23541" },
            new { Id = Guid.NewGuid(), FirstName = "Alex", LastName = "Brown", Email = "alexbrown@gmail.com", PhoneNumber = "+1-555-01022" },
            new { Id = Guid.NewGuid(), FirstName = "Chris", LastName = "Harry", Email = "chrisharry@gmail.com", PhoneNumber = "+1-555-81733" },
        };

        const string insertContact = @"INSERT INTO Contacts (Id, FirstName, LastName, Email, PhoneNumber)
                                      VALUES (@Id, @FirstName, @LastName, @Email, @PhoneNumber)";
        foreach (var c in contacts)
        {
            await connection.ExecuteAsync(insertContact, c);
        }

        const string insertMap = "INSERT INTO ContactGroups (ContactsId, GroupsId) VALUES (@ContactsId, @GroupsId)";
        var maps = new List<object>();
        for (int i = 0; i < contacts.Length; i++)
        {
            var groupId = groupIds[i % groupIds.Count];
            maps.Add(new { ContactsId = contacts[i].Id, GroupsId = groupId });
        }
        await connection.ExecuteAsync(insertMap, maps);
    }
}

