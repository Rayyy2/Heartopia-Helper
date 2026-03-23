using System;
using HarmonyLib;
using UnityEngine;

// Token: 0x0200000E RID: 14
[HarmonyPatch(typeof(Input), "GetMouseButton", new Type[]
{
	typeof(int)
})]
public class AutoFishGetMouseButtonPatch
{
	// Token: 0x0600003F RID: 63 RVA: 0x00006368 File Offset: 0x00004568
	public static void Postfix(int button, ref bool __result)
	{
		bool flag = button == 0 && AutoFishLogic.SimulateMouseButton0;
		if (flag)
		{
			__result = true;
		}
	}
}
