using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Arcanum.Runtime
{
    /// <summary>
    /// Resolves original Arcanum data files (the <c>.dat</c> archives) by file name (e.g. <c>arcanum2.dat</c>)
    /// without hard-coding a machine path in code. The install location is configured in a
    /// <see cref="GameDataConfig"/> asset (in a <c>Resources</c> folder); the project-local <c>GameData/</c>
    /// drop folder is also searched as a convenience unless the config turns that off. No machine-specific
    /// install paths live in code. Lets components reference an archive by name without knowing where it lives.
    /// </summary>
    public static class GameDataLocator
    {
        /// <summary>Resources name of the <see cref="GameDataConfig"/> asset.</summary>
        private const string ConfigResourceName = "GameDataConfig";

        private static GameDataConfig _config;
        private static bool _configLoaded;

        private static GameDataConfig Config
        {
            get
            {
                if (!_configLoaded)
                {
                    _config = Resources.Load<GameDataConfig>(ConfigResourceName);
                    _configLoaded = true;
                }

                return _config;
            }
        }

        /// <summary>Candidate directories searched in order; first hit wins.</summary>
        private static IEnumerable<string> CandidateRoots()
        {
            GameDataConfig config = Config;

            // 1) Roots configured in the GameDataConfig asset (set this up for your install).
            if (config != null)
                foreach (string root in config.Roots)
                {
                    if (!string.IsNullOrWhiteSpace(root))
                    {
                        yield return Path.Combine(root, "Arcanum");
                    }
                }
            else
            {
                Debug.LogError("No root config found");
            }


            // 2) Built-in conveniences — the project-local drop folder + common install locations.
            if (config == null || config.AlsoSearchCommonLocations)
                foreach (string root in DefaultRoots())
                    yield return root;
        }

        // The only non-config location: a project-local drop folder. No machine-specific install paths in code —
        // point the GameDataConfig asset's Data Roots at your install instead.
        private static IEnumerable<string> DefaultRoots()
        {
            // Relative, never committed.
            yield return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "GameData"));
        }

        /// <summary>Returns the full path to <paramref name="fileName"/> if found, otherwise null.</summary>
        public static string Find(string fileName)
        {
            foreach (string root in CandidateRoots())
            {
                if (string.IsNullOrEmpty(root)) continue;
                string candidate = Path.Combine(root, fileName);
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }
    }
}
