using System;
using HarmonyLib;
using UnityEngine;

// Token: 0x0200000D RID: 13
[HarmonyPatch(typeof(Input), "GetAxisRaw", new Type[]
{
	typeof(string)
})]
public class AutoFishGetAxisRawPatch
{
	// Token: 0x0600003D RID: 61 RVA: 0x000062D0 File Offset: 0x000044D0
	public static void Postfix(string axisName, ref float __result)
	{
		bool flag = axisName == null;
		if (!flag)
		{
			string a = axisName.ToLower();
			bool flag2 = a == "horizontal";
			if (flag2)
			{
				bool simulateAKeyHeld = AutoFishLogic.SimulateAKeyHeld;
				if (simulateAKeyHeld)
				{
					__result = -1f;
				}
				else
				{
					bool simulateDKeyHeld = AutoFishLogic.SimulateDKeyHeld;
					if (simulateDKeyHeld)
					{
						__result = 1f;
					}
				}
			}
			else
			{
				bool flag3 = a == "vertical";
				if (flag3)
				{
					bool simulateSKeyHeld = AutoFishLogic.SimulateSKeyHeld;
					if (simulateSKeyHeld)
					{
						__result = -1f;
					}
					else
					{
						bool simulateWKeyHeld = AutoFishLogic.SimulateWKeyHeld;
						if (simulateWKeyHeld)
						{
							__result = 1f;
						}
					}
				}
			}
		}
	}
}
