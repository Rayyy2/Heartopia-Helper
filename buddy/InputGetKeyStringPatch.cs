using System;
using HarmonyLib;
using UnityEngine;
using HeartopiaMod;

namespace HeartopiaMod
{
	[HarmonyPatch(typeof(Input), "GetKey", new Type[]
	{
		typeof(string)
	})]
	public class InputGetKeyStringPatch
	{
		public static void Postfix(string name, ref bool __result)
		{
			bool flag = (name == "f" || name == "F") && HeartopiaComplete.SimulateFKeyHeld;
			if (flag)
			{
				__result = true;
			}
		}
	}
}
