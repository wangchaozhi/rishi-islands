using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rishi.Core
{
    /// <summary>
    /// 跨模块共享的全局状态和事件。各模块只依赖 Core，不互相引用：
    /// 界面打开时登记输入锁定，过场写 CutsceneActive，玩家注册自己并写入水中状态，昼夜读碎片数，任何模块都能发提示。
    /// </summary>
    public static class GameSignals
    {
        static readonly HashSet<object> inputBlockers = new HashSet<object>();
        static readonly object DefaultBlocker = new object();

        /// <summary>有任何界面（笔记、日志、背包……）占用输入时为 true。</summary>
        public static bool InputBlocked
        {
            get => inputBlockers.Count > 0;
            set => SetInputBlock(DefaultBlocker, value);
        }

        /// <summary>按来源登记 / 取消输入锁定，多个界面互不覆盖。</summary>
        public static void SetInputBlock(object owner, bool blocked)
        {
            if (blocked) inputBlockers.Add(owner);
            else inputBlockers.Remove(owner);
        }

        /// <summary>过场动画播放中。</summary>
        public static bool CutsceneActive { get; set; }
        /// <summary>玩家移动、射击、互动都应暂停。</summary>
        public static bool GameplayBlocked => InputBlocked || CutsceneActive;

        /// <summary>已收集的太阳之心碎片数。</summary>
        public static int Fragments { get; private set; }
        public static event Action<int> FragmentsChanged;

        /// <summary>当前玩家（PlayerController 启用时注册）。</summary>
        public static Transform Player { get; private set; }

        /// <summary>屏幕提示（拾取、目标、获得物品……），由剧情界面显示。</summary>
        public static event Action<string> ToastRaised;

        // ---------- 水 ----------
        /// <summary>海平面高度（世界坐标 y）。</summary>
        public const float SeaLevel = 0f;
        /// <summary>玩家正在游泳（水面或水下）。</summary>
        public static bool Swimming { get; set; }
        /// <summary>摄像机在水面以下。</summary>
        public static bool HeadUnderwater { get; set; }
        /// <summary>摄像机在水面下的深度（米），不在水下时为 0。</summary>
        public static float CameraDepth { get; set; }
        /// <summary>剩余氧气比例（0~1）。</summary>
        public static float Oxygen { get; set; } = 1f;

        /// <summary>全屏黑场（0~1），昏迷、传送时用。</summary>
        public static float ScreenFade { get; set; }

        public static void SetFragments(int count)
        {
            if (count == Fragments) return;
            Fragments = count;
            FragmentsChanged?.Invoke(count);
        }

        public static void Toast(string text) => ToastRaised?.Invoke(text);

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
            inputBlockers.Clear();
            CutsceneActive = false;
            Fragments = 0;
            FragmentsChanged = null;
            ToastRaised = null;
            Player = null;
            Swimming = HeadUnderwater = false;
            CameraDepth = 0f;
            Oxygen = 1f;
            ScreenFade = 0f;
        }
    }
}
