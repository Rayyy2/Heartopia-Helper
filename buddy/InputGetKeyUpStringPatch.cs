using System;
using HarmonyLib;
using UnityEngine;
using HeartopiaMod;

namespace HeartopiaMod
{
	[HarmonyPatch(typeof(Input), "GetKeyUp", new Type[]
	{
		typeof(string)
	})]
	public class InputGetKeyUpStringPatch
	{
		public static void Postfix(string name, ref bool __result)
		{
			bool flag = (name == "f" || name == "F") && HeartopiaComplete.SimulateFKeyUp;
			if (flag)
			{
				__result = true;
			}
		}
	}
}
