using ARPGCombat.Core;
using ARPGCombat.Data;

namespace ARPGCombat.MVC.Model
{
    /// <summary>
    /// 伤害事件载荷（面试点：结构化事件参数）。
    /// 之前 Day 1 的事件 payload 是直接 int，真实项目里一个事件要带"谁打的、打了谁、多少伤害、是否暴击"，
    /// 所以用一个轻量 struct 打包。不装箱（传 struct 作 object 会装箱一次，但 GC 量远小于散参数）。
    /// </summary>
    public struct DamageEventData
    {
        public int instigatorId;       // 攻击者实例 ID（-1 表示环境伤害）
        public int targetId;           // 被击者实例 ID
        public int damage;             // 最终伤害
        public bool isCritical;        // 是否暴击
        public bool isDead;            // 是否致死
        public UnityEngine.Vector3 hitPoint;  // 命中点（飘字/特效坐标）
    }

    /// <summary>
    /// MVC Model 层：角色运行时数据（面试点：纯 C# 类，不继承 MonoBehaviour）。
    ///
    /// MVC 为什么 Model 要纯 C#？
    /// 1. **可单元测试**：不需要 Unity 运行时就能 new CharacterModel，NUnit 里直接测 TakeDamage。
    /// 2. **可序列化**：纯数据类可以被 JSON/Binary 序列化，做存档热更更容易。
    /// 3. **职责单一**：Model 只管数据和规则（扣血、判断死亡），不管移动、渲染、UI。
    ///
    /// "血量变化"通过 EventCenter 广播：View（血条/飘字）订阅，Controller 订阅"死亡"做状态切换。
    /// </summary>
    public class CharacterModel
    {
        // ==================== 只读配置（来自 SO，初始化后不变） ====================
        public int InstanceId { get; }
        public CharacterConfig Config { get; }
        public int MaxHp => Config.maxHp;
        public int AttackPower => Config.attackPower;
        public float MoveSpeed => Config.moveSpeed;
        public float AttackRange => Config.attackRange;
        public float AttackCooldown => Config.attackCooldown;
        public float AttackWindup => Config.attackWindup;
        public float DefenseRate => Config.defenseRate;
        public string Tag => Config.characterTag;

        // ==================== 运行时可变数据（只能通过方法修改） ====================
        public int CurrentHp { get; private set; }
        public bool IsDead { get; private set; }
        public float LastAttackTime { get; private set; } = -999f;  // 冷却计算用

        // ==================== 构造（初始化） ====================
        public CharacterModel(int instanceId, CharacterConfig config)
        {
            InstanceId = instanceId;
            Config = config;
            CurrentHp = config.maxHp;
            IsDead = false;
        }

        // ==================== 业务方法：唯一能改数据的入口 ====================

        /// <summary>
        /// 受到伤害。返回实际扣血量（含致死判断）。
        /// 调用方（Controller/DamageSystem）通过返回值或事件通知 UI。
        /// 这里数据变化**主动广播事件**，体现 MVC 中"Model 不知道 View 存在，但知道自己变化了要通知"。
        /// </summary>
        public int TakeDamage(int attackerId, int rawDamage, UnityEngine.Vector3 hitPoint, bool isCritical = false)
        {
            if (IsDead || rawDamage <= 0) return 0;

            // 减伤：实际伤害 = 原始伤害 * (1 - 防御率)
            int actualDamage = MathfMax(1, (int)(rawDamage * (1f - DefenseRate)));
            // 注意：不能用 UnityEngine.Mathf.Max，因为 Model 层应尽量避免引用 UnityEngine API。
            // 这里为零依赖我们写个小工具，下一行实际用的是本地 helper。

            CurrentHp -= actualDamage;
            if (CurrentHp < 0) CurrentHp = 0;

            bool died = CurrentHp <= 0;
            if (died) IsDead = true;

            // 广播伤害事件 → View（血条/飘字/特效）订阅，不用互相引用
            var payload = new DamageEventData
            {
                instigatorId = attackerId,
                targetId = InstanceId,
                damage = actualDamage,
                isCritical = isCritical,
                isDead = died,
                hitPoint = hitPoint
            };
            EventCenter.Instance.Emit("CharacterDamaged", payload);

            if (died)
            {
                // 死亡事件 → AI 切 DeathState，BattleController 加击杀计数
                EventCenter.Instance.Emit("CharacterDied", payload);
            }

            return actualDamage;
        }

        /// <summary>
        /// 回血。UI/治疗技能调用。独立方法让职责清晰（加血 vs 扣血走不同流程）。
        /// </summary>
        public int Heal(int amount)
        {
            if (IsDead || amount <= 0) return 0;
            int before = CurrentHp;
            CurrentHp += amount;
            if (CurrentHp > MaxHp) CurrentHp = MaxHp;
            int healed = CurrentHp - before;

            if (healed > 0)
            {
                EventCenter.Instance.Emit("CharacterHealed", new DamageEventData
                {
                    targetId = InstanceId,
                    damage = -healed,  // 负数表示治疗（飘字可以显示绿色）
                    hitPoint = UnityEngine.Vector3.zero
                });
            }
            return healed;
        }

        /// <summary>
        /// 记录一次攻击的时间戳。冷却计算使用：
        ///   CanAttack => Time.time - LastAttackTime >= AttackCooldown
        /// 放在 Model 层而不是 Controller 层是因为"冷却"是角色的**状态**，属于数据。
        /// </summary>
        public void MarkAttacked(float atTime)
        {
            LastAttackTime = atTime;
        }

        public bool CanAttack(float currentTime)
        {
            return !IsDead && (currentTime - LastAttackTime) >= AttackCooldown;
        }

        // ==================== 工具方法（最小化 UnityEngine 依赖） ====================
        private static int MathfMax(int a, int b) => a > b ? a : b;
    }
}
