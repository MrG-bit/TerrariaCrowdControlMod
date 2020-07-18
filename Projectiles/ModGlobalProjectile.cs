///<summary>
/// File: ModGlobalProjectile.cs
/// Last Updated: 2020-07-18
/// Author: MRG-bit
/// Description: Change things for every Projectile in the game
///</summary>

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CrowdControlMod.Projectiles
{
    public class ModGlobalProjectile : GlobalProjectile
    {
        public static int m_textureOffset = 0;      // Texture offset

        // Called before the projectile is drawn (false means no draw)
        public override bool PreDraw(Projectile projectile, SpriteBatch spriteBatch, Color lightColor)
        {
            // Don't draw projectile if timer is enabled
            if (CrowdControlMod._server != null && CrowdControlMod._server.m_projItemTimer.Enabled)
                return false;

            return base.PreDraw(projectile, spriteBatch, lightColor);
        }

        // Called after the projectile is drawn (to draw things above the projectile)
        public override void PostDraw(Projectile projectile, SpriteBatch spriteBatch, Color lightColor)
        {
            // Draw an item instead of the projectile
            if (CrowdControlMod._server != null && CrowdControlMod._server.m_projItemTimer.Enabled)
            {
                Texture2D texture = Main.itemTexture[(projectile.type + m_textureOffset) % Main.itemTexture.Length];
                Vector2 drawOrigin = texture.Size() / 2;
                Vector2 drawPos = projectile.position - Main.screenPosition + drawOrigin + new Vector2(0f, projectile.gfxOffY);

                spriteBatch.Draw(
                    texture: texture, 
                    position: drawPos, 
                    sourceRectangle: null, 
                    color: lightColor,
                    rotation: projectile.rotation, 
                    origin: drawOrigin, 
                    scale: projectile.scale,
                    effects: SpriteEffects.None, 
                    layerDepth: 0f);
            }

            base.PostDraw(projectile, spriteBatch, lightColor);
        }

        // Called when a projectile is killed
        public override void Kill(Projectile projectile, int timeLeft)
        {
            // Create splash of colour on nearby tiles when collision occurs
            if (CrowdControlMod._server != null && CrowdControlMod._server.m_rainbowPaintTimer.Enabled &&
                projectile.owner == Main.myPlayer && !projectile.minion && !projectile.sentry && !projectile.bobber
                && !Main.lightPet[projectile.type] && !Main.projPet[projectile.type] && !Main.vanityPet[projectile.type])
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

            base.Kill(projectile, timeLeft);
        }
    }
}
