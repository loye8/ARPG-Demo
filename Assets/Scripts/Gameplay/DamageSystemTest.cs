using UnityEngine;
using ARPGCombat.Core;

namespace ARPGCombat.Gameplay
{
    /// <summary>
    /// 【Day1 验证脚本】模拟伤害系统。
    /// 它监听 "AttackHit" 事件，处理后广播 "DamageDealt" 事件给飘字系统。
    /// 注意：这个脚本完全不知道 FloatingTextTest 的存在，二者零耦合。
    ///
    /// 挂载方式：随便挂在场景任意 GameObject 上即可。
    /// Day2 实现真实 DamageSystem 后此脚本可删除。
    /// </summary>
    public class DamageSystemTest : MonoBehaviour
    {
        private void OnEnable()
        {
            EventCenter.Instance.On("AttackHit", HandleAttackHit);
        }

        private void OnDisable()
        {
            // 必须注销，否则对象销毁后事件中心仍持有引用 → NullReferenceException
            EventCenter.Instance?.Off("AttackHit", HandleAttackHit);
        }

        private void Start()
        {
            // 模拟一次攻击命中：触发事件，启动整条链路
            // 真实游戏中这由 PlayerController 的攻击判定调用
            Debug.Log("[DamageSystemTest] === 事件中心链路测试开始 ===");
            Debug.Log("[DamageSystemTest] 触发 AttackHit 事件，伤害=25");
            EventCenter.Instance.Emit("AttackHit", 25);

            // 再触发一次，验证多播和多次调用
            Debug.Log("[DamageSystemTest] 再次触发 AttackHit，伤害=88");
            EventCenter.Instance.Emit("AttackHit", 88);

            // 验证 Off 注销：禁用自身后再触发，应无响应
            Debug.Log("[DamageSystemTest] 测试注销：移除监听后触发，应无任何响应");
            EventCenter.Instance.Off("AttackHit", HandleAttackHit);
            EventCenter.Instance.Emit("AttackHit", 999);
        }

        private void HandleAttackHit(object data)
        {
            int damage = (int)data;
            Debug.Log($"[DamageSystemTest] 收到 AttackHit，伤害={damage}，进行伤害计算...");

            // 伤害系统处理完后，把结果广播出去给 UI（飘字/血条/计数器）
            // 这里直接把伤害值转发；真实系统可能附加暴击/元素等信息
            EventCenter.Instance.Emit("DamageDealt", damage);
        }
    }
}
