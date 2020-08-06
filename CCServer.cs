///<summary>
/// File: CCServer.cs
/// Last Updated: 2020-08-06
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
using CrowdControlMod.Projectiles;
using CrowdControlMod.NPCs;

namespace CrowdControlMod
{
	public class CCServer
	{
        #region Network Types

		// byte effect, int sender
        public enum EPacketEffect : byte
		{
			CC_CONNECT,         // Broadcasts a message to the server when a client connects to Crowd Control
			SPAWN_NPC,          // int type, int x, int y
			SET_TIME,           // int time, bool dayTime
			SET_SPAWNRATE,      // float spawnRate
			GEN_STRUCT,         // string viewer
			SET_PAINTTILE,      // int x, int y, byte colour (Paints a tile with a colour)
			START_SUNDIAL,
			SEND_CONFIG,		// bool disableTombstones
			TOWN_MAYHEM,		// bool enabled
			EFFECT_MESSAGE		// string type
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

            // I hate structs
#pragma warning disable IDE0051 // Remove unused private members
            private Request(int n)
#pragma warning restore IDE0051 // Remove unused private members
            {
				id = n;
				code = "";
				viewer = "";
				type = 0;
            }

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

            // Structs suck
#pragma warning disable IDE0051 // Remove unused private members
            private Response(int n)
#pragma warning restore IDE0051 // Remove unused private members
            {
				id = n;
				status = 0;
				message = "";
            }

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

        #region Other Types

		public enum EProgression
        {
			PRE_EYE,
			PRE_SKELETRON,
			PRE_WOF,
			PRE_MECH,
			PRE_GOLEM,
			PRE_LUNAR,
			PRE_MOON_LORD,
			END_GAME
        }

        #endregion

        #region Timers

        // Timers used for some effects
        public readonly Timer m_fastPlayerTimer = null;
		public readonly Timer m_jumpPlayerTimer = null;
		public readonly Timer m_slipPlayerTimer = null;
		public readonly Timer m_incCoinsTimer = null;
		public readonly Timer m_infiniteManaTimer = null;
		public readonly Timer m_infiniteAmmoTimer = null;
		public readonly Timer m_rainbowPaintTimer = null;
		public readonly Timer m_shootBombTimer = null;
		public readonly Timer m_shootGrenadeTimer = null;
		public readonly Timer m_projItemTimer = null;
		public readonly Timer m_increasedSpawnsTimer = null;
		public readonly Timer m_invisTimer = null;
		public readonly Timer m_flipCameraTimer = null;
		public readonly Timer m_fishWallTimer = null;
		public readonly Timer m_rainbowScreenTimer = null;
		public readonly Timer m_corruptScreenTimer = null;
		public readonly Timer m_drunkScreenTimer = null;

		// Times for the various effects (in seconds)
		public readonly int m_timeFastPlayer = 25;
		public readonly int m_timeJumpPlayer = 25;
		public readonly int m_timeSlipPlayer = 25;
		public readonly int m_timeIncCoins = 40;
		public readonly int m_timeInfiniteMana = 25;
		public readonly int m_timeInfiniteAmmo = 25;
		public readonly int m_timeRainbowPaint = 45;
		public readonly int m_timeShootBomb = 20;
		public readonly int m_timeShootGrenade = 30;
		public readonly int m_timeProjItem = 45;
		public readonly int m_timeIncSpawnrate = 30;
		public readonly int m_timeBuffFreeze = 3;
		public readonly int m_timeBuffFire = 10;
		public readonly int m_timeBuffDaze = 25;
		public readonly int m_timeBuffLev = 25;
		public readonly int m_timeBuffConf = 25;
		public readonly int m_timeBuffInvis = 25;
		public readonly int m_timeBuffIron = 60;
		public readonly int m_timeBuffRegen = 60;
		public readonly int m_timeBuffLight = 60;
		public readonly int m_timeBuffTreasure = 60;
		public readonly int m_timeBuffLife = 60;
		public readonly int m_timeBuffSpeed = 60;
		public readonly int m_timeFlipScreen = 25;
		public readonly int m_timeFishWall = 25;
		public readonly int m_timeDarkScreen = 25;
		public readonly int m_timeRainbowScreen = 25;
		public readonly int m_timeCorruptScreen = 25;
		public readonly int m_timeDrunkScreen = 25;

        #endregion

        #region Effect Variables

        // Parameters for the various effects
        private readonly string[] m_killVerb =								// Collection of kill verbs used in the killplr command (one is chosen randomly each time) "<player> was <kill-verb> by <viewer>"
        {
			"murdered", "executed", "demolished", "destroyed", "spat on",
			"slam dunked", "killed", "slapped", "brutally ripped apart",
			"ripped to shreds", "attacked with a toothbrush", "hit with cotton-candy",
			"run over", "tormented", "subjected to a bad pun", "called stinky",
			"cancelled", "smacked with a fish", "hugged too tightly", "poked",
			"force-fed poison ivy", "led into a room of angry fans", "scuffed",
			"seen crying in the corner", "invited to a kitchen party",
			"fed [c/FFFF00:ra][c/FF0000:in][c/0000FF:bo][c/8B00FF:ws]",
			"shot with a watergun"
		};
		private readonly float m_damagePlayerPerc = 0.15f;                  // Set player health to this percentage of the max life
		private readonly float m_damagePlayerPercPM = 0.02f;				// +- percentage (range) added to damagePlayerPerc
		public readonly float m_fastPlrMaxSurfSpeed = 15f;					// Max movement speed on surface
		public readonly float m_fastPlrSurfAccel = 2f;						// Movement acceleration on surface
		public readonly float m_fastPlrMaxCaveSpeed = 9f;					// Max movement speed underground
		public readonly float m_fastPlrCaveAccel = 1f;						// Movement acceleration underground
		public readonly float m_jumpPlrBoost = 9f;			                // Player jump speed
		public readonly float m_slipPlrAccel = 0.4f;						// Player run acceleration when slippery (percentage of current accel)
		private readonly int m_potionStack = 2;                             // Number of potions to provide the player
		private readonly int[] m_preMelee =									// Prefix IDs used for melee weapons
		{
			1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,81
		};
		private readonly int[] m_preRange =									// Prefix IDs used for ranged weapons
		{
			16,17,18,19,20,21,22,23,24,25,58,82
		};
		private readonly int[] m_preMage =									// Prefix IDs used for magic weapons
		{
			26,27,28,29,30,31,32,33,34,35,52,83
		};
		private readonly int[] m_preUni =									// Prefix IDs used for universal weapons (can be applied to any weapon)
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
		private readonly CyclicArray<int> m_prevLightPets;
		private readonly int[] m_lightPets =
		{
			Terraria.ID.BuffID.ShadowOrb, Terraria.ID.BuffID.CrimsonHeart, Terraria.ID.BuffID.MagicLantern, Terraria.ID.BuffID.FairyBlue,
			Terraria.ID.BuffID.FairyGreen, Terraria.ID.BuffID.FairyRed, 201, Terraria.ID.BuffID.Wisp, Terraria.ID.BuffID.SuspiciousTentacle
		};
		private readonly byte[] m_rainbowPaint =							// Paint IDs that form a somewhat-rainbow (in order)
		{
			13,14,15,16,17,18,19,20,21,22,23,24
		};
		private int m_rainbowIndex = 0;										// Index of the paint in the rainbowPaint array to use next
		private readonly CyclicArray<Tile> m_rainbowTiles;                  // Keep track of the tiles painted so the same tile is painted repeatedly
		private readonly int m_spawnGuardHalfHeight = 16 * 34;              // Half height distance from player that the guardian can spawn
		private readonly int m_spawnGuardHalfWidth = 16 * 90;               // Half width distance from player that the guardian can spawn
		private readonly List<Tuple<int, int>> m_guardians =				// List of dungeon guardians that are spawned via effects
			new List<Tuple<int, int>>();
		private readonly int m_guardianSurvivalTime = 60 * 6;				// How long the player needs to survive the dungeon guardian for to "win"
        public readonly float m_increaseSpawnRate = 12f;					// Factor that the spawnrate is increased
		public readonly float m_fishWallOffset = 0.85f;                     // Offset between fish walls (janky)
		public readonly float m_drunkGlitchIntensity = 24f;                 // Glitch intensity for drunk shader
		private readonly int m_timeLovestruck = 3;							// Time to show the lovestruck particles

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

