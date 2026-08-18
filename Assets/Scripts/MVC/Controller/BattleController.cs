using UnityEngine;
using ARPGCombat.Core;
using ARPGCombat.MVC.Model;

namespace ARPGCombat.MVC.Controller
{
    /// <summary>
    /// MVC Controller 层：战斗总控（面试点：跨模块协调者，不做具体业务）。
    ///
    /// 职责（MVC 中 Controller 的职责）：
    /// 1. 协调全局：初始化时创建角色/敌人/UI（Day3 完善）
    /// 2. 统计类逻辑：击杀计数（监听 CharacterDied 事件累计）
    /// 3. 全局规则：胜负判定（监听玩家死亡 = GameOver）
    ///
    /// 为什么独立出来？
    /// - GameManager 管"应用级"状态（Boot/Playing/Paused）
    /// - BattleController 管"战斗级"逻辑（击杀/胜负/波次）
    /// 两者分层避免 GameManager 变"上帝类"。
    ///
    /// 不继承 Singleton：它只监听事件，不需要别人调用。挂在 GameManager 物体上即可。
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        public int KillCount { get; private set; }

        void OnEnable()
        {
            EventCenter.Instance.On("CharacterDied", HandleCharacterDied);
            EventCenter.Instance.On("PlayerCreated", HandlePlayerCreated);
        }

        void OnDisable()
        {
            EventCenter.Instance?.Off("CharacterDied", HandleCharacterDied);
            EventCenter.Instance?.Off("PlayerCreated", HandlePlayerCreated);
        }

        private void HandlePlayerCreated(object data)
        {
            // Day 3 调整：PlayerController 现在广播自身（this），不再传 Model
            if (data is not PlayerController player) return;

            // 初始化 HUD（Day3：把玩家 Model 传给 HealthBarView 绑定）
            Debug.Log($"[BattleController] 玩家已创建，实例ID = {player.Model.InstanceId}，Tag = {player.Model.Tag}");
        }

        private void HandleCharacterDied(object data)
        {
            if (data is not DamageEventData d) return;

            // 攻击者 = 玩家（实例ID 1 按当前 PlayerController._idCounter 的赋值规则）
            // 更严谨的方式：CharacterModel.Tag == "Player"，但要查表才能拿到 Model
            var targetModel = ARPGCombat.Gameplay.CharacterRegistry.Instance.GetById(d.targetId);
            if (targetModel != null && targetModel.Tag != "Player")
            {
                // 死的不是玩家，说明是敌人 → 击杀+1
                KillCount++;
                EventCenter.Instance.Emit("KillCountChanged", KillCount);
                Debug.Log($"<color=orange>[BattleController]</color> 击杀计数：{KillCount}（击杀了 {targetModel.Tag}）");
            }
            else if (targetModel != null && targetModel.Tag == "Player")
            {
                // 玩家死了 → GameOver
                Debug.Log($"<color=red>[BattleController]</color> 玩家死亡，游戏结束");
                GameManager.Instance?.GameOver();
            }
        }
    }
}
