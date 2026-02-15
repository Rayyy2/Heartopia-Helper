using HarmonyLib;
using System;
using UnityEngine;

namespace HeartopiaMod
{
    // Token: 0x02000005 RID: 5
    [HarmonyPatch(typeof(CharacterController), "Move")]
    public static class CharacterControllerPatch
    {
        // Token: 0x0600002B RID: 43 RVA: 0x00008304 File Offset: 0x00006504
        public static bool MovePrefix(CharacterController __instance, ref Vector3 motion)
        {
            bool overridePlayerPosition = HeartopiaComplete.OverridePlayerPosition;
            if (overridePlayerPosition)
            {
                Vector3 position = __instance.transform.position;
                Vector3 vector = HeartopiaComplete.OverridePosition - position;
                motion = vector;
            }
            return true;
        }
    }
}
