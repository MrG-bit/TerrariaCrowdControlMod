///<summary>
/// File: CCPlayer.cs
/// Last Updated: 2020-07-24
/// Author: MRG-bit
/// Description: Modded player file
///</summary>

using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.Graphics.Effects;
using System;
using System.Text;

namespace CrowdControlMod
{
    public class CCPlayer : ModPlayer
    {
        public enum EHairDye : int
        {
            NONE = ItemID.HairDyeRemover,
            LIFE = ItemID.LifeHairDye,
            MANA = ItemID.ManaHairDye,
            DEPTH = ItemID.DepthHairDye,
            MONEY = ItemID.MoneyHairDye,
            TIME = ItemID.TimeHairDye,
            TEAM = ItemID.TeamHairDye,
            BIOME = ItemID.BiomeHairDye,
            PARTY = ItemID.PartyHairDye,
            RAINBOW = ItemID.RainbowHairDye,
            SPEED = ItemID.SpeedHairDye,
            MARTIAN = ItemID.MartianHairDye,
            TWILIGHT = ItemID.TwilightHairDye
        }

        private readonly static short[] m_explosives =              // Explosives that can be shot
        {
            ProjectileID.Bomb,
            ProjectileID.StickyBomb,
            ProjectileID.BouncyBomb,
            ProjectileID.Dynamite,
            ProjectileID.BouncyDynamite,
        };
        private readonly static short[] m_grenades =                // Grenades that can be shot
        {
            ProjectileID.Grenade,
            ProjectileID.StickyGrenade,
            ProjectileID.BouncyGrenade,
            ProjectileID.Beenade,
            ProjectileID.PartyGirlGrenade
        };
        private readonly static int m_minExplosiveDelay = 60;
        private readonly static int m_maxExplosiveDelay = 80;
        private readonly static int m_minGrenadeDelay = 15;
        private readonly static int m_maxGrenadeDelay = 40;
        private readonly static float m_minExplosiveSpd = 5f;
        private readonly static float m_maxExplosiveSpd = 12f;
        private int m_explosiveDelay = 0;
        private int m_grenadeDelay = 0;
        private bool serverStartedViaControls = false;              // Whether server was started when joining a multiplayer server
        public float m_spawnRate = 1f;                              // NPC spawnrate for this player
        public bool m_servSpeed = false;                            // Whether player movement speed is increased (server-side)
        public bool m_servJump = false;                             // Whether player jump boost is increased (server-side)
        public bool m_reduceRespawn = false;                        // Reduce respawn cooldown when the player is killed (then set to false)
        private readonly int m_reducedCooldown = 200;               // Reduced respawn cooldown if reduceRespawn is true
        public int m_petID = -1;                                    // ID for the pet buff that should be activated when the player respawns
        private Vector2 m_deathPoint = Vector2.Zero;                // Player previous death point
        public float m_oldZoom = -1f;                               // Old zoom
        public bool m_servDisableTombstones = false;                // Whether to disable tombstones for this player (used by server)
        public bool m_ignoreImmuneConfusion = false;                // Whether the player is to ignore immunity to confusion whilst the debuff is active
        public bool m_ignoreImmuneFrozen = false;                   // Whether the player is to ignore immunity to frozen whilst the debuff is active

        // Called when the player enters a world
        public override void OnEnterWorld(Player player)
        {
            base.OnEnterWorld(player);

            // Set the player and start the server to begin connecting to Crowd Control
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                CrowdControlMod._server.SetPlayer(this);
                CrowdControlMod._server.Start();
            }

            m_deathPoint = Vector2.Zero;
        }

        // Called when the player disconnects from a server
        public override void PlayerDisconnect(Player player)
        {
            if (Main.myPlayer == player.whoAmI)
            {
                serverStartedViaControls = false;
                CrowdControlMod._server.Stop();
            }

            base.PlayerDisconnect(player);
        }

