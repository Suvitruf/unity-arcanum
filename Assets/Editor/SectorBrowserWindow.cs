using System;
using System.Collections.Generic;
using Arcanum.Formats.Database;
using Arcanum.Runtime;        // GameDataLocator
using Arcanum.World.Demo;
using UnityEditor;
using UnityEngine;

namespace Arcanum.Editor
{
    /// <summary>
    /// Browse the sectors in a <see cref="TileMapDemo"/>'s module archive, filter by name, and Load one into the
    /// demo. Sectors are enumerated straight from the archive (works in edit mode too). In Play mode, Load renders
    /// the sector immediately; otherwise it just sets the demo's Sector Path so it renders on the next Play.
    /// </summary>
    public sealed class SectorBrowserWindow : EditorWindow
    {
        private TileMapDemo _target;
        private readonly List<string> _sectors = new List<string>();
        private string _search = string.Empty;
        private Vector2 _scroll;
        private string _status;

        public static void Open(TileMapDemo target)
        {
            var window = GetWindow<SectorBrowserWindow>(utility: true, title: "Sector Browser", focus: true);
            window._target = target;
            window.minSize = new Vector2(380f, 420f);
            window.RefreshSectors();
            window.Show();
        }

        private void RefreshSectors()
        {
            _sectors.Clear();
            _status = null;

            if (_target == null)
            {
                _status = "No TileMapDemo selected.";
                return;
            }

            string moduleName = _target.ModuleArchiveName;
            string modulePath = GameDataLocator.Find(moduleName);
            if (string.IsNullOrEmpty(modulePath))
            {
                _status = $"Could not locate '{moduleName}'. Configure your game data (GameDataConfig).";
                return;
            }

            try
            {
                using var vfs = new DatVirtualFileSystem();
                vfs.MountFile(modulePath);
                foreach (string path in vfs.EnumerateFiles("maps/"))
                    if (path.EndsWith(".sec", StringComparison.OrdinalIgnoreCase))
                        _sectors.Add(path);
                _sectors.Sort(StringComparer.OrdinalIgnoreCase);
                _status = $"{_sectors.Count} sector(s) in '{moduleName}'.";
            }
            catch (Exception ex) { _status = $"Enumeration failed: {ex.Message}"; }
        }

        private void OnGUI()
        {
            if (!_target)
            {
                EditorGUILayout.HelpBox("Open this window from a TileMapDemo inspector (Browse sectors…).", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Target", _target.name);
                if (GUILayout.Button("Refresh", GUILayout.Width(70f))) RefreshSectors();
            }

            _search = EditorGUILayout.TextField("Search", _search);
            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);

            string current = _target.CurrentSector;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int shown = 0;
            foreach (string sector in _sectors)
            {
                if (!string.IsNullOrEmpty(_search) &&
                    sector.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                shown++;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Load", GUILayout.Width(50f))) LoadSector(sector);
                    bool isCurrent = string.Equals(sector, current, StringComparison.OrdinalIgnoreCase);
                    if (isCurrent) GUI.color = new Color(0.55f, 1f, 0.55f);
                    EditorGUILayout.LabelField(isCurrent ? sector + "  ◀ current" : sector);
                    GUI.color = Color.white;
                }
            }

            EditorGUILayout.EndScrollView();

            if (shown == 0 && _sectors.Count > 0)
                EditorGUILayout.LabelField("No matches.", EditorStyles.miniLabel);
        }

        private void LoadSector(string sector)
        {
            // Persist the choice on the component so it survives and re-serializes.
            var so = new SerializedObject(_target);
            SerializedProperty prop = so.FindProperty("SectorPath");
            if (prop != null)
            {
                prop.stringValue = sector;
                so.ApplyModifiedProperties();
            }

            if (Application.isPlaying) _target.LoadSector(sector);
            else EditorUtility.SetDirty(_target);

            Repaint();
        }
    }
}
