using System.Collections.Generic;
using UnityEngine;

namespace Combat.Dialogue
{
    /// <summary>
    /// Полный структурированный сценарий диалогов для режима обучения.
    /// Содержит все цепочки реплик из ТЗ.
    /// </summary>
    public static class TutorialDialogueData
    {
        /// <summary>
        /// Начальный диалог в стартовом коридоре
        /// </summary>
        public static List<DialogueLine> GetIntroCorridorLines()
        {
            return new List<DialogueLine>
            {
                DialogueLine.Stranger("Поглядите, кто это тут у нас? Неужто сам Иво, собственной персоной?"),
                DialogueLine.Stranger("Посланник Дуального клана. Сейчас ты очень далеко от своего дома."),
                DialogueLine.Stranger("Я не знаю, откуда ты взялся, но я не упущу такую прекрасную возможность провести репетицию до начала спектакля."),
                DialogueLine.Stranger("Не переживай: твоя роль будет главной, а задача — проще некуда."),
                DialogueLine.Stranger("Просто сражаться за своё выживание раз за разом.")
            };
        }

        /// <summary>
        /// Диалог при выходе на арену с манекеном (до настроек)
        /// </summary>
        public static List<DialogueLine> GetEnterDummyArenaLines()
        {
            return new List<DialogueLine>
            {
                DialogueLine.Stranger("Так, для начала научим тебя бить. Может быть, тебе повезёт, и при встрече с моими «актёрами» ты не умрёшь сразу."),
                DialogueLine.Stranger("..."),
                DialogueLine.Stranger("...?"),
                DialogueLine.Stranger("Ах да, совсем забыл. В этом измерении я настроил для тебя уникальное видение. Извини, мы тут всё впопыхах доделываем: я совсем не ждал гостей так рано, так что придётся тебе обслужить себя самому."),
                DialogueLine.Stranger("(Нажмите Esc и включите отображение колеса фехтования.)", tag: "OpenSettingsPrompt")
            };
        }

        /// <summary>
        /// Диалог после включения колеса фехтования в настройках
        /// </summary>
        public static List<DialogueLine> GetAfterWheelEnabledLines()
        {
            return new List<DialogueLine>
            {
                DialogueLine.Stranger("Превосходно. Видишь круг? Его я назвал «колесом фехтования». Специальная разработка для специального героя — тебя!"),
                DialogueLine.Stranger("Оно нужно тебе, чтобы орудовать мечом."),
                DialogueLine.Stranger("Подойди к манекену и покажи, чему ты научился.", tag: "StartDummySwordHits")
            };
        }

        /// <summary>
        /// Диалог после 3 успешных ударов мечом по манекену
        /// </summary>
        public static List<DialogueLine> GetAfterDummySwordHitsLines()
        {
            return new List<DialogueLine>
            {
                DialogueLine.Stranger("Вот она — сила главного героя."),
                DialogueLine.Stranger("Быть тем, чьи первые противники даже не могут ответить."),
                DialogueLine.Stranger("Ладно, давай перейдём к чему-то более весёлому."),
                DialogueLine.Stranger("Твоя задача — сосредоточиться на Архе."),
                DialogueLine.Stranger("(Нажмите Ctrl, чтобы перейти в стойку Архе.)", tag: "WaitForCtrlStance")
            };
        }

        /// <summary>
        /// Диалог сразу после нажатия Ctrl (перехода в Архе)
        /// </summary>
        public static List<DialogueLine> GetArcheExplanationLines()
        {
            return new List<DialogueLine>
            {
                DialogueLine.Stranger("А теперь — без лишних движений, приятель."),
                DialogueLine.Stranger("Архе — вещь такая, непостоянная."),
                DialogueLine.Stranger("Опасная."),
                DialogueLine.Stranger("Ты должен складывать руны непрерывно, и желательно делать это достаточно быстро, пока поток Архе не изменился."),
                DialogueLine.Stranger("А то кто знает, к чему может привести неконтролируемый поток энергии..."),
                DialogueLine.Stranger("(Зажав ЛКМ, вводите указанные направления над колесом фехтования, не отпуская клавишу.)", tag: "StartArcheCastTask")
            };
        }

        /// <summary>
        /// Диалог после первого взрыва и телепортации назад
        /// </summary>
        public static List<DialogueLine> GetFirstExplosionFailLines()
        {
            return new List<DialogueLine>
            {
                DialogueLine.Stranger("В первый раз?"),
                DialogueLine.Stranger("Скажи спасибо, что я предусмотрел такой исход событий и ограничил твои возможности к самоликвидации."),
                DialogueLine.Stranger("Так что давай, пробуй ещё раз.")
            };
        }

        /// <summary>
        /// Диалог после успешного попадания фаерболом по манекену
        /// </summary>
        public static List<DialogueLine> GetArcheSuccessLines()
        {
            return new List<DialogueLine>
            {
                DialogueLine.Stranger("Я в тебе никогда не сомневался!"),
                DialogueLine.Stranger("Ладно, иди в следующую комнату: пора разнообразить число «живых» в этом зале.", tag: "OpenGateToVova")
            };
        }

        /// <summary>
        /// Диалог при входе на арену со скелетом Вовой
        /// </summary>
        public static List<DialogueLine> GetVovaEncounterLines(Sprite vovaSprite)
        {
            return new List<DialogueLine>
            {
                DialogueLine.Stranger("Трепещи, Иво, ибо перед тобой… Вова!"),
                DialogueLine.Stranger("Я не знаю, почему его так зовут."),
                DialogueLine.Vova("Здрасьте...", vovaSprite),
                DialogueLine.Stranger("Вы будете драться насмерть!"),
                DialogueLine.Vova("Владыка, я не против, но мне ещё замок помогать обставлять... Не уверен, что разумно сейчас…", vovaSprite),
                DialogueLine.Stranger("Друг мой костяной, я говорю — ты делаешь."),
                DialogueLine.Vova("Повинуюсь, владыка.", vovaSprite),
                DialogueLine.Stranger("Обычным мечом Вову не одолеть. Сначала оглуши его — сбей выносливость серией ударов или парированием.", tag: "ShowFocusStanceUI"),
                DialogueLine.Stranger("Когда Вова оглушён: зажми [ПКМ], оттяни мышь назад (как тетиву лука), наводя белую линию на врага, и при максимальном натяжении нажми [ЛКМ]!", tag: "StartVovaFight")
            };
        }

        /// <summary>
        /// Диалог после добивания Вовы в режиме фокуса
        /// </summary>
        public static List<DialogueLine> GetVovaDefeatedLines()
        {
            return new List<DialogueLine>
            {
                DialogueLine.Stranger("Отлично! Просто превосходно! Думаю, этой базы знаний тебе хватит, чтобы не быть столь беспомощным."),
                DialogueLine.Stranger("Иди дальше: там будет кристалл-портал, который отправит тебя в подземелья, что я на скорую руку для тебя сделал."),
                DialogueLine.Stranger("Дальше разберёшься на месте со всем остальным."),
                DialogueLine.Stranger("И помни..."),
                DialogueLine.Stranger("Я слежу за тобой.", tag: "OpenGateToExitCrystal")
            };
        }
    }
}
