using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Application.Services;
using AddressBook.Infrastructure;
using AddressBook.Infrastructure.Repositories;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenIddict.Abstractions;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on both HTTP and HTTPS
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080); // HTTP
    options.ListenAnyIP(7255, listenOptions =>
    {
        // Prefer mounted PFX in Docker; fallback to dev cert
        var pfxPath = "/https/addressbook.pfx";
        var pfxPassword = "password123";
        if (File.Exists(pfxPath))
        {
            listenOptions.UseHttps(pfxPath, pfxPassword);
        }
        else
        {
            listenOptions.UseHttps(); // dev cert
        }
    });
});

// ---------------------------
// Add services
// ---------------------------

//Dapper
builder.Services.AddSingleton<DapperContext>();
// Register Guid handler for Dapper
SqlMapper.AddTypeHandler(new AddressBook.Infrastructure.GuidTypeHandler());

// SQLite DB
var conn = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=addressbook.db";
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
        // Use a fixed HTTPS issuer for both dev and docker (port must be published)
        options.SetIssuer(new Uri("https://localhost:7255/"));
        options.AllowClientCredentialsFlow();

        options.AcceptAnonymousClients();

        // Prefer persistent signing cert in Docker, otherwise dev certs
        var pfxPath = "/https/addressbook.pfx";
        var pfxPassword = "password123";
        if (File.Exists(pfxPath))
        {
            options.AddSigningCertificate(new X509Certificate2(pfxPath, pfxPassword));
            options.AddDevelopmentEncryptionCertificate();
        }
        else
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        

                      options.DisableAccessTokenEncryption();

        options.UseAspNetCore()
              .EnableTokenEndpointPassthrough()
                        .EnableAuthorizationEndpointPassthrough();
    });



builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://localhost:7255/"; // same as SetIssuer
        // In dev/docker, HTTPS uses a self-signed cert. Allow untrusted certs for metadata in Development.
        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false; // allow http/invalid cert for metadata
            options.BackchannelHttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
        }
        else
        {
            options.RequireHttpsMetadata = true;
        }

        // Token validation parameters
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://localhost:7255/",   // must match token 'iss'

            ValidateAudience = true,
            ValidAudience = "addressbook.api",        // must match your token's 'aud'
            ValidAudiences = new[] { "addressbook.api" },
            ValidateLifetime = true
        };

        // Optional: custom JSON error response
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                context.NoResult();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    error = "Authentication failed",
                    details = context.Exception?.Message
                });

                return context.Response.WriteAsync(result);
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    error = "You are not authorized",
                    details = context.ErrorDescription ?? "No token or invalid token provided"
                });

                return context.Response.WriteAsync(result);
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";

                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    error = "Forbidden",
                    details = "You do not have permission to access this resource"
                });

                return context.Response.WriteAsync(result);
            }
        };
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

    // OAuth2 definition for Client Credentials
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri("https://localhost:7255/connect/token", UriKind.Absolute),
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
        c.OAuthUsePkce(); // optional, for auth code flow
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

