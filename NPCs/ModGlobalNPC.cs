///<summary>
/// File: ModGlobalNPC.cs
/// Last Updated: 2020-07-18
/// Author: MRG-bit
/// Description: Change things for every NPC in the game
///</summary>

using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace CrowdControlMod.NPCs
{
    public class ModGlobalNPC : GlobalNPC
    {
        private static readonly Vector2[] m_paintDir =                          // Directions that the paint projectiles will shoot in
        {
            new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1),
            new Vector2(0, -1), new Vector2(0, 1),
            new Vector2(1, -1), new Vector2(1, 0), new Vector2(1, 1)
        };
        private static readonly float m_paintMinSpeed = 3f;                     // Minimum speed of paint projectiles
        private static readonly float m_paintMaxSpeed = 6f;                     // Maximum speed of paint projectiles

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

        // Called on the client when an NPC is hit
        public override void HitEffect(NPC npc, int hitDirection, double damage)
        {
            // Spawn paintballs if the NPC dies and rainbow-ify is enabled
            if (CrowdControlMod._server != null && CrowdControlMod._server.m_rainbowPaintTimer.Enabled && npc.life - damage <= 0)
                for (int i = 0; i < m_paintDir.Length; ++i)
                    Projectile.NewProjectile(npc.position, Vector2.Normalize(m_paintDir[i]) * Main.rand.NextFloat(m_paintMinSpeed, m_paintMaxSpeed), Terraria.ID.ProjectileID.PainterPaintball, 0, 0f, Main.myPlayer, 0, Main.rand.NextFloat());
            base.HitEffect(npc, hitDirection, damage);
        }
    }
}
