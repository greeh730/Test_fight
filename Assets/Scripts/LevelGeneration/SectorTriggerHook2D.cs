using System;
using UnityEngine;
using Combat.Player;

namespace LevelGeneration
{
    /// <summary>
    /// Вспомогательный триггер пересечения игроком порога боевого сектора.
    /// Вынесен в отдельный файл для корректной сериализации Unity.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class SectorTriggerHook2D : MonoBehaviour
    {
        public Action OnPlayerEntered;

        private void OnTriggerEnter2D(Collider2D other)
        {
            bool isPlayer = other.CompareTag("Player") ||
                            other.GetComponentInParent<PlayerController2D>() != null ||
                            other.gameObject.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isPlayer)
            {
                OnPlayerEntered?.Invoke();
            }
        }
    }
}
