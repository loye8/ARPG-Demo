using UnityEngine;

namespace ARPGCombat.Gameplay
{
    /// <summary>
    /// 玩家输入层（面试点：输入与业务逻辑分离）。
    ///
    /// 为什么独立出来？
    /// 1. **可替换**：未来想上 Input System / 手柄 / 触屏，只需改这一个文件。
    /// 2. **可测试**：单元测试里可以 mock InputReader，不走 Input.GetAxis 就能测 Controller。
    /// 3. **职责单一**：PlayerController 专注"怎么移动/攻击"，不关心"WASD 映射到什么 Vector2"。
    ///
    /// 注意：本 Demo 直接用 Input.GetAxis/Key（Unity 经典输入系统），项目里也带了
    /// InputSystem_Actions.inputactions（URP 模板自带）。如果面试官问"为什么不用 New Input System"，
    /// 答：我两种都能用，这里演示选 Legacy 是因为代码量少，迁移到 Input System 只需改 InputReader。
    /// </summary>
    public class PlayerInput : MonoBehaviour
    {
        // ---------- 原始输入（每帧更新，Controller 按需读取） ----------
        public Vector2 MoveAxis { get; private set; }       // WASD / 左摇杆
        public bool AttackPressed { get; private set; }     // 鼠标左键 / RB
        public bool SkillQPressed { get; private set; }
        public bool SkillEPressed { get; private set; }
        public bool MovePressed => MoveAxis.sqrMagnitude > 0.01f;

        // ---------- 边缘触发（事件，给"只响应一次"的动作如攻击使用） ----------
        public event System.Action OnAttackTriggered;
        public event System.Action<int> OnSkillTriggered;  // int = skill slot（0=Q, 1=E）

        private bool _prevAttack;
        private bool _prevQ;
        private bool _prevE;

        void Update()
        {
            // 1. 移动轴（-1~1）。Raw 是不加平滑的原始值，ARPG 需要灵敏响应。
            float h = Input.GetAxisRaw("Horizontal"); // A/D 或 ←/→
            float v = Input.GetAxisRaw("Vertical");   // W/S 或 ↑/↓
            MoveAxis = new Vector2(h, v);

            // 2. 攻击 / 技能：边缘检测（按下瞬间才触发一次，而不是按着就每帧触发）
            bool attack = Input.GetMouseButton(0);
            bool q = Input.GetKey(KeyCode.Q);
            bool e = Input.GetKey(KeyCode.E);

            AttackPressed = attack;
            SkillQPressed = q;
            SkillEPressed = e;

            if (attack && !_prevAttack) OnAttackTriggered?.Invoke();
            if (q && !_prevQ) OnSkillTriggered?.Invoke(0);
            if (e && !_prevE) OnSkillTriggered?.Invoke(1);

            _prevAttack = attack;
            _prevQ = q;
            _prevE = e;
        }
    }
}
