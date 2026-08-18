using System.Collections.Generic;
using UnityEngine;
using ARPGCombat.Core;
using ARPGCombat.MVC.Model;

namespace ARPGCombat.Gameplay
{
    /// <summary>
    /// 角色注册表：把 Unity 的 Collider 映射到 MVC Model（面试点：物理层 → 业务层的桥）。
    ///
    /// 为什么需要它？
    /// OverlapSphere 返回的是 Unity Collider，而扣血要操作的是 CharacterModel。
    /// DamageSystem 需要一个 O(1) 表把 Collider 查到 CharacterModel。
    ///
    /// 设计：双字典
    /// - id → model：方便按 ID 查（击杀计数、玩家/敌人定位）
    /// - collider → model：OverlapSphere 拿到 Collider 直接查
    ///
    /// 注册方式：PlayerController/EnemyController 在创建 Model 后调用 Register()。
    /// </summary>
    public class CharacterRegistry : Singleton<CharacterRegistry>
    {
        private readonly Dictionary<int, CharacterModel> _byId = new Dictionary<int, CharacterModel>();
        private readonly Dictionary<Collider, CharacterModel> _byCollider = new Dictionary<Collider, CharacterModel>();

        public void Register(CharacterModel model, Collider rootCollider)
        {
            if (model == null || rootCollider == null) return;
            _byId[model.InstanceId] = model;
            _byCollider[rootCollider] = model;
        }

        public void Unregister(CharacterModel model, Collider rootCollider)
        {
            if (model != null) _byId.Remove(model.InstanceId);
            if (rootCollider != null) _byCollider.Remove(rootCollider);
        }

        public CharacterModel GetById(int id) =>
            _byId.TryGetValue(id, out var m) ? m : null;

        public CharacterModel GetByCollider(Collider c) =>
            c != null && _byCollider.TryGetValue(c, out var m) ? m : null;

        public bool TryGetByCollider(Collider c, out CharacterModel model) =>
            _byCollider.TryGetValue(c, out model) && model != null;

        public int Count => _byId.Count;
    }
}
