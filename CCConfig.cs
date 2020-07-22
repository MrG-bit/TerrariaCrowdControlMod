///<summary>
/// File: CCConfig.cs
/// Last Updated: 2020-07-23
/// Author: MRG-bit
/// Description: Configuration for the mod.
///</summary>

using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace CrowdControlMod
{
    public class CCConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Label("Show Effect Messages In Chat")]
        [Tooltip("Disable this to stop effect messages from showing in chat. Useful for if you would like to use the browser source.")]
        [DefaultValue(true)]
        public bool ShowEffectMessagesInChat;

        [Label("Allow Connecting To Crowd Control")]
        [Tooltip("Disable this to stop the mod from connecting to Crowd Control upon entering a world if you need to do some testing.")]
        [DefaultValue(true)]
        public bool ConnectToCrowdControl;

        // Called when configuration parameters are changed by the user
        public override void OnChanged()
        {
            CCServer._showEffectMessages = ShowEffectMessagesInChat;
            CCServer._shouldConnectToCC = ConnectToCrowdControl;

            base.OnChanged();
        }
    }
}
