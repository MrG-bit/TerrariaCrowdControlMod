///<summary>
/// File: CrowdControlMod.cs
/// Last Updated: 2020-07-23
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
		public static CrowdControlMod _mod = null;		// Reference to the mod
		public static CCServer _server = null;          // Reference to the server

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
				}
				catch { };
			}

			Main.OnPostDraw += OnPostDraw;
		}

		// Called when the mod is unloaded
		public override void Unload()
		{
			base.Unload();

			// Stop the server when the mod is unloaded
			_server?.Stop();

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