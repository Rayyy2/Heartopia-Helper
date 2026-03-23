using System;
using HarmonyLib;
using UnityEngine;
using HeartopiaMod;

namespace HeartopiaMod
{
	[HarmonyPatch(typeof(Input), "GetKeyDown", new Type[]
	{
		typeof(string)
	})]
	public class InputGetKeyDownStringPatch
	{
		public static void Postfix(string name, ref bool __result)
		{
			bool flag = (name == "f" || name == "F") && HeartopiaComplete.SimulateFKeyDown;
			if (flag)
			{
				__result = true;
			}
		}
	}
}
