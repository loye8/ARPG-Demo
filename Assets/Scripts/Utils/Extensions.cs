using UnityEngine;

namespace ARPGCombat.Utils
{
    /// <summary>
    /// 通用扩展方法（面试点：C# 扩展方法提高可读性，减少重复工具函数）。
    /// 放 Utils 命名空间方便全项目 using ARPGCombat.Utils 后直接用。
    /// </summary>
    public static class Extensions
    {
        /// <summary>把 Vector3 的 Y 清零，用于水平距离/朝向计算（忽略高度差）。</summary>
        public static Vector3 Flatten(this Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>水平面上的距离（忽略 Y），ARPG 判断攻击/仇恨范围最常用的计算。</summary>
        public static float HorizontalDistance(this Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>LayerMask 里是否包含指定 layer。简化 Layer 判断写法。</summary>
        public static bool Contains(this LayerMask mask, int layer)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        /// <summary>把一个数值限制在 [0, max] 内，简化 HP 夹取写法。</summary>
        public static int Clamp0Max(this int v, int max) => Mathf.Clamp(v, 0, max);

        /// <summary>在相机空间下把屏幕点投射到地面（Y=0平面），ARPG 鼠标选目标必用。</summary>
        public static Vector3 ScreenPointToGroundPlane(this Camera cam, Vector3 screenPoint, float groundY = 0f)
        {
            Ray ray = cam.ScreenPointToRay(screenPoint);
            // 射线方程 O + t*D，令 y = groundY 求 t
            float t = (groundY - ray.origin.y) / ray.direction.y;
            if (t < 0) return Vector3.zero;  // 相机在地面下方，无交点
            return ray.GetPoint(t);
        }
    }
}
