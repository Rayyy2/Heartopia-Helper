using System;
using MelonLoader;
using UnityEngine;

// Token: 0x02000004 RID: 4
public class AutoFishFarm
{
	// Token: 0x06000003 RID: 3 RVA: 0x00002064 File Offset: 0x00000264
	public AutoFishFarm(AutoFishLogic fishLogic, Action<Vector3> teleportAction)
	{
		this.fishLogic = fishLogic;
		this.teleportAction = teleportAction;
	}

	// Token: 0x06000004 RID: 4 RVA: 0x000020E4 File Offset: 0x000002E4
	public void Update()
	{
		bool flag = !this.farmEnabled || this.fishLogic == null;
		if (!flag)
		{
			switch (this.currentFarmState)
			{
			case AutoFishFarm.FarmState.TeleportToSpot:
				this.DoTeleportToSpot();
				break;
			case AutoFishFarm.FarmState.WaitAfterTeleport:
				this.UpdateWaitAfterTeleport();
				break;
			case AutoFishFarm.FarmState.ScanningForFish:
				this.UpdateScanningForFish();
				break;
			case AutoFishFarm.FarmState.FishingAtSpot:
				this.UpdateFishingAtSpot();
				break;
			case AutoFishFarm.FarmState.PostCatch:
				this.UpdatePostCatch();
				break;
			}
		}
	}

	// Token: 0x06000005 RID: 5 RVA: 0x00002160 File Offset: 0x00000360
	private void DoTeleportToSpot()
	{
		bool flag = AutoFishFarm.fishingSpotPositions.Length == 0;
		if (!flag)
		{
			bool autoFishEnabled = this.fishLogic.autoFishEnabled;
			if (autoFishEnabled)
			{
				this.fishLogic.ForceStop();
			}
			Vector3 vector = AutoFishFarm.fishingSpotPositions[this.currentSpotIndex];
			MelonLogger.Msg($"[FishFarm] Teleporting to spot #{this.currentSpotIndex + 1}/{AutoFishFarm.fishingSpotPositions.Length} ({vector.x:F1}, {vector.y:F1}, {vector.z:F1})");
			Action<Vector3> action = this.teleportAction;
			if (action != null)
			{
				action(vector);
			}
			this.TransitionTo(AutoFishFarm.FarmState.WaitAfterTeleport);
		}
	}

	// Token: 0x06000006 RID: 6 RVA: 0x00002284 File Offset: 0x00000484
	private void UpdateWaitAfterTeleport()
	{
		bool flag = Time.unscaledTime - this.stateStartTime >= this.teleportDelay;
		if (flag)
		{
			this.caughtCountAtStart = this.fishLogic.fishCaughtCount;
			bool flag2 = !this.fishLogic.autoFishEnabled;
			if (flag2)
			{
				this.fishLogic.ToggleAutoFish();
			}
			MelonLogger.Msg($"[FishFarm] Arrived at spot #{this.currentSpotIndex + 1}, scanning for fish...");
			this.TransitionTo(AutoFishFarm.FarmState.ScanningForFish);
		}
	}

	// Token: 0x06000007 RID: 7 RVA: 0x00002328 File Offset: 0x00000528
	private void UpdateScanningForFish()
	{
		float num = Time.unscaledTime - this.stateStartTime;
		bool flag = this.fishLogic.currentState == AutoFishLogic.FishingState.Casting || this.fishLogic.currentState == AutoFishLogic.FishingState.WaitingBite || this.fishLogic.currentState == AutoFishLogic.FishingState.Reeling;
		if (flag)
		{
			this.spotsWithFish++;
			MelonLogger.Msg($"[FishFarm] Fish found at spot #{this.currentSpotIndex + 1}! Fishing...");
			this.TransitionTo(AutoFishFarm.FarmState.FishingAtSpot);
		}
		else
		{
			bool flag2 = num >= this.scanTimeout;
			if (flag2)
			{
				MelonLogger.Msg($"[FishFarm] No fish at spot #{this.currentSpotIndex + 1}, moving on...");
				bool autoFishEnabled = this.fishLogic.autoFishEnabled;
				if (autoFishEnabled)
				{
					this.fishLogic.ForceStop();
				}
				this.MoveToNextSpot();
			}
		}
	}

	// Token: 0x06000008 RID: 8 RVA: 0x00002448 File Offset: 0x00000648
	private void UpdateFishingAtSpot()
	{
		bool flag = this.fishLogic.fishCaughtCount > this.caughtCountAtStart;
		if (flag)
		{
			this.totalFishCaught++;
			MelonLogger.Msg($"[FishFarm] Fish caught at spot #{this.currentSpotIndex + 1}! Total: {this.totalFishCaught}");
			bool autoFishEnabled = this.fishLogic.autoFishEnabled;
			if (autoFishEnabled)
			{
				this.fishLogic.ForceStop();
			}
			this.TransitionTo(AutoFishFarm.FarmState.PostCatch);
		}
		else
		{
			bool flag2 = Time.unscaledTime - this.stateStartTime >= 120f;
			if (flag2)
			{
				MelonLogger.Msg("[FishFarm] Fishing timeout at spot, moving on...");
				bool autoFishEnabled2 = this.fishLogic.autoFishEnabled;
				if (autoFishEnabled2)
				{
					this.fishLogic.ForceStop();
				}
				this.MoveToNextSpot();
			}
		}
	}

