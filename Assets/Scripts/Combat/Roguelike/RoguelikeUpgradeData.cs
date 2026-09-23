using System;
using UnityEngine;
using Combat.Common;
using Combat.UI;
using Combat.Player;
using Combat.Tactician;

namespace Combat.Roguelike
{
    public enum UpgradeCategory
    {
        TacticianAbility, // 🔹 Разблокировка способности стойки Тактика
        CombatBuff,       // 🟢 Чистый боевой бафф игрока
        RiskAndReward     // 🔴 Перк с высоким риском: мощный бафф игроку + усиление врагов / дебафф
    }

    [Serializable]
    public class UpgradeCardDefinition
    {
        public string id;
        public string title;
        public UpgradeCategory category;
        public string iconSymbol;
        public string description;
        public string positiveEffectText;
        public string negativeEffectText;

        // Для способностей Тактика
        public Direction8 abilityDirection = Direction8.None;

        // Числовые параметры баффов для удобной сериализации и отображения
        public float playerHealthDelta = 0f;
        public float playerStaminaDelta = 0f;
        public float playerStaminaRegenMult = 1.0f;
        public float playerDamageMult = 1.0f;
        public float playerSpeedMult = 1.0f;

        public float enemyHealthMult = 1.0f;
        public float enemySpeedMult = 1.0f;

        public UpgradeCardDefinition() { }

        public UpgradeCardDefinition(
            string id,
            string title,
            UpgradeCategory category,
            string iconSymbol,
            string description,
            string positiveEffectText,
            string negativeEffectText = "")
        {
            this.id = id;
            this.title = title;
            this.category = category;
            this.iconSymbol = iconSymbol;
            this.description = description;
            this.positiveEffectText = positiveEffectText;
            this.negativeEffectText = negativeEffectText;
        }
    }
}
