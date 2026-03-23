using System;
using HarmonyLib;
using UnityEngine;

namespace HeartopiaMod
{
	// Token: 0x02000003 RID: 3
	[HarmonyPatch(typeof(CharacterController), "Move")]
	public static class CharacterControllerPatch
	{
		// Token: 0x060000AC RID: 172 RVA: 0x0001C0E0 File Offset: 0x0001A2E0
		public static bool MovePrefix(CharacterController __instance, ref Vector3 motion)
		{
			// Only apply motion override to the local player's CharacterController
			if (HeartopiaComplete.OverridePlayerPosition && __instance != null && __instance.gameObject == HeartopiaComplete.GetLocalPlayer())
			{
				Vector3 position = __instance.transform.position;
				Vector3 vector = HeartopiaComplete.OverridePosition - position;
				motion = vector;
			}
			return true;
		}
	}
}
