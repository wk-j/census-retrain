

// This code requires the Nuget package Microsoft.AspNet.WebApi.Client to be installed.
// Instructions for doing this in Visual Studio:
// Tools -> Nuget Package Manager -> Package Manager Console
// Install-Package Microsoft.AspNet.WebApi.Client
//
// Also, add a reference to Microsoft.WindowsAzure.Storage.dll for reading from and writing to the Azure blob storage

using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CensusRetrain
{
    public class AzureBlobDataReference
    {
        // Storage connection string used for regular blobs. It has the following format:
        // DefaultEndpointsProtocol=https;AccountName=ACCOUNT_NAME;AccountKey=ACCOUNT_KEY
        // It's not used for shared access signature blobs.
        public string ConnectionString { get; set; }

        // Relative uri for the blob, used for regular blobs as well as shared access 
        // signature blobs.
        public string RelativeLocation { get; set; }

        // Base url, only used for shared access signature blobs.
        public string BaseLocation { get; set; }

        // Shared access signature, only used for shared access signature blobs.
        public string SasBlobToken { get; set; }
    }

    public enum BatchScoreStatusCode
    {
        NotStarted,
        Running,
        Failed,
        Cancelled,
        Finished
    }

    public class BatchScoreStatus
    {
        // Status code for the batch scoring job
        public BatchScoreStatusCode StatusCode { get; set; }


        // Locations for the potential multiple batch scoring outputs
        public IDictionary<string, AzureBlobDataReference> Results { get; set; }

        // Error details, if any
        public string Details { get; set; }
    }

    public class BatchExecutionRequest
    {

        public IDictionary<string, AzureBlobDataReference> Inputs { get; set; }
        public IDictionary<string, string> GlobalParameters { get; set; }

        // Locations for the potential multiple batch scoring outputs
        public IDictionary<string, AzureBlobDataReference> Outputs { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            InvokeBatchExecutionService().Wait();
            // ********************* loy
            Console.WriteLine("End program");
            Console.Read();
        }

        static async Task WriteFailedResponse(HttpResponseMessage response)
        {
            Console.WriteLine(string.Format("The request failed with status code: {0}", response.StatusCode));

            // Print the headers - they include the requert ID and the timestamp, which are useful for debugging the failure
            Console.WriteLine(response.Headers.ToString());

            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseContent);
        }



        static void UploadFileToBlob(string inputFileLocation, string inputBlobName, string storageContainerName, string storageConnectionString)
        {
            // Make sure the file exists
            if (!File.Exists(inputFileLocation))
            {
                throw new FileNotFoundException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "File {0} doesn't exist on local computer.",
                        inputFileLocation));
            }

            Console.WriteLine("Uploading the input to blob storage...");

            var blobClient = CloudStorageAccount.Parse(storageConnectionString).CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(storageContainerName);
            container.CreateIfNotExistsAsync();
            var blob = container.GetBlockBlobReference(inputBlobName);
            //blob.UploadFromFileAsync(inputFileLocation, FileMode.Open);
            blob.UploadFromFileAsync(inputFileLocation);
        }



        static void ProcessResults(BatchScoreStatus status)
        {

            foreach (var output in status.Results)
            {
                var blobLocation = output.Value;
                Console.WriteLine(string.Format("The result '{0}' is available at the following Azure Storage location:", output.Key));
                Console.WriteLine(string.Format("BaseLocation: {0}", blobLocation.BaseLocation));
                Console.WriteLine(string.Format("RelativeLocation: {0}", blobLocation.RelativeLocation));
                Console.WriteLine(string.Format("SasBlobToken: {0}", blobLocation.SasBlobToken));
                Console.WriteLine();

            }
        }

        static async Task InvokeBatchExecutionService()
        {
            // How this works:
            //
            // 1. Assume the input is present in a local file (if the web service accepts input)
            // 2. Upload the file to an Azure blob - you'd need an Azure storage account
            // 3. Call the Batch Execution Service to process the data in the blob. Any output is written to Azure blobs.
            // 4. Download the output blob, if any, to local file

            const string BaseUrl = "https://ussouthcentral.services.azureml.net/workspaces/949043c26c024f2d8e8e071d5ea12ba9/services/2c11bc8f07814b8ca9f45b9f2883d4da/jobs";

            // ********************* loy
            const string StorageAccountName = "wksa1"; // Replace this with your Azure Storage Account name
            string StorageAccountKey = System.Environment.GetEnvironmentVariable("az_storage_key"); 
            const string StorageContainerName = "blob1"; // Replace this with your Azure Storage Container name
            string storageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", StorageAccountName, StorageAccountKey);
            string apiKey = System.Environment.GetEnvironmentVariable("az_census_key");

            // set a time out for polling status
            const int TimeOutInMilliseconds = 120 * 1000; // Set a timeout of 2 minutes


            // ********************* loy
            UploadFileToBlob(@"Data/cenin1.csv" /*Replace this with the location of your input file*/,
               "cenin1.csv" /*Replace this with the name you would like to use for your Azure blob; this needs to have the same extension as the input file */,
               StorageContainerName, storageConnectionString);

            using (HttpClient client = new HttpClient())
            {
                var request = new BatchExecutionRequest()
                {

                    Inputs = new Dictionary<string, AzureBlobDataReference>()
                    {

                        {
                            "input1",
                            new AzureBlobDataReference()
                            {
                                ConnectionString = storageConnectionString,
                                // ************************ loy
                                RelativeLocation = string.Format("{0}/cenin1.csv", StorageContainerName)
                            }
                        },
                    },

                    Outputs = new Dictionary<string, AzureBlobDataReference>()
                    {

                        {
                            "output1",
                            new AzureBlobDataReference()
                            {
                                ConnectionString = storageConnectionString,
                                // ********************* loy
                                RelativeLocation = string.Format("/{0}/cenout1.ilearner", StorageContainerName)
                            }
                        },

                        {
                            "output2",
                            new AzureBlobDataReference()
                            {
                                ConnectionString = storageConnectionString,
                                // ********************* loy
                                RelativeLocation = string.Format("/{0}/cenout2.csv", StorageContainerName)
                            }
                        },
                    },
                    GlobalParameters = new Dictionary<string, string>()
                    {
                    }
                };

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                // WARNING: The 'await' statement below can result in a deadlock if you are calling this code from the UI thread of an ASP.Net application.
                // One way to address this would be to call ConfigureAwait(false) so that the execution does not attempt to resume on the original context.
                // For instance, replace code such as:
                //      result = await DoSomeTask()
                // with the following:
                //      result = await DoSomeTask().ConfigureAwait(false)


                Console.WriteLine("Submitting the job...");

                // submit the job
                var response = await client.PostAsJsonAsync(BaseUrl + "?api-version=2.0", request);
                if (!response.IsSuccessStatusCode)
                {
                    await WriteFailedResponse(response);
                    return;
                }

                string jobId = await response.Content.ReadAsAsync<string>();
                Console.WriteLine(string.Format("Job ID: {0}", jobId));


                // start the job
                Console.WriteLine("Starting the job...");
                response = await client.PostAsync(BaseUrl + "/" + jobId + "/start?api-version=2.0", null);
                if (!response.IsSuccessStatusCode)
                {
                    await WriteFailedResponse(response);
                    return;
                }

                string jobLocation = BaseUrl + "/" + jobId + "?api-version=2.0";
                Stopwatch watch = Stopwatch.StartNew();
                bool done = false;
                while (!done)
                {
                    Console.WriteLine("Checking the job status...");
                    response = await client.GetAsync(jobLocation);
                    if (!response.IsSuccessStatusCode)
                    {
                        await WriteFailedResponse(response);
                        return;
                    }

                    BatchScoreStatus status = await response.Content.ReadAsAsync<BatchScoreStatus>();
                    if (watch.ElapsedMilliseconds > TimeOutInMilliseconds)
                    {
                        done = true;
                        Console.WriteLine(string.Format("Timed out. Deleting job {0} ...", jobId));
                        await client.DeleteAsync(jobLocation);
                    }
                    switch (status.StatusCode)
                    {
                        case BatchScoreStatusCode.NotStarted:
                            Console.WriteLine(string.Format("Job {0} not yet started...", jobId));
                            break;
                        case BatchScoreStatusCode.Running:
                            Console.WriteLine(string.Format("Job {0} running...", jobId));
                            break;
                        case BatchScoreStatusCode.Failed:
                            Console.WriteLine(string.Format("Job {0} failed!", jobId));
                            Console.WriteLine(string.Format("Error details: {0}", status.Details));
                            done = true;
                            break;
                        case BatchScoreStatusCode.Cancelled:
                            Console.WriteLine(string.Format("Job {0} cancelled!", jobId));
                            done = true;
                            break;
                        case BatchScoreStatusCode.Finished:
                            done = true;
                            Console.WriteLine(string.Format("Job {0} finished!", jobId));

                            ProcessResults(status);
                            break;
                    }

                    if (!done)
                    {
                        Thread.Sleep(1000); // Wait one second
                    }
                }
            }
        }
    }
}

