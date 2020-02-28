using System;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Stardust.Nucleus;
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Providers.Gremlin;
using Stardust.Paradox.Data.Traversals;
using Stardust.Particles;

namespace Stardust.Paradox.CosmosDbTest
{
    public class TestBp : IBlueprint
    {
        public void Bind(IConfigurator configuration)
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(async (authority, resource, scope) =>
                await azureServiceTokenProvider.GetAccessTokenAsync(resource));
            var cosmosDbAccount = keyVaultClient.GetSecretAsync("https://stardust-test-vault.vault.azure.net/",
                "cosmosAccountName").Result;
            var cosmosDbKey = keyVaultClient
                .GetSecretAsync("https://stardust-test-vault.vault.azure.net/", "cosmosAccountKey").Result;
            configuration.AddEntityBinding((type, type1) => { configuration.Bind(type).To(type1).SetTransientScope(); })
                .Bind<IGremlinLanguageConnector>()
                .ToConstructor(s =>
                    new GremlinNetLanguageConnector($"{cosmosDbAccount.Value}.gremlin.cosmosdb.azure.com", "graphTest",
                        "services", cosmosDbKey.Value));
            GremlinFactory.SetActivatorFactory(() =>
                new GremlinNetLanguageConnector($"{cosmosDbAccount.Value}.gremlin.cosmosdb.azure.com", "graphTest",
                    "services", cosmosDbKey.Value));
            ConfigurationManagerHelper.SetValueOnKey("cosmosDbAccount", cosmosDbAccount.Value);
            ConfigurationManagerHelper.SetValueOnKey("cosmosDbKey", cosmosDbKey.Value);
        }

        public Type LoggingType => typeof(LoggingDefaultImplementation);
    }
}