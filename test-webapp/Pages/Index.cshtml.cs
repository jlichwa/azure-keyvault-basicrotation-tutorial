using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace SimpleRotationSample_App.Pages
{
    public class IndexModel : PageModel
    {
        private IMemoryCache _cache;
        private readonly IConfiguration config;
        private const int numberOfRetries = 5;
        private const string secretName = "sqluser";
        private string dataSource = "simplerotationsample-sql.database.windows.net";
        private string keyVaultName = "simplerotationsample-kv";
        

        public string UserIdTag { get; set; }
        public string SecretValue { get; set; }
        public bool DatabaseConnected { get; set; }

        public int RetriesCount { get; set; }

        public IndexModel(IMemoryCache cache, IConfiguration config)
        {
            _cache = cache;
            this.config = config;
            if (!String.IsNullOrEmpty(config["DataSource"]))
            {
                dataSource = config["DataSource"];
            }
            if (!String.IsNullOrEmpty(config["KeyVaultName"]))
            {
                keyVaultName = config["KeyVaultName"];
            }
        }
        public void OnGet()
        {
            KeyVaultSecret secret = RetrieveSecret(false);

            //DB Connection builder
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = dataSource;

            for (int i = 0; i < numberOfRetries; i++)
            {
                builder.UserID = secret.Properties.Tags.ContainsKey("UserID")? secret.Properties.Tags["UserID"]:"";
                builder.Password = secret.Value;
                try
                {
                    using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                    {
                        connection.Open();
                        DatabaseConnected = true;
                        
                    }
                    SqlConnection.ClearAllPools();
                    break;
                }
                catch
                {
                    RetriesCount++;
                    Thread.Sleep(500);

                    //refresh secret
                    secret= RetrieveSecret(true);

                }
            }
        }

        private KeyVaultSecret RetrieveSecret(bool ignoreCache)
        {
            KeyVaultSecret secret = null;
            if (ignoreCache || !_cache.TryGetValue(secretName, out secret))
            {
                // Authenticate to Key Vault
                var kvUri = "https://" + keyVaultName + ".vault.azure.net";
                var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());

                //Retrieve Secret
                secret = client.GetSecret(secretName);

                //update cache
                _cache.Set(secretName, secret, TimeSpan.FromHours(8));
            }

            UserIdTag = secret.Properties.Tags.ContainsKey("UserID") ? secret.Properties.Tags["UserID"] : "";
            SecretValue = secret.Value;
            return secret;
        }
    }
}
