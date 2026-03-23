using System;
using HarmonyLib;
using UnityEngine;

// Token: 0x02000006 RID: 6
[HarmonyPatch(typeof(Input), "GetKey", new Type[]
{
	typeof(KeyCode)
})]
public class AutoFishGetKeyPatch
{
	// Token: 0x0600002F RID: 47 RVA: 0x00005F28 File Offset: 0x00004128
	public static void Postfix(KeyCode key, ref bool __result)
	{
		bool flag = key == KeyCode.F && AutoFishLogic.SimulateFishFKeyHeld;
		if (flag)
		{
			__result = true;
		}
		else
		{
			bool flag2 = key == KeyCode.W && AutoFishLogic.SimulateWKeyHeld;
			if (flag2)
			{
				__result = true;
			}
			else
			{
				bool flag3 = key == KeyCode.A && AutoFishLogic.SimulateAKeyHeld;
				if (flag3)
				{
					__result = true;
				}
				else
				{
					bool flag4 = key == KeyCode.S && AutoFishLogic.SimulateSKeyHeld;
					if (flag4)
					{
						__result = true;
					}
					else
					{
						bool flag5 = key == KeyCode.D && AutoFishLogic.SimulateDKeyHeld;
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
