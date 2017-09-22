    // This code requires the Nuget package Newtonsoft.Json to be installed.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CallUpdateResource
{
    public class AzureBlobDataReference
    {
        public string ConnectionString { get; set; }

        public string RelativeLocation { get; set; }

        public string BaseLocation { get; set; }

        public string SasBlobToken { get; set; }
    }

    public class ResourceLocation
    {
        public string Name { get; set; }

        public AzureBlobDataReference Location { get; set; }
    }

    public class ResourceLocations
    {
        public ResourceLocation[] Resources { get; set; }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            InvokeService().Wait();
        }

        static async Task InvokeService()
        {
            string url = "https://management.azureml.net/workspaces/949043c26c024f2d8e8e071d5ea12ba9/webservices/b7f327b3fbc24ffb9555794cae67c8f6/endpoints/retain";
                   url = "https://management.azureml.net/workspaces/949043c26c024f2d8e8e071d5ea12ba9/webservices/b7f327b3fbc24ffb9555794cae67c8f6/endpoints/retrain";
            string apiKey =  System.Environment.GetEnvironmentVariable("az_train_key"); 

            var resourceLocations = new ResourceLocations()
            {
                Resources = new ResourceLocation[] {
                    new ResourceLocation() 
                    {
                        Name = "Census Model 001 [trained model]",
                        Location = new AzureBlobDataReference()
                        {
                            BaseLocation = "https://wksa1.blob.core.windows.net",
                            RelativeLocation = "blob1/cenout1.ilearner",
                            SasBlobToken = "?st=2017-09-22T04%3A32%3A00Z&se=2017-09-23T04%3A32%3A00Z&sp=rl&sv=2016-05-31&sr=b&sig=QahgxzPFoguE9qWV474PriRZNPdsn5ukmzCySi46If0%3D"
                        }
                    }
                }
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Bearer", apiKey);
                using (HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), url))
                {
                    request.Content = new StringContent(JsonConvert.SerializeObject(resourceLocations), System.Text.Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Result: {0}", result);
                    }
                    else
                    {
                        Console.WriteLine("Failed with status code: {0}", response.StatusCode);
                        string responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(responseContent);
                    }
                }
            }
        }
    }
}