	// Token: 0x06000009 RID: 9 RVA: 0x00002540 File Offset: 0x00000740
	private void UpdatePostCatch()
	{
		bool flag = Time.unscaledTime - this.stateStartTime >= this.postCatchDelay;
		if (flag)
		{
			this.MoveToNextSpot();
		}
	}

	// Token: 0x0600000A RID: 10 RVA: 0x00002572 File Offset: 0x00000772
	private void MoveToNextSpot()
	{
		this.currentSpotIndex = (this.currentSpotIndex + 1) % AutoFishFarm.fishingSpotPositions.Length;
		this.spotsVisited++;
		this.TransitionTo(AutoFishFarm.FarmState.TeleportToSpot);
	}

	// Token: 0x0600000B RID: 11 RVA: 0x000025A1 File Offset: 0x000007A1
	private void TransitionTo(AutoFishFarm.FarmState newState)
	{
		this.currentFarmState = newState;
		this.stateStartTime = Time.unscaledTime;
	}

	// Token: 0x0600000C RID: 12 RVA: 0x000025B8 File Offset: 0x000007B8
	public void ToggleFarm()
	{
		this.farmEnabled = !this.farmEnabled;
		bool flag = this.farmEnabled;
		if (flag)
		{
			MelonLogger.Msg("[FishFarm] ENABLED - Auto teleport fishing started!");
			this.spotsVisited = 0;
			this.spotsWithFish = 0;
			this.totalFishCaught = 0;
			this.TransitionTo(AutoFishFarm.FarmState.TeleportToSpot);
		}
		else
		{
			this.StopFarm();
		}
	}

	// Token: 0x0600000D RID: 13 RVA: 0x00002614 File Offset: 0x00000814
	public void StopFarm()
	{
		this.farmEnabled = false;
		this.currentFarmState = AutoFishFarm.FarmState.Idle;
		bool flag = this.fishLogic != null && this.fishLogic.autoFishEnabled;
		if (flag)
		{
			this.fishLogic.ForceStop();
		}
		MelonLogger.Msg("[FishFarm] STOPPED");
	}

	// Token: 0x0600000E RID: 14 RVA: 0x00002664 File Offset: 0x00000864
	public string GetFarmStatusString()
	{
		string result;
		switch (this.currentFarmState)
		{
		case AutoFishFarm.FarmState.Idle:
			result = "Idle";
			break;
		case AutoFishFarm.FarmState.TeleportToSpot:
			result = $"Teleporting to #{this.currentSpotIndex + 1}";
			break;
		case AutoFishFarm.FarmState.WaitAfterTeleport:
			result = $"Arriving... {(Time.unscaledTime - this.stateStartTime):F1}s";
			break;
		case AutoFishFarm.FarmState.ScanningForFish:
			result = $"Scanning #{this.currentSpotIndex + 1} ({(Time.unscaledTime - this.stateStartTime):F0}s/{this.scanTimeout:F0}s)";
			break;
		case AutoFishFarm.FarmState.FishingAtSpot:
			result = $"Fishing at #{this.currentSpotIndex + 1}...";
			break;
		case AutoFishFarm.FarmState.PostCatch:
			result = "Fish caught! Moving...";
			break;
		default:
			result = "?";
			break;
		}
		return result;
	}

	// Token: 0x04000002 RID: 2
	public bool farmEnabled = false;

	// Token: 0x04000003 RID: 3
	public AutoFishFarm.FarmState currentFarmState = AutoFishFarm.FarmState.Idle;

	// Token: 0x04000004 RID: 4
	public int currentSpotIndex = 0;

	// Token: 0x04000005 RID: 5
	private float stateStartTime = 0f;

	// Token: 0x04000006 RID: 6
	private int caughtCountAtStart = 0;

	// Token: 0x04000007 RID: 7
	public float scanTimeout = 8f;

	// Token: 0x04000008 RID: 8
	public float teleportDelay = 2f;

	// Token: 0x04000009 RID: 9
	public float postCatchDelay = 2f;

	// Token: 0x0400000A RID: 10
	public int spotsVisited = 0;

	// Token: 0x0400000B RID: 11
	public int spotsWithFish = 0;

	// Token: 0x0400000C RID: 12
	public int totalFishCaught = 0;

	// Token: 0x0400000D RID: 13
	private AutoFishLogic fishLogic;

	// Token: 0x0400000E RID: 14
	private Action<Vector3> teleportAction;

