using UnityEngine;
using UnityEngine.AI;
using ARPGCombat.Core;
using ARPGCombat.Data;
using ARPGCombat.MVC.Model;
using ARPGCombat.FSM;
using ARPGCombat.FSM.EnemyStates;
using ARPGCombat.Gameplay;

namespace ARPGCombat.MVC.Controller
{
    /// <summary>
    /// MVC Controller 层：敌人行为控制（面试点：FSM 状态机 + NavMesh 寻路 + 事件驱动）。
    ///
    /// 职责：
    /// 1. 持有 CharacterModel（数据）和 StateMachine（行为调度器）
    /// 2. 创建 4 个状态实例（Patrol/Chase/Attack/Death），交给 StateMachine 管理
    /// 3. 每帧 Update 调用 stateMachine.Update() 让当前状态执行逻辑
    /// 4. 监听 CharacterDied 事件 → 如果是自己死了 → 切 DeathState（外部死亡触发，不靠状态自检）
    /// 5. 提供状态切换的 API（如 stateMachine.ChangeState(chaseState)）给各 State 调用
    ///
    /// 设计亮点：
    /// - Controller 不写"巡逻怎么走、追击怎么追"的具体逻辑，那些都在 State 类里。
    ///   Controller 只负责"装配 + 调度"。这正是 MVC Controller 的本职——协调，不实现。
    /// - 状态之间不互相直接引用，通过 controller.StateMachine.ChangeState() 切换，
    ///   降低耦合（PatrolState 不需要知道 ChaseState 存在，只需说"我要切到追击"，让 Controller 决定）。
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        // ===================== Inspector 配置 =====================
        [Header("数据配置")]
        public CharacterConfig config;

        [Header("FSM 参数")]
        [Tooltip("视野范围（米）：玩家进入此范围会触发追击")]
        public float sightRange = 8f;

        [Tooltip("巡逻范围（米）：以生成点为中心的随机巡逻半径")]
        public float patrolRadius = 6f;

        [Tooltip("巡逻点切换间隔（秒）：到达一个巡逻点后停顿多久再选下一个")]
        public float patrolWaitTime = 1.5f;

        [Tooltip("攻击间隔（秒）：进入攻击范围后多久打一下")]
        public float attackInterval = 1.2f;

        [Tooltip("死亡后销毁延迟（秒）")]
        public float destroyDelay = 3f;

        [Header("引用")]
        [Tooltip("Animator（可空，Capsule 方案不挂也能跑）")]
        public Animator anim;

        // ===================== 运行时 =====================
        public CharacterModel Model { get; private set; }
        public StateMachine StateMachine { get; private set; }

        // 4 个状态实例（启动时创建一次，状态机内部切换，不重复 new → 零 GC）
        public PatrolState PatrolState { get; private set; }
        public ChaseState ChaseState { get; private set; }
        public AttackState AttackState { get; private set; }
        public DeathState DeathState { get; private set; }

        public NavMeshAgent Agent { get; private set; }
        public PlayerController TargetPlayer { get; private set; }
        public Vector3 SpawnPosition { get; private set; }

        private static int _enemyIdCounter = 1000;  // 敌人 ID 从 1000 起，与玩家 ID 不冲突

        // ===================== 生命周期 =====================
        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            SpawnPosition = transform.position;

            // 1. 初始化 Model（从 SO 配置）
            if (config == null)
            {
                Debug.LogError($"[EnemyController] {name} 的 CharacterConfig 未配置！");
                enabled = false;
                return;
            }
            Model = new CharacterModel(_enemyIdCounter++, config);

            // 2. 注册到注册表（让 DamageSystem 能把 Collider → Model）
            // NavMeshAgent 自带 Collider（如果没加，会自动加 Capsule Collider）
            var col = GetComponent<Collider>();
            if (col != null)
                CharacterRegistry.Instance.Register(Model, col);

            // 3. 配置 NavMeshAgent（从 Model 读速度，确保数据驱动）
            Agent.speed = Model.MoveSpeed;
            Agent.stoppingDistance = Model.AttackRange * 0.8f;  // 留点余量，让 AttackState 判定更稳

            // 4. 创建 4 个状态（一次创建，状态机复用 → 运行时零 GC）
            PatrolState = new PatrolState(this);
            ChaseState = new ChaseState(this);
            AttackState = new AttackState(this);
            DeathState = new DeathState(this);

            // 5. 创建状态机并初始化为巡逻状态
            StateMachine = new StateMachine();
            StateMachine.Initialize(PatrolState);
        }

        private void OnEnable()
        {
            EventCenter.Instance.On("PlayerCreated", HandlePlayerCreated);
            EventCenter.Instance.On("CharacterDied", HandleCharacterDied);
        }

        private void OnDisable()
        {
            EventCenter.Instance?.Off("PlayerCreated", HandlePlayerCreated);
            EventCenter.Instance?.Off("CharacterDied", HandleCharacterDied);
        }

        private void Update()
        {
            if (Model.IsDead) return;  // 死亡后不再 Update（DeathState 自己处理收尾）
            StateMachine.Update();
        }

        // ===================== 事件处理 =====================
        private void HandlePlayerCreated(object data)
        {
            // 玩家创建后拿到引用，ChaseState/AttackState 用它读 transform.position
            if (data is PlayerController player)
                TargetPlayer = player;
        }

        private void HandleCharacterDied(object data)
        {
            if (data is not DamageEventData d) return;
            if (d.targetId != Model.InstanceId) return;  // 不是自己死，忽略

            // 自己死了 → 切 DeathState（DeathState 会处理动画/碰撞/销毁）
            // 注意：这里不再走 Model.IsDead 判断，而是事件直接驱动，保证响应即时
            StateMachine.ChangeState(DeathState);
        }

        // ===================== 工具方法（给 State 用） =====================

        /// <summary>玩家是否存在且未死。状态机里频繁用，封装避免重复判断。</summary>
        public bool HasValidTarget()
        {
            return TargetPlayer != null && !TargetPlayer.Model.IsDead;
        }

        /// <summary>水平距离玩家（忽略 Y）。State 用来判断视野/攻击范围。</summary>
        public float DistanceToPlayer()
        {
            if (!HasValidTarget()) return float.MaxValue;
            Vector3 a = transform.position;
            Vector3 b = TargetPlayer.transform.position;
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void OnDestroy()
        {
            if (Model != null)
            {
                var col = GetComponent<Collider>();
                CharacterRegistry.Instance?.Unregister(Model, col);
            }
        }
    }
}
