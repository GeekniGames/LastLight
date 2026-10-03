using LastLight.Monsters;
using LastLight.Player;
using UnityEngine;

namespace LastLight.Maze
{
    /// <summary>
    /// 모든 함정의 공통 베이스. 발동 감지(Trigger Collider2D)와 일회성/반복 처리,
    /// 플레이어/몬스터 영향 여부만 여기서 공통으로 처리하고, 실제 효과는 하위 클래스가
    /// OnTriggered()만 구현하면 된다. 함정 종류를 늘릴 때마다 이 틀을 새로 짤 필요가 없게 하려는 목적.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class TrapBase : MonoBehaviour
    {
        [Header("Trap Base")]
        [Tooltip("켜면 한 번 발동한 뒤 다시는 작동하지 않는다. 꺼두면 쿨다운만 지나면 몇 번이고 재발동(반복형 함정).")]
        [SerializeField] private bool oneShot = true;
        [Tooltip("반복형 함정일 때, 같은 대상이 다시 발동시킬 수 있기까지의 최소 간격(초).")]
        [SerializeField] private float reuseCooldown = 1f;
        [SerializeField] private bool affectsPlayer = true;
        [SerializeField] private bool affectsMonsters = false;

        [Header("Visual")]
        [Tooltip("함정 이미지를 그리는 렌더러.")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("꺼진(소모된) 상태의 공통 이미지.")]
        [SerializeField] private Sprite baseSprite;
        [Tooltip("켜진 상태의 이미지. 함정 종류마다 다르게 넣으면 됨.")]
        [SerializeField] private Sprite armedSprite;

        private bool _consumed;
        private float _cooldownTimer;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        // 풀링으로 재사용되는 경우를 대비해, 다시 켜질 때 소모/쿨다운 상태를 초기화한다.
        protected virtual void OnEnable()
        {
            _consumed = false;
            _cooldownTimer = 0f;

            if (spriteRenderer != null && armedSprite != null)
                spriteRenderer.sprite = armedSprite;
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_consumed || _cooldownTimer > 0f) return;

            bool isPlayer = other.GetComponent<PlayerController>() != null;
            bool isMonster = !isPlayer && other.GetComponent<IMonsterMarker>() != null;

            if (!isPlayer && !isMonster) return; // 함정과 무관한 콜라이더(벽 등)
            if (isPlayer && !affectsPlayer) return;
            if (isMonster && !affectsMonsters) return;

            OnTriggered(other.gameObject, isPlayer);

            if (oneShot)
            {
                _consumed = true;
                OnConsumed();
            }
            else
            {
                _cooldownTimer = reuseCooldown;
            }
        }

        /// <summary>실제 함정 효과. isPlayer가 false면 몬스터가 밟은 것.</summary>
        protected abstract void OnTriggered(GameObject target, bool isPlayer);

        /// <summary>일회성 함정이 소모된 직후 호출. 오브젝트는 그대로 두고 베이스(꺼짐) 이미지로 바꿔서
        /// 자리에 남아있지만 더는 반응하지 않는 '다 쓴 함정'처럼 보이게 한다.</summary>
        protected virtual void OnConsumed()
        {
            if (spriteRenderer != null && baseSprite != null)
                spriteRenderer.sprite = baseSprite;
        }
    }
}