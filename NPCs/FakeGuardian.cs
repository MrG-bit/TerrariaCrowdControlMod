///<summary>
/// File: FakeGuardian.cs
/// Last Updated: 2020-08-06
/// Author: MRG-bit
/// Description: Fake Dungeon Guardian
///</summary>

using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CrowdControlMod.NPCs
{
    public class FakeGuardian : ModNPC
    {
        public override string Texture => "Terraria/NPC_" + Terraria.ID.NPCID.DungeonGuardian;
        private int timeLeft = 0;

        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Dungeon Guardian");
            base.SetStaticDefaults();
        }

        public override void SetDefaults()
        {
            npc.CloneDefaults(Terraria.ID.NPCID.DungeonGuardian);
            aiType = Terraria.ID.NPCID.DungeonGuardian;
            npc.aiStyle = 11;
            timeLeft = 60 * 6;
            base.SetDefaults();
        }

        public override bool PreAI()
        {
            npc.type = Terraria.ID.NPCID.DungeonGuardian;

            timeLeft--;
            if (timeLeft == 0)
            {
                npc.ai[1] = 3f;
                string message = "[i:" + Terraria.ID.ItemID.WhoopieCushion + "] The Dungeon Guardian was a phony";
                if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer)
                {
                    Main.NewText(message, Color.Green);
                }
                else if (Main.netMode == Terraria.ID.NetmodeID.Server)
                {
                    NetMessage.SendChatMessageToClient(Terraria.Localization.NetworkText.FromLiteral(message), Color.Green, (int)npc.ai[NPC.maxAI - 1]);
                }
            }

            return base.PreAI();
        }

        // Whether the NPC can hit players
        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            return false;
        }

        // Whether the NPC can hit NPCs
        public override bool? CanHitNPC(NPC target)
        {
            return false;
        }
    }
}
