///<summary>
/// File: CCPlayer.cs
/// Last Updated: 2020-07-19
/// Author: MRG-bit
/// Description: Modded player file
///</summary>

using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using System.Collections.Generic;

namespace CrowdControlMod
{
    public class CCPlayer : ModPlayer
    {
        private readonly static HashSet<short> m_explosives =       // Explosives that can be shot
            new HashSet<short>()
        {
            ProjectileID.Grenade,
            ProjectileID.StickyGrenade,
            ProjectileID.BouncyGrenade,
            ProjectileID.Beenade,
            ProjectileID.PartyGirlGrenade,
            ProjectileID.Bomb,
            ProjectileID.StickyBomb,
            ProjectileID.BouncyBomb,
            ProjectileID.Dynamite,
            ProjectileID.BouncyDynamite,
            ProjectileID.RocketIII
        };
        private bool serverStartedViaControls = false;              // Whether server was started when joining a multiplayer server
        public float m_spawnRate = 1f;                              // NPC spawnrate for this player
        public bool m_servSpeed = false;                            // Whether player movement speed is increased (server-side)
        public bool m_servJump = false;                             // Whether player jump boost is increased (server-side)

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

        // Called before the player creates a projectile
        public override bool Shoot(Item item, ref Vector2 position, ref float speedX, ref float speedY, ref int type, ref int damage, ref float knockBack)
        {
            if (Main.myPlayer == player.whoAmI)
            {
                // Shoot an explosive instead of the intended projectile
                if (CrowdControlMod._server.m_shootBombTimer.Enabled && !m_explosives.Contains((short)item.shoot))
                {
                    int r = Main.rand.Next(100);
                    short id;
                    if (r < 3) id = ProjectileID.BouncyDynamite;
                    else if (r < 6) id = ProjectileID.Dynamite;
                    else if (r < 9) id = ProjectileID.BouncyBomb;
                    else if (r < 12) id = ProjectileID.StickyBomb;
                    else if (r < 15) id = ProjectileID.Bomb;
                    else if (r < 30) id = ProjectileID.Beenade;
                    else if (r < 45) id = ProjectileID.StickyGrenade;
                    else if (r < 60) id = ProjectileID.BouncyGrenade;
                    else if (r < 75) id = ProjectileID.PartyGirlGrenade;
                    else if (r < 87) id = ProjectileID.RocketIII;
                    else id = ProjectileID.Grenade;
                    
                    Projectile.NewProjectile(position, new Vector2(speedX, speedY), id, damage, knockBack, Main.myPlayer);
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
