using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Token: 0x02000005 RID: 5
public class AutoFishLogic
{
	// Token: 0x06000010 RID: 16 RVA: 0x00003030 File Offset: 0x00001230
	private static void DbgLog(string hypothesisId, string location, string message, string data = "{}")
	{
		try
		{
			long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			string text = "log_" + num.ToString() + "_" + UnityEngine.Random.Range(0, 99999).ToString();
			string contents = string.Concat(new string[]
			{
				"{\"id\":\"",
				text,
				"\",\"timestamp\":",
				num.ToString(),
				",\"location\":\"",
				location,
				"\",\"message\":\"",
				message.Replace("\"", "'").Replace("\\", "\\\\"),
				"\",\"data\":",
				data,
				",\"hypothesisId\":\"",
				hypothesisId,
				"\"}\n"
			});
			File.AppendAllText(AutoFishLogic._dbgLogPath, contents);
		}
		catch
		{
		}
	}

	// Token: 0x06000011 RID: 17 RVA: 0x00003124 File Offset: 0x00001324
	public AutoFishLogic(Func<GameObject> findPlayerRoot)
	{
		this.findPlayerRoot = findPlayerRoot;
	}

	// Token: 0x06000012 RID: 18 RVA: 0x000032B0 File Offset: 0x000014B0
	private bool IsFishingPanelActive()
	{
		GameObject gameObject = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/FishingPanel(Clone)");
		bool flag = gameObject != null && gameObject.activeInHierarchy;
		bool result;
		if (flag)
		{
			this.fishingPanelVisible = true;
			result = true;
		}
		else
		{
			try
			{
				GameObject gameObject2 = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status");
				bool flag2 = gameObject2 != null;
				if (flag2)
				{
					for (int i = 0; i < gameObject2.transform.childCount; i++)
					{
						Transform child = gameObject2.transform.GetChild(i);
						bool flag3 = child != null && child.gameObject.activeInHierarchy;
						if (flag3)
						{
							string text = child.name.ToLower();
							bool flag4 = text.Contains("fishing") && text.Contains("panel");
							if (flag4)
							{
								this.fishingPanelVisible = true;
								return true;
							}
						}
					}
				}
			}
			catch
			{
			}
			this.fishingPanelVisible = false;
			result = false;
		}
		return result;
	}

