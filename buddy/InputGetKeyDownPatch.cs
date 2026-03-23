using System;
using HarmonyLib;
using UnityEngine;
using HeartopiaMod;

namespace HeartopiaMod
{
	[HarmonyPatch(typeof(Input), "GetKeyDown", new Type[]
	{
		typeof(KeyCode)
	})]
	public class InputGetKeyDownPatch
	{
		public static void Postfix(KeyCode key, ref bool __result)
		{
			bool flag = key == KeyCode.F && HeartopiaComplete.SimulateFKeyDown;
			if (flag)
			{
				__result = true;
			}
		}
	}
}
