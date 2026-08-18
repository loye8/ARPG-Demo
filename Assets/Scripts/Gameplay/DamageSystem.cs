using UnityEngine;
using ARPGCombat.Core;
using ARPGCombat.MVC.Controller;
using ARPGCombat.MVC.Model;

namespace ARPGCombat.Gameplay
{
    /// <summary>
    /// 伤害计算系统（面试点：唯一伤害入口，集中处理暴击/减伤/免疫）。
    ///
    /// 职责：
    /// 1. 监听 "PlayerAttackHit" 事件（玩家命中目标）
    /// 2. 从 targetCollider 经 CharacterRegistry 反查 Model
    /// 3. 计算最终伤害（可扩展：暴击判定、元素抗性、伤害浮动）
    /// 4. 调用 Model.TakeDamage()（Model 内部会广播 CharacterDamaged 事件 → UI）
    ///
    /// 设计动机：
    /// 如果把伤害计算散落在 PlayerController / EnemyController / Skill 里，改"暴击率"要改 3 处。
    /// 统一入口让数值策划有唯一的修改点，也便于加伤害日志/调试工具。
    ///
    /// 不做成 Singleton：DamageSystem 不需要被别人调用，它的全部工作就是监听事件→扣血。
    /// 外部代码完全不知道它存在，挂在 GameManager 的 GameObject 上让它常驻即可。
    /// 这种"隐式模块"是事件驱动架构的特点——业务入口是 Emit，不是方法调用。
    /// </summary>
    public class DamageSystem : MonoBehaviour
    {
        [Header("数值")]
        [Tooltip("暴击率（0~1），命中时有概率造成 1.5x 伤害")]
        [Range(0f, 1f)] public float criticalChance = 0.15f;

        [Tooltip("暴击倍率")]
        public float criticalMultiplier = 1.5f;

        void OnEnable()
        {
            EventCenter.Instance.On("PlayerAttackHit", HandlePlayerAttackHit);
            // Day 3 EnemyController 会 emit EnemyAttackHit，这里先注册占坑
            EventCenter.Instance.On("EnemyAttackHit", HandleEnemyAttackHit);
        }

        void OnDisable()
        {
            EventCenter.Instance?.Off("PlayerAttackHit", HandlePlayerAttackHit);
            EventCenter.Instance?.Off("EnemyAttackHit", HandleEnemyAttackHit);
        }

        private void HandlePlayerAttackHit(object data)
        {
            if (data is not PlayerAttackHitData hit) return;

            // 用 CharacterRegistry 把 Collider → Model（O(1) 查表）
            if (!CharacterRegistry.Instance.TryGetByCollider(hit.targetCollider, out var targetModel))
            {
                // 玩家挥空打到墙/地面等非角色物体，静默忽略（面试点：LayerMask + Registry 双重保险）
                return;
            }

            // 伤害计算：基础攻击力 * 暴击（随机）* 浮动（10%）
            float raw = hit.attackerPower;
            bool isCrit = Random.value < criticalChance;
            if (isCrit) raw *= criticalMultiplier;
            raw *= Random.Range(0.9f, 1.1f);  // 10% 浮动，避免每次伤害相同

            // 最终由 Model 扣血（Model 内部会做减伤 + 广播 CharacterDamaged → 血条/飘字）
            int final = Mathf.Max(1, (int)raw);
            targetModel.TakeDamage(hit.attackerId, final, hit.hitPoint, isCrit);
        }

        private void HandleEnemyAttackHit(object data)
        {
            // Day3 实现：敌人命中玩家，对称处理
            // 逻辑与 HandlePlayerAttackHit 镜像，这里留占位体现架构一致性
            // (目前未使用，只是预定义，Day 3 再补)
            if (data is not EnemyAttackHitData ehit) return;

            var playerModel = CharacterRegistry.Instance.GetById(ehit.targetId);
            if (playerModel == null || playerModel.IsDead) return;

            bool isCrit = Random.value < 0.05f;
            int dmg = Mathf.Max(1, (int)(ehit.damage * (isCrit ? 1.5f : 1f)));
            playerModel.TakeDamage(ehit.attackerId, dmg, ehit.hitPoint, isCrit);
        }
    }

    /// <summary>
    /// 敌人命中事件载荷（Day3 用，架构占位，保证扩展时改动最小）。
    /// </summary>
    public struct EnemyAttackHitData
    {
        public int attackerId;
        public int targetId;
        public int damage;
        public Vector3 hitPoint;
    }
}
