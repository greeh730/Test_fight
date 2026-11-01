// Assets/Scripts/Fencing/FencingPlayer.cs
using UnityEngine;

namespace Fencing
{
    /// <summary>
    /// Тестовый персонаж игрока.
    /// Визуализируется как 2D-прямоугольник, слушает события OnAttack от FencingWheel
    /// и активирует видимый хитбокс меча в заданном направлении.
    /// </summary>
    public class FencingPlayer : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Ссылка на колесо фехтования")]
        [SerializeField] private FencingWheel fencingWheel;

        [Tooltip("Хитбокс рассечения мечом")]
        [SerializeField] private SwordHitbox swordHitbox;

        private SpriteRenderer spriteRenderer;
        private Combat.Player.PlayerAnimationController2D animController;

        public StanceType CurrentStance => (fencingWheel != null) ? fencingWheel.CurrentStance : StanceType.Fencing;

        public void ToggleStance()
        {
            if (fencingWheel != null) fencingWheel.ToggleStance();
        }

        private void Awake()
        {
            if (fencingWheel == null)
            {
#if UNITY_6000_0_OR_NEWER
                fencingWheel = FindAnyObjectByType<FencingWheel>();
#else
                fencingWheel = FindObjectOfType<FencingWheel>();
#endif
            }

            if (swordHitbox == null)
            {
                swordHitbox = GetComponentInChildren<SwordHitbox>();
                if (swordHitbox == null)
                {
                    GameObject hitboxObj = new GameObject("SwordHitbox");
                    hitboxObj.transform.SetParent(transform, false);
                    swordHitbox = hitboxObj.AddComponent<SwordHitbox>();
                }
            }

            animController = GetComponent<Combat.Player.PlayerAnimationController2D>();

            SetupPlayerVisual();
        }

        private void OnEnable()
        {
            if (fencingWheel != null)
            {
                fencingWheel.OnAttack += HandleAttack;
                fencingWheel.OnStanceChanged += HandleStanceChanged;
            }
        }

        private void OnDisable()
        {
            if (fencingWheel != null)
            {
                fencingWheel.OnAttack -= HandleAttack;
                fencingWheel.OnStanceChanged -= HandleStanceChanged;
            }
        }

        private void HandleStanceChanged(StanceType stance)
        {
            string stanceName = (stance == StanceType.Magic) ? "МАГИЯ" : "ФЕХТОВАНИЕ";
            string colorHex = (stance == StanceType.Magic) ? "#38B6FF" : "#FF4444";
            Debug.Log($"<color={colorHex}>[STANCE]</color> Стойка изменена на: <b>{stanceName}</b>");

            string popupText = (stance == StanceType.Magic) ? "🔮 МАГИЧЕСКАЯ СТОЙКА 🔮" : "⚔️ СТОЙКА ФЕХТОВАНИЯ ⚔️";
            Color popupColor = (stance == StanceType.Magic) ? new Color(0.25f, 0.75f, 1f, 1f) : new Color(1f, 0.3f, 0.3f, 1f);
            StartCoroutine(StancePopupRoutine(popupText, popupColor));
        }

        private System.Collections.IEnumerator StancePopupRoutine(string text, Color color)
        {
            GameObject popupObj = new GameObject("StancePopup");
            popupObj.transform.position = transform.position + new Vector3(0f, 1.4f, 0f);

            TextMesh textMesh = popupObj.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.fontSize = 24;
            textMesh.characterSize = 0.035f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = color;

            MeshRenderer mr = popupObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 15;

            Vector3 startPos = popupObj.transform.position;
            Vector3 endPos = startPos + new Vector3(0f, 0.7f, 0f);
            float duration = 0.65f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (popupObj != null)
                {
                    popupObj.transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
                    float scale = (t < 0.2f) ? Mathf.Lerp(0.5f, 1.15f, t / 0.2f) : Mathf.Lerp(1.15f, 1.0f, (t - 0.2f) / 0.8f);
                    popupObj.transform.localScale = Vector3.one * scale;

                    Color c = textMesh.color;
                    c.a = Mathf.Lerp(1f, 0f, t * t);
                    textMesh.color = c;
                }
                yield return null;
            }

