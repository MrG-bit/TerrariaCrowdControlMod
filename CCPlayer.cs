///<summary>
/// File: CCPlayer.cs
/// Last Updated: 2020-08-08
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
using Terraria.ModLoader.IO;
using System.Collections.Generic;

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
        };          // List of explosives that can be spawned
        private readonly static short[] m_grenades =                // Grenades that can be shot
        {
            ProjectileID.Grenade,
            ProjectileID.StickyGrenade,
            ProjectileID.BouncyGrenade,
            ProjectileID.Beenade,
            ProjectileID.PartyGirlGrenade,
            ProjectileID.SmokeBomb,
            ProjectileID.HappyBomb,
            ProjectileID.ConfettiGun,
            ProjectileID.FlowerPetal,
            ProjectileID.OrnamentFriendly,
            ProjectileID.ToxicFlask,
            589, // Friendly santa projectiles
            ProjectileID.DD2GoblinBomb
        };            // List of grenades that can be spawned
        private readonly static int m_minExplosiveDelay = 60;       // Min / max delay between bombs spawning from the player
        private readonly static int m_maxExplosiveDelay = 80;
        private readonly static int m_minGrenadeDelay = 15;         // Min / max delay between grenades spawning from the player
        private readonly static int m_maxGrenadeDelay = 40;
        private readonly static float m_minExplosiveSpd = 5f;       // Min / max speed of explosives (bombs / grenades) spawned
        private readonly static float m_maxExplosiveSpd = 12f;
        private int m_explosiveDelay = 0;                           // Delay between explosives spawning from the player
        private int m_grenadeDelay = 0;                             // Delay between grenades spawning from the player
        private bool threadStartedInMultiplayer = false;              // Whether server was started when joining a multiplayer server
        public float m_spawnRate = 1f;                              // NPC spawnrate for this player
        public bool m_reduceRespawn = false;                        // Reduce respawn cooldown when the player is killed (then set to false)
        private readonly int m_reducedCooldown = 200;               // Reduced respawn cooldown if reduceRespawn is true
        public int m_petID = -1;                                    // ID for the pet buff that should be activated when the player respawns
        public int m_lightPetID = -1;                               // ID for the light pet buff
        private Vector2 m_deathPoint = Vector2.Zero;                // Player previous death point
        public float m_oldZoom = -1f;                               // Old zoom amount
        public bool m_servDisableTombstones = false;                // Whether to disable tombstones for this player (used by server)
        public bool m_ignoreImmuneConfusion = false;                // Whether the player is to ignore immunity to confusion whilst the debuff is active
        public bool m_ignoreImmuneFrozen = false;                   // Whether the player is to ignore immunity to frozen whilst the debuff is active
        public Dictionary<int, BuffEffect> m_buffEffects = new Dictionary<int, BuffEffect>();

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
                if (threadStartedInMultiplayer)
                    threadStartedInMultiplayer = false;
                CrowdControlMod._server.Stop();
            }

            base.PlayerDisconnect(player);
        }

        // Called when the player is saved
        public override TagCompound Save()
        {
            if (Main.myPlayer == player.whoAmI && (Main.netMode == NetmodeID.SinglePlayer && threadStartedInMultiplayer))
            {
                if (threadStartedInMultiplayer)
                    threadStartedInMultiplayer = false;
                CrowdControlMod._server.Stop();
                TDebug.WriteDebug("Server stopped due to player save", Color.Yellow);
            }
            else
            {
                TDebug.WriteDebug("Server not stopped in Save() ignored due to not exiting to menu.", Color.Yellow);
            }
            return base.Save();
        }

        // Called when setting controls
        public override void SetControls()
        {
            // Set the player and start the server to begin connecting to Crowd Control
            if (Main.netMode == NetmodeID.MultiplayerClient && Main.myPlayer == player.whoAmI  && !threadStartedInMultiplayer)
            {
                threadStartedInMultiplayer = true;
                CrowdControlMod._server.SetPlayer(this);
                CrowdControlMod._server.Start();
                TDebug.WriteDebug("Server started through SetControls", Color.Yellow);
            }

            base.SetControls();
        }

        // Modify how the player is drawn
        public override void ModifyDrawInfo(ref PlayerDrawInfo drawInfo)
        {
            // Make the player invisible by drawing the player somewhere else
            if (CrowdControlMod._server != null && CrowdControlMod._server.IsRunning && Main.myPlayer == player.whoAmI && CrowdControlMod._server.m_invisTimer.Enabled)
                drawInfo.position = Vector2.Zero;

            base.ModifyDrawInfo(ref drawInfo);
        }

        // Called after updating accessories
        public override void UpdateVanityAccessories()
        {
            CheckImmunities();

            if (CrowdControlMod._server != null)
            {
                // Increase mining speed
                if (CrowdControlMod._server.m_incMiningTimer.Enabled)
                {
                    player.pickSpeed = 0f;
                    Player.tileRangeX = 10;
                    Player.tileRangeY = 10;
                    player.blockRange = 10;
                    player.tileSpeed = 0f;
                }

                // Increase coin drops
                if (CrowdControlMod._server.m_incCoinsTimer.Enabled)
                    player.coins = true;

                // Jump boost
                if (CrowdControlMod._server.m_jumpPlayerTimer.Enabled)
                {
                    player.jumpSpeedBoost = CrowdControlMod._server.m_jumpPlrBoost;
                    player.jumpBoost = true;
                }

                // Disable ice skate accessoriy if slippery
                if (CrowdControlMod._server.m_slipPlayerTimer.Enabled)
                    player.iceSkate = false;

                // Extra damage when infinite ammo is activated
                if (CrowdControlMod._server.m_infiniteAmmoTimer.Enabled)
                {
                    player.arrowDamage += 0.1f;
                    player.bulletDamage += 0.1f;
                }

                // Increase magic damage
                if (CrowdControlMod._server.m_infiniteManaTimer.Enabled)
                    player.magicDamage += 0.1f;
            }


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
                        if (!CCServer._reduceDrunkEffect)
                        {
                            float drunkSineIntensity = CrowdControlMod._server.m_drunkGlitchIntensity;
                            if (!Filters.Scene["Sine"].IsActive())
                                Filters.Scene.Activate("Sine", player.Center).GetShader().UseIntensity(drunkSineIntensity);
                            else
                                Filters.Scene["Sine"].GetShader().UseIntensity(drunkSineIntensity).UseTargetPosition(player.Center);
                        }

                        float drunkGlitchIntensity = CrowdControlMod._server.m_drunkGlitchIntensity;
                        if (!Filters.Scene["Glitch"].IsActive())
                            Filters.Scene.Activate("Glitch", player.Center).GetShader().UseIntensity(drunkGlitchIntensity);
                        else
                            Filters.Scene["Glitch"].GetShader().UseIntensity(drunkGlitchIntensity).UseTargetPosition(player.Center);

                        if (!CCServer._reduceDrunkEffect)
                            Main.GameZoomTarget = 1.2f + ((float)Math.Sin(Main.GlobalTime * 2.2f) * 0.2f);
                    }

                    // Infinite mana
                    if (CrowdControlMod._server.m_infiniteManaTimer.Enabled)
                        player.statMana = player.statManaMax2;

                    // Godmode
                    if (CrowdControlMod._server.m_godPlayerTimer.Enabled || CrowdControlMod._server.m_invincPlayerTimer.Enabled)
                        player.statLife = player.statLifeMax2;

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
            }
        }

        // Called to change running speed
        public override void PostUpdateRunSpeeds()
        {
            base.PostUpdateRunSpeeds();

            if (CrowdControlMod._server != null)
            {
                // Make player run faster
                if (CrowdControlMod._server.m_fastPlayerTimer.Enabled)
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
            }
        }

        // Called when the player respawns
        public override void OnRespawn(Player player)
        {
            if (Main.myPlayer == player.whoAmI)
            {
                ReapplyBuffEffects();

                // Respawn pet
                if (m_petID >= 0)
                {
                    if (!player.hideMisc[0])
                        m_petID = -1;
                    else
                        player.AddBuff(m_petID, 1);
                }

                // Respawn light pet
                if (m_lightPetID >= 0)
                {
                    if (!player.hideMisc[1])
                        m_lightPetID = -1;
                    else
                        player.AddBuff(m_lightPetID, 1);
                }
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

        // Whether the player can be hit by NPCs
        public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
        {
            if (CrowdControlMod._server != null && (CrowdControlMod._server.m_godPlayerTimer.Enabled || CrowdControlMod._server.m_invincPlayerTimer.Enabled))
                return false;
            return base.CanBeHitByNPC(npc, ref cooldownSlot);
        }

        // Whether the player can be hit by projectiles
        public override bool CanBeHitByProjectile(Projectile proj)
        {
            if (CrowdControlMod._server != null && (CrowdControlMod._server.m_godPlayerTimer.Enabled || CrowdControlMod._server.m_invincPlayerTimer.Enabled))
                return false;
            return base.CanBeHitByProjectile(proj);
        }

        // Called when the player is killed
        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            if (Main.myPlayer == player.whoAmI)
            {
                // Reduce respawn timer
                if (m_reduceRespawn)
                {
                    player.respawnTimer = m_reducedCooldown;
                    m_reduceRespawn = false;
                }
                else
                    player.respawnTimer = (int)(player.respawnTimer * CCServer._respawnTimeFactor);

                UpdateDeathPoint();
            }

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

        // Add a buff effect to the player
        public void AddBuffEffect(int buffType, int buffTime)
        {
            if (m_buffEffects.ContainsKey(buffType))
                m_buffEffects.Remove(buffType);
            m_buffEffects.Add(buffType, new BuffEffect(player, buffType, buffTime));
        }

        // Reapply buff effects after death
        public void ReapplyBuffEffects()
        {
            List<int> toRemove = new List<int>();
            foreach (int buffType in m_buffEffects.Keys)
            {
                if (m_buffEffects[buffType].Expired())
                {
                    toRemove.Add(buffType);
                    TDebug.WriteDebug("Removed expired buff: " + Lang.GetBuffName(buffType), Color.Yellow);
                }
                else
                {
                    int remainingTime = m_buffEffects[buffType].RemainingTime();
                    player.AddBuff(buffType, remainingTime);
                    TDebug.WriteDebug("Reapplied buff: " + Lang.GetBuffName(buffType) + " for " + (remainingTime / 60) + " seconds", Color.Yellow);
                }
            }
            foreach (int buffType in toRemove)
            {
                m_buffEffects.Remove(buffType);
            }
        }

        // Stop all buff effects
        public void StopBuffEffects()
        {
            foreach (int buffType in m_buffEffects.Keys)
                player.ClearBuff(buffType);
            m_buffEffects.Clear();
        }

        // Stop a given buff effect
        public void StopBuffEffect(int buffType)
        {
            if (m_buffEffects.ContainsKey(buffType))
            {
                m_buffEffects.Remove(buffType);
                player.ClearBuff(buffType);
            }
        }

        // Set the player's hair dye
        public void SetHairDye(EHairDye hairDye)
        {
            if (CCServer._disableHairDye) return;

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

    // Class to allow buffs to re-occur after player dies
    public class BuffEffect
    {
        private readonly DateTime m_start = default;
        private readonly DateTime m_end = default;

        public BuffEffect(Player player, int buffType, int buffTime)
        {
            player.AddBuff(buffType, buffTime);
            m_start = DateTime.Now;
            m_end = DateTime.Now.AddSeconds(buffTime / 60);
        }

        // Get whether the buff effect has expired
        public bool Expired()
        {
            return DateTime.Compare(DateTime.Now, m_end) == 1;
        }

        // Get the remaining time on the buff
        public int RemainingTime()
        {
            return (int)(m_end - DateTime.Now).TotalSeconds * 60;
        }
    }
}