		// Configuration variables
		public static bool _showEffectMessages = true;                      // Whether to show effect messages in chat
		public static bool _shouldConnectToCC = true;                       // Whether to connect to crowd control
		public static bool _disableTombstones = false;                      // Disable tombstones
		public static float _respawnTimeFactor = 1f;                        // Respawn time factor
		public static bool _disableHairDye = false;							// Whether to disable hair dye effects
		public static bool _disableMusic = true;							// Whether to disable music associated with some effects (mainly screen effects)
		public static bool _reduceDrunkEffect = false;						// Whether to prevent the screen from moving during the drunk effect
		public static bool _reduceCorruptEffect = false;                    // Whether to slow down the rate of colour changing during the corrupt effect
		public static bool _allowTimeChangeInBoss = true;                   // Whether to allow time-changing effects during bosses, invasions or events
		public static bool _allowTeleportingToPlayers = true;				// Whether to allow the player to teleport to other players in MP

        #endregion

        // Default constructor
        public CCServer()
        {
            // Ignore silent exceptions thrown by Socket.Connect (cleaner chat during testing)
            Logging.IgnoreExceptionContents("System.Net.Sockets.Socket.Connect");
            Logging.IgnoreExceptionContents("System.Net.Sockets.Socket.DoConnect");

            // Set readonly colours
            MSG_C_NEGATIVE = Color.Red;
            MSG_C_NEUTRAL = Color.Orange;
            MSG_C_POSITIVE = Color.Green;
			MSG_C_TIMEREND = Color.Green;

			m_prevPets = new CyclicArray<int>(5);
			m_prevLightPets = new CyclicArray<int>(m_lightPets.Length);
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

				m_slipPlayerTimer = new Timer
				{
					Interval = 1000 * m_timeSlipPlayer,
					AutoReset = false
				};
				m_slipPlayerTimer.Elapsed += delegate { StopEffect("slipplr"); };

				m_incCoinsTimer = new Timer
				{
					Interval = 1000 * m_timeIncCoins,
					AutoReset = false
				};
				m_incCoinsTimer.Elapsed += delegate { StopEffect("item_money"); };

				m_infiniteManaTimer = new Timer
				{
					Interval = 1000 * m_timeInfiniteMana,
					AutoReset = false
				};
				m_infiniteManaTimer.Elapsed += delegate { StopEffect("plr_mana"); };

				m_infiniteAmmoTimer = new Timer
				{
					Interval = 1000 * m_timeInfiniteAmmo,
					AutoReset = false
				};
				m_infiniteAmmoTimer.Elapsed += delegate { StopEffect("plr_ammo"); };

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

				m_shootGrenadeTimer = new Timer
                {
                    Interval = 1000 * m_timeShootGrenade,
                    AutoReset = false
                };
				m_shootGrenadeTimer.Elapsed += delegate { StopEffect("shoot_grenade"); };

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

				m_invisTimer = new Timer
				{
					Interval = 1000 * m_timeBuffInvis,
					AutoReset = false
				};
				m_invisTimer.Elapsed += delegate { StopEffect("buff_invis"); };

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

				m_rainbowScreenTimer = new Timer
				{
					Interval = 1000 * m_timeRainbowScreen,
					AutoReset = false
				};
				m_rainbowScreenTimer.Elapsed += delegate { StopEffect("cam_rainbow"); };

				m_corruptScreenTimer = new Timer
				{
					Interval = 1000 * m_timeCorruptScreen,
					AutoReset = false
				};
				m_corruptScreenTimer.Elapsed += delegate { StopEffect("cam_corrupt"); };

				m_drunkScreenTimer = new Timer
				{
					Interval = 1000 * m_timeDrunkScreen,
					AutoReset = false
				};
				m_drunkScreenTimer.Elapsed += delegate { StopEffect("cam_drunk"); };
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
			m_activeSocket = null;
			m_serverThread = null;
            TDebug.WriteDebug("Server stopped", Color.Yellow);

			if (m_fastPlayerTimer.Enabled)
				StopEffect("fastplr");
			if (m_jumpPlayerTimer.Enabled)
				StopEffect("jumpplr");
			if (m_slipPlayerTimer.Enabled)
				StopEffect("slipplr");
			if (m_incCoinsTimer.Enabled)
				StopEffect("item_money");
			if (m_infiniteManaTimer.Enabled)
				StopEffect("plr_mana");
			if (m_infiniteAmmoTimer.Enabled)
				StopEffect("plr_ammo");
			if (m_rainbowPaintTimer.Enabled)
				StopEffect("tile_paint");
            if (m_shootBombTimer.Enabled)
                StopEffect("shoot_bomb");
			if (m_shootGrenadeTimer.Enabled)
                StopEffect("shoot_grenade");
			if (m_projItemTimer.Enabled)
				StopEffect("proj_item");
            if (m_increasedSpawnsTimer.Enabled)
                StopEffect("inc_spawnrate");
			if (m_invisTimer.Enabled)
				StopEffect("buff_invis");
            if (m_flipCameraTimer.Enabled)
                StopEffect("cam_flip");
			if (m_fishWallTimer.Enabled)
				StopEffect("cam_fish");
			if (m_rainbowScreenTimer.Enabled)
				StopEffect("cam_rainbow");
			if (m_corruptScreenTimer.Enabled)
				StopEffect("cam_corrupt");
			if (m_drunkScreenTimer.Enabled)
				StopEffect("cam_drunk");

			m_prevPets.Clear();
			m_prevLightPets.Clear();
			m_player = null;
		}

