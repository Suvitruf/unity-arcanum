namespace Arcanum.Formats.Objects
{
    /// <summary>
    /// Object data-field storage types (<c>OD_TYPE_*</c> from arcanum-ce obj_private.c).
    /// Determines how a field serializes on disk.
    /// </summary>
    internal enum OdType : byte
    {
        Invalid = 0, Begin = 1, End = 2,
        Int32 = 3, Int64 = 4, String = 5, Handle = 6,
        Int32Array = 7, Int64Array = 8, UInt32Array = 9, UInt64Array = 10,
        ScriptArray = 11, QuestArray = 12, HandleArray = 13,
        Ptr = 14, PtrArray = 15,
    }

    /// <summary>
    /// Ports Arcanum's object field-bit allocation (obj.c <c>sub_40A400</c>/<c>sub_40B8E0</c>):
    /// every field gets a change-bitmap slot <c>(ChangeIdx, Mask)</c>, and each object type a
    /// <c>field_48</c> dword count. Computed once from <see cref="ObjectFieldData"/>.
    /// Validated against shipped data: walking a real sector's 563 objects lands byte-exact on
    /// the trailing object count.
    /// </summary>
    internal static class ObjectFieldEngine
    {
        public static readonly int[] ChangeIdx;   // per field: dword index into field_48
        public static readonly uint[] Mask;       // per field: bit mask within that dword
        public static readonly int[] DwordCount;  // per object type (0..17): field_48 dword count

        static ObjectFieldEngine()
        {
            int n = ObjectFieldData.FieldCount;
            ChangeIdx = new int[n];
            Mask = new uint[n];
            var begins = ObjectFieldData.GroupBegin;
            var baseDword = new int[begins.Length];

            for (int fld = 0; fld < n; fld++)
            {
                var od = (OdType)ObjectFieldData.OdType[fld];
                if (od == OdType.Begin)
                {
                    int gi = GroupIndex(fld);
                    int parentLast = ObjectFieldData.GroupParentLast[gi];
                    baseDword[gi] = parentLast < 0 ? 0 : ChangeIdx[parentLast] + 1;
                    continue;
                }
                ChangeIdx[fld] = -1;
                if (od == OdType.End) continue;

                int gs = GroupStart(fld);
                int localIdx = fld - gs - 1;
                ChangeIdx[fld] = localIdx / 32 + baseDword[GroupIndex(gs)];
                Mask[fld] = 1u << (localIdx % 32);
            }

            DwordCount = new int[18];
            for (int type = 0; type < 18; type++)
            {
                int last = type < ObjectFieldData.TypeLastField.Length ? ObjectFieldData.TypeLastField[type] : -1;
                DwordCount[type] = last >= 0 ? ChangeIdx[last] + 1 : 0;
            }
        }

        /// <summary>Largest group-begin field value strictly less than <paramref name="fld"/>.</summary>
        private static int GroupStart(int fld)
        {
            var begins = ObjectFieldData.GroupBegin;
            int start = 0;
            for (int i = 0; i < begins.Length; i++)
            {
                if (begins[i] < fld) start = begins[i];
                else break;
            }
            return start;
        }

        private static int GroupIndex(int beginFieldValue)
        {
            var begins = ObjectFieldData.GroupBegin;
            for (int i = 0; i < begins.Length; i++)
                if (begins[i] == beginFieldValue) return i;
            return 0;
        }
    }
}
