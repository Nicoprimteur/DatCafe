using System;
using System.Collections.Generic;
using Azure;
using System.Net.Http;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Azure.Messaging.EventGrid;
using System.Text;

namespace IotHubtoTwins
{
    public class IoTHubtoTwins
    {
        private static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName("IoTHubtoTwins")]
        // While async void should generally be used with caution, it's not uncommon for Azure function apps, since the function app isn't awaiting the task.
#pragma warning disable AZF0001 // Suppress async void error
        public async void Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
#pragma warning restore AZF0001 // Suppress async void error
        {
            if (adtInstanceUrl == null) log.LogError("Application setting \"ADT_SERVICE_URL\" not set");

            try
            {
                // Authenticate with Digital Twins
                var cred = new DefaultAzureCredential();
                var client = new DigitalTwinsClient(new Uri(adtInstanceUrl), cred);
                log.LogInformation($"ADT service client connection created.");

                if (eventGridEvent != null && eventGridEvent.Data != null)
                {
                    log.LogInformation(eventGridEvent.Data.ToString());

                    // <Find_device_ID_and_temperature>
                    JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
                    string deviceId = (string)deviceMessage["systemProperties"]["iothub-connection-device-id"];

                    var base64 = deviceMessage["body"];
                    var blob = Convert.FromBase64String((string)base64);
                    var json = Encoding.UTF8.GetString(blob);

                    JObject JsonObject = JObject.Parse(json);


                    log.LogInformation($"json:{json}");


                    var updateTwinData = new JsonPatchDocument();
                    log.LogInformation(string.Format("Device {0}.", deviceId));

                    foreach (var variable in new List<string>(){ "Temperature", "Humidity", "pH", "Conductivite", "Light" })
                    {
                        try
                        {
                            var value = JsonObject[variable];
                            if (value != null)
                            {
                                updateTwinData.AppendReplace(path: $"/{variable}", value: value.Value<double>());
                                log.LogInformation($"{variable} is : {value}.");
                            }
                        }
                        catch (Exception e) { log.LogInformation(e.ToString()); }
                    }

                    #region Trash
                    //try
                    //{

                    //    var temperature = JsonObject["Temperature"];
                    //    if (temperature != null)
                    //    {
                    //        updateTwinData.AppendReplace("/Temperature", temperature.Value<double>());
                    //        log.LogInformation($"Temperature is : {temperature}.");
                    //    }
                    //}
                    //catch (Exception e) { log.LogInformation(e.ToString()); }

                    //try
                    //{
                    //    var humidity = JsonObject["Humidity"];
                    //    if (humidity != null)
                    //    {
                    //        updateTwinData.AppendReplace("/Humidity", humidity.Value<double>());
                    //        log.LogInformation($"Humidity is : {humidity}.");
                    //    }
                    //}
                    //catch (Exception e) { log.LogInformation(e.ToString()); }

                    //try
                    //{
                    //    var pH = JsonObject["pH"];
                    //    if (pH != null)
                    //    {
                    //        updateTwinData.AppendReplace("/pH", pH.Value<double>());
                    //        log.LogInformation($"pH is : {pH}.");
                    //    }
                    //}
                    //catch (Exception e) { log.LogInformation(e.ToString()); }

                    //try
                    //{
                    //    var conductivite = JsonObject["Conductivite"];
                    //    if (conductivite != null)
                    //    {
                    //        updateTwinData.AppendReplace("/Conductivite", conductivite.Value<double>());
                    //        log.LogInformation($"Conductivite is : {conductivite}.");
                    //    }
                    //}
                    //catch (Exception e) { log.LogInformation(e.ToString()); } 
                    #endregion
                    // </Update_twin_with_device_temperature>

                    await client.UpdateDigitalTwinAsync(deviceId, updateTwinData);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Error in ingest function: {ex.Message}");
            }
        }
    }
}