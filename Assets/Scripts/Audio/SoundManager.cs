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
        private static bool _applicationIsQuitting = false;
        private static SoundManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnSubsystemRegistration()
        {
            _applicationIsQuitting = false;
            _instance = null;
        }

        public static SoundManager Instance
        {
            get
            {
                if (_applicationIsQuitting) return null;

                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<SoundManager>();
                    if (_instance == null)
                    {
                        var prefab = Resources.Load<GameObject>("SoundManager");
                        if (prefab != null)
                        {
                            var go = Instantiate(prefab);
                            go.name = "[SoundManager]";
                            _instance = go.GetComponent<SoundManager>();
                        }
                        else
                        {
                            var go = new GameObject("[SoundManager]");
                            _instance = go.AddComponent<SoundManager>();
                        }
                    }

                    if (Application.isPlaying && _instance != null && _instance.transform.parent == null)
                    {
                        DontDestroyOnLoad(_instance.gameObject);
                    }
                }
                return _instance;
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitializeAudioSystem()
        {
            _applicationIsQuitting = false;
            var sm = Instance;
            if (sm != null)
            {
                sm.InitializeAudioSources();
                sm.EnsureClipsLoaded();
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
        [Tooltip("Звук взрыва (провал огненной магии)")]
        [SerializeField] private AudioClip explosionClip;

        [Header("--- Magic & Effects Audio ---")]
        [Tooltip("Звук успешного запуска фаербола")]
        [SerializeField] private AudioClip fireballClip;
        [Tooltip("Звук эффекта горения / пламени")]
        [SerializeField] private AudioClip fireEffectClip;
        [Tooltip("Звук клубящегося дыма / тумана")]
        [SerializeField] private AudioClip smokeClip;

        [Header("--- Enemy (Skeleton) Audio ---")]
        [Tooltip("Вариации ударов по скелету")]
        [SerializeField] private AudioClip[] skeletonDamageClips = new AudioClip[4];
        [Tooltip("Звук гибели скелета")]
        [SerializeField] private AudioClip skeletonDeathClip;

        

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
                CopyClipsIfMissing(_instance, this);
                Destroy(gameObject);
                return;
            }

            InitializeAudioSources();
            EnsureClipsLoaded();
        }

        private static void CopyClipsIfMissing(SoundManager target, SoundManager source)
        {
            if (target == null || source == null) return;
            if (target.walkClip == null) target.walkClip = source.walkClip;
            if (target.runClip == null) target.runClip = source.runClip;
            if (target.jumpClip == null) target.jumpClip = source.jumpClip;
            if (target.attackSwingClip == null) target.attackSwingClip = source.attackSwingClip;
            if (target.parryClip == null) target.parryClip = source.parryClip;
            if (target.blockHitClip == null) target.blockHitClip = source.blockHitClip;
            if (target.enemyStunClip == null) target.enemyStunClip = source.enemyStunClip;
            if (target.explosionClip == null) target.explosionClip = source.explosionClip;
            if (target.skeletonDeathClip == null) target.skeletonDeathClip = source.skeletonDeathClip;
            if (target.fireballClip == null) target.fireballClip = source.fireballClip;
            if (target.fireEffectClip == null) target.fireEffectClip = source.fireEffectClip;
            if (target.smokeClip == null) target.smokeClip = source.smokeClip;
            

            if (target.skeletonDamageClips == null || target.skeletonDamageClips.Length < 4)
                target.skeletonDamageClips = new AudioClip[4];
            if (source.skeletonDamageClips != null)
            {
                for (int i = 0; i < Mathf.Min(target.skeletonDamageClips.Length, source.skeletonDamageClips.Length); i++)
                {
                    if (target.skeletonDamageClips[i] == null)
                        target.skeletonDamageClips[i] = source.skeletonDamageClips[i];
                }
            }
        }

        private void OnEnable()
        {
            Combat.Settings.CombatSettingsManager.OnSettingsChanged += HandleSettingsChanged;
        }

        private void OnDisable()
        {
            Combat.Settings.CombatSettingsManager.OnSettingsChanged -= HandleSettingsChanged;
        }

        private void HandleSettingsChanged(Combat.Settings.WheelSettingsData data)
        {
            if (data != null)
            {
                ApplyVolumeSettings(data.masterVolume, data.sfxVolume, data.footstepsVolume, data.isMuted);
            }
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

        public void StopAllSounds()
        {
            if (_movementSource != null)
            {
                _movementSource.Stop();
            }

            if (_sfxPool != null)
            {
                for (int i = 0; i < _sfxPool.Count; i++)
                {
                    if (_sfxPool[i] != null)
                    {
                        _sfxPool[i].Stop();
                    }
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
            if (explosionClip == null)
                explosionClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Fight/Взрыв 2.mp3") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Fight/Взрыв.mp3");

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

            if (fireballClip == null)
                fireballClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Fight/Файербол.mp3");
            if (fireEffectClip == null)
                fireEffectClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Fight/Эффект огня.mp3");
            if (smokeClip == null)
                smokeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Fight/Дым.mp3");

            
#endif

            // Рантайм-фоллбэк для автономных билдов (Standalone Build) через Resources
            if (walkClip == null) walkClip = Resources.Load<AudioClip>("Sound/Move/Шаг");
            if (runClip == null) runClip = Resources.Load<AudioClip>("Sound/Move/Бег");
            if (jumpClip == null) jumpClip = walkClip;
            if (attackSwingClip == null) attackSwingClip = Resources.Load<AudioClip>("Sound/Fight/Удар");
            if (parryClip == null) parryClip = Resources.Load<AudioClip>("Sound/Fight/Парирование");
            if (blockHitClip == null) blockHitClip = Resources.Load<AudioClip>("Sound/Fight/Звук удара в блок");
            if (enemyStunClip == null) enemyStunClip = Resources.Load<AudioClip>("Sound/Fight/Оглушения противника");
            if (explosionClip == null) explosionClip = Resources.Load<AudioClip>("Sound/Fight/Взрыв 2") ?? Resources.Load<AudioClip>("Sound/Fight/Взрыв");

            if (skeletonDamageClips == null || skeletonDamageClips.Length < 4)
                skeletonDamageClips = new AudioClip[4];

            if (skeletonDamageClips[0] == null) skeletonDamageClips[0] = Resources.Load<AudioClip>("Sound/Sleketon_damage/Удар по скелету 1");
            if (skeletonDamageClips[1] == null) skeletonDamageClips[1] = Resources.Load<AudioClip>("Sound/Sleketon_damage/Удар по скелету 2");
            if (skeletonDamageClips[2] == null) skeletonDamageClips[2] = Resources.Load<AudioClip>("Sound/Sleketon_damage/Удар по скелету 3");
            if (skeletonDamageClips[3] == null) skeletonDamageClips[3] = Resources.Load<AudioClip>("Sound/Sleketon_damage/Удар по скелету 4");

            if (skeletonDeathClip == null) skeletonDeathClip = Resources.Load<AudioClip>("Sound/Skeleton_die/Смерть скелета");

            if (fireballClip == null) fireballClip = Resources.Load<AudioClip>("Sound/Fight/Файербол");
            if (fireEffectClip == null) fireEffectClip = Resources.Load<AudioClip>("Sound/Fight/Эффект огня");
            if (smokeClip == null) smokeClip = Resources.Load<AudioClip>("Sound/Fight/Дым");

            
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
            if (jumpClip != null)
            {
                PlayClip(jumpClip, 0.9f, 0.96f, 1.04f);
            }
            else if (walkClip != null)
            {
                PlayClip(walkClip, 0.85f, 1.25f, 1.35f);
            }
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

        /// <summary>
        /// Воспроизводит звук взрыва (при провале огненной магии или детонации).
        /// </summary>
        public void PlayExplosion(float volume = 1f)
        {
            if (explosionClip == null) EnsureClipsLoaded();
            PlayClip(explosionClip, volume, 0.95f, 1.05f);
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
        // МАГИЯ И ЭФФЕКТЫ (MAGIC & EFFECTS)
        // ==========================================

        /// <summary>
        /// Воспроизводит звук успешного запуска фаербола (Файербол.mp3).
        /// </summary>
        public void PlayFireball(float volume = 1f)
        {
            if (fireballClip == null) EnsureClipsLoaded();
            PlayClip(fireballClip, volume, 0.97f, 1.03f);
        }

        /// <summary>
        /// Воспроизводит звук эффекта огня / горения (Эффект огня.mp3).
        /// </summary>
        public void PlayFireEffect(float volume = 1f, float minPitch = 0.95f, float maxPitch = 1.05f)
        {
            if (fireEffectClip == null) EnsureClipsLoaded();
            PlayClip(fireEffectClip, volume, minPitch, maxPitch);
        }

        /// <summary>
        /// Воспроизводит звук клубящегося дыма / тумана (Дым.mp3).
        /// </summary>
        public void PlaySmoke(float volume = 1f, float minPitch = 0.95f, float maxPitch = 1.05f)
        {
            if (smokeClip == null) EnsureClipsLoaded();
            PlayClip(smokeClip, volume, minPitch, maxPitch);
        }

        // ==========================================
        // МАГИЧЕСКИЕ СПОСОБНОСТИ ТАКТИКА (MAGIC)
        // ==========================================

        /// <summary>
        /// Воспроизводит заклинание Тактика по направлению компаса (8 способностей).
        /// </summary>
        

        // ==========================================
        // ВНУТРЕННИЙ ПУЛ ДЛЯ ОДНОКРАТНЫХ ЗВУКОВ
        // ==========================================

        private void PlayClip(AudioClip clip, float volume = 1f, float minPitch = 0.95f, float maxPitch = 1.05f)
        {
            if (clip == null) return;
            if (_sfxPool.Count == 0) InitializeAudioSources();
            if (_sfxPool.Count == 0) return;

            if (clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }

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
