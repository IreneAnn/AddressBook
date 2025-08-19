using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using System;
using System.Threading.Tasks;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AddressBook.Infrastructure
{
    public static class OpenIddictSeed
    {
        /*public static async Task InitializeAsync(IOpenIddictApplicationManager manager)
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
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.Prefixes.Scope + "addressbook.api"
            }
                });
            }           
        }*/
    }
}
