using System;
using System.IO;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
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
        // "cosmosAccountName").Result;
  //var cosmosDbKey = keyVaultClient
  // .GetSecretAsync("https://stardust-test-vault.vault.azure.net/", "cosmosAccountKey").Result;

// Load configuration from appsettings files
            var config = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
      .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
   .Build();

      var cosmosDbAccount = config["CosmosDb:Account"];
   var cosmosDbKey = config["CosmosDb:Key"];
            var database = config["CosmosDb:Database"] ?? "graphTest";
       var collection = config["CosmosDb:Collection"] ?? "graphTest";

   configuration.AddEntityBinding((type, type1) =>
        {
    configuration.Bind(type).To(type1).SetTransientScope(); 
 
         })
    .Bind<IGremlinLanguageConnector>()
         .ToConstructor(s=>new GremlinNetLanguageConnector($"{cosmosDbAccount}", database, collection, cosmosDbKey));
   GremlinFactory.SetActivatorFactory(()=> new GremlinNetLanguageConnector($"{cosmosDbAccount}", database, collection, cosmosDbKey));
      ConfigurationManagerHelper.SetValueOnKey("cosmosDbAccount",cosmosDbAccount);
   ConfigurationManagerHelper.SetValueOnKey("cosmosDbKey", cosmosDbKey);
        }

        public Type LoggingType => typeof(LoggingDefaultImplementation);
    }
}
