///<summary>
/// File: InstaDynamite.cs
/// Last Updated: 2020-07-22
/// Author: MRG-bit
/// Description: Dynamite that explodes instantly
///</summary>

using System;
using Terraria.ModLoader;

namespace CrowdControlMod.Projectiles
{
    public class InstaDynamite : ModProjectile
    {
        public override string Texture => "Terraria/Projectile_" + Terraria.ID.ProjectileID.Dynamite;

        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Dynamite");
            base.SetStaticDefaults();
        }

        // Set the defaults of the item
        public override void SetDefaults()
        {
            projectile.CloneDefaults(Terraria.ID.ProjectileID.Dynamite);
            aiType = Terraria.ID.ProjectileID.Dynamite;
            projectile.timeLeft = 3;
            projectile.damage = 1000;
            projectile.knockBack = 10f;

            base.SetDefaults();
        }

        // Called before AI is executed
        public override bool PreAI()
        {
            projectile.type = Terraria.ID.ProjectileID.Dynamite;

            return base.PreAI();
        }
    }
}
