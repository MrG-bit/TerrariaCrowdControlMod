///<summary>
/// File: CCServer.cs
/// Last Updated: 2020-07-19
/// Author: MRG-bit
/// Description: Connects to the socket that the Crowd Control app uses and responds to incoming effects
///</summary>

using System;
using System.Timers;
using System.Net.Sockets;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Newtonsoft.Json;
using Terraria.Graphics.Effects;
using System.IO;
using Terraria.ModLoader;

namespace CrowdControlMod
{
	public class CCServer
	{
        #region Network Types

        public enum EPacketEffect : byte
		{
			CC_CONNECT,         // Broadcasts a message to the server when a client connects to Crowd Control
			SPAWN_NPC,          // int type, int x, int y
			SET_TIME,           // int time, bool dayTime
			SET_SPAWNRATE,      // float spawnRate
			GEN_STRUCT,         // string viewer
			SET_PAINTTILE,      // int x, int y, byte colour (Paints a tile with a colour)
			SET_SPEED,			// bool enabled
			SET_JUMP,			// bool enabled
			START_SUNDIAL,
		}

		private enum RequestType
		{
			TEST, START, STOP
		}

		private enum EffectResult
		{
			SUCCESS, FAILURE, UNAVAILABLE, RETRY
		}

		private struct Request
		{
			public int id;
			public string code;
			public string viewer;
			public int type;

			public static Request FromJSON(string jsonData)
			{
				return JsonConvert.DeserializeObject<Request>(jsonData);
			}

			public string ToJSON()
			{
				return JsonConvert.SerializeObject(this);
			}
		}

		private struct Response
		{
			public int id;
			public int status;
			public string message;

			public static Response FromJSON(string jsonData)
			{
				return JsonConvert.DeserializeObject<Response>(jsonData);
			}

			public string ToJSON()
			{
				return JsonConvert.SerializeObject(this);
			}
		}

        #endregion

        #region Timers

        // Timers used for some effects
        public readonly Timer m_fastPlayerTimer = null;
		public readonly Timer m_jumpPlayerTimer = null;
		public readonly Timer m_rainbowPaintTimer = null;
		public readonly Timer m_shootBombTimer = null;
		public readonly Timer m_projItemTimer = null;
		public readonly Timer m_increasedSpawnsTimer = null;
		public readonly Timer m_flipCameraTimer = null;
		public readonly Timer m_fishWallTimer = null;

		// Times for the various effects (in seconds)
		public readonly int m_timeFastPlayer = 25;
		public readonly int m_timeJumpPlayer = 25;
		public readonly int m_timeRainbowPaint = 45;
		public readonly int m_timeShootBomb = 30;
		public readonly int m_timeProjItem = 45;
		public readonly int m_timeIncSpawnrate = 40;
		public readonly int m_timeBuffDaze = 25;
		public readonly int m_timeBuffLev = 25;
		public readonly int m_timeBuffConf = 25;
		public readonly int m_timeBuffIron = 120;
		public readonly int m_timeBuffRegen = 120;
		public readonly int m_timeBuffLife = 120;
		public readonly int m_timeBuffSpeed = 120;
		public readonly int m_timeFlipScreen = 25;
		public readonly int m_timeFishWall = 25;
		public readonly int m_timeDarkScreen = 25;

        #endregion

        #region Effect Variables

        // Parameters for the various effects
        public readonly string[] m_killVerb =								// Collection of kill verbs used in the killplr command (one is chosen randomly each time)
        {
			"murdered", "executed", "demolished", "destroyed", "spat on",
			"slam dunked", "killed", "slapped", "didn't regret killing"
		};
		public readonly float m_damagePlayerPerc = 0.15f;					// Set player health to this percentage of the max life
		public readonly float m_damagePlayerPercPM = 0.02f;					// +- percentage (range) added to damagePlayerPerc
		public readonly float m_fastPlrMaxSurfSpeed = 15f;					// Max movement speed on surface
		public readonly float m_fastPlrSurfAccel = 2f;						// Movement acceleration on surface
		public readonly float m_fastPlrMaxCaveSpeed = 9f;					// Max movement speed underground
		public readonly float m_fastPlrCaveAccel = 1f;						// Movement acceleration underground
		public readonly int m_jumpPlrHeight = 22;							// Player jump height
		public readonly float m_jumpPlrSpeed = 16f;							// Player jump speed
		public readonly int[] m_preMelee =									// Prefix IDs used for melee weapons
		{
			1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,81
		};
		public readonly int[] m_preRange =									// Prefix IDs used for ranged weapons
		{
			16,17,18,19,20,21,22,23,24,25,58,82
		};
		public readonly int[] m_preMage =									// Prefix IDs used for magic weapons
		{
			26,27,28,29,30,31,32,33,34,35,52,83
		};
		public readonly int[] m_preUni =									// Prefix IDs used for universal weapons (can be applied to any weapon)
		{
			36,37,38,39,40,41,53,54,55,56,57,59,60,61
		};
		private readonly CyclicArray<int> m_prevPets;						// Keep track of the previous 5 pets to prevent getting the same one multiple times in a row
		private readonly int[] m_pets =										// Pet IDs to be randomly chosen from
		{
			40,41,42,45,50,51,52,53,54,55,56,61,65,66,
			81,82,84,85,91,92,127,136,154,191,200,202,
			/* 1.4 pets
			216,217,218,219,258,259,260,261,262,264,266,
			267,268,274,284,285,286,287,288,289,290,291,
			292,293,295,296,297,300,301,302,303,304,317*/
		};
		private readonly byte[] m_rainbowPaint =							// Paint IDs that form a somewhat-rainbow (in order)
		{
			13,14,15,16,17,18,19,20,21,22,23,24
		};
		private int m_rainbowIndex = 0;										// Index of the paint in the rainbowPaint array to use next
		private readonly CyclicArray<Tile> m_rainbowTiles;					// Keep track of the tiles painted so the same tile is painted repeatedly
		public readonly int m_spawnGuardYOffset = -150;						// Y offset from the player's Y that the dungeon guardian is spawned
        public readonly int m_dayRate = 50;									// Amount that time increases per Terraria update (added to Main.time - default is 1)
        public readonly float m_increaseSpawnRate = 12f;					// Factor that the spawnrate is increased
		public readonly float m_fishWallOffset = 0.85f;                     // Offset between fish walls (janky)

        #endregion

        #region Server Variables

        // Effect message colours
        private readonly Color MSG_C_NEGATIVE;								// Colour used for negative effect chat messages
		private readonly Color MSG_C_NEUTRAL;							    // Colour used for neutral effect chat messages
		private readonly Color MSG_C_POSITIVE;                              // Colour used for positive effect chat messages
		private readonly Color MSG_C_TIMEREND;								// Colour used for when a timer ends
		private readonly int MSG_ITEM_TIMEREND = 3099;						// Item used to prefix messages about timers ending

		// Server variables
		public bool IsRunning { get; private set; } = false;				// Whether the server is running (should only be running when in a Terraria world)
        private System.Threading.Thread m_serverThread = null;				// Reference to the thread running the server loop
        private Socket m_activeSocket = null;								// Reference to the socket being used to communicate with Crowd Control
        private CCPlayer m_player = null;                                   // Reference to the ModPlayer instance affected by Crowd Control effects

        #endregion

