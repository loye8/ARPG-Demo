using UnityEngine;

namespace ARPGCombat.Core
{
    /// <summary>
    /// 泛型单例基类。
    /// 任何需要全局唯一访问的 MonoBehaviour 管理器都可以继承它。
    /// 设计要点：
    /// 1. 懒加载：首次访问 Instance 时才查找/创建，避免场景未加载就被实例化。
    /// 2. 重复实例保护：场景里若已存在同类型实例，新实例直接销毁自身。
    /// 3. DontDestroyOnLoad：切换场景时不被销毁，保证管理器生命周期 = 应用生命周期。
    /// 4. 退出标志：应用退出时禁止再访问 Instance，避免产生"幽灵"对象导致报错。
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        /// <summary>
        /// 全局唯一访问点。
        /// 使用双重检查锁（double-check locking）保证多线程访问安全。
        /// Unity API 只能在主线程调用，但 lock 仍可防止极端竞态下的重复创建。
        /// </summary>
        public static T Instance
        {
            get
            {
                // 应用退出后不再创建实例，防止 OnApplicationQuit 之后还有代码访问单例
                if (_applicationIsQuitting)
                    return null;

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 先在场景里找：如果开发者已经在 Inspector 里手动放好了实例，直接复用
                        _instance = FindObjectOfType<T>();

                        if (_instance == null)
                        {
                            // 场景里没有就新建一个 GameObject 并挂载组件
                            var go = new GameObject(typeof(T).Name);
                            _instance = go.AddComponent<T>();
                        }
                    }
                    return _instance;
                }
            }
        }

        /// <summary>
        /// 子类若需要重写 Awake，必须调用 base.Awake()，否则单例赋值逻辑会丢失。
        /// </summary>
        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                // 已经存在实例，销毁重复的自己
                Debug.LogWarning($"[Singleton] 检测到 {typeof(T).Name} 的重复实例，已销毁。");
                Destroy(gameObject);
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            // 只有当自己是当前实例时才清空，避免误清其他实例
            if (_instance == this)
                _instance = null;
        }
    }
}
