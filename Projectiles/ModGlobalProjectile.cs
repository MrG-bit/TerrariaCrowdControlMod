///<summary>
/// File: ModGlobalProjectile.cs
/// Last Updated: 2020-08-02
/// Author: MRG-bit
/// Description: Change things for every Projectile in the game
///</summary>

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CrowdControlMod.Projectiles
{
    public class ModGlobalProjectile : GlobalProjectile
    {
        public static int m_textureOffset = 0;                      // Texture offset
        public static int[] m_graves = new int[] {                  // Gravestones
            Terraria.ID.ProjectileID.Tombstone,
            Terraria.ID.ProjectileID.GraveMarker,
            Terraria.ID.ProjectileID.CrossGraveMarker,
            Terraria.ID.ProjectileID.Headstone,
            Terraria.ID.ProjectileID.Gravestone,
            Terraria.ID.ProjectileID.Obelisk,
            Terraria.ID.ProjectileID.RichGravestone1,
            Terraria.ID.ProjectileID.RichGravestone2,
            Terraria.ID.ProjectileID.RichGravestone3,
            Terraria.ID.ProjectileID.RichGravestone4,
            Terraria.ID.ProjectileID.RichGravestone5,
        };

        // Called before AI is run
        public override bool PreAI(Projectile projectile)
        {
            // Disable tombstones
            if (CrowdControlMod._server != null && ((Main.netMode != Terraria.ID.NetmodeID.Server && CCServer._disableTombstones) || Main.netMode == Terraria.ID.NetmodeID.Server) && m_graves.Contains(projectile.type))
            {
                if (Main.netMode == Terraria.ID.NetmodeID.Server)
                {
                    try
                    {
                        CCPlayer player = Main.player[projectile.owner].GetModPlayer<CCPlayer>();
                        if (player.m_servDisableTombstones)
                        {
                            projectile.active = false;
                            NetMessage.SendData(Terraria.ID.MessageID.SyncNPC, -1, player.player.whoAmI, null, projectile.whoAmI);
                            TDebug.WriteDebug("Server disabled " + player.player.name + "'s tombstone", Color.Yellow);
                            return false;
                        }
                    }
                    catch { }
                }
                else
                {
                    projectile.active = false;
                    TDebug.WriteDebug("Client disabled tombstone", Color.Yellow);
                    return false;
                }
            }

            return base.PreAI(projectile);
        }

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
            // Create splash of colour on nearby tiles when the projectile is destroyed
            if (CrowdControlMod._server != null && CrowdControlMod._server.m_rainbowPaintTimer.Enabled &&
                projectile.owner == Main.myPlayer && !projectile.minion && !projectile.sentry && !projectile.bobber)
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

        // Called before the projectile is killed
        public override bool PreKill(Projectile projectile, int timeLeft)
        {
            /*
            // Prevent bombs from destroying tiles
            if (CrowdControlMod._server.m_shootBombTimer.Enabled && (projectile.type == Terraria.ID.ProjectileID.Bomb || projectile.type == Terraria.ID.ProjectileID.StickyBomb ||
                projectile.type == Terraria.ID.ProjectileID.BouncyBomb || projectile.type == Terraria.ID.ProjectileID.Dynamite || projectile.type == Terraria.ID.ProjectileID.BouncyDynamite))
            {
                // Play sound and create particles
                Main.PlaySound(Terraria.ID.SoundID.Item14, projectile.position);
                for (int i = 0; i < 50; i++)
                {
                    int dustIndex = Dust.NewDust(new Vector2(projectile.position.X, projectile.position.Y), projectile.width, projectile.height, 31, 0f, 0f, 100, default, 2f);
                    Main.dust[dustIndex].velocity *= 1.4f;
                }
                return false;
            }
            */

            return base.PreKill(projectile, timeLeft);
        }
    }
}
