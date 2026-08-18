using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARPGCombat.Core
{
    /// <summary>
    /// 事件中心：基于观察者模式的全局消息广播系统。
    ///
    /// 设计动机（面试点：模块零耦合）：
    /// 战斗系统中典型的链式反应是 "攻击命中 → 伤害计算 → 飘字显示 → 血条更新 → 击杀计数"。
    /// 如果让攻击逻辑直接调用 DamageSystem、UI、Counter，会产生大量硬依赖，模块无法独立测试/替换。
    /// 事件中心让发布者只管 Emit("AttackHit", damage)，订阅者各自 On("AttackHit", ...) 处理，
    /// 双方互不认识，实现真正的"发布-订阅"解耦。
    ///
    /// 存储结构：Dictionary&lt;string, Action&lt;object&gt;&gt;
    /// - key = 事件名（字符串约定，便于跨模块沟通）
    /// - value = Action&lt;object&gt; 多播委托，同一事件可挂多个监听者
    /// </summary>
    public class EventCenter : Singleton<EventCenter>
    {
        private readonly Dictionary<string, Action<object>> _events =
            new Dictionary<string, Action<object>>();

        /// <summary>
        /// 注册监听。同一个 callback 重复注册会被多播委托合并调用多次，使用时注意去重。
        /// </summary>
        public void On(string eventName, Action<object> callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            if (_events.TryGetValue(eventName, out var existing))
                _events[eventName] = existing + callback; // 多播委托拼接
            else
                _events.Add(eventName, callback);
        }

        /// <summary>
        /// 注销监听。务必在 OnDestroy 时调用，否则会造成"悬挂委托"导致内存泄漏或重复触发。
        /// </summary>
        public void Off(string eventName, Action<object> callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            if (!_events.TryGetValue(eventName, out var existing)) return;

            var newDelegate = existing - callback;
            if (newDelegate == null)
                _events.Remove(eventName); // 没人监听了，直接移除 key，避免字典膨胀
            else
                _events[eventName] = newDelegate;
        }

        /// <summary>
        /// 触发事件。data 可为 null（无参事件）。
        /// 多播委托的 Invoke 内部是对当前委托链的快照调用，回调里即使再次 On/Off 同一事件也安全。
        /// </summary>
        public void Emit(string eventName, object data = null)
        {
            if (_events.TryGetValue(eventName, out var existing))
                existing?.Invoke(data);
        }

        /// <summary>
        /// 清空所有监听。通常只在场景切换/重开游戏时调用。
        /// </summary>
        public void Clear()
        {
            _events.Clear();
        }

        protected override void OnDestroy()
        {
            // 单例销毁时把所有委托清掉，避免持有已销毁对象的引用
            _events.Clear();
            base.OnDestroy();
        }
    }
}
