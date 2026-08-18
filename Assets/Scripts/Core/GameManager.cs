using System;
using UnityEngine;

namespace ARPGCombat.Core
{
    /// <summary>
    /// 游戏总管理器（面试点：单例 + 状态管理 + 框架初始化入口）。
    ///
    /// 职责：
    /// 1. 框架初始化：确保 EventCenter 等核心模块在游戏启动时被创建。
    /// 2. 全局游戏状态机：Boot → Playing → Paused → GameOver，状态变化通过事件中心广播。
    /// 3. 后续 Day 的敌人生成调度、关卡控制都会挂在这里。
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        /// <summary>
        /// 全局游戏状态。后续 FSM、UI、敌人生成都可以订阅 GameStateChanged 事件做响应。
        /// </summary>
        public enum GameState
        {
            Boot,       // 启动中（框架初始化）
            Playing,    // 战斗进行中
            Paused,     // 暂停
            GameOver    // 结束
        }

        public GameState CurrentState { get; private set; } = GameState.Boot;

        /// <summary>
        /// 状态变化 C# 事件（紧耦合场景用），同时也会通过 EventCenter 广播（解耦场景用）。
        /// 双轨制：给"知道 GameManager 存在的模块"用 event，给"完全解耦的模块"用 EventCenter。
        /// </summary>
        public event Action<GameState> OnStateChanged;

        protected override void Awake()
        {
            base.Awake();
            InitFramework();
        }

        private void Start()
        {
            // 框架初始化完成后切入 Playing
            ChangeState(GameState.Playing);
        }

        /// <summary>
        /// 框架初始化：强制访问各核心单例的 Instance，确保它们在游戏开始时就被创建。
        /// </summary>
        private void InitFramework()
        {
            // 访问 Instance 触发懒加载创建 EventCenter
            _ = EventCenter.Instance;
            Debug.Log("[GameManager] 框架初始化完成（EventCenter 已就绪）");
        }

        /// <summary>
        /// 切换游戏状态，并通过事件中心广播。
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            var oldState = CurrentState;
            CurrentState = newState;
            Debug.Log($"[GameManager] 状态切换：{oldState} → {newState}");

            // 紧耦合通知
            OnStateChanged?.Invoke(newState);

            // 解耦通知：UI、敌人 AI 等可以监听 "GameStateChanged" 做响应
            EventCenter.Instance?.Emit("GameStateChanged", newState);
        }

        public void Pause() => ChangeState(GameState.Paused);
        public void Resume() => ChangeState(GameState.Playing);
        public void GameOver() => ChangeState(GameState.GameOver);

        /// <summary>
        /// 退出游戏。编辑器模式停止运行，打包后退出应用。
        /// </summary>
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
