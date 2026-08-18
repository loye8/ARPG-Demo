using System.Collections;
using UnityEngine;
using ARPGCombat.Core;
using ARPGCombat.Data;
using ARPGCombat.MVC.Model;
using ARPGCombat.Utils;

namespace ARPGCombat.MVC.Controller
{
    /// <summary>
    /// MVC Controller 层：玩家角色行为控制（面试点：MVC 职责划分）。
    ///
    /// 职责：
    /// - Model：持有 CharacterModel（数据）和 SkillModel 列表
    /// - View：操作 Animator 参数（通过事件广播，Day 3 HUD 血条订阅 Model 事件刷新）
    /// - 业务：读取 PlayerInput → 移动/朝向 → 触发攻击判定 → Emit 事件给 DamageSystem
    ///
    /// 关键设计：
    /// - 数据层纯 C#（CharacterModel），Controller 是 MonoBehaviour 桥接 Unity 引擎与 Model。
    /// - **绝不直接调 UI**，UI 变化全靠订阅事件中心的 CharacterDamaged / CharacterDied。
    /// - 攻击判定用 **Physics.OverlapSphere** + LayerMask 过滤，避免误打地形/友军。
    /// - 攻击前摇用协程，让动画和判定时间点对齐，不依赖动画事件（不绑定具体美术资源，可演示）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerController : MonoBehaviour
    {
        // ===================== Inspector 配置 =====================
        [Header("数据配置")]
        [Tooltip("ScriptableObject：玩家属性配置")]
        public CharacterConfig config;

        [Tooltip("摄像机（计算方向用，默认取 Camera.main）")]
        public Camera cam;

        [Header("层过滤")]
        [Tooltip("哪些层算作敌人（OverlapSphere 只检测这些层）")]
        public LayerMask enemyLayerMask = ~0;  // 默认 All，实际请改成 Enemy 层

        // ===================== 运行时引用 =====================
        public CharacterModel Model { get; private set; }
        private CharacterController _cc;
        private PlayerInput _input;
        private Vector3 _velocity;  // 垂直速度（重力）

        // 攻击前摇协程引用（防止一次攻击还没出判定时，玩家又按了一次按键重复攻击）
        private Coroutine _activeAttackCoroutine;
        private int _idCounter = 1;  // 生成实例 ID（简易：场景内自增）

        // ===================== 生命周期 =====================
        private void Awake()
        {
            // 1. 拿到 Unity 组件
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInput>();

            // 2. 初始化 Model：从 SO 配置里读取数据，生成运行时 Model
            if (config == null)
            {
                Debug.LogError($"[PlayerController] {name} 的 CharacterConfig 未配置！");
                enabled = false;
                return;
            }
            Model = new CharacterModel(_idCounter++, config);

            // 3. 注册到注册表（让 DamageSystem 能把 Collider → Model）
            // CharacterController 本身继承自 Collider，可以直接用
            CharacterRegistry.Instance.Register(Model, _cc);

            // 4. 摄像机兜底：Inspector 没拖就取 Main Camera
            if (cam == null) cam = Camera.main;

            // 5. 绑定输入事件（边缘触发）
            _input.OnAttackTriggered += HandleAttackInput;
            _input.OnSkillTriggered += HandleSkillInput;

            // 6. 广播"玩家已创建"：传 this（PlayerController 自身），让 EnemyController / BattleController 能拿到玩家 Transform 和 Model
            // 之前传 Model 只能让订阅者知道数据，现在传自身让 AI 能读到 transform.position 做 NavMesh 追击
            EventCenter.Instance.Emit("PlayerCreated", this);
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.OnAttackTriggered -= HandleAttackInput;
                _input.OnSkillTriggered -= HandleSkillInput;
            }
            if (Model != null)
                CharacterRegistry.Instance?.Unregister(Model, _cc);
            StopAllCoroutines();
        }

        private void Update()
        {
            if (Model.IsDead) return;

            HandleMovement();
            HandleMouseLook();
            ApplyGravity();

            // 动画：通过事件广播移动量（Animator 订阅，避免 Controller 直接引用 Animator）
            float moveAmount = Mathf.Clamp01(_input.MoveAxis.magnitude);
            EventCenter.Instance.Emit("PlayerMoveAmountChanged", moveAmount);
        }

        // ===================== 移动（WASD + 摄像机朝向） =====================
        private void HandleMovement()
        {
            Vector2 axis = _input.MoveAxis;
            if (axis.sqrMagnitude < 0.01f) return;

            // 摄像机前后/左右向量，Y 清零（因为是俯视斜角，原始 forward 有向下分量）
            Vector3 camFwd = cam.transform.forward.Flatten().normalized;
            Vector3 camRight = cam.transform.right.Flatten().normalized;

            // 世界空间移动方向 = 前(W/S) * 摄像机前 + 右(A/D) * 摄像机右
            Vector3 moveDir = (camFwd * axis.y + camRight * axis.x).normalized;
            Vector3 moveDelta = moveDir * Model.MoveSpeed * Time.deltaTime;

            _cc.Move(moveDelta);
        }

