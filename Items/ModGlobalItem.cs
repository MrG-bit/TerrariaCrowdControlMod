///<summary>
/// File: ModGlobalItem.cs
/// Last Updated: 2020-08-06
/// Author: MRG-bit
/// Description: Change things for every Item in the game
///</summary>

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CrowdControlMod.Items
{
    public class ModGlobalItem : GlobalItem
    {
        // Called to determine if an item should be consumed upon use
        public override bool ConsumeItem(Item item, Player player)
        {
            //if (Main.myPlayer == player.whoAmI && player.team > 0 && Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient && CrowdControlMod._server != null && CrowdControlMod._server.IsRunning && item.type == Terraria.ID.ItemID.WormholePotion && CCServer._allowTeleportingToPlayers)
            //    return false;
            return base.ConsumeItem(item, player);
        }
    }
}
