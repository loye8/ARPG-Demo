using UnityEngine;
using UnityEngine.AI;
using ARPGCombat.Core;

namespace ARPGCombat.Gameplay
{
    /// <summary>
    /// 敌人生成器（面试点：NavMesh 表面采样 + 波次调度 + 对象池前置结构）。
    ///
    /// 职责：
    /// 1. 在玩家周围合法的 NavMesh 区域生成敌人（不是直接 Instantiate 在固定点）
    /// 2. 简单波次：每波 N 个敌人，全死后等下一波
    /// 3. 限制场上敌人上限，避免性能爆炸
    ///
    /// Day3 用 Instantiate，Day4 改造为对象池 Return 复用（结构已留好）
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("生成配置")]
        [Tooltip("敌人预制体（带 EnemyController）")]
        public GameObject enemyPrefab;

        [Tooltip("玩家 Transform（生成点会以玩家为中心）")]
        public Transform playerTransform;

        [Tooltip("生成半径：在玩家周围多少米范围内生成")]
        public float spawnRadius = 12f;

        [Tooltip("生成点到玩家的最小距离（防止贴脸生成）")]
        public float minSpawnDistance = 6f;

        [Header("波次配置")]
        [Tooltip("初始波次敌人数")]
        public int initialWaveCount = 3;

        [Tooltip("每波增加的敌人数")]
        public int waveIncrement = 1;

        [Tooltip("场上最大敌人数（防止性能爆炸）")]
        public int maxConcurrent = 8;

        [Tooltip("下一波延迟（秒）")]
        public float nextWaveDelay = 3f;

        [Header("调试")]
        public bool autoStart = true;

        private int _currentWave = 0;
        private int _aliveEnemies = 0;
        private float _nextWaveTimer = 0f;
        private bool _waitingForNextWave = false;

        void OnEnable()
        {
            EventCenter.Instance.On("EnemyDied", HandleEnemyDied);
        }

        void OnDisable()
        {
            EventCenter.Instance?.Off("EnemyDied", HandleEnemyDied);
        }

        void Start()
        {
            if (autoStart)
            {
                // 延迟 1 秒等 PlayerController.Awake 完成，避免 TargetPlayer 还没就绪
                Invoke(nameof(StartNextWave), 1f);
            }
        }

        void Update()
        {
            if (_waitingForNextWave)
            {
                _nextWaveTimer -= Time.deltaTime;
                if (_nextWaveTimer <= 0f)
                {
                    _waitingForNextWave = false;
                    StartNextWave();
                }
            }
        }

        // ===================== 波次调度 =====================
        private void StartNextWave()
        {
            _currentWave++;
            int count = initialWaveCount + (_currentWave - 1) * waveIncrement;

            Debug.Log($"<color=cyan>[EnemySpawner]</color> 开始第 {_currentWave} 波，目标生成 {count} 个敌人");

            for (int i = 0; i < count; i++)
            {
                if (_aliveEnemies >= maxConcurrent) break;  // 上限保护
                SpawnOneEnemy();
            }
        }

        private void SpawnOneEnemy()
        {
            if (enemyPrefab == null || playerTransform == null)
            {
                Debug.LogWarning("[EnemySpawner] enemyPrefab 或 playerTransform 未配置");
                return;
            }

            Vector3 spawnPos = GetRandomNavMeshPosition();
            if (spawnPos == Vector3.zero)
            {
                Debug.LogWarning("[EnemySpawner] 找不到合法生成点，跳过本次");
                return;
            }

            // Instantiate 生成敌人（Day4 改为对象池 Get）
            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            enemy.name = $"Enemy_{_aliveEnemies + 1}_Wave{_currentWave}";
            _aliveEnemies++;
        }

        /// <summary>在玩家周围 NavMesh 上找一个合法生成点。</summary>
        private Vector3 GetRandomNavMeshPosition()
        {
            // 在玩家周围 spawnRadius 球内取随机方向，minSpawnDistance 到 spawnRadius 之间取距离
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                float dist = Random.Range(minSpawnDistance, spawnRadius);
                Vector3 candidate = playerTransform.position + new Vector3(randomDir.x, 0, randomDir.y) * dist;

                // 在 NavMesh 上找最近可走点（半径 2m）
                if (NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }
            return Vector3.zero;  // 10 次都没找到合法点，返回 0 表示失败
        }

        // ===================== 事件处理 =====================
        private void HandleEnemyDied(object data)
        {
            _aliveEnemies--;
            if (_aliveEnemies < 0) _aliveEnemies = 0;

            Debug.Log($"<color=cyan>[EnemySpawner]</color> 敌人死亡，剩余 {_aliveEnemies} 个");

            // 当前波全部死亡 → 等延迟后开下一波
            if (_aliveEnemies == 0 && !_waitingForNextWave)
            {
                _waitingForNextWave = true;
                _nextWaveTimer = nextWaveDelay;
                Debug.Log($"<color=cyan>[EnemySpawner]</color> 当前波清空，{nextWaveDelay} 秒后开始下一波");
            }
        }
    }
}
