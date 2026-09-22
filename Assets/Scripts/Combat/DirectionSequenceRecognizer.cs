using System;
using System.Collections.Generic;
using UnityEngine;
using Combat.UI;

namespace Combat
{
    /// <summary>
    /// Распознаватель моушн-последовательностей из 8 направлений (Fighting-game gesture recognizer).
    /// Накапливает вводимые сектора и сопоставляет суффикс буфера с приемами из CombatSequenceLibrary.
    /// Автоматически отслеживает повторение одного и того же приема для наложения штрафа (Stale Move Penalty).
    /// </summary>
    [DisallowMultipleComponent]
    public class DirectionSequenceRecognizer : MonoBehaviour
    {
        public static DirectionSequenceRecognizer Instance { get; private set; }

        [Header("--- Настройки буфера ввода ---")]
        [Tooltip("Время бездействия в секундах, после которого накопленный буфер направлений сбрасывается")]
        [SerializeField] private float bufferTimeout = 1.25f;

        [Tooltip("Минимальный интервал между добавлением новых направлений (фильтр дребезга)")]
        [SerializeField] private float minTokenInterval = 0.035f;

        [Tooltip("Максимальный размер буфера истории направлений")]
        [SerializeField] private int maxBufferSize = 12;

        [Tooltip("Окно времени для фиксации спама одинакового приема подряд (Stale Move)")]
        [SerializeField] private float staleMoveWindow = 4.0f;

        // Runtime State
        private readonly List<Direction8> _buffer = new List<Direction8>();
        private float _lastTokenTime = -10f;
        private ComboSequenceDefinition _lastMatchedSequence = null;
        private float _lastMatchTime = -10f;
        private bool _lastWasStale = false;
        private int _consecutiveSameCount = 0;

        public IReadOnlyList<Direction8> CurrentBuffer => _buffer;
        public ComboSequenceDefinition LastMatchedSequence => _lastMatchedSequence;
        public bool LastWasStale => _lastWasStale;
        public int ConsecutiveSameCount => _consecutiveSameCount;

        // Events
        public event Action<Direction8> OnTokenAdded;
        public event Action<Direction8> OnRawTokenAdded;
        public event Action<ComboSequenceDefinition, bool> OnSequenceMatched;
        public event Action<IReadOnlyList<Direction8>> OnBufferChanged;
        public event Action OnBufferCleared;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
            }
        }

        private void Update()
        {
            if (_buffer.Count > 0 && Time.time - _lastTokenTime > bufferTimeout)
            {
                ClearBuffer();
            }
        }

        /// <summary>
        /// Добавляет новое направление в буфер (абсолютное экранное направление).
        /// Направление ударов и жестов не зависит от того, куда повернут персонаж.
        /// Если направление совпадает с предыдущим сектором, повторно не добавляется.
        /// </summary>
        public void AddToken(Direction8 rawDir, float facingSign = 1f)
        {
            if (rawDir == Direction8.None) return;

            // Направление абсолютно на экране и не зеркалится от взгляда персонажа
            Direction8 dir = rawDir;

            if (_buffer.Count > 0 && _buffer[_buffer.Count - 1] == dir)
            {
                // Уже находимся в этом секторе
                return;
            }

            if (Application.isPlaying && Time.time - _lastTokenTime < minTokenInterval)
            {
                return;
            }

            if (_buffer.Count >= maxBufferSize)
            {
                _buffer.RemoveAt(0);
            }

            _buffer.Add(dir);
            _lastTokenTime = Time.time;

            OnRawTokenAdded?.Invoke(rawDir);
            OnTokenAdded?.Invoke(dir);

            // Проверяем совпадение с библиотекой приёмов
            CheckForMatch();

            OnBufferChanged?.Invoke(_buffer);
        }

        private void CheckForMatch()
        {
            var library = CombatSequenceLibrary.Instance;
            if (library == null) return;

            var match = library.FindMatchingSequence(_buffer, _buffer.Count);
            if (match != null)
            {
                // Проверка на Stale Move (повторение того же приема 2 раза подряд)
                bool isSameAsPrevious = (_lastMatchedSequence != null &&
                                         _lastMatchedSequence.SequenceId == match.SequenceId &&
                                         Time.time - _lastMatchTime <= staleMoveWindow);

                if (isSameAsPrevious)
                {
                    _consecutiveSameCount++;
                    _lastWasStale = true;
                }
                else
                {
                    _consecutiveSameCount = 1;
                    _lastWasStale = false;
                }

                _lastMatchedSequence = match;
                _lastMatchTime = Time.time;

                Debug.Log($"<color=#FFAA00>[GESTURE MATCH]</color> Распознан приём: <b>{match.SequenceName}</b> ({match.GlyphPattern}) | Stale: {_lastWasStale} (x{_consecutiveSameCount})");

                OnSequenceMatched?.Invoke(match, _lastWasStale);

                // Очищаем буфер после успешного совпадения, чтобы начать следующий удар связки
                _buffer.Clear();
            }
        }

        /// <summary>
        /// Очищает текущий буфер накопленных стрелок.
        /// </summary>
        public void ClearBuffer()
        {
            if (_buffer.Count > 0)
            {
                _buffer.Clear();
                OnBufferCleared?.Invoke();
                OnBufferChanged?.Invoke(_buffer);
            }
        }

        /// <summary>
        /// Полный сброс истории (включая учет повторов stale move).
        /// </summary>
        public void ResetHistory()
        {
            ClearBuffer();
            _lastMatchedSequence = null;
            _lastMatchTime = -10f;
            _lastWasStale = false;
            _consecutiveSameCount = 0;
        }

        /// <summary>
        /// Возвращает текущий буфер в виде строки стрелок, разделенных пробелами (например: "⬅ ⮕").
        /// </summary>
        public string GetBufferGlyphString(string separator = " ")
        {
            return _buffer.ToGlyphString(separator);
        }
    }
}
