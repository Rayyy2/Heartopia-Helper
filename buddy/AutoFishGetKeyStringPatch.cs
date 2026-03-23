using System;
using HarmonyLib;
using UnityEngine;

// Token: 0x02000009 RID: 9
[HarmonyPatch(typeof(Input), "GetKey", new Type[]
{
	typeof(string)
})]
public class AutoFishGetKeyStringPatch
{
	// Token: 0x06000035 RID: 53 RVA: 0x00006064 File Offset: 0x00004264
	public static void Postfix(string name, ref bool __result)
	{
		bool flag = name == null;
		if (!flag)
		{
			string a = name.ToLower();
			bool flag2 = a == "f" && AutoFishLogic.SimulateFishFKeyHeld;
			if (flag2)
			{
				__result = true;
			}
			else
			{
				bool flag3 = a == "w" && AutoFishLogic.SimulateWKeyHeld;
				if (flag3)
				{
					__result = true;
				}
				else
				{
					bool flag4 = a == "a" && AutoFishLogic.SimulateAKeyHeld;
					if (flag4)
					{
						__result = true;
					}
					else
					{
						bool flag5 = a == "s" && AutoFishLogic.SimulateSKeyHeld;
						if (flag5)
						{
							__result = true;
						}
						else
						{
							bool flag6 = a == "d" && AutoFishLogic.SimulateDKeyHeld;
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
