using Arcanum.World.Demo;
using UnityEditor;
using UnityEngine;

namespace Arcanum.Editor
{
    /// <summary>
    /// Inspector for <see cref="TileMapDemo"/> — adds a button that opens the searchable
    /// <see cref="SectorBrowserWindow"/> for picking which sector to render.
    /// </summary>
    [CustomEditor(typeof(TileMapDemo))]
    public sealed class TileMapDemoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Browse sectors…"))
                SectorBrowserWindow.Open((TileMapDemo)target);

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox(
                    "Enter Play mode to load a sector live from the browser. In edit mode, Load just sets the " +
                    "Sector Path field (it renders when you press Play).", MessageType.Info);
        }
    }
}
