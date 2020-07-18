///<summary>
/// File: ModGlobalNPC.cs
/// Last Updated: 2020-07-15
/// Author: MRG-bit
/// Description: Change things for every NPC in the game
///</summary>

using Terraria;
using Terraria.ModLoader;

namespace CrowdControlMod.NPCs
{
    public class ModGlobalNPC : GlobalNPC
    {
        // Override the spawn rate for every NPC
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            // Increase spawnrate per player
            CCPlayer ccPlayer = player.GetModPlayer<CCPlayer>();
            if (ccPlayer != null)
            {
                spawnRate = (int)(spawnRate / ccPlayer.m_spawnRate);
                maxSpawns = (int)(maxSpawns * ccPlayer.m_spawnRate);
            }
        }
    }
}