        // Called when setting controls
        public override void SetControls()
        {
            // Set the player and start the server to begin connecting to Crowd Control
            if (Main.netMode == NetmodeID.MultiplayerClient && Main.myPlayer == player.whoAmI  && !serverStartedViaControls)
            {
                serverStartedViaControls = true;
                CrowdControlMod._server.SetPlayer(this);
                CrowdControlMod._server.Start();
            }

            base.SetControls();
        }

        // Modify how the player is drawn
        public override void ModifyDrawInfo(ref PlayerDrawInfo drawInfo)
        {
            // Make the player invisible by drawing the player somewhere else
            if (CrowdControlMod._server != null && CrowdControlMod._server.IsRunning && CrowdControlMod._server.m_invisTimer.Enabled)
                drawInfo.position = Vector2.Zero;

            base.ModifyDrawInfo(ref drawInfo);
        }

        // Called after updating accessories
        public override void UpdateVanityAccessories()
        {
            CheckImmunities();

            base.UpdateVanityAccessories();
        }

        // Called at the end of each player update
        public override void PostUpdate()
        {
            base.PostUpdate();

            if (Main.myPlayer == player.whoAmI)
            {
                if (CrowdControlMod._server != null)
                {
                    // Drunk shader
                    if (CrowdControlMod._server.m_drunkScreenTimer.Enabled)
                    {
                        float drunkSineIntensity = CrowdControlMod._server.m_drunkGlitchIntensity;
                        float drunkGlitchIntensity = CrowdControlMod._server.m_drunkGlitchIntensity;

                        if (!Filters.Scene["Sine"].IsActive())
                            Filters.Scene.Activate("Sine", player.Center).GetShader().UseIntensity(drunkSineIntensity);
                        else
                            Filters.Scene["Sine"].GetShader().UseIntensity(drunkSineIntensity).UseTargetPosition(player.Center);

                        if (!Filters.Scene["Glitch"].IsActive())
                            Filters.Scene.Activate("Glitch", player.Center).GetShader().UseIntensity(drunkGlitchIntensity);
                        else
                            Filters.Scene["Glitch"].GetShader().UseIntensity(drunkGlitchIntensity).UseTargetPosition(player.Center);

                        Main.GameZoomTarget = 1.2f + ((float)Math.Sin(Main.GlobalTime * 2.2f) * 0.2f);
                    }

                    // Infinite mana
                    if (CrowdControlMod._server.m_infiniteManaTimer.Enabled)
                        player.statMana = player.statManaMax2;

                    // Rainbow-ify the tiles below the player
                    if (CrowdControlMod._server.m_rainbowPaintTimer.Enabled && player.velocity.Y == 0f)
                    {
                        int x = (int)(player.position.X / 16);
                        int y = (int)(player.position.Y / 16);

                        CrowdControlMod._server.RainbowifyTileClient(x, y + 3);
                        CrowdControlMod._server.RainbowifyTileClient(x + 1, y + 3);
                    }

                    // Spawn bombs periodically
                    if (CrowdControlMod._server.m_shootBombTimer.Enabled)
                    {
                        m_explosiveDelay -= 1;
                        if (m_explosiveDelay <= 0)
                        {
                            m_explosiveDelay = Main.rand.Next(m_minExplosiveDelay, m_maxExplosiveDelay);
                            Projectile.NewProjectile(player.Center, Main.rand.NextVector2Unit() * Main.rand.NextFloat(m_minExplosiveSpd, m_maxExplosiveSpd), Main.rand.Next(m_explosives), 10, 1f, Main.myPlayer);
                        }
                    }

                    // Spawn grenades periodically
                    if (CrowdControlMod._server.m_shootGrenadeTimer.Enabled)
                    {
                        m_grenadeDelay -= 1;
                        if (m_grenadeDelay <= 0)
                        {
                            m_grenadeDelay = Main.rand.Next(m_minGrenadeDelay, m_maxGrenadeDelay);
                            Projectile.NewProjectile(player.Center, Main.rand.NextVector2Unit() * Main.rand.NextFloat(m_minExplosiveSpd, m_maxExplosiveSpd), Main.rand.Next(m_grenades), 10, 1f, Main.myPlayer);
                        }
                    }
                }

                // Manually start / stop the server if testing
                if (TDebug.IN_DEBUG && player.selectedItem == 9 && player.justJumped)
                {
                    if (CrowdControlMod._server.IsRunning)
                    {
                        TDebug.WriteDebug("Manually stopping server", Color.Yellow);
                        CrowdControlMod._server.Stop();
                    }
                    else
                    {
                        TDebug.WriteDebug("Manually starting server", Color.Yellow);
                        CrowdControlMod._server.Start();
                    }
                }
            }
        }

