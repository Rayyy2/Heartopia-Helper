using System;
using HarmonyLib;
using UnityEngine;
using HeartopiaMod;

namespace HeartopiaMod
{
	[HarmonyPatch(typeof(Input), "GetKeyUp", new Type[]
	{
		typeof(KeyCode)
	})]
	public class InputGetKeyUpPatch
	{
		public static void Postfix(KeyCode key, ref bool __result)
		{
			bool flag = key == KeyCode.F && HeartopiaComplete.SimulateFKeyUp;
			if (flag)
			{
				__result = true;
			}
		}
	}
}