        // Server loop (attempts to connect to Crowd Control)
        private void ServerLoop()
        {
            // Initialise Main.rand in this thread because it is marked as NON THREAD STATIC
            Main.rand = new Terraria.Utilities.UnifiedRandom();
            bool writeAttempt = true;
            bool connected = false;
			bool shouldNotConnectText = true;

            while (true)
            {
				if (!_shouldConnectToCC)
                {
					if (shouldNotConnectText)
                    {
						TDebug.WriteMessage(3643, "Crowd Control disabled in Mod Configuration", Color.Green);
						shouldNotConnectText = false;
                    }
					System.Threading.Thread.Sleep(1000 * 2);
					continue;
                }

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
            if ((Main.gamePaused || Main.player[Main.myPlayer].dead) && requestType != (int)RequestType.STOP)
            {
                TDebug.WriteDebug("Game is paused or player is dead. Will retry [" + code + "]", Color.Yellow);
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
					m_player.m_reduceRespawn = true;
					m_player.player.KillMe(
                        Terraria.DataStructures.PlayerDeathReason.ByCustomReason(m_player.player.name + " was " + (m_killVerb[Main.rand.Next(m_killVerb.Length)]) + " by " + viewer),
                        1000, 0, false);
                    break;

				case "explodeplr":
					m_player.m_reduceRespawn = true;
					m_player.player.KillMe(
						Terraria.DataStructures.PlayerDeathReason.ByCustomReason(m_player.player.name + " was brutally torn apart by " + viewer + "'s explosive"),
						1000, 0, false);
					Projectile.NewProjectile(m_player.player.Center, Vector2.Zero, ModContent.ProjectileType<InstaDynamite>(), 1, 1f, Main.myPlayer);
					break;

				case "healplr":
					if (m_player.player.statLife == m_player.player.statLifeMax2) return EffectResult.FAILURE;
					m_player.player.statLife = m_player.player.statLifeMax2;
					m_player.player.AddBuff(Terraria.ID.BuffID.Lovestruck, 60 * m_timeLovestruck);
                    ShowEffectMessage(58, viewer + " healed " + m_player.player.name, MSG_C_POSITIVE);
                    break;

                case "damplr":
					//int life = (int)(m_player.player.statLife * (m_damagePlayerPerc + Main.rand.NextFloat(-m_damagePlayerPercPM, m_damagePlayerPercPM)));
					//m_player.player.statLife = life;
					m_player.player.statLife -= (int)(m_player.player.statLifeMax2 * 0.15f);
					ShowEffectMessage(3106, viewer + " severely damaged " + m_player.player.name, MSG_C_NEGATIVE);
					break;

				case "plr_inclife":
					if (m_player.player.statLifeMax >= 500) return EffectResult.FAILURE;
					m_player.player.statLifeMax += 20;
					m_player.player.statLife += 20;
					m_player.player.AddBuff(Terraria.ID.BuffID.Lovestruck, 60 * m_timeLovestruck);
					ShowEffectMessage(29, viewer + " added 20 health to " + m_player.player.name + "'s total health", MSG_C_POSITIVE);
					break;

				case "plr_declife":
					if (m_player.player.statLifeMax <= 20) return EffectResult.FAILURE;
					m_player.player.statLifeMax -= 20;
					ShowEffectMessage(29, viewer + " removed 20 health from " + m_player.player.name + "'s total health", MSG_C_NEGATIVE);
					break;

				case "plr_incmana":
					if (m_player.player.statManaMax >= 200) return EffectResult.FAILURE;
					m_player.player.statManaMax += 20;
					m_player.player.statMana += 20;
					ShowEffectMessage(109, viewer + " added 20 mana to " + m_player.player.name + "'s total mana", MSG_C_POSITIVE);
					break;

				case "plr_decmana":
					if (m_player.player.statManaMax <= 20) return EffectResult.FAILURE;
					m_player.player.statManaMax -= 20;
					ShowEffectMessage(109, viewer + " removed 20 mana from " + m_player.player.name + "'s total mana", MSG_C_NEGATIVE);
					break;

				case "fastplr":
					if (m_fastPlayerTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_fastPlayerTimer);
					m_player.SetHairDye(CCPlayer.EHairDye.SPEED);
					ShowEffectMessage(898, viewer + " made " + m_player.player.name + " really, really fast for " + m_timeFastPlayer + " seconds", MSG_C_NEUTRAL);
					break;

				case "jumpplr":
					if (m_jumpPlayerTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_jumpPlayerTimer);
					ShowEffectMessage(1164, viewer + " made it so " + m_player.player.name + " can jump very high for " + m_timeJumpPlayer + " seconds", MSG_C_NEUTRAL);
					break;

				case "slipplr":
					if (m_slipPlayerTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_slipPlayerTimer);
					m_player.player.AddBuff(Terraria.ID.BuffID.Wet, 60 * m_timeSlipPlayer);
					ShowEffectMessage(950, viewer + " made the ground very slippery", MSG_C_NEGATIVE);
					break;

				case "randtp":
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
						NetMessage.SendData(Terraria.ID.MessageID.TeleportationPotion);
					else
					{
						m_player.player.TeleportationPotion();
						Main.PlaySound(Terraria.ID.SoundID.Item6, m_player.player.position);
					}
					m_player.SetHairDye(CCPlayer.EHairDye.BIOME);
					ShowEffectMessage(2351, viewer + " randomly teleported " + m_player.player.name, MSG_C_NEUTRAL);
                    break;

				case "deathtp":
					if (m_player.TeleportToDeathPoint())
						ShowEffectMessage(1326, viewer + " sent " + m_player.player.name + " back to their last death position", MSG_C_NEUTRAL);
					else
					{
						m_player.player.Spawn();
						ShowEffectMessage(1326, viewer + " sent " + m_player.player.name + " to spawn because there is no valid death position", MSG_C_NEUTRAL);
					}
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
					int coins = Item.buyPrice(0, Math.Max(Main.rand.Next(-7,2), 0), Main.rand.Next(50, 150));
					m_player.GiveCoins(coins);
					m_player.SetHairDye(CCPlayer.EHairDye.MONEY);
					ResetTimer(m_incCoinsTimer);
					ShowEffectMessage(855, viewer + " donated " + Main.ValueToCoins(coins) + " to " + m_player.player.name + " and increased coin drops from enemies for " + m_timeIncCoins + " seconds", MSG_C_POSITIVE);
					break;

				case "item_heal":
					int itemID = ChoosePerProgression(28, 188, 188, 499, 499, 499, 3544, 3544);
					int id = Item.NewItem((int)m_player.player.position.X, (int)m_player.player.position.Y, m_player.player.width, m_player.player.height, itemID, m_potionStack);
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
						NetMessage.SendData(Terraria.ID.MessageID.SyncItem, -1, -1, null, id, 1f);
					ShowEffectMessage(itemID, viewer + " gave " + m_player.player.name + " " + m_potionStack + " " + Main.item[id].Name + "s", MSG_C_POSITIVE);
					break;

				case "item_pickaxe":
					int pickType = ChoosePerProgression(
						preEye: Choose(3509, 3503, 1, 3497, 3515, 3491, 882, 3485),
						preSkeletron: Choose(3521, 1917, 1320),
						preWOF: Choose(103, 798),
						preMech: Choose(122, 776, 1188, 777, 1195),
						preGolem: Choose(778, 1202, 1506, 1230, 990, 2176),
						preLunar: Choose(1294, 2176, 990),
						preMoonLord: Choose(1294, 2176),
						postGame: Choose(2776, 2781, 2786, 3466)
						);
					int pickID = Item.NewItem((int)m_player.player.position.X, (int)m_player.player.position.Y, m_player.player.width, m_player.player.height, pickType, 1);
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
						NetMessage.SendData(Terraria.ID.MessageID.SyncItem, -1, -1, null, pickID, 1f);
					ShowEffectMessage(pickType, viewer + " gave " + m_player.player.name + " a " + Main.item[pickID].Name, MSG_C_POSITIVE);
					break;

				case "item_sword":
					int swordType = ChoosePerProgression(
						preEye: Choose(1827, 4, 3496, 3490, 1304, 3772, 881),
						preSkeletron: Choose(3520, 3484, 1166, 1909, 2273, 724, 46, 795),
						preWOF: Choose(155, 3349, 65, 1123, 190, 121, 273),
						preMech: Choose(3258, 483, 1185, 1192, 484, 3823, 1306, 426, 672, 482, 1199, 676, 723, 3013, 3211),
						preGolem: Choose(368, 1227, 674, 1327, 3106, 671, 1226, 1826, 1928, 675, 3018),
						preLunar: Choose(3018, 3827, 757, 2880),
						preMoonLord: Choose(3827, 757, 2880),
						postGame: Choose(3065, 3063)
					);
					int swordID = Item.NewItem((int)m_player.player.position.X, (int)m_player.player.position.Y, m_player.player.width, m_player.player.height, swordType, 1);
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
						NetMessage.SendData(Terraria.ID.MessageID.SyncItem, -1, -1, null, swordID, 1f);
					ShowEffectMessage(swordType, viewer + " gave " + m_player.player.name + " a " + Main.item[swordID].Name, MSG_C_POSITIVE);
					break;

				case "item_armour":
					//TODO: Armour
					int armourType = ChoosePerProgression(
						preEye: 0,
						preSkeletron: 0,
						preWOF: 0,
						preMech: 0,
						preGolem: 0,
						preLunar: 0,
						preMoonLord: 0,
						postGame: 0
					);
					int armourID = Item.NewItem((int)m_player.player.position.X, (int)m_player.player.position.Y, m_player.player.width, m_player.player.height, armourType, 1);
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
						NetMessage.SendData(Terraria.ID.MessageID.SyncItem, -1, -1, null, armourID, 1f);
					ShowEffectMessage(armourType, viewer + " gave " + m_player.player.name + " a " + Main.item[armourID].Name, MSG_C_POSITIVE);
					break;

				case "plr_mana":
					if (m_infiniteManaTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_infiniteManaTimer);
					m_player.SetHairDye(CCPlayer.EHairDye.MANA);
					ShowEffectMessage(555, viewer + " blessed " + m_player.player.name + " with infinite magical power for " + m_timeInfiniteMana + " seconds", MSG_C_POSITIVE);
					break;

				case "plr_ammo":
					if (m_infiniteAmmoTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_infiniteAmmoTimer);
					ShowEffectMessage(3103, viewer + " provided infinite ammo to " + m_player.player.name + " for " + m_timeInfiniteAmmo + " seconds", MSG_C_POSITIVE);
					break;

				case "item_pet":
					Effect_GivePet(viewer);
					break;

				case "item_lightpet":
					Effect_GiveLightPet(viewer);
					break;

				case "plr_gender":
					m_player.player.Male = !m_player.player.Male;
					Main.PlaySound(Terraria.ID.SoundID.Item6, m_player.player.position);
					m_player.SetHairDye(CCPlayer.EHairDye.PARTY);
					ShowEffectMessage(2756, viewer + " changed " + m_player.player.name + " to a " + (m_player.player.Male ? "boy" : "girl"), MSG_C_NEUTRAL);
					break;

				case "tile_paint":
					if (m_rainbowPaintTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_rainbowPaintTimer);
					m_rainbowIndex = Main.rand.Next(m_rainbowPaint.Length);
					m_rainbowTiles.Clear();
					m_player.SetHairDye(CCPlayer.EHairDye.RAINBOW);
					ShowEffectMessage(1066, viewer + " caused a rainbow to form underneath " + m_player.player.name + " for " + m_timeRainbowPaint + " seconds", MSG_C_NEUTRAL);
					break;

				case "shoot_bomb":
					if (m_shootBombTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_shootBombTimer);
                    ShowEffectMessage(166, "Shooting explosives for " + m_timeShootBomb + " seconds thanks to " + viewer, MSG_C_NEGATIVE);
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) SendData(EPacketEffect.EFFECT_MESSAGE, code);
					break;

				case "shoot_grenade":
					if (m_shootGrenadeTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_shootGrenadeTimer);
					ShowEffectMessage(168, "Shooting grenades for " + m_timeShootGrenade + " seconds thanks to " + viewer, MSG_C_NEUTRAL);
					break;

				case "proj_item":
					if (m_projItemTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_projItemTimer);
					ModGlobalProjectile.m_textureOffset = Main.rand.Next(Main.itemTexture.Length);
					//Items.ModGlobalItem.m_textureOffset = Main.rand.Next(Main.itemTexture.Length);
					ShowEffectMessage(3311, viewer + " randomised projectile sprites for " + m_timeProjItem + " seconds", MSG_C_NEUTRAL);
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
					Vector2 circlePos = Main.rand.NextVector2CircularEdge(m_spawnGuardHalfWidth, m_spawnGuardHalfHeight);
					Point spawnPos = new Point((int)m_player.player.position.X + (int)circlePos.X, (int)m_player.player.position.Y + (int)circlePos.Y);
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) m_guardians.Add(new Tuple<int, int>(NPC.NewNPC(spawnPos.X, spawnPos.Y, Terraria.ID.NPCID.DungeonGuardian), m_guardianSurvivalTime));
					else SendData(EPacketEffect.SPAWN_NPC, Terraria.ID.NPCID.DungeonGuardian, spawnPos.X, spawnPos.Y);
                    ShowEffectMessage(1274, viewer + " spawned a Dungeon Guardian", MSG_C_NEGATIVE);
                    break;

                case "sp_bunny":
                    Effect_SpawnBunny(viewer);
                    break;

                case "inc_spawnrate":
					if (m_increasedSpawnsTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_increasedSpawnsTimer);
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) m_player.m_spawnRate = m_increaseSpawnRate;
					else SendData(EPacketEffect.SET_SPAWNRATE, m_increaseSpawnRate);
                    ShowEffectMessage(148, viewer + " increased the spawnrate for " + m_timeIncSpawnrate + " seconds", MSG_C_NEUTRAL);
					if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) SendData(EPacketEffect.EFFECT_MESSAGE, code);
                    break;

