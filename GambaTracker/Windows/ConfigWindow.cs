using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using GambaTracker.Helpers;
using ImGuiNET;

namespace GambaTracker.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    public ConfigWindow(Plugin plugin) : base(
        "GambaTracker Configuration",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.Size = new Vector2(265, 150);
        this.SizeCondition = ImGuiCond.Always;

        this.Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var dealerKey = Configuration.DealerKey;
        if (ImGui.InputText("Dealer Key", ref dealerKey, (uint)50))
        {
            // Update the configuration with the new venue location (trim any excess space)
            Configuration.DealerKey = dealerKey.Trim();
            Configuration.Save(); // Save your configuration
        }


        if(ImGui.Button("Update Approved Dealers and Venues"))
        {
            // Pull the latest dealers from the server
            Task.Run(async () => await Utilities.FetchValidDealersAsync());

            // Pull the latest venues from the server
            Task.Run(async () => await Utilities.FetchValidVenuesAsync());
        }
    }
}
