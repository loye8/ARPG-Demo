using UnityEngine;
using UnityEngine.AI;
using ARPGCombat.MVC.Controller;

namespace ARPGCombat.FSM.EnemyStates
{
    /// <summary>
    /// 巡逻状态（面试点：NavMeshAgent 随机点寻路 + 状态切换条件）。
    ///
    /// 行为：
    /// 1. 进入状态 → 选一个随机巡逻点，设给 NavMeshAgent.destination
    /// 2. 每帧检测：玩家进入视野范围？→ 切 ChaseState
    /// 3. 到达巡逻点 → 停顿 patrolWaitTime 秒 → 选下一个随机点
    ///
    /// 设计细节：
    /// - 巡逻点用 NavMesh.SamplePosition 在 NavMesh 上"试探"取点，保证点在可行走区域
    ///   （直接 Random.insideUnitSphere 可能取到墙里/坑里，NavMeshAgent 走不过去会卡住）
    /// - 到达判定用 "剩余路径距离 < 阈值" + "agent 没在算路径"，比 Distance 判断更可靠
    /// </summary>
    public class PatrolState : IState
    {
        private readonly EnemyController _controller;
        private NavMeshAgent Agent => _controller.Agent;

        private float _waitTimer;       // 到达后的停顿计时
        private bool _isWaiting;        // 是否在停顿中
        private bool _isMovingToPoint;  // 是否正在走向某个巡逻点

        public PatrolState(EnemyController controller) => _controller = controller;

        public void OnEnter()
        {
            _waitTimer = 0f;
            _isWaiting = false;
            _isMovingToPoint = false;

            // 进入巡逻状态时启用 NavMeshAgent（ChaseState/AttackState 退出时可能停过）
            Agent.isStopped = false;
            Agent.speed = _controller.Model.MoveSpeed * 0.5f;  // 巡逻速度 = 移动速度的一半（散步感）

            PickNextPatrolPoint();
        }

        public void OnUpdate()
        {
            // 1. 视野检测：玩家进入视野 → 切追击
            if (_controller.HasValidTarget() && _controller.DistanceToPlayer() <= _controller.sightRange)
            {
                _controller.StateMachine.ChangeState(_controller.ChaseState);
                return;
            }

            // 2. 巡逻逻辑
            if (_isWaiting)
            {
                _waitTimer -= Time.deltaTime;
                if (_waitTimer <= 0f)
                {
                    _isWaiting = false;
                    PickNextPatrolPoint();
                }
                return;
            }

            // 3. 检查是否到达巡逻点
            if (_isMovingToPoint && HasReachedDestination())
            {
                _isMovingToPoint = false;
                _isWaiting = true;
                _waitTimer = _controller.patrolWaitTime;
                Agent.isStopped = true;
            }
        }

        public void OnExit()
        {
            // 离开巡逻状态时不必重置 Agent.isStopped，让下一个状态自己决定
            // 这里保持中性：不主动改状态，避免污染下个状态
        }

        // ===================== 工具方法 =====================

        /// <summary>在 NavMesh 上取一个合法巡逻点，设给 Agent。</summary>
        private void PickNextPatrolPoint()
        {
            // 在生成点周围球内取一个随机点
            Vector3 randomOffset = Random.insideUnitSphere * _controller.patrolRadius;
            randomOffset.y = 0;  // 水平面巡逻
            Vector3 candidate = _controller.SpawnPosition + randomOffset;

            // 用 NavMesh.SamplePosition 在 NavMesh 上找最近的可走点（半径 2m 内）
            // hit.hit=true 表示找到了合法点；不合法就重选（这里简化：直接重试一次，再不行就停）
            if (NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas))
            {
                Agent.destination = hit.position;
                Agent.isStopped = false;
                _isMovingToPoint = true;
            }
            else
            {
                // 没找到合法点：原地等下一轮再选
                _isWaiting = true;
                _waitTimer = 0.5f;
            }
        }

        /// <summary>判断是否到达目标点。NavMeshAgent 没有内置的"到达"事件，要自己判断。</summary>
        private bool HasReachedDestination()
        {
            if (Agent.pathPending) return false;  // 还在算路径，不算到达

            // 剩余路径长度 < stoppingDistance + 缓冲，且速度接近 0
            bool near = Agent.remainingDistance <= Agent.stoppingDistance + 0.1f;
            bool slow = Agent.velocity.sqrMagnitude < 0.04f;
            return near && slow;
        }
    }
}
