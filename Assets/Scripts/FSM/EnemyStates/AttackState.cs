using UnityEngine;
using ARPGCombat.Core;
using ARPGCombat.Gameplay;
using ARPGCombat.MVC.Controller;

namespace ARPGCombat.FSM.EnemyStates
{
    /// <summary>
    /// 攻击状态（面试点：周期性事件广播 + 状态退出条件）。
    ///
    /// 行为：
    /// 1. OnEnter：停 NavMeshAgent（站着打），朝向玩家，重置攻击计时器
    /// 2. OnUpdate：
    ///    - 玩家离开攻击范围 → 切回 ChaseState
    ///    - 玩家死了 → 切回 PatrolState（不继续打尸体）
    ///    - attackInterval 时间到 → Emit "EnemyAttackHit" 事件 → DamageSystem 扣玩家血
    /// 3. OnExit：无需特殊清理
    ///
    /// 攻击走事件链路（与玩家攻击完全对称）：
    ///   EnemyAttackState.Emit("EnemyAttackHit")
    ///     → DamageSystem.HandleEnemyAttackHit
    ///     → CharacterRegistry.GetById(playerId).TakeDamage()
    ///     → Model 内部 Emit("CharacterDamaged") → 血条/飘字/动画
    /// </summary>
    public class AttackState : IState
    {
        private readonly EnemyController _controller;
        private float _attackTimer;

        public AttackState(EnemyController controller) => _controller = controller;

        public void OnEnter()
        {
            // 停下脚步站着打
            _controller.Agent.isStopped = true;
            _controller.Agent.ResetPath();  // 清空残留路径，避免视觉上"边走边打"

            // 立即打第一下（玩家进入攻击范围的第一时间就要有反馈）
            _attackTimer = 0f;
        }

        public void OnUpdate()
        {
            // 1. 玩家死了或不存在 → 回巡逻
            if (!_controller.HasValidTarget())
            {
                _controller.StateMachine.ChangeState(_controller.PatrolState);
                return;
            }

            float dist = _controller.DistanceToPlayer();

            // 2. 玩家离开攻击范围 → 追击
            if (dist > _controller.Model.AttackRange * 1.1f)
            {
                // 1.1 倍做滞后，防止玩家在攻击范围边缘反复进出导致状态抖动
                _controller.StateMachine.ChangeState(_controller.ChaseState);
                return;
            }

            // 3. 朝向玩家（敌人站着也要转身面对玩家，更自然）
            Vector3 lookDir = (_controller.TargetPlayer.transform.position - _controller.transform.position);
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(lookDir);
                _controller.transform.rotation = Quaternion.Slerp(
                    _controller.transform.rotation, target,
                    _controller.Model.Config.rotationSpeed * Time.deltaTime);
            }

            // 4. 攻击计时
            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                _attackTimer = _controller.attackInterval;
                PerformAttack();
            }
        }

        public void OnExit()
        {
            // 离开攻击状态时不需要特别清理
        }

        /// <summary>
        /// 执行一次攻击：广播事件给 DamageSystem，由它扣玩家血。
        /// 注意：AttackState 只负责"何时打"和"打谁"，**不负责伤害计算**（暴击/减伤是 DamageSystem 的事）。
        /// </summary>
        private void PerformAttack()
        {
            // 攻击命中点：玩家中心位置（飘字/特效用）
            Vector3 hitPoint = _controller.TargetPlayer.transform.position + Vector3.up * 1f;

            // 广播敌人攻击事件（Day 2 DamageSystem 已注册监听 EnemyAttackHit）
            EventCenter.Instance.Emit("EnemyAttackHit", new EnemyAttackHitData
            {
                attackerId = _controller.Model.InstanceId,
                targetId = _controller.TargetPlayer.Model.InstanceId,
                damage = _controller.Model.AttackPower,
                hitPoint = hitPoint
            });

            // 广播攻击动画事件（CharacterAnimatorView 监听 → 播放攻击动画）
            // 注意：这里没区分敌人/玩家，演示用同一事件名。生产版可以分开：EnemyAttackStarted
            EventCenter.Instance.Emit("EnemyAttackStarted", _controller.Model.InstanceId);
        }
    }
}
