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
        [Header("--- 1. Отображение карточки ---")]
        public string id = "card_id";
        public string title = "Название карточки";
        public UpgradeCategory category = UpgradeCategory.CombatBuff;
        public string iconSymbol = "⚔️";
        [TextArea(2, 3)]
        public string description = "Художественное описание...";
        public string positiveEffectText = "+30 к Макс. HP";
        public string negativeEffectText = "";

        [Header("--- 2. Стойка Тактика (для категории TacticianAbility) ---")]
        public Direction8 abilityDirection = Direction8.None;

        [Header("--- 3. Бонусы характеристик Игрока ---")]
        [Tooltip("+/- к максимальному запасу здоровья игрока (например, +30 или -20)")]
        public float playerHealthDelta = 0f;
        [Tooltip("+/- к максимальному запасу выносливости игрока (например, +35)")]
        public float playerStaminaDelta = 0f;
        [Tooltip("Множитель скорости восстановления выносливости (1.25 = +25%, 0.85 = -15%)")]
        public float playerStaminaRegenMult = 1.0f;
        [Tooltip("Множитель наносимого урона игрока (1.20 = +20%, 1.45 = +45%)")]
        public float playerDamageMult = 1.0f;
        [Tooltip("Множитель скорости бега игрока (1.15 = +15%, 1.25 = +25%)")]
        public float playerSpeedMult = 1.0f;

        [Header("--- 4. Усложнение Врагов (для перков Риска) ---")]
        [Tooltip("Множитель здоровья врагов (1.30 = +30% к HP врагов)")]
        public float enemyHealthMult = 1.0f;
        [Tooltip("Множитель скорости атак/замахов врагов (1.20 = +20% к скорости замахов)")]
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

        public UpgradeCardDefinition Clone()
        {
            return (UpgradeCardDefinition)this.MemberwiseClone();
        }
    }
}