        // Default constructor
        public CCServer()
        {            
            // Ignore silent exceptions thrown by Socket.Connect (cleaner chat during testing)
            if (TDebug.IN_DEBUG)
            {
                Logging.IgnoreExceptionContents("System.Net.Sockets.Socket.Connect");
                Logging.IgnoreExceptionContents("System.Net.Sockets.Socket.DoConnect");
            }

            // Set readonly colours
            MSG_C_NEGATIVE = Color.Red;
            MSG_C_NEUTRAL = Color.Orange;
            MSG_C_POSITIVE = Color.Green;
			MSG_C_TIMEREND = Color.Green;

			m_prevPets = new CyclicArray<int>(5);
			m_rainbowTiles = new CyclicArray<Tile>(2);

            // Set up timers
            {
				m_fastPlayerTimer = new Timer
				{
					Interval = 1000 * m_timeFastPlayer,
					AutoReset = false
				};
				m_fastPlayerTimer.Elapsed += delegate { StopEffect("fastplr"); };

				m_jumpPlayerTimer = new Timer
				{
					Interval = 1000 * m_timeJumpPlayer,
					AutoReset = false
				};
				m_jumpPlayerTimer.Elapsed += delegate { StopEffect("jumpplr"); };

				m_rainbowPaintTimer = new Timer
				{
					Interval = 1000 * m_timeRainbowPaint,
					AutoReset = false
				};
				m_rainbowPaintTimer.Elapsed += delegate { StopEffect("tile_paint"); };

				m_shootBombTimer = new Timer
                {
                    Interval = 1000 * m_timeShootBomb,
                    AutoReset = false
                };
                m_shootBombTimer.Elapsed += delegate { StopEffect("shoot_bomb"); };

				m_projItemTimer = new Timer
				{
					Interval = 1000 * m_timeProjItem,
					AutoReset = false
				};
				m_projItemTimer.Elapsed += delegate { StopEffect("proj_item"); };

                m_increasedSpawnsTimer = new Timer
                {
                    Interval = 1000 * m_timeIncSpawnrate,
                    AutoReset = false
                };
                m_increasedSpawnsTimer.Elapsed += delegate { StopEffect("inc_spawnrate"); };

                m_flipCameraTimer = new Timer
                {
                    Interval = 1000 * m_timeFlipScreen,
                    AutoReset = false
                };
                m_flipCameraTimer.Elapsed += delegate { StopEffect("cam_flip"); };

				m_fishWallTimer = new Timer
				{
					Interval = 1000 * m_timeFishWall,
					AutoReset = false
				};
				m_fishWallTimer.Elapsed += delegate { StopEffect("cam_fish"); };
            }
        }

        // Start the server
        public void Start()
        {
			if (Main.dedServ)
			{
				TDebug.WriteDebug("Cannot start server due to being a dedicated server (netMode: " + Main.netMode + ")", Color.Yellow);
			}
			else if (!IsRunning)
            {
                // Create a new thread for the server loop
                IsRunning = true;
                TDebug.WriteDebug("Server started (netMode: " + Main.netMode + ")", Color.Yellow);
                m_serverThread = new System.Threading.Thread(new System.Threading.ThreadStart(ServerLoop));
                m_serverThread.Start();
            }
            else
            {
                TDebug.WriteDebug("Server is already running", Color.Yellow);
            }
        }

        // Stop the server
        public void Stop()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            m_serverThread.Abort();
            if (m_activeSocket != null && m_activeSocket.Connected)
            {
                try { m_activeSocket.Shutdown(SocketShutdown.Both); }
                finally { m_activeSocket.Close(); }
            }
            TDebug.WriteDebug("Server stopped", Color.Yellow);

			if (m_fastPlayerTimer.Enabled)
				StopEffect("fastplr");
			if (m_jumpPlayerTimer.Enabled)
				StopEffect("jumpplr");
			if (m_rainbowPaintTimer.Enabled)
				StopEffect("tile_paint");
            if (m_shootBombTimer.Enabled)
                StopEffect("shoot_bomb");
			if (m_projItemTimer.Enabled)
				StopEffect("proj_item");
            if (m_increasedSpawnsTimer.Enabled)
                StopEffect("inc_spawnrate");
            if (m_flipCameraTimer.Enabled)
                StopEffect("cam_flip");
			if (m_fishWallTimer.Enabled)
				StopEffect("cam_fish");

			m_prevPets.Clear();
        }

        // Server loop (attempts to connect to Crowd Control)
        private void ServerLoop()
        {
            // Initialise Main.rand in this thread because it is marked as NON THREAD STATIC
            Main.rand = new Terraria.Utilities.UnifiedRandom();
            bool writeAttempt = true;
            bool connected = false;

            while (true)
            {
                m_activeSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try { m_activeSocket.Connect("127.0.0.1", 58430); connected = true; }
                catch (System.Threading.ThreadAbortException e) { throw e; }
                catch (Exception) { }
                
                if (connected)
                {
					TDebug.WriteMessage(1525, "Connected to Crowd Control", Color.Green);
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
						SendData(EPacketEffect.CC_CONNECT);
                    while (m_activeSocket.Connected && m_activeSocket.Poll(1000, SelectMode.SelectWrite) && ClientLoop(m_activeSocket)) ;
                    try { m_activeSocket.Shutdown(SocketShutdown.Both); }
                    finally { m_activeSocket.Close(); }
                    TDebug.WriteMessage(1526, "Disconnected from Crowd Control", Color.Green);
                    writeAttempt = true;
                    connected = false;
                }
                else
                {
                    System.Threading.Thread.Sleep(1000 * 2);
                    if (writeAttempt)
                    {
                        writeAttempt = false;
                        TDebug.WriteMessage(3643, "Attempting to connect to Crowd Control", Color.Green);
                    }
                }
            }
        }

        // Client loop (processes incoming packets) (returns true if there is a valid connection)
        private bool ClientLoop(Socket client)
        {
            try
            {
                // Read incoming data
                byte[] buffer = new byte[1024];
                int size = client.Receive(buffer);
                string data = System.Text.Encoding.ASCII.GetString(buffer, 0, size);

                // Check if valid data
                if (data.StartsWith("{"))
                {
                    TDebug.WriteDebug("Received: " + data, Color.Yellow);
                    // Parse and process data
                    string result = ParseJSON(data);
                    TDebug.WriteDebug("Sending: " + result, Color.Yellow);

                    // Send response back to Crowd Control
                    byte[] tmp = System.Text.Encoding.ASCII.GetBytes(result);
                    byte[] outBuffer = new byte[tmp.Length + 1];
                    Array.Copy(tmp, 0, outBuffer, 0, tmp.Length);
                    outBuffer[outBuffer.Length - 1] = 0x00;
                    client.Send(outBuffer);
                }

                return true;
            }
            catch (System.Threading.ThreadAbortException e) { throw e; }
            catch (Exception) { return false; }
        }

        // Parse the Request data from JSON to a Request object, and process the effect (returns the Response as JSON)
        private string ParseJSON(string data)
        {
            Response res = new Response();
            try
            {
                Request req = Request.FromJSON(data);
                res.id = req.id;
                EffectResult cmdResult = ProcessEffect(req.code, req.viewer, req.type);
                res.status = (int)cmdResult;
                res.message = "Effect " + req.code + ": " + res.status;
                return res.ToJSON();
            }
            catch (Exception e)
            {
                res.message = e.Message;
                return res.ToJSON();
            }
        }

        // Process an effect request
        private EffectResult ProcessEffect(string code, string viewer, int requestType)
        {
            // Only process the request if the game is not paused
            if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer && Main.gamePaused && requestType != (int)RequestType.STOP)
            {
                TDebug.WriteDebug("Game is paused. Will retry [" + code + "]", Color.Yellow);
                return EffectResult.RETRY;
            }

