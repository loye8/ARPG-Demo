namespace ARPGCombat.FSM
{
    /// <summary>
    /// 有限状态机状态接口（面试点：面向接口编程，状态实现解耦）。
    ///
    /// 为什么用接口而不是抽象基类？
    /// 1. C# 单继承，如果用抽象类，状态类就不能再继承其他类（如 EnemyStateBase 这种通用基类）。
    ///    用接口留下继承空间，未来可以写 "public class PatrolState : EnemyStateBase, IState"。
    /// 2. 接口更轻量，没有成员字段，强调"行为契约"而不是"实现继承"。
    ///
    /// 三个生命周期方法对应状态机的三件事：
    /// - OnEnter：进入状态时做一次性的初始化（如设置 NavMeshAgent 目标）
    /// - OnUpdate：每帧的逻辑（如检测条件是否满足切换）
    /// - OnExit：离开状态时做收尾（如停止 NavMeshAgent，避免上一状态的移动残留）
    /// </summary>
    public interface IState
    {
        void OnEnter();
        void OnUpdate();
        void OnExit();
    }
}
