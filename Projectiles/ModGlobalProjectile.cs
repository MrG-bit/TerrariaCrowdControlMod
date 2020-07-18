///<summary>
/// File: ModGlobalProjectile.cs
/// Last Updated: 2020-07-18
/// Author: MRG-bit
/// Description: Change things for every Projectile in the game
///</summary>

using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CrowdControlMod.Projectiles
{
    public class ModGlobalProjectile : GlobalProjectile
    {
        // Override default values for a new projectile
        public override void SetDefaults(Projectile projectile)
        {
            base.SetDefaults(projectile);
        }

        // Called when a projectile hits a tile
        public override bool OnTileCollide(Projectile projectile, Vector2 oldVelocity)
        {
            // Create splash of colour on nearby tiles when collision occurs
            if (CrowdControlMod._server != null && CrowdControlMod._server.m_rainbowPaintTimer.Enabled && projectile.owner == Main.myPlayer && !projectile.minion && !projectile.sentry)
            {
                int x = (int)(projectile.Center.X / 16);
                int y = (int)(projectile.Center.Y / 16);
                CrowdControlMod._server.RainbowifyTileClient(x - 1, y, true);
                CrowdControlMod._server.RainbowifyTileClient(x - 1, y - 1, true);
                CrowdControlMod._server.RainbowifyTileClient(x - 1, y + 1, true);
                CrowdControlMod._server.RainbowifyTileClient(x, y, true);
                CrowdControlMod._server.RainbowifyTileClient(x, y - 1, true);
                CrowdControlMod._server.RainbowifyTileClient(x, y + 1, true);
                CrowdControlMod._server.RainbowifyTileClient(x + 1, y, true);
                CrowdControlMod._server.RainbowifyTileClient(x + 1, y - 1, true);
                CrowdControlMod._server.RainbowifyTileClient(x + 1, y + 1, true);
            }

            return base.OnTileCollide(projectile, oldVelocity);
        }
    }
}
