using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleHeroDefense.Progression
{
    public enum EquipmentSlot { Weapon, Armor }

    public sealed class EquipmentDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public EquipmentSlot Slot { get; }
        public int Attack { get; }
        public int Health { get; }
        public EquipmentDefinition(string id, string name, EquipmentSlot slot, int attack, int health)
        { Id = id; Name = name; Slot = slot; Attack = attack; Health = health; }
    }

    public static class EquipmentCatalog
    {
        private static readonly Dictionary<string, EquipmentDefinition> Items = new Dictionary<string, EquipmentDefinition>
        {
            ["bronze_sword"] = new EquipmentDefinition("bronze_sword", "Bronze Sword", EquipmentSlot.Weapon, 15, 0),
            ["oak_bow"] = new EquipmentDefinition("oak_bow", "Oak Bow", EquipmentSlot.Weapon, 20, 0),
            ["apprentice_staff"] = new EquipmentDefinition("apprentice_staff", "Apprentice Staff", EquipmentSlot.Weapon, 18, 20),
            ["guard_plate"] = new EquipmentDefinition("guard_plate", "Guard Plate", EquipmentSlot.Armor, 0, 100),
            ["ranger_leathers"] = new EquipmentDefinition("ranger_leathers", "Ranger Leathers", EquipmentSlot.Armor, 5, 60),
            ["mystic_robe"] = new EquipmentDefinition("mystic_robe", "Mystic Robe", EquipmentSlot.Armor, 8, 50)
        };

        public static EquipmentDefinition Get(string id) => Items.TryGetValue(id, out var item)
            ? item : throw new KeyNotFoundException($"Unknown equipment: {id}");
        public static IReadOnlyList<EquipmentDefinition> All => Items.Values.OrderBy(x => x.Id).ToList();
    }

    public sealed class EquipmentService
    {
        private readonly PlayerProfile profile;
        public EquipmentService(PlayerProfile profile) => this.profile = profile ?? throw new ArgumentNullException(nameof(profile));

        public void EnsureStarterEquipment()
        {
            if (profile.equipment.Count > 0) return;
            Add("eq_sword", "bronze_sword"); Add("eq_bow", "oak_bow"); Add("eq_staff", "apprentice_staff");
            Add("eq_plate", "guard_plate"); Add("eq_leathers", "ranger_leathers"); Add("eq_robe", "mystic_robe");
        }

        public bool AutoEquip(string heroId, out string reason)
        {
            if (profile.heroes.All(x => x.heroId != heroId)) { reason = "Hero is locked."; return false; }
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                var best = profile.equipment.Where(x => EquipmentCatalog.Get(x.definitionId).Slot == slot &&
                    (string.IsNullOrEmpty(x.equippedHeroId) || x.equippedHeroId == heroId))
                    .OrderByDescending(Power).FirstOrDefault();
                if (best == null) continue;
                foreach (var item in profile.equipment.Where(x => x.equippedHeroId == heroId && EquipmentCatalog.Get(x.definitionId).Slot == slot))
                    item.equippedHeroId = string.Empty;
                best.equippedHeroId = heroId;
            }
            reason = string.Empty;
            return true;
        }

        public EconomyResult Upgrade(string instanceId)
        {
            var item = profile.equipment.FirstOrDefault(x => x.instanceId == instanceId);
            if (item == null) return new EconomyResult(false, "Equipment not found.", profile.gearMaterials);
            var cost = item.level * 25;
            if (profile.gearMaterials < cost) return new EconomyResult(false, "Insufficient gear materials.", profile.gearMaterials);
            profile.gearMaterials -= cost;
            item.level++;
            return new EconomyResult(true, string.Empty, profile.gearMaterials);
        }

        public (int attack, int health) StatsFor(string heroId)
        {
            var attack = 0; var health = 0;
            foreach (var item in profile.equipment.Where(x => x.equippedHeroId == heroId))
            {
                var definition = EquipmentCatalog.Get(item.definitionId);
                attack += definition.Attack * item.level;
                health += definition.Health * item.level;
            }
            return (attack, health);
        }

        private void Add(string instanceId, string definitionId) => profile.equipment.Add(new EquipmentInstance(instanceId, definitionId));
        private static int Power(EquipmentInstance item)
        {
            var definition = EquipmentCatalog.Get(item.definitionId);
            return (definition.Attack * 5 + definition.Health) * item.level;
        }
    }
}