	// Token: 0x0400000F RID: 15
	public static readonly Vector3[] fishingSpotPositions = new Vector3[]
	{
		new Vector3(7.6f, 23.7f, 80.9f),
		new Vector3(-31f, 20.4f, 86.7f),
		new Vector3(-69.7f, 20.1f, 85.1f),
		new Vector3(-75.2f, 20.3f, 83.8f),
		new Vector3(-73.1f, 20f, 102.5f),
		new Vector3(-88.1f, 20f, 100.9f),
		new Vector3(-91.6f, 20.2f, 101.6f),
		new Vector3(-99.4f, 20f, 112.8f),
		new Vector3(-106.9f, 19.8f, 117.7f),
		new Vector3(-119.2f, 19.5f, 120f),
		new Vector3(-75.9f, 24.9f, 57.2f),
		new Vector3(-81.5f, 25.1f, 58f),
		new Vector3(-93.2f, 25f, 58.3f),
		new Vector3(-100.7f, 25.3f, 53.7f),
		new Vector3(-104.5f, 24.9f, 47.1f),
		new Vector3(-98f, 24.9f, 41.2f),
		new Vector3(-103.2f, 19.3f, -41.6f),
		new Vector3(-103.2f, 19.3f, -47.9f),
		new Vector3(-99f, 19.3f, -53.7f),
		new Vector3(-86f, 19.6f, -56.8f),
		new Vector3(-87.5f, 19.7f, -49.6f),
		new Vector3(-90.4f, 19.4f, -47.3f),
		new Vector3(-63.8f, 17.4f, -85.4f),
		new Vector3(-72.2f, 18.4f, -78.8f),
		new Vector3(-85.2f, 18.5f, -81.1f),
		new Vector3(-136f, 10.8f, -164.4f),
		new Vector3(80.3f, 21.7f, 2f),
		new Vector3(83.4f, 21.6f, 9.2f),
		new Vector3(83.2f, 21.6f, 14.3f),
		new Vector3(79f, 22f, 28.6f),
		new Vector3(81.1f, 22.2f, 33.5f),
		new Vector3(79.3f, 22.1f, 40.1f),
		new Vector3(78.9f, 21.8f, 47.1f),
		new Vector3(74.9f, 21.8f, 59.4f),
		new Vector3(74.8f, 21.5f, -22.9f),
		new Vector3(70.7f, 21.5f, -37.1f),
		new Vector3(65.2f, 21.5f, -33f),
		new Vector3(177.1f, 31.2f, 94.4f),
		new Vector3(181.8f, 31.2f, 89.2f),
		new Vector3(177.2f, 31.4f, 82.7f),
		new Vector3(158.5f, 21.1f, 19.7f),
		new Vector3(156.3f, 21.3f, 11.9f),
		new Vector3(166.4f, 21.2f, 3.1f),
		new Vector3(-34.8f, 10.6f, -142.6f),
		new Vector3(-30f, 11.3f, 154.9f),
		new Vector3(-39.6f, 11.3f, -161f),
		new Vector3(-30.5f, 10.8f, 130.2f),
		new Vector3(-23.7f, 11f, 131.1f),
		new Vector3(-17.9f, 11.1f, -134.8f),
		new Vector3(-11.2f, 10.7f, -134.6f),
		new Vector3(-5.4f, 11f, -148.6f),
		new Vector3(-3.9f, 10.9f, -155.1f),
		new Vector3(-2.6f, 10.9f, -158.5f),
		new Vector3(2.8f, 10.9f, -159.8f),
		new Vector3(11.1f, 10.7f, -154.1f),
		new Vector3(19.6f, 10.6f, -155.7f),
		new Vector3(25.1f, 10.7f, -155.8f),
		new Vector3(27.4f, 10.8f, -154f),
		new Vector3(42.5f, 11.1f, -166.9f),
		new Vector3(30f, 12.4f, -168.6f),
		new Vector3(17.1f, 11.5f, -173.7f),
		new Vector3(16f, 11.5f, -174.1f),
		new Vector3(15.3f, 11.5f, -175.9f),
		new Vector3(16.2f, 11.5f, -177.3f),
		new Vector3(22.5f, 10.5f, -178.1f),
		new Vector3(22.8f, 10.5f, -180f),
		new Vector3(22.7f, 10.6f, -182.6f),
		new Vector3(26.9f, 11.5f, -186.9f),
		new Vector3(23.5f, 11.5f, -187.6f),
		new Vector3(20.2f, 11.5f, -188.3f),
		new Vector3(18.2f, 11.5f, -189.6f),
		new Vector3(15.7f, 11.5f, -188.9f),
		new Vector3(16f, 11.5f, -186.6f),
		new Vector3(18f, 11.5f, -186.2f)
	};

	// Token: 0x0200001C RID: 28
	public enum FarmState
	{
		// Token: 0x04000105 RID: 261
		Idle,
		// Token: 0x04000106 RID: 262
		TeleportToSpot,
		// Token: 0x04000107 RID: 263
		WaitAfterTeleport,
		// Token: 0x04000108 RID: 264
		ScanningForFish,
		// Token: 0x04000109 RID: 265
		FishingAtSpot,
		// Token: 0x0400010A RID: 266
		PostCatch
	}
}
