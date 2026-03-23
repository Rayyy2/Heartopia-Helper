using System;
using HarmonyLib;
using UnityEngine;

// Token: 0x0200000B RID: 11
[HarmonyPatch(typeof(Input), "GetKeyUp", new Type[]
{
	typeof(string)
})]
public class AutoFishGetKeyUpStringPatch
{
	// Token: 0x06000039 RID: 57 RVA: 0x000061F4 File Offset: 0x000043F4
	public static void Postfix(string name, ref bool __result)
	{
		bool flag = name == null;
		if (!flag)
		{
			string a = name.ToLower();
			bool flag2 = a == "f" && AutoFishLogic.SimulateFishFKeyUp;
			if (flag2)
			{
				__result = true;
			}
		}
	}
}