				case "buff_freeze":
					if (m_player.HasBuff(Terraria.ID.BuffID.Frozen)) return EffectResult.RETRY;
					m_player.player.buffImmune[Terraria.ID.BuffID.Frozen] = false;
					m_player.player.AddBuff(Terraria.ID.BuffID.Frozen, 60 * m_timeBuffFreeze, true);
					m_player.m_ignoreImmuneFrozen = true;
					ShowEffectMessage(1253, viewer + " cast a chilly spell over " + m_player.player.name, MSG_C_NEGATIVE);
					break;

				case "buff_fire":
					if (m_player.HasBuff(Terraria.ID.BuffID.OnFire)) return EffectResult.RETRY;
					m_player.player.AddBuff(Terraria.ID.BuffID.OnFire, 60 * m_timeBuffFire, true);
					Projectile.NewProjectile(m_player.player.position, new Vector2(0f, 10f), Terraria.ID.ProjectileID.MolotovCocktail, 1, 1f, Main.myPlayer);
					ShowEffectMessage(2590, viewer + " threw a molotov at " + m_player.player.name + "'s feet", MSG_C_NEGATIVE);
					break;

				case "buff_daze":
					if (m_player.HasBuff(Terraria.ID.BuffID.Dazed)) return EffectResult.RETRY;
                    m_player.player.AddBuff(Terraria.ID.BuffID.Dazed, 60 * m_timeBuffDaze, true);
                    ShowEffectMessage(75, viewer + " dazed " + m_player.player.name, MSG_C_NEGATIVE);
                    break;

