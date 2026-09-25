using System;
using System.Collections.Generic;
using UnityEngine;
using Combat.UI;

namespace Combat.Audio
{
    /// <summary>
    /// Централизованный звуковой менеджер игры Abyss of Emotions.
    /// Управляет воспроизведением звуков передвижения (ходьба, бег по Shift, прыжки),
    /// ближнего боя (взмахи, парирование, удары в блок, оглушения),
    /// реакций скелетов (4 вариации ударов, смерть) и 8 способностей Тактика.
    /// </summary>
    [DisallowMultipleComponent]
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager _instance;
        public static SoundManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<SoundManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[SoundManager]");
                        _instance = go.AddComponent<SoundManager>();
                        if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        [Header("--- Move Audio ---")]
        [Tooltip("Шаги при обычной ходьбе")]
        [SerializeField] private AudioClip walkClip;
        [Tooltip("Звук бега при зажатом Shift")]
        [SerializeField] private AudioClip runClip;
        [Tooltip("Звук прыжка")]
        [SerializeField] private AudioClip jumpClip;

        [Header("--- Fight Audio ---")]
        [Tooltip("Взмах/удар клинком")]
        [SerializeField] private AudioClip attackSwingClip;
        [Tooltip("Идеальное парирование")]
        [SerializeField] private AudioClip parryClip;
        [Tooltip("Удар в выставленный блок")]
        [SerializeField] private AudioClip blockHitClip;
        [Tooltip("Оглушение врага (Stamina Break / стан)")]
        [SerializeField] private AudioClip enemyStunClip;

        [Header("--- Enemy (Skeleton) Audio ---")]
        [Tooltip("Вариации ударов по скелету")]
        [SerializeField] private AudioClip[] skeletonDamageClips = new AudioClip[4];
        [Tooltip("Звук гибели скелета")]
        [SerializeField] private AudioClip skeletonDeathClip;

        [Header("--- Tactician Magic Audio (8 Directions) ---")]
        [Tooltip("➡️ Прощупывающий выпад (Probing Thrust)")]
        [SerializeField] private AudioClip probingThrustClip;
        [Tooltip("⬇️ Глубинная печать (Abyssal Trap)")]
        [SerializeField] private AudioClip abyssalTrapClip;
        [Tooltip("⬆️ Гравитационный якорь (Gravity Anchor)")]
        [SerializeField] private AudioClip gravityAnchorClip;
        [Tooltip("⬅️ Тактический отход / Дым (Smoke)")]
        [SerializeField] private AudioClip tacticalSmokeClip;
        [Tooltip("↗️ Кинетический подброс (Kinetic Launch)")]
        [SerializeField] private AudioClip kineticLaunchClip;
        [Tooltip("↘️ Направленные шипы (Directional Spikes)")]
        [SerializeField] private AudioClip directionalSpikesClip;
        [Tooltip("↙️ Магический гарпун (Magic Harpoon)")]
        [SerializeField] private AudioClip magicHarpoonClip;
        [Tooltip("↖️ Веерная защита (Fan Guard)")]
        [SerializeField] private AudioClip fanGuardClip;

