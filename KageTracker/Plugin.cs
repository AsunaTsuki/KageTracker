using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using KageTracker.Windows;
using ECommons;
using KageTracker.Helpers;
using System.Threading.Tasks;
using Dalamud.Logging;
using System.Threading;
using ECommons.DalamudServices;
using System;
using Dalamud.Game.Text.SeStringHandling;
using System.Linq;
using ECommons.Logging;

namespace KageTracker
{
    public sealed partial class Plugin : IDalamudPlugin
    {
        public string Name => "KageTracker";
        private const string CommandName = "/kagegamba";
        private const string SettingsCommandName = "/kagesetup";
        
        private IDalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("KageTracker");

        private ConfigWindow ConfigWindow { get; init; }
        
        private MainWindow MainWindow { get; init; }
        private PopupWindow _confirmationPopup;

        public static Plugin P;


        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            ECommonsMain.Init(pluginInterface, this);
            P = this;

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);


            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this);


            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);

            _confirmationPopup = new PopupWindow("KageTracker", "It looks like you were still dealing when KageTracker crashed.\n Would you like to open the KageTracker plugin so you can continue dealing?", OnYes, OnNo);

            this.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the KageTracker window"
            });

            this.CommandManager.AddHandler(SettingsCommandName, new CommandInfo(OnSettingsCommand)
            {
                HelpMessage = "Opens the KageTracker settings window"
            });

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenMainUi += DrawMainWindow;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            // Pull the latest venues from the server
            Task.Run(async () => await Utilities.FetchValidVenuesAsync());

            // Pull the latest dealers from the server
            Task.Run(async () => await Utilities.FetchValidDealersAsync());

            // Trigger the confirmation popup if they were dealing when the plugin was stopped/crashed
            if (this.Configuration.isDealing == true)
            {
                Svc.Framework.Update += OnFrameworkUpdateOnce;
            }

            // Check that the selected one in the config matches a venue in the list, otherwise set to first in list
            //Utilities.ValidateCurrentVenueDropdown();
        }

        private void OnYes()
        {
            // Handle Yes action
            PluginLog.Verbose("Pressed Yes");
            ToggleDealerWindow();
        }

        private void OnNo()
        {
            // Handle No action
            PluginLog.Verbose("Pressed No");
            this.Configuration.isDealing = false;
            MainWindow.currentStatus = "Not Dealing";
            MainWindow._updateTimer?.Change(Timeout.Infinite, 0); // Stop the timer
            MainWindow._updateTimer?.Dispose(); // Dispose of the timer
            MainWindow._isUpdating = false;
            this.Configuration.Save();
        }

        // This method triggers the popup
        public void TriggerPopup()
        {
            _confirmationPopup.Show();
        }

        private void OnFrameworkUpdateOnce(IFramework framework)
        {
            // Check if the player is targetable
            if (Svc.ClientState.LocalPlayer?.IsTargetable == true)
            {
                // Unsubscribe to ensure this check is only done once
                Svc.Framework.Update -= OnFrameworkUpdateOnce;

                // Now trigger the popup
                TriggerPopup();
            }
        }


        public void Dispose()
        {
            this.WindowSystem.RemoveAllWindows();
            
            ConfigWindow.Dispose();
            MainWindow.Dispose();
            _confirmationPopup.Dispose();

            // Unsubscribe from the Framework update event
            Svc.Framework.Update -= OnFrameworkUpdateOnce;

            this.CommandManager.RemoveHandler(CommandName);
        }

        private void ToggleDealerWindow()
        {


            if (this.MainWindow.IsOpen)
            {
                // The window is currently open, so close it
                this.MainWindow.IsOpen = false;
            }
            else
            {
                string dealerName = Svc.ClientState?.LocalPlayer.Name.ToString();
                string dealerWorld = Svc.ClientState?.LocalPlayer.HomeWorld.Value.Name.ToString();
                string dealerNameWorld = $"{dealerName}@{dealerWorld}";
                var validDealers = P.Configuration.Dealers;
                var dealerKey = P.Configuration.DealerKey;
                PluginLog.Verbose($"Character name: {dealerName}@{dealerWorld}");

                // Pull the latest dealers from the server
                Task.Run(async () => await Utilities.FetchValidDealersAsync());

                // Pull the latest venues from the server
                Task.Run(async () => await Utilities.FetchValidVenuesAsync());

                if (validDealers.Contains(dealerNameWorld))
                {
                    this.MainWindow.IsOpen = true;
                }
                else
                {
                    Svc.Chat.Print("You are not an authorized dealer");
                }

                if (dealerKey == "")
                {
                    Svc.Chat.Print("Please make sure your dealer key is set in /gambasetup as it is currently blank.");
                }
            }
        }

        private void ToggleSettingsWindow()
        {
            // Assuming you have an instance of your MainWindow accessible
            // For example, this could be stored in a property like this.PluginUi.MainWindow

            if (this.ConfigWindow.IsOpen)
            {
                // The window is currently open, so close it
                this.ConfigWindow.IsOpen = false;
            }
            else
            {
                this.ConfigWindow.IsOpen = true;
            }
        }



        private void OnCommand(string command, string args)
        {
            SeString name = Svc.ClientState.LocalPlayer?.Name;
            String homeworld = Svc.ClientState.LocalPlayer?.HomeWorld.Value.Name.ToString();

            string nameWorld = $"{name}@{homeworld}";
            if (args == "debug")
            {
                if (nameWorld == "Asuna Tsuki@Phoenix" || nameWorld == "Asuna Tsuki@Midgardsormr")
                {
                    if (this.Configuration.DebugMode == false)
                    {
                        this.Configuration.DebugMode = true;
                    } else
                    {
                        this.Configuration.DebugMode = false;
                    }
                    this.Configuration.Save();
                }else
                {
                    this.Configuration.DebugMode = false;
                    this.Configuration.Save();
                    ToggleDealerWindow();
                }
            }else
            {
                ToggleDealerWindow();
            }

        }

        private void OnSettingsCommand(string command, string args)
        {
            ToggleSettingsWindow();
        }

        public void DrawUI()
        {
            this.WindowSystem.Draw();
            _confirmationPopup.Draw();
        }

        private void DrawMainWindow()
        {
            this.Configuration.DebugMode = false;
            this.Configuration.Save();
            ToggleDealerWindow();
        }

        public void DrawConfigUI()
        {
            ConfigWindow.IsOpen = true;
        }
    }
}
