using System.Globalization;
using System.Linq;
using System.Text;
using IdleHeroDefense.Infrastructure;
using IdleHeroDefense.Monetization;
using IdleHeroDefense.Progression;

namespace IdleHeroDefense.Configuration
{
    public static class BalanceSnapshotService
    {
        public static string CreateCanonicalText()
        {
            var text = new StringBuilder();
            text.AppendLine("HERO,id,class,faction,hp,attack,interval,ability,ability_damage,target,status");
            foreach (var id in HeroCatalog.AllIds.OrderBy(x => x))
            {
                var hero = HeroCatalog.Get(id);
                text.Append("HERO,").Append(hero.Id).Append(',').Append(hero.Class).Append(',').Append(hero.Faction).Append(',')
                    .Append(hero.MaxHealth).Append(',').Append(hero.Attack).Append(',')
                    .Append(hero.AttackInterval.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                    .Append(hero.Ultimate.Id).Append(',').Append(hero.Ultimate.Damage).Append(',').Append(hero.Ultimate.Targeting).Append(',')
                    .Append(hero.Ultimate.StatusEffect?.Type.ToString() ?? "None").AppendLine();
            }
            text.AppendLine("EQUIPMENT,id,slot,attack,health");
            foreach (var item in EquipmentCatalog.All)
                text.Append("EQUIPMENT,").Append(item.Id).Append(',').Append(item.Slot).Append(',').Append(item.Attack).Append(',').Append(item.Health).AppendLine();
            text.AppendLine("PRODUCT,id,kind,gems,entitlement");
            foreach (var product in ProductCatalog.Products.OrderBy(x => x.Id))
                text.Append("PRODUCT,").Append(product.Id).Append(',').Append(product.Kind).Append(',').Append(product.Gems).Append(',').Append(product.Entitlement).AppendLine();
            var config = LiveConfig.Defaults();
            text.Append("CONFIG,campaign_gold,").Append(config.campaignGold).AppendLine()
                .Append("CONFIG,daily_dungeon_gold,").Append(config.dailyDungeonGold).AppendLine()
                .Append("CONFIG,tower_base_gold,").Append(config.towerBaseGold).AppendLine()
                .Append("CONFIG,daily_attempts,").Append(config.dailyDungeonAttempts).AppendLine()
                .Append("CONFIG,idle_cap_hours,").Append(config.idleCapHours).AppendLine();
            return text.ToString().Replace("\r\n", "\n");
        }
    }
}

