///<summary>
/// File: ModGlobalItem.cs
/// Last Updated: 2020-07-22
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
        /*
        public static int m_textureOffset = 0;      // Texture offset

        // called before the item is drawn in the world (false means no draw)
        public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            // Don't draw item if timer is enabled
            if (CrowdControlMod._server != null && CrowdControlMod._server.m_projItemTimer.Enabled)
                return false;

            return base.PreDrawInWorld(item, spriteBatch, lightColor, alphaColor, ref rotation, ref scale, whoAmI);
        }

        // Called before the item is drawn in the inventory (false means no draw)
        public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            // Don't draw item if timer is enabled
            if (CrowdControlMod._server != null && CrowdControlMod._server.m_projItemTimer.Enabled)
                return false;

            return base.PreDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale);
        }

        // Called to add graphics to the inventory
        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            // Draw a random item
            if (CrowdControlMod._server != null && CrowdControlMod._server.m_projItemTimer.Enabled)
            {
                Texture2D texture = Main.itemTexture[(item.type + m_textureOffset) % Main.itemTexture.Length];

                spriteBatch.Draw(
                    texture: texture,
                    position: position,
                    sourceRectangle: null,
                    color: Color.White,
                    rotation: 0f,
                    origin: origin,
                    scale: 1f,
                    effects: SpriteEffects.None,
                    layerDepth: 0f);
            }

            base.PostDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale);
        }

        // Called to add graphics to the world
        public override void PostDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
        {
            // Draw a random item
            if (CrowdControlMod._server != null && CrowdControlMod._server.m_projItemTimer.Enabled)
            {
                Texture2D texture = Main.itemTexture[(item.type + m_textureOffset) % Main.itemTexture.Length];
                Vector2 drawOrigin = texture.Size() / 2;
                Vector2 drawPos = item.position - Main.screenPosition + drawOrigin;

                spriteBatch.Draw(
                    texture: texture,
                    position: drawPos,
                    sourceRectangle: null,
                    color: lightColor,
                    rotation: rotation,
                    origin: drawOrigin,
                    scale: scale,
                    effects: SpriteEffects.None,
                    layerDepth: 0f);
            }

            base.PostDrawInWorld(item, spriteBatch, lightColor, alphaColor, rotation, scale, whoAmI);
        }
        */
    }
}
