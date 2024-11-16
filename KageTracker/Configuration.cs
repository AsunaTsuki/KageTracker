using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace KageTracker
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public string DealerKey { get; set; } = "";
        public bool DebugMode { get; set; } = false;
        public string DebugDealer { get; set; } = "";
        public int DebugPartySize { get; set; } = 0;
        public bool isDealing { get; set; } = false;
        public string CurrentVenueDropdown { get; set; } = "Custom";
        public string CustomVenueName { get; set; } = "";
        public string CustomVenueLocation { get; set; } = "";
        public string BetLimits { get; set; } = "";
        public string CurrentGameDropdown { get; set; } = "Blackjack";
        public string CurrentStandDropdown { get; set; } = "16";
        public string StartTime { get; set; } = "";
        public string[] Venues {  get; set; } = ["Custom"];
        public string[] Dealers { get; set; } = [];

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private IDalamudPluginInterface? PluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.PluginInterface!.SavePluginConfig(this);
        }
    }
}