        // Called to change running speed
        public override void PostUpdateRunSpeeds()
        {
            base.PostUpdateRunSpeeds();

            if (CrowdControlMod._server != null)
            {
                // Make player run faster
                if ((CrowdControlMod._server.m_fastPlayerTimer.Enabled && Main.myPlayer == player.whoAmI) || m_servSpeed)
                {
                    bool aboveSurface = (int)(player.position.Y / 16) < Main.worldSurface;
                    player.maxRunSpeed = aboveSurface ? CrowdControlMod._server.m_fastPlrMaxSurfSpeed : CrowdControlMod._server.m_fastPlrMaxCaveSpeed;
                    player.runAcceleration = aboveSurface ? CrowdControlMod._server.m_fastPlrSurfAccel : CrowdControlMod._server.m_fastPlrCaveAccel;
                }

                // Make the ground slippery
                if (CrowdControlMod._server.m_slipPlayerTimer.Enabled && IsGrounded())
                {
                    player.runAcceleration *= CrowdControlMod._server.m_slipPlrAccel;
                    player.runSlowdown = 0f;
                }

                // Make player jump higher
                if ((CrowdControlMod._server.m_jumpPlayerTimer.Enabled && Main.myPlayer == player.whoAmI) || m_servJump)
                {
                    Player.jumpHeight = CrowdControlMod._server.m_jumpPlrHeight;
                    Player.jumpSpeed = CrowdControlMod._server.m_jumpPlrSpeed;
                }
            }
        }

        // Called when the player respawns
        public override void OnRespawn(Player player)
        {
            // Respawn pet
            if (m_petID >= 0)
            {
                if (!player.hideMisc[0])
                    m_petID = -1;
                else
                    player.AddBuff(m_petID, 1);
            }

            base.OnRespawn(player);
        }

        // Called before the player creates a projectile
        public override bool Shoot(Item item, ref Vector2 position, ref float speedX, ref float speedY, ref int type, ref int damage, ref float knockBack)
        {
            if (Main.myPlayer == player.whoAmI)
            {
                // Shoot a grenade instead of the intended projectile
                if (CrowdControlMod._server.m_shootGrenadeTimer.Enabled)
                {
                    Projectile.NewProjectile(position, new Vector2(speedX, speedY), Main.rand.Next(m_grenades), damage, knockBack, Main.myPlayer);
                    return false;
                }

                // Flip shoot direction if screen is flipped
                if (CrowdControlMod._server.m_flipCameraTimer.Enabled)
                {
                    speedY *= -1f;
                }
            }

            return base.Shoot(item, ref position, ref speedX, ref speedY, ref type, ref damage, ref knockBack);
        }

        // Determine if ammo is consumed
        public override bool ConsumeAmmo(Item weapon, Item ammo)
        {
            if (Main.myPlayer == player.whoAmI && CrowdControlMod._server.m_shootBombTimer.Enabled)
                return false;
            if (Main.myPlayer == player.whoAmI && CrowdControlMod._server.m_infiniteAmmoTimer.Enabled)
                return false;
            return base.ConsumeAmmo(weapon, ammo);
        }

