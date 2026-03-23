using System;
using HarmonyLib;
using UnityEngine;

// Token: 0x02000011 RID: 17
[HarmonyPatch(typeof(Input), "get_mousePosition")]
public class AutoFishMousePositionPatch
{
	// Token: 0x06000045 RID: 69 RVA: 0x000063EC File Offset: 0x000045EC
	public static void Postfix(ref Vector3 __result)
	{
		bool overrideMousePosition = AutoFishLogic.OverrideMousePosition;
		if (overrideMousePosition)
		{
			__result = AutoFishLogic.SimulateMousePosition;
		}
	}
}
