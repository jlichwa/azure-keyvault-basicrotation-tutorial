using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using SimpleRotationFunc;

namespace SimpleRotationSample_fn
{
    public static class SimpleRotationHttpHandler
    {
        private const string UserIdTagName = "UserID";
        private const string DataSourceTagName = "DataSource";

        //Default values
        private static string KeyVaultName = "simplerotationsample-kv";
        private static string SecretName = "sqluser";
        private static string SecretVersion = "";

        [FunctionName("SimpleRotationHttpTest")]
        public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
			ILogger log)
        {
            log.LogInformation(req.ToString());

            log.LogInformation("C# Http trigger function processed a request.");
            SecretRotator.RotateSecret(log,SecretName, SecretVersion,KeyVaultName);

            return new OkObjectResult($"Secret Rotated Successfully");
        }

       


    }
}