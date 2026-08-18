using UnityEngine;

namespace ARPGCombat.Data
{
    /// <summary>
    /// ScriptableObject：角色属性配置（面试点：数据驱动设计）。
    ///
    /// 为什么用 SO 存配置？
    /// 1. 策划友好：非程序员也能在 Inspector 里调数值，不改代码就能调平衡。
    /// 2. 多实例复用：同一份 Config 可以创建多个角色（Enemy1/Enemy2 共用 EnemyConfig.asset），
    ///    改一次所有实例生效。
    /// 3. 无 GC：SO 是引用类型对象，数据引用而非值拷贝，运行时不分配。
    /// 4. 热更潜力：未来上 Addressables/ScriptableBuildPipeline 时可以把 SO 打成 AB 包热更。
    ///
    /// 命名约定：字段全部只读（private，只有 Inspector 和初始化读），运行时数据放 Model 层。
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterConfig", menuName = "ARPGCombat/Character Config", order = 0)]
    public class CharacterConfig : ScriptableObject
    {
        [Header("基础属性")]
        [Tooltip("最大生命值")]
        public int maxHp = 100;

        [Tooltip("基础攻击力")]
        public int attackPower = 20;

        [Header("移动")]
        [Tooltip("移动速度（单位/秒）")]
        public float moveSpeed = 5f;

        [Tooltip("旋转速度（度/秒），越大转向越灵敏")]
        public float rotationSpeed = 15f;

        [Header("攻击")]
        [Tooltip("攻击范围（米，用于 OverlapSphere）")]
        public float attackRange = 2f;

        [Tooltip("攻击冷却（秒），攻击后到下一次可攻击的间隔")]
        public float attackCooldown = 0.6f;

        [Tooltip("攻击前摇（秒）：按键后到实际出判定的延迟（动画匹配用）")]
        public float attackWindup = 0.2f;

        [Header("防御（Day3 用）")]
        [Tooltip("防御百分比 0~0.8，减少伤害的比例")]
        public float defenseRate = 0.1f;

        [Header("标签（分类用，方便 Layer/Tag 判断）")]
        [Tooltip(""敌人"/"玩家"")]
        public string characterTag = "Player";
    }
}
