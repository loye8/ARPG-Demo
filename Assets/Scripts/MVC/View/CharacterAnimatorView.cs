using UnityEngine;
using ARPGCombat.Core;

namespace ARPGCombat.MVC.View
{
    /// <summary>
    /// MVC View 层：角色动画控制器（面试点：MVC 中 View 只负责显示/表现）。
    ///
    /// 设计：
    /// - View 不持有 Model/Controller 引用，完全通过订阅事件中心切换动画状态。
    ///   这样 Controller 改了（比如从 PlayerController 改成 AIController），View 不用动。
    /// - Animator 参数用 int（状态枚举）而不是 bool trigger：状态机转换更清晰。
    ///
    /// Animator Controller 参数（你在编辑器里新建）：
    ///   - MoveAmount (Float, 0~1)：0=Idle, >0=Walk/Run，BlendTree 混合
    ///   - Attack     (Trigger)：收到 PlayerAttackStarted 就 SetTrigger
    ///   - Dead       (Bool)：死亡时设 true，锁住状态机
    /// </summary>
    public class CharacterAnimatorView : MonoBehaviour
    {
        public Animator anim;

        // Animator 参数 ID 缓存（字符串每帧 hash 有开销，缓存为 int 是标准优化）
        private static readonly int ParamMoveAmount = Animator.StringToHash("MoveAmount");
        private static readonly int ParamAttack = Animator.StringToHash("Attack");
        private static readonly int ParamDead = Animator.StringToHash("Dead");

        void Reset()
        {
            // Reset 是 Unity 在首次添加组件时的回调，用来自动填引用
            anim = GetComponent<Animator>();
        }

        void Awake()
        {
            if (anim == null) anim = GetComponent<Animator>();
        }

        void OnEnable()
        {
            EventCenter.Instance.On("PlayerMoveAmountChanged", OnMoveAmountChanged);
            EventCenter.Instance.On("PlayerAttackStarted", OnAttackStarted);
            EventCenter.Instance.On("CharacterDied", OnCharacterDied);
        }

        void OnDisable()
        {
            EventCenter.Instance?.Off("PlayerMoveAmountChanged", OnMoveAmountChanged);
            EventCenter.Instance?.Off("PlayerAttackStarted", OnAttackStarted);
            EventCenter.Instance?.Off("CharacterDied", OnCharacterDied);
        }

        private void OnMoveAmountChanged(object data)
        {
            if (anim == null) return;
            float amount = (float)data;
            anim.SetFloat(ParamMoveAmount, amount);
        }

        private void OnAttackStarted(object data)
        {
            if (anim == null) return;
            anim.SetTrigger(ParamAttack);
        }

        private void OnCharacterDied(object data)
        {
            if (anim == null) return;
            // 注意：这个事件所有角色死亡都会广播。
            // Day 3 每个 CharacterAnimatorView 会持有自己的 instanceId 过滤。
            // Day 2 演示：直接切死亡状态，方便观察效果。
            anim.SetBool(ParamDead, true);
        }
    }
}