            // Start or stop the effect
            if (requestType != (int)RequestType.STOP)
                return StartEffect(code, viewer);
            else
                return StopEffect(code);
        }

        // Start an effect
        private EffectResult StartEffect(string code, string viewer)
        {
            switch (code)
            {
                case "killplr":
                    m_player.player.KillMe(
                        Terraria.DataStructures.PlayerDeathReason.ByCustomReason(m_player.player.name + " was " + (m_killVerb[Main.rand.Next(m_killVerb.Length)]) + " by " + viewer + "."),
                        1000, 0, false);
                    break;

                case "healplr":
					m_player.player.statLife = m_player.player.statLifeMax2;
                    TDebug.WriteMessage(29, viewer + " healed " + m_player.player.name, MSG_C_POSITIVE);
                    break;

                case "damplr":
					int life = (int)(m_player.player.statLife * (m_damagePlayerPerc + Main.rand.NextFloat(-m_damagePlayerPercPM, m_damagePlayerPercPM)));
					m_player.player.statLife = life;
					TDebug.WriteMessage(3106, viewer + " severely damaged " + m_player.player.name, MSG_C_NEGATIVE);
					break;

				case "fastplr":
					ResetTimer(m_fastPlayerTimer);
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
						SendData(EPacketEffect.SET_SPEED, true);
					TDebug.WriteMessage(898, viewer + " made " + m_player.player.name + " really, really fast for " + m_timeFastPlayer + " seconds", MSG_C_NEUTRAL);
					break;

				case "jumpplr":
					ResetTimer(m_jumpPlayerTimer);
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
						SendData(EPacketEffect.SET_JUMP, true);
					TDebug.WriteMessage(1164, viewer + " made it so " + m_player.player.name + " can jump very high for " + m_timeJumpPlayer + " seconds", MSG_C_NEUTRAL);
					break;

				case "randtp":
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) 
						NetMessage.SendData(Terraria.ID.MessageID.TeleportationPotion);
					else
					{
						m_player.player.TeleportationPotion();
						Main.PlaySound(Terraria.ID.SoundID.Item6, m_player.player.position);
					}
					TDebug.WriteMessage(2351, viewer + " randomly teleported " + m_player.player.name, MSG_C_NEUTRAL);
                    break;

                case "item_drop":
                    if (Effect_DropItem(viewer) == EffectResult.RETRY)
                        return EffectResult.RETRY;
                    break;

				case "item_prefix":
					if (Effect_RandomItemPrefix(viewer) == EffectResult.RETRY)
						return EffectResult.RETRY;
					break;

				case "item_money":
					int coins = Item.buyPrice(0, 0, Main.rand.Next(1, 100), Main.rand.Next(1, 100));
					m_player.GiveCoins(coins);
					TDebug.WriteMessage(855,viewer + " donated " + Main.ValueToCoins(coins) + " to " + m_player.player.name, MSG_C_POSITIVE);
					break;

				case "item_pet":
					Effect_GivePet(viewer);
					break;

				case "tile_paint":
					if (!m_rainbowPaintTimer.Enabled)
					{
						m_rainbowIndex = Main.rand.Next(m_rainbowPaint.Length);
						m_rainbowTiles.Clear();
					}
					ResetTimer(m_rainbowPaintTimer);
					TDebug.WriteMessage(662, viewer + " caused a rainbow to form underneath " + m_player.player.name + " for " + m_timeRainbowPaint + " seconds", MSG_C_NEUTRAL);
					break;

				case "shoot_bomb":
					ResetTimer(m_shootBombTimer);
                    TDebug.WriteMessage(166, "Shooting explosives for " + m_timeShootBomb + " seconds thanks to " + viewer, MSG_C_NEGATIVE);
					break;

				case "proj_item":
					ResetTimer(m_projItemTimer);
					Projectiles.ModGlobalProjectile.m_textureOffset = Main.rand.Next(Main.itemTexture.Length);
					TDebug.WriteMessage(3311, viewer + " randomised projectile sprites for " + m_timeProjItem + " seconds", MSG_C_NEUTRAL);
					break;

				case "sp_house":
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer)
					{
						if (Effect_SpawnHouse(m_player.player, viewer) == EffectResult.RETRY)
							return EffectResult.RETRY;
					}
					else
						SendData(EPacketEffect.GEN_STRUCT, viewer);
                    break;

                case "sp_guard":
					int px = (int)m_player.player.position.X;
					int py = (int)m_player.player.position.Y;
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) NPC.NewNPC(px, py + m_spawnGuardYOffset, Terraria.ID.NPCID.DungeonGuardian);
					else SendData(EPacketEffect.SPAWN_NPC, Terraria.ID.NPCID.DungeonGuardian, px, py);
                    TDebug.WriteMessage(1274, viewer + " spawned a Dungeon Guardian", MSG_C_NEGATIVE);
                    break;

                case "sp_bunny":
                    Effect_SpawnBunny(viewer);
                    break;

                case "inc_spawnrate":
					ResetTimer(m_increasedSpawnsTimer);
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) m_player.m_spawnRate = m_increaseSpawnRate;
					else SendData(EPacketEffect.SET_SPAWNRATE, m_increaseSpawnRate);
                    TDebug.WriteMessage(148, viewer + " increased the spawnrate for " + m_timeIncSpawnrate + " seconds", MSG_C_NEUTRAL);
                    break;

                case "buff_daze":
                    m_player.player.AddBuff(Terraria.ID.BuffID.Dazed, 60 * m_timeBuffDaze, true);
                    TDebug.WriteMessage(75, viewer + " dazed " + m_player.player.name, MSG_C_NEGATIVE);
                    break;

                case "buff_lev":
                    m_player.player.AddBuff(Terraria.ID.BuffID.VortexDebuff, 60 * m_timeBuffLev, true);
                    TDebug.WriteMessage(3456, viewer + " distorted gravity around " + m_player.player.name, MSG_C_NEGATIVE);
                    break;

                case "buff_confuse":
                    m_player.player.AddBuff(Terraria.ID.BuffID.Confused, 60 * m_timeBuffConf, true);
                    TDebug.WriteMessage(3223, viewer + " confused " + m_player.player.name, MSG_C_NEGATIVE);
                    break;

                case "buff_iron":
                    m_player.player.AddBuff(Terraria.ID.BuffID.Ironskin, 60 * m_timeBuffIron, true);
                    m_player.player.AddBuff(Terraria.ID.BuffID.Endurance, 60 * m_timeBuffIron, true);
                    TDebug.WriteMessage(292, viewer + " provided " + m_player.player.name + " with survivability buffs", MSG_C_POSITIVE);
                    break;

                case "buff_regen":
                    m_player.player.AddBuff(Terraria.ID.BuffID.Regeneration, 60 * m_timeBuffRegen, true);
                    m_player.player.AddBuff(Terraria.ID.BuffID.ManaRegeneration, 60 * m_timeBuffRegen, true);
                    TDebug.WriteMessage(289, viewer + " provided " + m_player.player.name + " with regeneration buffs", MSG_C_POSITIVE);
                    break;

                case "buff_life":
                    m_player.player.AddBuff(Terraria.ID.BuffID.Lifeforce, 60 * m_timeBuffLife, true);
                    TDebug.WriteMessage(1291, viewer + " boosted the maximum health of " + m_player.player.name, MSG_C_POSITIVE);
                    break;

				case "buff_move":
					m_player.player.AddBuff(Terraria.ID.BuffID.Swiftness, 60 * m_timeBuffSpeed, true);
					m_player.player.AddBuff(Terraria.ID.BuffID.SugarRush, 60 * m_timeBuffSpeed, true);
					m_player.player.AddBuff(Terraria.ID.BuffID.Panic, 60 * m_timeBuffSpeed, true);
					TDebug.WriteMessage(54, viewer + " boosted the movement speed of " + m_player.player.name, MSG_C_POSITIVE);
					break;

				case "inc_time":
					if (Main.fastForwardTime)
					{
						TDebug.WriteDebug("Time is already advancing - will retry", Color.Yellow);
						return EffectResult.RETRY;
					}

					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) Main.fastForwardTime = true;
					else SendData(EPacketEffect.START_SUNDIAL);
					TDebug.WriteMessage(3064, viewer + " advanced time to sunrise", MSG_C_NEUTRAL);
                    break;

                case "time_noon":
					SetTime(27000, true);
					TDebug.WriteMessage(3733, viewer + " set the time to noon", MSG_C_NEUTRAL);
                    break;

                case "time_midnight":
					SetTime(16200, false);
					TDebug.WriteMessage(485, viewer + " set the time to midnight", MSG_C_NEUTRAL);
                    break;

                case "time_sunrise":
					SetTime(0, true);
					TDebug.WriteMessage(3733, viewer + " set the time to sunrise", MSG_C_NEUTRAL);
                    break;

                case "time_sunset":
					SetTime(0, false);
					TDebug.WriteMessage(485, viewer + " set the time to sunset", MSG_C_NEUTRAL);
                    break;

                case "cam_flip":
                    if (!Filters.Scene["FlipVertical"].IsActive())
                        Filters.Scene.Activate("FlipVertical").GetShader();
					ResetTimer(m_flipCameraTimer);
                    TDebug.WriteMessage(viewer + " turned the world upside down for " + m_timeFlipScreen + " seconds", MSG_C_NEGATIVE);
                    break;

				case "cam_fish":
					ResetTimer(m_fishWallTimer);
					TDebug.WriteMessage(669, viewer + " covered the screen with fish for " + m_timeFishWall + " seconds", MSG_C_NEUTRAL);
					break;

				case "cam_darken":
					m_player.player.AddBuff(Terraria.ID.BuffID.Obstructed, 60 * m_timeDarkScreen, true);
					TDebug.WriteMessage(1311, viewer + " darkened the screen for " + m_timeDarkScreen +" seconds", MSG_C_NEGATIVE);
					break;

			}

            TDebug.WriteDebug(viewer + " ran [" + code + "]", Color.Yellow);
            return EffectResult.SUCCESS;
        }

        // Stop an effect
        private EffectResult StopEffect(string code)
        {
            switch (code)
            {
				case "fastplr":
					m_fastPlayerTimer.Stop();
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
						SendData(EPacketEffect.SET_SPEED, false);
					TDebug.WriteMessage(MSG_ITEM_TIMEREND, "Movement speed is back to normal", MSG_C_TIMEREND);
					break;

				case "jumpplr":
					m_jumpPlayerTimer.Stop();
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
						SendData(EPacketEffect.SET_JUMP, false);
					TDebug.WriteMessage(MSG_ITEM_TIMEREND, "Jump height is back to normal", MSG_C_TIMEREND);
					break;

				case "tile_paint":
					m_rainbowPaintTimer.Stop();
					TDebug.WriteMessage(MSG_ITEM_TIMEREND, "Rainbows are no longer forming", MSG_C_TIMEREND);
					break;

				case "shoot_bomb":
                    m_shootBombTimer.Stop();
                    TDebug.WriteMessage(MSG_ITEM_TIMEREND, "No longer shooting explosives", MSG_C_TIMEREND);
                    break;

				case "proj_item":
					m_projItemTimer.Stop();
					TDebug.WriteMessage(MSG_ITEM_TIMEREND, "Projectiles are no longer randomised", MSG_C_TIMEREND);
					break;

				case "inc_spawnrate":
                    m_increasedSpawnsTimer.Stop();
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) m_player.m_spawnRate = 1f;
					else SendData(EPacketEffect.SET_SPAWNRATE, 1f);
                    TDebug.WriteMessage(MSG_ITEM_TIMEREND, "Spawnrate is back to normal", MSG_C_TIMEREND);
					break;

                case "cam_flip":
                    if (Filters.Scene["FlipVertical"].IsActive())
                        Filters.Scene.Deactivate("FlipVertical");
                    m_flipCameraTimer.Stop();
                    TDebug.WriteMessage(MSG_ITEM_TIMEREND, "World is no longer flipped", MSG_C_TIMEREND);
                    break;

				case "cam_fish":
					m_fishWallTimer.Stop();
					TDebug.WriteMessage(MSG_ITEM_TIMEREND, "Fish is no longer covering the screen", MSG_C_TIMEREND);
					break;
			}

            return EffectResult.SUCCESS;
        }

		/// Reset timer
		private void ResetTimer(Timer timer)
        {
			timer.Stop();
			timer.Start();
        }

        // Drop the selected item or drop a random item from the hotbar
        private EffectResult Effect_DropItem(string viewer)
        {
			Item droppedItem;
			if (m_player.player.inventory[m_player.player.selectedItem].type == Terraria.ID.ItemID.None)
            {
                List<int> slots = new List<int>();
                for (int i = 0; i < 10; ++i)
                    if (m_player.player.inventory[i].type != Terraria.ID.ItemID.None)
                        slots.Add(i);

                if (slots.Count > 0)
                {
                    int slot = Main.rand.Next(slots);
                    int oldSel = m_player.player.selectedItem;
                    m_player.player.inventory[slot].favorited = false;
                    droppedItem = m_player.player.inventory[slot];
                    m_player.player.selectedItem = slot;
                    m_player.player.DropSelectedItem();
                    m_player.player.selectedItem = oldSel;
                }
                else
                {
                    TDebug.WriteDebug("No item present in hotbar - will retry", Color.Yellow);
                    return EffectResult.RETRY;
                }
            }
            else
            {
                m_player.player.inventory[m_player.player.selectedItem].favorited = false;
                droppedItem = m_player.player.inventory[m_player.player.selectedItem];
                m_player.player.DropSelectedItem();
            }

            if (droppedItem.stack > 1)
                TDebug.WriteMessage(droppedItem.type, viewer + " caused " + m_player.player.name + " to fumble and drop " + droppedItem.stack + " " + droppedItem.Name + "s", MSG_C_NEGATIVE);
            else
                TDebug.WriteMessage(droppedItem.type, viewer + " caused" + m_player.player.name + " to fumble and drop their " + droppedItem.Name, MSG_C_NEGATIVE);

            return EffectResult.SUCCESS;
        }

		// Randomise an item's prefix
		private EffectResult Effect_RandomItemPrefix(string viewer)
		{
			if (m_player.player.inventory[m_player.player.selectedItem].type == Terraria.ID.ItemID.None)
			{
				TDebug.WriteDebug("No item selected - will retry", Color.Yellow);
				return EffectResult.RETRY;
			}

			Item affectedItem = m_player.player.inventory[m_player.player.selectedItem];
			int prefix;
			int index;

			if (affectedItem.melee)
			{
				do
				{
					index = Main.rand.Next(m_preMelee.Length + m_preUni.Length);
					prefix = (index < m_preMelee.Length) ? m_preMelee[index] : m_preUni[index - m_preMelee.Length];
				} while (prefix == affectedItem.prefix);
			}
			else if (affectedItem.ranged)
			{
				do
				{
					index = Main.rand.Next(m_preRange.Length + m_preUni.Length);
					prefix = (index < m_preRange.Length) ? m_preRange[index] : m_preUni[index - m_preRange.Length];
				} while (prefix == affectedItem.prefix);
			}
			else if (affectedItem.magic)
			{
				do
				{
					index = Main.rand.Next(m_preMage.Length + m_preUni.Length);
					prefix = (index < m_preMage.Length) ? m_preMage[index] : m_preUni[index - m_preMage.Length];
				} while (prefix == affectedItem.prefix);
			}
			else
			{
				TDebug.WriteDebug("Item isn't melee, ranged or magic - will retry", Color.Yellow);
				return EffectResult.RETRY;
			}

			Item newItem = new Item();
			newItem.SetDefaults(affectedItem.type);
			if (newItem.Prefix(prefix))
			{
				newItem.favorited = affectedItem.favorited;
				newItem.stack = affectedItem.stack;
				m_player.player.inventory[m_player.player.selectedItem] = newItem;
				string prefixName = Lang.prefix[prefix].Value; //Terraria.ID.PrefixID.GetUniqueKey((byte)prefix).Split(' ')[1];
				if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
					NetMessage.SendData(Terraria.ID.MessageID.SyncEquipment, -1, -1, null, Main.myPlayer, m_player.player.selectedItem, newItem.stack, newItem.prefix, newItem.netID);
				TDebug.WriteMessage(affectedItem.type, viewer + " changed " + m_player.player.name + "'s " + affectedItem.Name + " to be " + prefixName, MSG_C_NEUTRAL);
			}
			else
			{
				TDebug.WriteDebug("Item prefix failed - will retry", Color.Yellow);
				return EffectResult.RETRY;
			}
			
			return EffectResult.SUCCESS;
		}

		// Give the player a random pet
		private void Effect_GivePet(string viewer)
		{
			if (!m_player.player.hideMisc[0]) m_player.player.ClearBuff(m_player.player.miscEquips[0].buffType);
			m_player.player.hideMisc[0] = true;
			int id;
			do { id = m_pets[Main.rand.Next(m_pets.Length)]; } while (m_prevPets.Contains(id));
			m_prevPets.Add(id);
			m_player.player.AddBuff(id, 1);
			TDebug.WriteMessage(1927, viewer + " provided " + m_player.player.name + " with a " + Lang.GetBuffName(id), MSG_C_POSITIVE);
		}

        // Generate a structure around the player depending on their biome
        private EffectResult Effect_SpawnHouse(Player player, string viewer)
        {
            try
            {
                int x = (int)(player.position.X / 16);
                int y = (int)(player.position.Y / 16);
				bool tree = false;

                if (player.ZoneCorrupt || player.ZoneCrimson)
                {
                    TDebug.WriteMessage(viewer + " generated a deep chasm below " + player.name, MSG_C_NEUTRAL);
                    WorldGen.ChasmRunner(x, y, Main.rand.Next(12, 24), false);
                }
                else if (y > Main.worldSurface && player.ZoneJungle)
                {
                    TDebug.WriteMessage(viewer + " generated a bee hive surrounding " + player.name, MSG_C_NEUTRAL);
                    WorldGen.Hive(x, y);
                }
                else if (player.ZoneUnderworldHeight)
                {
                    TDebug.WriteMessage(viewer + " generated a hell fortress around " + player.name, MSG_C_NEUTRAL);
                    WorldGen.HellFort(x, y);
                }
                else if (y < Main.worldSurface - 220)
                {
					TDebug.WriteMessage(viewer + " generated a sky island house around " + player.name, MSG_C_NEUTRAL);
					WorldGen.IslandHouse(x, y);
				}
                else if (y > Main.worldSurface)
                {
                    TDebug.WriteMessage(viewer + " generated an abandoned house around " + player.name, MSG_C_NEUTRAL);
                    WorldGen.MineHouse(x, y);
                }
                else
                {
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer)
					{
						TDebug.WriteMessage(viewer + " generated a huge living tree around " + player.name, MSG_C_NEUTRAL);
						try
						{
							GrowLivingTree(x, y);
							tree = true;
							TDebug.WriteDebug("Tree generated successfully", Color.Yellow);
						}
						catch (Exception e) { TDebug.WriteDebug("Tree failed to generate: " + e.Message, Color.Yellow); }
					}
					else
					{
						TDebug.WriteMessage(viewer + " generated an abandoned house around " + player.name, MSG_C_NEUTRAL);
						WorldGen.MineHouse(x, y);
					}
                }

				if (!tree)
				{
					if (Main.dedServ)
					{
						for (int i = 0; i < Main.player.Length; ++i)
						{
							if (Main.player[i] != null)
							{
								int o = 55;
								NetMessage.SendTileRange(i, x - o, y - o, o, o, Terraria.ID.TileChangeType.None);
								NetMessage.SendTileRange(i, x - o, y, o, o, Terraria.ID.TileChangeType.None);
								NetMessage.SendTileRange(i, x, y - o, o, o, Terraria.ID.TileChangeType.None);
								NetMessage.SendTileRange(i, x, y, o, o, Terraria.ID.TileChangeType.None);
							}
						}
					}
					else
						WorldGen.RangeFrame(x - 100, y - 150, x + 100, y + 150);
				}
            }
            catch { }
            return EffectResult.SUCCESS;
        }

        // Spawn bunny effect (can spawn evil, gold, or other bunny variants)
        private void Effect_SpawnBunny(string viewer)
        {
			short type;
			int px = (int)m_player.player.position.X;
			int py = (int)m_player.player.position.Y;

            if (Main.rand.Next(0, 100) < 5)
            {
				type = WorldGen.crimson ? Terraria.ID.NPCID.CrimsonBunny : Terraria.ID.NPCID.CorruptBunny;
				if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) NPC.NewNPC(px, py, type);
				else SendData(EPacketEffect.SPAWN_NPC, type, px, py);
				TDebug.WriteMessage(1338, viewer + " spawned an Evil Bunny", MSG_C_NEGATIVE);
            }
            else if (Main.rand.Next(0, 100) < 5)
            {
				type = Terraria.ID.NPCID.GoldBunny;
				if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) NPC.NewNPC(px, py, type);
				else SendData(EPacketEffect.SPAWN_NPC, type, px, py);
                TDebug.WriteMessage(2890, viewer + " spawned a Gold Bunny", MSG_C_POSITIVE);
            }
            else
            {
                type = Main.rand.Next(new short[] { Terraria.ID.NPCID.Bunny, Terraria.ID.NPCID.BunnySlimed, Terraria.ID.NPCID.BunnyXmas, Terraria.ID.NPCID.PartyBunny });
				if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) NPC.NewNPC(px, py, type);
				else SendData(EPacketEffect.SPAWN_NPC, type, px, py);
                TDebug.WriteMessage(2019, viewer + " spawned a Bunny", MSG_C_NEUTRAL);
            }
        }

		// Generate a living tree post WorldGen
		private bool GrowLivingTree(int i, int j, bool genUnderground = false)
		{
			Terraria.Utilities.UnifiedRandom genRand = Main.rand;
			int num111 = 0;
			int[] array = new int[1000];
			int[] array3 = new int[1000];
			int[] array7 = new int[1000];
			int[] array6 = new int[1000];
			int num113 = 0;
			int[] array5 = new int[2000];
			int[] array4 = new int[2000];
			bool[] array2 = new bool[2000];
			int num112 = i - genRand.Next(1, 4);
			int num110 = i + genRand.Next(1, 4);
			if (j < 150)
			{
				return false;
			}
			int num108 = num112;
			int num107 = num110;
			int num106 = num112;
			int num105 = num110;
			int num104 = num110 - num112;
			bool flag10 = true;
			int num103 = genRand.Next(-10, -5);
			int num102 = genRand.Next(2);
			int num101 = j;
			while (flag10)
			{
				num103++;
				if (num103 > genRand.Next(5, 30))
				{
					num103 = 0;
					array3[num111] = num101 + genRand.Next(5);
					if (genRand.Next(5) == 0)
					{
						num102 = ((num102 == 0) ? 1 : 0);
					}
					if (num102 == 0)
					{
						array7[num111] = -1;
						array[num111] = num112;
						array6[num111] = num110 - num112;
						if (genRand.Next(2) == 0)
						{
							num112++;
						}
						num108++;
						num102 = 1;
					}
					else
					{
						array7[num111] = 1;
						array[num111] = num110;
						array6[num111] = num110 - num112;
						if (genRand.Next(2) == 0)
						{
							num110--;
						}
						num107--;
						num102 = 0;
					}
					if (num108 == num107)
					{
						flag10 = false;
					}
					num111++;
				}
				for (int l = num112; l <= num110; l++)
				{
					Main.tile[l, num101].type = 191;
					Main.tile[l, num101].active(active: true);
					Main.tile[l, num101].halfBrick(halfBrick: false);
				}
				num101--;
			}
			for (int m = 0; m < num111; m++)
			{
				int num21 = array[m] + array7[m];
				int num20 = array3[m];
				int num19 = (int)((float)array6[m] * (1f + (float)genRand.Next(20, 30) * 0.1f));
				Main.tile[num21, num20 + 1].type = 191;
				Main.tile[num21, num20 + 1].active(active: true);
				Main.tile[num21, num20 + 1].halfBrick(halfBrick: false);
				int num18 = genRand.Next(3, 5);
				while (num19 > 0)
				{
					num19--;
					Main.tile[num21, num20].type = 191;
					Main.tile[num21, num20].active(active: true);
					Main.tile[num21, num20].halfBrick(halfBrick: false);
					if (genRand.Next(10) == 0)
					{
						num20 = ((genRand.Next(2) != 0) ? (num20 + 1) : (num20 - 1));
					}
					else
					{
						num21 += array7[m];
					}
					if (num18 > 0)
					{
						num18--;
					}
					else if (genRand.Next(2) == 0)
					{
						num18 = genRand.Next(2, 5);
						if (genRand.Next(2) == 0)
						{
							Main.tile[num21, num20].type = 191;
							Main.tile[num21, num20].active(active: true);
							Main.tile[num21, num20].halfBrick(halfBrick: false);
							Main.tile[num21, num20 - 1].type = 191;
							Main.tile[num21, num20 - 1].active(active: true);
							Main.tile[num21, num20 - 1].halfBrick(halfBrick: false);
							array5[num113] = num21;
							array4[num113] = num20;
							num113++;
						}
						else
						{
							Main.tile[num21, num20].type = 191;
							Main.tile[num21, num20].active(active: true);
							Main.tile[num21, num20].halfBrick(halfBrick: false);
							Main.tile[num21, num20 + 1].type = 191;
							Main.tile[num21, num20 + 1].active(active: true);
							Main.tile[num21, num20 + 1].halfBrick(halfBrick: false);
							array5[num113] = num21;
							array4[num113] = num20;
							num113++;
						}
					}
					if (num19 == 0)
					{
						array5[num113] = num21;
						array4[num113] = num20;
						num113++;
					}
				}
			}
			int num100 = (num112 + num110) / 2;
			int num99 = num101;
			int num98 = genRand.Next(num104 * 3, num104 * 5);
			int num97 = 0;
			int num96 = 0;
			while (num98 > 0)
			{
				Main.tile[num100, num99].type = 191;
				Main.tile[num100, num99].active(active: true);
				Main.tile[num100, num99].halfBrick(halfBrick: false);
				if (num97 > 0)
				{
					num97--;
				}
				if (num96 > 0)
				{
					num96--;
				}
				for (int num29 = -1; num29 < 2; num29++)
				{
					if (num29 == 0 || ((num29 >= 0 || num97 != 0) && (num29 <= 0 || num96 != 0)) || genRand.Next(2) != 0)
					{
						continue;
					}
					int num28 = num100;
					int num27 = num99;
					int num26 = genRand.Next(num104, num104 * 3);
					if (num29 < 0)
					{
						num97 = genRand.Next(3, 5);
					}
					if (num29 > 0)
					{
						num96 = genRand.Next(3, 5);
					}
					int num25 = 0;
					while (num26 > 0)
					{
						num26--;
						num28 += num29;
						Main.tile[num28, num27].type = 191;
						Main.tile[num28, num27].active(active: true);
						Main.tile[num28, num27].halfBrick(halfBrick: false);
						if (num26 == 0)
						{
							array5[num113] = num28;
							array4[num113] = num27;
							array2[num113] = true;
							num113++;
						}
						if (genRand.Next(5) == 0)
						{
							num27 = ((genRand.Next(2) != 0) ? (num27 + 1) : (num27 - 1));
							Main.tile[num28, num27].type = 191;
							Main.tile[num28, num27].active(active: true);
							Main.tile[num28, num27].halfBrick(halfBrick: false);
						}
						if (num25 > 0)
						{
							num25--;
						}
						else if (genRand.Next(3) == 0)
						{
							num25 = genRand.Next(2, 4);
							int num24 = num28;
							int num23 = num27;
							num23 = ((genRand.Next(2) != 0) ? (num23 + 1) : (num23 - 1));
							Main.tile[num24, num23].type = 191;
							Main.tile[num24, num23].active(active: true);
							Main.tile[num24, num23].halfBrick(halfBrick: false);
							array5[num113] = num24;
							array4[num113] = num23;
							array2[num113] = true;
							num113++;
						}
					}
				}
				array5[num113] = num100;
				array4[num113] = num99;
				num113++;
				if (genRand.Next(4) == 0)
				{
					num100 = ((genRand.Next(2) != 0) ? (num100 + 1) : (num100 - 1));
					Main.tile[num100, num99].type = 191;
					Main.tile[num100, num99].active(active: true);
					Main.tile[num100, num99].halfBrick(halfBrick: false);
				}
				num99--;
				num98--;
			}

			int top = 0;
			int bottom = 0;

			// TRUNK
			for (int num95 = num106; num95 <= num105; num95++)
			{
				int num41 = genRand.Next(1, 6);
				int num40 = j + 1;
				while (num41 > 0)
				{
					if (WorldGen.SolidTile(num95, num40))
					{
						num41--;
					}
					Main.tile[num95, num40].type = 191;
					Main.tile[num95, num40].active(active: true);
					Main.tile[num95, num40].halfBrick(halfBrick: false);
					num40++;
				}
				bottom = num40;
				int num39 = num40;
				for (int num38 = 0; num38 < 2; num38++)
				{
					num40 = num39;
					int num36 = (num106 + num105) / 2;
					int num35;
					int num34 = 1;
					num35 = ((num95 >= num36) ? 1 : (-1));
					if (num95 == num36 || (num104 > 6 && (num95 == num36 - 1 || num95 == num36 + 1)))
					{
						num35 = 0;
					}
					int num32 = num35;
					int num31 = num95;
					num41 = genRand.Next((int)((double)num104 * 2.5), num104 * 4);
					while (num41 > 0)
					{
						num41--;
						num31 += num35;
						Main.tile[num31, num40].type = 191;
						Main.tile[num31, num40].active(active: true);
						Main.tile[num31, num40].halfBrick(halfBrick: false);
						num40 += num34;
						Main.tile[num31, num40].type = 191;
						Main.tile[num31, num40].active(active: true);
						Main.tile[num31, num40].halfBrick(halfBrick: false);
						if (!Main.tile[num31, num40 + 1].active())
						{
							num35 = 0;
							num34 = 1;
						}
						if (genRand.Next(3) == 0)
						{
							num35 = ((num32 >= 0) ? ((num32 <= 0) ? genRand.Next(-1, 2) : ((num35 == 0) ? 1 : 0)) : ((num35 == 0) ? (-1) : 0));
						}
						if (genRand.Next(3) == 0)
						{
							num34 = ((num34 == 0) ? 1 : 0);
						}
					}
				}
			}
			
			// LEAVES
			for (int num94 = 0; num94 < num113; num94++)
			{
				int num50 = genRand.Next(5, 8);
				num50 = (int)((float)num50 * (1f + (float)num104 * 0.05f));
				if (array2[num94])
				{
					num50 = genRand.Next(7, 13);
				}
				int num48 = array5[num94] - num50;
				int num47 = array5[num94] + num50;
				int num46 = array4[num94] - num50;
				top = num46;
				int num45 = array4[num94] + num50;
				float num44 = 2f - (float)genRand.Next(5) * 0.1f;
				for (int num43 = num48; num43 <= num47; num43++)
				{
					for (int num42 = num46; num42 <= num45; num42++)
					{
						if (Main.tile[num43, num42].type != 191 && (float)Math.Abs(array5[num94] - num43) + (float)Math.Abs(array4[num94] - num42) * num44 < (float)num50)
						{
							Main.tile[num43, num42].type = 192;
							Main.tile[num43, num42].active(active: true);
							Main.tile[num43, num42].halfBrick(halfBrick: false);
						}
					}
				}
			}
			
			// UNDERGROUND
			if (genUnderground && num104 >= 4 && genRand.Next(3) != 0)
			{
				bool flag9 = false;
				int num93 = num106;
				int num92 = num105;
				int num91 = j - 5;
				int num90 = 50;
				int num89 = genRand.Next(400, 700);
				int num88 = 1;
				bool flag8 = true;
				while (num89 > 0)
				{
					num91++;
					num89--;
					num90--;
					int num80 = (num106 + num105) / 2;
					int num79 = 0;
					if (num91 > j && num104 == 4)
					{
						num79 = 1;
					}
					for (int num78 = num106 - num79; num78 <= num105 + num79; num78++)
					{
						if (num78 > num80 - 2 && num78 <= num80 + 1)
						{
							if (Main.tile[num78, num91].type != 19)
							{
								Main.tile[num78, num91].active(active: false);
							}
							Main.tile[num78, num91].wall = 78;
							if (Main.tile[num78 - 1, num91].wall > 0 || (double)num91 >= Main.worldSurface)
							{
								Main.tile[num78 - 1, num91].wall = 78;
							}
							if (Main.tile[num78 + 1, num91].wall > 0 || (double)num91 >= Main.worldSurface)
							{
								Main.tile[num78 + 1, num91].wall = 78;
							}
						}
						else
						{
							Main.tile[num78, num91].type = 191;
							Main.tile[num78, num91].active(active: true);
							Main.tile[num78, num91].halfBrick(halfBrick: false);
						}
					}
					num88++;
					if (num88 >= 6)
					{
						num88 = 0;
						int num77 = genRand.Next(3);
						if (num77 == 0)
						{
							num77 = -1;
						}
						if (flag8)
						{
							num77 = 2;
						}
						if (num77 == 2)
						{
							flag8 = false;
							for (int num76 = num106; num76 <= num105; num76++)
							{
								if (num76 > num80 - 2 && num76 <= num80 + 1)
								{
									Main.tile[num76, num91 + 1].active(active: false);
									WorldGen.PlaceTile(num76, num91 + 1, 19, mute: true, forced: false, -1, 23);
								}
							}
						}
						else
						{
							num106 += num77;
							num105 += num77;
						}
						if (num90 <= 0 && !flag9)
						{
							flag9 = true;
							int num75 = genRand.Next(2);
							if (num75 == 0)
							{
								num75 = -1;
							}
							int num74 = num91 - 2;
							int num73 = num91;
							int num72 = (num106 + num105) / 2;
							if (num75 < 0)
							{
								num72--;
							}
							if (num75 > 0)
							{
								num72++;
							}
							int num71 = genRand.Next(15, 30);
							int num70 = num72 + num71;
							if (num75 < 0)
							{
								num70 = num72;
								num72 -= num71;
							}
							bool flag6 = false;
							for (int num69 = num72; num69 < num70; num69++)
							{
								for (int num53 = num91 - 20; num53 < num91 + 10; num53++)
								{
									if (Main.tile[num69, num53].wall == 0 && !Main.tile[num69, num53].active() && (double)num53 < Main.worldSurface)
									{
										flag6 = true;
									}
								}
							}
							if (!flag6)
							{
								for (int num68 = num72; num68 <= num70; num68++)
								{
									for (int num54 = num74 - 2; num54 <= num73 + 2; num54++)
									{
										if (Main.tile[num68, num54].wall != 78 && Main.tile[num68, num54].type != 19)
										{
											Main.tile[num68, num54].active(active: true);
											Main.tile[num68, num54].type = 191;
											Main.tile[num68, num54].halfBrick(halfBrick: false);
										}
										if (num54 >= num74 && num54 <= num73)
										{
											Main.tile[num68, num54].liquid = 0;
											Main.tile[num68, num54].wall = 78;
											Main.tile[num68, num54].active(active: false);
										}
									}
								}
								int i3 = (num106 + num105) / 2 + 3 * num75;
								int j2 = num91;
								WorldGen.PlaceTile(i3, j2, 10, mute: true, forced: false, -1, 7);
								int num67 = genRand.Next(5, 9);
								int num66 = genRand.Next(4, 6);
								if (num75 < 0)
								{
									num70 = num72 + num67;
									num72 -= num67;
								}
								else
								{
									num72 = num70 - num67;
									num70 += num67;
								}
								num74 = num73 - num66;
								for (int num62 = num72 - 2; num62 <= num70 + 2; num62++)
								{
									for (int num55 = num74 - 2; num55 <= num73 + 2; num55++)
									{
										if (Main.tile[num62, num55].wall != 78 && Main.tile[num62, num55].type != 19)
										{
											Main.tile[num62, num55].active(active: true);
											Main.tile[num62, num55].type = 191;
											Main.tile[num62, num55].halfBrick(halfBrick: false);
										}
										if (num55 >= num74 && num55 <= num73 && num62 >= num72 && num62 <= num70)
										{
											Main.tile[num62, num55].liquid = 0;
											Main.tile[num62, num55].wall = 78;
											Main.tile[num62, num55].active(active: false);
										}
									}
								}
								i3 = num72 - 2;
								if (num75 < 0)
								{
									i3 = num70 + 2;
								}
								WorldGen.PlaceTile(i3, j2, 10, mute: true, forced: false, -1, 7);
								int num61 = num70;
								if (num75 < 0)
								{
									num61 = num72;
								}
								WorldGen.PlaceTile(num61, num91, 15, mute: true, forced: false, -1, 5);
								if (num75 < 0)
								{
									Main.tile[num61, num91 - 1].frameX += 18;
									Main.tile[num61, num91].frameX += 18;
								}
								num61 = num70 - 2;
								if (num75 < 0)
								{
									num61 = num72 + 2;
								}
								WorldGen.PlaceTile(num61, num91, 14, mute: true, forced: false, -1, 6);
								num61 = num70 - 4;
								if (num75 < 0)
								{
									num61 = num72 + 4;
								}
								WorldGen.PlaceTile(num61, num91, 15, mute: true, forced: false, -1, 5);
								if (num75 > 0)
								{
									Main.tile[num61, num91 - 1].frameX += 18;
									Main.tile[num61, num91].frameX += 18;
								}
								num61 = num70 - 7;
								if (num75 < 0)
								{
									num61 = num72 + 8;
								}
								int num57 = 832;
								WorldGen.AddBuriedChest(num61, num91, num57, notNearOtherChests: false, 12);
							}
						}
					}
					if (num90 > 0)
					{
						continue;
					}
					bool flag5 = true;
					for (int num52 = num106; num52 <= num105; num52++)
					{
						for (int num51 = num91 + 1; num51 <= num91 + 4; num51++)
						{
							if (WorldGen.SolidTile(num52, num51))
							{
								flag5 = false;
							}
						}
					}
					if (flag5)
					{
						num89 = 0;
					}
				}
				num106 = num93;
				num105 = num92;
				int num85 = (num106 + num105) / 2;
				if (genRand.Next(2) == 0)
				{
					num105 = num85;
				}
				else
				{
					num106 = num85;
				}
				for (int num84 = num106; num84 <= num105; num84++)
				{
					for (int num83 = j - 3; num83 <= j; num83++)
					{
						Main.tile[num84, num83].active(active: false);
						bool flag7 = true;
						for (int num82 = num84 - 1; num82 <= num84 + 1; num82++)
						{
							for (int num81 = num83 - 1; num81 <= num83 + 1; num81++)
							{
								if (!Main.tile[num82, num81].active() && Main.tile[num82, num81].wall == 0)
								{
									flag7 = false;
								}
							}
						}
						if (flag7)
						{
							Main.tile[num84, num83].wall = 78;
						}
					}
				}
			}

			for (int x = num106; x <= num105; ++x)
				for (int y = j - 2; y < j + 3; ++y)
					Main.tile[x, y].active(false);

			for (int x = num106 + 1; x <= num105 - 1; ++x)
				for (int y = j - 2; y < j + 3; ++y)
					Main.tile[x, y].wall = Terraria.ID.WallID.LivingWood;

			if (Main.dedServ)
			{
				for (int pi = 0; pi < Main.player.Length; ++pi)
				{
					if (Main.player[pi] != null)
					{
						for (int ty = top - 10; ty < bottom + 10; ty += 10)
						{
							NetMessage.SendTileRange(pi, i - 50, ty, 100, ty + 10, Terraria.ID.TileChangeType.None);
						}
					}
				}
			}
			else
				WorldGen.RangeFrame(i - 50, top - 10, i + 50, bottom + 10);
			
			return true;
		}

		// Set world time
		public void SetTime(int time, bool dayTime)
		{
			if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer)
			{
				Main.time = time;
				Main.dayTime = dayTime;
			}
			else
			{
				SendData(EPacketEffect.SET_TIME, time, dayTime);
			}
		}

		// Rainbowify a given tile (called from client)
		public void RainbowifyTileClient(int x, int y, bool randomColour = false)
		{
			// Check if tile is active and in the bounds of the array
			if (x >= 0 && x < Main.maxTilesX && y >= 0 && y < Main.maxTilesY && !Main.tile[x, y].active()) 
				return; 

			// Forcefully set colour of tile
			if (randomColour)
			{
				byte colour = m_rainbowPaint[Main.rand.Next(m_rainbowPaint.Length)];
				if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer)
					WorldGen.paintTile(x, y, colour, false);
				else
					SendData(EPacketEffect.SET_PAINTTILE, x, y, colour);
			}

			// Rainbowify if not already rainbowified
			else if (!m_rainbowTiles.Contains(Main.tile[x, y]))
			{
				if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer)
					WorldGen.paintTile(x, y, m_rainbowPaint[m_rainbowIndex], false);
				else
					SendData(EPacketEffect.SET_PAINTTILE, x, y, m_rainbowPaint[m_rainbowIndex]);

				m_rainbowTiles.Add(Main.tile[x, y]);
				m_rainbowIndex = (m_rainbowIndex + 1) % m_rainbowPaint.Length;
			}
		}

		// Send a modded effect packet to the server (as client)
		public void SendData(EPacketEffect packetEffect, params object[] data)
		{
			if (Main.dedServ)
				return;

			try
			{
				ModPacket packet = CrowdControlMod._mod.GetPacket(data.Length);
				packet.Write((byte)packetEffect);
				packet.Write(Main.myPlayer);
				if (data != null)
				{
					for (int i = 0; i < data.Length; ++i)
						WriteToPacket(packet, data[i]);
				}
				packet.Send(-1, -1);
				TDebug.WriteDebug("Client sent packet type: " + packetEffect, Color.Yellow);
				
			}
			catch (Exception e) { TDebug.WriteDebug("Failed to send modded packet: " + e.Message, Color.Yellow); }
		}

		// Handle a modded effect packet (as server)
		public void HandleData(EPacketEffect packetEffect, int sender, BinaryReader reader)
		{
			string debugText = "Server received packet type: " + packetEffect + " (";
			int x, y;
			bool enabled;
			switch (packetEffect)
			{
				case EPacketEffect.CC_CONNECT:
					NetMessage.BroadcastChatMessage(Terraria.Localization.NetworkText.FromLiteral("[i:1525] " + Main.player[sender].name + " has connected to Crowd Control"), Color.Green, sender);
					break;
				case EPacketEffect.SPAWN_NPC:
					int type = reader.ReadInt16();
					 x = reader.ReadInt32();
					 y = reader.ReadInt32();
					int id = NPC.NewNPC(x, y, type);
					NetMessage.SendData(Terraria.ID.MessageID.SyncNPC, -1, -1, null, id);
					debugText += type + ", " + x + ", " + y;
					break;
				case EPacketEffect.SET_TIME:
					int time = reader.ReadInt32();
					bool dayTime = reader.ReadBoolean();
					Main.time = time;
					Main.dayTime = dayTime;
					NetMessage.SendData(Terraria.ID.MessageID.WorldData, -1, -1, null);
					debugText += time + ", " + dayTime;
					break;
				case EPacketEffect.SET_SPAWNRATE:
					float spawnRate = reader.ReadSingle();
					CCPlayer player = Main.player[sender].GetModPlayer<CCPlayer>();
					if (player != null) player.m_spawnRate = spawnRate;
					debugText += spawnRate;
					break;
				case EPacketEffect.GEN_STRUCT:
					string viewer = reader.ReadString();
					Effect_SpawnHouse(Main.player[sender], viewer);
					debugText += viewer;
					break;
				case EPacketEffect.SET_PAINTTILE:
					x = reader.ReadInt32();
					y = reader.ReadInt32();
					byte colour = reader.ReadByte();
					WorldGen.paintTile(x, y, colour, true);
					debugText += x + ", " + y + ", " + colour;
					break;
				case EPacketEffect.SET_SPEED:
					enabled = reader.ReadBoolean();
					Main.player[sender].GetModPlayer<CCPlayer>().m_servSpeed = enabled;
					debugText += enabled;
					break;
				case EPacketEffect.SET_JUMP:
					enabled = reader.ReadBoolean();
					Main.player[sender].GetModPlayer<CCPlayer>().m_servJump = enabled;
					debugText += enabled;
					break;
				case EPacketEffect.START_SUNDIAL:
					Main.fastForwardTime = true;
					NetMessage.SendData(Terraria.ID.MessageID.WorldData, -1, -1, null);
					break;
			}

			TDebug.WriteDebug(debugText + ") from " + Main.player[sender].name, Color.Yellow);
		}

		// Attempt to write data to a packet
		private void WriteToPacket(ModPacket packet, object data)
		{
			if (data is bool) packet.Write((bool)data);
			else if (data is byte) packet.Write((byte)data);
			else if (data is byte[]) packet.Write((byte[])data);
			else if (data is int) packet.Write((int)data);
			else if (data is float) packet.Write((float)data);
			else if (data is string) packet.Write((string)data);
			else if (data is char) packet.Write((char)data);
			else if (data is short) packet.Write((short)data);
			else packet.Write(0);
		}

		// Set the ModPlayer instance affected by Crowd Control Effects (note that the mod should be used in Singleplayer)
		public void SetPlayer(CCPlayer player)
        {
            m_player = player;
            TDebug.WriteDebug("Setting player to " + m_player.player.name, Color.Yellow);
        }

        // Get the ModPlayer instance affected by Crowd Control Effects
        public CCPlayer GetPlayer()
        {
            return m_player;
        }
    }
}
