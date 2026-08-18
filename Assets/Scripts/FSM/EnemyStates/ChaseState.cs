using UnityEngine;
using ARPGCombat.MVC.Controller;

namespace ARPGCombat.FSM.EnemyStates
{
    /// <summary>
    /// 追击状态（面试点：NavMeshAgent 追踪动态目标 + 三态切换条件）。
    ///
    /// 行为：
    /// 1. OnEnter：恢复正常移动速度，启用 NavMeshAgent
    /// 2. OnUpdate：每帧把 Agent.destination 设为玩家位置（实时跟随）
    ///    检测：
    ///    - 进入攻击范围 → 切 AttackState
    ///    - 玩家脱离视野范围（追不上/玩家瞬移远了）→ 切回 PatrolState
    /// 3. OnExit：无需特殊清理
    ///
    /// 设计细节：
    /// - 不要每帧都 SetDestination：NavMeshAgent 重新计算路径有开销（10Hz 是平衡值）
    /// - 用"脱离视野 1.5 倍"作为回去巡逻的阈值，避免玩家在视野边缘反复进出导致状态机抖动
    ///   （例如 sightRange=8，玩家在 8.1 米就会回巡逻，8.0 又追击 → 闪一下 → 8.1 又回巡逻 → 抖动）
    /// </summary>
    public class ChaseState : IState
    {
        private readonly EnemyController _controller;
        private float _pathUpdateTimer;
        private const float PathUpdateInterval = 0.1f;  // 每 0.1 秒重设一次目标

        public ChaseState(EnemyController controller) => _controller = controller;

        public void OnEnter()
        {
            _controller.Agent.isStopped = false;
            _controller.Agent.speed = _controller.Model.MoveSpeed;  // 追击用全速
            _pathUpdateTimer = 0f;
        }

        public void OnUpdate()
        {
            // 玩家无效（死了或没创建）→ 回巡逻
            if (!_controller.HasValidTarget())
            {
                _controller.StateMachine.ChangeState(_controller.PatrolState);
                return;
            }

            float dist = _controller.DistanceToPlayer();

            // 1. 进入攻击范围 → 切攻击
            if (dist <= _controller.Model.AttackRange)
            {
                _controller.StateMachine.ChangeState(_controller.AttackState);
                return;
            }

            // 2. 玩家脱离视野（用 1.5 倍视野做滞后，防抖动）→ 回巡逻
            if (dist > _controller.sightRange * 1.5f)
            {
                _controller.StateMachine.ChangeState(_controller.PatrolState);
                return;
            }

            // 3. 定时更新 NavMeshAgent 目标（不要每帧都更新，省 CPU）
            _pathUpdateTimer -= Time.deltaTime;
            if (_pathUpdateTimer <= 0f)
            {
                _pathUpdateTimer = PathUpdateInterval;
                _controller.Agent.destination = _controller.TargetPlayer.transform.position;
            }
        }

        public void OnExit()
        {
            // 不主动停 Agent：让下个状态决定（AttackState 会停，PatrolState 会设新目标）
        }
    }
}
