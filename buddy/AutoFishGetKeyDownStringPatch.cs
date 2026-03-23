using System;
using HarmonyLib;
using UnityEngine;

// Token: 0x0200000A RID: 10
[HarmonyPatch(typeof(Input), "GetKeyDown", new Type[]
{
	typeof(string)
})]
public class AutoFishGetKeyDownStringPatch
{
	// Token: 0x06000037 RID: 55 RVA: 0x0000612C File Offset: 0x0000432C
	public static void Postfix(string name, ref bool __result)
	{
		bool flag = name == null;
		if (!flag)
		{
			string a = name.ToLower();
			bool flag2 = a == "f" && AutoFishLogic.SimulateFishFKeyDown;
			if (flag2)
			{
				__result = true;
			}
			else
			{
				bool flag3 = a == "w" && AutoFishLogic.SimulateWKeyDown;
				if (flag3)
				{
					__result = true;
				}
				else
				{
					bool flag4 = a == "a" && AutoFishLogic.SimulateAKeyDown;
					if (flag4)
					{
						__result = true;
					}
					else
					{
						bool flag5 = a == "s" && AutoFishLogic.SimulateSKeyDown;
						if (flag5)
						{
							__result = true;
						}
						else
						{
							bool flag6 = a == "d" && AutoFishLogic.SimulateDKeyDown;
							if (flag6)
							{
								__result = true;
							}
						}
					}
				}
			}
		}
	}
}
