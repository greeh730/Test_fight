using System;
using UnityEngine;

namespace Combat.Dialogue
{
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("Имя говорящего персонажа (например: Незнакомец, Вова, Иво)")]
        public string speakerName = "Незнакомец";

        [Tooltip("Цвет имени говорящего в диалоговом окне")]
        public Color speakerColor = new Color(1f, 0.85f, 0.35f, 1f);

        [Tooltip("Текст реплики")]
        [TextArea(2, 5)]
        public string text = "";

        [Tooltip("Спрайт портрета говорящего (если null — портрет скрыт)")]
        public Sprite portrait;

        [Tooltip("Отображать портрет справа (true) или слева (false). По сценарию у Вовы — справа.")]
        public bool portraitOnRight = true;

        [Tooltip("Масштаб портрета для зума/кадрирования (например 1.8 для приближения к лицу)")]
        public Vector2 portraitScale = Vector2.one;

        [Tooltip("Смещение портрета внутри маски в пикселях")]
        public Vector2 portraitOffset = Vector2.zero;

        [Tooltip("Время задержки перед автопереходом (если 0 или меньше — переход только по клику игрока)")]
        public float autoAdvanceDelay = 0f;

        [Tooltip("Скорость печати текста (символов в секунду). 0 = мгновенный вывод")]
        public float typingSpeed = 38f;

        [Tooltip("Специальный строковый тег для триггера логики обучения")]
        public string eventTag = "";

        public DialogueLine() { }

        public DialogueLine(string speaker, string content, Color? nameColor = null, Sprite portraitSprite = null, bool onRight = true, string tag = "")
        {
            speakerName = speaker;
            text = content;
            speakerColor = nameColor ?? new Color(1f, 0.85f, 0.35f, 1f);
            portrait = portraitSprite;
            portraitOnRight = onRight;
            eventTag = tag;
            portraitScale = Vector2.one;
            portraitOffset = Vector2.zero;
        }

        public static DialogueLine Stranger(string content, string tag = "")
        {
            return new DialogueLine("Незнакомец", content, new Color(1f, 0.82f, 0.35f, 1f), null, false, tag);
        }

        public static DialogueLine Vova(string content, Sprite vovaPortrait, string tag = "")
        {
            var line = new DialogueLine("Вова", content, new Color(0.65f, 0.88f, 1f, 1f), vovaPortrait, true, tag);
            // Приближение и центрирование черепа Вовы
            line.portraitScale = new Vector2(2.1f, 2.1f);
            line.portraitOffset = new Vector2(0f, -20f);
            return line;
        }
    }
}