                case "buff_lev":
					if (m_player.HasBuff(Terraria.ID.BuffID.VortexDebuff)) return EffectResult.RETRY;
                    m_player.player.AddBuff(Terraria.ID.BuffID.VortexDebuff, 60 * m_timeBuffLev, true);
                    ShowEffectMessage(3456, viewer + " distorted gravity around " + m_player.player.name, MSG_C_NEGATIVE);
                    break;

                case "buff_confuse":
					if (m_player.HasBuff(Terraria.ID.BuffID.Confused)) return EffectResult.RETRY;
					m_player.player.buffImmune[Terraria.ID.BuffID.Confused] = false;
					m_player.player.AddBuff(Terraria.ID.BuffID.Confused, 60 * m_timeBuffConf, true);
					m_player.m_ignoreImmuneConfusion = true;
					ShowEffectMessage(3223, viewer + " confused " + m_player.player.name, MSG_C_NEGATIVE);
                    break;

				case "buff_invis":
					if (m_invisTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_invisTimer);
					ShowEffectMessage(1752, viewer + " stole " + m_player.player.name + "'s body for " + m_timeBuffInvis + " seconds O-o", MSG_C_NEGATIVE);
					break;

				case "buff_iron":
					if (m_player.HasBuff(Terraria.ID.BuffID.Ironskin, Terraria.ID.BuffID.Endurance)) return EffectResult.RETRY;
                    m_player.player.AddBuff(Terraria.ID.BuffID.Ironskin, 60 * m_timeBuffIron, true);
                    m_player.player.AddBuff(Terraria.ID.BuffID.Endurance, 60 * m_timeBuffIron, true);
                    ShowEffectMessage(292, viewer + " provided " + m_player.player.name + " with survivability buffs", MSG_C_POSITIVE);
                    break;

                case "buff_regen":
					if (m_player.HasBuff(Terraria.ID.BuffID.Regeneration, Terraria.ID.BuffID.ManaRegeneration)) return EffectResult.RETRY;
                    m_player.player.AddBuff(Terraria.ID.BuffID.Regeneration, 60 * m_timeBuffRegen, true);
                    m_player.player.AddBuff(Terraria.ID.BuffID.SoulDrain, 60 * m_timeBuffRegen, true);
                    m_player.player.AddBuff(Terraria.ID.BuffID.ManaRegeneration, 60 * m_timeBuffRegen, true);
					m_player.SetHairDye(CCPlayer.EHairDye.LIFE);
					m_player.player.AddBuff(Terraria.ID.BuffID.Lovestruck, 60 * m_timeLovestruck);
                    ShowEffectMessage(289, viewer + " provided " + m_player.player.name + " with regeneration buffs", MSG_C_POSITIVE);
                    break;

				case "buff_light":
					if (m_player.HasBuff(Terraria.ID.BuffID.NightOwl, Terraria.ID.BuffID.Shine)) return EffectResult.RETRY;
					m_player.player.AddBuff(Terraria.ID.BuffID.NightOwl, 60 * m_timeBuffLight, true);
					m_player.player.AddBuff(Terraria.ID.BuffID.Shine, 60 * m_timeBuffLight, true);
					m_player.SetHairDye(CCPlayer.EHairDye.MARTIAN);
					ShowEffectMessage(3043, viewer + " provided " + m_player.player.name + " with light", MSG_C_POSITIVE);
					break;

				case "buff_treasure":
					if (m_player.HasBuff(Terraria.ID.BuffID.Spelunker, Terraria.ID.BuffID.Hunter, Terraria.ID.BuffID.Dangersense)) return EffectResult.RETRY;
					m_player.player.AddBuff(Terraria.ID.BuffID.Spelunker, 60 * m_timeBuffTreasure, true);
					m_player.player.AddBuff(Terraria.ID.BuffID.Hunter, 60 * m_timeBuffTreasure, true);
					m_player.player.AddBuff(Terraria.ID.BuffID.Dangersense, 60 * m_timeBuffTreasure, true);
					m_player.SetHairDye(CCPlayer.EHairDye.DEPTH);
					ShowEffectMessage(306, viewer + " helped " + m_player.player.name + " to search for treasure", MSG_C_POSITIVE);
					break;

				case "buff_life":
					if (m_player.HasBuff(Terraria.ID.BuffID.Lifeforce)) return EffectResult.RETRY;
                    m_player.player.AddBuff(Terraria.ID.BuffID.Lifeforce, 60 * m_timeBuffLife, true);
					m_player.player.AddBuff(Terraria.ID.BuffID.Lovestruck, 60 * m_timeLovestruck);
                    ShowEffectMessage(2345, viewer + " provided lifeforce to " + m_player.player.name, MSG_C_POSITIVE);
                    break;

				case "buff_move":
					if (m_player.HasBuff(Terraria.ID.BuffID.Swiftness, Terraria.ID.BuffID.SugarRush, Terraria.ID.BuffID.Panic)) return EffectResult.RETRY;
					m_player.player.AddBuff(Terraria.ID.BuffID.Swiftness, 60 * m_timeBuffSpeed, true);
					m_player.player.AddBuff(Terraria.ID.BuffID.SugarRush, 60 * m_timeBuffSpeed, true);
					m_player.player.AddBuff(Terraria.ID.BuffID.Panic, 60 * m_timeBuffSpeed, true);
					ShowEffectMessage(54, viewer + " boosted the movement speed of " + m_player.player.name, MSG_C_POSITIVE);
					break;

