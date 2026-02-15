using System;
using HarmonyLib;
using UnityEngine;

namespace HeartopiaMod
{
    // Token: 0x02000007 RID: 7
    [HarmonyPatch(typeof(Transform), "rotation", MethodType.Getter)]
    public static class TransformRotationPatch
    {
        // Token: 0x0600002D RID: 45 RVA: 0x000083D4 File Offset: 0x000065D4
        public static bool SetRotationPrefix(Transform __instance, ref Quaternion value)
        {
            bool flag = __instance == null || __instance.gameObject == null;
            bool result;
            if (flag)
            {
                result = true;
            }
            else
            {
                bool flag2 = HeartopiaComplete.OverrideCameraPosition && __instance.gameObject.name == "Main Camera";
                if (flag2)
                {
                    value = HeartopiaComplete.CameraOverrideRot;
                }
                result = true;
            }
            return result;
        }
    }
}
