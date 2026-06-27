// AUTO-GENERATED from arcanum-ce obj.c/obj.h — Arcanum object field schema.
namespace Arcanum.Formats.Objects
{
    internal static class ObjectFieldData
    {
        public const int FieldCount = 313;
        public const int F_CURRENT_AID = 1;
        public const int F_LOCATION = 2;
        public const int F_BEGIN = 0, F_END = 37;
        // OD type per field enum value (0..FieldCount).
        public static readonly byte[] OdType = {
            1,3,4,3,3,3,9,9,9,3,3,3,3,3,3,3,9,9,9,3,3,3,3,3,
            3,3,3,3,3,3,3,7,11,3,3,9,10,2,1,3,3,3,9,10,2,1,3,3,
            3,3,3,3,9,10,2,1,3,3,3,3,13,3,3,3,3,9,10,2,1,3,6,3,
            3,9,10,2,1,3,3,3,6,3,3,9,10,2,1,3,6,3,3,3,3,3,3,3,
            3,3,3,3,3,3,3,3,3,3,3,3,9,10,2,1,3,3,3,3,7,7,7,3,
            3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,9,10,2,1,3,3,3,
            3,3,9,10,2,1,3,3,3,3,7,7,3,3,3,3,9,10,2,1,3,3,3,3,
            9,10,2,1,3,3,3,9,10,2,1,3,3,3,9,10,2,1,3,3,3,9,10,2,
            1,3,9,3,3,9,10,2,1,3,3,3,3,3,3,9,10,2,1,3,3,3,9,10,
            2,1,3,3,7,7,7,9,3,3,3,3,9,9,6,3,6,6,6,6,6,3,13,3,
            3,13,4,3,3,3,3,3,3,9,10,2,1,3,3,9,10,3,3,12,9,10,9,10,
            3,10,9,9,9,3,5,3,9,9,3,3,9,10,2,1,3,6,3,6,6,3,3,10,
            3,4,4,3,3,3,6,3,3,13,9,9,3,3,3,9,13,2,1,3,3,3,9,10,
            2
        };
        public static readonly int[] GroupBegin = {0,38,45,55,68,76,86,111,140,149,163,171,178,185,192,200,210,217,252,279,306};
        public static readonly int[] GroupParentLast = {-1,36,36,36,36,36,36,109,109,109,109,109,109,109,109,109,109,36,250,250,36};
        public static readonly int[] TypeRangeBegin = {38,45,55,68,76,86,111,86,140,86,149,86,163,86,171,86,178,86,185,86,192,86,200,86,210,217,252,217,279,306};
        public static readonly int[] TypeRangeEnd = {44,54,67,75,85,110,139,110,148,110,162,110,170,110,177,110,184,110,191,110,199,110,209,110,216,251,278,251,305,312};
        public static readonly int[] TypeRangeOffset = {0,1,2,3,4,5,7,9,11,13,15,17,19,21,23,25,27,29,30};  // index by type, [off,off+1)..
        // [16]=Npc must be the last NPC field (304), not 250 (the last CRITTER field): NPC extends
        // CRITTER, so its field_48 bitmap includes the npc-specific dwords too. 250 left the bitmap
        // one dword short, desyncing every NPC's field walk (no location / array-size overflows).
        public static readonly int[] TypeLastField = {43,53,66,74,84,138,147,161,169,176,183,190,198,208,215,277,304,311};
    }
}