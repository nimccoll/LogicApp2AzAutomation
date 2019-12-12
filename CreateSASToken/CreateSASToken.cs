//===============================================================================
// Microsoft FastTrack for Azure
// Logic App to Azure Automation Sample
//===============================================================================
// Copyright © Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CreateSASToken
{
    public static class CreateSASToken
    {
        [FunctionName("CreateSASToken")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string sasQueueToken = string.Empty;
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string queueName = data?.queueName;

            if (queueName != null)
            {
                IConfigurationRoot config = new ConfigurationBuilder()
                    .SetBasePath(context.FunctionAppDirectory)
                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(config.GetValue<string>("AzureStorageConnectionString"));
                CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                CloudQueue cloudQueue = queueClient.GetQueueReference(queueName);
                sasQueueToken = GetQueueSasToken(cloudQueue);
            }

            return queueName != null
                ? (ActionResult)new OkObjectResult(sasQueueToken)
                : new BadRequestObjectResult("Please pass a queueName in the request body");
        }

        private static string GetQueueSasToken(CloudQueue cloudQueue)
        {
            string sasQueueToken = string.Empty;
            SharedAccessQueuePolicy sasConstraints = new SharedAccessQueuePolicy();

            switch (cloudQueue.Name)
            {
                case "accessvminputqueue":
                    sasConstraints.SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddMinutes(2);
                    sasConstraints.Permissions = SharedAccessQueuePermissions.Add;
                    break;
                case "accessvmoutputqueue":
                    sasConstraints.SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddMinutes(20);
                    sasConstraints.Permissions = SharedAccessQueuePermissions.ProcessMessages | SharedAccessQueuePermissions.Read;
                    break;
                case "deployvminputqueue":
                    sasConstraints.SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddMinutes(2);
                    sasConstraints.Permissions = SharedAccessQueuePermissions.Add;
                    break;
                case "deployvmoutputqueue":
                    sasConstraints.SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddMinutes(10);
                    sasConstraints.Permissions = SharedAccessQueuePermissions.ProcessMessages | SharedAccessQueuePermissions.Read;
                    break;
                default:
                    sasConstraints.SharedAccessExpiryTime = DateTimeOffset.UtcNow;
                    sasConstraints.Permissions = SharedAccessQueuePermissions.None;
                    break;
            }

            sasQueueToken = cloudQueue.GetSharedAccessSignature(sasConstraints);

            return sasQueueToken;
        }
    }
}
