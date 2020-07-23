///<summary>
/// File: ModGlobalTile.cs
/// Last Updated: 2020-07-23
/// Author: MRG-bit
/// Description: Change things for every Tile in the game
///</summary>

using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CrowdControlMod.Tiles
{
    public class ModGlobalTile : GlobalTile
    {
        // Override drawing tile
        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (CrowdControlMod._server != null && (CrowdControlMod._server.m_rainbowScreenTimer.Enabled || CrowdControlMod._server.m_corruptScreenTimer.Enabled))
            {
                Texture2D texture;
                if (Main.canDrawColorTile(i, j))
                    texture = Main.tileAltTexture[type, Main.tile[i, j].color()];
                else
                    texture = Main.tileTexture[type];

                Vector2 zero = new Vector2(Main.offScreenRange, Main.offScreenRange);
                if (Main.drawToScreen)
                    zero = Vector2.Zero;

                Color c;
                if (CrowdControlMod._server.m_rainbowScreenTimer.Enabled)
                    c = Main.hslToRgb(((Main.GlobalTime * 0.75f) + (j * 0.05f)) % 1f, 1f, 0.5f);
                else
                    c = new Color(Main.rand.NextFloat(), Main.rand.NextFloat(), Main.rand.NextFloat(), Main.rand.NextFloat());

                spriteBatch.Draw(
                    texture: texture,
                    position: (new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y) + zero),
                    sourceRectangle: new Rectangle(Main.tile[i, j].frameX + Main.tileFrame[type], Main.tile[i, j].frameY, 16, 16),
                    color: c,
                    rotation: 0f,
                    origin: Vector2.Zero,
                    scale: 1f,
                    effects: SpriteEffects.None,
                    layerDepth: 0f);

                return false;
            }

            return base.PreDraw(i, j, type, spriteBatch);
        }
    }
}
