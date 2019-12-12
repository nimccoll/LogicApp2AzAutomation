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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Okta.Core.Models;
using Salesforce.Common;
using Salesforce.Force;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace Okta.Core.Controllers
{
    public class HomeController : Controller
    {
        private IConfiguration _configuration;

        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [Authorize]
        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.VirtualMachineName = string.Empty;
            ViewBag.Status = string.Empty;
            ViewBag.User = User.Identity.Name;
            ViewBag.IPAddress = Request.HttpContext.Connection.RemoteIpAddress;
            return View();
        }

        [Authorize]
        [HttpPost]
        [ActionName("Index")]
        public IActionResult IndexPost()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_configuration.GetValue<string>("ConnectionStrings:AzureStorageConnectionString-1"));

            // Create the CloudQueueClient object for the storage account.
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();

            string virtualMachineName = string.Empty;
            string status = string.Empty;
            if (!string.IsNullOrEmpty(Request.Form["btnRequestVM"]))
            {
                virtualMachineName = Request.Form["txtVirtualMachineName"];
                status = "Processing";
                // Place request on input queue
                CloudQueue messageQueue = queueClient.GetQueueReference("deployvminputqueue");
                // Create a message and add it to the queue.
                CloudQueueMessage message = new CloudQueueMessage(virtualMachineName);
                messageQueue.AddMessageAsync(message).Wait();
            }
            else if (!string.IsNullOrEmpty(Request.Form["btnCheckStatus"]))
            {
                virtualMachineName = Request.Form["hidVirtualMachineName"];
                status = "Processing";

                // Check output queue
                CloudQueue messageQueue = queueClient.GetQueueReference("deployvmoutputqueue");

                // Get the next message in the queue.
                CloudQueueMessage retrievedMessage = messageQueue.GetMessageAsync().Result;

                if (retrievedMessage != null)
                {
                    // Process the message in less than 30 seconds.
                    status = "Ready";
                    // Then delete the message.
                    messageQueue.DeleteMessageAsync(retrievedMessage).Wait();
                }
            }
            else if (!string.IsNullOrEmpty(Request.Form["btnRequestAccess"]))
            {
                virtualMachineName = Request.Form["hidVirtualMachineName"];
                status = "Requesting Access";

                // Place request on input queue
                CloudQueue messageQueue = queueClient.GetQueueReference("accessvminputqueue");
                // Create a message and add it to the queue.
                string accessRequest = "{" + string.Format("\"userName\":\"{0}\", \"virtualMachineName\":\"{1}\", \"ipAddress\":\"{2}\"", User.Identity.Name, virtualMachineName, Request.HttpContext.Connection.RemoteIpAddress) + "}";
                CloudQueueMessage message = new CloudQueueMessage(accessRequest);
                messageQueue.AddMessageAsync(message).Wait();
            }
            else if (!string.IsNullOrEmpty(Request.Form["btnCheckAccess"]))
            {
                virtualMachineName = Request.Form["hidVirtualMachineName"];
                status = "Requesting Access";

                // Check output queue
                CloudQueue messageQueue = queueClient.GetQueueReference("accessvmoutputqueue");

                // Get the next message in the queue.
                CloudQueueMessage retrievedMessage = messageQueue.GetMessageAsync().Result;

                if (retrievedMessage != null)
                {
                    // Process the message in less than 30 seconds.
                    status = "Access Granted";
                    // Then delete the message.
                    messageQueue.DeleteMessageAsync(retrievedMessage).Wait();
                }
            }
            ViewBag.VirtualMachineName = virtualMachineName;
            ViewBag.Status = status;
            ViewBag.User = User.Identity.Name;
            ViewBag.IPAddress = Request.HttpContext.Connection.RemoteIpAddress;
            return View();
        }

        [Authorize]
        public IActionResult JSExample()
        {
            ViewBag.FunctionUrl = _configuration.GetValue<string>("FunctionUrl");
            ViewBag.FunctionKey = _configuration.GetValue<string>("FunctionKey");
            ViewBag.QueueUrl = _configuration.GetValue<string>("QueueUrl");
            ViewBag.User = User.Identity.Name;
            ViewBag.IPAddress = Request.HttpContext.Connection.RemoteIpAddress;
            return View();
        }

        [Authorize]
        [HttpGet]
        public IActionResult SFExample()
        {
            ViewBag.VirtualMachineName = string.Empty;
            ViewBag.Status = string.Empty;
            ViewBag.User = User.Identity.Name;
            ViewBag.IPAddress = Request.HttpContext.Connection.RemoteIpAddress;
            ViewBag.EnvRecordId = string.Empty;
            ViewBag.AccReqRecordId = string.Empty;
            return View();
        }

        [Authorize]
        [HttpPost]
        [ActionName("SFExample")]
        public IActionResult SFExamplePost()
        {
            var auth = new AuthenticationClient();

            auth.UsernamePasswordAsync(_configuration.GetValue<string>("SFConsumerKey"), _configuration.GetValue<string>("SFConsumerSecret"), _configuration.GetValue<string>("SFUserName"), _configuration.GetValue<string>("SFPassword")).Wait();

            var instanceUrl = auth.InstanceUrl;
            var accessToken = auth.AccessToken;
            var apiVersion = auth.ApiVersion;

            var client = new ForceClient(instanceUrl, accessToken, apiVersion);
            string virtualMachineName = string.Empty;
            string status = string.Empty;
            string envRecordId = string.Empty;
            string accReqRecordId = string.Empty;
            if (!string.IsNullOrEmpty(Request.Form["btnRequestVM"]))
            {
                virtualMachineName = Request.Form["txtVirtualMachineName"];
                status = "Processing";
                // Add an environment
                Environment environment = new Environment()
                {
                    UserName__c = User.Identity.Name,
                    VirtualMachineName__c = virtualMachineName,
                    Status__c = status
                };
                var success = client.CreateAsync("Environment__c", environment).Result;
                if (success.Success)
                {
                    envRecordId = success.Id;
                }
                else
                {
                    // Handle error here
                }
            }
            else if (!string.IsNullOrEmpty(Request.Form["btnCheckStatus"]))
            {
                envRecordId = Request.Form["hidEnvRecordId"];
                virtualMachineName = Request.Form["hidVirtualMachineName"];
                status = "Processing";

                // Check the environment status
                var environments = client.QueryAsync<Environment>(string.Format("SELECT id, UserName__c, VirtualMachineName__c, Status__c FROM Environment__c WHERE id = '{0}'", envRecordId)).Result;

                if (environments.Records.Count > 0)
                {
                    if (environments.Records[0].Status__c == "Ready")
                    {
                        status = "Ready";
                    }
                }
            }
            else if (!string.IsNullOrEmpty(Request.Form["btnRequestAccess"]))
            {
                virtualMachineName = Request.Form["hidVirtualMachineName"];
                status = "Requested";

                AccessRequest accessRequest = new AccessRequest()
                {
                    UserName__c = User.Identity.Name,
                    VirtualMachineName__c = virtualMachineName,
                    IPAddress__c = Request.HttpContext.Connection.RemoteIpAddress.ToString(),
                    Status__c = status
                };
                var success = client.CreateAsync("AccessRequest__c", accessRequest).Result;
                if (success.Success)
                {
                    accReqRecordId = success.Id;
                }
                else
                {
                    // Handle error here
                }
            }
            else if (!string.IsNullOrEmpty(Request.Form["btnCheckAccess"]))
            {
                accReqRecordId = Request.Form["hidAccReqRecordId"];
                virtualMachineName = Request.Form["hidVirtualMachineName"];
                status = "Requested";

                // Check the access request status
                var accessRequests = client.QueryAllAsync<AccessRequest>(string.Format("SELECT id, UserName__c, VirtualMachineName__c, IPAddress__c, Status__c FROM AccessRequest__c WHERE id = '{0}'", accReqRecordId)).Result;

                if (accessRequests.Records.Count > 0)
                {
                    if (accessRequests.Records[0].Status__c == "Granted")
                    {
                        status = "Granted";
                    }
                }
            }
            ViewBag.VirtualMachineName = virtualMachineName;
            ViewBag.Status = status;
            ViewBag.User = User.Identity.Name;
            ViewBag.IPAddress = Request.HttpContext.Connection.RemoteIpAddress;
            ViewBag.EnvRecordId = envRecordId;
            ViewBag.AccReqRecordId = accReqRecordId;
            return View();
        }

        [Authorize]
        [HttpGet]
        public IActionResult RequestFile()
        {
            var auth = new AuthenticationClient();

            auth.UsernamePasswordAsync(_configuration.GetValue<string>("SFConsumerKey"), _configuration.GetValue<string>("SFConsumerSecret"), _configuration.GetValue<string>("SFUserName"), _configuration.GetValue<string>("SFPassword")).Wait();

            var instanceUrl = auth.InstanceUrl;
            var accessToken = auth.AccessToken;
            var apiVersion = auth.ApiVersion;

            var client = new ForceClient(instanceUrl, accessToken, apiVersion);

            // Get list of files available for download from SalesForce
            List<FileRequest> files = new List<FileRequest>();
            var availableFiles = client.QueryAsync<AvailableFile>(string.Format("SELECT id, FileName__c FROM AvailableFile__c ORDER BY FileName__c")).Result;
            if (availableFiles.Records.Count > 0)
            {
                for (int i = 0; i < availableFiles.Records.Count; i++)
                {
                    files.Add(new FileRequest() { FileName__c = availableFiles.Records[i].FileName__c, UserName__c = User.Identity.Name });
                }
            }

            // Check for existing file requests by this user
            var fileRequests = client.QueryAsync<FileRequest>(string.Format("SELECT id, FileName__c, UserName__c, Status__c, FileUrl__c FROM FileRequest__c WHERE UserName__c = '{0}'", User.Identity.Name)).Result;

            if (fileRequests.Records.Count > 0)
            {
                ViewBag.Refresh = true;
                for (int i = 0; i < fileRequests.Records.Count; i++)
                {
                    foreach (FileRequest file in files)
                    {
                        if (file.FileName__c == fileRequests.Records[i].FileName__c)
                        {
                            bool isExpired = HandleExpiredRequest(client, fileRequests.Records[i]);
                            if (!isExpired)
                            {
                                file.Id = fileRequests.Records[0].Id;
                                file.Status__c = fileRequests.Records[i].Status__c;
                                file.FileUrl__c = fileRequests.Records[i].FileUrl__c;
                            }
                        }
                    }
                }
            }

            ViewBag.User = User.Identity.Name;
            ViewBag.IPAddress = Request.HttpContext.Connection.RemoteIpAddress;
            ViewBag.Files = files;
            return View();
        }

        [Authorize]
        [HttpPost]
        [ActionName("RequestFile")]
        public IActionResult RequestFilePost()
        {
            var auth = new AuthenticationClient();

            auth.UsernamePasswordAsync(_configuration.GetValue<string>("SFConsumerKey"), _configuration.GetValue<string>("SFConsumerSecret"), _configuration.GetValue<string>("SFUserName"), _configuration.GetValue<string>("SFPassword")).Wait();

            var instanceUrl = auth.InstanceUrl;
            var accessToken = auth.AccessToken;
            var apiVersion = auth.ApiVersion;

            var client = new ForceClient(instanceUrl, accessToken, apiVersion);

            string fileName = string.Empty;
            foreach(string key in Request.Form.Keys)
            {
                if (key != "__RequestVerificationToken")
                {
                    fileName = key;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(fileName))
            {
                FileRequest fileRequest = new FileRequest()
                {
                    UserName__c = User.Identity.Name,
                    FileName__c = fileName,
                    Status__c = "Processing"
                };
                var success = client.CreateAsync("FileRequest__c", fileRequest).Result;
                if (success.Success)
                {
                    fileRequest.Id = success.Id;
                }
            }

            return RedirectToAction("RequestFile");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public FileContentResult RDP(string id)
        {
            // This string contains the contents of the .rdp file, the id parameter contains the virtual
            // machine name. The example is hard-coded to the eastus location so you can pass in the location as needed.
            string rdp = string.Format("full address:s:{0}.eastus.cloudapp.azure.com:3389\r\nprompt for credentials:i:1\r\nadministrative session:i:1", id);
            byte[] rdpBytes = Encoding.UTF8.GetBytes(rdp);

            // Make sure to include the "application/x-rdp" content type header
            return new FileContentResult(rdpBytes, "application/x-rdp")
            {
                FileDownloadName = string.Format("{0}.rdp", id)
            };
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private bool HandleExpiredRequest(ForceClient forceClient, FileRequest fileRequest)
        {
            // Delete expired file requests
            bool isExpired = false;
            if (!string.IsNullOrEmpty(fileRequest.FileUrl__c))
            {
                System.Uri url = new System.Uri(fileRequest.FileUrl__c);
                string queryString = System.Net.WebUtility.UrlDecode(url.Query);
                string[] queryParts = queryString.Split('&');
                foreach (string queryPart in queryParts)
                {
                    if (queryPart.StartsWith("se="))
                    {
                        System.DateTime endDateTime = System.DateTime.Parse(queryPart.Replace("se=", ""));
                        if (endDateTime < System.DateTime.Now)
                        {
                            // Delete SalesForce record
                            isExpired = true;
                            bool isSuccess = forceClient.DeleteAsync("FileRequest__c", fileRequest.Id).Result;
                        }
                        break;
                    }
                }
            }
            return isExpired;
        }
    }
}
