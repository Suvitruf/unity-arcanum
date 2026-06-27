using System;

namespace Arcanum.Formats
{
    /// <summary>
    /// Thrown when an Arcanum data file cannot be parsed because its contents
    /// do not match the expected on-disk layout. The message is intentionally
    /// verbose so format discrepancies are easy to diagnose against real data.
    /// </summary>
    public sealed class DatFormatException : Exception
    {
        public DatFormatException(string message) : base(message)
        {
        }

        public DatFormatException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
