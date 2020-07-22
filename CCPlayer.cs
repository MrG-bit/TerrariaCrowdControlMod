///<summary>
/// File: CCPlayer.cs
/// Last Updated: 2020-07-21
/// Author: MRG-bit
/// Description: Modded player file
///</summary>

using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using System.Collections.Generic;
using Terraria.DataStructures;

namespace CrowdControlMod
{
    public class CCPlayer : ModPlayer
    {
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

        // Called at the end of each player update
        public override void PostUpdate()
        {
            base.PostUpdate();

            if (Main.myPlayer == player.whoAmI)
            {
                // Rainbow-ify the tiles below the player
                if (CrowdControlMod._server != null && CrowdControlMod._server.m_rainbowPaintTimer.Enabled && player.velocity.Y == 0f)
                {
                    int x = (int)(player.position.X / 16);
                    int y = (int)(player.position.Y / 16);

                    CrowdControlMod._server.RainbowifyTileClient(x, y + 3);
                    CrowdControlMod._server.RainbowifyTileClient(x + 1, y + 3);
                }

                // Spawn bombs periodically
                if (CrowdControlMod._server != null && CrowdControlMod._server.m_shootBombTimer.Enabled)
                {
                    m_explosiveDelay -= 1;
                    if (m_explosiveDelay <= 0)
                    {
                        m_explosiveDelay = Main.rand.Next(m_minExplosiveDelay, m_maxExplosiveDelay);
                        Projectile.NewProjectile(player.Center, Main.rand.NextVector2Unit() * Main.rand.NextFloat(m_minExplosiveSpd, m_maxExplosiveSpd), Main.rand.Next(m_explosives), 10, 1f, Main.myPlayer);
                    }
                }

                // Spawn grenades periodically
                if (CrowdControlMod._server != null && CrowdControlMod._server.m_shootGrenadeTimer.Enabled)
                {
                    m_grenadeDelay -= 1;
                    if (m_grenadeDelay <= 0)
                    {
                        m_grenadeDelay = Main.rand.Next(m_minGrenadeDelay, m_maxGrenadeDelay);
                        Projectile.NewProjectile(player.Center, Main.rand.NextVector2Unit() * Main.rand.NextFloat(m_minExplosiveSpd, m_maxExplosiveSpd), Main.rand.Next(m_grenades), 10, 1f, Main.myPlayer);
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
                TDebug.WriteDebug("Reduced respawn timer to " + player.respawnTimer, Color.Yellow);
            }

            base.Kill(damage, hitDirection, pvp, damageSource);
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
    }
}
