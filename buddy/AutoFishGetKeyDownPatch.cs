using System;
using HarmonyLib;
using UnityEngine;

// Token: 0x02000007 RID: 7
[HarmonyPatch(typeof(Input), "GetKeyDown", new Type[]
{
	typeof(KeyCode)
})]
public class AutoFishGetKeyDownPatch
{
	// Token: 0x06000031 RID: 49 RVA: 0x00005FB0 File Offset: 0x000041B0
	public static void Postfix(KeyCode key, ref bool __result)
	{
		bool flag = key == KeyCode.F && AutoFishLogic.SimulateFishFKeyDown;
		if (flag)
		{
			__result = true;
		}
		else
		{
			bool flag2 = key == KeyCode.W && AutoFishLogic.SimulateWKeyDown;
			if (flag2)
			{
				__result = true;
			}
			else
			{
				bool flag3 = key == KeyCode.A && AutoFishLogic.SimulateAKeyDown;
				if (flag3)
				{
					__result = true;
				}
				else
				{
					bool flag4 = key == KeyCode.S && AutoFishLogic.SimulateSKeyDown;
					if (flag4)
					{
						__result = true;
					}
					else
					{
						bool flag5 = key == KeyCode.D && AutoFishLogic.SimulateDKeyDown;
						if (flag5)
						{
							__result = true;
						}
					}
				}
			}
		}
	}
}
