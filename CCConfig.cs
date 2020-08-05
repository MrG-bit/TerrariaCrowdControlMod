﻿///<summary>
/// File: CCConfig.cs
/// Last Updated: 2020-08-06
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
        [Tooltip("Disable this to stop effect messages from showing in chat.\nUseful for if you would like to use the browser source.")]
        [DefaultValue(true)]
        public bool ShowEffectMessagesInChat;

        [Label("Allow Connecting To Crowd Control")]
        [Tooltip("Disable this to stop the mod from connecting to Crowd Control upon entering a world.\nUseful if you need to do some testing.")]
        [DefaultValue(true)]
        public bool ConnectToCrowdControl;

        [Label("Disable Tombstones")]
        [Tooltip("Enable this to prevent your tombstones from spawning when you die.")]
        [DefaultValue(false)]
        public bool DisableTombstones;

        [Label("Respawn Timer")]
        [Tooltip("Reduce the respawn timer by this factor.\nThis allows you to get back into the game quicker after getting killed.\nx1 is default time.")]
        [Range(0.4f, 1f)]
        [Increment(0.1f)]
        [DrawTicks]
        [DefaultValue(1f)]
        public float RespawnTime;

        [Label("Allow Effects To Set Hair Dye")]
        [Tooltip("Disable this to stop certain effects from changing the player's hair dye.")]
        [DefaultValue(true)]
        public bool UseHairDyes;

        [Label("Enable Effect Music")]
        [Tooltip("Disable this to stop some effects from play fitting music whilst active.\nThis is used by most of the effects that alter the screen.")]
        [DefaultValue(true)]
        public bool EnableEffectMusic;

        [Label("Reduce Drunk Effect")]
        [Tooltip("Enable this to prevent the screen from being moved during the drunk-mode effect.")]
        [DefaultValue(false)]
        public bool ReduceDrunkEffect;

        [Label("Reduce Corruption Effect")]
        [Tooltip("Enable this to altar the corrupt-screen effect to be less flashy.\nThe effect will instead show a moving wave of darkness over the tiles.")]
        [DefaultValue(false)]
        public bool ReduceCorruptionEffect;

        [Label("[Advanced] Show Developer Messages In Chat")]
        [Tooltip("Enable this to show developer messages in chat.\nThis is for debugging purposes for advanced users only.")]
        [DefaultValue(false)]
        public bool ShowDeveloperMessages;

        // Called when configuration parameters are changed by the user
        public override void OnChanged()
        {
            CCServer._showEffectMessages = ShowEffectMessagesInChat;
            CCServer._shouldConnectToCC = ConnectToCrowdControl;
            CCServer._disableTombstones = DisableTombstones;
            CCServer._respawnTimeFactor = RespawnTime;
            CCServer._disableHairDye = !UseHairDyes;
            CCServer._disableMusic = !EnableEffectMusic;
            CCServer._reduceDrunkEffect = ReduceDrunkEffect;
            CCServer._reduceCorruptEffect = ReduceCorruptionEffect;
            TDebug._debugMode = ShowDeveloperMessages;

            if (CrowdControlMod._server != null)
                CrowdControlMod._server.SendConfigToServer();

            base.OnChanged();
        }
    }
}
