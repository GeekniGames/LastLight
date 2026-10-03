using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Core
{
    /// <summary>
    /// 간단한 제네릭 오브젝트 풀.
    /// 층이 바뀔 때마다 벽 오브젝트를 매번 Instantiate/Destroy하면 GC 할당과 인스턴스화 비용이 계속 쌓이므로,
    /// 한 번 만든 오브젝트를 비활성화만 해뒀다가 재사용한다.
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _inactive = new Stack<T>();
        private readonly List<T> _active = new List<T>();

        public ObjectPool(T prefab, Transform parent, int prewarmCount = 0)
        {
            _prefab = prefab;
            _parent = parent;

            for (int i = 0; i < prewarmCount; i++)
            {
                T instance = Object.Instantiate(_prefab, _parent);
                instance.gameObject.SetActive(false);
                _inactive.Push(instance);
            }
        }

        public T Get()
        {
            T instance = _inactive.Count > 0
                ? _inactive.Pop()
                : Object.Instantiate(_prefab, _parent);

            instance.gameObject.SetActive(true);
            _active.Add(instance);
            return instance;
        }

        /// <summary>현재 활성화된 모든 인스턴스를 비활성화해 풀로 되돌린다(층 재생성 시 호출).</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                _active[i].gameObject.SetActive(false);
                _inactive.Push(_active[i]);
            }
            _active.Clear();
        }
    }
}