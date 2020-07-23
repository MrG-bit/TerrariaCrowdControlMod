///<summary>
/// File: ModGlobalWall.cs
/// Last Updated: 2020-07-23
/// Author: MRG-bit
/// Description: Change things for every Wall in the game
///</summary>

using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CrowdControlMod.Walls
{
    public class ModGlobalWall : GlobalWall
    {
        // Override drawing tile
        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (CrowdControlMod._server != null && CrowdControlMod._server.m_rainbowScreenTimer.Enabled)
            {
                Texture2D texture;
                if (Main.canDrawColorWall(i, j))
                    texture = Main.wallAltTexture[type, Main.tile[i, j].color()];
                else
                    texture = Main.wallTexture[type];

                Vector2 zero = new Vector2(Main.offScreenRange, Main.offScreenRange);
                if (Main.drawToScreen)
                    zero = Vector2.Zero;

                spriteBatch.Draw(
                    texture: texture,
                    position: (new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y) + zero) - new Vector2(32f, 32f),
                    sourceRectangle: new Rectangle(Main.tile[i, j].wallFrameX() + Main.wallFrame[type], Main.tile[i, j].wallFrameX(), 16, 16),
                    color: Main.hslToRgb(((Main.GlobalTime * -0.75f) + (j * 0.05f)) % 1f, 1f, 0.5f),
                    rotation: 0f,
                    origin: Vector2.Zero,
                    scale: 4f,
                    effects: SpriteEffects.None,
                    layerDepth: 0f);

                return false;
            }

            return base.PreDraw(i, j, type, spriteBatch);
        }
    }
}