using System;
using System.Collections.Generic;
using UnityEngine;
using Combat.UI;

namespace Combat
{
    /// <summary>
    /// Описание приёма или связки ударов, активируемой цепочкой из 8 направлений.
    /// </summary>
    [Serializable]
    public class ComboSequenceDefinition
    {
        [Tooltip("Уникальный строковый идентификатор приёма")]
        [SerializeField] private string sequenceId;

        [Tooltip("Отображаемое название приёма в HUD")]
        [SerializeField] private string sequenceName;

        [Tooltip("Последовательность стрелок в виде строки (например: '⬅⮕' или '⬋⬇⬊⮕⬈')")]
        [SerializeField] private string glyphPattern;

        [Tooltip("Список направлений 8-way для распознавания")]
        [SerializeField] private List<Direction8> requiredDirections = new List<Direction8>();

        [Tooltip("Конфигурация параметров удара (урон, тайминги, хитбокс, отталкивание)")]
        [SerializeField] private AttackConfig attackData;

        [Tooltip("Расход выносливости игрока на исполнение")]
        [SerializeField] private float staminaCost = 14f;

        [Tooltip("Подбрасывает ли приём врага в воздух (Launcher), если стамина врага на нуле?")]
        [SerializeField] private bool isLauncher = false;

        [Tooltip("Сила дополнительного выпада/рывка вперед при ударе")]
        [SerializeField] private float lungeForce = 4.0f;

        [Tooltip("Разрешено ли связывать этот приём с последующими ударами комбо")]
        [SerializeField] private bool canCancelIntoOthers = true;

        public string SequenceId => sequenceId;
        public string SequenceName => sequenceName;
        public string GlyphPattern => glyphPattern;
        public IReadOnlyList<Direction8> RequiredDirections => requiredDirections;
        public AttackConfig AttackData => attackData;
        public float StaminaCost => staminaCost;
        public bool IsLauncher => isLauncher;
        public float LungeForce => lungeForce;
        public bool CanCancelIntoOthers => canCancelIntoOthers;

        public int Length => requiredDirections != null ? requiredDirections.Count : 0;

        public ComboSequenceDefinition(
            string id,
            string name,
            string glyphs,
            AttackConfig attack,
            float stamina = 14f,
            bool launcher = false,
            float lunge = 4f)
        {
            sequenceId = id;
            sequenceName = name;
            glyphPattern = glyphs;
            requiredDirections = Direction8Extensions.ParseSequence(glyphs);
            attackData = attack;
            staminaCost = stamina;
            isLauncher = launcher;
            lungeForce = lunge;
            canCancelIntoOthers = true;
        }

        public ComboSequenceDefinition(
            string id,
            string name,
            IEnumerable<Direction8> dirs,
            AttackConfig attack,
            float stamina = 14f,
            bool launcher = false,
            float lunge = 4f)
        {
            sequenceId = id;
            sequenceName = name;
            requiredDirections = new List<Direction8>(dirs);
            glyphPattern = requiredDirections.ToGlyphString();
            attackData = attack;
            staminaCost = stamina;
            isLauncher = launcher;
            lungeForce = lunge;
            canCancelIntoOthers = true;
        }

        /// <summary>
        /// Проверяет, совпадает ли конец истории ввода с этим приёмом.
        /// </summary>
        public bool Matches(IReadOnlyList<Direction8> buffer, int bufferCount)
        {
            if (requiredDirections == null || requiredDirections.Count == 0) return false;
            if (bufferCount < requiredDirections.Count) return false;

            int patternLen = requiredDirections.Count;
            int bufferOffset = bufferCount - patternLen;

            for (int i = 0; i < patternLen; i++)
            {
                if (buffer[bufferOffset + i] != requiredDirections[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
