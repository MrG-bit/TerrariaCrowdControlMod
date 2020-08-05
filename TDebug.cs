///<summary>
/// File: TDebug.cs
/// Last Updated: 2020-08-06
/// Author: MRG-bit
/// Description: Simple static class for displaying chat messages
///</summary>

using Terraria;
using Microsoft.Xna.Framework;

namespace CrowdControlMod
{
    public static class TDebug
    {
        public static bool _debugMode = false;

        // Send a message in chat if IN_DEBUG is true
        public static void WriteDebug(string text, Color colour = default)
        {
            if (!_debugMode)
                return;

            if (Main.dedServ)
                NetMessage.BroadcastChatMessage(Terraria.Localization.NetworkText.FromLiteral("[D] " + text), colour);
            else
                Main.NewText(Terraria.Localization.NetworkText.FromLiteral("[D] " + text), colour);
        }

        // Send a message in chat
        public static void WriteMessage(string text, Color colour = default)
        {
            Main.NewText(Terraria.Localization.NetworkText.FromLiteral(text), colour);
        }

        // Send a message in chat with an item prefix
        public static void WriteMessage(int itemType, string text, Color colour = default)
        {
            if (itemType == 0) WriteMessage(text, colour);
            else WriteMessage("[i:" + itemType + "] " + text, colour);
        }
    }
}