        // ===================== 鼠标朝向（ARPG 标准：角色始终面朝鼠标） =====================
        private void HandleMouseLook()
        {
            Vector3 targetPoint = cam.ScreenPointToGroundPlane(Input.mousePosition);
            if (targetPoint == Vector3.zero) return;  // 无合法交点，不转

            Vector3 lookDir = (targetPoint - transform.position).Flatten();
            if (lookDir.sqrMagnitude < 0.001f) return;

            // Slerp 平滑旋转，而不是瞬间 LookAt（表现更自然）
            Quaternion target = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, target,
                Model.Config.rotationSpeed * Time.deltaTime);
        }

        // ===================== 重力（CharacterController 没有内置重力） =====================
        private void ApplyGravity()
        {
            if (_cc.isGrounded && _velocity.y < 0)
                _velocity.y = -2f;  // 贴地的微小向下速度，防止从地面上飘起来
            else
                _velocity.y += Physics.gravity.y * Time.deltaTime;

            _cc.Move(_velocity * Time.deltaTime);
        }

        // ===================== 攻击：鼠标左键（边缘触发 + 冷却 + 前摇） =====================
        private void HandleAttackInput()
        {
            if (Model.IsDead) return;
            if (!Model.CanAttack(Time.time)) return;         // 冷却中
            if (_activeAttackCoroutine != null) return;      // 前摇中，防重入

            _activeAttackCoroutine = StartCoroutine(AttackRoutine());
        }

        /// <summary>
        /// 攻击流程：
        /// 1. 立即 MarkAttacked（占用冷却）+ 广播"开始攻击"（触发攻击动画）
        /// 2. 等 Windup 秒（前摇时间：角色抬手动作）
        /// 3. 到判定时间点做 OverlapSphere，找范围内敌人
        /// 4. 每个命中目标广播一次 "PlayerAttackHit" 给 DamageSystem 做伤害计算
        /// 5. 广播"攻击结束"（取消攻击动画锁定等）
        /// </summary>
        private IEnumerator AttackRoutine()
        {
            Model.MarkAttacked(Time.time);

            // 播放攻击动画事件（AnimatorController 接收）
            EventCenter.Instance.Emit("PlayerAttackStarted", null);

            // 前摇等待
            yield return new WaitForSeconds(Model.AttackWindup);

            // 攻击判定中心：角色身前 1 米（而不是角色中心，这样更有"向前挥砍"感觉）
            Vector3 hitCenter = transform.position + transform.forward.Flatten() * 1f;
            float range = Model.AttackRange;

            // 面试点：非 GC Alloc 版 OverlapSphere
            // 这里用普通版是为了代码简洁，面试官问 GC 可以主动回答"生产版本我会用 NonAlloc 版 + 固定 buffer"
            Collider[] hits = Physics.OverlapSphere(hitCenter, range, enemyLayerMask);

            foreach (var hit in hits)
            {
                // 命中点（取离角色中心最近的碰撞体上的点，特效/飘字用）
                Vector3 hitPoint = hit.ClosestPoint(transform.position);

                // 广播"玩家攻击命中了某个碰撞体"，DamageSystem 负责把 target 解析成 EnemyModel 并扣血
                EventCenter.Instance.Emit("PlayerAttackHit", new PlayerAttackHitData
                {
                    attackerId = Model.InstanceId,
                    attackerPower = Model.AttackPower,
                    targetCollider = hit,
                    hitPoint = hitPoint
                });
            }

            // 攻击动画结束（退出 Attack 状态回 Idle/Move）
            EventCenter.Instance.Emit("PlayerAttackEnded", null);
            _activeAttackCoroutine = null;
        }

        // ===================== 技能按键：占位（Day3 完善，当前仅广播事件） =====================
        private void HandleSkillInput(int slot)
        {
            if (Model.IsDead) return;
            EventCenter.Instance.Emit("PlayerSkillRequested", slot);
        }
    }

    /// <summary>
    /// 玩家攻击命中事件载荷。DamageSystem 订阅后转伤害计算。
    /// 放在 PlayerController 同文件里是因为它只服务于"玩家攻击命中 → 伤害系统"这一条链路。
    /// </summary>
    public struct PlayerAttackHitData
    {
        public int attackerId;
        public int attackerPower;
        public Collider targetCollider;
        public Vector3 hitPoint;
    }
}
