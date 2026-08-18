using ARPGCombat.Data;

namespace ARPGCombat.MVC.Model
{
    /// <summary>
    /// MVC Model 层：技能运行时数据（Day 3 技能系统会重度使用）。
    /// Day 2 预定义为了架构完整性——PlayerController 持有的技能集合就是 List&lt;SkillModel&gt;。
    /// </summary>
    public class SkillModel
    {
        public SkillConfig Config { get; }
        public float LastCastTime { get; private set; } = -9999f;
        public int CurrentLevel { get; private set; } = 1;

        public SkillModel(SkillConfig config, int level = 1)
        {
            Config = config;
            CurrentLevel = level;
        }

        public bool CanCast(float currentTime)
        {
            return (currentTime - LastCastTime) >= Config.cooldown;
        }

        public float GetCooldownRemaining(float currentTime)
        {
            float remain = Config.cooldown - (currentTime - LastCastTime);
            return remain < 0 ? 0 : remain;
        }

        /// <summary>技能栏 UI 进度条用：0~1，1 表示冷却完成</summary>
        public float GetCooldownRatio(float currentTime)
        {
            if (Config.cooldown <= 0f) return 1f;
            return 1f - GetCooldownRemaining(currentTime) / Config.cooldown;
        }

        public void MarkCasted(float atTime)
        {
            LastCastTime = atTime;
        }

        /// <summary>等级加成：伤害 = 基础伤害 + (等级-1)*每级增量</summary>
        public int GetDamageWithLevel()
        {
            return Config.damage + (CurrentLevel - 1) * (Config.damage / 10);
        }
    }
}
