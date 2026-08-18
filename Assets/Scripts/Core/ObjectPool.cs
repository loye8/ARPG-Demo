using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ARPGCombat.Core
{
    /// <summary>
    /// 泛型对象池（面试点：性能优化、零 GC）。
    ///
    /// 设计动机：
    /// ARPG 战斗中"攻击命中特效、伤害飘字、子弹"会被频繁创建/销毁，
    /// 每次 Instantiate/Destroy 都会产生 GC 和 CPU 开销，长时间战斗会卡顿。
    /// 对象池预生成一批对象，用完归还而不是销毁，下次 Get 复用，达到"零 GC"运行时分配。
    ///
    /// 设计要点：
    /// - Queue&lt;T&gt;：FIFO 结构，最先归还的最先被取用，缓存友好。
    /// - Func&lt;T&gt; createFunc：创建工厂由调用方传入，对象池不关心具体怎么 new/Instantiate。
    /// - Action&lt;T&gt; onGet / onReturn：获取时启用、归还时禁用并复位位置，状态由调用方决定。
    /// - maxSize 上限：防止池无限膨胀，超过上限的对象直接 Destroy。
    /// </summary>
    public class ObjectPool<T> where T : class
    {
        private readonly Queue<T> _pool = new Queue<T>();
        private readonly Func<T> _createFunc;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onReturn;
        private readonly int _maxSize;

        // 统计字段，便于调试和性能分析（面试时可以说"我加了 CountAll/Active/Inactive 监控")
        public int CountAll { get; private set; }
        public int CountActive => CountAll - CountInactive;
        public int CountInactive => _pool.Count;

        public ObjectPool(Func<T> createFunc, Action<T> onGet = null, Action<T> onReturn = null, int maxSize = 1000)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _onGet = onGet;
            _onReturn = onReturn;
            _maxSize = maxSize;
        }

        /// <summary>
        /// 从池中获取一个对象。池为空时调用工厂创建新对象。
        /// </summary>
        public T Get()
        {
            T obj;
            if (_pool.Count > 0)
            {
                obj = _pool.Dequeue();
            }
            else
            {
                obj = _createFunc();
                CountAll++;
            }
            _onGet?.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// 归还对象到池中。超出容量上限则销毁（避免内存占用失控）。
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null) return;

            _onReturn?.Invoke(obj);

            if (_pool.Count < _maxSize)
            {
                _pool.Enqueue(obj);
            }
            else
            {
                // 超出上限：若对象是 Unity Object 则调用 Destroy 释放原生资源
                if (obj is Object uo)
                    Object.Destroy(uo);
                CountAll--;
            }
        }

        /// <summary>
        /// 预热：提前创建 count 个对象放入池中，避免运行时首次创建卡顿。
        /// </summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var obj = _createFunc();
                CountAll++;
                _onReturn?.Invoke(obj);
                _pool.Enqueue(obj);
            }
        }

        /// <summary>
        /// 清空池并销毁所有缓存对象。场景切换时调用。
        /// </summary>
        public void Clear()
        {
            foreach (var item in _pool)
            {
                if (item is Object uo)
                    Object.Destroy(uo);
            }
            _pool.Clear();
            CountAll = 0;
        }
    }
}
