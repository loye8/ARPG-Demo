using UnityEngine;

namespace ARPGCombat.FSM
{
    /// <summary>
    /// 通用状态机管理器（面试点：与具体状态解耦，可复用给玩家/敌人/Boss）。
    ///
    /// 设计要点：
    /// 1. 只持有"当前状态"引用，不关心状态具体是什么类型 → 任何实现 IState 的类都能塞进来。
    /// 2. ChangeState 做三件事：旧状态.OnExit → 切换 → 新状态.OnEnter。保证状态切换的"原子性"。
    /// 3. Update 每帧调当前状态.OnUpdate，由 MonoBehaviour 驱动（Controller 在自己的 Update 里调用）。
    ///
    /// 为什么不用 Unity 自带的 Animator StateMachine？
    /// - Animator 是动画状态机，它管的是"动画怎么播"，不管业务逻辑（如"敌人要不要追玩家"）。
    /// - 我们的业务状态机决定"该做什么"，动画只是表现层。两者解耦：业务 FSM 切状态 → 广播事件 → 动画 View 响应。
    /// </summary>
    public class StateMachine
    {
        public IState CurrentState { get; private set; }

        /// <summary>
        /// 初始状态由外部初始化时调用（不触发 OnExit，因为还没有旧状态）。
        /// </summary>
        public void Initialize(IState startingState)
        {
            CurrentState = startingState;
            CurrentState?.OnEnter();
        }

        /// <summary>
        /// 切换状态。旧状态的 OnExit 先执行，再做切换，最后新状态 OnEnter。
        /// 顺序很重要：如果反过来，可能在 OnExit 里访问的状态数据已被新状态改了。
        /// </summary>
        public void ChangeState(IState newState)
        {
            if (newState == null)
            {
                Debug.LogWarning("[StateMachine] 尝试切换到 null 状态，已忽略。");
                return;
            }

            CurrentState?.OnExit();
            CurrentState = newState;
            CurrentState.OnEnter();
        }

        /// <summary>
        /// 每帧由 Controller 调用。
        /// </summary>
        public void Update()
        {
            CurrentState?.OnUpdate();
        }
    }
}
