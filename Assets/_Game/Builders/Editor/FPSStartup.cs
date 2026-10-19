using UnityEditor;
using UnityEditor.SceneManagement;
using Rishi.Core;
using Rishi.Inventory;
using Rishi.World;
using Rishi.Audio;
using Rishi.Player;
using Rishi.Weapons;
using Rishi.UI;
using Rishi.Story;
using Rishi.Cutscene;
using Rishi.Wildlife;
using Rishi.Rendering;

namespace Rishi.Builders
{
    /// <summary>
    /// 编辑器启动时自动打开荒岛场景，并让 Play 始终从荒岛场景开始（没有荒岛场景时退回 FPS 靶场）。
    /// </summary>
    [InitializeOnLoad]
    public static class FPSStartup
    {
        static readonly string[] Candidates = { IslandBuilder.ScenePath, "Assets/Scenes/FPSTest.unity" };

        static FPSStartup()
        {
            EditorApplication.delayCall += () =>
            {
                foreach (var path in Candidates)
                {
                    var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                    if (scene == null) continue;

                    EditorSceneManager.playModeStartScene = scene;
                    var active = EditorSceneManager.GetActiveScene();
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && active.path != path && !active.isDirty
                        && !UnityEditorInternal.InternalEditorUtility.inBatchMode)
                        EditorSceneManager.OpenScene(path);
                    return;
                }
            };
        }
    }
}
