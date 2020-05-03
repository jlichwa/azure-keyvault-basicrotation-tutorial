using Azure.Security.KeyVault.Secrets;
using Microsoft.Data.SqlClient;
using System;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs;
using System.Text.RegularExpressions;


namespace SimpleRotationFunc
{
    
    public class SecretRotator
    {
        private const string UserIdTagName = "UserID";
        private const string DataSourceTagName = "DataSource";
        private const int SecretExpirationDays = 31;

        public static void RotateSecret(ILogger log, string secretName, string secretVersion, string keyVaultName)
        {
            //Retrieve Current Secret
            var kvUri = "https://" + keyVaultName + ".vault.azure.net";
            var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());
            KeyVaultSecret secret = client.GetSecret(secretName, secretVersion);
            log.LogInformation("Secret Info Retrieved");

            //Retrieve Secret Info
            var userId = secret.Properties.Tags.ContainsKey(UserIdTagName) ? secret.Properties.Tags[UserIdTagName] : "";
            var datasource = secret.Properties.Tags.ContainsKey(DataSourceTagName) ? secret.Properties.Tags[DataSourceTagName] : "";
            log.LogInformation($"Data Source Name: {datasource}");
            log.LogInformation($"User Id Name: {userId}");

            //Check SQL connection
            CheckServiceConnection(secret);
            log.LogInformation("Service  Connection Validated");
            
            //Create new password
            var randomPassword = CreateRandomPassword();
            log.LogInformation("New Password Generated");

            //Add secret version with new password to Key Vault
            CreateNewSecretVersion(client, secret, randomPassword);
            log.LogInformation("New Secret Version Generated");

            //Update SQL Server with new password
            UpdateServicePassword(secret, randomPassword);
            log.LogInformation("Password Changed");
            log.LogInformation($"Secret Rotated Successfully");
        }

        private static void CreateNewSecretVersion(SecretClient client, KeyVaultSecret secret, string newSecretValue)
        {
            string userId = secret.Properties.Tags.ContainsKey(UserIdTagName) ? secret.Properties.Tags[UserIdTagName] : "";
            var datasource = secret.Properties.Tags.ContainsKey(DataSourceTagName) ? secret.Properties.Tags[DataSourceTagName] : "";

            //add new secret version to key vault
            var newSecret = new KeyVaultSecret(secret.Name, newSecretValue);
            newSecret.Properties.Tags.Add(UserIdTagName, userId);
            newSecret.Properties.Tags.Add(DataSourceTagName, datasource);
            newSecret.Properties.ExpiresOn = DateTime.UtcNow.AddDays(SecretExpirationDays);
            client.SetSecret(newSecret);
        }

        private static void UpdateServicePassword(KeyVaultSecret secret, string newpassword)
        {
            var userId = secret.Properties.Tags.ContainsKey(UserIdTagName) ? secret.Properties.Tags[UserIdTagName] : "";
            var datasource = secret.Properties.Tags.ContainsKey(DataSourceTagName) ? secret.Properties.Tags[DataSourceTagName] : "";

            var password = secret.Value;
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = datasource;
            builder.UserID = userId;
            builder.Password = password;

            //Update password
            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand($"ALTER LOGIN azureuser WITH Password='{newpassword}';", connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private static string CreateRandomPassword()
        {
            const int length = 60;
            // Create a string of characters, numbers, special characters that allowed in the password  
            string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*?_-";
            Random random = new Random();

            // Select one random character at a time from the string  
            // and create an array of chars  
            char[] chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = validChars[random.Next(0, validChars.Length)];
            }

            return new String((char[])chars);
        }
         private static void CheckServiceConnection(KeyVaultSecret secret)
        {
            var userId = secret.Properties.Tags.ContainsKey(UserIdTagName) ? secret.Properties.Tags[UserIdTagName] : "";
            var datasource = secret.Properties.Tags.ContainsKey(DataSourceTagName) ? secret.Properties.Tags[DataSourceTagName] : "";

            var password = secret.Value;
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = datasource;
            builder.UserID = userId;
            builder.Password = password;
            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();
            }
        }
    }
}
