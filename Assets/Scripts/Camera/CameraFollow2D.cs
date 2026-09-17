using System;
using UnityEngine;

namespace Combat.Cameras
{
    [DisallowMultipleComponent]
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("--- Target ---")]
        [SerializeField] private Transform target;

        [Header("--- Offsets & Damping ---")]
        [SerializeField] private Vector2 offset = new Vector2(0f, 1.2f);
        [SerializeField] private float smoothTime = 0.22f;

        [Header("--- Lookahead ---")]
        [SerializeField] private bool enableLookahead = true;
        [SerializeField] private float lookaheadDistance = 1.8f;
        [SerializeField] private float lookaheadSpeed = 3.5f;

        private Vector3 _currentVelocity = Vector3.zero;
        private float _currentLookaheadX = 0f;
        private float _targetLookaheadX = 0f;

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.Find("Player");
                if (player != null) target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Расчет lookahead
            if (enableLookahead)
            {
                var rb = target.GetComponent<Rigidbody2D>();
                if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.5f)
                {
                    _targetLookaheadX = Mathf.Sign(rb.linearVelocity.x) * lookaheadDistance;
                }
                _currentLookaheadX = Mathf.MoveTowards(_currentLookaheadX, _targetLookaheadX, Time.deltaTime * lookaheadSpeed);
            }
            else
            {
                _currentLookaheadX = 0f;
            }

            Vector3 targetPos = new Vector3(
                target.position.x + offset.x + _currentLookaheadX,
                target.position.y + offset.y,
                transform.position.z
            );

            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _currentVelocity, smoothTime);
        }
    }
}
