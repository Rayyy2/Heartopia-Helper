using System;
using HarmonyLib;
using UnityEngine;

namespace HeartopiaMod
{
    [HarmonyPatch(typeof(Transform), "rotation", MethodType.Setter)]
    public static class CharacterRotationPatch
    {
        public static bool SetRotationPrefix(Transform __instance, ref Quaternion value)
        {
            if (__instance == null || __instance.gameObject == null) return true;

            // Override player character rotation
            if (HeartopiaComplete.OverridePlayerRotation &&
                __instance.gameObject.name == "p_player_skeleton(Clone)")
            {
                // Replace the value being set with our override rotation
                value = HeartopiaComplete.PlayerOverrideRot;
            }

            return true; // Continue with setter using our modified value
        }
    }
}
