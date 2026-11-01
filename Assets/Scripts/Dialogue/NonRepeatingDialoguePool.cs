using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Dialogue
{
    /// <summary>
    /// Пул случайных фраз без повторений.
    /// Каждая фраза выбирается случайно из оставшихся.
    /// Когда фразы заканчиваются, метод GetNextPhrase() возвращает null (больше ничего не воспроизводится).
    /// </summary>
    [Serializable]
    public class NonRepeatingDialoguePool
    {
        [Tooltip("Исходный список всех доступных фраз")]
        [SerializeField] private List<string> sourcePhrases = new List<string>();

        [Tooltip("Оставшиеся неиспользованные фразы")]
        [SerializeField] private List<string> remainingPhrases = new List<string>();

        public int RemainingCount => remainingPhrases.Count;
        public int TotalCount => sourcePhrases.Count;
        public bool HasPhrases => remainingPhrases.Count > 0;

        public NonRepeatingDialoguePool()
        {
            sourcePhrases = new List<string>();
            remainingPhrases = new List<string>();
        }

        public NonRepeatingDialoguePool(IEnumerable<string> phrases)
        {
            sourcePhrases = new List<string>(phrases);
            ResetPool();
        }

        public void SetPhrases(IEnumerable<string> phrases)
        {
            sourcePhrases = new List<string>(phrases);
            ResetPool();
        }

        /// <summary>
        /// Сбрасывает пул, делая все фразы снова доступными.
        /// </summary>
        public void ResetPool()
        {
            remainingPhrases = new List<string>(sourcePhrases);
        }

        /// <summary>
        /// Получает следующую случайную фразу без повторений.
        /// Если фразы закончились — возвращает null.
        /// </summary>
        public string GetNextPhrase()
        {
            if (remainingPhrases == null || remainingPhrases.Count == 0)
            {
                return null;
            }

            int randomIndex = UnityEngine.Random.Range(0, remainingPhrases.Count);
            string chosenPhrase = remainingPhrases[randomIndex];
            remainingPhrases.RemoveAt(randomIndex);

            return chosenPhrase;
        }

        #region Factory Presets for Tutorial
        /// <summary>
        /// Создает пул из 8 фраз при повторных взрывах Архе
        /// </summary>
        public static NonRepeatingDialoguePool CreateExplosionFailPool()
        {
            return new NonRepeatingDialoguePool(new string[]
            {
                "Любишь взрывы?",
                "Поаккуратнее.",
                "Стабильность — признак мастерства.",
                "Нравится боль?",
                "А может, мне найти другого главного героя?...",
                "Ошибаться — это нормально. Главное — делать выводы.",
                "Вот ещё чуть-чуть, и я в тебя уверую.",
                "Нет, ну технически — огонь ты призвал."
            });
        }

        /// <summary>
        /// Создает пул из 4 фраз при касте мимо манекена
        /// </summary>
        public static NonRepeatingDialoguePool CreateSpellMissPool()
        {
            return new NonRepeatingDialoguePool(new string[]
            {
                "А теперь — по манекену.",
                "Манекен, кстати, не уворачивается.",
                "Какая битва! Кто же победит? Манекен или Великий Герой?",
                "А манекен бы попал."
            });
        }
        #endregion
    }
}
