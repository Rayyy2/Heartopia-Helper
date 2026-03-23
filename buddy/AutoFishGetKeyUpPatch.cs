using System;
using HarmonyLib;
using UnityEngine;

// Token: 0x02000008 RID: 8
[HarmonyPatch(typeof(Input), "GetKeyUp", new Type[]
{
	typeof(KeyCode)
})]
public class AutoFishGetKeyUpPatch
{
	// Token: 0x06000033 RID: 51 RVA: 0x00006038 File Offset: 0x00004238
	public static void Postfix(KeyCode key, ref bool __result)
	{
		bool flag = key == KeyCode.F && AutoFishLogic.SimulateFishFKeyUp;
		if (flag)
		{
			__result = true;
		}
	}
}
