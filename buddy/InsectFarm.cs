using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HeartopiaMod
{
    public static class InsectFarm
    {
        // Config / state
        private static bool insectFarmEnabled = false;
        public static bool IsEnabled => insectFarmEnabled;
        // Time between teleport attempts (controlled by UI slider)
        private static float insectTeleportCooldown = 1.5f;
        // Auto-stop for Insect Farm
        private static bool autoInsectFarmAutoStopEnabled = false;
        private static int autoInsectFarmAutoStopHours = 0;
        private static int autoInsectFarmAutoStopMinutes = 0;
        private static int autoInsectFarmAutoStopSeconds = 0;
        private static string autoInsectFarmAutoStopHoursInput = "0";
        private static string autoInsectFarmAutoStopMinutesInput = "0";
        private static string autoInsectFarmAutoStopSecondsInput = "0";
        private static float autoInsectFarmAutoStopAt = -1f;
        // Fixed duration to run the simulated F-key sequence after teleport
        private const float insectSimDuration = 0.6f;
        private static float insectTeleportOffset = 1f;
        private static float insectScanTimeout = 10f;
            public static float GetTeleportCooldown() => insectTeleportCooldown;
            public static void SetTeleportCooldown(float v) { insectTeleportCooldown = v; }
            public static float GetScanTimeout() => insectScanTimeout;
            public static void SetScanTimeout(float v) { insectScanTimeout = v; }
            public static float GetTeleportOffset() => insectTeleportOffset;
            public static void SetTeleportOffset(float v) { insectTeleportOffset = v; }
        private static List<Vector3> patrolLocations = new List<Vector3>()
        {
            new Vector3(72.630f,25.590f,-2.451f),
            new Vector3(65.529f,26.348f,32.722f),
            new Vector3(69.554f,23.489f,70.941f),
            new Vector3(13.055f,25.171f,79.288f),
            new Vector3(-22.724f,23.678f,88.086f),
            new Vector3(-54.698f,21.164f,86.624f),
            new Vector3(-97.321f,20.629f,113.111f),
            new Vector3(-91.336f,25.547f,69.312f),
            new Vector3(-109.310f,19.931f,-40.587f),
            new Vector3(-90.820f,19.002f,-96.760f),
            new Vector3(-82.552f,27.026f,-153.556f),
            new Vector3(-21.354f,15.274f,-101.155f),
            new Vector3(9.246f,14.482f,-132.875f),
            new Vector3(58.208f,19.692f,-137.300f),
            new Vector3(107.229f,19.699f,-135.771f),
            new Vector3(158.384f,24.081f,-97.293f),
            new Vector3(150.486f,21.504f,-52.707f),
            new Vector3(185.881f,20.919f,-20.343f),
            new Vector3(207.724f,21.619f,62.205f),
            new Vector3(170.220f,31.484f,89.347f),
            new Vector3(113.930f,20.028f,116.534f),
            new Vector3(50.200f,22.680f,165.194f),
            new Vector3(11.113f,24.039f,229.267f),
            new Vector3(-19.243f,28.761f,208.386f),
            new Vector3(-52.120f,31.123f,212.418f),
            new Vector3(-81.265f,31.086f,186.662f),
            new Vector3(-129.224f,35.219f,219.063f),
            new Vector3(-146.526f,27.472f,181.798f),
            new Vector3(-185.296f,23.685f,104.815f),
            new Vector3(-238.499f,35.778f,18.708f),
            new Vector3(-194.233f,19.750f,-12.592f),
            new Vector3(-184.032f,25.326f,-87.924f),
            new Vector3(-136.144f,17.994f,-136.361f),
            new Vector3(-3.093f,23.280f,156.825f)
        };
        private static int patrolIndex = 0;
        private static float lastPatrolTeleportTime = 0f;

        // Runtime
        private static float lastTeleportTime = 0f;
        private static bool insectSimulating = false;
        private static float insectSimStart = 0f;
        private static int insectSimFrame = 0;
        // Track whether host requested a resource-repair pause (so we can clear sims once)
        private static bool wasHostPaused = false;

        // Draws the Insect Farm subtab UI. Returns new Y position (height) as float.
        public static float DrawTab(HeartopiaComplete host, int startY)
        {
            int num = startY;
            GUIStyle small = new GUIStyle(GUI.skin.label) { fontSize = 12 };

            if (host.UI_DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), "Equip Net"))
            {
                host.StartToolEquipRequest(2);
            }
            num += 45;

            string toggleText = insectFarmEnabled ? "DISABLE INSECT CATCHING" : "ENABLE INSECT CATCHING";
            if (host.UI_DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 40f), toggleText))
            {
                insectFarmEnabled = !insectFarmEnabled;
                host.UI_AddMenuNotification($"Insect Farm {(insectFarmEnabled ? "Enabled" : "Disabled")}", insectFarmEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                if (insectFarmEnabled)
                {
                    host.showInsectRadar = true;
                    host.isRadarActive = true;
                    host.RunRadar();
                    int secs = GetAutoInsectFarmAutoStopSeconds();
                    if (autoInsectFarmAutoStopEnabled && secs > 0)
                    {
                        autoInsectFarmAutoStopAt = Time.unscaledTime + secs;
                        host.UI_AddMenuNotification("Insect Farm auto-stop set: " + host.FormatDurationHms(secs), new Color(0.55f, 0.88f, 1f));
                    }
                    else
                    {
                        autoInsectFarmAutoStopAt = -1f;
                    }
                }
                else
                {
                    host.showInsectRadar = false;
                    autoInsectFarmAutoStopAt = -1f;
                }
            }
            num += 50;

            GUI.Label(new Rect(20f, (float)num, 360f, 24f), "Status: " + (insectFarmEnabled ? "Running" : "Idle"));
            num += 28;

            // Auto Stop Timer (placed above Teleport Cooldown)
            autoInsectFarmAutoStopEnabled = host.UI_DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), autoInsectFarmAutoStopEnabled, "Auto Stop Timer");
            num += 30;
            if (autoInsectFarmAutoStopEnabled)
            {
                GUIStyle timerSmall = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                GUI.Label(new Rect(20f, (float)num, 260f, 18f), "Timer (HH:MM:SS)", timerSmall);
                num += 20;

                GUI.Label(new Rect(20f, (float)num, 45f, 20f), "H", timerSmall);
                autoInsectFarmAutoStopHoursInput = GUI.TextField(new Rect(35f, (float)num, 55f, 22f), autoInsectFarmAutoStopHoursInput, 2);
                GUI.Label(new Rect(95f, (float)num, 10f, 20f), ":", timerSmall);

                GUI.Label(new Rect(108f, (float)num, 45f, 20f), "M", timerSmall);
                autoInsectFarmAutoStopMinutesInput = GUI.TextField(new Rect(123f, (float)num, 55f, 22f), autoInsectFarmAutoStopMinutesInput, 2);
                GUI.Label(new Rect(183f, (float)num, 10f, 20f), ":", timerSmall);

                GUI.Label(new Rect(196f, (float)num, 45f, 20f), "S", timerSmall);
                autoInsectFarmAutoStopSecondsInput = GUI.TextField(new Rect(211f, (float)num, 55f, 22f), autoInsectFarmAutoStopSecondsInput, 2);
                num += 28;

                int parsed;
                if (int.TryParse(autoInsectFarmAutoStopHoursInput, out parsed)) { autoInsectFarmAutoStopHours = Mathf.Clamp(parsed, 0, 23); autoInsectFarmAutoStopHoursInput = autoInsectFarmAutoStopHours.ToString(); }
                if (int.TryParse(autoInsectFarmAutoStopMinutesInput, out parsed)) { autoInsectFarmAutoStopMinutes = Mathf.Clamp(parsed, 0, 59); autoInsectFarmAutoStopMinutesInput = autoInsectFarmAutoStopMinutes.ToString(); }
                if (int.TryParse(autoInsectFarmAutoStopSecondsInput, out parsed)) { autoInsectFarmAutoStopSeconds = Mathf.Clamp(parsed, 0, 59); autoInsectFarmAutoStopSecondsInput = autoInsectFarmAutoStopSeconds.ToString(); }

                int secs = GetAutoInsectFarmAutoStopSeconds();
                if (secs <= 0)
                {
                    Color prev = GUI.color; GUI.color = new Color(1f, 0.45f, 0.45f); GUI.Label(new Rect(20f, (float)num, 300f, 20f), "Set at least 1 second to enable auto-stop.", timerSmall); GUI.color = prev; num += 24;
                }
                else
                {
                    GUI.Label(new Rect(20f, (float)num, 320f, 20f), "Auto-stop after: " + host.FormatDurationHms(secs), timerSmall);
                    num += 22;
                    if (insectFarmEnabled && autoInsectFarmAutoStopAt > 0f)
                    {
                        int remaining = Mathf.Max(0, Mathf.CeilToInt(autoInsectFarmAutoStopAt - Time.unscaledTime));
                        GUI.Label(new Rect(20f, (float)num, 320f, 20f), "Time remaining: " + host.FormatDurationHms(remaining), timerSmall);
                        num += 22;
                    }
                }
            }

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Teleport Cooldown: {insectTeleportCooldown:F1}s", small);
            num += 22;
            float prevInsectTp = insectTeleportCooldown;
            insectTeleportCooldown = host.UI_DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), insectTeleportCooldown, 0.2f, 10f);
            if (Math.Abs(insectTeleportCooldown - prevInsectTp) > 0.0001f) { SetTeleportCooldown(insectTeleportCooldown); try { host.UI_SaveKeybinds(false); } catch { } }
            num += 30;

            // (Simulate duration removed; simulation uses a fixed short duration)

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Scan Timeout: {insectScanTimeout:F1}s", small);
            num += 22;
            float prevInsectScan = insectScanTimeout;
            insectScanTimeout = host.UI_DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), insectScanTimeout, 1f, 30f);
            if (Math.Abs(insectScanTimeout - prevInsectScan) > 0.0001f) { SetScanTimeout(insectScanTimeout); try { host.UI_SaveKeybinds(false); } catch { } }
            num += 30;

            GUI.Label(new Rect(20f, (float)num, 360f, 20f), $"Auto-Repair (Paused TP Farm): {host.GetResourceAutoRepairPauseSeconds():F0}s", small);
            num += 22;
            float newPause = host.GetResourceAutoRepairPauseSeconds();
            newPause = host.UI_DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), newPause, 0f, 60f);
            if (newPause != host.GetResourceAutoRepairPauseSeconds()) { host.SetResourceAutoRepairPauseSeconds(newPause); try { host.UI_SaveKeybinds(false); } catch { } }
            num += 30;

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Teleport Offset: {insectTeleportOffset:F2}m", small);
            num += 22;
            float prevInsectOffset = insectTeleportOffset;
            insectTeleportOffset = host.UI_DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), insectTeleportOffset, 0f, 5f);
            if (Math.Abs(insectTeleportOffset - prevInsectOffset) > 0.0001f) { SetTeleportOffset(insectTeleportOffset); try { host.UI_SaveKeybinds(false); } catch { } }
            num += 30;
            
            return (float)num;
        }

        // Called every update from host. Uses host radar/tracked markers via public helpers.
        public static void Update(HeartopiaComplete host)
        {
            try
            {
                if (!insectFarmEnabled) return;
                // Auto-stop check
                if (autoInsectFarmAutoStopEnabled && autoInsectFarmAutoStopAt > 0f && Time.unscaledTime >= autoInsectFarmAutoStopAt)
                {
                    insectFarmEnabled = false;
                    host.UI_AddMenuNotification("Insect Farm auto-stopped (timer)", new Color(1f, 0.75f, 0.45f));
                    autoInsectFarmAutoStopAt = -1f;
                    return;
                }

            // Respect teleport cooldown
            if (Time.unscaledTime - lastTeleportTime < insectTeleportCooldown) return;

                List<Vector3> insects = host.GetTrackedInsectPositions() ?? new List<Vector3>();

                GameObject player = host.GetPlayerObject();
                Vector3 playerPos = (player != null) ? player.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

                // Respect host auto-repair pause: don't teleport while paused
                try {
                    bool paused = host.IsResourceRepairPaused();
                    if (paused)
                    {
                        // On entering pause, immediately clear any simulated input and stop current sim
                        if (!wasHostPaused)
                        {
                            wasHostPaused = true;
                            StopSimulateFSequence();
                            TryUseDirectButton(host);
                        }
                        return;
                    }
                    else
                    {
                        wasHostPaused = false;
                    }
                } catch { }

                // If any insects detected by radar, teleport to the nearest one.
                if (insects != null && insects.Count > 0)
                {
                    Vector3 bestPos = Vector3.zero;
                    float bestDist = float.MaxValue;
                    foreach (var pos in insects)
                    {
                        float d = Vector3.Distance(playerPos, pos);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestPos = pos;
                        }
                    }

                    if (bestDist < float.MaxValue)
                    {
                        host.TeleportToLocationWithOffset(bestPos, insectTeleportOffset);
                        lastTeleportTime = Time.unscaledTime;
                        lastPatrolTeleportTime = Time.unscaledTime;

                        StartSimulateFSequence(host);
                    }
                }
                else
                {
                    if (Time.unscaledTime - lastPatrolTeleportTime >= insectScanTimeout)
                    {
                            if (patrolLocations != null && patrolLocations.Count > 0)
                            {
                                Vector3 tp = patrolLocations[patrolIndex % patrolLocations.Count];
                                patrolIndex = (patrolIndex + 1) % patrolLocations.Count;
                                host.TeleportToLocationWithOffset(tp, 0f);
                                lastTeleportTime = Time.unscaledTime;
                                lastPatrolTeleportTime = Time.unscaledTime;
                            }
                    }
                }

                // Handle ongoing simulate-F sequence
                if (insectSimulating)
                {
                    float dtSim = Time.unscaledTime - insectSimStart;
                    if (dtSim > insectSimDuration)
                    {
                        StopSimulateFSequence();
                    }
                    else
                    {
                        UpdateSimulateFSequence(host);
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Msg($"[InsectFarm] Update error: {ex.Message}");
            }
        }

        private static void TryInteract(HeartopiaComplete host)
        {
            try
            {
                // Only perform direct UI clicks when the EventSystem is active and enabled
                if (EventSystem.current != null && EventSystem.current.enabled)
                {
                    host.UI_DirectClickInteractButton();
                }
                else
                {
                    // Otherwise rely on simulated F-key flags already being set
                }
            }
            catch { }
        }

        // Attempt to click the in-game interact button directly. Returns true if click attempted.
        public static bool TryUseDirectButton(HeartopiaComplete host)
        {
            try
            {
                if (EventSystem.current != null && EventSystem.current.enabled)
                {
                    host.UI_DirectClickInteractButton();
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void StartSimulateFSequence(HeartopiaComplete host)
        {
            insectSimulating = true;
            insectSimStart = Time.unscaledTime;
            insectSimFrame = 0;
            HeartopiaComplete.SimulateFKeyDown = true;
            HeartopiaComplete.SimulateFKeyHeld = true;
            HeartopiaComplete.SimulateFKeyUp = false;
            TryUseDirectButton(host);
        }

        private static void UpdateSimulateFSequence(HeartopiaComplete host)
        {
            insectSimFrame++;
            int m = insectSimFrame % 6;
            if (m == 0)
            {
                HeartopiaComplete.SimulateFKeyDown = true;
                HeartopiaComplete.SimulateFKeyHeld = true;
                HeartopiaComplete.SimulateFKeyUp = false;
            }
            else if (m <= 3)
            {
                HeartopiaComplete.SimulateFKeyDown = false;
                HeartopiaComplete.SimulateFKeyHeld = true;
                HeartopiaComplete.SimulateFKeyUp = false;
            }
            else if (m == 4)
            {
                HeartopiaComplete.SimulateFKeyDown = false;
                HeartopiaComplete.SimulateFKeyHeld = false;
                HeartopiaComplete.SimulateFKeyUp = true;
            }
            else
            {
                HeartopiaComplete.SimulateFKeyDown = false;
                HeartopiaComplete.SimulateFKeyHeld = false;
                HeartopiaComplete.SimulateFKeyUp = false;
            }
            TryUseDirectButton(host);
        }

        private static void StopSimulateFSequence()
        {
            insectSimulating = false;
            HeartopiaComplete.SimulateFKeyHeld = false;
            HeartopiaComplete.SimulateFKeyDown = false;
            HeartopiaComplete.SimulateFKeyUp = false;
            insectSimFrame = 0;
        }

        private static int GetAutoInsectFarmAutoStopSeconds()
        {
            return Math.Max(0, autoInsectFarmAutoStopHours) * 3600
                + Math.Max(0, autoInsectFarmAutoStopMinutes) * 60
                + Math.Max(0, autoInsectFarmAutoStopSeconds);
        }
    }
}
