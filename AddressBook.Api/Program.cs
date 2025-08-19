using AddressBook.Application.Interfaces.Repositories;
using AddressBook.Application.Interfaces.Services;
using AddressBook.Application.Services;
using AddressBook.Infrastructure;
using AddressBook.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
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

        options.AddEphemeralEncryptionKey()
                        .AddEphemeralSigningKey()
                        .DisableAccessTokenEncryption();

        options.UseAspNetCore()
              .EnableTokenEndpointPassthrough()
                        .EnableAuthorizationEndpointPassthrough();
    });



builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://localhost:44397/"; // your OpenIddict server URL
        options.RequireHttpsMetadata = false;

        // Token validation parameters
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://localhost:44397/",   // must match your token's 'iss'

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
        policy.RequireClaim("scope", "addressbook.read");
        //policy.RequireClaim("scope", "addressbook.read".Split(' '));
    });

    options.AddPolicy("WriteScope", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("scope", "addressbook.write");
        //policy.RequireClaim("scope", "addressbook.write.Split(' ')");
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

    // OAuth2 / Bearer token definition
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {token}' to authenticate."
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
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AddressBook API v1");
        c.OAuthClientId("addressbook.client");
        c.OAuthClientSecret("secret");
        c.OAuthUsePkce();
        c.OAuthScopes("addressbook.read", "addressbook.write");
    });
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
                OpenIddictConstants.Permissions.Prefixes.Scope + "addressbook.read",
                OpenIddictConstants.Permissions.Prefixes.Scope + "addressbook.write"
            }
        });
    }
}

