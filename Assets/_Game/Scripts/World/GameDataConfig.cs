using System.Collections.Generic;
using UnityEngine;

namespace Arcanum.Runtime
{
    /// <summary>
    /// Where to find the original Arcanum data (the <c>.dat</c> archives) — so install paths live in an asset,
    /// not hard-coded in code. Create one via <b>Assets → Create → Arcanum → Game Data Config</b>, place it in a
    /// <c>Resources</c> folder named <c>GameDataConfig</c>, and point <see cref="Roots"/> at your install (the
    /// folder that contains <c>arcanum1.dat … arcanum4.dat</c>). <see cref="GameDataLocator"/> reads it.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDataConfig", menuName = "Arcanum/Game Data Config")]
    public sealed class GameDataConfig : ScriptableObject
    {
        [Tooltip("Folders searched (in order) for the .dat archives. Point one at your Arcanum install — the " +
                 "folder containing arcanum1.dat … arcanum4.dat.")]
        [SerializeField]
        private string[] DataRoots = System.Array.Empty<string>();

        [Tooltip("Also search the project-local GameData/ drop folder (relative to the project), in addition to Data Roots.")]
        [SerializeField]
        private bool SearchCommonLocations = true;

        public IReadOnlyList<string> Roots => DataRoots;
        public bool AlsoSearchCommonLocations => SearchCommonLocations;
    }
}
