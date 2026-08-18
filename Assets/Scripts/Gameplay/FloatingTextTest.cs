using UnityEngine;
using ARPGCombat.Core;

namespace ARPGCombat.Gameplay
{
    /// <summary>
    /// 【Day1 验证脚本】模拟伤害飘字 UI。
    /// 它监听 "DamageDealt" 事件并显示飘字（这里用 Debug.Log 模拟）。
    /// 这个脚本完全不知道 DamageSystemTest 的存在，二者通过 EventCenter 解耦。
    ///
    /// 挂载方式：随便挂在场景任意 GameObject 上（与 DamageSystemTest 不需要同一物体）。
    /// Day3 实现真实 DamageTextView 后此脚本可删除。
    /// </summary>
    public class FloatingTextTest : MonoBehaviour
    {
        private void OnEnable()
        {
            EventCenter.Instance.On("DamageDealt", HandleDamageDealt);
            EventCenter.Instance.On("GameStateChanged", HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventCenter.Instance?.Off("DamageDealt", HandleDamageDealt);
            EventCenter.Instance?.Off("GameStateChanged", HandleGameStateChanged);
        }

        private void HandleDamageDealt(object data)
        {
            int damage = (int)data;
            // 真实实现：从对象池取一个 DamageText，设置文本，播放上浮动画
            Debug.Log($"<color=yellow>[FloatingText]</color> 显示伤害飘字：-{damage}");
        }

        /// <summary>
        /// 顺带验证 GameManager 的状态广播也能收到，证明跨模块事件链路通畅。
        /// </summary>
        private void HandleGameStateChanged(object data)
        {
            var state = (GameManager.GameState)data;
            Debug.Log($"<color=cyan>[FloatingText]</color> 收到游戏状态变化：{state}");
        }
    }
}
