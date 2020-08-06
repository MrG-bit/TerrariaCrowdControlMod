///<summary>
/// File: CrowdControlMod.cs
/// Last Updated: 2020-08-06
/// Author: MRG-bit
/// Description: Main mod file
///</summary>

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CrowdControlMod
{
	public class CrowdControlMod : Mod
	{
		public static CrowdControlMod _mod = null;						// Reference to the mod
		public static CCServer _server = null;                          // Reference to the server
		private bool m_mapOpened = false;

        public override uint ExtraPlayerBuffSlots => 27;				// Extra buff slots that the player can use (22 + ExtraPlayerBuffSlots = Player.MaxBuffs)

		// Called when the mod is loaded
		public override void Load()
		{
			base.Load();

			_mod = this;
			// Instantiate server instance (but don't start it)
			_server = new CCServer();

			// Load shader(s)
			if (!Main.dedServ)
			{
				try
				{
					Ref<Effect> flipVerticalRef = new Ref<Effect>(GetEffect("Effects/SH_FlipVertical"));
					Filters.Scene["FlipVertical"] = new Filter(new ScreenShaderData(flipVerticalRef, "FilterMyShader"), EffectPriority.High);

					Ref<Effect> sineRef = new Ref<Effect>(GetEffect("Effects/SH_Sine"));
					Filters.Scene["Sine"] = new Filter(new ScreenShaderData(sineRef, "CreateSine"), EffectPriority.VeryHigh);
					Filters.Scene["Sine"].Load();

					Ref<Effect> glitchRef = new Ref<Effect>(GetEffect("Effects/SH_Glitch"));
					Filters.Scene["Glitch"] = new Filter(new ScreenShaderData(glitchRef, "CreateGlitch"), EffectPriority.VeryHigh);
					Filters.Scene["Glitch"].Load();
				}
				catch { };
			}

			Main.OnPostDraw += OnPostDraw;
		}

		// Called when the mod is unloaded (null-ify references and static references)
		public override void Unload()
		{
			base.Unload();

			// Stop the server when the mod is unloaded
			_server?.Stop();

			_server = null;
			_mod = null;

			Main.OnPostDraw -= OnPostDraw;
		}

        // Called when the user quits the world
        public override void PreSaveAndQuit()
		{
			// Stop the server when the user exits the world
			_server.Stop();

			base.PreSaveAndQuit();
		}

		// Called when a modded packet is received
		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			// Pass forward packet to CC server
			if (_server != null)
				_server.RouteIncomingPacket(reader);

			base.HandlePacket(reader, whoAmI);
		}

		// Called in the update during time update
        public override void MidUpdateTimeWorld()
        {
			if (_server != null && Main.netMode != Terraria.ID.NetmodeID.MultiplayerClient)
				_server.CheckDungeonGuardians();

			base.MidUpdateTimeWorld();
        }

		// Called after everything is updated
        public override void PostUpdateEverything()
        {
			if (!Main.mapFullscreen && m_mapOpened)
				m_mapOpened = false;

            base.PostUpdateEverything();
        }

		// Called whilst the full screen map is active
        public override void PostDrawFullscreenMap(ref string mouseText)
        {
			// Give wormhole potion if there isn't one in the inventory
			if (!m_mapOpened && _server != null && _server.IsRunning && Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient && CCServer._allowTeleportingToPlayers && Main.player[Main.myPlayer].team > 0 && !Main.player[Main.myPlayer].HasUnityPotion())
            {
				m_mapOpened = true;
				Player player = Main.player[Main.myPlayer];
				bool success = false;
				for (int i = 57; i >= 0; i--)
				{
					if (player.inventory[i] == null || player.inventory[i].netID == 0)
                    {
						int id = Item.NewItem((int)player.position.X, (int)player.position.Y, player.width, player.height, Terraria.ID.ItemID.WormholePotion, 1);
						if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
							NetMessage.SendData(Terraria.ID.MessageID.SyncItem, -1, -1, null, id, 1);
						success = true;
						TDebug.WriteDebug("Provided wormhole potion in empty slot", Color.Yellow);
						break;
                    }
				}
				if (!success)
                {
					int oldSelected = player.selectedItem;
					player.selectedItem = 49;
					player.DropSelectedItem();
					player.selectedItem = oldSelected;
					int id = Item.NewItem((int)player.position.X, (int)player.position.Y, player.width, player.height, Terraria.ID.ItemID.WormholePotion, 1);
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
						NetMessage.SendData(Terraria.ID.MessageID.SyncItem, -1, -1, null, id, 1);
					TDebug.WriteDebug("Provided wormhole potion by force", Color.Yellow);
				}
            }

            base.PostDrawFullscreenMap(ref mouseText);
        }

        // Called after game is rendered
        private void OnPostDraw(GameTime gameTime)
		{
			// Draw wall of fish across the screen
			if (_server != null && _server.IsRunning && _server.m_fishWallTimer.Enabled)
			{
				Main.spriteBatch.Begin();
				int maxFish = Main.screenWidth / (14 * 5 * 2);
				try
				{
					for (int i = 0; i < maxFish; ++i)
						DrawWallOfFish(i * _server.m_fishWallOffset);
				}
				catch (Exception e) { TDebug.WriteDebug("Error drawing wall of fish: " + e.Message, Color.Yellow); }
				finally { Main.spriteBatch.End(); }
			}
		}

		// Draw a wall of fish (offset is applied to Main.GlobalTime) (extracted and edited from the Terraria source code)
		private void DrawWallOfFish(float offset = 0f)
		{
			List<int> list = new List<int>();
			for (int k = 2297; k <= 2321; k++)
			{
				list.Add(k);
			}
			for (int j = 2450; j <= 2488; j++)
			{
				list.Add(j);
			}
			for (int i = 0; i < 5; i++)
			{
				float num3 = 10f + offset;
				Vector2 vector = new Vector2((float)Main.screenWidth / num3 * (Main.GlobalTime % num3), -100f);
				vector.X += 14 * i;
				vector.Y += i % 2 * 14;
				int num2 = 30 * i;
				while (vector.Y < (float)(Main.screenHeight + 100))
				{
					if (++num2 >= list.Count)
					{
						num2 = 0;
					}
					vector.Y += 26f;
					Texture2D texture2D = Main.itemTexture[list[num2]];
					//Point point = (vector + Main.screenPosition).ToTileCoordinates();		/* EDIT: Disable lighting on the sprites */
					Color colour = Color.White; //Lighting.GetColor(point.X, point.Y);
					Main.spriteBatch.Draw(texture2D, vector, null, colour, (float)Math.PI / 4f, texture2D.Size() / 2f, 1f, SpriteEffects.None, 0f);
				}
			}
		}
	}
}