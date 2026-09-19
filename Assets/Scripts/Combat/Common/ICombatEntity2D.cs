using UnityEngine;

namespace Combat.Common
{
    /// <summary>
    /// Единый интерфейс для любой боевой сущности (Враг, Манекен и др.), способной получать удары и подвергаться контролю толпы (CC).
    /// Расширяет IHurtboxTarget2D.
    /// </summary>
    public interface ICombatEntity2D : IHurtboxTarget2D
    {
        Transform transform { get; }
        Rigidbody2D Rigidbody { get; }
        GameObject gameObject { get; }
        bool IsDead { get; }
        float CurrentHealth { get; }

        void ApplyVulnerabilityMark(float duration, float multiplier);
        void ApplyRoot(float duration);
        void ApplyGravitySuspension(float duration, Vector2 center);
        void PullTowards(Vector2 targetPos, float speed);
        void Disorient(float duration);
    }
}
