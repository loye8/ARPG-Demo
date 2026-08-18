using UnityEngine;

namespace ARPGCombat.Data
{
    /// <summary>
    /// ScriptableObject：技能配置。Day 2 写出来为 Day 3 做准备（架构前置，保证字段齐全）。
    /// 数据驱动：策划在 Inspector 调技能伤害、冷却、范围，代码只按 SO 数值执行。
    /// </summary>
    [CreateAssetMenu(fileName = "SkillConfig", menuName = "ARPGCombat/Skill Config", order = 1)]
    public class SkillConfig : ScriptableObject
    {
        [Header("基础")]
        [Tooltip("技能 ID（用于按键映射，0=Q 1=E）")]
        public int skillId;

        [Tooltip("技能名（UI 显示用）")]
        public string skillName = "未知技能";

        [Header("伤害")]
        [Tooltip("基础伤害")]
        public int damage = 40;

        [Tooltip("伤害类型：0 物理 / 1 魔法，用于抗性计算")]
        public int damageType = 0;

        [Header("范围")]
        [Tooltip("施放范围：技能生效最大距离")]
        public float range = 8f;

        [Tooltip("半径：范围型技能的 AoE 半径；单体技能填 0")]
        public float aoeRadius = 3f;

        [Header("冷却 & 资源")]
        [Tooltip("冷却（秒）")]
        public float cooldown = 6f;

        [Tooltip("消耗（可表示法力/怒气，Day 3 扩展）")]
        public int cost = 20;

        [Header("表现")]
        [Tooltip("技能特效预制体（Resources 加载），路径例：VFX/SkillEffect")]
        public string effectPrefabPath = "VFX/SkillEffect";

        [Tooltip("技能前摇（秒）：按键到生效的延迟，用于动画对齐")]
        public float windup = 0.3f;

        [Header("按键绑定（UI 图标显示用）")]
        public KeyCode keyBind = KeyCode.Q;
    }
}
