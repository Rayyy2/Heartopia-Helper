using System;
using HarmonyLib;
using UnityEngine;

// Token: 0x02000010 RID: 16
[HarmonyPatch(typeof(Input), "GetMouseButtonUp", new Type[]
{
	typeof(int)
})]
public class AutoFishGetMouseButtonUpPatch
{
	// Token: 0x06000043 RID: 67 RVA: 0x000063C0 File Offset: 0x000045C0
	public static void Postfix(int button, ref bool __result)
	{
		bool flag = button == 0 && AutoFishLogic.SimulateMouseButton0Up;
		if (flag)
		{
			__result = true;
		}
	}
}
