using System;
using UnityEngine;

namespace Rishi.Core
{
    /// <summary>
    /// 跨模块共享的全局状态和事件。各模块只依赖 Core，不互相引用：
    /// 剧情 UI 写 InputBlocked，过场写 CutsceneActive，玩家注册自己，昼夜读碎片数。
    /// </summary>
    public static class GameSignals
    {
        /// <summary>阅读笔记、查看日志时为 true。</summary>
        public static bool InputBlocked { get; set; }
        /// <summary>过场动画播放中。</summary>
        public static bool CutsceneActive { get; set; }
        /// <summary>玩家移动、射击、互动都应暂停。</summary>
        public static bool GameplayBlocked => InputBlocked || CutsceneActive;

        /// <summary>已收集的太阳之心碎片数。</summary>
        public static int Fragments { get; private set; }
        public static event Action<int> FragmentsChanged;

        /// <summary>当前玩家（PlayerController 启用时注册）。</summary>
        public static Transform Player { get; private set; }

        public static void SetFragments(int count)
        {
            if (count == Fragments) return;
            Fragments = count;
            FragmentsChanged?.Invoke(count);
        }

        public static void RegisterPlayer(Transform player) => Player = player;

        public static void UnregisterPlayer(Transform player)
        {
            if (Player == player) Player = null;
        }

        /// <summary>碰撞体是否属于玩家（触发器用）。</summary>
        public static bool IsPlayer(Component other) => Player != null && other != null && other.transform.IsChildOf(Player);

        /// <summary>进入播放模式时清空（关闭 Domain Reload 时静态变量不会自动重置）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetAll()
        {
            InputBlocked = false;
            CutsceneActive = false;
            Fragments = 0;
            FragmentsChanged = null;
            Player = null;
        }
    }
}