				case "inc_time":
					if (!_allowTimeChangeInBoss && ModGlobalNPC.ActiveBossEventOrInvasion(false, true)) return EffectResult.FAILURE;
					if (Main.fastForwardTime) return EffectResult.RETRY;
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) Main.fastForwardTime = true;
					else SendData(EPacketEffect.START_SUNDIAL);
					m_player.SetHairDye(CCPlayer.EHairDye.TIME);
					ShowEffectMessage(3064, viewer + " advanced time to sunrise", MSG_C_NEUTRAL);
                    break;

                case "time_noon":
					if (!_allowTimeChangeInBoss && ModGlobalNPC.ActiveBossEventOrInvasion(false, true)) return EffectResult.FAILURE;
					SetTime(27000, true);
					ShowEffectMessage(3733, viewer + " set the time to noon", MSG_C_NEUTRAL);
                    break;

                case "time_midnight":
					if (!_allowTimeChangeInBoss && ModGlobalNPC.ActiveBossEventOrInvasion(false, true)) return EffectResult.FAILURE;
					SetTime(16200, false);
					ShowEffectMessage(485, viewer + " set the time to midnight", MSG_C_NEUTRAL);
                    break;

                case "time_sunrise":
					if (!_allowTimeChangeInBoss && ModGlobalNPC.ActiveBossEventOrInvasion(false, true)) return EffectResult.FAILURE;
					SetTime(0, true);
					ShowEffectMessage(3733, viewer + " set the time to sunrise", MSG_C_NEUTRAL);
                    break;

                case "time_sunset":
					if (!_allowTimeChangeInBoss && ModGlobalNPC.ActiveBossEventOrInvasion(false, true)) return EffectResult.FAILURE;
					SetTime(0, false);
					ShowEffectMessage(485, viewer + " set the time to sunset", MSG_C_NEUTRAL);
                    break;

                case "cam_flip":
					if (m_flipCameraTimer.Enabled) return EffectResult.RETRY;
                    if (!Filters.Scene["FlipVertical"].IsActive())
                        Filters.Scene.Activate("FlipVertical").GetShader();
					ResetTimer(m_flipCameraTimer);
                    ShowEffectMessage(395, viewer + " turned the world upside down for " + m_timeFlipScreen + " seconds", MSG_C_NEGATIVE);
                    break;

				case "cam_fish":
					if (m_fishWallTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_fishWallTimer);
					m_player.player.AddBuff(Terraria.ID.BuffID.Stinky, 60 * (m_timeFishWall + 4));
					ShowEffectMessage(669, viewer + " covered the screen with fish for " + m_timeFishWall + " seconds", MSG_C_NEUTRAL);
					break;

				case "cam_darken":
					if (m_player.HasBuff(Terraria.ID.BuffID.Obstructed)) return EffectResult.RETRY;
					m_player.player.AddBuff(Terraria.ID.BuffID.Obstructed, 60 * m_timeDarkScreen, true);
					m_player.SetHairDye(CCPlayer.EHairDye.TWILIGHT);
					ShowEffectMessage(1311, viewer + " darkened the screen for " + m_timeDarkScreen +" seconds", MSG_C_NEGATIVE);
					break;

				case "cam_rainbow":
					if (!Main.hasFocus || m_rainbowScreenTimer.Enabled || m_corruptScreenTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_rainbowScreenTimer);
					ShowEffectMessage(662, viewer + " covered the screen in rainbows for " + m_timeRainbowScreen + " seconds", MSG_C_NEUTRAL);
					break;

				case "cam_corrupt":
					if (!Main.hasFocus || m_rainbowScreenTimer.Enabled || m_corruptScreenTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_corruptScreenTimer);
					ShowEffectMessage(3617, viewer + " corrupted the screen for " + m_timeCorruptScreen + " seconds", MSG_C_NEUTRAL);
					break;