	// Token: 0x06000013 RID: 19 RVA: 0x000033C4 File Offset: 0x000015C4
	private bool IsStrugglingVfxActiveRaw()
	{
		foreach (string text in this.STRUGGLING_VFX_NAMES)
		{
			GameObject gameObject = GameObject.Find(text);
			bool flag = gameObject != null && gameObject.activeInHierarchy;
			if (flag)
			{
				Vector3 position = gameObject.transform.position;
				bool flag2 = position.x != 0f || position.y != 0f || position.z != 0f;
				if (flag2)
				{
					return true;
				}
			}
		}
		try
		{
			GameObject[] array = UnityEngine.Object.FindObjectsOfType<GameObject>();
			foreach (GameObject gameObject2 in array)
			{
				bool flag3 = gameObject2 == null || !gameObject2.activeInHierarchy;
				if (!flag3)
				{
					string text2 = gameObject2.name.ToLower();
					bool flag4 = text2.StartsWith("p_vfx_fishing_") && (text2.Contains("struggling") || text2.Contains("bite"));
					if (flag4)
					{
						Vector3 position2 = gameObject2.transform.position;
						bool flag5 = position2.x != 0f || position2.y != 0f || position2.z != 0f;
						if (flag5)
						{
							return true;
						}
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	// Token: 0x06000014 RID: 20 RVA: 0x00003560 File Offset: 0x00001760
	private bool IsStrugglingVfxActive()
	{
		bool flag = false;
		foreach (string text in this.STRUGGLING_VFX_NAMES)
		{
			GameObject gameObject = GameObject.Find(text);
			bool flag2 = gameObject != null && gameObject.activeInHierarchy;
			if (flag2)
			{
				Vector3 position = gameObject.transform.position;
				bool flag3 = position.x != 0f || position.y != 0f || position.z != 0f;
				if (flag3)
				{
					flag = true;
					break;
				}
			}
		}
		bool flag4 = !flag;
		if (flag4)
		{
			try
			{
				GameObject[] array = UnityEngine.Object.FindObjectsOfType<GameObject>();
				foreach (GameObject gameObject2 in array)
				{
					bool flag5 = gameObject2 == null || !gameObject2.activeInHierarchy;
					if (!flag5)
					{
						string text2 = gameObject2.name.ToLower();
						bool flag6 = text2.StartsWith("p_vfx_fishing_") && (text2.Contains("struggling") || text2.Contains("bite"));
						if (flag6)
						{
							Vector3 position2 = gameObject2.transform.position;
							bool flag7 = position2.x != 0f || position2.y != 0f || position2.z != 0f;
							if (flag7)
							{
								flag = true;
								break;
							}
						}
					}
				}
			}
			catch
			{
			}
		}
		this.strugglingVfxVisible = flag;
		return flag;
	}

	// Token: 0x06000015 RID: 21 RVA: 0x00003714 File Offset: 0x00001914
	private bool IsFishBitingEdgeDetect()
	{
		bool flag = this.IsFishingPanelActive();
		bool flag2 = !this.fishingPanelWasActive && flag;
		bool result;
		if (flag2)
		{
			AutoFishLogic.DbgLog("H1", "IsFishBitingEdgeDetect:PANEL", "Bite via FishingPanel in edge detect!", "{\"panelWasActive\":false,\"panelNow\":true}");
			MelonLogger.Msg("[AutoFish] Bite detected via FishingPanel appearance!");
			result = true;
		}
		else
		{
			bool flag3 = this.IsStrugglingVfxActive();
			bool flag4 = !flag3;
			if (flag4)
			{
				bool flag5 = this.vfxActiveAtCastStart && !this.vfxBecameInactive;
				if (flag5)
				{
					this.vfxBecameInactive = true;
				}
				result = false;
			}
			else
			{
				bool flag6 = this.vfxActiveAtCastStart && !this.vfxBecameInactive;
				if (flag6)
				{
					result = false;
				}
				else
				{
					AutoFishLogic.DbgLog("H2", "IsFishBitingEdgeDetect:VFX", "Genuine VFX bite!", string.Concat(new string[]
					{
						"{\"vfxActiveAtCastStart\":",
						this.vfxActiveAtCastStart ? "true" : "false",
						",\"vfxBecameInactive\":",
						this.vfxBecameInactive ? "true" : "false",
						"}"
					}));
					result = true;
				}
			}
		}
		return result;
	}

	// Token: 0x06000016 RID: 22 RVA: 0x00003828 File Offset: 0x00001A28
	public void Update()
	{
		bool flag = !this.autoFishEnabled;
		if (!flag)
		{
			switch (this.currentState)
			{
			case AutoFishLogic.FishingState.Idle:
				this.TransitionTo(AutoFishLogic.FishingState.Scanning);
				break;
			case AutoFishLogic.FishingState.Scanning:
				this.UpdateScanning();
				break;
			case AutoFishLogic.FishingState.Casting:
				this.UpdateCasting();
				break;
			case AutoFishLogic.FishingState.WaitingBite:
				this.UpdateWaitingBite();
				break;
			case AutoFishLogic.FishingState.Reeling:
				this.UpdateReeling();
				break;
			case AutoFishLogic.FishingState.Cooldown:
				this.UpdateCooldown();
				break;
			}
		}
	}

	// Token: 0x06000017 RID: 23 RVA: 0x000038A8 File Offset: 0x00001AA8
	private void UpdateScanning()
	{
		bool flag = Time.unscaledTime - this.lastScanTime >= 1.5f;
		if (flag)
		{
			this.lastScanTime = Time.unscaledTime;
			this.ScanForFishShadows();
			bool flag2 = this.fishShadowCount > 0;
			if (flag2)
			{
				this.fishAttemptCount++;
				this.TransitionTo(AutoFishLogic.FishingState.Casting);
			}
		}
	}

	// Token: 0x06000018 RID: 24 RVA: 0x0000390C File Offset: 0x00001B0C
	private void UpdateCasting()
	{
		float num = Time.unscaledTime - this.stateStartTime;
		bool flag = this.autoAimEnabled && this.targetFishShadow == null;
		if (flag)
		{
			this.ClearAllFlags();
			this.TransitionTo(AutoFishLogic.FishingState.Scanning);
		}
		else
		{
			float num2 = 0.5f;
			bool flag2 = this.autoAimEnabled && this.targetFishShadow != null && num < num2;
			if (flag2)
			{
				this.AimPlayerAtFish();
			}
			float num3 = this.autoAimEnabled ? num2 : 0f;
			bool flag3 = !this.castClickDone && num >= num3;
			if (flag3)
			{
				this.castClickDone = true;
				AutoFishLogic.SimulateFishFKeyDown = true;
				AutoFishLogic.SimulateFishFKeyHeld = true;
				List<GameObject> list = this.FindCastButtons();
				foreach (GameObject target in list)
				{
					try
					{
						this.ClickButtonOnce(target);
					}
					catch
					{
					}
				}
			}
			bool flag4 = this.castClickDone && num >= num3 + 0.15f && AutoFishLogic.SimulateFishFKeyHeld;
			if (flag4)
			{
				AutoFishLogic.SimulateFishFKeyDown = false;
				AutoFishLogic.SimulateFishFKeyHeld = false;
			}
			bool flag5 = this.castClickDone && num >= num3 + 1.5f;
			if (flag5)
			{
				bool flag6 = this.IsFishingPanelActive();
				AutoFishLogic.DbgLog("H29", "UpdateCasting:cast_verify", "Cast verification (edge detect)", string.Concat(new string[]
				{
					"{\"panelActive\":",
					flag6 ? "true" : "false",
					",\"panelWasActive\":",
					this.fishingPanelWasActive ? "true" : "false",
					",\"t\":",
					num.ToString(),
					"}"
				}));
				bool flag7 = flag6 && !this.fishingPanelWasActive;
				if (flag7)
				{
					MelonLogger.Msg("[AutoFish] Cast SUCCESS - FishingPanel APPEARED (edge detect), waiting for bite...");
					this.ClearAllFlags();
					this.TransitionTo(AutoFishLogic.FishingState.WaitingBite);
					return;
				}
			}
			float num4 = this.fishingPanelWasActive ? 2f : 5f;
			bool flag8 = this.castClickDone && num >= num3 + num4;
			if (flag8)
			{
				bool flag9 = this.IsFishingPanelActive();
				float value = -1f;
				bool flag10 = this.targetFishShadow != null;
				if (flag10)
				{
					GameObject gameObject = this.findPlayerRoot();
					bool flag11 = gameObject != null;
					if (flag11)
					{
						value = Vector3.Distance(gameObject.transform.position, this.targetFishShadow.transform.position);
					}
				}
				AutoFishLogic.DbgLog("H39", "UpdateCasting:cast_timeout", "Cast timeout reached", string.Concat(new string[]
				{
					"{\"panelActive\":",
					flag9 ? "true" : "false",
					",\"panelWasActive\":",
					this.fishingPanelWasActive ? "true" : "false",
					",\"t\":",
					num.ToString(),
					",\"targetDist\":",
					value.ToString("F1"),
					",\"castTimeout\":",
					num4.ToString("F1"),
					"}"
				}));
				bool flag12 = !flag9;
				if (flag12)
				{
					MelonLogger.Msg($"[AutoFish] Cast FAILED - no FishingPanel (fish dist: {value:F1}m), going to cooldown.");
					this.ClearAllFlags();
					this.TransitionTo(AutoFishLogic.FishingState.Cooldown);
				}
				else
				{
					MelonLogger.Msg("[AutoFish] Cast → WaitingBite (panel active)");
					AutoFishLogic.DbgLog("H39", "UpdateCasting:proceed", "Proceeding to WaitingBite", string.Concat(new string[]
					{
						"{\"panelWasActive\":",
						this.fishingPanelWasActive ? "true" : "false",
						",\"t\":",
						num.ToString(),
						"}"
					}));
					this.ClearAllFlags();
					this.TransitionTo(AutoFishLogic.FishingState.WaitingBite);
				}
			}
		}
	}

	// Token: 0x06000019 RID: 25 RVA: 0x00003D40 File Offset: 0x00001F40
	private void AimPlayerAtFish()
	{
		bool flag = this.targetFishShadow == null || !this.targetFishShadow.activeInHierarchy;
		if (!flag)
		{
			try
			{
				GameObject gameObject = this.findPlayerRoot();
				bool flag2 = gameObject == null;
				if (!flag2)
				{
					Vector3 position = gameObject.transform.position;
					Vector3 position2 = this.targetFishShadow.transform.position;
					Vector3 vector = position2 - position;
					vector.y = 0f;
					bool flag3 = vector.sqrMagnitude > 0.1f;
					if (flag3)
					{
						gameObject.transform.rotation = Quaternion.LookRotation(vector);
					}
				}
			}
			catch
			{
			}
		}
	}

	// Token: 0x0600001A RID: 26 RVA: 0x00003E08 File Offset: 0x00002008
	private void UpdateWaitingBite()
	{
		float num = Time.unscaledTime - this.stateStartTime;
		bool flag = this.IsFishingPanelActive();
		bool flag2 = (num < 1f && (int)(num * 10f) % 10 == 0) || (flag && !this.fishingPanelWasActive);
		if (flag2)
		{
			AutoFishLogic.DbgLog("H1,H5", "UpdateWaitingBite:panel", "Panel check in WaitBite", string.Concat(new string[]
			{
				"{\"panelNow\":",
				flag ? "true" : "false",
				",\"panelWasActive\":",
				this.fishingPanelWasActive ? "true" : "false",
				",\"t\":",
				num.ToString("F2"),
				"}"
			}));
		}
		bool flag3 = flag && !this.fishingPanelWasActive;
		if (flag3)
		{
			MelonLogger.Msg($"[AutoFish] FishingPanel appeared at {num:F2}s - INSTANT REEL!");
			AutoFishLogic.DbgLog("H1", "UpdateWaitingBite:BITE_PANEL", "BITE via FishingPanel - instant reel!", "{\"t\":" + num.ToString("F2") + "}");
			this.biteConfirmCount = 0;
			this.TransitionTo(AutoFishLogic.FishingState.Reeling);
		}
		else
		{
			bool flag4 = num >= 1f;
			if (flag4)
			{
				bool flag5 = num >= 8f && this.vfxActiveAtCastStart && !this.vfxBecameInactive;
				if (flag5)
				{
					AutoFishLogic.DbgLog("H39", "UpdateWaitingBite:vfx_stale_reset", "VFX stale 8s - force reset edge detection", "{\"t\":" + num.ToString("F2") + ",\"vfxActiveAtCastStart\":true,\"vfxBecameInactive\":false}");
					MelonLogger.Msg("[AutoFish] VFX stale for 8s - resetting edge detection");
					this.vfxActiveAtCastStart = false;
					this.vfxBecameInactive = true;
				}
				bool flag6 = this.IsFishBitingEdgeDetect();
				bool flag7 = flag6;
				if (flag7)
				{
					AutoFishLogic.DbgLog("H2", "UpdateWaitingBite:vfx", "VFX bite detected", string.Concat(new string[]
					{
						"{\"biteDetected\":true,\"biteConfirmCount\":",
						(this.biteConfirmCount + 1).ToString(),
						",\"threshold\":",
						2.ToString(),
						",\"t\":",
						num.ToString("F2"),
						",\"vfxAtStart\":",
						this.vfxActiveAtCastStart ? "true" : "false",
						",\"vfxBecameInactive\":",
						this.vfxBecameInactive ? "true" : "false",
						"}"
					}));
				}
				bool flag8 = flag6;
				if (flag8)
				{
					this.biteConfirmCount++;
					bool flag9 = this.biteConfirmCount >= 2;
					if (flag9)
					{
						MelonLogger.Msg($"[AutoFish] Bite VFX confirmed ({this.biteConfirmCount}x) at {num:F2}s - REEL!");
						AutoFishLogic.DbgLog("H2", "UpdateWaitingBite:BITE_VFX", "BITE via VFX confirmed - reel!", string.Concat(new string[]
						{
							"{\"biteConfirmCount\":",
							this.biteConfirmCount.ToString(),
							",\"t\":",
							num.ToString("F2"),
							"}"
						}));
						this.biteConfirmCount = 0;
						this.TransitionTo(AutoFishLogic.FishingState.Reeling);
						return;
					}
				}
				else
				{
					bool flag10 = this.biteConfirmCount > 0;
					if (flag10)
					{
						this.biteConfirmCount = 0;
					}
				}
			}
			bool flag11 = num >= this.waitBiteTimeout;
			if (flag11)
			{
				bool flag12 = this.IsFishingPanelActive();
				if (flag12)
				{
					MelonLogger.Msg("[AutoFish] Bite timeout but FishingPanel is active - attempting reel!");
					this.biteConfirmCount = 0;
					this.TransitionTo(AutoFishLogic.FishingState.Reeling);
				}
				else
				{
					MelonLogger.Msg("[AutoFish] Bite timeout, no panel detected. Going to cooldown.");
					this.biteConfirmCount = 0;
					this.TransitionTo(AutoFishLogic.FishingState.Cooldown);
				}
			}
		}
	}

	// Token: 0x0600001B RID: 27 RVA: 0x00004214 File Offset: 0x00002414
	private void UpdateReeling()
	{
		float num = Time.unscaledTime - this.stateStartTime;
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		bool flag = num >= this.reelMaxDuration;
		if (flag)
		{
			this.DoRelease();
			this.ClearAllFlags();
			this.fishCaughtCount++;
			AutoFishLogic.DbgLog("H10", "UpdateReeling:timeout", "Reel TIMEOUT (max duration)", string.Concat(new string[]
			{
				"{\"duration\":",
				num.ToString("F2"),
				",\"maxDuration\":",
				this.reelMaxDuration.ToString("F1"),
				",\"panelStillActive\":",
				this.IsFishingPanelActive() ? "true" : "false",
				"}"
			}));
			MelonLogger.Msg($"[AutoFish] Reel max duration reached. Fish caught: {this.fishCaughtCount}");
			this.TransitionTo(AutoFishLogic.FishingState.Cooldown);
		}
		else
		{
			bool flag2 = this.targetFishShadow == null || !this.targetFishShadow.activeInHierarchy;
			bool flag3 = this.IsFishingPanelActive();
			bool flag4 = (flag2 || !flag3) && num > 2f;
			if (flag4)
			{
				AutoFishLogic.DbgLog("H18", "UpdateReeling:catch_signal", "Fish caught signal detected!", string.Concat(new string[]
				{
					"{\"fishShadowGone\":",
					flag2 ? "true" : "false",
					",\"panelActive\":",
					flag3 ? "true" : "false",
					",\"totalTime\":",
					num.ToString("F2"),
					",\"holdSent\":",
					this.holdSent ? "true" : "false",
					"}"
				}));
			}
			bool flag5 = (flag2 || !flag3) && num > 3f;
			if (flag5)
			{
				this.DoRelease();
				this.ClearAllFlags();
				this.fishCaughtCount++;
				this.targetFishShadow = null;
				this.nearbyFishShadows.Clear();
				this.biteConfirmCount = 0;
				this.holdSent = false;
				this.castClickDone = false;
				this.fishingPanelWasActive = false;
				this.vfxActiveAtCastStart = false;
				this.vfxBecameInactive = false;
				AutoFishLogic.DbgLog("H28", "UpdateReeling:fish_caught", "Fish caught! Go to COOLDOWN first", string.Concat(new string[]
				{
					"{\"duration\":",
					num.ToString("F2"),
					",\"caughtCount\":",
					this.fishCaughtCount.ToString(),
					",\"fishShadowGone\":",
					flag2 ? "true" : "false",
					",\"panelActive\":",
					flag3 ? "true" : "false",
					"}"
				}));
				MelonLogger.Msg($"[AutoFish] ==== FISH CAUGHT! Cooldown then re-scan ==== Total: {this.fishCaughtCount}");
				this.TransitionTo(AutoFishLogic.FishingState.Cooldown);
			}
			else
			{
				bool flag6 = flag2;
				if (flag6)
				{
					this.DoRelease();
					this.ClearAllFlags();
				}
				else
				{
					this.cycleTimer += unscaledDeltaTime;
					bool flag7 = this.isHolding && this.cycleTimer >= this.reelHoldDuration;
					if (flag7)
					{
						bool flag8 = this.targetFishShadow == null || !this.targetFishShadow.activeInHierarchy;
						bool flag9 = false;
						bool flag10 = false;
						bool flag11 = this.IsStrugglingVfxActive();
						int val = 0;
						string text = "";
						try
						{
							GameObject gameObject = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/FishingPanel(Clone)/skill_main_hold@go@w/Joy@ani");
							flag9 = (gameObject != null && gameObject.activeInHierarchy);
						}
						catch
						{
						}
						try
						{
							GameObject gameObject2 = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)");
							flag10 = (gameObject2 != null && gameObject2.activeInHierarchy);
						}
						catch
						{
						}
						try
						{
							GameObject gameObject3 = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/FishingPanel(Clone)");
							bool flag12 = gameObject3 != null;
							if (flag12)
							{
								val = gameObject3.transform.childCount;
								for (int i = 0; i < Math.Min(val, 5); i++)
								{
									text = text + gameObject3.transform.GetChild(i).name + (gameObject3.transform.GetChild(i).gameObject.activeInHierarchy ? "(ON)" : "(OFF)") + ";";
								}
							}
						}
						catch
						{
						}
						AutoFishLogic.DbgLog("H18-22", "UpdateReeling:deep_scan", "HOLD->PAUSE deep scan", string.Concat(new string[]
						{
							"{\"totalTime\":",
							num.ToString("F2"),
							",\"panelActive\":",
							flag3 ? "true" : "false",
							",\"fishShadowGone\":",
							flag8 ? "true" : "false",
							",\"skillMainHoldActive\":",
							flag9 ? "true" : "false",
							",\"statusPanelActive\":",
							flag10 ? "true" : "false",
							",\"vfxActive\":",
							flag11 ? "true" : "false",
							",\"fpChildCount\":",
							val.ToString(),
							",\"fpChildren\":\"",
							text,
							"\"}"
						}));
						this.isHolding = false;
						this.cycleTimer = 0f;
						this.DoRelease();
						this.ClearAllFlags();
						this.holdSent = false;
					}
					else
					{
						bool flag13 = !this.isHolding && this.cycleTimer >= this.reelPauseDuration;
						if (flag13)
						{
							AutoFishLogic.DbgLog("H7", "UpdateReeling:cycle", "PAUSE -> HOLD", string.Concat(new string[]
							{
								"{\"totalTime\":",
								num.ToString("F2"),
								",\"panelActive\":",
								flag3 ? "true" : "false",
								"}"
							}));
							this.isHolding = true;
							this.cycleTimer = 0f;
							this.holdSent = false;
						}
						bool flag14 = this.isHolding;
						if (flag14)
						{
							AutoFishLogic.SimulateFishFKeyHeld = true;
							AutoFishLogic.SimulateMouseButton0 = true;
							bool flag15 = !this.holdSent;
							if (flag15)
							{
								AutoFishLogic.SimulateFishFKeyDown = true;
								AutoFishLogic.SimulateMouseButton0Down = true;
								List<GameObject> list = this.FindReelButtons();
								AutoFishLogic.DbgLog("H17", "UpdateReeling:hold_start", "COMBO: F+Mouse+UI", string.Concat(new string[]
								{
									"{\"reelBtnsCount\":",
									list.Count.ToString(),
									",\"totalTime\":",
									num.ToString("F2"),
									"}"
								}));
								bool flag16 = list.Count > 0;
								if (flag16)
								{
									this.DoHoldDown(list);
								}
								this.holdSent = true;
							}
							else
							{
								AutoFishLogic.SimulateFishFKeyDown = false;
								AutoFishLogic.SimulateMouseButton0Down = false;
							}
						}
					}
				}
			}
		}
	}

	// Token: 0x0600001C RID: 28 RVA: 0x00004950 File Offset: 0x00002B50
	private void UpdateCooldown()
	{
		bool flag = Time.unscaledTime - this.stateStartTime >= this.cooldownDuration;
		if (flag)
		{
			this.DoRelease();
			this.ClearAllFlags();
			this.targetFishShadow = null;
			this.nearbyFishShadows.Clear();
			this.biteConfirmCount = 0;
			this.holdSent = false;
			this.castClickDone = false;
			this.fishingPanelWasActive = false;
			this.vfxActiveAtCastStart = false;
			this.vfxBecameInactive = false;
			bool flag2 = this.IsFishingPanelActive();
			AutoFishLogic.DbgLog("H13", "UpdateCooldown:reset", "FULL RESET before new round", string.Concat(new string[]
			{
				"{\"time\":",
				Time.unscaledTime.ToString(),
				",\"panelActive\":",
				flag2 ? "true" : "false",
				"}"
			}));
			MelonLogger.Msg("[AutoFish] ==== NEW ROUND: All state reset ====");
			this.TransitionTo(AutoFishLogic.FishingState.Scanning);
		}
	}

	// Token: 0x0600001D RID: 29 RVA: 0x00004A40 File Offset: 0x00002C40
	private void TransitionTo(AutoFishLogic.FishingState newState)
	{
		AutoFishLogic.FishingState value = this.currentState;
		this.currentState = newState;
		this.stateStartTime = Time.unscaledTime;
		string text = (this.targetFishShadow != null) ? this.targetFishShadow.name.Replace("\"", "'") : "null";
		AutoFishLogic.DbgLog("H3", "TransitionTo", value.ToString() + " -> " + newState.ToString(), string.Concat(new string[]
		{
			"{\"from\":\"",
			value.ToString(),
			"\",\"to\":\"",
			newState.ToString(),
			"\",\"targetFish\":\"",
			text,
			"\",\"time\":",
			Time.unscaledTime.ToString(),
			"}"
		}));
		bool flag = newState == AutoFishLogic.FishingState.Reeling;
		if (flag)
		{
			this.holdSent = false;
			this.cycleTimer = 0f;
			this.isHolding = true;
		}
		else
		{
			bool flag2 = newState == AutoFishLogic.FishingState.Scanning;
			if (flag2)
			{
				this.targetFishShadow = null;
				this.nearbyFishShadows.Clear();
				this.lastScanTime = 0f;
			}
			else
			{
				bool flag3 = newState == AutoFishLogic.FishingState.Casting;
				if (flag3)
				{
					this.castClickDone = false;
					this.vfxActiveAtCastStart = this.IsStrugglingVfxActiveRaw();
					this.vfxBecameInactive = !this.vfxActiveAtCastStart;
					this.fishingPanelWasActive = this.IsFishingPanelActive();
				}
				else
				{
					bool flag4 = newState == AutoFishLogic.FishingState.WaitingBite;
					if (flag4)
					{
						this.fishingPanelWasActive = this.IsFishingPanelActive();
						this.vfxActiveAtCastStart = this.IsStrugglingVfxActiveRaw();
						this.vfxBecameInactive = !this.vfxActiveAtCastStart;
						this.biteConfirmCount = 0;
						AutoFishLogic.DbgLog("H39", "TransitionTo:WaitingBite", "WaitingBite VFX state at start", string.Concat(new string[]
						{
							"{\"panelWasActive\":",
							this.fishingPanelWasActive ? "true" : "false",
							",\"vfxActiveAtCastStart\":",
							this.vfxActiveAtCastStart ? "true" : "false",
							",\"vfxBecameInactive\":",
							this.vfxBecameInactive ? "true" : "false",
							"}"
						}));
						MelonLogger.Msg($"[AutoFish] WaitingBite started: panelWasActive={this.fishingPanelWasActive}, vfxAtStart={this.vfxActiveAtCastStart}");
					}
				}
			}
		}
		bool flag5 = newState == AutoFishLogic.FishingState.Cooldown || newState == AutoFishLogic.FishingState.Idle;
		if (flag5)
		{
			this.DoRelease();
			this.ClearAllFlags();
		}
		MelonLogger.Msg($"[AutoFish] {value} -> {newState}");
	}

	// Token: 0x0600001E RID: 30 RVA: 0x00004D3C File Offset: 0x00002F3C
	private List<GameObject> FindCastButtons()
	{
		List<GameObject> list = new List<GameObject>();
		string[] array = new string[]
		{
			"GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/middle_right_layout@go/skill_bar@w@go/skill_bar@go/main_joy@go@w/Joy@ani",
			"GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/middle_right_layout@go/skill_bar@w@go/skill_bar@go/main_joy@go@w/Joy@ani/stick@frame/normal",
			"GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/middle_right_layout@go/skill_bar@w@go/skill_bar@go/main_joy@go@w",
			"GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_fish@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn",
			"GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn"
		};
		foreach (string text in array)
		{
			GameObject gameObject = GameObject.Find(text);
			bool flag = gameObject != null && gameObject.activeInHierarchy;
			if (flag)
			{
				list.Add(gameObject);
			}
		}
		return list;
	}

	// Token: 0x0600001F RID: 31 RVA: 0x00004DCC File Offset: 0x00002FCC
	private List<GameObject> FindReelButtons()
	{
		List<GameObject> list = new List<GameObject>();
		string[] array = new string[]
		{
			"GameApp/startup_root(Clone)/XDUIRoot/Status/FishingPanel(Clone)/skill_main_hold@go@w/Joy@ani",
			"GameApp/startup_root(Clone)/XDUIRoot/Status/FishingPanel(Clone)/skill_main_hold@go@w/Joy@ani/stick@frame/normal",
			"GameApp/startup_root(Clone)/XDUIRoot/Status/FishingPanel(Clone)/skill_main_hold@go@w/Joy@ani/joyAniRoot@ani/Icon",
			"GameApp/startup_root(Clone)/XDUIRoot/Status/FishingPanel(Clone)/skill_main_hold@go@w"
		};
		foreach (string text in array)
		{
			GameObject gameObject = GameObject.Find(text);
			bool flag = gameObject != null && gameObject.activeInHierarchy;
			if (flag)
			{
				list.Add(gameObject);
			}
		}
		try
		{
			GameObject gameObject2 = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/FishingPanel(Clone)");
			bool flag2 = gameObject2 != null;
			if (flag2)
			{
				this.ScanPanelForButtons(gameObject2, list);
			}
		}
		catch
		{
		}
		bool flag3 = list.Count == 0;
		if (flag3)
		{
			try
			{
				GameObject gameObject3 = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status");
				bool flag4 = gameObject3 != null;
				if (flag4)
				{
					for (int j = 0; j < gameObject3.transform.childCount; j++)
					{
						Transform child = gameObject3.transform.GetChild(j);
						bool flag5 = child != null && child.gameObject.activeInHierarchy;
						if (flag5)
						{
							string text2 = child.name.ToLower();
							bool flag6 = text2.Contains("fishing") || text2.Contains("reel") || text2.Contains("pull") || text2.Contains("skill_main");
							if (flag6)
							{
								this.ScanPanelForButtons(child.gameObject, list);
							}
						}
					}
				}
			}
			catch
			{
			}
		}
		bool flag7 = list.Count == 0;
		if (flag7)
		{
			try
			{
				GameObject gameObject4 = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot");
				bool flag8 = gameObject4 != null;
				if (flag8)
				{
					Transform[] array3 = gameObject4.GetComponentsInChildren<Transform>(false);
					foreach (Transform transform in array3)
					{
						bool flag9 = transform == null || !transform.gameObject.activeInHierarchy;
						if (!flag9)
						{
							string text3 = transform.name.ToLower();
							bool flag10 = text3.Contains("skill_main_hold") || (text3.Contains("fishing") && (text3.Contains("hold") || text3.Contains("reel") || text3.Contains("joy")));
							if (flag10)
							{
								bool flag11 = !list.Contains(transform.gameObject);
								if (flag11)
								{
									list.Add(transform.gameObject);
								}
								this.ScanPanelForButtons(transform.gameObject, list);
							}
						}
					}
				}
			}
			catch
			{
			}
		}
		return list;
	}

	// Token: 0x06000020 RID: 32 RVA: 0x000050F4 File Offset: 0x000032F4
	private void ScanPanelForButtons(GameObject panel, List<GameObject> btns)
	{
		try
		{
			Selectable[] array = panel.GetComponentsInChildren<Selectable>(false);
			foreach (Selectable selectable in array)
			{
				bool flag = selectable != null && selectable.gameObject.activeInHierarchy && !btns.Contains(selectable.gameObject);
				if (flag)
				{
					btns.Add(selectable.gameObject);
				}
			}
			EventTrigger[] array3 = panel.GetComponentsInChildren<EventTrigger>(false);
			foreach (EventTrigger eventTrigger in array3)
			{
				bool flag2 = eventTrigger != null && eventTrigger.gameObject.activeInHierarchy && !btns.Contains(eventTrigger.gameObject);
				if (flag2)
				{
					btns.Add(eventTrigger.gameObject);
				}
			}
			Transform[] array5 = panel.GetComponentsInChildren<Transform>(false);
			foreach (Transform transform in array5)
			{
				bool flag3 = transform == null || !transform.gameObject.activeInHierarchy;
				if (!flag3)
				{
					string text = transform.name.ToLower();
					bool flag4 = (text.Contains("joy") || text.Contains("hold") || text.Contains("skill") || text.Contains("normal") || text.Contains("icon")) && !btns.Contains(transform.gameObject);
					if (flag4)
					{
						btns.Add(transform.gameObject);
					}
				}
			}
		}
		catch
		{
		}
	}

	// Token: 0x06000021 RID: 33 RVA: 0x000052D0 File Offset: 0x000034D0
	private void ClickButtonOnce(GameObject target)
	{
		PointerEventData pointerEventData = this.MakePointerData(target);
		ExecuteEvents.Execute<IPointerEnterHandler>(target, pointerEventData, ExecuteEvents.pointerEnterHandler);
		ExecuteEvents.Execute<IPointerDownHandler>(target, pointerEventData, ExecuteEvents.pointerDownHandler);
		ExecuteEvents.Execute<IPointerUpHandler>(target, pointerEventData, ExecuteEvents.pointerUpHandler);
		ExecuteEvents.Execute<IPointerClickHandler>(target, pointerEventData, ExecuteEvents.pointerClickHandler);
		ExecuteEvents.ExecuteHierarchy<IPointerDownHandler>(target, pointerEventData, ExecuteEvents.pointerDownHandler);
		ExecuteEvents.ExecuteHierarchy<IPointerUpHandler>(target, pointerEventData, ExecuteEvents.pointerUpHandler);
		ExecuteEvents.ExecuteHierarchy<IPointerClickHandler>(target, pointerEventData, ExecuteEvents.pointerClickHandler);
		Button component = target.GetComponent<Button>();
		bool flag = component != null && component.interactable;
		if (flag)
		{
			component.onClick.Invoke();
		}
	}

	// Token: 0x06000022 RID: 34 RVA: 0x0000536C File Offset: 0x0000356C
	private void DoHoldDown(List<GameObject> targets)
	{
		this.DoRelease();
		foreach (GameObject gameObject in targets)
		{
			try
			{
				PointerEventData pointerEventData = this.MakePointerData(gameObject);
				ExecuteEvents.Execute<IPointerEnterHandler>(gameObject, pointerEventData, ExecuteEvents.pointerEnterHandler);
				bool flag = ExecuteEvents.Execute<IPointerDownHandler>(gameObject, pointerEventData, ExecuteEvents.pointerDownHandler);
				bool flag2 = flag;
				if (flag2)
				{
					this.heldButtons.Add(gameObject);
					this.heldPointerDatas.Add(pointerEventData);
				}
				GameObject gameObject2 = ExecuteEvents.ExecuteHierarchy<IPointerDownHandler>(gameObject, pointerEventData, ExecuteEvents.pointerDownHandler);
				bool flag3 = gameObject2 != null && gameObject2 != gameObject;
				if (flag3)
				{
					this.heldButtons.Add(gameObject2);
					this.heldPointerDatas.Add(pointerEventData);
				}
				ExecuteEvents.Execute<IBeginDragHandler>(gameObject, pointerEventData, ExecuteEvents.beginDragHandler);
			}
			catch
			{
			}
		}
	}

	// Token: 0x06000023 RID: 35 RVA: 0x00005474 File Offset: 0x00003674
	private void DoRelease()
	{
		for (int i = 0; i < this.heldButtons.Count; i++)
		{
			try
			{
				bool flag = this.heldButtons[i] != null;
				if (flag)
				{
					PointerEventData pointerEventData = (i < this.heldPointerDatas.Count) ? this.heldPointerDatas[i] : this.MakePointerData(this.heldButtons[i]);
					ExecuteEvents.Execute<IPointerUpHandler>(this.heldButtons[i], pointerEventData, ExecuteEvents.pointerUpHandler);
					ExecuteEvents.Execute<IEndDragHandler>(this.heldButtons[i], pointerEventData, ExecuteEvents.endDragHandler);
					ExecuteEvents.Execute<IPointerExitHandler>(this.heldButtons[i], pointerEventData, ExecuteEvents.pointerExitHandler);
				}
			}
			catch
			{
			}
		}
		this.heldButtons.Clear();
		this.heldPointerDatas.Clear();
	}

	// Token: 0x06000024 RID: 36 RVA: 0x00005564 File Offset: 0x00003764
	private PointerEventData MakePointerData(GameObject target)
	{
		Vector2 zero = Vector2.zero;
		RectTransform component = target.GetComponent<RectTransform>();
		bool flag = component != null;
		if (flag)
		{
			zero = new Vector2(component.position.x, component.position.y);
		}
		PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
		pointerEventData.button = 0;
		pointerEventData.position = zero;
		pointerEventData.pressPosition = zero;
		pointerEventData.pointerPress = target;
		pointerEventData.rawPointerPress = target;
		pointerEventData.pointerEnter = target;
		pointerEventData.clickCount = 1;
		pointerEventData.eligibleForClick = true;
		GraphicRaycaster componentInParent = target.GetComponentInParent<GraphicRaycaster>();
		pointerEventData.pointerCurrentRaycast = new RaycastResult
		{
			gameObject = target,
			module = componentInParent,
			screenPosition = zero
		};
		pointerEventData.pointerPressRaycast = pointerEventData.pointerCurrentRaycast;
		return pointerEventData;
	}

	// Token: 0x06000025 RID: 37 RVA: 0x00005634 File Offset: 0x00003834
	private void ScanForFishShadows()
	{
		this.nearbyFishShadows.Clear();
		this.targetFishShadow = null;
		GameObject gameObject = this.findPlayerRoot();
		bool flag = gameObject == null;
		if (!flag)
		{
			Vector3 position = gameObject.transform.position;
			try
			{
				GameObject[] array = UnityEngine.Object.FindObjectsOfType<GameObject>();
				foreach (GameObject gameObject2 in array)
				{
					bool flag2 = gameObject2 == null || !gameObject2.activeInHierarchy;
					if (!flag2)
					{
						string name = gameObject2.name;
						bool flag3 = !name.StartsWith("p_fishshadow_shadow_");
						if (!flag3)
						{
							bool flag4 = !name.EndsWith("(Clone)");
							if (!flag4)
							{
								float num = Vector3.Distance(position, gameObject2.transform.position);
								bool flag5 = num <= this.fishShadowDetectRange;
								if (flag5)
								{
									this.nearbyFishShadows.Add(gameObject2);
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Warning("[AutoFish] Scan: " + ex.Message);
			}
			this.fishShadowCount = this.nearbyFishShadows.Count;
			bool flag6 = this.nearbyFishShadows.Count > 0;
			if (flag6)
			{
				float num2 = float.MaxValue;
				foreach (GameObject gameObject3 in this.nearbyFishShadows)
				{
					bool flag7 = gameObject3 == null;
					if (!flag7)
					{
						float num3 = Vector3.Distance(position, gameObject3.transform.position);
						bool flag8 = num3 < num2;
						if (flag8)
						{
							num2 = num3;
							this.targetFishShadow = gameObject3;
						}
					}
				}
				string text = (this.targetFishShadow != null) ? this.targetFishShadow.name.Replace("\"", "'") : "null";
				string text2 = "";
				foreach (GameObject gameObject4 in this.nearbyFishShadows)
				{
					bool flag9 = gameObject4 != null;
					if (flag9)
					{
						text2 = text2 + gameObject4.name.Replace("\"", "'") + ";";
					}
				}
				AutoFishLogic.DbgLog("H4,H9", "ScanForFishShadows", "Found fish shadows", string.Concat(new string[]
				{
					"{\"count\":",
					this.fishShadowCount.ToString(),
					",\"target\":\"",
					text,
					"\",\"bestDist\":",
					num2.ToString("F1"),
					",\"allFish\":\"",
					text2,
					"\"}"
				}));
			}
			else
			{
				AutoFishLogic.DbgLog("H9", "ScanForFishShadows", "No fish found in range", "{\"count\":0,\"maxRange\":" + this.fishShadowDetectRange.ToString("F1") + "}");
			}
		}
	}

	// Token: 0x06000026 RID: 38 RVA: 0x00005970 File Offset: 0x00003B70
	private void ClearAllFlags()
	{
		AutoFishLogic.SimulateFishFKeyHeld = false;
		AutoFishLogic.SimulateFishFKeyDown = false;
		AutoFishLogic.SimulateFishFKeyUp = false;
		AutoFishLogic.SimulateMouseButton0 = false;
		AutoFishLogic.SimulateMouseButton0Down = false;
		AutoFishLogic.SimulateMouseButton0Up = false;
		AutoFishLogic.OverrideMousePosition = false;
		AutoFishLogic.SimulateWKeyHeld = (AutoFishLogic.SimulateAKeyHeld = (AutoFishLogic.SimulateSKeyHeld = (AutoFishLogic.SimulateDKeyHeld = false)));
		AutoFishLogic.SimulateWKeyDown = (AutoFishLogic.SimulateAKeyDown = (AutoFishLogic.SimulateSKeyDown = (AutoFishLogic.SimulateDKeyDown = false)));
	}

	// Token: 0x06000027 RID: 39 RVA: 0x000059D8 File Offset: 0x00003BD8
	public void ToggleAutoFish()
	{
		this.autoFishEnabled = !this.autoFishEnabled;
		bool flag = this.autoFishEnabled;
		if (flag)
		{
			MelonLogger.Msg("[AutoFish] ON  *** BUILD=LOCAL_H39_20260210 ***");
			AutoFishLogic.DbgLog("H23", "ToggleAutoFish", "Auto fish ENABLED - BUILD MARKER", "{\"build\":\"LOCAL_H39_20260210\",\"time\":" + Time.unscaledTime.ToString() + "}");
			this.TransitionTo(AutoFishLogic.FishingState.Scanning);
		}
		else
		{
			MelonLogger.Msg("[AutoFish] OFF");
			this.ForceStop();
		}
	}

	// Token: 0x06000028 RID: 40 RVA: 0x00005A5C File Offset: 0x00003C5C
	public void HandleHotkeyInput()
	{
		bool flag = this.isListeningForFishHotkey;
		if (flag)
		{
			foreach (object obj in Enum.GetValues(typeof(KeyCode)))
			{
				KeyCode keyCode = (KeyCode)obj;
				bool flag2 = Input.GetKeyDown(keyCode) && keyCode != KeyCode.None && (int)keyCode != 323 && (int)keyCode != 324;
				if (flag2)
				{
					this.autoFishHotkey = keyCode;
					this.autoFishHotkeyDisplayName = keyCode.ToString();
					this.isListeningForFishHotkey = false;
					break;
				}
			}
		}
		else
		{
			bool keyDown = Input.GetKeyDown(this.autoFishHotkey);
			if (keyDown)
			{
				this.ToggleAutoFish();
			}
		}
	}

	// Token: 0x06000029 RID: 41 RVA: 0x00005B34 File Offset: 0x00003D34
	public void ForceStop()
	{
		this.autoFishEnabled = false;
		this.DoRelease();
		this.ClearAllFlags();
		this.currentState = AutoFishLogic.FishingState.Idle;
		this.targetFishShadow = null;
		this.nearbyFishShadows.Clear();
		this.biteConfirmCount = 0;
		this.holdSent = false;
		this.castClickDone = false;
		this.fishingPanelWasActive = false;
		this.vfxActiveAtCastStart = false;
		this.vfxBecameInactive = false;
		AutoFishLogic.DbgLog("H11", "ForceStop", "Auto fish stopped - all state reset", "{\"time\":" + Time.unscaledTime.ToString() + "}");
	}

	// Token: 0x0600002A RID: 42 RVA: 0x00005BCC File Offset: 0x00003DCC
	public void ResetStats()
	{
		this.fishAttemptCount = 0;
		this.fishCaughtCount = 0;
	}

	// Token: 0x0600002B RID: 43 RVA: 0x00005BE0 File Offset: 0x00003DE0
	public string GetStatusString()
	{
		string result;
		switch (this.currentState)
		{
		case AutoFishLogic.FishingState.Idle:
			result = "Idle";
			break;
		case AutoFishLogic.FishingState.Scanning:
			result = $"Scanning ({this.fishShadowCount})";
			break;
		case AutoFishLogic.FishingState.Casting:
			result = "Cast!";
			break;
		case AutoFishLogic.FishingState.WaitingBite:
		{
			float value = Time.unscaledTime - this.stateStartTime;
			string value2 = this.strugglingVfxVisible ? " [VFX!]" : (this.fishingPanelVisible ? " [FP!]" : "");
			result = $"Waiting bite {value:F0}s{value2}";
			break;
		}
		case AutoFishLogic.FishingState.Reeling:
		{
			float value = Time.unscaledTime - this.stateStartTime;
			string value3 = this.isHolding ? "HOLD" : "PAUSE";
			result = $"REEL {value3} {value:F1}s ({this.cycleTimer:F1}s)";
			break;
		}
		case AutoFishLogic.FishingState.Cooldown:
		{
			float value4 = this.cooldownDuration - (Time.unscaledTime - this.stateStartTime);
			result = $"Cooldown {value4:F1}s";
			break;
		}
		default:
			result = "?";
			break;
		}
		return result;
	}

	// Token: 0x0600002C RID: 44 RVA: 0x00005DE1 File Offset: 0x00003FE1
	public float GetStateTime()
	{
		return Time.unscaledTime - this.stateStartTime;
	}

	// Token: 0x0600002D RID: 45 RVA: 0x00005DF0 File Offset: 0x00003FF0
	public string GetTargetInfo()
	{
		bool flag = this.targetFishShadow != null && this.targetFishShadow.activeInHierarchy;
		string result;
		if (flag)
		{
			GameObject gameObject = this.findPlayerRoot();
			bool flag2 = gameObject != null;
			if (flag2)
			{
				float value = Vector3.Distance(gameObject.transform.position, this.targetFishShadow.transform.position);
				result = $"Locked ({value:F1}m)";
			}
			else
			{
				result = "Locked";
			}
		}
		else
		{
			result = "Searching...";
		}
		return result;
	}

	// Token: 0x04000010 RID: 16
	public static bool SimulateFishFKeyHeld = false;

	// Token: 0x04000011 RID: 17
	public static bool SimulateFishFKeyDown = false;

	// Token: 0x04000012 RID: 18
	public static bool SimulateFishFKeyUp = false;

	// Token: 0x04000013 RID: 19
	public static bool SimulateWKeyHeld = false;

	// Token: 0x04000014 RID: 20
	public static bool SimulateAKeyHeld = false;

	// Token: 0x04000015 RID: 21
	public static bool SimulateSKeyHeld = false;

	// Token: 0x04000016 RID: 22
	public static bool SimulateDKeyHeld = false;

	// Token: 0x04000017 RID: 23
	public static bool SimulateWKeyDown = false;

	// Token: 0x04000018 RID: 24
	public static bool SimulateAKeyDown = false;

	// Token: 0x04000019 RID: 25
	public static bool SimulateSKeyDown = false;

	// Token: 0x0400001A RID: 26
	public static bool SimulateDKeyDown = false;

	// Token: 0x0400001B RID: 27
	public static bool SimulateMouseButton0 = false;

	// Token: 0x0400001C RID: 28
	public static bool SimulateMouseButton0Down = false;

	// Token: 0x0400001D RID: 29
	public static bool SimulateMouseButton0Up = false;

	// Token: 0x0400001E RID: 30
	public static Vector3 SimulateMousePosition = Vector3.zero;

	// Token: 0x0400001F RID: 31
	public static bool OverrideMousePosition = false;

	// Token: 0x04000020 RID: 32
	public bool autoFishEnabled = false;

	// Token: 0x04000021 RID: 33
	public AutoFishLogic.FishingState currentState = AutoFishLogic.FishingState.Idle;

	// Token: 0x04000022 RID: 34
	private float stateStartTime = 0f;

	// Token: 0x04000023 RID: 35
	private float lastScanTime = 0f;

	// Token: 0x04000024 RID: 36
	public float waitBiteTimeout = 30f;

	// Token: 0x04000025 RID: 37
	public float reelMaxDuration = 45f;

	// Token: 0x04000026 RID: 38
	public float cooldownDuration = 3f;

	// Token: 0x04000027 RID: 39
	public float fishShadowDetectRange = 5f;

	// Token: 0x04000028 RID: 40
	private GameObject targetFishShadow = null;

	// Token: 0x04000029 RID: 41
	private List<GameObject> nearbyFishShadows = new List<GameObject>();

	// Token: 0x0400002A RID: 42
	public int fishShadowCount = 0;

	// Token: 0x0400002B RID: 43
	private bool holdSent = false;

	// Token: 0x0400002C RID: 44
	public float reelHoldDuration = 2.5f;

	// Token: 0x0400002D RID: 45
	public float reelPauseDuration = 0.5f;

	// Token: 0x0400002E RID: 46
	private float cycleTimer = 0f;

	// Token: 0x0400002F RID: 47
	private bool isHolding = true;

	// Token: 0x04000030 RID: 48
	private List<GameObject> heldButtons = new List<GameObject>();

	// Token: 0x04000031 RID: 49
	private List<PointerEventData> heldPointerDatas = new List<PointerEventData>();

	// Token: 0x04000032 RID: 50
	private bool castClickDone = false;

	// Token: 0x04000033 RID: 51
	private bool vfxActiveAtCastStart = false;

	// Token: 0x04000034 RID: 52
	private bool vfxBecameInactive = false;

	// Token: 0x04000035 RID: 53
	private int biteConfirmCount = 0;

	// Token: 0x04000036 RID: 54
	private const int BITE_CONFIRM_THRESHOLD = 2;

	// Token: 0x04000037 RID: 55
	private const float MIN_WAIT_BEFORE_VFX_CHECK = 1f;

	// Token: 0x04000038 RID: 56
	private const string FISHING_PANEL_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Status/FishingPanel(Clone)";

	// Token: 0x04000039 RID: 57
	private readonly string[] STRUGGLING_VFX_NAMES = new string[]
	{
		"p_vfx_fishing_struggling_01_H(Clone)",
		"p_vfx_fishing_struggling_02_H(Clone)",
		"p_vfx_fishing_struggling_03_H(Clone)",
		"p_vfx_fishing_struggling_04_H(Clone)",
		"p_vfx_fishing_bite_01(Clone)",
		"p_vfx_fishing_bite_02(Clone)",
		"p_vfx_fishing_bite_03(Clone)",
		"p_vfx_fishing_bite_01_H(Clone)",
		"p_vfx_fishing_bite_02_H(Clone)",
		"p_vfx_fishing_bite_03_H(Clone)"
	};

	// Token: 0x0400003A RID: 58
	private const string SCOPE_VFX_NAME = "p_vfx_basic_scope_01_H(Clone)";

	// Token: 0x0400003B RID: 59
	public bool strugglingVfxVisible = false;

	// Token: 0x0400003C RID: 60
	public bool fishingPanelVisible = false;

	// Token: 0x0400003D RID: 61
	private bool fishingPanelWasActive = false;

	// Token: 0x0400003E RID: 62
	public bool autoAimEnabled = true;

	// Token: 0x0400003F RID: 63
	public int fishAttemptCount = 0;

	// Token: 0x04000040 RID: 64
	public int fishCaughtCount = 0;

	// Token: 0x04000041 RID: 65
	public KeyCode autoFishHotkey = (KeyCode)291;

	// Token: 0x04000042 RID: 66
	public string autoFishHotkeyDisplayName = "F10";

	// Token: 0x04000043 RID: 67
	public bool isListeningForFishHotkey = false;

	// Token: 0x04000044 RID: 68
	private Func<GameObject> findPlayerRoot;

	// Token: 0x04000045 RID: 69
	private static readonly string _dbgLogPath = "d:\\SRC\\exedll\\.cursor\\debug.log";

	// Token: 0x0200001D RID: 29
	public enum FishingState
	{
		// Token: 0x0400010C RID: 268
		Idle,
		// Token: 0x0400010D RID: 269
		Scanning,
		// Token: 0x0400010E RID: 270
		Casting,
		// Token: 0x0400010F RID: 271
		WaitingBite,
		// Token: 0x04000110 RID: 272
		Reeling,
		// Token: 0x04000111 RID: 273
		Cooldown
	}
}