        // Called when the player is killed
        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            // Reduce respawn timer
            if (m_reduceRespawn)
            {
                player.respawnTimer = m_reducedCooldown;
                m_reduceRespawn = false;
            }
            else
                player.respawnTimer = (int)(player.respawnTimer *CCServer._respawnTimeFactor);

            UpdateDeathPoint();

            base.Kill(damage, hitDirection, pvp, damageSource);
        }

        // Set the death point if far from spawn
        private void UpdateDeathPoint()
        {
            m_deathPoint = player.position;
            TDebug.WriteDebug("Saved death position: " + m_deathPoint, Color.Yellow);
        }

        // Teleport the player to the previous death point
        public bool TeleportToDeathPoint()
        {
            if (m_deathPoint == Vector2.Zero)
                return false;
            player.Teleport(m_deathPoint, 1);
            return true;
        }

        // Give the player coins (extracted from the Terraria source code)
        public void GiveCoins(int money)
        {
            int num13 = money;
            while (num13 > 0)
            {
                if (num13 > 1000000)
                {
                    int num12 = num13 / 1000000;
                    num13 -= 1000000 * num12;
                    int number7 = Item.NewItem((int)player.position.X, (int)player.position.Y, player.width, player.height, 74, num12);
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, number7, 1f);
                    }
                    continue;
                }
                if (num13 > 10000)
                {
                    int num11 = num13 / 10000;
                    num13 -= 10000 * num11;
                    int number6 = Item.NewItem((int)player.position.X, (int)player.position.Y, player.width, player.height, 73, num11);
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, number6, 1f);
                    }
                    continue;
                }
                if (num13 > 100)
                {
                    int num10 = num13 / 100;
                    num13 -= 100 * num10;
                    int number5 = Item.NewItem((int)player.position.X, (int)player.position.Y, player.width, player.height, 72, num10);
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, number5, 1f);
                    }
                    continue;
                }
                int num9 = num13;
                if (num9 < 1)
                {
                    num9 = 1;
                }
                num13 -= num9;
                int number4 = Item.NewItem((int)player.position.X, (int)player.position.Y, player.width, player.height, 71, num9);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, number4, 1f);
                }
            }
        }

        // Check if the player is on the ground
        public bool IsGrounded()
        {
            int px = (int)(player.position.X / 16);
            int py = (int)(player.position.Y / 16);
            return Main.tileSolid[Main.tile[px, py + 4].type] && player.velocity.Y == 0f;
        }

        // Check if the player the given buffs active
        public bool HasBuff(params int[] buffs)
        {
            for (int i = 0; i < buffs.Length; ++i)
                if (!player.HasBuff(buffs[i]))
                    return false;
            return true;
        }

        // Set the player's hair dye
        public void SetHairDye(EHairDye hairDye)
        {
            if (CCServer.m_disableHairDye) return;

            Item item = new Item();
            item.SetDefaults((int)hairDye);
            player.hairDye = (byte)item.hairDye;
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SyncPlayer, -1, -1, null, player.whoAmI);
        }

        // Check if should ignore immunities
        private void CheckImmunities()
        {
            // Ignore immunity to confusion
            if (m_ignoreImmuneConfusion)
            {
                if (HasBuff(BuffID.Confused))
                    player.buffImmune[BuffID.Confused] = false;
                else
                    m_ignoreImmuneConfusion = false;
            }

            // Ignore immunity to frozen
            if (m_ignoreImmuneFrozen)
            {
                if (HasBuff(BuffID.Frozen))
                    player.buffImmune[BuffID.Frozen] = false;
                else
                    m_ignoreImmuneFrozen = false;
            }
        }

        // Check if the player has a buff, even if immune
        private bool HasBuff(int type)
        {
            for (int i = 0; i < Player.MaxBuffs; i++)
                if (player.buffTime[i] >= 1 && player.buffType[i] == type)
                    return true;
            return false;
        }
    }
}
