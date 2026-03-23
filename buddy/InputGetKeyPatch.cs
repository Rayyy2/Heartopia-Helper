using System;
using HarmonyLib;
using UnityEngine;
using HeartopiaMod;

namespace HeartopiaMod
{
	[HarmonyPatch(typeof(Input), "GetKey", new Type[]
	{
		typeof(KeyCode)
	})]
	public class InputGetKeyPatch
	{
		public static void Postfix(KeyCode key, ref bool __result)
		{
			bool flag = key == KeyCode.F && HeartopiaComplete.SimulateFKeyHeld;
			if (flag)
			{
				__result = true;
			}
		}
	}
}
