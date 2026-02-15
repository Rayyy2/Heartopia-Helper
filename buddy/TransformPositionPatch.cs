using System;
using HarmonyLib;
using UnityEngine;

namespace HeartopiaMod
{
    // Token: 0x02000006 RID: 6
    [HarmonyPatch(typeof(Transform), "position", MethodType.Getter)]
    public static class TransformPositionPatch
    {
        // Token: 0x0600002C RID: 44 RVA: 0x00008344 File Offset: 0x00006544
        public static bool SetPositionPrefix(Transform __instance, ref Vector3 value)
        {
            bool flag = __instance == null || __instance.gameObject == null;
            bool result;
            if (flag)
            {
                result = true;
            }
            else
            {
                string name = __instance.gameObject.name;
                bool flag2 = HeartopiaComplete.OverridePlayerPosition && name == "p_player_skeleton(Clone)";
                if (flag2)
                {
                    value = HeartopiaComplete.OverridePosition;
                }
                bool flag3 = HeartopiaComplete.OverrideCameraPosition && name == "Main Camera";
                if (flag3)
                {
                    value = HeartopiaComplete.CameraOverridePos;
                }
                result = true;
            }
            return result;
        }
    }
}
