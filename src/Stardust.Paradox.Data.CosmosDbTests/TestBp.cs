using System;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Stardust.Core;
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
	        //var azureServiceTokenProvider = new AzureServiceTokenProvider();
	        //var keyVaultClient = new KeyVaultClient(async (authority, resource, scope) =>
		       // await azureServiceTokenProvider.GetAccessTokenAsync(resource));
	        //var cosmosDbAccount = keyVaultClient.GetSecretAsync("https://stardust-test-vault.vault.azure.net/",
		       // "   ").Result;
	        //var cosmosDbKey = keyVaultClient
		       // .GetSecretAsync("https://stardust-test-vault.vault.azure.net/", "cosmosAccountKey").Result;

               var cosmosDbAccount = "jonas-playground.gremlin.cosmos.azure.com";
               var cosmosDbKey =
                   "9PjaiGNCsv7zTE1PU02FRR0sw1h1gp0qwnVzSRYFRed5gz1NrXGJpK9112nADL6kyCZSVWIeRkGPACDbPOHhBA==";
               configuration.AddEntityBinding((type, type1) =>
                {
                    configuration.Bind(type).To(type1).SetTransientScope(); 
                    
                })
                .Bind<IGremlinLanguageConnector>()
				.ToConstructor(s=>new GremlinNetLanguageConnector($"{cosmosDbAccount}", "graphTest", "graphTest", cosmosDbKey));
            GremlinFactory.SetActivatorFactory(()=> new GremlinNetLanguageConnector($"{cosmosDbAccount}", "graphTest", "graphTest", cosmosDbKey));
			ConfigurationManagerHelper.SetValueOnKey("cosmosDbAccount",cosmosDbAccount);
	        ConfigurationManagerHelper.SetValueOnKey("cosmosDbKey", cosmosDbKey);
		}

        public Type LoggingType => typeof(LoggingDefaultImplementation);
    }
}