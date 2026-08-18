using UnityEngine;
using ARPGCombat.Core;

namespace ARPGCombat.FSM.EnemyStates
{
    /// <summary>
    /// 死亡状态（面试点：状态终态 + 资源回收 + 事件清理）。
    ///
    /// 行为：
    /// 1. OnEnter：禁用碰撞体（不再被攻击判定命中）+ 停 NavMeshAgent + 播死亡动画 + 广播 EnemyDied
    /// 2. OnUpdate：累加计时，达到 destroyDelay 后销毁 GameObject
    /// 3. OnExit：死亡是终态，正常情况下不会 OnExit（对象已销毁）
    ///
    /// 设计细节：
    /// - 禁用 Collider 而不是 Destroy：先禁用碰撞让 OverlapSphere 不再命中，
    ///   留 destroyDelay 时间让死亡动画播完再销毁
    /// - 不在 OnEnter 直接 Destroy：要给动画/特效 3 秒表现时间
    /// - 广播 EnemyDied：BattleController 监听做击杀计数 +1
    /// </summary>
    public class DeathState : IState
    {
        private readonly ARPGCombat.MVC.Controller.EnemyController _controller;
        private float _deathTimer;

        public DeathState(ARPGCombat.MVC.Controller.EnemyController controller) => _controller = controller;

        public void OnEnter()
        {
            _deathTimer = 0f;

            // 1. 停 NavMeshAgent + 清路径
            var agent = _controller.Agent;
            if (agent != null)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            // 2. 禁用碰撞体（防止 OverlapSphere 继续命中已经死亡的敌人）
            //    注意：禁用 Collider 后 NavMeshAgent 可能丢失代理，所以要在停 Agent 之后做
            var col = _controller.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // 3. 播放死亡动画（CharacterAnimatorView 监听 CharacterDied 已经处理，这里再保险一下）
            if (_controller.anim != null)
            {
                _controller.anim.SetBool("Dead", true);
            }

            // 4. 广播 EnemyDied 事件（BattleController 加击杀计数 / 掉落奖励等）
            //    注意：Model.TakeDamage 已经发过 CharacterDied，BattleController 已经做过击杀判断。
            //    这里再发 EnemyDied 是给"只关心敌人死亡"的模块（如掉落系统、敌人计数 UI）用，
            //    避免它们去监听 CharacterDied 还要判断 Tag=="Enemy"。
            EventCenter.Instance.Emit("EnemyDied", _controller.Model.InstanceId);

            Debug.Log($"[DeathState] 敌人 {_controller.Model.InstanceId} 死亡，{ _controller.destroyDelay} 秒后销毁");
        }

        public void OnUpdate()
        {
            // 累加计时，到时间销毁
            _deathTimer += Time.deltaTime;
            if (_deathTimer >= _controller.destroyDelay)
            {
                // 销毁 GameObject。这里直接 Destroy，未来 Day4 的对象池版本会改成 Return()
                GameObject.Destroy(_controller.gameObject);
            }
        }

        public void OnExit()
        {
            // 死亡状态是终态，正常不会 OnExit。如果切走了说明对象被回收，这里不做事
        }
    }
}
