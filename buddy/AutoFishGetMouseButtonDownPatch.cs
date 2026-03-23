using System;
using HarmonyLib;
using UnityEngine;

// Token: 0x0200000F RID: 15
[HarmonyPatch(typeof(Input), "GetMouseButtonDown", new Type[]
{
	typeof(int)
})]
public class AutoFishGetMouseButtonDownPatch
{
	// Token: 0x06000041 RID: 65 RVA: 0x00006394 File Offset: 0x00004594
	public static void Postfix(int button, ref bool __result)
	{
		bool flag = button == 0 && AutoFishLogic.SimulateMouseButton0Down;
		if (flag)
		{
			__result = true;
		}
	}
}
