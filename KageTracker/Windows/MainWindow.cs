using System;
using System.Numerics;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using KageTracker.Helpers;
using System.Threading.Tasks;
using System.Text.Json;
using ECommons.DalamudServices;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.PartyFunctions;
using System.Drawing.Printing;
using Dalamud.Logging;
using System.Threading;
using System.Xml.Linq;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.Automation;

namespace KageTracker.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    
    string[] validGames = new string[] { "Blackjack", "Poker", "Bingo", "Roulette", "Deathroll Tournament" };
    string[] validStandOptions = new string[] { "16", "16/Soft 17", "17", "17/Soft 18" };
    public Timer _updateTimer;
    public bool _isUpdating = false;
    public string currentStatus = "Not Dealing";

    public MainWindow(Plugin plugin) : base(
        "KageTracker###KageTrackerMainWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 270),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        this.Plugin = plugin;
    }

    public void Dispose()
    {
        _updateTimer?.Dispose();
        Plugin.P.Configuration.isDealing = false;
        Plugin.P.Configuration.Save();

    }

    public override void PreDraw()
    {
        this.WindowName = $"KageTracker - {currentStatus}###KageTrackerMainWindow";
    }

    public override void Draw()
    {
        try
        {
            string[] validVenues = Plugin.P.Configuration.Venues;
            string[] validDealers = Plugin.P.Configuration.Dealers;
            string dealerKey = Plugin.P.Configuration.DealerKey;
            var betLimits = Plugin.Configuration.BetLimits;
            var startTime = Plugin.Configuration.StartTime;
            var customVenueName = Plugin.Configuration.CustomVenueName;
            var customVenueLocation = Plugin.Configuration.CustomVenueLocation;
            int currentVenueIndex = Math.Max(0, Array.IndexOf(validVenues, Plugin.Configuration.CurrentVenueDropdown)); // Ensure index is at least 0
            int currentDebugDealerIndex = Math.Max(0, Array.IndexOf(validDealers, Plugin.Configuration.DebugDealer)); // Ensure index is at least 0
            int currentGameIndex = Math.Max(0, Array.IndexOf(validGames, Plugin.Configuration.CurrentGameDropdown)); // Ensure index is at least 0
            int currentStandIndex = Math.Max(0, Array.IndexOf(validStandOptions, Plugin.Configuration.CurrentStandDropdown)); // Ensure index is at least 0

            if (currentVenueIndex >= validVenues.Length)
            {
                PluginLog.Error("Current venue index is out of range. Resetting to default.");
                currentVenueIndex = 0; // Reset to default or handle appropriately
                Plugin.Configuration.CurrentGameDropdown = validGames[currentGameIndex];
                Plugin.Configuration.Save(); // Save your configuration
            }

            if (currentGameIndex >= validGames.Length)
            {
                PluginLog.Error("Current game index is out of range. Resetting to default.");
                currentGameIndex = 0; // Reset to default or handle appropriately
                Plugin.Configuration.CurrentGameDropdown = validGames[currentGameIndex];
                Plugin.Configuration.Save(); // Save your configuration
            }

            if (currentStatus == "Not Dealing")
            {
                ImGuiEx.RightFloat(() =>
                {
                    if (ImGui.Button("Start Dealing"))
                    {
                        if(dealerKey.Trim() == "")
                        {
                            PluginLog.Warning("KageTracker dealer key is blank!");
                            var message = new SeStringBuilder().AddUiForeground((ushort)16).AddText("Dealer key is blank. Please set it in /gambasetup to deal").AddUiForegroundOff().Build();
                            Svc.Chat.Print(new() { Message = message });
                            return;
                        }

                        if (!_isUpdating)
                        {
                            currentStatus = "Currently Dealing";
                            Plugin.Configuration.isDealing = true;
                            Plugin.Configuration.Save();
                            _isUpdating = true;
                            _updateTimer = new System.Threading.Timer(async _ =>
                            {
                                if (Svc.ClientState.LocalPlayer?.IsTargetable == true)
                                {
                                    SeString name = Svc.ClientState.LocalPlayer.Name;
                                    String homeworld = Svc.ClientState.LocalPlayer.HomeWorld.Value.Name.ToString();
                                    String dealerNameWorld = "";
                                    int partyCount = 0;

                                    if (Plugin.Configuration.DebugMode)
                                    {
                                        dealerNameWorld = Plugin.Configuration.DebugDealer;
                                        partyCount = Plugin.Configuration.DebugPartySize;
                                    }
                                    else
                                    {
                                        dealerNameWorld = $"{name}@{homeworld}";
                                        partyCount = UniversalParty.Members.Count;
                                    }


                                    PluginLog.Verbose($"Dealer name: {dealerNameWorld}");
                                    PluginLog.Verbose($"Venue Name: {validVenues[currentVenueIndex]}");
                                    PluginLog.Verbose($"Game: {validGames[currentGameIndex]}");
                                    PluginLog.Verbose($"Player Count: {partyCount}");

                                    string venueName = "";
                                    string venueLocation = "";
                                    if (validVenues[currentVenueIndex] != "Custom")
                                    {
                                        venueName = validVenues[currentVenueIndex];
                                        venueLocation = "";
                                    }
                                    else
                                    {
                                        venueName = customVenueName;
                                        venueLocation = customVenueLocation;
                                    }

                                    var data = new
                                    {
                                        dealer_name = dealerNameWorld,
                                        venue_name = venueName,
                                        location = venueLocation,
                                        bet_limits = betLimits,
                                        dealer_stand = validStandOptions[currentStandIndex],
                                        start_time = startTime,
                                        game = validGames[currentGameIndex],
                                        player_count = partyCount,
                                        dealer_key = dealerKey
                                    };
                                    string jsonData = JsonSerializer.Serialize(data);

                                    int statusCode = 0;
                                    //Task.Run(async () => await Utilities.SendPostRequestAsync("https://kagetracker.gamba.pro/update_dealer", jsonData));
                                    _ = Task.Run(async () =>
                                    {
                                        statusCode = await Utilities.SendPostRequestAsync("https://kagekagetracker.gamba.pro/update_dealer", jsonData);
                                        PluginLog.Verbose($"Operation completed with status code: {statusCode}");

                                        if (statusCode == 400)
                                        {
                                            Plugin.Configuration.isDealing = false;
                                            Plugin.Configuration.Save();
                                            currentStatus = "Not Dealing";
                                            _updateTimer?.Change(Timeout.Infinite, 0); // Stop the timer
                                            _updateTimer?.Dispose(); // Dispose of the timer
                                            _isUpdating = false;
                                            var message = new SeStringBuilder().AddUiForeground((ushort)16).AddText("You are not authorized to deal").AddUiForegroundOff().Build();
                                            Svc.Chat.Print(new() { Message = message });
                                        }
                                    });


                                }
                            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
                        }



                    }
                });
            }

            if (currentStatus == "Currently Dealing")
            {
                ImGuiEx.RightFloat(() =>
                {
                    if (ImGui.Button("Stop Dealing"))
                    {
                        if (_isUpdating)
                        {
                            Plugin.Configuration.isDealing = false;
                            Plugin.Configuration.Save();
                            currentStatus = "Not Dealing";
                            _updateTimer?.Change(Timeout.Infinite, 0); // Stop the timer
                            _updateTimer?.Dispose(); // Dispose of the timer
                            _isUpdating = false;

                            SeString name = Svc.ClientState.LocalPlayer.Name;
                            String homeworld = Svc.ClientState.LocalPlayer.HomeWorld.Value.Name.ToString();

                            // Declare the 'data' variable as an anonymous type with default values
                            var data = new
                            {
                                dealer_name = string.Empty,
                                venue_name = "None",
                                location = "None",
                                player_count = UniversalParty.Members.Count,
                                dealer_key = dealerKey
                            };

                            // Assign values based on the DebugMode condition
                            if (Plugin.Configuration.DebugMode)
                            {
                                data = new
                                {
                                    dealer_name = $"{Plugin.Configuration.DebugDealer}",
                                    venue_name = data.venue_name,
                                    location = data.location,
                                    player_count = data.player_count,
                                    dealer_key = data.dealer_key
                                };
                            }
                            else
                            {
                                data = new
                                {
                                    dealer_name = $"{name}@{homeworld}",
                                    venue_name = data.venue_name,
                                    location = data.location,
                                    player_count = data.player_count,
                                    dealer_key = data.dealer_key
                                };
                            }

                            string jsonData = JsonSerializer.Serialize(data);

                            Task.Run(async () => await Utilities.SendPostRequestAsync("https://kagetracker.gamba.pro/update_dealer", jsonData));
                        }
                    }
                });
            }


            System.Numerics.Vector4 color = new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f); // Default to white

            if (currentStatus == "Currently Dealing")
            {
                color = new System.Numerics.Vector4(0.0f, 1.0f, 0.0f, 1.0f); // Green for active
            }
            else if (currentStatus == "Not Dealing")
            {
                color = new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f); // Red for inactive
            }

            // Display "Status: " in default color
            ImGui.Text("Status: ");

            // Same line to keep the text on the same line
            ImGui.SameLine();

            // Display currentStatus in the specified color
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.Text(currentStatus);
            ImGui.PopStyleColor();

            ImGui.NewLine();

            // Create a simple header
            ImGui.Text("Venue Options");

            // Optional: Draw a separator line
            ImGui.Separator();

            if (currentStatus == "Currently Dealing")
            {
                ImGui.BeginDisabled();
            }



            if (currentVenueIndex == -1) currentVenueIndex = 0; // Default to first item if not found
            if (ImGui.Combo("Venue", ref currentVenueIndex, validVenues, validVenues.Length))
            {
                // Update the configuration with the new selection
                Plugin.Configuration.CurrentVenueDropdown = validVenues[currentVenueIndex];
                Plugin.Configuration.Save(); // Save your configuration
            }

            // Buffer sizes for input text
            int inputTextSize = 100; // Adjust size as needed


            if (validVenues[currentVenueIndex] == "Custom")
            {

                // InputText for Venue Name
                if (ImGui.InputText("Venue Name", ref customVenueName, (uint)inputTextSize))
                {
                    // Update the configuration with the new venue name (trim any excess space)
                    Plugin.Configuration.CustomVenueName = customVenueName.Trim();
                    Plugin.Configuration.Save(); // Save your configuration
                }

                // InputText for Venue Location
                if (ImGui.InputText("Venue Location", ref customVenueLocation, (uint)inputTextSize))
                {
                    // Update the configuration with the new venue location (trim any excess space)
                    Plugin.Configuration.CustomVenueLocation = customVenueLocation.Trim();
                    Plugin.Configuration.Save(); // Save your configuration
                }
            }

            if (currentGameIndex == -1) currentGameIndex = 0; // Default to first item if not found
            if (ImGui.Combo("Game", ref currentGameIndex, validGames, validGames.Length))
            {
                // Update the configuration with the new selection
                Plugin.Configuration.CurrentGameDropdown = validGames[currentGameIndex];
                Plugin.Configuration.Save(); // Save your configuration
            }


            if (validGames[currentGameIndex] == "Blackjack")
            {
                if (currentStandIndex == -1) currentStandIndex = 0; // Default to first item if not found
                if (ImGui.Combo("Dealer Stands", ref currentStandIndex, validStandOptions, validStandOptions.Length))
                {
                    // Update the configuration with the new selection
                    Plugin.Configuration.CurrentStandDropdown = validStandOptions[currentStandIndex];
                    Plugin.Configuration.Save(); // Save your configuration
                }
            }

            string betlimitName = "";
            if (validGames[currentGameIndex] == "Blackjack" || validGames[currentGameIndex] == "Roulette") { betlimitName = "Bet Limits"; } else { betlimitName = "Buy-in"; }


            if (ImGui.InputText(betlimitName, ref betLimits, (uint)inputTextSize))
            {
                // Update the configuration with the new venue location (trim any excess space)
                Plugin.Configuration.BetLimits = betLimits.Trim();
                Plugin.Configuration.Save(); // Save your configuration
            }

            if (validGames[currentGameIndex] == "Deathroll Tournament")
            {
                if (ImGui.InputText("Start Time", ref startTime, (uint)inputTextSize))
                {
                    // Update the configuration with the new venue location (trim any excess space)
                    Plugin.Configuration.StartTime = startTime.Trim();
                    Plugin.Configuration.Save(); // Save your configuration
                }
            }

            if(Plugin.Configuration.DebugMode)
            {
                ImGui.NewLine();
                ImGui.Text("Debug");
                ImGui.Separator();

                if (currentDebugDealerIndex == -1) currentDebugDealerIndex = 0; // Default to first item if not found
                if (ImGui.Combo("Dealer", ref currentDebugDealerIndex, validDealers, validDealers.Length))
                {
                    // Update the configuration with the new selection
                    Plugin.Configuration.DebugDealer = validDealers[currentDebugDealerIndex];
                    Plugin.Configuration.Save(); // Save your configuration
                }

                int debugPartySize = Plugin.Configuration.DebugPartySize;
                if (ImGui.InputInt("Party Size",ref debugPartySize))
                {
                    Plugin.Configuration.DebugPartySize = debugPartySize;
                    Plugin.Configuration.Save();
                }
            }


            if (currentStatus == "Currently Dealing")
            {
                ImGui.EndDisabled();
            }


        }catch (Exception ex)
        {
            PluginLog.Error($"Encountered an error: {ex}");
        }
    }
}