				case "cam_drunk":
					if (m_drunkScreenTimer.Enabled) return EffectResult.RETRY;
					ResetTimer(m_drunkScreenTimer);
					m_player.m_oldZoom = Main.GameZoomTarget;
					m_player.player.AddBuff(Terraria.ID.BuffID.Tipsy, 60 * m_timeDrunkScreen);
					m_player.player.AddBuff(Terraria.ID.BuffID.Stinky, 60 * m_timeDrunkScreen);
					m_player.player.AddBuff(Terraria.ID.BuffID.Slimed, 60 * m_timeDrunkScreen);
					Projectile.NewProjectile(m_player.player.Center, Main.rand.NextVector2Unit() * Main.rand.Next(2, 5), Terraria.ID.ProjectileID.Ale, 1, 1f, Main.myPlayer);
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) NPCs.ModGlobalNPC.SetTownNPCMayhem(true);
					else SendData(EPacketEffect.TOWN_MAYHEM, true);
					ShowEffectMessage(353, viewer + " made " + m_player.player.name + " feel very tipsy for " + m_timeDrunkScreen + " seconds", MSG_C_NEGATIVE);
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
					ShowEffectMessage(MSG_ITEM_TIMEREND, "Movement speed is back to normal", MSG_C_TIMEREND);
					break;

				case "jumpplr":
					m_jumpPlayerTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "Jump height is back to normal", MSG_C_TIMEREND);
					break;

				case "slipplr":
					m_slipPlayerTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "Ground is no longer slippery", MSG_C_TIMEREND);
					break;

				case "item_money":
					m_incCoinsTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "Coin drops are back to normal", MSG_C_TIMEREND);
					break;

				case "plr_mana":
					m_infiniteManaTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "No longer have infinite mana", MSG_C_TIMEREND);
					break;

				case "plr_ammo":
					m_infiniteAmmoTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "No longer have infinite ammo", MSG_C_TIMEREND);
					break;

				case "tile_paint":
					m_rainbowPaintTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "No longer spreading the rainbow", MSG_C_TIMEREND);
					break;

				case "shoot_bomb":
                    m_shootBombTimer.Stop();
                    ShowEffectMessage(MSG_ITEM_TIMEREND, "No longer shooting explosives", MSG_C_TIMEREND);
                    break;

				case "shoot_grenade":
					m_shootGrenadeTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "No longer shooting grenades", MSG_C_TIMEREND);
					break;

				case "proj_item":
					m_projItemTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "Projecitle sprites are no longer randomised", MSG_C_TIMEREND);
					break;

				case "inc_spawnrate":
                    m_increasedSpawnsTimer.Stop();
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) m_player.m_spawnRate = 1f;
					else SendData(EPacketEffect.SET_SPAWNRATE, 1f);
                    ShowEffectMessage(MSG_ITEM_TIMEREND, "Spawnrate is back to normal", MSG_C_TIMEREND);
					break;

				case "buff_invis":
					m_invisTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "Player is no longer invisible", MSG_C_TIMEREND);
					break;

                case "cam_flip":
                    if (Filters.Scene["FlipVertical"].IsActive())
                        Filters.Scene.Deactivate("FlipVertical");
                    m_flipCameraTimer.Stop();
                    ShowEffectMessage(MSG_ITEM_TIMEREND, "World is no longer flipped", MSG_C_TIMEREND);
                    break;

				case "cam_fish":
					m_fishWallTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "Fish is no longer covering the screen", MSG_C_TIMEREND);
					break;

				case "cam_rainbow":
					m_rainbowScreenTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "The screen is no longer covered in rainbows", MSG_C_TIMEREND);
					break;

				case "cam_corrupt":
					m_corruptScreenTimer.Stop();
					ShowEffectMessage(MSG_ITEM_TIMEREND, "The screen is no longer corrupted", MSG_C_TIMEREND);
					break;

				case "cam_drunk":
					if (Filters.Scene["Sine"].IsActive()) Filters.Scene.Deactivate("Sine");
					if (Filters.Scene["Glitch"].IsActive()) Filters.Scene.Deactivate("Glitch");
					Main.GameZoomTarget = m_player.m_oldZoom;
					m_drunkScreenTimer.Stop();
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) NPCs.ModGlobalNPC.SetTownNPCMayhem(false);
					else SendData(EPacketEffect.TOWN_MAYHEM, false);
					ShowEffectMessage(MSG_ITEM_TIMEREND, "No longer drunk", MSG_C_TIMEREND);
					break;
			}

            return EffectResult.SUCCESS;
        }

		// Attempt to show an effect message in chat
		private void ShowEffectMessage(int itemType, string text, Color colour)
        {
			if (_showEffectMessages)
				TDebug.WriteMessage(itemType, text, colour);
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
                ShowEffectMessage(droppedItem.type, viewer + " caused " + m_player.player.name + " to fumble and drop " + droppedItem.stack + " " + droppedItem.Name + "s", MSG_C_NEGATIVE);
            else
                ShowEffectMessage(droppedItem.type, viewer + " caused " + m_player.player.name + " to fumble and drop their " + droppedItem.Name, MSG_C_NEGATIVE);

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
				string prefixName = Lang.prefix[newItem.prefix].Value;
				if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
					NetMessage.SendData(Terraria.ID.MessageID.SyncEquipment, -1, -1, null, Main.myPlayer, m_player.player.selectedItem, newItem.stack, newItem.prefix, newItem.netID);
				ShowEffectMessage(affectedItem.type, viewer + " changed " + m_player.player.name + "'s " + affectedItem.Name + " to be " + prefixName, MSG_C_NEUTRAL);
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
			m_player.m_petID = id;
			ShowEffectMessage(1927, viewer + " provided " + m_player.player.name + " with a " + Lang.GetBuffName(id), MSG_C_POSITIVE);
		}

		// Give the player a random light pet
		private void Effect_GiveLightPet(string viewer)
        {
			if (!m_player.player.hideMisc[1]) m_player.player.ClearBuff(m_player.player.miscEquips[1].buffType);
			m_player.player.hideMisc[1] = true;
			int id;
			do { id = m_lightPets[Main.rand.Next(m_lightPets.Length)]; } while (m_prevLightPets.Contains(id));
			m_prevLightPets.Add(id);
			m_player.player.AddBuff(id, 1);
			m_player.m_lightPetID = id;
			ShowEffectMessage(1183, viewer + " provided " + m_player.player.name + " with a " + Lang.GetBuffName(id), MSG_C_POSITIVE);
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
                    ShowEffectMessage(0, viewer + " generated a deep chasm below " + player.name, MSG_C_NEUTRAL);
                    WorldGen.ChasmRunner(x, y, Main.rand.Next(12, 24), false);
                }
                else if (y > Main.worldSurface && player.ZoneJungle)
                {
                    ShowEffectMessage(0, viewer + " generated a bee hive surrounding " + player.name, MSG_C_NEUTRAL);
                    WorldGen.Hive(x, y);
                }
                else if (player.ZoneUnderworldHeight)
                {
                    ShowEffectMessage(0, viewer + " generated a hell fortress around " + player.name, MSG_C_NEUTRAL);
                    WorldGen.HellFort(x, y);
                }
                else if (y < Main.worldSurface - 220)
                {
					ShowEffectMessage(0, viewer + " generated a sky island house around " + player.name, MSG_C_NEUTRAL);
					WorldGen.IslandHouse(x, y);
				}
                else if (y > Main.worldSurface)
                {
                    ShowEffectMessage(0, viewer + " generated an abandoned house around " + player.name, MSG_C_NEUTRAL);
                    WorldGen.MineHouse(x, y);
                }
                else
                {
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer)
					{
						ShowEffectMessage(0, viewer + " generated a huge living tree around " + player.name, MSG_C_NEUTRAL);
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
						ShowEffectMessage(0, viewer + " generated an abandoned house around " + player.name, MSG_C_NEUTRAL);
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
			int px = (int)m_player.player.Center.X;
			int py = (int)m_player.player.Center.Y;

            if (Main.rand.Next(0, 100) < 5)
            {
				type = WorldGen.crimson ? Terraria.ID.NPCID.CrimsonBunny : Terraria.ID.NPCID.CorruptBunny;
				if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) NPC.NewNPC(px, py, type);
				else SendData(EPacketEffect.SPAWN_NPC, type, px, py);
				ShowEffectMessage(1338, viewer + " spawned an Evil Bunny", MSG_C_NEGATIVE);
            }
            else if (Main.rand.Next(0, 100) < 5)
            {
				type = Terraria.ID.NPCID.GoldBunny;
				if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) NPC.NewNPC(px, py, type);
				else SendData(EPacketEffect.SPAWN_NPC, type, px, py);
                ShowEffectMessage(2890, viewer + " spawned a Gold Bunny", MSG_C_POSITIVE);
            }
            else
            {
				short[] normalBunnTypes = new short[] { Terraria.ID.NPCID.Bunny, Terraria.ID.NPCID.BunnySlimed, Terraria.ID.NPCID.BunnyXmas, Terraria.ID.NPCID.PartyBunny };
				if (Main.rand.Next(100) < 5)
                {
					for (int i = 0; i < 10; i++)
					{
						if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) NPC.NewNPC(px, py, Main.rand.Next(normalBunnTypes));
						else SendData(EPacketEffect.SPAWN_NPC, Main.rand.Next(normalBunnTypes), px, py);
					}
					ShowEffectMessage(2019, viewer + " spawned lots of Bunnies", MSG_C_NEUTRAL);
				}
				else
                {
					if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) NPC.NewNPC(px, py, Main.rand.Next(normalBunnTypes));
					else SendData(EPacketEffect.SPAWN_NPC, Main.rand.Next(normalBunnTypes), px, py);
					ShowEffectMessage(2019, viewer + " spawned a Bunny", MSG_C_NEUTRAL);
				}
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
			if (x >= 0 && x < Main.maxTilesX && y >= 0 && y < Main.maxTilesY && Main.tile[x, y] != null && !Main.tile[x, y].active())
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

		// Check if dungeon guardians should be despawned (call once per tick)
		public void CheckDungeonGuardians()
        {
			for (int i = 0; i < m_guardians.Count; ++i)
            {
				int guardianID = m_guardians[i].Item1;
				int timeLeft = m_guardians[i].Item2;

				// Check if valid npc
				if (Main.npc[guardianID] == null || !Main.npc[guardianID].active || Main.npc[guardianID].type != Terraria.ID.NPCID.DungeonGuardian)
                {
					m_guardians.RemoveAt(i);
					--i;
					TDebug.WriteDebug("Removed guardian due to being an invalid NPC", Color.Yellow);
					continue;
                }

				--timeLeft;

				// Check if time expired
				if (timeLeft <= 0)
				{
					Main.npc[guardianID].ai[1] = 3f;
					if (Main.netMode == Terraria.ID.NetmodeID.Server)
						NetMessage.SendData(Terraria.ID.MessageID.SyncNPC, -1, -1, null, guardianID);

					m_guardians.RemoveAt(i);
					--i;
					TDebug.WriteDebug("Removed guardian due to time expiring", Color.Yellow);
					continue;
				}

				m_guardians[i] = new Tuple<int, int>(guardianID, timeLeft);
            }
        }

		// Check progression of the world
		public EProgression CheckProgression()
        {
			if (NPC.downedMoonlord)
				return EProgression.END_GAME;
			else if (NPC.downedAncientCultist)
				return EProgression.PRE_MOON_LORD;
			else if (NPC.downedGolemBoss)
				return EProgression.PRE_LUNAR;
			else if (NPC.downedMechBossAny)
				return EProgression.PRE_GOLEM;
			else if (Main.hardMode)
				return EProgression.PRE_MECH;
			else if (NPC.downedBoss3)
				return EProgression.PRE_WOF;
			else if (NPC.downedBoss1)
				return EProgression.PRE_SKELETRON;
			else
				return EProgression.PRE_EYE;
        }

		// Choose an options based on the progression of the world
		public T ChoosePerProgression<T>(T preEye, T preSkeletron, T preWOF, T preMech, T preGolem, T preLunar, T preMoonLord, T postGame)
        {
			switch (CheckProgression())
            {
				case EProgression.PRE_EYE:
					return preEye;
				case EProgression.PRE_SKELETRON:
					return preSkeletron;
				case EProgression.PRE_WOF:
					return preWOF;
				case EProgression.PRE_MECH:
					return preMech;
				case EProgression.PRE_GOLEM:
					return preGolem;
				case EProgression.PRE_LUNAR:
					return preLunar;
				case EProgression.PRE_MOON_LORD:
					return preMoonLord;
				default:
					return postGame;
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

		// Route an incoming packet to the correct handler
		public void RouteIncomingPacket(BinaryReader reader)
        {
			if (Main.dedServ)
				HandleData((EPacketEffect)reader.ReadByte(), reader.ReadInt32(), reader);
        }

		// Handle a modded effect packet (as server)
		private void HandleData(EPacketEffect packetEffect, int sender, BinaryReader reader)
		{
			string debugText = "Server received packet type: " + packetEffect + " (";
			int x, y;
			switch (packetEffect)
			{
				case EPacketEffect.CC_CONNECT:
					NetMessage.BroadcastChatMessage(Terraria.Localization.NetworkText.FromLiteral("[i:1525] " + Main.player[sender].name + " has connected to Crowd Control"), Color.Green, sender);
					break;
				case EPacketEffect.EFFECT_MESSAGE:
					string effectCode = reader.ReadString();
					int preItemID = 0;
					string effectMessage = "";
					Color effectColour = default;
					if (effectCode == "shoot_bomb")
                    {
						preItemID = Terraria.ID.ItemID.Bomb;
						effectMessage = Main.player[sender].name + " is dropping bombs";
						effectColour = MSG_C_NEGATIVE;
                    }
					else if (effectCode == "inc_spawnrate")
                    {
						preItemID = Terraria.ID.ItemID.WaterCandle;
						effectMessage = "Enemy spawnrates are increased around " + Main.player[sender].name;
						effectColour = MSG_C_NEUTRAL;
                    }
					NetMessage.BroadcastChatMessage(Terraria.Localization.NetworkText.FromLiteral("[i:" + preItemID.ToString() + "] " + effectMessage), effectColour, sender);
					break;
				case EPacketEffect.SPAWN_NPC:
					int type = reader.ReadInt16();
					 x = reader.ReadInt32();
					 y = reader.ReadInt32();
					int id = NPC.NewNPC(x, y, type);
					if (type == Terraria.ID.NPCID.DungeonGuardian) m_guardians.Add(new Tuple<int, int>(id, m_guardianSurvivalTime));
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
				case EPacketEffect.START_SUNDIAL:
					Main.fastForwardTime = true;
					NetMessage.SendData(Terraria.ID.MessageID.WorldData, -1, -1, null);
					break;
				case EPacketEffect.SEND_CONFIG:
					bool disableTombstones = reader.ReadBoolean();
					Main.player[sender].GetModPlayer<CCPlayer>().m_servDisableTombstones = disableTombstones;
					debugText += disableTombstones;
					break;
				case EPacketEffect.TOWN_MAYHEM:
					bool mayhemEnabled = reader.ReadBoolean();
					NPCs.ModGlobalNPC.SetTownNPCMayhem(mayhemEnabled);
					debugText += mayhemEnabled;
					break;
			}

			TDebug.WriteDebug(debugText + ") from " + Main.player[sender].name, Color.Yellow);
		}

		// Attempt to write data to a packet
		private void WriteToPacket(ModPacket packet, object data)
		{
			if (data is bool _bool) packet.Write(_bool);
			else if (data is byte _byte) packet.Write(_byte);
			else if (data is byte[] _byteA) packet.Write(_byteA);
			else if (data is int _int) packet.Write(_int);
			else if (data is float _float) packet.Write(_float);
			else if (data is string _string) packet.Write(_string);
			else if (data is char _char) packet.Write((_char));
			else if (data is short _short) packet.Write(_short);
			else packet.Write(0);
		}

		// Send config to server
		public void SendConfigToServer()
        {
			if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
				SendData(EPacketEffect.SEND_CONFIG, _disableTombstones);
        }

		private T Choose<T>(params T[] options) { return options[Main.rand.Next(options.Length)]; }

		// Set the ModPlayer instance affected by Crowd Control Effects (note that the mod should be used in Singleplayer)
		public void SetPlayer(CCPlayer player)
        {
            m_player = player;
			SendConfigToServer();
            TDebug.WriteDebug("Setting player to " + m_player.player.name, Color.Yellow);
        }

        // Get the ModPlayer instance affected by Crowd Control Effects
        public CCPlayer GetPlayer()
        {
            return m_player;
        }
    }
}
