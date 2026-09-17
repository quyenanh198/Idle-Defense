using System;
using System.Collections.Generic;
using IdleHeroDefense.Domain;
using UnityEngine;

namespace IdleHeroDefense.Configuration
{
    [CreateAssetMenu(menuName = "Idle Hero Defense/Stage", fileName = "Stage_")]
    public sealed class StageConfig : ScriptableObject
    {
        [Serializable]
        private sealed class EnemyEntry
        {
            public string id = "slime";
            [Min(1)] public int health = 50;
            [Min(0)] public int attack = 5;
            [Min(0.1f)] public float attackInterval = 1.5f;
            public bool isBoss;
        }

        [Serializable]
        private sealed class Wave
        {
            public List<EnemyEntry> enemies = new List<EnemyEntry>();
        }

        [SerializeField] private string stageId = "1-1";
        [Min(1)] [SerializeField] private int baseHealth = 250;
        [SerializeField] private List<Wave> waves = new List<Wave>();

        public string StageId => stageId;
        public int BaseHealth => baseHealth;

        public IReadOnlyList<IReadOnlyList<EnemyDefinition>> CreateWaves()
        {
            var result = new List<IReadOnlyList<EnemyDefinition>>();
            foreach (var wave in waves)
            {
                var enemies = new List<EnemyDefinition>();
                foreach (var enemy in wave.enemies)
                    enemies.Add(new EnemyDefinition(enemy.id, enemy.health, enemy.attack,
                        enemy.attackInterval, enemy.isBoss));
                result.Add(enemies);
            }
            return result;
        }
    }
}

