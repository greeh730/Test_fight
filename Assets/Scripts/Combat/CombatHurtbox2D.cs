using System;
using UnityEngine;

namespace Combat
{
    public interface IHurtboxTarget2D
    {
        void TakeHit(AttackConfig attack, CombatZone hitZone, Vector2 hitPoint, Vector2 knockbackDirection);
    }

    /// <summary>
    /// Компонент зоны уязвимости (Hurtbox) на персонаже или манекене.
    /// Определяет конкретную зону тела (Low, Mid, High).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CombatHurtbox2D : MonoBehaviour
    {
        [Tooltip("К какой зоне тела относится данный коллайдер")]
        [SerializeField] private CombatZone bodyZone = CombatZone.Mid;

        [Tooltip("Ссылка на родительский контроллер получения урона")]
        [SerializeField] private MonoBehaviour targetReceiver;

        public CombatZone BodyZone => bodyZone;

        public void Initialize(CombatZone zone, MonoBehaviour receiver)
        {
            bodyZone = zone;
            targetReceiver = receiver;
        }

        public IHurtboxTarget2D GetTargetReceiver()
        {
            var receiver = targetReceiver as IHurtboxTarget2D;
            if (receiver == null && targetReceiver != null)
            {
                receiver = targetReceiver.GetComponent<IHurtboxTarget2D>();
            }
            if (receiver == null)
            {
                receiver = GetComponentInParent<IHurtboxTarget2D>();
            }
            return receiver;
        }

        public void ReceiveHit(AttackConfig attack, Vector2 hitPoint, Vector2 knockbackDirection)
        {
            var receiver = GetTargetReceiver();
            receiver?.TakeHit(attack, bodyZone, hitPoint, knockbackDirection);
        }
    }
}