        [Header("--- Volume Settings ---")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float footstepsVolume = 0.85f;

        // Пул источников звука
        private const int SfxPoolSize = 16;
        private readonly List<AudioSource> _sfxPool = new List<AudioSource>(SfxPoolSize);
        private int _poolIndex = 0;

        // Выделенный канал для перемещения (шаги/бег)
        private AudioSource _movementSource;

        // Предотвращение повтора одного и того же звука урона дважды подряд
        private int _lastSkeletonHitIndex = -1;

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                UpdateActiveMovementVolume();
            }
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                UpdateActiveMovementVolume();
            }
        }

        public float FootstepsVolume
        {
            get => footstepsVolume;
            set
            {
                footstepsVolume = Mathf.Clamp01(value);
                UpdateActiveMovementVolume();
            }
        }

        private bool _isMuted = false;
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                AudioListener.pause = value;
            }
        }

        public void ApplyVolumeSettings(float master, float sfx, float footsteps, bool muted = false)
        {
            masterVolume = Mathf.Clamp01(master);
            sfxVolume = Mathf.Clamp01(sfx);
            footstepsVolume = Mathf.Clamp01(footsteps);
            IsMuted = muted;
            UpdateActiveMovementVolume();
        }

        private void UpdateActiveMovementVolume()
        {
            if (_movementSource != null && _movementSource.isPlaying)
            {
                _movementSource.volume = masterVolume * sfxVolume * footstepsVolume;
            }
        }

        /// <summary>
        /// Воспроизводит тестовый звук для мгновенной проверки громкости в меню настроек.
        /// </summary>
        public void PlayTestSound()
        {
            if (parryClip != null)
            {
                PlayClip(parryClip, 1.0f, 1.0f, 1.0f);
            }
            else if (attackSwingClip != null)
            {
                PlayClip(attackSwingClip, 1.0f, 1.0f, 1.0f);
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                if (Application.isPlaying && transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeAudioSources();
            EnsureClipsLoaded();
        }

        private void Start()
        {
            // Синхронизация с сохраненными настройками
            if (Combat.Settings.CombatSettingsManager.Instance != null)
            {
                var s = Combat.Settings.CombatSettingsManager.Instance.CurrentSettings;
                if (s != null)
                {
                    ApplyVolumeSettings(s.masterVolume, s.sfxVolume, s.footstepsVolume, s.isMuted);
                }
            }
        }

        private void OnValidate()
        {
            EnsureClipsLoaded();
        }

        private void InitializeAudioSources()
        {
            // Канал перемещения
            if (_movementSource == null)
            {
                var moveObj = new GameObject("AudioChannel_Movement");
                moveObj.transform.SetParent(transform, false);
                _movementSource = moveObj.AddComponent<AudioSource>();
                _movementSource.playOnAwake = false;
                _movementSource.spatialBlend = 0f; // 2D звук
                _movementSource.loop = true;
            }

            // Пул SFX
            if (_sfxPool.Count == 0)
            {
                var poolRoot = new GameObject("AudioPool_SFX");
                poolRoot.transform.SetParent(transform, false);

                for (int i = 0; i < SfxPoolSize; i++)
                {
                    var srcObj = new GameObject($"SfxSource_{i}");
                    srcObj.transform.SetParent(poolRoot.transform, false);
                    var src = srcObj.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.spatialBlend = 0f; // 2D звук для четкости
                    _sfxPool.Add(src);
                }
            }
        }

        public void EnsureClipsLoaded()
        {
#if UNITY_EDITOR
            if (walkClip == null)
                walkClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Move/Шаг.mp3");

            if (runClip == null)
                runClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Move/Бег.mp3");

            if (jumpClip == null)
                jumpClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Move/Звук-прыжка.wav");

            if (attackSwingClip == null)
                attackSwingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Fight/Удар.mp3");

            if (parryClip == null)
                parryClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Fight/Парирование.wav");

            if (blockHitClip == null)
                blockHitClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Fight/Звук удара в блок.mp3");

            if (enemyStunClip == null)
                enemyStunClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Fight/Оглушения противника.mp3");

            if (skeletonDamageClips == null || skeletonDamageClips.Length < 4)
                skeletonDamageClips = new AudioClip[4];

            if (skeletonDamageClips[0] == null)
                skeletonDamageClips[0] = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Sleketon_damage/Удар по скелету 1.mp3");
            if (skeletonDamageClips[1] == null)
                skeletonDamageClips[1] = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Sleketon_damage/Удар по скелету 2.mp3");
            if (skeletonDamageClips[2] == null)
                skeletonDamageClips[2] = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Sleketon_damage/Удар по скелету 3.mp3");
            if (skeletonDamageClips[3] == null)
                skeletonDamageClips[3] = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Sleketon_damage/Удар по скелету 4.mp3");

            if (skeletonDeathClip == null)
                skeletonDeathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Skeleton_die/Смерть скелета.mp3");

            if (probingThrustClip == null)
                probingThrustClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Magic/Прощупывающий выпад.mp3");

            if (abyssalTrapClip == null)
                abyssalTrapClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Magic/Глубинная печать.mp3");

            if (gravityAnchorClip == null)
                gravityAnchorClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Magic/Гравитационный якорь.mp3");

            if (tacticalSmokeClip == null)
                tacticalSmokeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Magic/дым.mp3");

            if (kineticLaunchClip == null)
                kineticLaunchClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Magic/Кинечитеский подброс_[cut_1sec].mp3");

            if (directionalSpikesClip == null)
                directionalSpikesClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Magic/Направленные шипы.mp3");

            if (magicHarpoonClip == null)
                magicHarpoonClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Magic/Магический гарпун.mp3");

            if (fanGuardClip == null)
                fanGuardClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Magic/Веерная защита.mp3");
#endif
        }

        // ==========================================
        // ПЕРЕДВИЖЕНИЕ (MOVE)
        // ==========================================

        /// <summary>
        /// Обновляет звук передвижения игрока по земле.
        /// Если зажат Shift и игрок бежит — играет звук бега (Бег.mp3).
        /// Иначе при обычной ходьбе — играет звук шага (Шаг.mp3).
        /// При остановке или в воздухе звук мгновенно выключается.
        /// </summary>
        public void UpdateMovementAudio(bool isGrounded, bool isMoving, bool isSprinting, float currentSpeedX)
        {
            if (_movementSource == null) InitializeAudioSources();
            if (_movementSource == null) return;

            if (!isGrounded || !isMoving || Mathf.Abs(currentSpeedX) < 0.15f)
            {
                if (_movementSource.isPlaying)
                {
                    _movementSource.Stop();
                }
                return;
            }

            AudioClip desiredClip = isSprinting ? runClip : walkClip;
            if (desiredClip == null)
            {
                EnsureClipsLoaded();
                desiredClip = isSprinting ? runClip : walkClip;
            }

            if (desiredClip == null) return;

            float targetVolume = masterVolume * sfxVolume * footstepsVolume * (isSprinting ? 1.0f : 0.85f);
            float targetPitch = isSprinting
                ? Mathf.Clamp(Mathf.Abs(currentSpeedX) / 6.0f, 0.95f, 1.25f)
                : Mathf.Clamp(Mathf.Abs(currentSpeedX) / 3.4f, 0.9f, 1.15f);

            if (_movementSource.clip != desiredClip || !_movementSource.isPlaying)
            {
                _movementSource.clip = desiredClip;
                _movementSource.volume = targetVolume;
                _movementSource.pitch = targetPitch;
                _movementSource.loop = true;
                _movementSource.Play();
            }
            else
            {
                _movementSource.volume = targetVolume;
                _movementSource.pitch = targetPitch;
            }
        }

        /// <summary>
        /// Воспроизводит звук прыжка.
        /// </summary>
        public void PlayJump()
        {
            if (_movementSource != null && _movementSource.isPlaying)
            {
                _movementSource.Stop();
            }
            PlayClip(jumpClip, 0.9f, 0.96f, 1.04f);
        }

        // ==========================================
        // БЛИЖНИЙ БОЙ И ЗАЩИТА (FIGHT)
        // ==========================================

        /// <summary>
        /// Воспроизводит взмах/удар клинком игрока.
        /// </summary>
        public void PlayPlayerAttack()
        {
            PlayClip(attackSwingClip, 0.95f, 0.94f, 1.06f);
        }

        /// <summary>
        /// Воспроизводит звук идеального парирования.
        /// </summary>
        public void PlayParry()
        {
            PlayClip(parryClip, 1.0f, 0.98f, 1.02f);
        }

        /// <summary>
        /// Воспроизводит звук поглощения удара блоком.
        /// </summary>
        public void PlayBlockHit()
        {
            PlayClip(blockHitClip, 0.95f, 0.95f, 1.05f);
        }

        /// <summary>
        /// Воспроизводит звук оглушения противника (парирование / Stamina Break).
        /// </summary>
        public void PlayEnemyStun()
        {
            PlayClip(enemyStunClip, 0.95f, 0.97f, 1.03f);
        }

        // ==========================================
        // УРОН И СМЕРТЬ ВРАГОВ (SKELETON)
        // ==========================================

        /// <summary>
        /// Воспроизводит случайный удар по скелету из 4 вариаций (без повтора подряд).
        /// </summary>
        public void PlayEnemyHit()
        {
            if (skeletonDamageClips == null || skeletonDamageClips.Length == 0)
            {
                EnsureClipsLoaded();
            }

            var validClips = new List<AudioClip>();
            for (int i = 0; i < skeletonDamageClips.Length; i++)
            {
                if (skeletonDamageClips[i] != null) validClips.Add(skeletonDamageClips[i]);
            }

            if (validClips.Count == 0) return;

            int nextIndex;
            if (validClips.Count == 1)
            {
                nextIndex = 0;
            }
            else
            {
                do
                {
                    nextIndex = UnityEngine.Random.Range(0, validClips.Count);
                } while (nextIndex == _lastSkeletonHitIndex && validClips.Count > 1);
            }

            _lastSkeletonHitIndex = nextIndex;
            PlayClip(validClips[nextIndex], 0.95f, 0.92f, 1.08f);
        }

        /// <summary>
        /// Воспроизводит звук гибели врага.
        /// </summary>
        public void PlayEnemyDeath()
        {
            PlayClip(skeletonDeathClip, 1.0f, 0.97f, 1.03f);
        }

        // ==========================================
        // МАГИЧЕСКИЕ СПОСОБНОСТИ ТАКТИКА (MAGIC)
        // ==========================================

        /// <summary>
        /// Воспроизводит заклинание Тактика по направлению компаса (8 способностей).
        /// </summary>
        public void PlayTacticianAbility(Direction8 dir)
        {
            AudioClip clip = null;

            switch (dir)
            {
                case Direction8.Right:     // ➡️ Прощупывающий выпад
                    clip = probingThrustClip;
                    break;

                case Direction8.Down:      // ⬇️ Глубинная печать
                    clip = abyssalTrapClip;
                    break;

                case Direction8.Up:        // ⬆️ Гравитационный якорь
                    clip = gravityAnchorClip;
                    break;

                case Direction8.Left:      // ⬅️ Дымовой отход
                    clip = tacticalSmokeClip;
                    break;

                case Direction8.UpRight:   // ↗️ Кинетический подброс
                    clip = kineticLaunchClip;
                    break;

                case Direction8.DownRight: // ↘️ Направленные шипы
                    clip = directionalSpikesClip;
                    break;

                case Direction8.DownLeft:  // ↙️ Магический гарпун
                    clip = magicHarpoonClip;
                    break;

                case Direction8.UpLeft:    // ↖️ Веерная защита
                    clip = fanGuardClip;
                    break;
            }

            if (clip == null)
            {
                EnsureClipsLoaded();
                switch (dir)
                {
                    case Direction8.Right: clip = probingThrustClip; break;
                    case Direction8.Down: clip = abyssalTrapClip; break;
                    case Direction8.Up: clip = gravityAnchorClip; break;
                    case Direction8.Left: clip = tacticalSmokeClip; break;
                    case Direction8.UpRight: clip = kineticLaunchClip; break;
                    case Direction8.DownRight: clip = directionalSpikesClip; break;
                    case Direction8.DownLeft: clip = magicHarpoonClip; break;
                    case Direction8.UpLeft: clip = fanGuardClip; break;
                }
            }

            if (clip != null)
            {
                PlayClip(clip, 1.0f, 0.97f, 1.03f);
            }
        }

        // ==========================================
        // ВНУТРЕННИЙ ПУЛ ДЛЯ ОДНОКРАТНЫХ ЗВУКОВ
        // ==========================================

        private void PlayClip(AudioClip clip, float volume = 1f, float minPitch = 0.95f, float maxPitch = 1.05f)
        {
            if (clip == null) return;
            if (_sfxPool.Count == 0) InitializeAudioSources();
            if (_sfxPool.Count == 0) return;

            var src = _sfxPool[_poolIndex];
            _poolIndex = (_poolIndex + 1) % _sfxPool.Count;

            src.clip = clip;
            src.volume = masterVolume * sfxVolume * volume;
            src.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            src.loop = false;
            src.Play();
        }
    }
}