            if (popupObj != null)
            {
                Destroy(popupObj);
            }
        }

        private void HandleAttack(AttackDirection direction)
        {
            // В магической стойке игрок не может бить мечом
            if (CurrentStance == StanceType.Magic)
            {
                return;
            }

            string arrow = FencingWheelUI.GetDirectionArrow(direction);
            Debug.Log($"<color=#33CCFF>[Fencing Slash]</color> Игрок наносит удар в направлении: <b>{direction} {arrow}</b>");

            if (swordHitbox != null)
            {
                float angle = (fencingWheel != null) ? fencingWheel.GetSectorCenterAngle(direction) : direction.ToAngle();
                swordHitbox.PerformSlash(direction, angle);
            }

            if (animController == null) animController = GetComponent<Combat.Player.PlayerAnimationController2D>();
            if (animController != null)
            {
                animController.PlayAttackAnimation(direction);
            }
            Vector2 dirVec = direction.ToVector2();
            if (Mathf.Abs(dirVec.x) > 0.05f && spriteRenderer != null)
            {
                spriteRenderer.flipX = (dirVec.x < 0f);
            }

            if (Combat.Audio.SoundManager.Instance != null)
            {
                Combat.Audio.SoundManager.Instance.PlayPlayerAttack();
            }
        }

        private void SetupPlayerVisual()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            // Если у персонажа уже настроен спрайт (или анимации), не перезаписываем его
            if (spriteRenderer.sprite != null) return;
            int width = 32;
            int height = 56;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            Color knightBlue = new Color(0.2f, 0.45f, 0.85f, 1f);
            Color border = new Color(0.1f, 0.2f, 0.4f, 1f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = (x == 0 || x == width - 1 || y == 0 || y == height - 1);
                    tex.SetPixel(x, y, isBorder ? border : knightBlue);
                }
            }
            tex.Apply();

            spriteRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
            spriteRenderer.sortingOrder = 2;
        }

        /// <summary>
        /// Отображает всплывающее сообщение над игроком при успешном парировании.
        /// Текст плавно взлетает вверх, вспыхивает и исчезает.
        /// </summary>
        public void ShowParryFeedback()
        {
            if (FocusManager.Instance != null)
            {
                FocusManager.Instance.AddFocus(FocusManager.Instance.focusGainOnParry);
            }
            StartCoroutine(ParryPopupRoutine());
        }

        private System.Collections.IEnumerator ParryPopupRoutine()
        {
            GameObject popupObj = new GameObject("ParryPopup");
            popupObj.transform.position = transform.position + new Vector3(0f, 1.25f, 0f);

            TextMesh textMesh = popupObj.AddComponent<TextMesh>();
            textMesh.text = "✨ ПАРИРОВАНИЕ! ✨";
            textMesh.fontSize = 28;
            textMesh.characterSize = 0.038f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = new Color(0.2f, 1f, 0.85f, 1f); // Неоновый бирюзово-золотой

            MeshRenderer mr = popupObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 15;

            // Кратковременная вспышка игрока
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.6f, 0.9f, 1f, 1f);
            }

            Vector3 startPos = popupObj.transform.position;
            Vector3 endPos = startPos + new Vector3(0f, 0.85f, 0f);
            float duration = 0.75f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (popupObj != null)
                {
                    popupObj.transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
                    float scale = (t < 0.2f) ? Mathf.Lerp(0.5f, 1.25f, t / 0.2f) : Mathf.Lerp(1.25f, 1.0f, (t - 0.2f) / 0.8f);
                    popupObj.transform.localScale = Vector3.one * scale;

                    Color c = textMesh.color;
                    c.a = Mathf.Lerp(1f, 0f, t * t);
                    textMesh.color = c;
                }

                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.Lerp(new Color(0.6f, 0.9f, 1f, 1f), Color.white, t * 2f);
                }

                yield return null;
            }

            if (popupObj != null)
            {
                Destroy(popupObj);
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
        }

        /// <summary>
        /// Вызывается при пропуске удара от врага (если игрок не спарировал атаку).
        /// </summary>
        public void TakeEnemyHit(AttackDirection dir)
        {
            Debug.Log($"<color=#FF4444>[УДАР ВРАГА!]</color> Игрок пропустил удар в зону: <b>{dir}</b>!");
            StartCoroutine(HurtFlashRoutine());
        }

        private System.Collections.IEnumerator HurtFlashRoutine()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1f, 0.25f, 0.25f, 1f);
            }

            yield return new WaitForSeconds(0.14f);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
        }
    }
}
