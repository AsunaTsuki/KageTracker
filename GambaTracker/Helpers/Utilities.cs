using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using ECommons.Logging;
using Newtonsoft.Json;

namespace GambaTracker.Helpers
{
    public static class Utilities
    {
        private static readonly HttpClient client = new HttpClient();
        public static async Task<int> SendPostRequestAsync(string url, string jsonData)
        {
            try
            {
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    // Handle the response content here
                    PluginLog.Verbose(responseContent);
                    return (int)response.StatusCode;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest) // Check for 400 Bad Request
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    // Handle the 400 Bad Request here
                    PluginLog.Error($"{errorContent}");
                    return 400;
                }
                else
                {
                    // Handle the error here
                    PluginLog.Error($"Error: {response.StatusCode}");
                    return (int)response.StatusCode;
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions here
                PluginLog.Error($"Exception occurred: {ex.Message}");
                return -1;
            }
        }

        public static async Task FetchValidVenuesAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // Adjust the URL to where your Flask app is hosted
                    string url = "https://tracker.gamba.pro/get_venue_list";
                    var response = await client.GetStringAsync(url);

                    // Deserialize the JSON response into a string array
                    var venues = JsonConvert.DeserializeObject<string[]>(response);

                    if (venues != null)
                    {
                        // Ensure "Minx" is always the first item
                        List<string> sortedVenues = new List<string>();
                        if (venues.Contains("Club Minx"))
                        {
                            sortedVenues.Add("Club Minx");
                            // Remove "Minx" from the original list to avoid duplication
                            venues = venues.Where(v => v != "Club Minx").ToArray();
                        }

                        // Add the rest of the venues
                        sortedVenues.AddRange(venues);

                        // Add "Custom" to the end of the list
                        sortedVenues.Add("Custom");

                        if (Plugin.P?.Configuration != null)
                        {
                            // Update the configuration with the sorted list
                            Plugin.P.Configuration.Venues = sortedVenues.ToArray();

                            var validVenues = sortedVenues.ToArray();
                            if (!validVenues.Contains(Plugin.P.Configuration.CurrentVenueDropdown))
                            {
                                // If the current selection is invalid, reset to the first venue
                                Plugin.P.Configuration.CurrentVenueDropdown = validVenues.FirstOrDefault();
                            }

                            Plugin.P.Configuration.Save();
                        }
                        else
                        {
                            PluginLog.Error("Plugin.P or Plugin.P.Configuration is not initialized.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during the request
                PluginLog.Error($"Error fetching valid venues: {ex.Message}");
            }
        }

        public static async Task FetchValidDealersAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // Adjust the URL to where your Flask app is hosted
                    string url = "https://tracker.gamba.pro/get_dealer_list";
                    var response = await client.GetStringAsync(url);

                    // Deserialize the JSON response into a string array
                    var dealers = JsonConvert.DeserializeObject<string[]>(response);

                    if (dealers != null)
                    {

                        if (Plugin.P?.Configuration != null)
                        {
                            var validDealers = dealers.ToArray();
                            if (Plugin.P?.Configuration != null)
                            {
                                // Update the configuration with the sorted list
                                Plugin.P.Configuration.Dealers = dealers;

                                Plugin.P.Configuration.Save();
                            }
                            else
                            {
                                PluginLog.Error("Plugin.P or Plugin.P.Configuration is not initialized.");
                            }
                        }
                        else
                        {
                            PluginLog.Error("Plugin.P or Plugin.P.Configuration is not initialized.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during the request
                PluginLog.Error($"Error fetching valid dealers: {ex.Message}");
            }
        }


        public static void ValidateCurrentVenueDropdown()
        {
            var validVenues = Plugin.P.Configuration.Venues;
            if (!validVenues.Contains(Plugin.P.Configuration.CurrentVenueDropdown))
            {
                // If the current selection is invalid, reset to the first venue
                Plugin.P.Configuration.CurrentVenueDropdown = validVenues.FirstOrDefault();
                Plugin.P.Configuration.Save();
            }
        }



    }
}
