using System.Collections;
using UnityEngine;

namespace Combat.Common
{
    /// <summary>
    /// Единый менеджер и генератор всплывающего боевого текста (Floating Combat Text) в мировом пространстве.
    /// Заменяет разрозненные корутины и защищает сцену от застревания временных надписей (HideFlags.DontSave).
    /// </summary>
    public static class CombatFloatingText
    {
        public static void Spawn(Vector3 worldPos, string text, Color color, float duration = 1.0f, float floatHeight = 1.1f, float charSize = 0.085f, int fontSize = 42, int sortingOrder = 75)
        {
            if (!Application.isPlaying) return;

            var go = new GameObject("Combat_FloatingText");
            go.hideFlags = HideFlags.DontSave;
            go.transform.position = worldPos;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = sortingOrder;

            var runner = go.AddComponent<FloatingTextRunner>();
            runner.Initialize(tm, duration, floatHeight);
        }

        public static void ShowEmpowered(Vector3 pos)
        {
            Spawn(pos + new Vector3(0f, 1.1f, 0f), "УСИЛЕННАЯ АТАКА!", new Color(1f, 0.75f, 0.1f, 1f), 0.9f, 1.1f, 0.082f, 42, 70);
        }

        public static void ShowCounterAttack(Vector3 pos)
        {
            Spawn(pos + new Vector3(0f, 1.2f, 0f), "КОНТРАТАКА!", new Color(1f, 0.9f, 0.2f, 1f), 0.95f, 1.1f, 0.085f, 44, 70);
        }

        public static void ShowDefeat(Vector3 pos)
        {
            Spawn(pos + new Vector3(0f, 1.3f, 0f), "ВРАГ ПОВЕРЖЕН!", new Color(0.3f, 1f, 0.45f, 1f), 1.2f, 1.2f, 0.088f, 46, 75);
        }

        public static void ShowNoStamina(Vector3 pos)
        {
            Spawn(pos + new Vector3(0f, 1.4f, 0f), "НЕТ СТАМИНЫ!", new Color(1f, 0.45f, 0.1f, 1f), 1.15f, 1.1f, 0.088f, 46, 76);
        }

        public static void ShowDummyBroken(Vector3 pos)
        {
            Spawn(pos + new Vector3(0f, 1.2f, 0f), "МАНЕКЕН СЛОМАН!", new Color(1f, 0.45f, 0.15f, 1f), 1.0f, 0.9f, 0.082f, 42, 70);
        }

        public static void ShowAntiAirCrit(Vector3 pos, Color color)
        {
            Spawn(pos, "[АНТИ-ЭЙР КРИТ!]", color, 1.0f, 1.0f, 0.082f, 42, 72);
        }

        public static void ShowAdaptation(Vector3 pos)
        {
            Spawn(pos + new Vector3(0f, 1.3f, 0f), "АДАПТАЦИЯ! ⚡\n(+75% СТАМИНА)", new Color(0.35f, 0.9f, 1f, 1f), 1.25f, 1.2f, 0.082f, 40, 75);
        }

        public static void ShowLauncher(Vector3 pos)
        {
            Spawn(pos + new Vector3(0f, 1.4f, 0f), "В ВОЗДУХ! ⚡", new Color(0.95f, 0.3f, 0.95f, 1f), 1.2f, 1.3f, 0.088f, 44, 76);
        }

        private class FloatingTextRunner : MonoBehaviour
        {
            private TextMesh _tm;
            private float _duration;
            private float _floatHeight;
            private float _elapsed;
            private Vector3 _startPos;
            private Vector3 _endPos;
            private Color _baseColor;

            public void Initialize(TextMesh tm, float duration, float floatHeight)
            {
                _tm = tm;
                _duration = Mathf.Max(0.01f, duration);
                _floatHeight = floatHeight;
                _startPos = transform.position;
                _endPos = _startPos + new Vector3(0f, _floatHeight, 0f);
                _baseColor = tm != null ? tm.color : Color.white;
            }

            private void Update()
            {
                _elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);

                transform.position = Vector3.Lerp(_startPos, _endPos, t);

                if (_tm != null)
                {
                    Color c = _baseColor;
                    c.a = Mathf.Clamp01(1f - (t * t));
                    _tm.color = c;
                }

                if (_elapsed >= _duration)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
