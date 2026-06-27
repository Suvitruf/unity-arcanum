using System.Runtime.CompilerServices;

// Expose internal schema tables (ObjectFieldData / ObjectFieldEngine) to the EditMode test assembly so tests can
// build valid synthetic object records (the field-48 change bitmap) without duplicating the engine's tables.
[assembly: InternalsVisibleTo("Arcanum.Formats.Tests")]
