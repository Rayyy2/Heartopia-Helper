using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

using UnityObject = UnityEngine.Object;
using Il2CppType = Il2CppSystem.Type;
using Il2CppFieldInfo = Il2CppSystem.Reflection.FieldInfo;
using Il2CppMethodInfo = Il2CppSystem.Reflection.MethodInfo;
using Il2CppBindingFlags = Il2CppSystem.Reflection.BindingFlags;
using Il2CppObject = Il2CppSystem.Object;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(HeartopiaMod.HeartopiaComplete), "Heartopia Helper", "1.0.0", "HeartopiaMod")]
[assembly: MelonGame(null, null)]

namespace HeartopiaMod
{
    // Harmony Patch for Bulk Selector Item Detection
    [HarmonyPatch(typeof(UnityEngine.UI.Image), "set_sprite")]
    public static class SpriteDetectionPatch
    {
        public static void Postfix(UnityEngine.UI.Image __instance, UnityEngine.Sprite value)
        {
            if (value == null) return;
            string spriteName = value.name;
            if (spriteName.Contains("ui_item_normal"))
            {
                if (!HeartopiaComplete.discoveredItems.Contains(spriteName))
                {
                    HeartopiaComplete.discoveredItems.Add(spriteName);
                }
                Transform slot = __instance.transform.parent?.parent;
                if (slot != null)
                {
                    if (!HeartopiaComplete.slotCache.ContainsKey(spriteName))
                    {
                        HeartopiaComplete.slotCache[spriteName] = new List<Transform>();
                    }
                    if (!HeartopiaComplete.slotCache[spriteName].Contains(slot))
                    {
                        HeartopiaComplete.slotCache[spriteName].Add(slot);
                    }
                }
            }
        }
    }

    // Token: 0x02000004 RID: 4
    public class HeartopiaComplete : MelonMod
    {
        // Token: 0x06000003 RID: 3 RVA: 0x0000206C File Offset: 0x0000026C
        private void ScanMeteorites()
        {
            // Finds all objects starting with p_rock_meteorite (handles 6, 7, 8 etc.)
            meteorList = GameObject.FindObjectsOfType<GameObject>()
            .Where(obj => obj != null && obj.activeInHierarchy && obj.name.StartsWith("p_rock_meteorite"))
            .ToList();
        }

        // Keybinds Management
        private KeyCode keyToggleMenu = KeyCode.Insert;
        private KeyCode keyToggleRadar = KeyCode.None;
        private KeyCode keyAutoFarm = KeyCode.None;
        private KeyCode keyAutoCook = KeyCode.None;
        private KeyCode keyBypassUI = KeyCode.None;
        private KeyCode keyDisableAll = KeyCode.None;
        private KeyCode keyInspectPlayer = KeyCode.None;
        private KeyCode keyInspectMove = KeyCode.None;
        private KeyCode keyJumpBoost = KeyCode.None;
        private KeyCode keyAutoRepair = KeyCode.None;
        private KeyCode keyAutoJoinFriend = KeyCode.None;
        private KeyCode keyJoinPublic = KeyCode.None;
        private KeyCode keyJoinMyTown = KeyCode.None;
        
        // Key Rebinding State
        private string keyBindingActive = "";
        
        // Notification check for auto repair
        private float lastNotificationCheck = 0f;
        private const float NOTIFICATION_CHECK_INTERVAL = 2f; // Check every 2 seconds
        
        // --- WINDOWS API FOR ESC KEY ---
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll", SetLastError = true)]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        const uint WM_KEYDOWN = 0x0100;
        const uint WM_KEYUP = 0x0101;
        const uint WM_LBUTTONDOWN = 0x0201;
        const uint WM_LBUTTONUP = 0x0202;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const int MK_LBUTTON = 0x0001;
        const int VK_ESCAPE = 0x1B;
        const int VK_F = 0x46;

        // --- PATROL SYSTEM VARIABLES ---
        public class PatrolData { public List<SerializableVector3> Points = new List<SerializableVector3>(); }

        [Serializable]
        public class SerializableVector3
        {
            public float x, y, z;
            public SerializableVector3() { }
            public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
            public Vector3 ToVector3() { return new Vector3(x, y, z); }
        }

        [Serializable]
        public class SerializableQuaternion
        {
            public float x, y, z, w;
            public SerializableQuaternion() { }
            public SerializableQuaternion(Quaternion q) { x = q.x; y = q.y; z = q.z; w = q.w; }
            public Quaternion ToQuaternion() { return new Quaternion(x, y, z, w); }
        }

        [Serializable]
        public class CookingPatrolPoint
        {
            public SerializableVector3 Position;
            public SerializableQuaternion Rotation;
            public CookingPatrolPoint() { }
            public CookingPatrolPoint(Vector3 pos, Quaternion rot)
            {
                Position = new SerializableVector3(pos);
                Rotation = new SerializableQuaternion(rot);
            }
        }

        [Serializable]
        public class TreeFarmPatrolPoint
        {
            public SerializableVector3 Position;
            public SerializableQuaternion Rotation;
            public TreeFarmPatrolPoint() { }
            public TreeFarmPatrolPoint(Vector3 pos, Quaternion rot)
            {
                Position = new SerializableVector3(pos);
                Rotation = new SerializableQuaternion(rot);
            }
        }

        private class MenuNotification
        {
            public string Message;
            public Color Color;
            public float CreatedAt;
            public float ExpireAt;
            public float Duration;
        }

        public class CookingPatrolData
        {
            public List<CookingPatrolPoint> Points = new List<CookingPatrolPoint>();
        }

        public class TreeFarmPatrolData
        {
            public List<TreeFarmPatrolPoint> Points = new List<TreeFarmPatrolPoint>();
        }

        private List<Vector3> patrolPoints = new List<Vector3>();
        private bool isPatrolActive = false;
        private float waitAtSpot = 0.3f;
        private object patrolCoroutine;

        // --- COOKING PATROL VARIABLES ---
        private List<CookingPatrolPoint> cookingPatrolPoints = new List<CookingPatrolPoint>();
        private float cookingWaitAtSpot = 0.3f;
        private bool isCookingPatrolActive = false;
        private object cookingPatrolCoroutine;

        // --- AUTO REPAIR VARIABLES ---
        private const string REPAIR_KIT_SPRITE = "ui_item_normal_p_toolrestorer_toolrestorer_1";
        private const string AUTO_EAT_FOOD_KEY = "food_bluejam";
        private const string BAG_BUTTON_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/top_right_layout@go@t/menu_bar@go/bag@go@btn@frame";
        private const string USE_BUTTON_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Scene/BagPanel(Clone)/tip@w@t/operate@go/operate1@btn";
        private const string CLOSE_BUTTON_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Scene/BagPanel(Clone)/close@btn";
        private const string SCROLL_CONTENT_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Scene/BagPanel(Clone)/bag1@unbreakscroll/Content";
        private const string INTERACT_PROMPT_BUTTON_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn";
        private const string LOGIN_PANEL_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Full/LoginPanel(Clone)";
        private const string LOGIN_ROOM_PANEL_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Full/LoginRoomPanel(Clone)";
        private const string START_GAME_BUTTON_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Full/LoginPanel(Clone)/AniRoot@queueanimation/startGame@btn";
        private const string ROOM_ENTRY_BUTTON_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Full/LoginPanel(Clone)/AniRoot@queueanimation/room@btn";
        private const string FRIEND_TAB_BUTTON_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Full/LoginRoomPanel(Clone)/AniRoot/popup/content/background/tab_bg/tabBar@w/tab@list/Viewport/Content/friend@w/cell@btn";
        private const string ROOM_REFRESH_BUTTON_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Full/LoginRoomPanel(Clone)/AniRoot/popup/content/background/refresh@btn";
        private bool isRepairing = false;
        private int repairStep = 0;
        private float stepTimer = 0f;
        private int scrollAttempts = 0;
        private const int MAX_SCROLL_ATTEMPTS = 30;
        private bool isAutoEating = false;
        private int autoEatStep = 0;
        private float autoEatStepTimer = 0f;
        private int autoEatScrollAttempts = 0;

        // --- TARGET PATHS FOR PATROL ACTIONS ---
        private readonly string[] workPaths = new string[]
        {
            "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_cook_danger@list/CommonIconForCookDanger(Clone)/root_visible@go/icon@img@btn",
            "GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)/AniRoot@queueanimation/detail@t/btnBar@go/confirm@swapbtn",
            "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn",
            "GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)/AniRoot@go@ani/exit@btn@go"
        };

        // Settings/Keybinds Persistence
        private string GetKeybindsPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "keybinds.json");
        }

        private void SaveKeybinds()
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"keyToggleMenu\": {(int)this.keyToggleMenu},");
                sb.AppendLine($"  \"keyToggleRadar\": {(int)this.keyToggleRadar},");
                sb.AppendLine($"  \"keyAutoFarm\": {(int)this.keyAutoFarm},");
                sb.AppendLine($"  \"keyAutoCook\": {(int)this.keyAutoCook},");
                sb.AppendLine($"  \"keyBypassUI\": {(int)this.keyBypassUI},");
                sb.AppendLine($"  \"keyDisableAll\": {(int)this.keyDisableAll},");
                sb.AppendLine($"  \"keyInspectPlayer\": {(int)this.keyInspectPlayer},");
                sb.AppendLine($"  \"keyInspectMove\": {(int)this.keyInspectMove},");
                sb.AppendLine($"  \"keyJumpBoost\": {(int)this.keyJumpBoost},");
                sb.AppendLine($"  \"keyAutoRepair\": {(int)this.keyAutoRepair},");
                sb.AppendLine($"  \"keyAutoJoinFriend\": {(int)this.keyAutoJoinFriend},");
                sb.AppendLine($"  \"keyJoinPublic\": {(int)this.keyJoinPublic},");
                sb.AppendLine($"  \"keyJoinMyTown\": {(int)this.keyJoinMyTown},");
                sb.AppendLine($"  \"notificationsEnabled\": {(this.notificationsEnabled ? 1 : 0)},");
                sb.AppendLine($"  \"hideIdEnabled\": {(this.hideIdEnabled ? 1 : 0)}");
                sb.AppendLine("}");
                File.WriteAllText(this.GetKeybindsPath(), sb.ToString());
                MelonLogger.Msg("Keybinds Saved!");
                this.AddMenuNotification("Keybinds saved", new Color(0.55f, 0.88f, 1f));
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error Saving Keybinds: " + ex.Message);
                this.AddMenuNotification("Failed to save keybinds", new Color(1f, 0.4f, 0.4f));
            }
        }

        private void LoadKeybinds()
        {
            try
            {
                string path = this.GetKeybindsPath();
                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (string line in lines)
                    {
                        if (line.Contains("keyToggleMenu")) this.keyToggleMenu = (KeyCode)GetJsonInt(line, "\"keyToggleMenu\":");
                        else if (line.Contains("keyToggleRadar")) this.keyToggleRadar = (KeyCode)GetJsonInt(line, "\"keyToggleRadar\":");
                        else if (line.Contains("keyAutoFarm")) this.keyAutoFarm = (KeyCode)GetJsonInt(line, "\"keyAutoFarm\":");
                        else if (line.Contains("keyAutoCook")) this.keyAutoCook = (KeyCode)GetJsonInt(line, "\"keyAutoCook\":");
                        else if (line.Contains("keyBypassUI")) this.keyBypassUI = (KeyCode)GetJsonInt(line, "\"keyBypassUI\":");
                        else if (line.Contains("keyDisableAll")) this.keyDisableAll = (KeyCode)GetJsonInt(line, "\"keyDisableAll\":");
                        else if (line.Contains("keyInspectPlayer")) this.keyInspectPlayer = (KeyCode)GetJsonInt(line, "\"keyInspectPlayer\":");
                        else if (line.Contains("keyInspectMove")) this.keyInspectMove = (KeyCode)GetJsonInt(line, "\"keyInspectMove\":");
                        else if (line.Contains("keyJumpBoost")) this.keyJumpBoost = (KeyCode)GetJsonInt(line, "\"keyJumpBoost\":");
                        else if (line.Contains("keyAutoRepair")) this.keyAutoRepair = (KeyCode)GetJsonInt(line, "\"keyAutoRepair\":");
                        else if (line.Contains("keyAutoJoinFriend")) this.keyAutoJoinFriend = (KeyCode)GetJsonInt(line, "\"keyAutoJoinFriend\":");
                        else if (line.Contains("keyJoinPublic")) this.keyJoinPublic = (KeyCode)GetJsonInt(line, "\"keyJoinPublic\":");
                        else if (line.Contains("keyJoinMyTown")) this.keyJoinMyTown = (KeyCode)GetJsonInt(line, "\"keyJoinMyTown\":");
                        else if (line.Contains("notificationsEnabled")) this.notificationsEnabled = GetJsonInt(line, "\"notificationsEnabled\":") != 0;
                        else if (line.Contains("hideIdEnabled")) this.hideIdEnabled = GetJsonInt(line, "\"hideIdEnabled\":") != 0;
                    }
                    MelonLogger.Msg("Keybinds Loaded.");
                    this.AddMenuNotification("Keybinds loaded", new Color(0.55f, 0.88f, 1f));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error Loading Keybinds: " + ex.Message);
                this.AddMenuNotification("Failed to load keybinds", new Color(1f, 0.4f, 0.4f));
            }
        }
        
        private int GetJsonInt(string line, string key)
        {
            int startIdx = line.IndexOf(key);
            if (startIdx == -1) return 0;
            startIdx += key.Length;
            while (startIdx < line.Length && (line[startIdx] == ' ' || line[startIdx] == ':')) startIdx++;
            int endIdx = startIdx;
            while (endIdx < line.Length && line[endIdx] != ',' && line[endIdx] != '}') endIdx++;
            string valStr = line.Substring(startIdx, endIdx - startIdx).Trim();
            int result;
            if (int.TryParse(valStr, out result)) return result;
            return 0;
        }

        private void InvalidateThemeCache()
        {
            this.themeInitialized = false;
            this.themeWindowStyle = null;
            this.themePanelStyle = null;
            this.themeContentStyle = null;
            this.themeSidebarButtonStyle = null;
            this.themeSidebarButtonActiveStyle = null;
            this.themePrimaryButtonStyle = null;
            this.themeDangerButtonStyle = null;
            this.themeTopTabStyle = null;
            this.themeTopTabActiveStyle = null;
            this.uiCircleTexture = null;
            this.uiHueTexture = null;
            this.uiSvTexture = null;
            this.uiPickerHueCached = -1f;
            if (this.themeTextures.Count > 0)
            {
                foreach (Texture2D texture in this.themeTextures)
                {
                    if (texture != null)
                    {
                        Object.Destroy(texture);
                    }
                }
                this.themeTextures.Clear();
            }
        }

        private string GetUiThemePath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "ui_theme.json");
        }

        private void SaveUiTheme()
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"uiAccentR\": {this.uiAccentR.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiAccentG\": {this.uiAccentG.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiAccentB\": {this.uiAccentB.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiTextR\": {this.uiTextR.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiTextG\": {this.uiTextG.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiTextB\": {this.uiTextB.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiMainTabTextR\": {this.uiMainTabTextR.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiMainTabTextG\": {this.uiMainTabTextG.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiMainTabTextB\": {this.uiMainTabTextB.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiSubTabTextR\": {this.uiSubTabTextR.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiSubTabTextG\": {this.uiSubTabTextG.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiSubTabTextB\": {this.uiSubTabTextB.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiWindowR\": {this.uiWindowR.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiWindowG\": {this.uiWindowG.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiWindowB\": {this.uiWindowB.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiPanelR\": {this.uiPanelR.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiPanelG\": {this.uiPanelG.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiPanelB\": {this.uiPanelB.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiContentR\": {this.uiContentR.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiContentG\": {this.uiContentG.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiContentB\": {this.uiContentB.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiWindowAlpha\": {this.uiWindowAlpha.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiPanelAlpha\": {this.uiPanelAlpha.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"  \"uiContentAlpha\": {this.uiContentAlpha.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                sb.AppendLine("}");
                File.WriteAllText(this.GetUiThemePath(), sb.ToString());
                MelonLogger.Msg("UI Theme Saved.");
                this.AddMenuNotification("UI theme saved", new Color(0.55f, 0.88f, 1f));
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error Saving UI Theme: " + ex.Message);
                this.AddMenuNotification("Failed to save UI theme", new Color(1f, 0.4f, 0.4f));
            }
        }

        private void LoadUiTheme()
        {
            try
            {
                string path = this.GetUiThemePath();
                if (!File.Exists(path))
                {
                    return;
                }

                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    if (line.Contains("uiAccentR")) this.uiAccentR = GetJsonFloat(line, "\"uiAccentR\":");
                    else if (line.Contains("uiAccentG")) this.uiAccentG = GetJsonFloat(line, "\"uiAccentG\":");
                    else if (line.Contains("uiAccentB")) this.uiAccentB = GetJsonFloat(line, "\"uiAccentB\":");
                    else if (line.Contains("uiTextR")) this.uiTextR = GetJsonFloat(line, "\"uiTextR\":");
                    else if (line.Contains("uiTextG")) this.uiTextG = GetJsonFloat(line, "\"uiTextG\":");
                    else if (line.Contains("uiTextB")) this.uiTextB = GetJsonFloat(line, "\"uiTextB\":");
                    else if (line.Contains("uiMainTabTextR")) this.uiMainTabTextR = GetJsonFloat(line, "\"uiMainTabTextR\":");
                    else if (line.Contains("uiMainTabTextG")) this.uiMainTabTextG = GetJsonFloat(line, "\"uiMainTabTextG\":");
                    else if (line.Contains("uiMainTabTextB")) this.uiMainTabTextB = GetJsonFloat(line, "\"uiMainTabTextB\":");
                    else if (line.Contains("uiSubTabTextR")) this.uiSubTabTextR = GetJsonFloat(line, "\"uiSubTabTextR\":");
                    else if (line.Contains("uiSubTabTextG")) this.uiSubTabTextG = GetJsonFloat(line, "\"uiSubTabTextG\":");
                    else if (line.Contains("uiSubTabTextB")) this.uiSubTabTextB = GetJsonFloat(line, "\"uiSubTabTextB\":");
                    else if (line.Contains("uiWindowR")) this.uiWindowR = GetJsonFloat(line, "\"uiWindowR\":");
                    else if (line.Contains("uiWindowG")) this.uiWindowG = GetJsonFloat(line, "\"uiWindowG\":");
                    else if (line.Contains("uiWindowB")) this.uiWindowB = GetJsonFloat(line, "\"uiWindowB\":");
                    else if (line.Contains("uiPanelR")) this.uiPanelR = GetJsonFloat(line, "\"uiPanelR\":");
                    else if (line.Contains("uiPanelG")) this.uiPanelG = GetJsonFloat(line, "\"uiPanelG\":");
                    else if (line.Contains("uiPanelB")) this.uiPanelB = GetJsonFloat(line, "\"uiPanelB\":");
                    else if (line.Contains("uiContentR")) this.uiContentR = GetJsonFloat(line, "\"uiContentR\":");
                    else if (line.Contains("uiContentG")) this.uiContentG = GetJsonFloat(line, "\"uiContentG\":");
                    else if (line.Contains("uiContentB")) this.uiContentB = GetJsonFloat(line, "\"uiContentB\":");
                    else if (line.Contains("uiWindowAlpha")) this.uiWindowAlpha = GetJsonFloat(line, "\"uiWindowAlpha\":");
                    else if (line.Contains("uiPanelAlpha")) this.uiPanelAlpha = GetJsonFloat(line, "\"uiPanelAlpha\":");
                    else if (line.Contains("uiContentAlpha")) this.uiContentAlpha = GetJsonFloat(line, "\"uiContentAlpha\":");
                }

                this.uiAccentR = Mathf.Clamp01(this.uiAccentR);
                this.uiAccentG = Mathf.Clamp01(this.uiAccentG);
                this.uiAccentB = Mathf.Clamp01(this.uiAccentB);
                this.uiTextR = Mathf.Clamp01(this.uiTextR);
                this.uiTextG = Mathf.Clamp01(this.uiTextG);
                this.uiTextB = Mathf.Clamp01(this.uiTextB);
                this.uiMainTabTextR = Mathf.Clamp01(this.uiMainTabTextR);
                this.uiMainTabTextG = Mathf.Clamp01(this.uiMainTabTextG);
                this.uiMainTabTextB = Mathf.Clamp01(this.uiMainTabTextB);
                this.uiSubTabTextR = Mathf.Clamp01(this.uiSubTabTextR);
                this.uiSubTabTextG = Mathf.Clamp01(this.uiSubTabTextG);
                this.uiSubTabTextB = Mathf.Clamp01(this.uiSubTabTextB);
                this.uiWindowR = Mathf.Clamp01(this.uiWindowR);
                this.uiWindowG = Mathf.Clamp01(this.uiWindowG);
                this.uiWindowB = Mathf.Clamp01(this.uiWindowB);
                this.uiPanelR = Mathf.Clamp01(this.uiPanelR);
                this.uiPanelG = Mathf.Clamp01(this.uiPanelG);
                this.uiPanelB = Mathf.Clamp01(this.uiPanelB);
                this.uiContentR = Mathf.Clamp01(this.uiContentR);
                this.uiContentG = Mathf.Clamp01(this.uiContentG);
                this.uiContentB = Mathf.Clamp01(this.uiContentB);
                this.uiWindowAlpha = Mathf.Clamp(this.uiWindowAlpha, 0.15f, 1f);
                this.uiPanelAlpha = Mathf.Clamp(this.uiPanelAlpha, 0.15f, 1f);
                this.uiContentAlpha = Mathf.Clamp(this.uiContentAlpha, 0.15f, 1f);

                this.InvalidateThemeCache();
                this.uiThemeHexInput = this.ColorToHex(this.GetUiThemeColorTargetValue(this.uiThemeColorTarget));
                MelonLogger.Msg("UI Theme Loaded.");
                this.AddMenuNotification("UI theme loaded", new Color(0.55f, 0.88f, 1f));
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error Loading UI Theme: " + ex.Message);
                this.AddMenuNotification("Failed to load UI theme", new Color(1f, 0.4f, 0.4f));
            }
        }

        public override void OnInitializeMelon()
        {
            HeartopiaComplete.Instance = this;
            HeartopiaComplete.harmonyInstance = new HarmonyLib.Harmony("com.heartopia.teleport");
            MelonLogger.Msg("Heartopia Helper initialized!");
            this.LoadCustomTeleports();
            this.LoadKeybinds();
            this.LoadUiTheme();
            this.LoadPatrolPoints();
            MelonLogger.Msg("=== Attempting Harmony Patches ===");
            try
            {
                MethodInfo method = typeof(CharacterController).GetMethod("Move", new Type[]
                {
                    typeof(Vector3)
                });
                MethodInfo method2 = typeof(CharacterControllerPatch).GetMethod("MovePrefix");
                bool flag = method != null && method2 != null;
                if (flag)
                {
                    HeartopiaComplete.harmonyInstance.Patch(method, new HarmonyMethod(method2), null, null, null, null);
                    MelonLogger.Msg("✓ Successfully patched CharacterController.Move!");
                }
                else
                {
                    MelonLogger.Msg($"✗ Failed to find methods - ccMove: {method != null}, prefix: {method2 != null}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("✗ CharacterController.Move patch failed: " + ex.Message);
            }
            try
            {
                MethodInfo setMethod = typeof(Transform).GetProperty("position").GetSetMethod();
                MethodInfo method3 = typeof(TransformPositionPatch).GetMethod("SetPositionPrefix");
                bool flag2 = setMethod != null && method3 != null;
                if (flag2)
                {
                    HeartopiaComplete.harmonyInstance.Patch(setMethod, new HarmonyMethod(method3), null, null, null, null);
                    MelonLogger.Msg("✓ Successfully patched Transform.position setter!");
                }
                else
                {
                    MelonLogger.Msg($"✗ Failed to find methods - setPos: {setMethod != null}, prefix: {method3 != null}");
                }
            }
            catch (Exception ex2)
            {
                MelonLogger.Msg("✗ Transform.position patch failed: " + ex2.Message);
            }
            try
            {
                MethodInfo setMethod2 = typeof(Transform).GetProperty("rotation").GetSetMethod();
                MethodInfo method4 = typeof(TransformRotationPatch).GetMethod("SetRotationPrefix");
                bool flag3 = setMethod2 != null && method4 != null;
                if (flag3)
                {
                    HeartopiaComplete.harmonyInstance.Patch(setMethod2, new HarmonyMethod(method4), null, null, null, null);
                    MelonLogger.Msg("✓ Successfully patched Transform.rotation setter!");
                }
                else
                {
                    MelonLogger.Msg($"✗ Failed to find rotation methods - setRot: {setMethod2 != null}, prefix: {method4 != null}");
                }
            }
            catch (Exception ex3)
            {
                MelonLogger.Msg("✗ Transform.rotation patch failed: " + ex3.Message);
            }
            try
            {
                MethodInfo setMethod3 = typeof(Transform).GetProperty("rotation").GetSetMethod();
                MethodInfo method5 = typeof(CharacterRotationPatch).GetMethod("SetRotationPrefix");
                bool flag5 = setMethod3 != null && method5 != null;
                if (flag5)
                {
                    HeartopiaComplete.harmonyInstance.Patch(setMethod3, new HarmonyMethod(method5), null, null, null, null);
                    MelonLogger.Msg("✓ Successfully patched Transform.rotation setter for Character Rotation!");
                }
                else
                {
                    MelonLogger.Msg($"✗ Failed to find character rotation methods - setRot: {setMethod3 != null}, prefix: {method5 != null}");
                }
            }
            catch (Exception ex4)
            {
                MelonLogger.Msg("✗ Character rotation patch failed: " + ex4.Message);
            }
            try
            {
                MethodInfo spriteSetMethod = typeof(UnityEngine.UI.Image).GetProperty("sprite").GetSetMethod();
                MethodInfo spritePatchMethod = typeof(SpriteDetectionPatch).GetMethod("Postfix");
                bool flag4 = spriteSetMethod != null && spritePatchMethod != null;
                if (flag4)
                {
                    HeartopiaComplete.harmonyInstance.Patch(spriteSetMethod, null, new HarmonyMethod(spritePatchMethod), null, null, null);
                    MelonLogger.Msg("✓ Successfully patched Image.sprite setter for Bulk Selector!");
                }
                else
                {
                    MelonLogger.Msg($"✗ Failed to find sprite methods - setter: {spriteSetMethod != null}, patch: {spritePatchMethod != null}");
                }
            }
            catch (Exception ex4)
            {
                MelonLogger.Msg("✗ Image.sprite patch failed: " + ex4.Message);
            }
            MelonLogger.Msg("=== Patch Attempt Complete ===");
        }

        // Token: 0x06000004 RID: 4 RVA: 0x00002390 File Offset: 0x00000590
        public override void OnLateUpdate()
        {
            bool flag = this.monitorPosition;
            if (flag)
            {
                GameObject gameObject = GameObject.Find("p_player_skeleton(Clone)");
                bool flag2 = gameObject != null;
                if (flag2)
                {
                    Vector3 position = gameObject.transform.position;
                    bool flag3 = Vector3.Distance(position, this.lastKnownPosition) > 0.01f;
                    if (flag3)
                    {
                        MelonLogger.Msg($"[POSITION CHANGED] From {this.lastKnownPosition} to {position}");
                        this.lastKnownPosition = position;
                    }
                }
            }
            bool flag4 = HeartopiaComplete.OverrideCameraPosition && this.cameraOverrideFramesRemaining > 0;
            if (flag4)
            {
                GameObject gameObject2 = GameObject.Find("GameApp/startup_root(Clone)/Main Camera");
                bool flag5 = gameObject2 != null;
                if (flag5)
                {
                    gameObject2.transform.position = HeartopiaComplete.CameraOverridePos;
                    gameObject2.transform.rotation = HeartopiaComplete.CameraOverrideRot;
                }
                this.cameraOverrideFramesRemaining--;
                bool flag6 = this.cameraOverrideFramesRemaining <= 0;
                if (flag6)
                {
                    HeartopiaComplete.OverrideCameraPosition = false;
                }
            }
        }

        // Token: 0x06000005 RID: 5 RVA: 0x000024C0 File Offset: 0x000006C0
        public override void OnUpdate()
        {
            this.UpdateGameUiClickBlockState();
            bool flag2 = HeartopiaComplete.OverridePlayerPosition && this.teleportFramesRemaining > 0;
            if (flag2)
            {
                this.teleportFramesRemaining--;
                bool flag3 = this.teleportFramesRemaining <= 0;
                if (flag3)
                {
                    HeartopiaComplete.OverridePlayerPosition = false;
                }
            }
            this.SyncTeleportPosition();

            // Handle player rotation override
            bool flagRotation = HeartopiaComplete.OverridePlayerRotation && this.playerRotationFramesRemaining > 0;
            if (flagRotation)
            {
                this.playerRotationFramesRemaining--;
                GameObject player = GetPlayer();
                if (player != null)
                {
                    player.transform.rotation = HeartopiaComplete.PlayerOverrideRot;
                }
                if (this.playerRotationFramesRemaining <= 0)
                {
                    HeartopiaComplete.OverridePlayerRotation = false;
                }
            }
            
            // Check for keybinds (Only if not currently rebinding)
            if (string.IsNullOrEmpty(this.keyBindingActive))
            {
                if (Input.GetKeyDown(this.keyToggleMenu))
                {
                    this.showMenu = !this.showMenu;
                }
                if (Input.GetKeyDown(this.keyToggleRadar))
                {
                    this.ToggleRadar();
                    this.AddMenuNotification($"Radar {(this.isRadarActive ? "Enabled" : "Disabled")}", this.isRadarActive ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyAutoFarm))
                {
                    this.autoFarmEnabled = !this.autoFarmEnabled;
                    MelonLogger.Msg("Auto Collect " + (this.autoFarmEnabled ? "Enabled" : "Disabled"));
                    this.AddMenuNotification($"Auto Farm {(this.autoFarmEnabled ? "Enabled" : "Disabled")}", this.autoFarmEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyAutoCook))
                {
                    if (this.autoCookEnabled)
                    {
                        this.StopAutoCookInternal("Disabled");
                    }
                    else
                    {
                        this.StartAutoCookInternal();
                    }
                }
                if (Input.GetKeyDown(this.keyBypassUI))
                {
                    this.bypassEnabled = !this.bypassEnabled;
                    MelonLogger.Msg("Bypass UI/Skeleton " + (this.bypassEnabled ? "Enabled" : "Disabled"));
                    this.RunBypassLogic(this.bypassEnabled);
                    this.AddMenuNotification($"Bypass UI {(this.bypassEnabled ? "Enabled" : "Disabled")}", this.bypassEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyDisableAll))
                {
                    this.autoFarmEnabled = false;
                    this.bypassEnabled = false;
                    this.StopAutoCookInternal("Disabled");
                    this.pendingToolEquipType = 0;
                    this.isAutoEating = false;
                    this.StopTreeFarm("Stopped");
                    this.gameSpeed = 1f;
                    Time.timeScale = 1f;
                    MelonLogger.Msg("All features disabled and game speed reset");
                    this.AddMenuNotification("All features disabled", new Color(1f, 0.55f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyInspectPlayer))
                {
                    this.InspectPlayerComponents();
                }
                if (Input.GetKeyDown(this.keyInspectMove))
                {
                    this.InspectMovementComponent();
                }
                if (Input.GetKeyDown(this.keyAutoRepair))
                {
                    if (!this.isRepairing && !this.isAutoEating)
                    {
                        this.StartRepair();
                        this.AddMenuNotification("Auto Repair started", new Color(0.45f, 1f, 0.55f));
                    }
                    else
                    {
                        this.AddMenuNotification("Auto Repair already running", new Color(1f, 0.55f, 0.55f));
                    }
                }
                if (Input.GetKeyDown(this.keyAutoJoinFriend))
                {
                    this.StartLobbyAutoJoinFriend("Hotkey triggered");
                }
                if (Input.GetKeyDown(this.keyJoinPublic))
                {
                    this.autoJoinFriendEnabled = false;
                    this.autoClickStartEnabled = false;
                    bool success = this.ClickButtonIfExistsReturn(START_GAME_BUTTON_PATH);
                    this.AddMenuNotification($"Join Public: {(success ? "Clicked" : "Button not found")}", success ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyJoinMyTown))
                {
                    this.StartLobbyAutoJoinMyTown("Hotkey triggered");
                }
            }

            // Check for durability notification
            if (Time.time - lastNotificationCheck > NOTIFICATION_CHECK_INTERVAL)
            {
                lastNotificationCheck = Time.time;
                if (CheckForDurabilityNotification())
                {
                    if (!this.isRepairing && !this.isAutoEating)
                    {
                        this.StartRepair();
                        this.AddMenuNotification("Auto Repair triggered by durability notification", new Color(0.45f, 1f, 0.55f));
                    }
                }
            }

            // Update ID display
            this.UpdateIdDisplay();

            if (this.isRepairing && Time.time >= this.stepTimer)
            {
                this.ExecuteRepairStep();
            }
            if (this.isAutoEating && Time.time >= this.autoEatStepTimer)
            {
                this.ExecuteAutoEatStep();
            }

            // Meteor Scan (Every 60 frames)
            if (Time.frameCount % 60 == 0)
            {
                this.ScanMeteorites();
            }
            // NEW: Apply Camera FOV continuously
            this.ApplyCameraFOV();

            // NEW: Apply Jump Height Multiplier
            this.ApplyJumpHeight();
            bool flag4 = Math.Abs(Time.timeScale - this.gameSpeed) > 0.05f;
            if (flag4)
            {
                Time.timeScale = this.gameSpeed;
            }
            bool flag5 = this.autoFarmEnabled && Time.time > this.nextFarmTime;
            if (flag5)
            {
                this.nextFarmTime = Time.time + this.farmPeriod;
                this.RunAutoCollectLogic();
            }
            // Advanced Auto Cook with Player Detection
            bool flag6 = this.autoCookEnabled;
            if (flag6)
            {
                // Check for nearby players (if enabled)
                if (this.enablePlayerDetection && !this.cookingCleanupMode)
                {
                    float nearestPlayer = this.GetNearestPlayerDistance();
                    if (nearestPlayer < cookingPlayerAlertRadius)
                    {
                        this.cookingCleanupMode = true;
                        MelonLogger.Msg($"[Cooking] ⚠️ PLAYER DETECTED ({nearestPlayer:F0}m) - Starting cleanup!");
                    }
                }

                // Patrol coroutine already drives cook interactions; avoid double-running in OnUpdate.
                if (!this.isCookingPatrolActive && this.autoCookEnabled && Time.time >= this.nextCookTime)
                {
                    if (this.cookingCleanupMode)
                    {
                        // Cleanup mode
                        bool hasCleanupItems = this.ClickCookingCleanupThrottled(0.18f);

                        if (!hasCleanupItems)
                        {
                            // Cleanup complete - stop everything
                            this.StopAutoCookInternal("Stopped (cleanup complete)");
                        }

                        this.nextCookTime = Time.time + 0.18f; // Less aggressive to reduce long-run pressure
                    }
                    else
                    {
                        this.ClickCookConfirmButtonIfAvailable();
                        this.ClickCookDangerButtonIfAvailable();
                        this.ClickCookingCleanupThrottled(0.45f);
                        this.nextCookTime = Time.time + 0.2f;
                    }
                }
            }
            this.ProcessPendingToolEquipRequest();
            this.RunTreeFarmLogic();
            this.RunLobbyAutoActions();
            this.RunBypassLogic(this.bypassEnabled);
            bool flag7 = this.birdVacuumEnabled;
            if (flag7)
            {
                this.VacuumBirds();
            }
            this.CheckManualBlueberryCollection();
            this.CheckManualRaspberryCollection();
            bool flag8 = this.isRadarActive;
            if (flag8)
            {
                bool flag9 = Time.unscaledTime - this.lastScanTime > 2f;
                if (flag9)
                {
                    this.RunRadar();
                    this.lastScanTime = Time.unscaledTime;
                }
                this.UpdateMarkers();
                if (this.isRadarActive)
                {
                    this.CleanupExpiredCooldowns();
                }
            }
            bool flag10 = this.autoFarmActive;
            if (flag10)
            {
                this.RunAutoFarmLogic();
            }
        }

        // Token: 0x06000006 RID: 6 RVA: 0x000027FC File Offset: 0x000009FC
        public override void OnGUI()
        {
            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            this.EnsureThemeStyles();

            // --- METEOR ESP DRAWING ---
            if (this.showMeteorESP)
            {
                GUI.backgroundColor = new Color(1f, 0.6f, 0f);
                GUI.color = Color.white; // White Text
                GUI.Box(new Rect((float)Screen.width / 2f - 75f, 20f, 150f, 25f), $"Meteors Found: {this.meteorList.Count}");
                GUI.backgroundColor = Color.white;

                foreach (var meteor in this.meteorList)
                {
                    if (meteor == null) continue;

                    Vector3 screenPos = Camera.main.WorldToScreenPoint(meteor.transform.position);

                    // Only draw if the meteorite is in front of the camera
                    if (screenPos.z > 0)
                    {
                        float distance = Vector3.Distance(Camera.main.transform.position, meteor.transform.position);

                        // Draw a simple box at the meteorite location
                        GUI.color = new Color(1f, 0.6f, 0f);
                        // Use screenPos.y but inverted for GUI coords
                        Rect boxRect = new Rect(screenPos.x - 25f, (float)Screen.height - screenPos.y - 25f, 50f, 50f);
                        GUI.Box(boxRect, "");

                        // Distance label
                        GUI.color = Color.white;
                        GUI.Label(new Rect(screenPos.x - 40f, (float)Screen.height - screenPos.y - 45f, 100f, 20f), $"Meteor [{distance:F0}m]");
                    }
                }
            }
            // Reset Color
            GUI.color = Color.white;

            bool flag = !this.showMenu;
            if (flag)
            {
                this.wasMouseOverMenuLastFrame = false;
            }
            else
            {
                this.targetWindowWidth = 1060f;
                this.targetWindowHeight = 680f;

                // Auto - Resize Window Height & Width
                if (Mathf.Abs(this.windowRect.height - this.targetWindowHeight) > 1f)
                {
                    this.windowRect.height = Mathf.Lerp(this.windowRect.height, this.targetWindowHeight, Time.unscaledDeltaTime * 10f);
                }
                if (Mathf.Abs(this.windowRect.width - this.targetWindowWidth) > 1f)
                {
                    this.windowRect.width = Mathf.Lerp(this.windowRect.width, this.targetWindowWidth, Time.unscaledDeltaTime * 10f);
                }

                Color prevColor = GUI.color;
                Color prevBg = GUI.backgroundColor;
                Color prevContent = GUI.contentColor;
                try
                {
                    GUI.color = Color.white;
                    GUI.backgroundColor = Color.white;
                    GUI.contentColor = Color.white;
                    this.windowRect = GUI.Window(0, this.windowRect, (GUI.WindowFunction)this.DrawWindow, "Heartopia Helper", this.themeWindowStyle ?? GUI.skin.window);
                }
                finally
                {
                    GUI.color = prevColor;
                    GUI.backgroundColor = prevBg;
                    GUI.contentColor = prevContent;
                }
                Vector2 vector = new Vector2(Input.mousePosition.x, (float)Screen.height - Input.mousePosition.y);
                this.wasMouseOverMenuLastFrame = this.windowRect.Contains(vector);
                bool flag2 = this.wasMouseOverMenuLastFrame;
                if (flag2)
                {
                    Event current = Event.current;
                    bool flag3 = current != null && current.isMouse;
                    if (flag3)
                    {
                        current.Use();
                    }
                }
            }

            this.DrawMenuNotifications(new Rect((float)Screen.width - 280f, 14f, 260f, (float)Screen.height - 20f));
        }

        private void DrawWindow(int windowID)
        {
            this.targetWindowWidth = 1060f;
            this.targetWindowHeight = 680f;
            this.windowRect.width = 1060f;
            this.windowRect.height = 680f;
            GUI.DragWindow(new Rect(0f, 0f, this.windowRect.width, 28f));

            Rect chromeRect = new Rect(18f, 36f, this.windowRect.width - 36f, this.windowRect.height - 54f);
            GUI.Box(chromeRect, "", this.themeContentStyle ?? GUI.skin.box);
            this.DrawCardOutline(chromeRect, 1f);

            Rect leftNavRect = new Rect(30f, 52f, 210f, 592f);
            Rect logoRect = new Rect(leftNavRect.x + 12f, leftNavRect.y + 12f, leftNavRect.width - 24f, 68f);
            Rect navListRect = new Rect(leftNavRect.x + 12f, leftNavRect.y + 92f, leftNavRect.width - 24f, leftNavRect.height - 104f);
            Rect workspaceRect = new Rect(252f, 52f, 778f, 592f);
            Rect titleRect = new Rect(workspaceRect.x + 12f, workspaceRect.y + 12f, workspaceRect.width - 24f, 58f);

            GUI.Box(leftNavRect, "", this.themePanelStyle ?? GUI.skin.box);
            GUI.Box(logoRect, "", this.themeTopTabStyle ?? this.themePanelStyle ?? GUI.skin.box);
            GUI.Box(workspaceRect, "", this.themePanelStyle ?? GUI.skin.box);
            GUI.Box(titleRect, "", this.themeTopTabStyle ?? this.themePanelStyle ?? GUI.skin.box);
            this.DrawCardOutline(leftNavRect, 1f);
            this.DrawCardOutline(logoRect, 1f);
            this.DrawCardOutline(workspaceRect, 1f);
            this.DrawCardOutline(titleRect, 1f);

            GUIStyle logoStyle = new GUIStyle(GUI.skin.label);
            logoStyle.fontSize = 18;
            logoStyle.fontStyle = FontStyle.Bold;
            logoStyle.alignment = TextAnchor.MiddleCenter;
            logoStyle.normal.textColor = new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB);
            GUI.Label(logoRect, "HEARTOPIA", logoStyle);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 32;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.normal.textColor = new Color(this.uiTextR, this.uiTextG, this.uiTextB);
            GUI.Label(new Rect(titleRect.x + 16f, titleRect.y + 2f, titleRect.width - 32f, 54f), $"[{this.GetSelectedTabHeader()}]", titleStyle);

            var subTabs = this.GetActiveTopSubTabs();
            Rect subTabStrip = new Rect(workspaceRect.x + 12f, workspaceRect.y + 78f, workspaceRect.width - 24f, subTabs.Count > 1 ? 46f : 0f);
            if (subTabs.Count > 1)
            {
                GUI.Box(subTabStrip, "", this.themeTopTabStyle ?? this.themePanelStyle ?? GUI.skin.box);
                this.DrawCardOutline(subTabStrip, 1f);
                GUIStyle weaponStyle = new GUIStyle(GUI.skin.label);
                weaponStyle.fontSize = 13;
                weaponStyle.fontStyle = FontStyle.Bold;
                weaponStyle.alignment = TextAnchor.MiddleCenter;
                weaponStyle.normal.textColor = new Color(this.uiSubTabTextR, this.uiSubTabTextG, this.uiSubTabTextB);
                GUIStyle weaponActiveStyle = new GUIStyle(weaponStyle);
                weaponActiveStyle.normal.textColor = new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB);

                float tabW = Mathf.Min(115f, (subTabStrip.width - 18f) / Mathf.Max(1f, subTabs.Count));
                for (int i = 0; i < subTabs.Count; i++)
                {
                    var tab = subTabs[i];
                    Rect tRect = new Rect(subTabStrip.x + 9f + i * tabW, subTabStrip.y + 6f, tabW - 8f, 34f);
                    bool active = tab.isActive();
                    GUI.Box(tRect, "", active ? (this.themeTopTabActiveStyle ?? this.themePanelStyle ?? GUI.skin.box) : (this.themeTopTabStyle ?? this.themePanelStyle ?? GUI.skin.box));
                    this.DrawCardOutline(tRect, 1f);
                    if (GUI.Button(tRect, "", GUIStyle.none))
                    {
                        tab.setActive();
                    }
                    GUI.Label(tRect, tab.label, active ? weaponActiveStyle : weaponStyle);
                }
            }

            GUILayout.BeginArea(navListRect);
            GUILayout.BeginVertical();
            this.DrawSidebarTabButton("Auto Farm", 0);
            this.DrawSidebarTabButton("Automation", 1);
            this.DrawSidebarTabButton("Auto Cook", 2);
            this.DrawSidebarTabButton("Radar", 3);
            this.DrawSidebarTabButton("Teleport", 4);
            this.DrawSidebarTabButton("Items Selector", 5);
            this.DrawSidebarTabButton("Settings", 6);
            GUILayout.EndVertical();
            GUILayout.EndArea();

            GUIStyle sectionStyle = new GUIStyle(GUI.skin.label);
            sectionStyle.fontSize = 17;
            sectionStyle.fontStyle = FontStyle.Bold;
            sectionStyle.normal.textColor = new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB);

            float bodyTop = workspaceRect.y + (subTabs.Count > 1 ? 132f : 86f);
            Rect leftContentCard = new Rect(workspaceRect.x + 12f, bodyTop, 486f, workspaceRect.yMax - bodyTop - 12f);
            Rect rightInfoCard = new Rect(workspaceRect.x + 508f, bodyTop, 258f, workspaceRect.yMax - bodyTop - 12f);
            GUI.Box(leftContentCard, "", this.themeContentStyle ?? GUI.skin.box);
            GUI.Box(rightInfoCard, "", this.themeContentStyle ?? GUI.skin.box);
            this.DrawCardOutline(leftContentCard, 1f);
            this.DrawCardOutline(rightInfoCard, 1f);
            GUI.Label(new Rect(rightInfoCard.x + 12f, rightInfoCard.y + 10f, 220f, 30f), "STATUS", sectionStyle);

            Rect tabDrawRect = new Rect(leftContentCard.x + 6f, leftContentCard.y + 34f, leftContentCard.width - 12f, leftContentCard.height - 40f);
            GUI.BeginGroup(tabDrawRect);
            try
            {
                float contentHeight = this.GetSelectedTabEstimatedHeight();
                float contentWidth = tabDrawRect.width - 18f;
                this.tabScrollPos = GUI.BeginScrollView(new Rect(0f, 0f, tabDrawRect.width, tabDrawRect.height), this.tabScrollPos, new Rect(0f, 0f, contentWidth, contentHeight));
                float calculatedHeight = 500f;
                int contentY = 10;
                if (this.selectedTab == 0) calculatedHeight = this.DrawAutoFarmTab(contentY);
                else if (this.selectedTab == 1) calculatedHeight = this.DrawAutomationTab(contentY);
                else if (this.selectedTab == 2) calculatedHeight = this.DrawAutoCookTab(contentY);
                else if (this.selectedTab == 3) calculatedHeight = this.DrawRadarTab(contentY);
                else if (this.selectedTab == 4) calculatedHeight = this.DrawTeleportTab(contentY);
                else if (this.selectedTab == 5) calculatedHeight = this.DrawBulkSelectorTab(contentY);
                else if (this.selectedTab == 6) calculatedHeight = this.DrawSettingsTab(contentY);
                GUI.EndScrollView();
            }
            finally
            {
                GUI.EndGroup();
            }

            this.DrawQuickStatusPanel(rightInfoCard);
        }

        private void DrawCardOutline(Rect rect, float thickness = 1f)
        {
            Color prev = GUI.color;
            float alpha = Mathf.Clamp(0.2f + (this.uiPanelAlpha * 0.35f), 0.18f, 0.55f);
            Color border = new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB, alpha);

            GUI.color = border;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private float GetSelectedTabEstimatedHeight()
        {
            if (this.selectedTab == 0)
            {
                if (this.autoFarmSubTab == 1) return 780f;
                return 1200f;
            }
            if (this.selectedTab == 1) return 760f;
            if (this.selectedTab == 2) return 880f;
            if (this.selectedTab == 3) return 900f;
            if (this.selectedTab == 4)
            {
                if (this.teleportSubTab == 0) return 620f;
                if (this.teleportSubTab == 1) return 80f + (this.animalCareLocations.Length * 45f);
                if (this.teleportSubTab == 2) return 80f + (this.npcLocations.Count * 45f);
                if (this.teleportSubTab == 3) return 80f + (this.fastTravelLocations.Count * 45f);
                if (this.teleportSubTab == 4) return 80f + (this.eventLocations.Count * 45f);
                if (this.teleportSubTab == 5) return 80f + (this.houseLocations.Length * 45f);
                if (this.teleportSubTab == 6) return 180f + (this.customTeleportList.Count * 38f);
                return 420f; // XYZ
            }
            if (this.selectedTab == 5) return 780f;
            if (this.selectedTab == 6 && this.settingsSubTab == 2)
            {
                return this.uiThemePickerOpen ? 1180f : 860f;
            }
            return 740f;
        }

        private void DrawSidebarTabButton(string label, int tabIndex)
        {
            GUIStyle style = this.selectedTab == tabIndex
                ? (this.themeSidebarButtonActiveStyle ?? GUI.skin.button)
                : (this.themeSidebarButtonStyle ?? GUI.skin.button);

            string drawLabel = this.selectedTab == tabIndex ? ("> " + label) : label;
            if (GUILayout.Button(drawLabel, style))
            {
                if (this.selectedTab != tabIndex)
                {
                    this.selectedTab = tabIndex;
                    this.tabScrollPos = Vector2.zero;
                }
            }
        }

        private bool DrawPrimaryActionButton(Rect rect, string label)
        {
            return GUI.Button(rect, label, this.themePrimaryButtonStyle ?? GUI.skin.button);
        }

        private bool DrawDangerActionButton(Rect rect, string label)
        {
            return GUI.Button(rect, label, this.themeDangerButtonStyle ?? GUI.skin.button);
        }

        private bool DrawSwitchToggle(Rect rect, bool value, string label)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width - 56f, rect.height), label);

            this.EnsureUiPrimitiveTextures();
            Rect switchRect = new Rect(rect.xMax - 42f, rect.y + Mathf.Max(0f, (rect.height - 18f) * 0.5f), 36f, 18f);
            Rect trackRect = new Rect(switchRect.x, switchRect.y + 1f, switchRect.width, 16f);
            Color accent = new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB, 0.98f);
            Color offTrack = new Color(0.09f, 0.11f, 0.14f, 0.96f);
            Color onTrack = new Color(accent.r * 0.9f, accent.g * 0.9f, accent.b, 1f);

            this.DrawCapsule(trackRect, value ? onTrack : offTrack);

            float knobDiameter = 12f;
            float knobX = value ? (trackRect.xMax - knobDiameter - 2f) : (trackRect.x + 2f);
            Rect knobRect = new Rect(knobX, trackRect.y + (trackRect.height - knobDiameter) * 0.5f, knobDiameter, knobDiameter);
            Color knobColor = value ? new Color(0.05f, 0.06f, 0.08f, 1f) : accent;
            GUI.color = knobColor;
            GUI.DrawTexture(knobRect, this.uiCircleTexture);
            GUI.color = Color.white;

            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                value = !value;
                e.Use();
            }

            return value;
        }

        private float DrawAccentSlider(Rect rect, float value, float min, float max)
        {
            Event e = Event.current;
            if (e != null && e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && rect.Contains(e.mousePosition))
            {
                float tInput = Mathf.Clamp01((e.mousePosition.x - rect.x) / Mathf.Max(1f, rect.width));
                value = Mathf.Lerp(min, max, tInput);
                e.Use();
            }

            float t = Mathf.InverseLerp(min, max, value);
            float lineY = rect.y + rect.height * 0.5f - 2f;
            Rect bgRect = new Rect(rect.x, lineY, rect.width, 4f);
            Rect fillRect = new Rect(rect.x, lineY, rect.width * t, 4f);
            float thumbX = rect.x + rect.width * t - 4f;
            Rect thumbRect = new Rect(thumbX, rect.y + rect.height * 0.5f - 7f, 8f, 14f);

            GUI.color = new Color(0.11f, 0.13f, 0.16f, 0.95f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);
            GUI.color = new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB, 0.95f);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.95f, 0.97f, 1f, 1f);
            GUI.DrawTexture(thumbRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            return Mathf.Clamp(value, min, max);
        }

        private void EnsureUiPrimitiveTextures()
        {
            if (this.uiCircleTexture != null)
            {
                return;
            }

            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float radius = (size - 1f) * 0.5f;
            Vector2 c = new Vector2(radius, radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    float a = d <= radius ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            this.uiCircleTexture = tex;
            this.themeTextures.Add(tex);
        }

        private void DrawCapsule(Rect rect, Color color)
        {
            this.EnsureUiPrimitiveTextures();
            float r = rect.height * 0.5f;
            Rect mid = new Rect(rect.x + r, rect.y, rect.width - 2f * r, rect.height);
            Rect left = new Rect(rect.x, rect.y, rect.height, rect.height);
            Rect right = new Rect(rect.xMax - rect.height, rect.y, rect.height, rect.height);
            GUI.color = color;
            GUI.DrawTexture(mid, Texture2D.whiteTexture);
            GUI.DrawTexture(left, this.uiCircleTexture);
            GUI.DrawTexture(right, this.uiCircleTexture);
            GUI.color = Color.white;
        }

        private string GetSelectedTabHeader()
        {
            if (this.selectedTab == 0) return "Auto Farm";
            if (this.selectedTab == 1) return "Automation";
            if (this.selectedTab == 2) return "Auto Cook";
            if (this.selectedTab == 3) return "Radar";
            if (this.selectedTab == 4) return "Teleport";
            if (this.selectedTab == 5) return "Items Selector";
            if (this.selectedTab == 6) return "Settings";
            return "Unknown";
        }
    

        private void DrawQuickStatusPanel(Rect panelRect)
        {
            GUIStyle title = new GUIStyle(GUI.skin.label);
            title.fontSize = 14;
            title.fontStyle = FontStyle.Bold;
            title.normal.textColor = new Color(this.uiTextR, this.uiTextG, this.uiTextB);

            GUIStyle value = new GUIStyle(GUI.skin.label);
            value.fontSize = 13;
            value.normal.textColor = new Color(this.uiSubTabTextR, this.uiSubTabTextG, this.uiSubTabTextB);

            float y = panelRect.y + 46f;
            GUI.Label(new Rect(panelRect.x + 14f, y, panelRect.width - 28f, 24f), "Tab", title);
            y += 20f;
            GUI.Label(new Rect(panelRect.x + 14f, y, panelRect.width - 28f, 24f), this.GetSelectedTabHeader(), value);

            y += 32f;
            GUI.Label(new Rect(panelRect.x + 14f, y, panelRect.width - 28f, 24f), "Radar", title);
            y += 20f;
            GUI.Label(new Rect(panelRect.x + 14f, y, panelRect.width - 28f, 24f), this.isRadarActive ? "Enabled" : "Disabled", value);

            y += 32f;
            GUI.Label(new Rect(panelRect.x + 14f, y, panelRect.width - 28f, 24f), "Auto Farm", title);
            y += 20f;
            GUI.Label(new Rect(panelRect.x + 14f, y, panelRect.width - 28f, 24f), this.autoFarmActive ? "Running" : "Idle", value);

            y += 32f;
            GUI.Label(new Rect(panelRect.x + 14f, y, panelRect.width - 28f, 24f), "Auto Cook", title);
            y += 20f;
            GUI.Label(new Rect(panelRect.x + 14f, y, panelRect.width - 28f, 24f), this.autoCookEnabled ? "Running" : "Idle", value);

            y += 32f;
            GUI.Label(new Rect(panelRect.x + 14f, y, panelRect.width - 28f, 24f), "Speed", title);
            y += 20f;
            GUI.Label(new Rect(panelRect.x + 14f, y, panelRect.width - 28f, 24f), $"{this.gameSpeed:F1}x", value);
        }

        private void AddMenuNotification(string message, Color color, float duration = 5f)
        {
            if (!this.notificationsEnabled)
            {
                return;
            }
            this.menuNotifications.Add(new HeartopiaComplete.MenuNotification
            {
                Message = message,
                Color = color,
                CreatedAt = Time.unscaledTime,
                ExpireAt = Time.unscaledTime + duration,
                Duration = Mathf.Max(0.1f, duration)
            });
            if (this.menuNotifications.Count > 6)
            {
                this.menuNotifications.RemoveAt(0);
            }
        }

        private void DrawMenuNotifications(Rect area)
        {
            if (!this.notificationsEnabled)
            {
                return;
            }
            float now = Time.unscaledTime;
            this.menuNotifications.RemoveAll(n => n == null || n.ExpireAt <= now);
            if (this.menuNotifications.Count == 0)
            {
                return;
            }

            float y = area.y;
            for (int i = this.menuNotifications.Count - 1; i >= 0; i--)
            {
                HeartopiaComplete.MenuNotification item = this.menuNotifications[i];
                float remain = Mathf.Clamp01((item.ExpireAt - now) / item.Duration);

                // Smooth in/out animation.
                float inAnim = Mathf.Clamp01((now - item.CreatedAt) / 0.12f);
                float outAnim = Mathf.Clamp01((item.ExpireAt - now) / 0.18f);
                float anim = Mathf.Min(inAnim, outAnim);
                float alpha = Mathf.Lerp(0f, 1f, anim);
                float slide = (1f - anim) * 18f;

                Rect box = new Rect(area.x + slide, y, area.width, 42f);
                Rect shadow = new Rect(box.x + 2f, box.y + 3f, box.width, box.height);
                Rect accentStrip = new Rect(box.x, box.y, 4f, box.height);
                Rect messageRect = new Rect(box.x + 16f, box.y + 4f, box.width - 22f, 24f);
                Rect progressBg = new Rect(box.x + 10f, box.y + box.height - 8f, box.width - 20f, 3f);
                Rect progressFg = new Rect(progressBg.x, progressBg.y, progressBg.width * remain, progressBg.height);

                Color cardBg = new Color(0.04f, 0.06f, 0.09f, 0.93f * alpha);
                Color shadowBg = new Color(0f, 0f, 0f, 0.32f * alpha);
                Color stripColor = new Color(item.Color.r, item.Color.g, item.Color.b, 0.95f * alpha);
                Color progressBgColor = new Color(0.2f, 0.24f, 0.32f, 0.6f * alpha);
                Color progressColor = new Color(item.Color.r, item.Color.g, item.Color.b, 0.95f * alpha);

                GUI.color = shadowBg;
                GUI.DrawTexture(shadow, Texture2D.whiteTexture);
                GUI.color = cardBg;
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = stripColor;
                GUI.DrawTexture(accentStrip, Texture2D.whiteTexture);
                GUI.color = progressBgColor;
                GUI.DrawTexture(progressBg, Texture2D.whiteTexture);
                GUI.color = progressColor;
                GUI.DrawTexture(progressFg, Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.fontSize = 12;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleLeft;
                style.normal.textColor = new Color(0.94f, 0.96f, 1f, alpha);
                GUI.Label(messageRect, item.Message, style);

                // small status dot icon
                this.EnsureUiPrimitiveTextures();
                GUI.color = stripColor;
                GUI.DrawTexture(new Rect(box.x + 8f, box.y + 16f, 6f, 6f), this.uiCircleTexture);
                GUI.color = Color.white;

                y += 48f;
                if (y > area.yMax - 48f)
                {
                    break;
                }
            }
        }

        private void UpdateGameUiClickBlockState()
        {
            bool shouldBlock = this.showMenu;
            if (shouldBlock && !this.eventSystemBlockedByMenu)
            {
                EventSystem current = EventSystem.current;
                if (current != null)
                {
                    this.blockedEventSystem = current;
                    this.eventSystemPrevEnabled = current.enabled;
                    current.enabled = false;
                    this.eventSystemBlockedByMenu = true;
                }
            }
            else if (!shouldBlock && this.eventSystemBlockedByMenu)
            {
                EventSystem restoreTarget = this.blockedEventSystem != null ? this.blockedEventSystem : EventSystem.current;
                if (restoreTarget != null)
                {
                    restoreTarget.enabled = this.eventSystemPrevEnabled;
                }
                this.eventSystemBlockedByMenu = false;
                this.blockedEventSystem = null;
            }
        }

        private List<(string label, Func<bool> isActive, Action setActive)> GetActiveTopSubTabs()
        {
            var tabs = new List<(string label, Func<bool> isActive, Action setActive)>();
            if (this.selectedTab == 0)
            {
                tabs.Add(("Main", () => this.autoFarmSubTab == 0, () => this.SetAutoFarmSubTab(0)));
                tabs.Add(("Tree Farm", () => this.autoFarmSubTab == 1, () => this.SetAutoFarmSubTab(1)));
            }
            else if (this.selectedTab == 1)
            {
                tabs.Add(("Main", () => this.automationSubTab == 0, () => this.SetAutomationSubTab(0)));
                tabs.Add(("Bag", () => this.automationSubTab == 1, () => this.SetAutomationSubTab(1)));
                tabs.Add(("Tools", () => this.automationSubTab == 2, () => this.SetAutomationSubTab(2)));
            }
            else if (this.selectedTab == 2)
            {
                // No sub-tabs for Auto Cook
            }
            else if (this.selectedTab == 3)
            {
                // No sub-tabs for Radar
            }
            else if (this.selectedTab == 4)
            {
                tabs.Add(("Home", () => this.teleportSubTab == 0, () => this.SetTeleportSubTab(0)));
                tabs.Add(("Animal Care", () => this.teleportSubTab == 1, () => this.SetTeleportSubTab(1)));
                tabs.Add(("NPCs", () => this.teleportSubTab == 2, () => this.SetTeleportSubTab(2)));
                tabs.Add(("Locations", () => this.teleportSubTab == 3, () => this.SetTeleportSubTab(3)));
                tabs.Add(("Events", () => this.teleportSubTab == 4, () => this.SetTeleportSubTab(4)));
                tabs.Add(("House", () => this.teleportSubTab == 5, () => this.SetTeleportSubTab(5)));
                tabs.Add(("Custom", () => this.teleportSubTab == 6, () => this.SetTeleportSubTab(6)));
                tabs.Add(("XYZ", () => this.teleportSubTab == 7, () => this.SetTeleportSubTab(7)));
            }
            else if (this.selectedTab == 5)
            {
                // No sub-tabs for Items Selector
            }
            else if (this.selectedTab == 6)
            {
                tabs.Add(("Main", () => this.settingsSubTab == 0, () => this.SetSettingsSubTab(0)));
                tabs.Add(("Keybinds", () => this.settingsSubTab == 1, () => this.SetSettingsSubTab(1)));
                tabs.Add(("UI Theme", () => this.settingsSubTab == 2, () => this.SetSettingsSubTab(2)));
            }
            return tabs;
        }

        private void SetTeleportSubTab(int subTab)
        {
            if (this.teleportSubTab != subTab)
            {
                this.teleportSubTab = subTab;
                this.tabScrollPos = Vector2.zero;
                this.fastTravelScrollPosition = Vector2.zero;
            }
        }

        private void SetSettingsSubTab(int subTab)
        {
            if (this.settingsSubTab != subTab)
            {
                this.settingsSubTab = subTab;
                this.tabScrollPos = Vector2.zero;
                if (subTab != 1)
                {
                    this.keyBindingActive = "";
                }
                if (subTab == 2)
                {
                    this.uiThemeHexInput = this.ColorToHex(this.GetUiThemeColorTargetValue(this.uiThemeColorTarget));
                    this.uiThemePickerOpen = false;
                }
            }
        }

        private void SetAutomationSubTab(int subTab)
        {
            if (this.automationSubTab != subTab)
            {
                this.automationSubTab = subTab;
                this.tabScrollPos = Vector2.zero;
            }
        }

        private void SetAutoFarmSubTab(int subTab)
        {
            if (this.autoFarmSubTab != subTab)
            {
                this.autoFarmSubTab = subTab;
                this.tabScrollPos = Vector2.zero;
            }
        }

        private void EnsureThemeStyles()
        {
            bool themeInvalid =
                this.themeWindowStyle == null ||
                this.themePanelStyle == null ||
                this.themeContentStyle == null ||
                this.themeSidebarButtonStyle == null ||
                this.themeSidebarButtonActiveStyle == null ||
                this.themePrimaryButtonStyle == null ||
                this.themeDangerButtonStyle == null ||
                this.themeTopTabStyle == null ||
                this.themeTopTabActiveStyle == null ||
                this.themeWindowStyle.normal.background == null ||
                this.themePanelStyle.normal.background == null ||
                this.themeContentStyle.normal.background == null;

            if (this.themeInitialized && !themeInvalid)
            {
                return;
            }

            if (GUI.skin == null)
            {
                return;
            }

            this.InvalidateThemeCache();

            Color textPrimary = new Color(this.uiTextR, this.uiTextG, this.uiTextB);
            Color textMuted = new Color(0.5f, 0.58f, 0.56f);
            Color accent = new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB);
            Color mainTabText = new Color(this.uiMainTabTextR, this.uiMainTabTextG, this.uiMainTabTextB);
            Color windowBase = new Color(this.uiWindowR, this.uiWindowG, this.uiWindowB, this.uiWindowAlpha);
            Color panel = new Color(this.uiPanelR, this.uiPanelG, this.uiPanelB, this.uiPanelAlpha);
            Color contentBase = new Color(this.uiContentR, this.uiContentG, this.uiContentB, this.uiContentAlpha);

            Texture2D windowBg = this.MakeThemeTexture(windowBase);
            Texture2D panelBg = this.MakeThemeTexture(panel);
            Texture2D contentBg = this.MakeThemeTexture(contentBase);
            float buttonAlpha = Mathf.Clamp(this.uiContentAlpha, 0.15f, 1f);
            Texture2D buttonBg = this.MakeThemeTexture(new Color(0.04f, 0.055f, 0.065f, buttonAlpha));
            Texture2D buttonHover = this.MakeThemeTexture(new Color(0.07f, 0.09f, 0.1f, Mathf.Min(1f, buttonAlpha + 0.08f)));
            Texture2D buttonActive = this.MakeThemeTexture(new Color(0.03f, 0.04f, 0.045f, buttonAlpha));
            Texture2D tabActive = this.MakeThemeTexture(new Color(accent.r * 0.5f, Mathf.Min(1f, accent.g * 0.22f), accent.b * 0.3f, Mathf.Clamp(this.uiPanelAlpha, 0.15f, 1f)));
            Texture2D topTabBg = this.MakeThemeTexture(new Color(0.045f, 0.055f, 0.07f, Mathf.Clamp(this.uiPanelAlpha + 0.02f, 0.2f, 1f)));
            Texture2D topTabActive = this.MakeThemeTexture(new Color(accent.r * 0.28f, accent.g * 0.26f, accent.b * 0.36f, Mathf.Clamp(this.uiPanelAlpha + 0.08f, 0.2f, 1f)));
            Texture2D primaryButtonBg = this.MakeThemeTexture(new Color(0.12f, 0.18f, 0.28f, Mathf.Clamp(this.uiContentAlpha + 0.08f, 0.25f, 1f)));
            Texture2D primaryButtonHover = this.MakeThemeTexture(new Color(0.16f, 0.24f, 0.36f, Mathf.Clamp(this.uiContentAlpha + 0.08f, 0.25f, 1f)));
            Texture2D dangerButtonBg = this.MakeThemeTexture(new Color(0.33f, 0.12f, 0.14f, Mathf.Clamp(this.uiContentAlpha + 0.08f, 0.25f, 1f)));
            Texture2D dangerButtonHover = this.MakeThemeTexture(new Color(0.46f, 0.16f, 0.18f, Mathf.Clamp(this.uiContentAlpha + 0.08f, 0.25f, 1f)));
            Texture2D sliderBg = this.MakeThemeTexture(new Color(0.08f, 0.09f, 0.1f, 1f));
            Texture2D sliderThumb = this.MakeThemeTexture(accent);
            Texture2D boxBg = this.MakeThemeTexture(new Color(0.03f, 0.04f, 0.055f, Mathf.Clamp(this.uiPanelAlpha, 0.15f, 1f)));
            Texture2D fieldBg = this.MakeThemeTexture(new Color(0.035f, 0.045f, 0.055f, Mathf.Clamp(this.uiContentAlpha, 0.15f, 1f)));
            Texture2D cursorBg = this.MakeThemeTexture(accent);

            this.themeWindowStyle = new GUIStyle(GUI.skin.window);
            this.themeWindowStyle.normal.background = windowBg;
            this.themeWindowStyle.onNormal.background = windowBg;
            this.themeWindowStyle.border = new RectOffset(8, 8, 24, 8);
            this.themeWindowStyle.padding = new RectOffset(10, 10, 28, 10);
            this.themeWindowStyle.normal.textColor = accent;
            this.themeWindowStyle.alignment = TextAnchor.UpperCenter;
            this.themeWindowStyle.fontStyle = FontStyle.Bold;
            this.themeWindowStyle.fontSize = 18;

            this.themePanelStyle = new GUIStyle(GUI.skin.box);
            this.themePanelStyle.normal.background = panelBg;
            this.themePanelStyle.onNormal.background = panelBg;
            this.themePanelStyle.border = new RectOffset(6, 6, 6, 6);
            this.themePanelStyle.normal.textColor = textMuted;

            this.themeContentStyle = new GUIStyle(GUI.skin.box);
            this.themeContentStyle.normal.background = contentBg;
            this.themeContentStyle.onNormal.background = contentBg;
            this.themeContentStyle.border = new RectOffset(6, 6, 6, 6);

            this.themeSidebarButtonStyle = new GUIStyle(GUI.skin.button);
            this.themeSidebarButtonStyle.fixedHeight = 42f;
            this.themeSidebarButtonStyle.margin = new RectOffset(2, 2, 4, 4);
            this.themeSidebarButtonStyle.fontSize = 13;
            this.themeSidebarButtonStyle.fontStyle = FontStyle.Bold;
            this.themeSidebarButtonStyle.alignment = TextAnchor.MiddleLeft;
            this.themeSidebarButtonStyle.padding = new RectOffset(14, 10, 4, 4);
            this.themeSidebarButtonStyle.normal.background = buttonBg;
            this.themeSidebarButtonStyle.hover.background = buttonHover;
            this.themeSidebarButtonStyle.active.background = buttonActive;
            this.themeSidebarButtonStyle.normal.textColor = mainTabText;
            this.themeSidebarButtonStyle.hover.textColor = accent;
            this.themeSidebarButtonStyle.active.textColor = accent;

            this.themeSidebarButtonActiveStyle = new GUIStyle(this.themeSidebarButtonStyle);
            this.themeSidebarButtonActiveStyle.normal.background = tabActive;
            this.themeSidebarButtonActiveStyle.hover.background = tabActive;
            this.themeSidebarButtonActiveStyle.active.background = tabActive;
            this.themeSidebarButtonActiveStyle.normal.textColor = accent;
            this.themeSidebarButtonActiveStyle.hover.textColor = accent;
            this.themeSidebarButtonActiveStyle.active.textColor = accent;

            this.themeTopTabStyle = new GUIStyle(GUI.skin.box);
            this.themeTopTabStyle.normal.background = topTabBg;
            this.themeTopTabStyle.onNormal.background = topTabBg;
            this.themeTopTabStyle.border = new RectOffset(5, 5, 5, 5);

            this.themeTopTabActiveStyle = new GUIStyle(GUI.skin.box);
            this.themeTopTabActiveStyle.normal.background = topTabActive;
            this.themeTopTabActiveStyle.onNormal.background = topTabActive;
            this.themeTopTabActiveStyle.border = new RectOffset(5, 5, 5, 5);

            this.themePrimaryButtonStyle = new GUIStyle(GUI.skin.button);
            this.themePrimaryButtonStyle.normal.background = primaryButtonBg;
            this.themePrimaryButtonStyle.hover.background = primaryButtonHover;
            this.themePrimaryButtonStyle.active.background = primaryButtonHover;
            this.themePrimaryButtonStyle.fontStyle = FontStyle.Bold;
            this.themePrimaryButtonStyle.normal.textColor = Color.white;
            this.themePrimaryButtonStyle.hover.textColor = Color.white;
            this.themePrimaryButtonStyle.active.textColor = Color.white;

            this.themeDangerButtonStyle = new GUIStyle(this.themePrimaryButtonStyle);
            this.themeDangerButtonStyle.normal.background = dangerButtonBg;
            this.themeDangerButtonStyle.hover.background = dangerButtonHover;
            this.themeDangerButtonStyle.active.background = dangerButtonHover;

            GUI.skin.label.normal.textColor = textPrimary;

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = buttonBg;
            buttonStyle.hover.background = buttonHover;
            buttonStyle.active.background = buttonActive;
            buttonStyle.normal.textColor = textPrimary;
            buttonStyle.hover.textColor = accent;
            buttonStyle.active.textColor = accent;
            buttonStyle.fontStyle = FontStyle.Bold;
            GUI.skin.button = buttonStyle;

            GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.normal.textColor = textPrimary;
            toggleStyle.onNormal.textColor = accent;
            toggleStyle.hover.textColor = accent;
            toggleStyle.onHover.textColor = accent;
            toggleStyle.active.textColor = accent;
            toggleStyle.onActive.textColor = accent;
            GUI.skin.toggle = toggleStyle;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = boxBg;
            boxStyle.onNormal.background = boxBg;
            boxStyle.normal.textColor = textPrimary;
            GUI.skin.box = boxStyle;

            GUIStyle sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            sliderStyle.normal.background = sliderBg;
            sliderStyle.fixedHeight = 6f;
            sliderStyle.margin = new RectOffset(4, 4, 10, 10);
            GUI.skin.horizontalSlider = sliderStyle;

            GUIStyle sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
            sliderThumbStyle.normal.background = sliderThumb;
            sliderThumbStyle.hover.background = sliderThumb;
            sliderThumbStyle.active.background = sliderThumb;
            sliderThumbStyle.fixedWidth = 12f;
            sliderThumbStyle.fixedHeight = 16f;
            GUI.skin.horizontalSliderThumb = sliderThumbStyle;

            GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
            textFieldStyle.normal.background = fieldBg;
            textFieldStyle.focused.background = fieldBg;
            textFieldStyle.hover.background = fieldBg;
            textFieldStyle.active.background = fieldBg;
            textFieldStyle.normal.textColor = textPrimary;
            textFieldStyle.focused.textColor = Color.white;
            textFieldStyle.padding = new RectOffset(8, 8, 4, 4);
            GUI.skin.textField = textFieldStyle;

            GUIStyle scrollStyle = new GUIStyle(GUI.skin.verticalScrollbar);
            scrollStyle.normal.background = this.MakeThemeTexture(new Color(0.05f, 0.06f, 0.07f, 1f));
            GUI.skin.verticalScrollbar = scrollStyle;

            GUIStyle scrollThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
            scrollThumbStyle.normal.background = this.MakeThemeTexture(new Color(0.07f, 0.3f, 0.12f, 1f));
            scrollThumbStyle.hover.background = cursorBg;
            scrollThumbStyle.active.background = cursorBg;
            GUI.skin.verticalScrollbarThumb = scrollThumbStyle;

            this.themeInitialized = true;
        }

        private Texture2D MakeThemeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            this.themeTextures.Add(texture);
            return texture;
        }

        // Token: 0x06000008 RID: 8 RVA: 0x00002AA0 File Offset: 0x00000CA0
        private float DrawAutoFarmTab(int startY)
        {
            if (this.autoFarmSubTab == 1)
            {
                return this.DrawTreeFarmTab(startY);
            }

            bool flag = this.showMushroomRadar || this.showBlueberryRadar || this.showRaspberryRadar || this.showBubbleRadar || this.showInsectRadar || this.showFishShadowRadar;
            string text = this.autoFarmActive ? "DISABLE AUTO FARM" : "ENABLE AUTO FARM";
            bool flag2 = this.DrawPrimaryActionButton(new Rect(20f, (float)startY, 260f, 40f), text);
            if (flag2)
            {
                this.ToggleAutoFarm();
            }
            int num = startY + 50;

            // NEW: Status Box Container
            Rect statusBoxRect = new Rect(20f, (float)num, 260f, 90f);
            GUI.Box(statusBoxRect, "");
            this.DrawCardOutline(statusBoxRect, 1f);
            
            // Centered Header
            GUIStyle centerHeader = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(20f, (float)num + 5f, 260f, 20f), "Status:", centerHeader);

            // Relative Y for text inside box
            float statusTextY = (float)num + 25f;

            GUIStyle guistyle = new GUIStyle(GUI.skin.label);
            guistyle.fontSize = 16;
            guistyle.fontStyle = (FontStyle)1;
            guistyle.alignment = TextAnchor.MiddleCenter; // Center text inside box

            bool flag3 = !flag;
            if (flag3)
            {
                guistyle.normal.textColor = Color.red;
                bool flag4 = this.autoFarmActive;
                if (flag4)
                {
                    this.autoFarmActive = false;
                    this.autoFarmEnabled = false;
                    this.gameSpeed = 1f;
                    this.farmState = HeartopiaComplete.AutoFarmState.Idle;
                }
                GUI.Label(new Rect(20f, statusTextY, 260f, 60f), "✗ No radar toggles selected", guistyle);
            }
            else
            {
                bool flag5 = this.autoFarmStatus == "NO_TOGGLES_ERROR";
                if (flag5)
                {
                    guistyle.normal.textColor = Color.red;
                    GUI.Label(new Rect(20f, statusTextY, 260f, 60f), "✗ No radar toggles selected\n   Auto Farm disabled...", guistyle);
                }
                else
                {
                    bool flagRadarOff = this.autoFarmStatus == "RADAR_OFF_ERROR";
                    if (flagRadarOff)
                    {
                        guistyle.normal.textColor = Color.red;
                        GUI.Label(new Rect(20f, statusTextY, 260f, 60f), "✗ Radar is OFF!\n   Enable Radar first.", guistyle);
                    }
                    else
                    {
                        bool flag6 = !this.autoFarmActive && (this.autoFarmStatus == "READY" || this.autoFarmStatus == "Idle" || this.autoFarmStatus == "NO_TOGGLES");
                        if (flag6)
                        {
                            guistyle.normal.textColor = Color.green;
                            GUI.Label(new Rect(20f, statusTextY, 260f, 30f), "✓ Ready", guistyle);
                        }
                        else
                        {
                            guistyle.normal.textColor = Color.white; // Ensure visibility
                            GUI.Label(new Rect(20f, statusTextY, 260f, 60f), this.autoFarmStatus, guistyle);
                            bool flag7 = this.cameraStuckDisplayTimer > 0f;
                            if (flag7)
                            {
                                GUIStyle guistyle2 = new GUIStyle(GUI.skin.label);
                                guistyle2.fontSize = 14;
                                guistyle2.fontStyle = (FontStyle)1;
                                guistyle2.normal.textColor = Color.red;
                                guistyle2.alignment = TextAnchor.MiddleCenter;
                                GUI.Label(new Rect(20f, statusTextY + 30f, 260f, 30f), "⚠ Camera Stuck Attempting Fix!", guistyle2);
                            }
                        }
                    }
                }
            }
            num += 100; // Increased spacing for the box
            Rect rect = new Rect(20f, (float)num, 260f, 20f);
            GUI.Label(rect, $"Area Load Delay: {(int)this.areaLoadDelay}s");
            num += 22;
            this.areaLoadDelay = Mathf.Round(this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.areaLoadDelay, 1f, 10f));
            num += 30;

            // --- LOOT PRIORITIES SECTION ---
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), "LOOT PRIORITIES");
            num += 25;

            int lootTop = num;
            int leftX = 20;
            int rightX = 250;
            int colW = 200;

            int leftY = lootTop;
            GUI.Label(new Rect((float)leftX, (float)leftY, (float)colW, 20f), "Mushrooms:");
            leftY += 22;
            this.priorityOysterMushroom = this.DrawSwitchToggle(new Rect((float)(leftX + 20), (float)leftY, (float)(colW - 10), 20f), this.priorityOysterMushroom, "Oyster Mushroom");
            leftY += 22;
            this.priorityButtonMushroom = this.DrawSwitchToggle(new Rect((float)(leftX + 20), (float)leftY, (float)(colW - 10), 20f), this.priorityButtonMushroom, "Button Mushroom");
            leftY += 22;
            this.priorityPennyBun = this.DrawSwitchToggle(new Rect((float)(leftX + 20), (float)leftY, (float)(colW - 10), 20f), this.priorityPennyBun, "Penny Bun");
            leftY += 22;
            this.priorityShiitake = this.DrawSwitchToggle(new Rect((float)(leftX + 20), (float)leftY, (float)(colW - 10), 20f), this.priorityShiitake, "Shiitake");
            leftY += 22;
            this.priorityTruffle = this.DrawSwitchToggle(new Rect((float)(leftX + 20), (float)leftY, (float)(colW - 10), 20f), this.priorityTruffle, "Truffle");
            leftY += 8;

            int rightY = lootTop;
            GUI.Label(new Rect((float)rightX, (float)rightY, (float)colW, 20f), "Other:");
            rightY += 22;
            this.priorityBlueberry = this.DrawSwitchToggle(new Rect((float)(rightX + 20), (float)rightY, (float)(colW - 10), 20f), this.priorityBlueberry, "Blueberries");
            rightY += 22;
            this.priorityRaspberry = this.DrawSwitchToggle(new Rect((float)(rightX + 20), (float)rightY, (float)(colW - 10), 20f), this.priorityRaspberry, "Raspberries");
            rightY += 22;
            this.priorityBubble = this.DrawSwitchToggle(new Rect((float)(rightX + 20), (float)rightY, (float)(colW - 10), 20f), this.priorityBubble, "Bubbles");
            rightY += 22;
            this.priorityInsect = this.DrawSwitchToggle(new Rect((float)(rightX + 20), (float)rightY, (float)(colW - 10), 20f), this.priorityInsect, "Insects");
            rightY += 8;

            num = Mathf.Max(leftY, rightY) + 20;

            // Show active priority location
            Vector3? activePriorityLoc = this.GetActivePriorityLocation();
            if (activePriorityLoc != null)
            {
                GUI.Label(new Rect(20f, (float)num, 260f, 50f), $"Priority Location:\n{activePriorityLoc.Value.x:F1}, {activePriorityLoc.Value.y:F1}, {activePriorityLoc.Value.z:F1}\n(Will visit FIRST)", GUI.skin.label);
                num += 60;
            }
            else
            {
                GUI.Label(new Rect(20f, (float)num, 260f, 20f), "Priority Location: None");
                num += 30;
            }

            GUI.Label(new Rect(20f, (float)num, 260f, 180f), "Auto Farm will:\n• Enable Auto Collect\n• Enable x5.0 GameSpeed\n• Teleport to closest node\n• Auto-rotate camera if stuck\n• Respect radar toggles for farming\n• Skip nodes on cooldown\n• Cycle through locations if nothing is found\n• PRIORITIZE selected loot types first\n• SEEK priority locations FIRST when enabled");
            return (float)num + 190f;
        }

        private float DrawTreeFarmTab(int startY)
        {
            int num = startY;
            string toggleText = this.treeFarmEnabled ? "DISABLE TREE FARM" : "ENABLE TREE FARM";
            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 40f), toggleText))
            {
                if (this.treeFarmEnabled)
                {
                    this.StopTreeFarm("Stopped");
                }
                else if (this.treeFarmPoints.Count > 0)
                {
                    this.StartTreeFarm();
                }
                else
                {
                    this.AddMenuNotification("Add at least 1 tree point first", new Color(1f, 0.55f, 0.55f));
                }
            }
            num += 50;

            GUI.Label(new Rect(20f, (float)num, 320f, 24f), "Status: " + this.treeFarmStatus);
            num += 28;
            GUI.Label(new Rect(20f, (float)num, 320f, 24f), $"Points: {this.treeFarmPoints.Count}");
            num += 28;
            GUI.Label(new Rect(20f, (float)num, 320f, 24f), this.treeFarmPoints.Count > 0 ? $"Current Index: {this.treeFarmCurrentIndex + 1}/{this.treeFarmPoints.Count}" : "Current Index: -");
            num += 32;

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Next Location Wait: {this.treeFarmNextLocationWait:F1}s");
            num += 22;
            this.treeFarmNextLocationWait = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.treeFarmNextLocationWait, 0.2f, 10f);
            num += 30;

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Chop Presses: {this.treeFarmChopPressCount}");
            num += 22;
            this.treeFarmChopPressCount = Mathf.RoundToInt(this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.treeFarmChopPressCount, 1f, 6f));
            num += 30;

            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), "Add Current Position"))
            {
                GameObject p = this.GetPlayer();
                if (p != null)
                {
                    this.treeFarmPoints.Add(new TreeFarmPatrolPoint(p.transform.position, p.transform.rotation));
                    this.AddMenuNotification($"Tree point added ({this.treeFarmPoints.Count})", new Color(0.45f, 1f, 0.55f));
                }
            }
            num += 40;
            if (this.DrawDangerActionButton(new Rect(20f, (float)num, 260f, 35f), "Clear Tree Points"))
            {
                this.treeFarmPoints.Clear();
                this.treeFarmCurrentIndex = 0;
                if (this.treeFarmEnabled)
                {
                    this.StopTreeFarm("No points");
                }
            }
            num += 45;

            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 120f, 35f), "SAVE"))
            {
                this.SaveTreeFarmPatrolPoints();
            }
            if (this.DrawPrimaryActionButton(new Rect(150f, (float)num, 120f, 35f), "LOAD"))
            {
                this.LoadTreeFarmPatrolPoints();
            }
            num += 45;

            GUI.Label(new Rect(20f, (float)num, 360f, 120f), "Tree Farm flow:\n1) Auto equip Axe\n2) Wait 2s\n3) Teleport to each saved point\n4) Click interact/chop 3x\n5) Wait slider time\n6) Go next point");
            return (float)num + 130f;
        }

        // Token: 0x06000009 RID: 9 RVA: 0x00002E24 File Offset: 0x00001024
        private float DrawAutomationTab(int startY)
        {
            GUI.Label(new Rect(20f, (float)startY, 260f, 20f), "Automation & Tweaks");
            int num = startY + 25;

            if (this.automationSubTab == 0)
            {
                this.autoFarmEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.autoFarmEnabled, "Auto Collect");
                num += 25;
                bool flag = this.autoFarmEnabled;
                if (flag)
                {
                    GUI.Label(new Rect(40f, (float)num, 240f, 20f), "Collect Types:");
                    num += 22;
                    this.collectMushrooms = this.DrawSwitchToggle(new Rect(40f, (float)num, 240f, 20f), this.collectMushrooms, "  Mushrooms");
                    num += 22;
                    this.collectBerries = this.DrawSwitchToggle(new Rect(40f, (float)num, 240f, 20f), this.collectBerries, "  Berries / Bushes / Plants");
                    num += 32;
                }
                else
                {
                    num += 5;
                }
                this.bypassEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.bypassEnabled, "Hide UI + Player (Client Side)");
                num += 30;
                this.birdVacuumEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.birdVacuumEnabled, "Bird Vacuum (Client Side)");
                num += 35;
                Rect rect = new Rect(20f, (float)num, 260f, 20f);
                GUI.Label(rect, $"Game Speed: {this.gameSpeed:F1}x");
                num += 22;
                this.gameSpeed = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.gameSpeed, 1f, 10f);

                num += 30;
                Rect rectJump = new Rect(20f, (float)num, 260f, 20f);
                GUI.Label(rectJump, string.Format("Jump Height: {0:F1}x", this.jumpHeightMultiplier));
                num += 22;
                this.jumpHeightMultiplier = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.jumpHeightMultiplier, 1f, 5f);
                num += 30;
                Rect rectFOV = new Rect(20f, (float)num, 260f, 20f);
                GUI.Label(rectFOV, string.Format("Camera FOV: {0:F0}°", this.cameraFOV));
                num += 22;
                float newFOV = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.cameraFOV, 30f, 120f);
                if (newFOV != this.cameraFOV)
                {
                    this.cameraFOV = newFOV;
                    this.ApplyCameraFOV();
                }
                num += 40;
                bool flag2 = this.DrawDangerActionButton(new Rect(20f, (float)num, 260f, 35f), "DISABLE ALL");
                if (flag2)
                {
                    this.autoFarmEnabled = false;
                    this.bypassEnabled = false;
                    this.birdVacuumEnabled = false;
                    this.StopAutoCookInternal("Disabled");
                    this.pendingToolEquipType = 0;
                    this.isAutoEating = false;
                    this.StopTreeFarm("Stopped");
                    this.cookingCleanupMode = false;
                    this.cookingSpeedActive = false;
                    this.gameSpeed = 1f;
                    this.jumpHeightMultiplier = 1f;
                    this.cameraFOV = 60f;
                    this.ApplyCameraFOV();
                }
                num += 45;
                GUI.Label(new Rect(20f, (float)num, 260f, 160f), $"Controls:\n{this.keyToggleRadar} - Radar\n{this.keyAutoFarm} - Auto Collect\n{this.keyAutoCook} - Auto Cook\n{this.keyBypassUI} - UI/Skeleton\n{this.keyDisableAll} - Disable All\n{this.keyJoinMyTown} - Join My Town");
                return (float)num + 170f;
            }

            if (this.automationSubTab == 1)
            {
                GUI.Label(new Rect(20f, (float)num, 260f, 24f), "Bag");
                num += 30;
                if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), "Auto Repair"))
                {
                    if (!this.isRepairing && !this.isAutoEating)
                    {
                        this.StartRepair();
                        this.AddMenuNotification("Auto Repair started", new Color(0.45f, 1f, 0.55f));
                    }
                    else
                    {
                        this.AddMenuNotification("Bag automation already running", new Color(1f, 0.85f, 0.35f));
                    }
                }
                num += 42;

                string repairStatus = this.isRepairing ? $"Running (step {this.repairStep})" : "Idle";
                GUI.Label(new Rect(20f, (float)num, 320f, 24f), "Repair Status: " + repairStatus);
                num += 28;
                if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), "Auto Eat Blue Jam"))
                {
                    if (!this.isAutoEating && !this.isRepairing)
                    {
                        this.StartAutoEat();
                        this.AddMenuNotification("Auto Eat started", new Color(0.45f, 1f, 0.55f));
                    }
                    else
                    {
                        this.AddMenuNotification("Bag automation already running", new Color(1f, 0.85f, 0.35f));
                    }
                }
                num += 42;
                string autoEatStatus = this.isAutoEating ? $"Running (step {this.autoEatStep})" : "Idle";
                GUI.Label(new Rect(20f, (float)num, 320f, 24f), "Auto Eat Status: " + autoEatStatus);
                num += 28;
                GUI.Label(new Rect(20f, (float)num, 340f, 84f), "Auto Repair: open bag -> find repair kit -> Use -> close bag.\nAuto Eat: open bag -> find BLUEJAM -> Use -> close bag.");
                return (float)num + 110f;
            }

            GUI.Label(new Rect(20f, (float)num, 260f, 22f), "Tools");
            num += 28;
            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), "Auto Equip Axe"))
            {
                this.StartToolEquipRequest(1);
            }
            num += 40;
            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), "Auto Equip Net"))
            {
                this.StartToolEquipRequest(2);
            }
            num += 40;
            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), "Auto Equip Fishing Rod"))
            {
                this.StartToolEquipRequest(3);
            }
            num += 45;
            GUI.Label(new Rect(20f, (float)num, 260f, 70f), "One-click test action:\nOpens toolbox, then tries to equip selected tool.");
            return (float)num + 90f;
        }

        private float DrawAutoCookTab(int startY)
        {
            int num = startY;
            string text = this.autoCookEnabled ? "DISABLE AUTO COOK" : "ENABLE AUTO COOK";
            bool flag = this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 40f), text);
            if (flag)
            {
                if (this.autoCookEnabled)
                {
                    this.StopAutoCookInternal("Disabled");
                }
                else
                {
                    this.StartAutoCookInternal();
                }
            }
            num += 50;

            this.enablePlayerDetection = this.DrawSwitchToggle(new Rect(20f, (float)num, 240f, 25f), this.enablePlayerDetection, "Player Alert (25m)");
            num += 30;

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Cooking Speed: {this.cookingAutoSpeed:F1}x");
            num += 22;
            float newCookingSpeed = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.cookingAutoSpeed, 1f, 10f);
            if (newCookingSpeed != this.cookingAutoSpeed)
            {
                this.cookingAutoSpeed = newCookingSpeed;
                // If Auto Cook is currently enabled, update the game speed immediately
                if (this.autoCookEnabled)
                {
                    this.gameSpeed = this.cookingAutoSpeed;
                }
            }
            num += 30;

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Cooking Points: {cookingPatrolPoints.Count}");
            num += 25;

            if (GUI.Button(new Rect(20f, (float)num, 120f, 35f), "SAVE"))
            {
                SaveCookingPatrolPoints();
            }
            if (GUI.Button(new Rect(160f, (float)num, 120f, 35f), "LOAD"))
            {
                LoadCookingPatrolPoints();
            }
            num += 45;

            if (GUI.Button(new Rect(20f, (float)num, 260f, 40f), "ADD CURRENT POSITION + ROTATION"))
            {
                GameObject p = GetPlayer();
                if (p != null)
                {
                    Vector3 position = p.transform.position;
                    Quaternion rotation = p.transform.rotation;
                    cookingPatrolPoints.Add(new CookingPatrolPoint(position, rotation));
                    MelonLogger.Msg($"Added cooking patrol point at {position} facing {rotation.eulerAngles}");

                    // Start cooking patrol if Auto Cook is enabled and this is the first point
                    if (autoCookEnabled && cookingPatrolPoints.Count == 1 && !isCookingPatrolActive)
                    {
                        isCookingPatrolActive = true;
                        cookingPatrolCoroutine = MelonCoroutines.Start(CookingPatrolRoutine());
                        MelonLogger.Msg("[Cooking Patrol] STARTED");
                    }
                }
            }
            num += 50;

            if (GUI.Button(new Rect(20f, (float)num, 260f, 35f), "CLEAR ALL"))
            {
                cookingPatrolPoints.Clear();
                MelonLogger.Msg("Cleared all cooking patrol points.");

                // Stop cooking patrol if it was active
                if (isCookingPatrolActive)
                {
                    isCookingPatrolActive = false;
                    if (cookingPatrolCoroutine != null)
                    {
                        MelonCoroutines.Stop(cookingPatrolCoroutine);
                        cookingPatrolCoroutine = null;
                        MelonLogger.Msg("[Cooking Patrol] STOPPED");
                    }
                }
            }
            num += 45;

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Wait Time: {cookingWaitAtSpot:F2}s");
            num += 22;
            cookingWaitAtSpot = Mathf.Round(this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), cookingWaitAtSpot, 0.1f, 2.0f) * 100f) / 100f;
            num += 40;

            return (float)num + 20f;
        }

        private void StartAutoCookInternal()
        {
            this.autoCookEnabled = true;
            this.cookingCleanupMode = false;
            this.cookingSpeedActive = true;
            this.gameSpeed = this.cookingAutoSpeed;
            MelonLogger.Msg($"[Cooking] Bot STARTED (Auto Speed x{this.cookingAutoSpeed:F1})");
            this.AddMenuNotification("Auto Cook Enabled", new Color(0.45f, 1f, 0.55f));

            if (cookingPatrolPoints.Count > 0 && !isCookingPatrolActive)
            {
                isCookingPatrolActive = true;
                cookingPatrolCoroutine = MelonCoroutines.Start(CookingPatrolRoutine());
                MelonLogger.Msg("[Cooking Patrol] STARTED");
            }
        }

        private void StopAutoCookInternal(string reason)
        {
            bool wasEnabled = this.autoCookEnabled || this.isCookingPatrolActive;
            this.autoCookEnabled = false;
            this.cookingSpeedActive = false;
            this.cookingCleanupMode = false;
            this.gameSpeed = 1f;

            isCookingPatrolActive = false;
            if (cookingPatrolCoroutine != null)
            {
                MelonCoroutines.Stop(cookingPatrolCoroutine);
                cookingPatrolCoroutine = null;
            }

            if (wasEnabled)
            {
                MelonLogger.Msg("[Cooking] Bot STOPPED: " + reason);
                this.AddMenuNotification("Auto Cook " + reason, new Color(1f, 0.7f, 0.45f));
            }
        }

        // Token: 0x0600000A RID: 10 RVA: 0x000030E8 File Offset: 0x000012E8
        private float DrawRadarTab(int startY)
        {
            int num = startY;
            string text = this.isRadarActive ? "DISABLE RADAR" : "ENABLE RADAR";
            bool flag = this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 40f), text);
            if (flag)
            {
                this.ToggleRadar();
            }
            num += 50;
            bool flag2 = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showMushroomRadar, "Mushrooms");
            bool flag3 = flag2 != this.showMushroomRadar;
            if (flag3)
            {
                this.showMushroomRadar = flag2;
                this.CheckRadarAutoToggle();
                bool flag4 = this.isRadarActive;
                if (flag4)
                {
                    this.RunRadar();
                }
            }
            num += 30;
            bool flag5 = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showBlueberryRadar, "Blueberries");
            bool flag6 = flag5 != this.showBlueberryRadar;
            if (flag6)
            {
                this.showBlueberryRadar = flag5;
                this.CheckRadarAutoToggle();
                bool flag7 = this.isRadarActive;
                if (flag7)
                {
                    this.RunRadar();
                }
            }
            num += 30;
            bool flag8 = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showRaspberryRadar, "Raspberries");
            bool flag9 = flag8 != this.showRaspberryRadar;
            if (flag9)
            {
                this.showRaspberryRadar = flag8;
                this.CheckRadarAutoToggle();
                bool flag10 = this.isRadarActive;
                if (flag10)
                {
                    this.RunRadar();
                }
            }
            num += 30;
            bool flag11 = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showBubbleRadar, "Bubbles");
            bool flag12 = flag11 != this.showBubbleRadar;
            if (flag12)
            {
                this.showBubbleRadar = flag11;
                this.CheckRadarAutoToggle();
                bool flag13 = this.isRadarActive;
                if (flag13)
                {
                    this.RunRadar();
                }
            }
            num += 30;
            bool flag14 = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showInsectRadar, "Insects");
            bool flag15 = flag14 != this.showInsectRadar;
            if (flag15)
            {
                this.showInsectRadar = flag14;
                bool flag16 = !this.showInsectRadar && this.radarContainer != null;
                if (flag16)
                {
                    List<GameObject> list = new List<GameObject>();
                    List<int> list2 = new List<int>();
                    foreach (KeyValuePair<int, GameObject> keyValuePair in this.trackedObjectMarkers.ToList<KeyValuePair<int, GameObject>>())
                    {
                        GameObject value = keyValuePair.Value;
                        bool flag17 = value != null && value.name.StartsWith("TrackedMarker_");
                        if (flag17)
                        {
                            GameObject gameObject = null;
                            foreach (KeyValuePair<GameObject, GameObject> keyValuePair2 in this.markerToTarget)
                            {
                                bool flag18 = keyValuePair2.Key == value;
                                if (flag18)
                                {
                                    gameObject = keyValuePair2.Value;
                                    break;
                                }
                            }
                            bool flag19 = gameObject != null && gameObject.name.ToLower().Contains("p_insect_insect");
                            if (flag19)
                            {
                                list.Add(value);
                                list2.Add(keyValuePair.Key);
                            }
                        }
                    }
                    foreach (GameObject gameObject2 in list)
                    {
                        this.markerToTarget.Remove(gameObject2);
                        Object.Destroy(gameObject2);
                    }
                    foreach (int key in list2)
                    {
                        this.trackedObjectMarkers.Remove(key);
                    }
                }
                this.CheckRadarAutoToggle();
                bool flag20 = this.isRadarActive;
                if (flag20)
                {
                    this.RunRadar();
                }
            }
            num += 30;
            bool flag28 = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showFishShadowRadar, "Fish Shadows");
            bool flag29 = flag28 != this.showFishShadowRadar;
            if (flag29)
            {
                this.showFishShadowRadar = flag28;
                bool flag30 = !this.showFishShadowRadar && this.radarContainer != null;
                if (flag30)
                {
                    List<GameObject> list3 = new List<GameObject>();
                    List<int> list4 = new List<int>();
                    foreach (KeyValuePair<int, GameObject> keyValuePair3 in this.trackedObjectMarkers.ToList<KeyValuePair<int, GameObject>>())
                    {
                        GameObject value2 = keyValuePair3.Value;
                        bool flag31 = value2 != null && value2.name.StartsWith("TrackedMarker_");
                        if (flag31)
                        {
                            GameObject gameObject3 = null;
                            foreach (KeyValuePair<GameObject, GameObject> keyValuePair4 in this.markerToTarget)
                            {
                                bool flag32 = keyValuePair4.Key == value2;
                                if (flag32)
                                {
                                    gameObject3 = keyValuePair4.Value;
                                    break;
                                }
                            }
                            bool flag33 = gameObject3 != null && gameObject3.name.ToLower().Contains("fishshadow");
                            if (flag33)
                            {
                                list3.Add(value2);
                                list4.Add(keyValuePair3.Key);
                            }
                        }
                    }
                    foreach (GameObject gameObject4 in list3)
                    {
                        this.markerToTarget.Remove(gameObject4);
                        Object.Destroy(gameObject4);
                    }
                    foreach (int key2 in list4)
                    {
                        this.trackedObjectMarkers.Remove(key2);
                    }
                }
                this.CheckRadarAutoToggle();
                bool flag34 = this.isRadarActive;
                if (flag34)
                {
                    this.RunRadar();
                }
            }
            num += 40;
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), "Status: " + (this.isRadarActive ? "Active" : "Disabled"));
            num += 25;
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Quick Toggle: {this.keyToggleRadar}");
            num += 25;
            bool flag21 = this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 30f), "Force Refresh Scan") && this.isRadarActive;
            if (flag21)
            {
                this.RunRadar();
            }
            num += 40;

            // --- METEOR ESP SECTION ---
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), "METEORITE ESP");
            num += 25;

            string meteorText = this.showMeteorESP ? "ESP: ENABLED" : "ESP: DISABLED";
            if ((this.showMeteorESP ? this.DrawDangerActionButton(new Rect(20f, (float)num, 260f, 40f), meteorText) : this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 40f), meteorText)))
            {
                this.showMeteorESP = !this.showMeteorESP;
            }
            num += 50;

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Detected Meteors: {this.meteorList.Count}");
            num += 25;

            GUI.Label(new Rect(20f, (float)num, 260f, 40f), "Status: " + (this.showMeteorESP ? "Running" : "Idle"));
            num += 50;

            GUI.Label(new Rect(20f, (float)num, 260f, 120f), "  Credits: OG dll creator :)\n• breckdareck for ForagerRadar");
            return (float)num + 130f;
        }

        // Token: 0x0600000B RID: 11 RVA: 0x00003648 File Offset: 0x00001848
        private void MarkNearestBlueberryCollected()
        {
            Camera main = Camera.main;
            Transform transform = (main != null) ? main.transform : null;
            bool flag = transform == null;
            if (!flag)
            {
                Vector3 position = transform.position;
                int num = -1;
                float num2 = float.MaxValue;
                for (int i = 0; i < this.blueberryPositions.Length; i++)
                {
                    bool flag2 = this.blueberryCooldowns.ContainsKey(i) && Time.unscaledTime < this.blueberryCooldowns[i];
                    if (!flag2)
                    {
                        bool flag3 = this.blueberryJustCollected.ContainsKey(i) && Time.unscaledTime < this.blueberryJustCollected[i];
                        if (!flag3)
                        {
                            float num3 = Vector3.Distance(position, this.blueberryPositions[i]);
                            bool flag4 = num3 < num2 && num3 < 5f;
                            if (flag4)
                            {
                                num2 = num3;
                                num = i;
                            }
                        }
                    }
                }
                bool flag5 = num != -1;
                if (flag5)
                {
                    float unscaledTime = Time.unscaledTime;
                    this.blueberryJustCollected[num] = unscaledTime + 4f;
                    this.blueberryCooldowns[num] = unscaledTime + this.blueberryCooldownDuration;
                    this.blueberryHideUntil[num] = unscaledTime + 4f + 10f;
                }
            }
        }

        // Token: 0x0600000C RID: 12 RVA: 0x0000379C File Offset: 0x0000199C
        private void CheckManualBlueberryCollection()
        {
            GameObject gameObject = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn");
            bool flag = gameObject != null && gameObject.activeInHierarchy;
            if (flag)
            {
                Image component = gameObject.GetComponent<Image>();
                bool flag2 = component != null && component.sprite != null && component.sprite.name.ToLower().Contains("interaction_8");
                if (flag2)
                {
                    Button component2 = gameObject.GetComponent<Button>();
                    bool flag3 = component2 != null && component2 != this.lastBlueberryButton;
                    if (flag3)
                    {
                        this.lastBlueberryButton = component2;
                        component2.onClick.AddListener(new Action(delegate ()
                        {
                            this.MarkNearestBlueberryCollected();
                        }));
                    }
                }
            }
            else
            {
                this.lastBlueberryButton = null;
            }
        }

        // Token: 0x0600000D RID: 13 RVA: 0x0000386C File Offset: 0x00001A6C
        private void MarkNearestRaspberryCollected()
        {
            Camera main = Camera.main;
            Transform transform = (main != null) ? main.transform : null;
            bool flag = transform == null;
            if (!flag)
            {
                Vector3 position = transform.position;
                int num = -1;
                float num2 = float.MaxValue;
                for (int i = 0; i < this.raspberryPositions.Length; i++)
                {
                    bool flag2 = this.raspberryCooldowns.ContainsKey(i) && Time.unscaledTime < this.raspberryCooldowns[i];
                    if (!flag2)
                    {
                        bool flag3 = this.raspberryJustCollected.ContainsKey(i) && Time.unscaledTime < this.raspberryJustCollected[i];
                        if (!flag3)
                        {
                            float num3 = Vector3.Distance(position, this.raspberryPositions[i]);
                            bool flag4 = num3 < num2 && num3 < 5f;
                            if (flag4)
                            {
                                num2 = num3;
                                num = i;
                            }
                        }
                    }
                }
                bool flag5 = num != -1;
                if (flag5)
                {
                    float unscaledTime = Time.unscaledTime;
                    this.raspberryJustCollected[num] = unscaledTime + 4f;
                    this.raspberryCooldowns[num] = unscaledTime + this.raspberryCooldownDuration;
                    this.raspberryHideUntil[num] = unscaledTime + 4f + 10f;
                }
            }
        }

        // Token: 0x0600000E RID: 14 RVA: 0x000039C0 File Offset: 0x00001BC0
        private void CheckManualRaspberryCollection()
        {
            GameObject gameObject = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn");
            bool flag = gameObject != null && gameObject.activeInHierarchy;
            if (flag)
            {
                Image component = gameObject.GetComponent<Image>();
                bool flag2 = component != null && component.sprite != null;
                if (flag2)
                {
                    string text = component.sprite.name.ToLower();
                    bool flag3 = text.Contains("interaction_8");
                    if (flag3)
                    {
                        Button component2 = gameObject.GetComponent<Button>();
                        bool flag4 = component2 != null && component2 != this.lastRaspberryButton;
                        if (flag4)
                        {
                            this.lastRaspberryButton = component2;
                            component2.onClick.AddListener(new Action(delegate ()
                            {
                                this.MarkNearestRaspberryCollected();
                            }));
                        }
                    }
                }
            }
            else
            {
                this.lastRaspberryButton = null;
            }
        }

        // Token: 0x0600000F RID: 15 RVA: 0x00003A9C File Offset: 0x00001C9C
        private void CleanupExpiredCooldowns()
        {
            List<int> list = new List<int>();
            foreach (KeyValuePair<int, float> keyValuePair in this.blueberryCooldowns)
            {
                bool flag = Time.unscaledTime >= keyValuePair.Value;
                if (flag)
                {
                    list.Add(keyValuePair.Key);
                }
            }
            foreach (int key in list)
            {
                this.blueberryCooldowns.Remove(key);
                this.blueberryHideUntil.Remove(key);
                this.blueberryJustCollected.Remove(key);
                this.RunRadar();
            }
            list.Clear();
            foreach (KeyValuePair<int, float> keyValuePair2 in this.raspberryCooldowns)
            {
                bool flag2 = Time.unscaledTime >= keyValuePair2.Value;
                if (flag2)
                {
                    list.Add(keyValuePair2.Key);
                }
            }
            foreach (int key2 in list)
            {
                this.raspberryCooldowns.Remove(key2);
                this.raspberryHideUntil.Remove(key2);
                this.raspberryJustCollected.Remove(key2);
                this.RunRadar();
            }
        }

        // Token: 0x06000010 RID: 16 RVA: 0x00003C60 File Offset: 0x00001E60
        private void RunAutoCollectLogic()
        {
            GameObject gameObject = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn");
            bool flag = gameObject == null || !gameObject.activeInHierarchy;
            if (!flag)
            {
                Image component = gameObject.GetComponent<Image>();
                bool flag2 = component == null || component.sprite == null;
                if (!flag2)
                {
                    string text = component.sprite.name.ToLower();
                    bool flag3 = false;
                    bool flag4 = text.Contains("mushroom") && this.collectMushrooms;
                    if (flag4)
                    {
                        flag3 = true;
                    }
                    else
                    {
                        bool flag5 = text.Contains("interaction_8") && this.collectBerries;
                        if (flag5)
                        {
                            flag3 = true;
                        }
                        else
                        {
                            bool flag6 = this.collectOther && !text.Contains("mushroom") && !text.Contains("interaction_8");
                            if (flag6)
                            {
                                flag3 = true;
                            }
                        }
                    }
                    bool flag7 = flag3;
                    if (flag7)
                    {
                        Button component2 = gameObject.GetComponent<Button>();
                        bool flag8 = component2 != null && component2.interactable;
                        if (flag8)
                        {
                            component2.onClick.Invoke();
                            this.autoCollectClickedSinceArrival = true;
                            if (this.IsCurrentPriorityNodeNearby(8f))
                            {
                                this.priorityCollectClickedSinceArrival = true;
                            }
                            this.ClickButtonIfExists("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)/AniRoot@go@ani/exit@btn@go");
                        }
                    }
                }
            }
        }

        // Token: 0x06000011 RID: 17 RVA: 0x00003D94 File Offset: 0x00001F94
        private void RunAutoCookLogic()
        {
            foreach (string path in new string[]
            {
                "GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)/AniRoot@queueanimation/detail@t/btnBar@go/confirm@swapbtn",
                "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn",
                "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_cook_danger@list/CommonIconForCookDanger(Clone)/root_visible@go/icon@img@btn"
            })
            {
                this.ClickButtonIfExists(path);
            }
        }

        // Token: 0x06000012 RID: 18 RVA: 0x00003DE0 File Offset: 0x00001FE0
        private void ClickButtonIfExists(string path)
        {
            GameObject gameObject = GameObject.Find(path);
            bool flag = gameObject == null;
            if (!flag)
            {
                Button component = gameObject.GetComponent<Button>();
                bool flag2 = component != null && gameObject.activeInHierarchy && component.interactable;
                if (flag2)
                {
                    component.onClick.Invoke();
                }
            }
        }


        // Advanced Cooking Cleanup (sprite-based)
        private bool ClickCookingCleanup()
        {
            bool result = false;
            bool flag = false;
            string[] array = new string[]
            {
                "ui_common_btn_close",
                "ui_common_close",
                "btn_close"
            };

            this.cookImageScanBuffer.Clear();
            this.CollectImagesFromPath("GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)", this.cookImageScanBuffer);
            this.CollectImagesFromPath("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list", this.cookImageScanBuffer);
            this.CollectImagesFromPath("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_cook_danger@list", this.cookImageScanBuffer);
            if (this.cookImageScanBuffer.Count == 0)
            {
                return false;
            }

            foreach (Image image in this.cookImageScanBuffer)
            {
                if (!(image == null) && !(image.sprite == null) && image.gameObject.activeInHierarchy && image.sprite.name.Contains("ui_cooking_icon_time01"))
                {
                    flag = true;
                }
            }
            foreach (Image image2 in this.cookImageScanBuffer)
            {
                if (!(image2 == null) && !(image2.sprite == null) && image2.gameObject.activeInHierarchy)
                {
                    string name = image2.sprite.name;
                    foreach (string text in array)
                    {
                        if (name.Contains(text))
                        {
                            Button button = image2.GetComponent<Button>();
                            if (button == null)
                            {
                                button = image2.GetComponentInParent<Button>();
                            }
                            if (button != null && button.interactable)
                            {
                                button.onClick.Invoke();
                                return true;
                            }
                        }
                    }
                }
            }
            if (!flag)
            {
                foreach (Image image3 in this.cookImageScanBuffer)
                {
                    if (!(image3 == null) && !(image3.sprite == null) && image3.gameObject.activeInHierarchy)
                    {
                        string name2 = image3.sprite.name;
                        if (name2.Contains("ui_dynamic_interaction_900"))
                        {
                            result = true;
                            Button button2 = image3.GetComponent<Button>();
                            if (button2 == null)
                            {
                                button2 = image3.GetComponentInParent<Button>();
                            }
                            if (button2 != null && button2.interactable)
                            {
                                button2.onClick.Invoke();
                            }
                        }
                        else if (name2.Contains("ui_dynamic_interaction_902"))
                        {
                            result = true;
                            Button button3 = image3.GetComponent<Button>();
                            if (button3 == null)
                            {
                                button3 = image3.GetComponentInParent<Button>();
                            }
                            if (button3 != null && button3.interactable)
                            {
                                button3.onClick.Invoke();
                            }
                        }
                        else if (name2.Contains("ui_cooking_icon_time01"))
                        {
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        private bool ClickCookingCleanupThrottled(float interval)
        {
            if (Time.unscaledTime < this.nextCookingCleanupScanAt)
            {
                return this.lastCookingCleanupResult;
            }

            this.lastCookingCleanupResult = this.ClickCookingCleanup();
            this.nextCookingCleanupScanAt = Time.unscaledTime + Mathf.Max(0.05f, interval);
            return this.lastCookingCleanupResult;
        }

        private void CollectImagesFromPath(string path, List<Image> target)
        {
            GameObject root = GameObject.Find(path);
            if (root == null || !root.activeInHierarchy)
            {
                return;
            }

            Image[] imgs = root.GetComponentsInChildren<Image>(true);
            if (imgs == null || imgs.Length == 0)
            {
                return;
            }

            for (int i = 0; i < imgs.Length; i++)
            {
                Image img = imgs[i];
                if (img != null)
                {
                    target.Add(img);
                }
            }
        }

        private bool IsCurrentCookTimerActive()
        {
            string spriteName = this.GetCurrentCookInteractSpriteName();
            if (string.IsNullOrEmpty(spriteName))
            {
                return false;
            }

            bool timerActive = spriteName.Contains("ui_cooking_icon_time") || spriteName.Contains("cooking_icon_time") || spriteName.Contains("icon_time") || spriteName.Contains("timer") || spriteName.Contains("clock");
            if (timerActive)
            {
                this.lastCookingTimerSeenAt = Time.unscaledTime;
            }
            return timerActive;
        }

        private bool IsCurrentCookTakeoutReady()
        {
            string spriteName = this.GetCurrentCookInteractSpriteName();
            if (string.IsNullOrEmpty(spriteName))
            {
                return false;
            }

            return spriteName.Contains("ui_dynamic_interaction_902") || spriteName.Contains("heart");
        }

        private string GetCurrentCookInteractSpriteName()
        {
            GameObject gameObject = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn");
            if (gameObject == null || !gameObject.activeInHierarchy)
            {
                return string.Empty;
            }

            Image image = gameObject.GetComponent<Image>();
            if (image == null || image.sprite == null)
            {
                return string.Empty;
            }

            return image.sprite.name.ToLowerInvariant();
        }

        private void ClickCookInteractSafely()
        {
            GameObject gameObject = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn");
            if (gameObject == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            Image image = gameObject.GetComponent<Image>();
            if (image == null || image.sprite == null)
            {
                return;
            }

            string spriteName = image.sprite.name.ToLowerInvariant();
            bool timerActive = spriteName.Contains("ui_cooking_icon_time") || spriteName.Contains("cooking_icon_time") || spriteName.Contains("icon_time") || spriteName.Contains("timer") || spriteName.Contains("clock");
            if (timerActive)
            {
                this.lastCookingTimerSeenAt = Time.unscaledTime;
                return;
            }

            // While cook panel is open, avoid clicking generic interact icon.
            // Panel actions are handled by confirm/danger/cleanup buttons.
            if (this.IsCookPanelOpen())
            {
                return;
            }

            bool isTakeout = spriteName.Contains("ui_dynamic_interaction_902") || spriteName.Contains("heart");
            // Allow unknown non-timer/non-takeout interact icons for stove entry/start.
            if (isTakeout)
            {
                return;
            }

            bool isGlove = spriteName.Contains("ui_dynamic_interaction_902");
            bool timerRecentlySeen = (Time.unscaledTime - this.lastCookingTimerSeenAt) < this.GetCookTakeoutSafetyDelay();
            if (isGlove && timerRecentlySeen)
            {
                return;
            }

            Button button = gameObject.GetComponent<Button>();
            if (button != null && button.interactable)
            {
                button.onClick.Invoke();
            }
        }

        private void ClickCookPanelCloseIfOpen()
        {
            GameObject cookPanel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)");
            if (cookPanel == null || !cookPanel.activeInHierarchy)
            {
                return;
            }

            Button[] buttons = cookPanel.GetComponentsInChildren<Button>(true);
            if (buttons == null)
            {
                return;
            }

            foreach (Button btn in buttons)
            {
                if (btn == null || btn.gameObject == null || !btn.gameObject.activeInHierarchy || !btn.interactable)
                {
                    continue;
                }

                string n = btn.name.ToLowerInvariant();
                if (n.Contains("close") || n.Contains("back") || n.Contains("exit") || n.Contains("return"))
                {
                    btn.onClick.Invoke();
                    return;
                }
            }
        }

        private bool IsToolboxOpen()
        {
            GameObject detailButton = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/bottom_right_layout@go/handable_bar@go/toolbox@w@go/handable_detail@go/handable_detail@btn");
            return detailButton != null && detailButton.activeInHierarchy;
        }

        private bool IsToolsPanelOpen()
        {
            GameObject toolsPanel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/ToolsPanel(Clone)");
            return toolsPanel != null && toolsPanel.activeInHierarchy;
        }

        private void StartToolEquipRequest(int toolType)
        {
            this.pendingToolEquipType = toolType;
            this.pendingToolEquipUntil = Time.time + 3f;
            this.nextToolEquipAttemptAt = Time.time;
            this.ProcessPendingToolEquipRequest();
        }

        private void ProcessPendingToolEquipRequest()
        {
            if (this.pendingToolEquipType == 0)
            {
                return;
            }
            if (Time.time > this.pendingToolEquipUntil)
            {
                this.pendingToolEquipType = 0;
                return;
            }
            if (Time.time < this.nextToolEquipAttemptAt)
            {
                return;
            }

            this.nextToolEquipAttemptAt = Time.time + 0.2f;

            if (!this.IsToolboxOpen())
            {
                this.ClickButtonIfExistsWithParent("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/bottom_right_layout@go/handable_bar@go/handable@btn@ani");
                this.ClickButtonIfExistsWithParent("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/bottom_right_layout@go/handable_bar@go/toolbox@w@go/handable_detail@go/handable_detail@btn");
                return;
            }

            string friendlyName;
            string[] needles;
            if (this.pendingToolEquipType == 1)
            {
                friendlyName = "Axe";
                needles = new string[] { "axe_axe001", "axe" };
            }
            else if (this.pendingToolEquipType == 2)
            {
                friendlyName = "Net";
                needles = new string[] { "net_net001", "catchingnet", "bugnet", "net" };
            }
            else
            {
                friendlyName = "Fishing Rod";
                needles = new string[] { "rod_rod001", "fishingrod", "fishing_rod", "rod" };
            }

            bool clicked = this.TryEquipToolboxItemBySpriteAny(needles);

            if (clicked)
            {
                MelonLogger.Msg("[Tools] Equip click sent for " + friendlyName);
                this.AddMenuNotification($"Equip click: {friendlyName}", new Color(0.45f, 1f, 0.55f));
                this.CloseToolboxIfOpen();
                this.pendingToolEquipType = 0;
            }
        }

        private bool TryEquipToolboxItemBySpriteAny(string[] spriteNeedles)
        {
            GameObject toolboxRoot = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/bottom_right_layout@go/handable_bar@go/toolbox@w@go");
            if (toolboxRoot == null || !toolboxRoot.activeInHierarchy)
            {
                return false;
            }

            Image[] images = toolboxRoot.GetComponentsInChildren<Image>(true);
            if (images == null || images.Length == 0)
            {
                return false;
            }

            foreach (Image image in images)
            {
                if (image == null || image.sprite == null || !image.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string spriteName = image.sprite.name.ToLowerInvariant();
                bool isMatch = false;
                foreach (string needle in spriteNeedles)
                {
                    if (!string.IsNullOrEmpty(needle) && spriteName.Contains(needle.ToLowerInvariant()))
                    {
                        isMatch = true;
                        break;
                    }
                }
                if (!isMatch)
                {
                    continue;
                }

                Button button = image.GetComponent<Button>();
                if (button == null) button = image.GetComponentInParent<Button>();
                if (button != null && button.interactable && button.gameObject.activeInHierarchy)
                {
                    button.onClick.Invoke();
                    return true;
                }
            }

            return false;
        }

        private string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private bool IsSelectedToolInUse()
        {
            GameObject equippedGo = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/ToolsPanel(Clone)/content/infoBar/layout/equiped@go");
            if (equippedGo == null || !equippedGo.activeInHierarchy)
            {
                return false;
            }

            Text txt = equippedGo.GetComponent<Text>();
            if (txt == null)
            {
                txt = equippedGo.GetComponentInChildren<Text>(true);
            }
            if (txt == null || string.IsNullOrEmpty(txt.text))
            {
                return true; // Active badge with no readable text still indicates equipped.
            }

            string label = txt.text.Trim().ToLowerInvariant();
            return label.Contains("in use") || label.Contains("equipped");
        }

        private bool ClickButtonIfExistsWithParent(string path)
        {
            GameObject gameObject = GameObject.Find(path);
            if (gameObject == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            Button button = gameObject.GetComponent<Button>();
            if (button == null)
            {
                button = gameObject.GetComponentInParent<Button>();
            }
            if (button != null && button.interactable && button.gameObject.activeInHierarchy)
            {
                button.onClick.Invoke();
                return true;
            }

            return false;
        }

        private void CloseToolboxIfOpen()
        {
            if (this.ClickButtonIfExistsWithParent("GameApp/startup_root(Clone)/XDUIRoot/Full/ToolsPanel(Clone)/back@w/title/back/back@btn"))
            {
                return;
            }

            GameObject toolboxRoot = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/bottom_right_layout@go/handable_bar@go/toolbox@w@go");
            if (toolboxRoot == null || !toolboxRoot.activeInHierarchy)
            {
                return;
            }

            // Prefer explicit close/back buttons if present in current layout.
            Button[] buttons = toolboxRoot.GetComponentsInChildren<Button>(true);
            if (buttons != null)
            {
                foreach (Button btn in buttons)
                {
                    if (btn == null || btn.gameObject == null || !btn.gameObject.activeInHierarchy || !btn.interactable)
                    {
                        continue;
                    }

                    string n = btn.name.ToLowerInvariant();
                    if (n.Contains("close") || n.Contains("back") || n.Contains("exit") || n.Contains("return"))
                    {
                        btn.onClick.Invoke();
                        return;
                    }
                }
            }

            // Fallback: toggle toolbox detail/entry buttons to collapse it.
            if (this.ClickButtonIfExistsWithParent("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/bottom_right_layout@go/handable_bar@go/toolbox@w@go/handable_detail@go/handable_detail@btn"))
            {
                return;
            }

            this.ClickButtonIfExistsWithParent("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/bottom_right_layout@go/handable_bar@go/handable@btn@ani");
        }

        private void StartTreeFarm()
        {
            if (this.autoFarmActive)
            {
                this.autoFarmActive = false;
                this.autoFarmEnabled = false;
                this.farmState = HeartopiaComplete.AutoFarmState.Idle;
                this.autoFarmStatus = "READY";
            }
            this.treeFarmEnabled = true;
            this.treeFarmState = HeartopiaComplete.TreeFarmState.EquipAxe;
            this.treeFarmCurrentIndex = Mathf.Clamp(this.treeFarmCurrentIndex, 0, Math.Max(0, this.treeFarmPoints.Count - 1));
            this.treeFarmChopSent = 0;
            this.treeFarmNextActionAt = Time.time;
            this.treeFarmStatus = "Equipping Axe...";
            this.StartToolEquipRequest(1);
            this.AddMenuNotification("Tree Farm enabled", new Color(0.45f, 1f, 0.55f));
        }

        private void StopTreeFarm(string reason = "Idle")
        {
            this.treeFarmEnabled = false;
            this.treeFarmState = HeartopiaComplete.TreeFarmState.Idle;
            this.treeFarmChopSent = 0;
            this.treeFarmStatus = reason;
            this.CloseToolboxIfOpen();
            this.pendingToolEquipType = 0;
        }

        private void RunTreeFarmLogic()
        {
            if (!this.treeFarmEnabled)
            {
                return;
            }

            if (this.treeFarmPoints.Count == 0)
            {
                this.StopTreeFarm("No points");
                return;
            }

            if (Time.time < this.treeFarmNextActionAt)
            {
                return;
            }

            switch (this.treeFarmState)
            {
                case HeartopiaComplete.TreeFarmState.EquipAxe:
                    this.StartToolEquipRequest(1);
                    this.treeFarmStatus = "Waiting after axe equip...";
                    this.treeFarmState = HeartopiaComplete.TreeFarmState.WaitAfterEquip;
                    this.treeFarmNextActionAt = Time.time + 2f;
                    break;

                case HeartopiaComplete.TreeFarmState.WaitAfterEquip:
                    this.treeFarmState = HeartopiaComplete.TreeFarmState.TeleportToPoint;
                    this.treeFarmNextActionAt = Time.time;
                    break;

                case HeartopiaComplete.TreeFarmState.TeleportToPoint:
                    if (this.treeFarmCurrentIndex < 0 || this.treeFarmCurrentIndex >= this.treeFarmPoints.Count)
                    {
                        this.treeFarmCurrentIndex = 0;
                    }
                    TreeFarmPatrolPoint point = this.treeFarmPoints[this.treeFarmCurrentIndex];
                    Vector3 targetPos = point.Position.ToVector3();
                    Quaternion targetRot = point.Rotation.ToQuaternion();
                    MelonLogger.Msg($"[TreeFarm] Teleporting to point {this.treeFarmCurrentIndex + 1}/{this.treeFarmPoints.Count} at {targetPos}");
                    this.TeleportToLocation(targetPos, targetRot);
                    this.treeFarmStatus = $"Teleported to tree point {this.treeFarmCurrentIndex + 1}/{this.treeFarmPoints.Count}";
                    this.treeFarmState = HeartopiaComplete.TreeFarmState.WaitAfterTeleport;
                    this.treeFarmNextActionAt = Time.time + this.treeFarmArrivalDelay;
                    break;

                case HeartopiaComplete.TreeFarmState.WaitAfterTeleport:
                    GameObject player = GameObject.Find("p_player_skeleton(Clone)");
                    if (player != null)
                    {
                        Vector3 currentPos = player.transform.position;
                        MelonLogger.Msg($"[TreeFarm] After teleport, current position: {currentPos}");
                    }
                    this.treeFarmChopSent = 0;
                    this.treeFarmNoPromptAttempts = 0;
                    this.treeFarmState = HeartopiaComplete.TreeFarmState.ChopAtPoint;
                    this.treeFarmNextActionAt = Time.time;
                    break;

                case HeartopiaComplete.TreeFarmState.ChopAtPoint:
                    bool chopped = this.TryClickInteractPrompt();
                    if (chopped)
                    {
                        this.treeFarmChopSent++;
                        this.treeFarmNoPromptAttempts = 0;
                    }
                    else
                    {
                        this.treeFarmNoPromptAttempts++;
                    }

                    MelonLogger.Msg($"[TreeFarm] Chop attempt {this.treeFarmChopSent}/{this.treeFarmChopPressCount} - Success: {chopped}, NoPromptAttempts: {this.treeFarmNoPromptAttempts}");
                    this.treeFarmStatus = chopped
                        ? $"Chopping {this.treeFarmChopSent}/{this.treeFarmChopPressCount}..."
                        : "Waiting for chop prompt...";

                    if (this.treeFarmChopSent >= Math.Max(1, this.treeFarmChopPressCount))
                    {
                        MelonLogger.Msg($"[TreeFarm] Finished chopping at point {this.treeFarmCurrentIndex + 1}, moving to next");
                        this.treeFarmState = HeartopiaComplete.TreeFarmState.WaitNextPoint;
                        this.treeFarmNextActionAt = Time.time + this.treeFarmNextLocationWait;
                    }
                    else if (this.treeFarmNoPromptAttempts >= 20)
                    {
                        MelonLogger.Msg($"[TreeFarm] No chop action after 20 attempts at point {this.treeFarmCurrentIndex + 1}, skipping");
                        this.treeFarmStatus = "No chop action, skipping point...";
                        this.treeFarmState = HeartopiaComplete.TreeFarmState.WaitNextPoint;
                        this.treeFarmNextActionAt = Time.time + 0.3f;
                    }
                    else
                    {
                        this.treeFarmNextActionAt = Time.time + (chopped ? this.treeFarmChopPressGap : 0.15f);
                    }
                    break;

                case HeartopiaComplete.TreeFarmState.WaitNextPoint:
                    this.treeFarmCurrentIndex++;
                    if (this.treeFarmCurrentIndex >= this.treeFarmPoints.Count)
                    {
                        this.treeFarmCurrentIndex = 0;
                    }
                    this.treeFarmState = HeartopiaComplete.TreeFarmState.TeleportToPoint;
                    this.treeFarmNextActionAt = Time.time;
                    this.treeFarmStatus = "Moving to next point...";
                    break;
            }
        }

        private bool IsHoldingTool()
        {
            try
            {
                GameObject statusPanel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)");
                if (statusPanel == null || !statusPanel.activeInHierarchy)
                {
                    MelonLogger.Msg("[TreeFarm] IsHoldingTool: Status panel not found or not active");
                    return false;
                }

                Image[] images = statusPanel.GetComponentsInChildren<Image>(true);
                if (images == null || images.Length == 0)
                {
                    MelonLogger.Msg("[TreeFarm] IsHoldingTool: No images in status panel");
                    return false;
                }

                foreach (Image img in images)
                {
                    if (img == null || img.gameObject == null || !img.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    string fullPath = this.GetHierarchyPath(img.transform);
                    if (!string.IsNullOrEmpty(fullPath) &&
                        fullPath.Contains("/handable_bar@go/toolbox@w@go/handable@list/CommonIconForTool(Clone)/") &&
                        fullPath.Contains("/root_visible@go/icon@img@btn"))
                    {
                        MelonLogger.Msg($"[TreeFarm] IsHoldingTool: Found tool image at {fullPath}");
                        return true; // Holding a tool
                    }
                }
                MelonLogger.Msg("[TreeFarm] IsHoldingTool: No tool images found");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[TreeFarm] IsHoldingTool error: {ex.Message}");
            }
            return false;
        }

        private void WithdrawHeldTools()
        {
            try
            {
                GameObject statusPanel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)");
                if (statusPanel != null && statusPanel.activeInHierarchy)
                {
                    Button[] buttons = statusPanel.GetComponentsInChildren<Button>(true);
                    if (buttons != null && buttons.Length > 0)
                    {
                        foreach (Button btn in buttons)
                        {
                            if (btn == null || btn.gameObject == null || !btn.gameObject.activeInHierarchy || !btn.interactable)
                            {
                                continue;
                            }

                            string fullPath = this.GetHierarchyPath(btn.transform);
                            if (!string.IsNullOrEmpty(fullPath) &&
                                fullPath.Contains("/handable_bar@go/toolbox@w@go/handable@list/CommonIconForTool(Clone)/") &&
                                fullPath.Contains("/root_visible@go/icon@img@btn"))
                            {
                                btn.onClick.Invoke();
                                return; // Withdraw one tool at a time
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private bool TryClickInteractPrompt()
        {
            bool holdingTool = this.IsHoldingTool();
            MelonLogger.Msg($"[TreeFarm] TryClickInteractPrompt - Holding tool: {holdingTool}");

            try
            {
                GameObject trackingPanel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)");
                if (trackingPanel != null && trackingPanel.activeInHierarchy)
                {
                    Button[] buttons = trackingPanel.GetComponentsInChildren<Button>(true);
                    if (buttons != null && buttons.Length > 0)
                    {
                        foreach (Button btn in buttons)
                        {
                            if (btn == null || btn.gameObject == null || !btn.gameObject.activeInHierarchy || !btn.interactable)
                            {
                                continue;
                            }

                            string fullPath = this.GetHierarchyPath(btn.transform);
                            if (!string.IsNullOrEmpty(fullPath) &&
                                fullPath.Contains("/tracking_common@list/IconsBarWidget(Clone)/") &&
                                fullPath.Contains("/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn"))
                            {
                                MelonLogger.Msg("[TreeFarm] Found interact button in tracking panel, clicking");
                                btn.onClick.Invoke();
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[TreeFarm] Error in tracking panel search: {ex.Message}");
            }

            if (this.ClickButtonIfExistsReturn(INTERACT_PROMPT_BUTTON_PATH))
            {
                MelonLogger.Msg("[TreeFarm] Clicked interact button via path");
                return true;
            }

            // No interact icon: fallback to simulated Interact key (F).
            MelonLogger.Msg("[TreeFarm] No interact button, sending F key");
            this.SendFMessage();
            return true;
        }

        private bool IsCookPanelOpen()
        {
            GameObject cookPanel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)");
            return cookPanel != null && cookPanel.activeInHierarchy;
        }

        private void ClickCookRefreshButtonIfAvailable()
        {
            if (Time.unscaledTime - this.lastCookRefreshClickAt < 0.2f)
            {
                return;
            }

            GameObject cookPanel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)");
            if (cookPanel == null || !cookPanel.activeInHierarchy)
            {
                return;
            }

            Button[] buttons = cookPanel.GetComponentsInChildren<Button>(true);
            if (buttons == null || buttons.Length == 0)
            {
                return;
            }

            foreach (Button btn in buttons)
            {
                if (btn == null || btn.gameObject == null || !btn.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string name = btn.name.ToLowerInvariant();
                if (name.Contains("refresh@btn") && btn.interactable)
                {
                    btn.onClick.Invoke();
                    this.lastCookRefreshClickAt = Time.unscaledTime;
                    return;
                }
            }
        }

        private void ClickCookConfirmButtonIfAvailable()
        {
            if (Time.unscaledTime - this.lastCookConfirmClickAt < 0.3f)
            {
                return;
            }

            // Avoid hammering Start Cooking while current stove already has active timer.
            if (this.IsCurrentCookTimerActive())
            {
                return;
            }

            if (this.ClickButtonIfExistsReturn("GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)/AniRoot@queueanimation/detail@t/btnBar@go/confirm@swapbtn"))
            {
                this.lastCookConfirmClickAt = Time.unscaledTime;
            }
        }

        private void ClickCookDangerButtonIfAvailable()
        {
            if (Time.unscaledTime - this.lastCookRefreshClickAt < 0.3f)
            {
                return;
            }

            if (this.ClickButtonIfExistsReturn("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_cook_danger@list/CommonIconForCookDanger(Clone)/root_visible@go/icon@img@btn"))
            {
                this.lastCookRefreshClickAt = Time.unscaledTime;
            }
        }

        private bool ClickButtonIfExistsReturn(string path)
        {
            GameObject gameObject = GameObject.Find(path);
            if (gameObject == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            Button component = gameObject.GetComponent<Button>();
            if (component != null && component.interactable)
            {
                component.onClick.Invoke();
                return true;
            }

            return false;
        }

        private bool IsLoginPanelActive()
        {
            GameObject go = GameObject.Find(LOGIN_PANEL_PATH);
            return go != null && go.activeInHierarchy;
        }

        private bool IsLoginRoomPanelActive()
        {
            GameObject go = GameObject.Find(LOGIN_ROOM_PANEL_PATH);
            return go != null && go.activeInHierarchy;
        }

        private bool ClickFirstFriendJoinButton()
        {
            GameObject panel = GameObject.Find(LOGIN_ROOM_PANEL_PATH);
            if (panel == null || !panel.activeInHierarchy)
            {
                return false;
            }

            Button[] buttons = panel.GetComponentsInChildren<Button>(true);
            if (buttons == null || buttons.Length == 0)
            {
                return false;
            }

            foreach (Button btn in buttons)
            {
                if (btn == null || btn.gameObject == null || !btn.gameObject.activeInHierarchy || !btn.interactable)
                {
                    continue;
                }

                string path = this.GetHierarchyPath(btn.transform);
                if (string.IsNullOrEmpty(path) || !path.Contains("/friend@go/friend@btn"))
                {
                    continue;
                }

                btn.onClick.Invoke();
                return true;
            }

            return false;
        }

        private void StartLobbyAutoJoinFriend(string reason)
        {
            this.lobbyJoinState = HeartopiaComplete.LobbyJoinState.OpenRoomPanel;
            this.lobbyJoinInProgress = true;
            this.lobbyJoinIsMyTown = false;
            this.lobbyJoinRefreshAttempts = 0;
            this.lobbyJoinNextActionAt = Time.unscaledTime;
            this.lobbyAutoJoinStatus = "Starting auto join...";
            this.lobbyNextAutoJoinAttemptAt = Time.unscaledTime + 2f;
            this.AddMenuNotification($"Auto Join Friend: {reason}", new Color(0.55f, 0.88f, 1f));
        }

        private void StopLobbyAutoJoin(string status)
        {
            this.lobbyJoinInProgress = false;
            this.lobbyJoinIsMyTown = false;
            this.lobbyJoinState = HeartopiaComplete.LobbyJoinState.Idle;
            this.lobbyAutoJoinStatus = status;
            this.lobbyJoinNextActionAt = 0f;
            this.lobbyJoinRefreshAttempts = 0;
            this.lobbyNextAutoJoinAttemptAt = Time.unscaledTime + 2f;
        }

        private void StartLobbyAutoJoinMyTown(string reason)
        {
            this.lobbyJoinState = HeartopiaComplete.LobbyJoinState.OpenRoomPanel;
            this.lobbyJoinInProgress = true;
            this.lobbyJoinIsMyTown = true;
            this.lobbyJoinRefreshAttempts = 0;
            this.lobbyJoinNextActionAt = Time.unscaledTime;
            this.lobbyAutoJoinStatus = "Starting auto join My Town...";
            this.lobbyNextAutoJoinAttemptAt = Time.unscaledTime + 2f;
            this.AddMenuNotification($"Auto Join My Town: {reason}", new Color(0.55f, 0.88f, 1f));
        }

        private void RunLobbyAutoActions()
        {
            if (!this.IsLoginPanelActive())
            {
                if (this.lobbyJoinInProgress)
                {
                    this.StopLobbyAutoJoin("Stopped (left lobby)");
                }
                return;
            }

            if (this.autoJoinFriendEnabled && !this.lobbyJoinInProgress && Time.unscaledTime >= this.lobbyNextAutoJoinAttemptAt)
            {
                this.StartLobbyAutoJoinFriend("Auto mode");
            }

            if (this.autoClickStartEnabled && !this.autoJoinFriendEnabled && !this.lobbyJoinInProgress && Time.unscaledTime >= this.lobbyNextAutoStartClickAt)
            {
                if (!this.IsLoginRoomPanelActive() && this.ClickButtonIfExistsReturn(START_GAME_BUTTON_PATH))
                {
                    this.lobbyAutoJoinStatus = "Clicked Start";
                }
                this.lobbyNextAutoStartClickAt = Time.unscaledTime + 2f;
            }

            if (!this.lobbyJoinInProgress || Time.unscaledTime < this.lobbyJoinNextActionAt)
            {
                return;
            }

            switch (this.lobbyJoinState)
            {
                case HeartopiaComplete.LobbyJoinState.OpenRoomPanel:
                    if (this.IsLoginRoomPanelActive())
                    {
                        if (this.lobbyJoinIsMyTown)
                        {
                            this.lobbyJoinState = HeartopiaComplete.LobbyJoinState.SelectMyTownTab;
                        }
                        else
                        {
                            this.lobbyJoinState = HeartopiaComplete.LobbyJoinState.SelectFriendTab;
                        }
                        this.lobbyJoinNextActionAt = Time.unscaledTime + 0.3f;
                        this.lobbyAutoJoinStatus = "Room panel opened";
                        break;
                    }
                    if (this.ClickButtonIfExistsReturn(ROOM_ENTRY_BUTTON_PATH))
                    {
                        this.lobbyAutoJoinStatus = "Opening room panel...";
                        this.lobbyJoinNextActionAt = Time.unscaledTime + 0.6f;
                    }
                    else
                    {
                        this.StopLobbyAutoJoin("Room button not found");
                    }
                    break;

                case HeartopiaComplete.LobbyJoinState.SelectFriendTab:
                    this.ClickButtonIfExistsReturn(FRIEND_TAB_BUTTON_PATH);
                    this.lobbyAutoJoinStatus = "Selecting Friend's Town tab...";
                    this.lobbyJoinState = HeartopiaComplete.LobbyJoinState.ClickFriendJoin;
                    this.lobbyJoinNextActionAt = Time.unscaledTime + 0.4f;
                    break;

                case HeartopiaComplete.LobbyJoinState.ClickFriendJoin:
                    if (this.ClickFirstFriendJoinButton())
                    {
                        this.StopLobbyAutoJoin("Joined friend town");
                        break;
                    }

                    if (this.lobbyJoinRefreshAttempts < 2)
                    {
                        this.lobbyJoinState = HeartopiaComplete.LobbyJoinState.RefreshAndRetry;
                        this.lobbyJoinNextActionAt = Time.unscaledTime + 0.2f;
                        this.lobbyAutoJoinStatus = "No friend slot, refreshing...";
                    }
                    else
                    {
                        this.StopLobbyAutoJoin("No friend town found");
                    }
                    break;

                case HeartopiaComplete.LobbyJoinState.SelectMyTownTab:
                    this.ClickButtonIfExistsReturn("GameApp/startup_root(Clone)/XDUIRoot/Full/LoginRoomPanel(Clone)/AniRoot/popup/content/background/tab_bg/tabBar@w/tab@list/Viewport/Content/self@w/cell@btn");
                    this.lobbyAutoJoinStatus = "Selecting My Town tab...";
                    this.lobbyJoinState = HeartopiaComplete.LobbyJoinState.ClickMyTownJoin;
                    this.lobbyJoinNextActionAt = Time.unscaledTime + 0.4f;
                    break;

                case HeartopiaComplete.LobbyJoinState.ClickMyTownJoin:
                    if (this.ClickButtonIfExistsReturn("GameApp/startup_root(Clone)/XDUIRoot/Full/LoginRoomPanel(Clone)/AniRoot/popup/content/background/town@unbreakscroll/Content/RoomCellWidget/selfRoom@go/selfRoomEnter@btn"))
                    {
                        this.StopLobbyAutoJoin("Joined My Town");
                        break;
                    }
                    else
                    {
                        this.StopLobbyAutoJoin("My Town join button not found");
                    }
                    break;

                case HeartopiaComplete.LobbyJoinState.RefreshAndRetry:
                    this.ClickButtonIfExistsReturn(ROOM_REFRESH_BUTTON_PATH);
                    this.lobbyJoinRefreshAttempts++;
                    this.lobbyJoinState = HeartopiaComplete.LobbyJoinState.SelectFriendTab;
                    this.lobbyJoinNextActionAt = Time.unscaledTime + 0.7f;
                    this.lobbyAutoJoinStatus = $"Refresh {this.lobbyJoinRefreshAttempts}/2";
                    break;
            }
        }

        private float GetCookTakeoutSafetyDelay()
        {
            return Mathf.Max(this.cookingTakeoutSafetyDelay, this.gameSpeed * 0.06f);
        }

        // Player Distance Detection
        private float GetNearestPlayerDistance()
        {
            if (this.cachedPlayerObject == null || !this.cachedPlayerObject.activeInHierarchy)
            {
                this.cachedPlayerObject = GameObject.Find("p_player_skeleton(Clone)");
                if (this.cachedPlayerObject == null) return 999f;
            }

            Vector3 myPosition = this.cachedPlayerObject.transform.position;
            float nearest = 999f;

            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj == null) continue;

                // Find other player skeletons (not our own)
                if (obj.name.Contains("p_player_skeleton") && obj != this.cachedPlayerObject)
                {
                    float distance = Vector3.Distance(myPosition, obj.transform.position);
                    if (distance < nearest)
                    {
                        nearest = distance;
                    }
                }
            }

            return nearest;
        }

        // Token: 0x06000013 RID: 19 RVA: 0x00003E34 File Offset: 0x00002034
        private void RunBypassLogic(bool shouldHide)
        {
            bool targetState = !shouldHide;
            this.ManageObject(ref this.cacheStatusAnim, "GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation", targetState);
            this.ManageObject(ref this.cacheCookUI, "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_cook_normal@list", targetState);
            this.ManageObject(ref this.cacheSkeletonBody, "p_player_skeleton(Clone)/sk_player_player_skeleton", targetState);
        }

        // Token: 0x06000014 RID: 20 RVA: 0x00003E80 File Offset: 0x00002080
        private void ManageObject(ref GameObject cached, string path, bool targetState)
        {
            bool flag = cached == null;
            if (flag)
            {
                cached = GameObject.Find(path);
            }
            bool flag2 = cached != null && cached.activeSelf != targetState;
            if (flag2)
            {
                cached.SetActive(targetState);
            }
        }

        // Token: 0x06000015 RID: 21 RVA: 0x00003ECC File Offset: 0x000020CC
        private void RunAutoFarmLogic()
        {
            this.RefreshActivePriorityLocations();
            this.autoFarmTimer += Time.unscaledDeltaTime;
            this.priorityRecheckTimer += Time.unscaledDeltaTime;
            bool flag = this.cameraStuckDisplayTimer > 0f;
            if (flag)
            {
                this.cameraStuckDisplayTimer -= Time.unscaledDeltaTime;
            }
            switch (this.farmState)
            {
                case HeartopiaComplete.AutoFarmState.ScanningForNodes:
                    {
                        // Periodic recheck of priority locations
                        if (this.priorityRecheckTimer >= 60f) // 1 minute
                        {
                            this.priorityRecheckTimer = 0f;
                            Vector3? recheckLocation = this.GetActivePriorityLocation();
                            if (recheckLocation != null)
                            {
                                float distance = Vector3.Distance(Camera.main.transform.position, recheckLocation.Value);
                                this.autoFarmStatus = $"Rechecking priority location ({distance:F0}m)...";
                                this.TeleportToLocation(recheckLocation.Value);
                                this.currentPriorityLocation = recheckLocation;
                                this.lastTeleportWasPriorityLocation = true;
                                this.consecutivePriorityNoCollectCycles = 0;
                                this.farmState = HeartopiaComplete.AutoFarmState.WaitingForPriorityArea;
                                this.autoFarmTimer = 0f;
                                break;
                            }
                        }

                        // FIRST: Check for visible priority nodes
                        Vector3? priorityNode = this.FindClosestPriorityNode(Camera.main.transform.position, Time.unscaledTime);
                        if (priorityNode != null)
                        {
                            float distance = Vector3.Distance(Camera.main.transform.position, priorityNode.Value);
                            this.autoFarmStatus = $"Teleporting to priority node ({distance:F0}m)...";
                            this.TeleportToLocation(priorityNode.Value);
                            this.lastNodePosition = priorityNode.Value;
                            if (this.lastFoundPriorityNodeLocation.HasValue)
                            {
                                this.currentPriorityLocation = this.lastFoundPriorityNodeLocation;
                            }
                            this.lastTeleportWasPriorityLocation = this.currentPriorityLocation.HasValue;
                            this.farmState = HeartopiaComplete.AutoFarmState.Collecting;
                            this.autoFarmTimer = 0f;
                            this.autoCollectClickedSinceArrival = false;
                            this.priorityCollectClickedSinceArrival = false;
                            this.cameraRotationAttempts = 0;
                            break;
                        }

                        // SECOND: Only continue priority-location routing while a priority target is active.
                        Vector3? priorityLocation = this.currentPriorityLocation.HasValue ? this.GetActivePriorityLocation() : null;
                        if (priorityLocation != null)
                        {
                            float distance = Vector3.Distance(Camera.main.transform.position, priorityLocation.Value);
                            this.autoFarmStatus = $"Going to priority location ({distance:F0}m)...";
                            this.TeleportToLocation(priorityLocation.Value);
                            this.currentPriorityLocation = priorityLocation;
                            this.lastTeleportWasPriorityLocation = true;
                            this.consecutivePriorityNoCollectCycles = 0;
                            this.farmState = HeartopiaComplete.AutoFarmState.WaitingForPriorityArea;
                            this.autoFarmTimer = 0f;
                            break;
                        }

                        // THIRD: Normal scanning logic
                        Vector3? vector = this.FindClosestAvailableNode();
                        bool flag2 = vector != null;
                        if (flag2)
                        {
                            float value = Vector3.Distance(Camera.main.transform.position, vector.Value);
                            this.autoFarmStatus = $"Teleporting to node ({value:F0}m)...";
                            this.TeleportToLocation(vector.Value);
                            this.lastNodePosition = vector.Value;
                            this.lastTeleportWasPriorityLocation = false;
                            this.farmState = HeartopiaComplete.AutoFarmState.Collecting;
                            this.autoFarmTimer = 0f;
                            this.autoCollectClickedSinceArrival = false;
                            this.priorityCollectClickedSinceArrival = false;
                            this.cameraRotationAttempts = 0;
                        }
                        else
                        {
                            this.farmState = HeartopiaComplete.AutoFarmState.MovingToLocation;
                            this.autoFarmTimer = 0f;
                        }
                        break;
                    }
                case HeartopiaComplete.AutoFarmState.Collecting:
                    {
                        bool flag3 = this.autoFarmTimer >= 5f;
                        if (flag3)
                        {
                            this.recentlyVisitedNodes[this.lastNodePosition] = Time.unscaledTime + 15f;
                            this.FinishCollectingCycle();
                        }
                        else
                        {
                            bool flag4 = this.autoFarmTimer >= 3f;
                            if (flag4)
                            {
                                this.recentlyVisitedNodes[this.lastNodePosition] = Time.unscaledTime + 15f;
                                this.FinishCollectingCycle();
                            }
                            else
                            {
                                bool flag5 = !this.autoCollectClickedSinceArrival && !HeartopiaComplete.OverrideCameraPosition;
                                if (flag5)
                                {
                                    bool flag6 = this.autoFarmTimer >= 1f && this.cameraRotationAttempts == 0;
                                    if (flag6)
                                    {
                                        this.RotateCameraAroundPlayer(90f);
                                        this.cameraRotationAttempts = 1;
                                        this.autoFarmStatus = "Adjusting camera (90°)...";
                                        this.cameraStuckDisplayTimer = 2f;
                                    }
                                    else
                                    {
                                        bool flag7 = this.autoFarmTimer >= 1.75f && this.cameraRotationAttempts == 1;
                                        if (flag7)
                                        {
                                            this.RotateCameraAroundPlayer(90f);
                                            this.cameraRotationAttempts = 2;
                                            this.autoFarmStatus = "Adjusting camera (180°)...";
                                            this.cameraStuckDisplayTimer = 2f;
                                        }
                                        else
                                        {
                                            bool flag8 = this.autoFarmTimer >= 2.5f && this.cameraRotationAttempts == 2;
                                            if (flag8)
                                            {
                                                this.RotateCameraAroundPlayer(90f);
                                                this.cameraRotationAttempts = 3;
                                                this.autoFarmStatus = "Adjusting camera (270°)...";
                                                this.cameraStuckDisplayTimer = 2f;
                                            }
                                            else
                                            {
                                                bool flag9 = this.cameraRotationAttempts < 3;
                                                if (flag9)
                                                {
                                                    this.autoFarmStatus = $"Collecting... ({3f - this.autoFarmTimer:F1}s remaining)";
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    this.autoFarmStatus = $"Collecting... ({3f - this.autoFarmTimer:F1}s remaining)";
                                }
                            }
                        }
                        break;
                    }
                case HeartopiaComplete.AutoFarmState.MovingToLocation:
                    {
                        bool flag10 = this.farmLocations.Count == 0;
                        if (flag10)
                        {
                            this.autoFarmStatus = "No locations configured!";
                        }
                        else
                        {
                            bool flag11 = this.showMushroomRadar;
                            bool flag12 = this.showBlueberryRadar || this.showRaspberryRadar;
                            int num = this.currentLocationIndex;
                            HeartopiaComplete.FarmLocation farmLocation = null;
                            int num2 = 0;
                            HeartopiaComplete.FarmLocation farmLocation2;
                            for (; ; )
                            {
                                this.currentLocationIndex = (this.currentLocationIndex + 1) % this.farmLocations.Count;
                                farmLocation2 = this.farmLocations[this.currentLocationIndex];
                                bool flag13 = false;
                                bool flag14 = farmLocation2.Type == "any";
                                if (flag14)
                                {
                                    flag13 = true;
                                }
                                else
                                {
                                    bool flag15 = farmLocation2.Type == "both" && (flag11 || flag12);
                                    if (flag15)
                                    {
                                        flag13 = true;
                                    }
                                    else
                                    {
                                        bool flag16 = farmLocation2.Type == "mushroom" && flag11;
                                        if (flag16)
                                        {
                                            flag13 = true;
                                        }
                                        else
                                        {
                                            bool flag17 = farmLocation2.Type == "berry" && flag12;
                                            if (flag17)
                                            {
                                                flag13 = true;
                                            }
                                        }
                                    }
                                }
                                bool flag18 = flag13;
                                if (flag18)
                                {
                                    break;
                                }
                                num2++;
                                if (num2 >= this.farmLocations.Count)
                                {
                                    goto IL_4AB;
                                }
                            }
                            farmLocation = farmLocation2;
                        IL_4AB:
                            bool flag19 = farmLocation == null;
                            if (flag19)
                            {
                                this.autoFarmStatus = "No matching locations for enabled toggles!";
                            }
                            else
                            {
                                this.autoFarmStatus = "Moving to " + farmLocation.Name + "...";
                                this.TeleportToLocation(farmLocation.Position);
                                this.farmState = HeartopiaComplete.AutoFarmState.LoadingArea;
                                this.autoFarmTimer = 0f;
                            }
                        }
                        break;
                    }
                case HeartopiaComplete.AutoFarmState.LoadingArea:
                    {
                        bool flag20 = this.autoFarmTimer >= this.areaLoadDelay;
                        if (flag20)
                        {
                            this.farmState = HeartopiaComplete.AutoFarmState.WaitingForNodes;
                            this.autoFarmTimer = 0f;
                        }
                        else
                        {
                            this.autoFarmStatus = $"Loading area... ({this.areaLoadDelay - this.autoFarmTimer:F1}s remaining)";
                        }
                        break;
                    }
                case HeartopiaComplete.AutoFarmState.WaitingForNodes:
                    {
                        Vector3? vector2 = this.FindClosestAvailableNode();
                        bool flag21 = vector2 != null;
                        if (flag21)
                        {
                            float value2 = Vector3.Distance(Camera.main.transform.position, vector2.Value);
                            this.autoFarmStatus = $"Node found! Teleporting ({value2:F0}m)...";
                            this.TeleportToLocation(vector2.Value);
                            this.lastNodePosition = vector2.Value;
                            this.farmState = HeartopiaComplete.AutoFarmState.Collecting;
                            this.autoFarmTimer = 0f;
                            this.autoCollectClickedSinceArrival = false;
                            this.priorityCollectClickedSinceArrival = false;
                            this.cameraRotationAttempts = 0;
                        }
                        else
                        {
                            bool flag22 = this.autoFarmTimer >= 5f;
                            if (flag22)
                            {
                                this.autoFarmStatus = "No nodes found, cycling...";
                                this.farmState = HeartopiaComplete.AutoFarmState.MovingToLocation;
                                this.autoFarmTimer = 0f;
                            }
                            else
                            {
                                this.autoFarmStatus = $"Scanning for nodes... ({5f - this.autoFarmTimer:F1}s)";
                            }
                        }
                        break;
                    }
                case HeartopiaComplete.AutoFarmState.WaitingForPriorityArea:
                    {
                        bool flag23 = this.autoFarmTimer >= this.areaLoadDelay;
                        if (flag23)
                        {
                            // Start collecting at priority location
                            this.farmState = HeartopiaComplete.AutoFarmState.Collecting;
                            this.autoFarmTimer = 0f;
                            this.autoCollectClickedSinceArrival = false;
                            this.priorityCollectClickedSinceArrival = false;
                            this.cameraRotationAttempts = 0;
                            this.autoFarmStatus = "Farming at priority location...";
                        }
                        else
                        {
                            this.autoFarmStatus = $"Loading priority area... ({this.areaLoadDelay - this.autoFarmTimer:F1}s remaining)";
                        }
                        break;
                    }
            }
        }

        // Token: 0x06000016 RID: 22 RVA: 0x0000459C File Offset: 0x0000279C
        private Vector3? FindClosestAvailableNode()
        {
            bool flag = !this.isRadarActive || this.radarContainer == null;
            Vector3? result;
            if (flag)
            {
                result = null;
            }
            else
            {
                Vector3 position = Camera.main.transform.position;
                Vector3? vector = null;
                float num = float.MaxValue;
                float unscaledTime = Time.unscaledTime;
                List<Vector3> list = new List<Vector3>();
                foreach (KeyValuePair<Vector3, float> keyValuePair in this.recentlyVisitedNodes)
                {
                    bool flag2 = unscaledTime >= keyValuePair.Value;
                    if (flag2)
                    {
                        list.Add(keyValuePair.Key);
                    }
                }
                foreach (Vector3 key in list)
                {
                    this.recentlyVisitedNodes.Remove(key);
                }

                // Scan for all enabled items
                for (int i = 0; i < this.radarContainer.transform.childCount; i++)
                {
                    Transform child = this.radarContainer.transform.GetChild(i);
                    bool flag3 = child == null;
                    if (!flag3)
                    {
                        GameObject gameObject = child.gameObject;
                        TextMesh componentInChildren = gameObject.GetComponentInChildren<TextMesh>();
                        bool flag4 = componentInChildren == null;
                        if (!flag4)
                        {
                            string text = componentInChildren.text;
                            bool flag5 = text.Contains("[CD]");
                            if (!flag5)
                            {
                                bool flag6 = false;
                                foreach (Vector3 vector2 in this.recentlyVisitedNodes.Keys)
                                {
                                    bool flag7 = Vector3.Distance(child.position, vector2) < 2f;
                                    if (flag7)
                                    {
                                        flag6 = true;
                                        break;
                                    }
                                }
                                bool flag8 = flag6;
                                if (!flag8)
                                {
                                    bool flag9 = false;
                                    bool flag10 = this.showMushroomRadar && (text.Contains("Mushroom") || text.Contains("Oyster") || text.Contains("Button") || text.Contains("Penny Bun") || text.Contains("Shiitake") || text.Contains("Truffle"));
                                    if (flag10)
                                    {
                                        flag9 = true;
                                    }
                                    else
                                    {
                                        bool flag11 = text.Contains("Blueberry") && this.showBlueberryRadar;
                                        if (flag11)
                                        {
                                            flag9 = true;
                                        }
                                        else
                                        {
                                            bool flag12 = text.Contains("Raspberry") && this.showRaspberryRadar;
                                            if (flag12)
                                            {
                                                flag9 = true;
                                            }
                                            else
                                            {
                                                bool flag13 = text.Contains("Bubble") && this.showBubbleRadar;
                                                if (flag13)
                                                {
                                                    flag9 = true;
                                                }
                                                else
                                                {
                                                    bool flag14 = text.Contains("Insect") && this.showInsectRadar;
                                                    if (flag14)
                                                    {
                                                        flag9 = true;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    bool flag15 = !flag9;
                                    if (!flag15)
                                    {
                                        float num2 = Vector3.Distance(position, child.position);
                                        bool flag16 = num2 < num;
                                        if (flag16)
                                        {
                                            num = num2;
                                            vector = new Vector3?(child.position);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                result = vector;
            }
            return result;
        }

        private Vector3? FindClosestPriorityNode(Vector3 playerPos, float currentTime)
        {
            this.lastFoundPriorityNodeLocation = null;
            // Check if any priorities are enabled
            bool hasPriorities = this.priorityOysterMushroom || this.priorityButtonMushroom || this.priorityPennyBun ||
                                this.priorityShiitake || this.priorityTruffle || this.priorityBlueberry ||
                                this.priorityRaspberry || this.priorityBubble || this.priorityInsect;

            if (!hasPriorities)
            {
                return null; // No priorities set, return null to use normal scanning
            }

            Vector3? closestPriority = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < this.radarContainer.transform.childCount; i++)
            {
                Transform child = this.radarContainer.transform.GetChild(i);
                if (child == null) continue;

                GameObject gameObject = child.gameObject;
                TextMesh componentInChildren = gameObject.GetComponentInChildren<TextMesh>();
                if (componentInChildren == null) continue;

                string text = componentInChildren.text;
                if (text.Contains("[CD]")) continue; // Skip cooldown items

                // Check if recently visited
                bool isRecentlyVisited = false;
                foreach (Vector3 vector2 in this.recentlyVisitedNodes.Keys)
                {
                    if (Vector3.Distance(child.position, vector2) < 2f)
                    {
                        isRecentlyVisited = true;
                        break;
                    }
                }
                if (isRecentlyVisited) continue;

                // Check if this node matches a priority
                bool isPriorityMatch = false;

                if (this.priorityOysterMushroom && text.Contains("Oyster"))
                    isPriorityMatch = true;
                else if (this.priorityButtonMushroom && text.Contains("Button"))
                    isPriorityMatch = true;
                else if (this.priorityPennyBun && text.Contains("Penny Bun"))
                    isPriorityMatch = true;
                else if (this.priorityShiitake && text.Contains("Shiitake"))
                    isPriorityMatch = true;
                else if (this.priorityTruffle && text.Contains("Truffle"))
                    isPriorityMatch = true;
                else if (this.priorityBlueberry && text.Contains("Blueberry") && this.showBlueberryRadar)
                    isPriorityMatch = true;
                else if (this.priorityRaspberry && text.Contains("Raspberry") && this.showRaspberryRadar)
                    isPriorityMatch = true;
                else if (this.priorityBubble && text.Contains("Bubble") && this.showBubbleRadar)
                    isPriorityMatch = true;
                else if (this.priorityInsect && text.Contains("Insect") && this.showInsectRadar)
                    isPriorityMatch = true;

                if (isPriorityMatch)
                {
                    Vector3? mappedPriorityLocation = this.GetPriorityLocationForNodeText(text);
                    if (mappedPriorityLocation.HasValue && !this.IsPriorityLocationAvailable(mappedPriorityLocation.Value, currentTime))
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(playerPos, child.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestPriority = child.position;
                        this.lastFoundPriorityNodeLocation = mappedPriorityLocation;
                    }
                }
            }

            return closestPriority;
        }

        private bool IsPriorityLocationAvailable(Vector3 loc, float currentTime)
        {
            bool stillActive = false;
            for (int i = 0; i < this.activePriorityLocations.Count; i++)
            {
                if (this.activePriorityLocations[i] == loc)
                {
                    stillActive = true;
                    break;
                }
            }
            if (!stillActive)
            {
                return false;
            }

            return !this.priorityLocationCooldowns.ContainsKey(loc) || (currentTime - this.priorityLocationCooldowns[loc]) > 300f;
        }

        private Vector3? GetPriorityLocationForNodeText(string text)
        {
            if (text.Contains("Oyster")) return this.priorityLocations["Oyster Mushroom"];
            if (text.Contains("Button")) return this.priorityLocations["Button Mushroom"];
            if (text.Contains("Penny Bun")) return this.priorityLocations["Penny Bun"];
            if (text.Contains("Shiitake")) return this.priorityLocations["Shiitake"];
            if (text.Contains("Truffle")) return this.priorityLocations["Black Truffle"];
            if (text.Contains("Blueberry")) return this.priorityLocations["Blueberry"];
            if (text.Contains("Raspberry")) return this.priorityLocations["Raspberry"];
            return null;
        }

        private Vector3? GetActivePriorityLocation()
        {
            this.RefreshActivePriorityLocations();
            if (this.currentPriorityLocation.HasValue)
            {
                Vector3 value = this.currentPriorityLocation.Value;
                bool stillEnabled = false;
                for (int i = 0; i < this.activePriorityLocations.Count; i++)
                {
                    if (this.activePriorityLocations[i] == value)
                    {
                        stillEnabled = true;
                        break;
                    }
                }
                if (stillEnabled && (!this.priorityLocationCooldowns.ContainsKey(value) || (Time.unscaledTime - this.priorityLocationCooldowns[value]) > 300f))
                {
                    return value;
                }
            }
            // Return the first active priority location not on cooldown
            foreach (Vector3 loc in this.activePriorityLocations)
            {
                if (!this.priorityLocationCooldowns.ContainsKey(loc) || (Time.unscaledTime - this.priorityLocationCooldowns[loc]) > 300f)
                {
                    return loc;
                }
            }
            return null; // All on cooldown or none active
        }

        private void FinishCollectingCycle()
        {
            // Priority flow:
            // If no collect happened in a priority cycle, cooldown that priority location immediately.
            if (this.lastTeleportWasPriorityLocation && this.currentPriorityLocation.HasValue)
            {
                if (this.autoCollectClickedSinceArrival)
                {
                    this.consecutivePriorityNoCollectCycles = 0;
                    this.priorityLocationCooldowns.Remove(this.currentPriorityLocation.Value);
                }
                else
                {
                    this.priorityLocationCooldowns[this.currentPriorityLocation.Value] = Time.unscaledTime;
                    this.consecutivePriorityNoCollectCycles = 0;
                    this.currentPriorityLocation = null;
                }
            }

            this.priorityCollectClickedSinceArrival = false;
            this.lastTeleportWasPriorityLocation = false;
            this.farmState = HeartopiaComplete.AutoFarmState.ScanningForNodes;
            this.autoFarmTimer = 0f;
        }

        private bool IsCurrentPriorityNodeNearby(float maxDistance)
        {
            if (!this.currentPriorityLocation.HasValue || this.radarContainer == null || Camera.main == null)
            {
                return false;
            }

            string token = this.GetPriorityTokenForLocation(this.currentPriorityLocation.Value);
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            Vector3 playerPos = Camera.main.transform.position;
            for (int i = 0; i < this.radarContainer.transform.childCount; i++)
            {
                Transform child = this.radarContainer.transform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                TextMesh label = child.gameObject.GetComponentInChildren<TextMesh>();
                if (label == null || string.IsNullOrEmpty(label.text))
                {
                    continue;
                }

                string text = label.text;
                if (text.Contains("[CD]"))
                {
                    continue;
                }

                if (text.Contains(token) && Vector3.Distance(playerPos, child.position) <= maxDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetPriorityTokenForLocation(Vector3 loc)
        {
            if (loc == this.priorityLocations["Oyster Mushroom"]) return "Oyster";
            if (loc == this.priorityLocations["Button Mushroom"]) return "Button";
            if (loc == this.priorityLocations["Penny Bun"]) return "Penny Bun";
            if (loc == this.priorityLocations["Shiitake"]) return "Shiitake";
            if (loc == this.priorityLocations["Black Truffle"]) return "Truffle";
            if (loc == this.priorityLocations["Blueberry"]) return "Blueberry";
            if (loc == this.priorityLocations["Raspberry"]) return "Raspberry";
            return string.Empty;
        }

        private void RefreshActivePriorityLocations()
        {
            List<Vector3> newActive = new List<Vector3>();

            if (this.priorityOysterMushroom) newActive.Add(this.priorityLocations["Oyster Mushroom"]);
            if (this.priorityButtonMushroom) newActive.Add(this.priorityLocations["Button Mushroom"]);
            if (this.priorityPennyBun) newActive.Add(this.priorityLocations["Penny Bun"]);
            if (this.priorityShiitake) newActive.Add(this.priorityLocations["Shiitake"]);
            if (this.priorityTruffle) newActive.Add(this.priorityLocations["Black Truffle"]);
            if (this.priorityBlueberry) newActive.Add(this.priorityLocations["Blueberry"]);
            if (this.priorityRaspberry) newActive.Add(this.priorityLocations["Raspberry"]);

            this.activePriorityLocations = newActive;

            // Remove cooldowns for locations that are no longer enabled.
            List<Vector3> stale = new List<Vector3>();
            foreach (Vector3 loc in this.priorityLocationCooldowns.Keys)
            {
                bool stillActive = false;
                for (int i = 0; i < this.activePriorityLocations.Count; i++)
                {
                    if (this.activePriorityLocations[i] == loc)
                    {
                        stillActive = true;
                        break;
                    }
                }
                if (!stillActive)
                {
                    stale.Add(loc);
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                this.priorityLocationCooldowns.Remove(stale[i]);
            }
        }

        // Token: 0x06000017 RID: 23 RVA: 0x00004900 File Offset: 0x00002B00
        private void ToggleRadar()
        {
            this.isRadarActive = !this.isRadarActive;
            bool flag = !this.isRadarActive;
            if (flag)
            {
                this.Cleanup();
            }
            else
            {
                this.lastScanTime = Time.unscaledTime;
                this.RunRadar();
            }
            MelonLogger.Msg(this.isRadarActive ? "Radar Active" : "Radar Cleaned Up");
        }

        // Token: 0x06000018 RID: 24 RVA: 0x00004964 File Offset: 0x00002B64
        private void CheckRadarAutoToggle()
        {
            bool flag = this.showMushroomRadar || this.showBlueberryRadar || this.showRaspberryRadar || this.showBubbleRadar || this.showInsectRadar || this.showFishShadowRadar;
            // REMOVED AUTO-ENABLE: Checking a radar type will NOT automatically turn on the radar
            // You must manually press the "ENABLE RADAR" button to activate
            bool flag3 = !flag && this.isRadarActive;
            if (flag3)
            {
                this.isRadarActive = false;
                this.Cleanup();
                MelonLogger.Msg("Radar Auto-Disabled");
            }
            bool flag4 = !this.autoFarmActive;
            if (flag4)
            {
                bool flag5 = flag;
                if (flag5)
                {
                    this.autoFarmStatus = "READY";
                }
                else
                {
                    this.autoFarmStatus = "NO_TOGGLES";
                }
            }
        }

        // Token: 0x06000019 RID: 25 RVA: 0x00004A34 File Offset: 0x00002C34
        private void ToggleAutoFarm()
        {
            bool flag = this.showMushroomRadar || this.showBlueberryRadar || this.showRaspberryRadar || this.showBubbleRadar || this.showInsectRadar || this.showFishShadowRadar;
            
            // Fix: Check if Radar is active before enabling Auto Farm
            if (!this.autoFarmActive)
            {
                if (!flag)
                {
                    this.autoFarmStatus = "NO_TOGGLES_ERROR";
                    return;
                }
                if (!this.isRadarActive)
                {
                    this.autoFarmStatus = "RADAR_OFF_ERROR";
                    return;
                }
            }

            this.autoFarmActive = !this.autoFarmActive;
            bool flag3 = this.autoFarmActive;
            if (flag3)
            {
                this.autoFarmEnabled = true;
                this.gameSpeed = 5f;
                this.CheckRadarAutoToggle(); // This won't auto-enable radar, but checks consistency
                this.farmState = HeartopiaComplete.AutoFarmState.ScanningForNodes;
                this.autoFarmStatus = "Starting Auto Farm...";
                this.autoFarmTimer = 0f;
                this.currentLocationIndex = 0;
                this.recentlyVisitedNodes.Clear();
                this.cameraRotationAttempts = 0;
                this.priorityLocationCooldowns.Clear();
                this.RefreshActivePriorityLocations();
                this.currentPriorityLocation = this.GetActivePriorityLocation();
                this.lastTeleportWasPriorityLocation = false;
                this.consecutivePriorityNoCollectCycles = 0;
                this.priorityCollectClickedSinceArrival = false;
                this.priorityRecheckTimer = 0f; // Reset recheck timer
                MelonLogger.Msg("[AUTO FARM] Enabled");
            }
            else
            {
                this.farmState = HeartopiaComplete.AutoFarmState.Idle;
                this.autoFarmStatus = "READY";
                this.autoFarmTimer = 0f;
                this.autoFarmEnabled = false;
                this.gameSpeed = 1f;
                HeartopiaComplete.OverrideCameraPosition = false;
                this.cameraOverrideFramesRemaining = 0;
                this.currentPriorityLocation = null;
                this.lastTeleportWasPriorityLocation = false;
                this.consecutivePriorityNoCollectCycles = 0;
                this.priorityCollectClickedSinceArrival = false;
                MelonLogger.Msg("[AUTO FARM] Disabled");
            }
        }

        // Token: 0x0600001A RID: 26 RVA: 0x00004B54 File Offset: 0x00002D54
        private void RunRadar()
        {
            bool flag = this.radarContainer == null;
            if (flag)
            {
                this.radarContainer = new GameObject("Universal_Mushroom_Radar");
                Object.DontDestroyOnLoad(this.radarContainer);
            }
            else
            {
                List<GameObject> list = new List<GameObject>();
                List<GameObject> list2 = new List<GameObject>();
                foreach (KeyValuePair<GameObject, GameObject> keyValuePair in this.markerToTarget.ToList<KeyValuePair<GameObject, GameObject>>())
                {
                    bool flag2 = keyValuePair.Key == null || keyValuePair.Value == null;
                    if (flag2)
                    {
                        list2.Add(keyValuePair.Key);
                    }
                }
                foreach (GameObject key in list2)
                {
                    this.markerToTarget.Remove(key);
                }
                for (int i = this.radarContainer.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = this.radarContainer.transform.GetChild(i);
                    bool flag3 = child == null;
                    if (!flag3)
                    {
                        GameObject gameObject = child.gameObject;
                        bool flag4 = gameObject.name.StartsWith("TrackedMarker_");
                        bool flag5 = !flag4;
                        if (flag5)
                        {
                            list.Add(gameObject);
                        }
                    }
                }
                foreach (GameObject gameObject2 in list)
                {
                    Object.Destroy(gameObject2);
                }
            }
            Il2CppBindingFlags bindingFlags = (Il2CppBindingFlags)62;
            Il2CppType type = Il2CppType.GetType("ScriptsRefactory.BaseService.RenderSystem.Brg.BrgManager, Client");
            Il2CppObject @object;
            if (type == null)
            {
                @object = null;
            }
            else
            {
                Il2CppFieldInfo field = type.GetField("_manager", bindingFlags);
                @object = ((field != null) ? (Il2CppObject)field.GetValue(null) : null);
            }
            Il2CppObject object2 = @object;
            Material material = new Material(Shader.Find("Hidden/Internal-Colored"));
            material.SetInt("_ZTest", 0);
            Material material2 = new Material(Shader.Find("Hidden/Internal-Colored"));
            material2.SetInt("_ZTest", 0);
            material2.SetInt("_SrcBlend", 5);
            material2.SetInt("_DstBlend", 10);
            material2.SetInt("_ZWrite", 0);
            Vector3 position = Camera.main.transform.position;
            bool flag6 = this.showBlueberryRadar;
            if (flag6)
            {
                float unscaledTime = Time.unscaledTime;
                int j = 0;
                while (j < this.blueberryPositions.Length)
                {
                    Vector3 vector = this.blueberryPositions[j];
                    bool flag7 = Vector3.Distance(position, vector) <= 80f;
                    if (flag7)
                    {
                        bool flag8 = this.blueberryHideUntil.ContainsKey(j) && unscaledTime < this.blueberryHideUntil[j];
                        if (flag8)
                        {
                            float num = this.blueberryHideUntil[j] - 10f - 4f;
                            float num2 = num + 4f;
                            bool flag9 = unscaledTime >= num2;
                            if (flag9)
                            {
                                goto IL_38F;
                            }
                        }
                        bool flag10 = this.blueberryCooldowns.ContainsKey(j) && unscaledTime < this.blueberryCooldowns[j];
                        bool flag11 = flag10 && (!this.blueberryHideUntil.ContainsKey(j) || unscaledTime >= this.blueberryHideUntil[j]);
                        if (flag11)
                        {
                            this.CreateMarker(vector, "blueberry_cooldown", material, material2, null);
                        }
                        else
                        {
                            bool flag12 = !flag10;
                            if (flag12)
                            {
                                this.CreateMarker(vector, "blueberry", material, material2, null);
                            }
                        }
                    }
                IL_38F:
                    j++;
                    continue;
                    goto IL_38F;
                }
            }
            bool flag13 = this.showRaspberryRadar;
            if (flag13)
            {
                float unscaledTime2 = Time.unscaledTime;
                int k = 0;
                while (k < this.raspberryPositions.Length)
                {
                    Vector3 vector2 = this.raspberryPositions[k];
                    bool flag14 = Vector3.Distance(position, vector2) <= 80f;
                    if (flag14)
                    {
                        bool flag15 = this.raspberryHideUntil.ContainsKey(k) && unscaledTime2 < this.raspberryHideUntil[k];
                        if (flag15)
                        {
                            float num3 = this.raspberryHideUntil[k] - 10f - 4f;
                            float num4 = num3 + 4f;
                            bool flag16 = unscaledTime2 >= num4;
                            if (flag16)
                            {
                                goto IL_4E9;
                            }
                        }
                        bool flag17 = this.raspberryCooldowns.ContainsKey(k) && unscaledTime2 < this.raspberryCooldowns[k];
                        bool flag18 = flag17 && (!this.raspberryHideUntil.ContainsKey(k) || unscaledTime2 >= this.raspberryHideUntil[k]);
                        if (flag18)
                        {
                            this.CreateMarker(vector2, "raspberry_cooldown", material, material2, null);
                        }
                        else
                        {
                            bool flag19 = !flag17;
                            if (flag19)
                            {
                                this.CreateMarker(vector2, "raspberry", material, material2, null);
                            }
                        }
                    }
                IL_4E9:
                    k++;
                    continue;
                    goto IL_4E9;
                }
            }
            bool flag20 = this.showBubbleRadar;
            if (flag20)
            {
                GameObject[] array = Object.FindObjectsOfType<GameObject>();
                foreach (GameObject gameObject3 in array)
                {
                    bool flag21 = gameObject3 == null || gameObject3.name == null;
                    if (!flag21)
                    {
                        string text = gameObject3.name.ToLower();
                        bool flag22 = text.Contains("p_bubble_bubble_") && text.Contains("(clone)");
                        if (flag22)
                        {
                            int instanceID = gameObject3.GetInstanceID();
                            bool flag23 = !this.trackedObjectMarkers.ContainsKey(instanceID);
                            if (flag23)
                            {
                                this.CreateMarker(gameObject3.transform.position, "bubble", material, material2, gameObject3);
                                this.trackedObjectMarkers[instanceID] = this.markerToTarget.Keys.Last<GameObject>();
                            }
                        }
                    }
                }
            }
            bool flag24 = this.showInsectRadar;
            if (flag24)
            {
                GameObject[] array3 = Object.FindObjectsOfType<GameObject>();
                List<int> list3 = new List<int>();
                foreach (KeyValuePair<int, GameObject> keyValuePair2 in this.trackedObjectMarkers)
                {
                    bool flag25 = keyValuePair2.Value == null;
                    if (flag25)
                    {
                        list3.Add(keyValuePair2.Key);
                    }
                }
                foreach (int key2 in list3)
                {
                    this.trackedObjectMarkers.Remove(key2);
                }
                foreach (GameObject gameObject4 in array3)
                {
                    bool flag26 = gameObject4 == null || gameObject4.name == null;
                    if (!flag26)
                    {
                        string text2 = gameObject4.name.ToLower();
                        bool flag27 = text2.Contains("p_insect_insect") && text2.Contains("(clone)");
                        if (flag27)
                        {
                            int instanceID2 = gameObject4.GetInstanceID();
                            bool flag28 = !this.trackedObjectMarkers.ContainsKey(instanceID2);
                            if (flag28)
                            {
                                this.CreateMarker(gameObject4.transform.position, "insect", material, material2, gameObject4);
                                this.trackedObjectMarkers[instanceID2] = this.markerToTarget.Keys.Last<GameObject>();
                            }
                        }
                    }
                }
            }
            bool flag29 = this.showFishShadowRadar;
            if (flag29)
            {
                GameObject[] array4 = Object.FindObjectsOfType<GameObject>();
                List<int> list5 = new List<int>();
                foreach (KeyValuePair<int, GameObject> keyValuePair3 in this.trackedObjectMarkers)
                {
                    bool flag30 = keyValuePair3.Value == null;
                    if (flag30)
                    {
                        list5.Add(keyValuePair3.Key);
                    }
                }
                foreach (int key3 in list5)
                {
                    this.trackedObjectMarkers.Remove(key3);
                }
                foreach (GameObject gameObject5 in array4)
                {
                    bool flag31 = gameObject5 == null || gameObject5.name == null;
                    if (!flag31)
                    {
                        string text3 = gameObject5.name.ToLower();
                        bool flag32 = text3.Contains("fishshadow") && text3.Contains("(clone)");
                        if (flag32)
                        {
                            int instanceID3 = gameObject5.GetInstanceID();
                            bool flag33 = !this.trackedObjectMarkers.ContainsKey(instanceID3);
                            if (flag33)
                            {
                                this.CreateMarker(gameObject5.transform.position, "fishshadow", material, material2, gameObject5);
                                this.trackedObjectMarkers[instanceID3] = this.markerToTarget.Keys.Last<GameObject>();
                            }
                        }
                    }
                }
            }
            bool flag34 = object2 != null;
            if (this.showMushroomRadar)
            {
                if (flag34)
                {
                    HashSet<string> hashSet = new HashSet<string>();
                    Il2CppReferenceArray<Il2CppFieldInfo> fields = object2.GetIl2CppType().GetFields(bindingFlags);
                    for (int n = 0; n < fields.Count; n++)
                    {
                        Il2CppObject value = fields[n].GetValue(object2);
                        Il2CppObject object3;
                        if (value == null)
                        {
                            object3 = null;
                        }
                        else
                        {
                            Il2CppFieldInfo field2 = value.GetIl2CppType().GetField("_brgData", bindingFlags);
                            object3 = ((field2 != null) ? field2.GetValue(value) : null);
                        }
                        Il2CppObject object4 = object3;
                        Il2CppObject object5;
                        if (object4 == null)
                        {
                            object5 = null;
                        }
                        else
                        {
                            Il2CppFieldInfo field3 = object4.GetIl2CppType().GetField("CycleEntities", bindingFlags);
                            object5 = ((field3 != null) ? field3.GetValue(object4) : null);
                        }
                        Il2CppObject object6 = object5;
                        bool flag30 = object6 != null;
                        if (flag30)
                        {
                            Il2CppType il2CppType = object6.GetIl2CppType();
                            int num5 = il2CppType.GetProperty("Count").GetValue(object6).Unbox<int>();
                            for (int num6 = 0; num6 < num5; num6++)
                            {
                                try
                                {
                                    Il2CppObject boxedIndex = this.BoxInt(num6);
                                    Il2CppObject object7 = il2CppType.GetMethod("get_Item").Invoke(object6, new Il2CppReferenceArray<Il2CppObject>(new Il2CppObject[]
                                    {
                                        boxedIndex
                                    }));
                                    string meshName = this.GetMeshName(object7, bindingFlags);
                                    bool flag31 = meshName.ToLower().Contains("dynamicbush");
                                    if (flag31)
                                    {
                                        Il2CppFieldInfo field4 = object7.GetIl2CppType().GetField("blocks", bindingFlags);
                                        Il2CppObject object8 = (field4 != null) ? field4.GetValue(object7) : null;
                                        bool flag32 = object8 != null;
                                        if (flag32)
                                        {
                                            int num7 = object8.GetIl2CppType().GetProperty("Count").GetValue(object8).Unbox<int>();
                                            for (int num8 = 0; num8 < num7; num8++)
                                            {
                                                Il2CppObject boxedBlockIndex = this.BoxInt(num8);
                                                Il2CppObject block = object8.GetIl2CppType().GetMethod("get_Item").Invoke(object8, new Il2CppReferenceArray<Il2CppObject>(new Il2CppObject[]
                                                {
                                                    boxedBlockIndex
                                                }));
                                                Vector3 blockPos = this.GetBlockPos(block, bindingFlags);
                                                string item = $"{blockPos.x:F1}{blockPos.z:F1}";
                                                bool flag33 = !hashSet.Contains(item);
                                                if (flag33)
                                                {
                                                    hashSet.Add(item);
                                                    this.CreateMarker(blockPos, meshName, material, material2, null);
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    MelonLogger.Msg($"Error processing item {num6}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
        }

        // Token: 0x06000018 RID: 24 RVA: 0x00005598 File Offset: 0x00003798
        private void CreateMarker(Vector3 pos, string meshName, Material xRay, Material bg, GameObject targetObject = null)
        {
            bool flag = meshName.Contains("step0") || meshName == "blueberry_cooldown" || meshName == "raspberry_cooldown";
            string text = meshName.ToLower();
            string text2 = "Mushroom";
            string icon = "▲"; // Default mushroom icon
            Color endColor = Color.white;
            Color bgColor = new Color(0.5f, 0.5f, 0.5f, 0.7f); // Default gray background
            bool flag2 = meshName == "blueberry" || meshName == "blueberry_cooldown";
            if (flag2)
            {
                text2 = "Blueberry";
                icon = "●";
                endColor = new Color(0.3f, 0.5f, 1f); // Light blue
                bgColor = new Color(0.1f, 0.2f, 0.5f, 0.85f);
            }
            else
            {
                bool flag3 = meshName == "raspberry" || meshName == "raspberry_cooldown";
                if (flag3)
                {
                    text2 = "Raspberry";
                    icon = "●";
                    endColor = new Color(1f, 0.3f, 0.4f); // Light red
                    bgColor = new Color(0.5f, 0.1f, 0.15f, 0.85f);
                }
                else
                {
                    bool flag4 = meshName == "bubble";
                    if (flag4)
                    {
                        text2 = "Bubble";
                        icon = "◯";
                        endColor = new Color(1f, 0.5f, 1f); // Light magenta
                        bgColor = new Color(0.4f, 0.1f, 0.4f, 0.85f);
                    }
                    else
                    {
                        bool flag5 = meshName == "insect";
                        if (flag5)
                        {
                            text2 = "Insect";
                            icon = "✦";
                            endColor = new Color(1f, 0.8f, 0.4f); // Light orange
                            bgColor = new Color(0.5f, 0.3f, 0.1f, 0.85f);
                        }
                        else
                        {
                            bool flagTree = meshName == "tree";
                            if (flagTree)
                            {
                                text2 = "Tree";
                                icon = "●";
                                endColor = new Color(0.72f, 0.86f, 1f);
                                bgColor = new Color(0.05f, 0.22f, 0.72f, 0.86f);
                            }
                            else
                            {
                            bool flag35 = meshName == "fishshadow";
                            if (flag35)
                            {
                                text2 = "Fish Shadow";
                                icon = "🐟";
                                endColor = new Color(0.2f, 0.6f, 1f); // Light blue
                                bgColor = new Color(0.05f, 0.2f, 0.5f, 0.85f);
                            }
                            else
                            {
                                bool flag6 = text.Contains("pleurotus");
                                if (flag6)
                                {
                                    text2 = "Oyster";
                                    icon = "▲";
                                    endColor = new Color(0.5f, 1f, 1f); // Light cyan
                                    bgColor = new Color(0.1f, 0.4f, 0.4f, 0.85f);
                                }
                                else
                                {
                                    bool flag7 = text.Contains("tricholoma");
                                    if (flag7)
                                    {
                                        text2 = "Button";
                                        icon = "▲";
                                        endColor = new Color(0.6f, 1f, 0.6f); // Light green
                                        bgColor = new Color(0.15f, 0.4f, 0.15f, 0.85f);
                                    }
                                    else
                                    {
                                        bool flag8 = text.Contains("boletus");
                                        if (flag8)
                                        {
                                            text2 = "Penny Bun";
                                            icon = "▲";
                                            endColor = new Color(0.9f, 0.7f, 1f); // Light purple
                                            bgColor = new Color(0.35f, 0.2f, 0.5f, 0.85f);
                                        }
                                        else
                                        {
                                            bool flag9 = text.Contains("shiitake");
                                            if (flag9)
                                            {
                                                text2 = "Shiitake";
                                                icon = "▲";
                                                endColor = new Color(1f, 0.7f, 0.5f); // Light orange-brown
                                                bgColor = new Color(0.5f, 0.25f, 0.1f, 0.85f);
                                            }
                                            else
                                            {
                                                bool flag10 = text.Contains("truffle");
                                                if (flag10)
                                                {
                                                    text2 = "Truffle";
                                                    icon = "◆";
                                                    endColor = new Color(1f, 1f, 0.5f); // Light yellow
                                                    bgColor = new Color(0.5f, 0.5f, 0.1f, 0.85f);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            }
                        }
                    }
                }
            }
            bool flag11 = flag;
            if (flag11)
            {
                endColor = new Color(1f, 0.3f, 0.3f); // Red for cooldown
                bgColor = new Color(0.5f, 0.05f, 0.05f, 0.9f); // Dark red background
            }
            GameObject gameObject = new GameObject("ItemMarker");
            gameObject.transform.position = pos;
            gameObject.transform.SetParent(this.radarContainer.transform);
            bool flag12 = targetObject != null;
            if (flag12)
            {
                gameObject.name = "TrackedMarker_" + targetObject.GetInstanceID().ToString();
                this.markerToTarget[gameObject] = targetObject;
            }
            LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = xRay;
            lineRenderer.useWorldSpace = false;
            lineRenderer.startWidth = (lineRenderer.endWidth = 0.08f);
            lineRenderer.positionCount = 5;
            endColor.a = 0.8f;
            lineRenderer.startColor = (lineRenderer.endColor = endColor);
            lineRenderer.SetPosition(0, new Vector3(-0.6f, 0.1f, -0.6f));
            lineRenderer.SetPosition(1, new Vector3(0.6f, 0.1f, -0.6f));
            lineRenderer.SetPosition(2, new Vector3(0.6f, 0.1f, 0.6f));
            lineRenderer.SetPosition(3, new Vector3(-0.6f, 0.1f, 0.6f));
            lineRenderer.SetPosition(4, new Vector3(-0.6f, 0.1f, -0.6f));
            GameObject gameObject2 = new GameObject("LabelAnchor");
            gameObject2.transform.SetParent(gameObject.transform);
            gameObject2.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            GameObject gameObject3 = GameObject.CreatePrimitive((PrimitiveType)5);
            Object.Destroy(gameObject3.GetComponent<MeshCollider>());
            gameObject3.transform.SetParent(gameObject2.transform);
            gameObject3.transform.localPosition = Vector3.zero;
            gameObject3.transform.localScale = new Vector3(3.2f, 1.1f, 1f); // Slightly larger
            gameObject3.GetComponent<MeshRenderer>().material = bg;
            gameObject3.GetComponent<MeshRenderer>().material.color = bgColor; // Use colored background
            
            // Add subtle border effect
            GameObject border = GameObject.CreatePrimitive((PrimitiveType)5);
            Object.Destroy(border.GetComponent<MeshCollider>());
            border.transform.SetParent(gameObject2.transform);
            border.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            border.transform.localScale = new Vector3(3.3f, 1.2f, 1f);
            border.GetComponent<MeshRenderer>().material = bg;
            border.GetComponent<MeshRenderer>().material.color = new Color(endColor.r * 0.8f, endColor.g * 0.8f, endColor.b * 0.8f, 0.6f);
            
            GameObject gameObject4 = new GameObject("Text");
            TextMesh textMesh = gameObject4.AddComponent<TextMesh>();
            gameObject4.transform.SetParent(gameObject2.transform);
            gameObject4.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            textMesh.text = (flag ? (icon + " " + text2 + " [CD]") : (icon + " " + text2));
            textMesh.color = endColor; // Use colored text
            textMesh.fontStyle = (FontStyle)1;
            textMesh.fontSize = 55; // Slightly larger font
            textMesh.characterSize = 0.065f;
            textMesh.anchor = (TextAnchor)4;
        }

        // Token: 0x0600001C RID: 28 RVA: 0x00005A64 File Offset: 0x00003C64
        private void UpdateMarkers()
        {
            bool flag = this.radarContainer == null;
            if (!flag)
            {
                Transform transform = Camera.main.transform;
                Vector3 position = transform.position;
                for (int i = 0; i < this.radarContainer.transform.childCount; i++)
                {
                    Transform child = this.radarContainer.transform.GetChild(i);
                    bool flag2 = child == null;
                    if (!flag2)
                    {
                        GameObject gameObject = child.gameObject;
                        float num = Vector3.Distance(position, child.position);
                        bool flag3 = num > 75f;
                        if (flag3)
                        {
                            Object.Destroy(gameObject);
                            bool flag4 = gameObject.name.StartsWith("TrackedMarker_");
                            if (flag4)
                            {
                                foreach (KeyValuePair<GameObject, GameObject> keyValuePair in this.markerToTarget.ToList<KeyValuePair<GameObject, GameObject>>())
                                {
                                    bool flag5 = keyValuePair.Key.name == gameObject.name;
                                    if (flag5)
                                    {
                                        this.markerToTarget.Remove(keyValuePair.Key);
                                        break;
                                    }
                                }
                                foreach (KeyValuePair<int, GameObject> keyValuePair2 in this.trackedObjectMarkers.ToList<KeyValuePair<int, GameObject>>())
                                {
                                    bool flag6 = keyValuePair2.Value == gameObject;
                                    if (flag6)
                                    {
                                        this.trackedObjectMarkers.Remove(keyValuePair2.Key);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            bool flag7 = gameObject.name.StartsWith("TrackedMarker_");
                            if (flag7)
                            {
                                GameObject gameObject2 = null;
                                foreach (KeyValuePair<GameObject, GameObject> keyValuePair3 in this.markerToTarget)
                                {
                                    bool flag8 = keyValuePair3.Key.name == gameObject.name;
                                    if (flag8)
                                    {
                                        gameObject2 = keyValuePair3.Value;
                                        break;
                                    }
                                }
                                bool flag9 = gameObject2 != null && gameObject2.name.ToLower().Contains("p_insect_insect");
                                bool flag10 = flag9 && !this.showInsectRadar;
                                if (flag10)
                                {
                                    Object.Destroy(gameObject);
                                    foreach (KeyValuePair<GameObject, GameObject> keyValuePair4 in this.markerToTarget.ToList<KeyValuePair<GameObject, GameObject>>())
                                    {
                                        bool flag11 = keyValuePair4.Key.name == gameObject.name;
                                        if (flag11)
                                        {
                                            this.markerToTarget.Remove(keyValuePair4.Key);
                                            break;
                                        }
                                    }
                                    foreach (KeyValuePair<int, GameObject> keyValuePair5 in this.trackedObjectMarkers.ToList<KeyValuePair<int, GameObject>>())
                                    {
                                        bool flag12 = keyValuePair5.Value == gameObject;
                                        if (flag12)
                                        {
                                            this.trackedObjectMarkers.Remove(keyValuePair5.Key);
                                            break;
                                        }
                                    }
                                    goto IL_505;
                                }
                                bool flag13 = gameObject2 != null && gameObject2.activeInHierarchy;
                                if (flag13)
                                {
                                    child.position = gameObject2.transform.position;
                                }
                                else
                                {
                                    Object.Destroy(gameObject);
                                    foreach (KeyValuePair<GameObject, GameObject> keyValuePair6 in this.markerToTarget.ToList<KeyValuePair<GameObject, GameObject>>())
                                    {
                                        bool flag14 = keyValuePair6.Key.name == gameObject.name;
                                        if (flag14)
                                        {
                                            this.markerToTarget.Remove(keyValuePair6.Key);
                                            break;
                                        }
                                    }
                                    foreach (KeyValuePair<int, GameObject> keyValuePair7 in this.trackedObjectMarkers.ToList<KeyValuePair<int, GameObject>>())
                                    {
                                        bool flag15 = keyValuePair7.Value == gameObject;
                                        if (flag15)
                                        {
                                            this.trackedObjectMarkers.Remove(keyValuePair7.Key);
                                            break;
                                        }
                                    }
                                }
                            }
                            Transform transform2 = child.Find("LabelAnchor");
                            bool flag16 = transform2 != null;
                            if (flag16)
                            {
                                transform2.LookAt(transform);
                                transform2.Rotate(0f, 180f, 0f);
                                TextMesh componentInChildren = transform2.GetComponentInChildren<TextMesh>();
                                bool flag17 = componentInChildren != null;
                                if (flag17)
                                {
                                    float value = Vector3.Distance(transform.position, child.position);
                                    string[] array = componentInChildren.text.Split(new char[] { '\n' }, StringSplitOptions.None);
                                    bool flag18 = array.Length != 0;
                                    if (flag18)
                                    {
                                        TextMesh textMesh = componentInChildren;
                                        textMesh.text = $"{array[0]}\n{value:F0}m";
                                    }
                                }
                            }
                        }
                    }
                IL_505:;
                }
            }
        }

        // Token: 0x0600001D RID: 29 RVA: 0x00005FF0 File Offset: 0x000041F0
        private string GetMeshName(Il2CppObject group, Il2CppBindingFlags flags)
        {
            string result;
            try
            {
                Il2CppFieldInfo field = group.GetIl2CppType().GetField("meshInfo", flags);
                Il2CppObject @object = (field != null) ? field.GetValue(group) : null;
                Il2CppObject object2;
                if (@object == null)
                {
                    object2 = null;
                }
                else
                {
                    Il2CppFieldInfo field2 = @object.GetIl2CppType().GetField("lodMesh", flags);
                    object2 = ((field2 != null) ? field2.GetValue(@object) : null);
                }
                Il2CppObject object3 = object2;
                bool flag = object3 == null;
                if (flag)
                {
                    result = "";
                }
                else
                {
                    Il2CppMethodInfo method = object3.GetIl2CppType().GetMethod("GetValue", new Il2CppReferenceArray<Il2CppType>(new Il2CppType[]
                    {
                        Il2CppType.GetType("System.Int32")
                    }));
                    bool flag2 = method == null;
                    if (flag2)
                    {
                        result = "";
                    }
                    else
                    {
                        Il2CppObject object4 = method.Invoke(object3, new Il2CppReferenceArray<Il2CppObject>(new Il2CppObject[]
                        {
                            this.BoxInt(0)
                        }));
                        Mesh mesh = (object4 != null) ? object4.TryCast<Mesh>() : null;
                        result = (((mesh != null) ? mesh.name : null) ?? "");
                    }
                }
            }
            catch
            {
                result = "";
            }
            return result;
        }

        // Token: 0x0600001E RID: 30 RVA: 0x000060F8 File Offset: 0x000042F8
        private Vector3 GetBlockPos(Il2CppObject block, Il2CppBindingFlags flags)
        {
            Il2CppFieldInfo field = block.GetIl2CppType().GetField("aabb", flags);
            Il2CppObject @object = (field != null) ? field.GetValue(block) : null;
            Il2CppObject object2;
            if (@object == null)
            {
                object2 = null;
            }
            else
            {
                Il2CppFieldInfo field2 = @object.GetIl2CppType().GetField("m_Center", flags);
                object2 = ((field2 != null) ? field2.GetValue(@object) : null);
            }
            Il2CppObject object3 = object2;
            float num = object3.GetIl2CppType().GetField("x").GetValue(object3).Unbox<float>();
            float num2 = object3.GetIl2CppType().GetField("y").GetValue(object3).Unbox<float>();
            float num3 = object3.GetIl2CppType().GetField("z").GetValue(object3).Unbox<float>();
            return new Vector3(num, num2, num3);
        }

        private Il2CppObject BoxInt(int val)
        {
            // FIX: Direct field assignment for maximum compatibility
            return new Il2CppSystem.Int32 { m_value = val }.BoxIl2CppObject();
        }

        // Token: 0x0600001F RID: 31 RVA: 0x000061B0 File Offset: 0x000043B0

        // NEW FEATURE: Apply Camera FOV
        private void ApplyCameraFOV()
        {
            if (this.mainCamera == null)
            {
                this.mainCamera = Camera.main;
                if (this.mainCamera != null && this.originalFOV < 0f)
                {
                    this.originalFOV = this.mainCamera.fieldOfView;
                }
            }

            if (this.mainCamera != null)
            {
                this.mainCamera.fieldOfView = this.cameraFOV;
            }
        }

        // NEW FEATURE: Apply Jump Height Multiplier
        // NEW FEATURE: Apply Jump Height Multiplier

        private float jumpCooldown = 0f;
        private float lastJumpTime = 0f;

        private Vector3 jumpStartPos = Vector3.zero;
        private Vector3 jumpTargetPos = Vector3.zero;
        private float jumpProgress = 0f;
        private bool isJumpBoosting = false;

        private void ApplyJumpHeight()
        {
            if (this.jumpHeightMultiplier <= 1f)
            {
                // Make sure override is off when multiplier is 1x
                if (HeartopiaComplete.OverridePlayerPosition && this.isCurrentlyJumpBoosting)
                {
                    HeartopiaComplete.OverridePlayerPosition = false;
                    this.isCurrentlyJumpBoosting = false;
                }
                return;
            }

            GameObject player = GameObject.Find("p_player_skeleton(Clone)");
            if (player == null) return;
            
            // Rebinding check
            if (!string.IsNullOrEmpty(this.keyBindingActive)) return;

            bool spacePressed = Input.GetKeyDown(this.keyJumpBoost);

            // START: When Space is pressed, begin smooth jump
            if (spacePressed && !this.isCurrentlyJumpBoosting && Time.time > this.lastJumpBoostPressTime + 0.5f)
            {
                // Calculate how much extra height to add
                float extraHeight = 1.5f * (this.jumpHeightMultiplier - 1f); // HEIGHT: Adjust multiplier (1.0-2.0 recommended) // Reduced from 1f per multiplier

                Vector3 currentPos = player.transform.position;
                this.jumpBoostStartPos = currentPos;
                this.jumpBoostTargetPos = new Vector3(currentPos.x, currentPos.y + extraHeight, currentPos.z);
                this.jumpBoostProgress = 0f;
                this.isCurrentlyJumpBoosting = true;
                this.lastJumpBoostPressTime = Time.time;

                MelonLogger.Msg(string.Format("[JUMP START] Boosting from Y:{0:F2} to Y:{1:F2} (+{2:F2}m) over {3}s",
                    currentPos.y, this.jumpBoostTargetPos.y, extraHeight, 0.3f));
                this.AddMenuNotification($"Jump Boost: +{extraHeight:F1}m", new Color(0.55f, 0.88f, 1f));
            }

            // UPDATE: Apply smooth movement over time
            if (this.isCurrentlyJumpBoosting)
            {
                // Increment progress (0.3 seconds total duration)
                float boostDuration = 0.6f; // SMOOTHNESS: Increase for slower, smoother jumps (0.3-1.0 recommended)
                this.jumpBoostProgress += Time.deltaTime / boostDuration;

                if (this.jumpBoostProgress >= 1f)
                {
                    // Finished boosting
                    HeartopiaComplete.OverridePlayerPosition = false;
                    this.isCurrentlyJumpBoosting = false;
                    MelonLogger.Msg("[JUMP END] Boost complete");
                }
                else
                {
                    // Smooth interpolation (ease-out for natural feel)
                    float easedProgress = 1f - Mathf.Pow(1f - this.jumpBoostProgress, 3f); // Cubic ease-out

                    // Calculate current position
                    Vector3 currentPlayerPos = player.transform.position;
                    float targetY = Mathf.Lerp(this.jumpBoostStartPos.y, this.jumpBoostTargetPos.y, easedProgress);

                    // Set override position (keep X and Z from actual position)
                    HeartopiaComplete.OverridePlayerPosition = true;
                    HeartopiaComplete.OverridePosition = new Vector3(currentPlayerPos.x, targetY, currentPlayerPos.z);
                }
            }
        }

        private void Cleanup()
        {
            bool flag = this.radarContainer != null;
            if (flag)
            {
                Object.Destroy(this.radarContainer);
                this.radarContainer = null;
            }
            this.markerToTarget.Clear();
            this.trackedObjectMarkers.Clear();
        }

        // Token: 0x06000020 RID: 32 RVA: 0x000061FC File Offset: 0x000043FC
        private void VacuumBirds()
        {
            GameObject gameObject = GameObject.Find("p_player_skeleton(Clone)");
            bool flag = gameObject == null;
            if (!flag)
            {
                Transform transform = gameObject.transform;
                Vector3 position = transform.position + transform.forward * 3f;
                position.y = transform.position.y;
                GameObject[] array = Object.FindObjectsOfType<GameObject>();
                foreach (GameObject gameObject2 in array)
                {
                    bool flag2 = gameObject2 == null || gameObject2.name == null;
                    if (!flag2)
                    {
                        string text = gameObject2.name.ToLower();
                        bool flag3 = text.Contains("p_bird") && !text.Contains("birdscanner");
                        if (flag3)
                        {
                            gameObject2.transform.position = position;
                        }
                    }
                }
            }
        }

        // Token: 0x06000021 RID: 33 RVA: 0x000062EC File Offset: 0x000044EC
        private float DrawTeleportTab(int startY)
        {
            int num = startY;

            // Home tab: keep all home controls isolated here.
            if (this.teleportSubTab == 0)
            {
                GUI.Label(new Rect(20f, (float)num, 260f, 20f), "Home Position");
                num += 25;
                if (GUI.Button(new Rect(20f, (float)num, 125f, 35f), "Set Home"))
                {
                    this.SetHomePosition();
                }
                GUI.enabled = this.homePositionSet;
                if (this.DrawPrimaryActionButton(new Rect(155f, (float)num, 125f, 35f), "TP Home"))
                {
                    this.TeleportToHome();
                }
                GUI.enabled = true;
                num += 45;

                if (this.homePositionSet)
                {
                    GUI.color = Color.green;
                    GUI.Label(new Rect(20f, (float)num, 340f, 20f), $"Home Set: ({this.homePosition.x:F1}, {this.homePosition.y:F1}, {this.homePosition.z:F1})");
                }
                else
                {
                    GUI.color = Color.red;
                    GUI.Label(new Rect(20f, (float)num, 340f, 20f), "Home: Not Set");
                }
                GUI.color = Color.white;
                num += 25;

                GUI.Label(new Rect(20f, (float)num, 260f, 20f), "Status:");
                num += 25;
                if (HeartopiaComplete.OverridePlayerPosition)
                {
                    GUI.color = Color.yellow;
                    GUI.Label(new Rect(20f, (float)num, 260f, 20f), "Teleporting...");
                }
                else
                {
                    GUI.color = Color.green;
                    GUI.Label(new Rect(20f, (float)num, 260f, 20f), "Ready to Travel");
                }
                GUI.color = Color.white;
                num += 30;

                GameObject playerObj = GameObject.Find("p_player_skeleton(Clone)");
                if (playerObj != null)
                {
                    Vector3 p = playerObj.transform.position;
                    GUI.Label(new Rect(20f, (float)num, 340f, 40f), $"Current Position:\n({p.x:F1}, {p.y:F1}, {p.z:F1})");
                }
                else
                {
                    GUI.Label(new Rect(20f, (float)num, 340f, 40f), "Current Position:\nPlayer not found");
                }
                return (float)num + 60f;
            }

            float panelX = 20f;
            float panelY = (float)num;
            float listButtonWidth = 440f;

            if (this.teleportSubTab == 1)
            {
                int y = num;
                for (int animalIdx = 0; animalIdx < this.animalCareLocations.Length; animalIdx++)
                {
                    Vector3 animalPos = this.animalCareLocations[animalIdx];
                    if (GUI.Button(new Rect(panelX, (float)y, listButtonWidth, 40f), string.Format("Animal Care #{0}\n({1:F0}, {2:F0}, {3:F0})", animalIdx + 1, animalPos.x, animalPos.y, animalPos.z)))
                    {
                        this.TeleportToLocation(animalPos);
                    }
                    y += 45;
                }
                return y + 20f;
            }

            if (this.teleportSubTab == 3)
            {
                int y = num;
                foreach (KeyValuePair<string, Vector3> keyValuePair in this.fastTravelLocations)
                {
                    if (GUI.Button(new Rect(panelX, (float)y, listButtonWidth, 40f), $"{keyValuePair.Key}\n({keyValuePair.Value.x:F0}, {keyValuePair.Value.y:F0}, {keyValuePair.Value.z:F0})"))
                    {
                        this.TeleportToLocation(keyValuePair.Value);
                    }
                    y += 45;
                }
                return y + 20f;
            }

            if (this.teleportSubTab == 2)
            {
                int y = num;
                foreach (KeyValuePair<string, Vector3> keyValuePair2 in this.npcLocations)
                {
                    if (GUI.Button(new Rect(panelX, (float)y, listButtonWidth, 40f), $"{keyValuePair2.Key}\n({keyValuePair2.Value.x:F0}, {keyValuePair2.Value.y:F0}, {keyValuePair2.Value.z:F0})"))
                    {
                        this.TeleportToLocation(keyValuePair2.Value);
                    }
                    y += 45;
                }
                return y + 20f;
            }

            if (this.teleportSubTab == 4)
            {
                int y = num;
                foreach (KeyValuePair<string, Vector3> keyValuePair3 in this.eventLocations)
                {
                    if (GUI.Button(new Rect(panelX, (float)y, listButtonWidth, 40f), $"{keyValuePair3.Key}\n({keyValuePair3.Value.x:F0}, {keyValuePair3.Value.y:F0}, {keyValuePair3.Value.z:F0})"))
                    {
                        this.TeleportToLocation(keyValuePair3.Value);
                    }
                    y += 45;
                }
                return y + 20f;
            }

            if (this.teleportSubTab == 5)
            {
                int y = num;
                for (int houseIdx = 0; houseIdx < this.houseLocations.Length; houseIdx++)
                {
                    Vector3 housePos = this.houseLocations[houseIdx];
                    if (GUI.Button(new Rect(panelX, (float)y, listButtonWidth, 40f), string.Format("House Slot #{0}\n({1:F0}, {2:F0}, {3:F0})", houseIdx + 1, housePos.x, housePos.y, housePos.z)))
                    {
                        this.TeleportToLocation(housePos);
                    }
                    y += 45;
                }
                return y + 20f;
            }

            if (this.teleportSubTab == 6)
            {
                GUI.Label(new Rect(panelX, panelY + 8f, 40f, 25f), "Name:");
                this.customTeleportName = GUI.TextField(new Rect(panelX + 45f, panelY + 8f, 160f, 25f), this.customTeleportName);
                if (GUI.Button(new Rect(panelX + 212f, panelY + 8f, 70f, 25f), "Save"))
                {
                    GameObject player = GameObject.Find("p_player_skeleton(Clone)");
                    if (player != null)
                    {
                        this.customTeleportList.Add(new CustomTeleportEntry { name = this.customTeleportName, position = player.transform.position });
                        this.SaveCustomTeleports();
                    }
                }

                int y = (int)(panelY + 46f);
                if (this.customTeleportList.Count > 0)
                {
                    GUI.Label(new Rect(panelX, (float)y, 260f, 22f), "Saved Teleports");
                    y += 24;
                    for (int i = 0; i < this.customTeleportList.Count; ++i)
                    {
                        var entry = this.customTeleportList[i];
                        if (GUI.Button(new Rect(panelX, (float)y, 320f, 32f), entry.name))
                        {
                            this.TeleportToLocation(entry.position);
                        }
                        if (this.DrawDangerActionButton(new Rect(panelX + 328f, (float)y, 70f, 32f), "DEL"))
                        {
                            this.customTeleportList.RemoveAt(i);
                            this.SaveCustomTeleports();
                            break;
                        }
                        y += 38;
                    }
                }
                return y + 20f;
            }

            if (this.teleportSubTab == 7)
            {
                GUI.Label(new Rect(panelX, panelY + 8f, 260f, 24f), "Direct XYZ Teleport");
                GUI.Label(new Rect(panelX, panelY + 40f, 18f, 25f), "X:");
                this.customTPX = GUI.TextField(new Rect(panelX + 18f, panelY + 40f, 90f, 25f), this.customTPX);
                GUI.Label(new Rect(panelX + 118f, panelY + 40f, 18f, 25f), "Y:");
                this.customTPY = GUI.TextField(new Rect(panelX + 136f, panelY + 40f, 90f, 25f), this.customTPY);
                GUI.Label(new Rect(panelX + 236f, panelY + 40f, 18f, 25f), "Z:");
                this.customTPZ = GUI.TextField(new Rect(panelX + 254f, panelY + 40f, 90f, 25f), this.customTPZ);

                if (GUI.Button(new Rect(panelX, panelY + 78f, 344f, 32f), "Teleport to XYZ"))
                {
                    if (float.TryParse(this.customTPX, out float x) &&
                        float.TryParse(this.customTPY, out float y) &&
                        float.TryParse(this.customTPZ, out float z))
                    {
                        this.TeleportToLocation(new Vector3(x, y, z));
                    }
                }
                return panelY + 130f;
            }

            return panelY + 20f;
        }

        // Token: 0x06000022 RID: 34 RVA: 0x00006C04 File Offset: 0x00004E04
        private void SetHomePosition()
        {
            GameObject gameObject = GameObject.Find("p_player_skeleton(Clone)");
            bool flag = gameObject == null;
            if (flag)
            {
                MelonLogger.Msg("Player not found!");
            }
            else
            {
                this.homePosition = gameObject.transform.position;
                this.homePositionSet = true;
                MelonLogger.Msg($"[HOME] Home position set to: {this.homePosition}");
            }
        }

        // Token: 0x06000023 RID: 35 RVA: 0x00006C80 File Offset: 0x00004E80
        private void TeleportToHome()
        {
            bool flag = !this.homePositionSet;
            if (flag)
            {
                MelonLogger.Msg("[HOME] Home position not set!");
            }
            else
            {
                this.TeleportToLocation(this.homePosition);
                MelonLogger.Msg($"[HOME] Teleported to home: {this.homePosition}");
            }
        }

        // Token: 0x06000024 RID: 36 RVA: 0x00006CE8 File Offset: 0x00004EE8
        private void RotateCameraAroundPlayer(float degrees = 180f)
        {
            GameObject gameObject = GameObject.Find("p_player_skeleton(Clone)");
            GameObject gameObject2 = GameObject.Find("GameApp/startup_root(Clone)/Main Camera");
            bool flag = gameObject == null || gameObject2 == null;
            if (flag)
            {
                MelonLogger.Msg("[CAMERA] Failed to rotate - player or camera not found");
            }
            else
            {
                Vector3 position = gameObject.transform.position;
                Vector3 position2 = gameObject2.transform.position;
                Vector3 vector = position2 - position;
                float num = degrees * Mathf.Deg2Rad;
                float num2 = vector.x * Mathf.Cos(num) - vector.z * Mathf.Sin(num);
                float num3 = vector.x * Mathf.Sin(num) + vector.z * Mathf.Cos(num);
                Vector3 vector2 = new Vector3(num2, vector.y, num3);
                Vector3 vector3 = position + vector2;
                Vector3 vector4 = position - vector3;
                bool flag2 = vector4 != Vector3.zero;
                if (flag2)
                {
                    HeartopiaComplete.CameraOverrideRot = Quaternion.LookRotation(vector4);
                }
                HeartopiaComplete.CameraOverridePos = vector3;
                HeartopiaComplete.OverrideCameraPosition = true;
                this.cameraOverrideFramesRemaining = 60;
            }
        }

        // Token: 0x06000025 RID: 37 RVA: 0x00006E00 File Offset: 0x00005000
        private void TeleportToLocation(Vector3 targetPos)
        {
            GameObject gameObject = GameObject.Find("p_player_skeleton(Clone)");
            bool flag = gameObject == null;
            if (flag)
            {
                MelonLogger.Msg("Player not found!");
            }
            else
            {
                Vector3 position = gameObject.transform.position;
                HeartopiaComplete.OverridePosition = targetPos;
                HeartopiaComplete.OverridePlayerPosition = true;
                CharacterController component = gameObject.GetComponent<CharacterController>();
                bool flag2 = component != null;
                if (flag2)
                {
                    component.enabled = false;
                }
                gameObject.transform.position = targetPos;
                bool flag3 = component != null;
                if (flag3)
                {
                    component.enabled = true;
                }
                this.teleportFramesRemaining = 30;
            }
        }

        private void TeleportToLocation(Vector3 targetPos, Quaternion targetRot)
        {
            GameObject gameObject = GameObject.Find("p_player_skeleton(Clone)");
            bool flag = gameObject == null;
            if (flag)
            {
                MelonLogger.Msg("Player not found!");
            }
            else
            {
                Vector3 position = gameObject.transform.position;
                HeartopiaComplete.OverridePosition = targetPos;
                HeartopiaComplete.OverridePlayerPosition = true;
                HeartopiaComplete.PlayerOverrideRot = targetRot;
                HeartopiaComplete.OverridePlayerRotation = true;
                CharacterController component = gameObject.GetComponent<CharacterController>();
                bool flag2 = component != null;
                if (flag2)
                {
                    component.enabled = false;
                }
                gameObject.transform.position = targetPos;
                gameObject.transform.rotation = targetRot;
                bool flag3 = component != null;
                if (flag3)
                {
                    component.enabled = true;
                }
                this.teleportFramesRemaining = 30;
                this.playerRotationFramesRemaining = 30;
            }
        }

        // Token: 0x06000026 RID: 38 RVA: 0x00006E94 File Offset: 0x00005094
        private void InspectPlayerComponents()
        {
            GameObject gameObject = GameObject.Find("p_player_skeleton(Clone)");
            bool flag = gameObject == null;
            if (flag)
            {
                MelonLogger.Msg("Player not found!");
            }
            else
            {
                MelonLogger.Msg("=== PLAYER COMPONENTS (Il2Cpp) ===");
                Component[] array = gameObject.GetComponents<Component>();
                foreach (Component component in array)
                {
                    bool flag2 = component == null;
                    if (!flag2)
                    {
                        try
                        {
                            string fullName = component.GetType().FullName;
                            MelonLogger.Msg("GetType().FullName: " + fullName);
                            Il2CppType il2CppType = component.GetIl2CppType();
                            bool flag3 = il2CppType != null;
                            if (flag3)
                            {
                                MelonLogger.Msg("Il2CppType.Name: " + il2CppType.Name);
                                MelonLogger.Msg("Il2CppType.FullName: " + il2CppType.FullName);
                            }
                            Type baseType = component.GetType().BaseType;
                            bool flag4 = baseType != null;
                            if (flag4)
                            {
                                MelonLogger.Msg("BaseType: " + baseType.FullName);
                            }
                            MelonLogger.Msg("comp.name: " + component.name);
                            MelonLogger.Msg("---");
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Msg("Error inspecting component: " + ex.Message);
                        }
                    }
                }
                MelonLogger.Msg("=== END ===");
            }
        }

        // --- PATROL SYSTEM METHODS ---
        private void StartPatrol()
        {
            if (patrolPoints.Count == 0) return;
            isPatrolActive = true;
            patrolCoroutine = MelonCoroutines.Start(PatrolRoutine());
        }

        private System.Collections.IEnumerator PatrolRoutine()
        {
            int index = 0;
            while (isPatrolActive)
            {
                if (patrolPoints.Count == 0) break;

                // 1. TELEPORT
                TeleportTo(patrolPoints[index]);

                // 2. WAIT
                yield return new WaitForSeconds(waitAtSpot);

                // 3. WORK LOOP (Prioritize Cooking)
                // Loop 15 times to ensure buttons are clicked.
                for (int i = 0; i < 15; i++)
                {
                    RunSpamClicker();
                    yield return new WaitForSeconds(0.12f);
                }

                // 4. CLEANUP (Unstuck)
                // If menu is still open, close it now.
                ForceCloseMenuIfOpen();

                // 5. NEXT POINT
                index++;
                if (index >= patrolPoints.Count) index = 0;
            }
            isPatrolActive = false;
        }

        private System.Collections.IEnumerator CookingPatrolRoutine()
        {
            int index = 0;
            while (isCookingPatrolActive && autoCookEnabled)
            {
                if (cookingPatrolPoints.Count == 0) break;

                CookingPatrolPoint point = cookingPatrolPoints[index];

                // 1. TELEPORT to cooking location
                TeleportTo(point.Position.ToVector3());

                // 2. APPLY CHARACTER ROTATION (face the cooking station)
                Quaternion targetRotation = point.Rotation.ToQuaternion();
                HeartopiaComplete.OverridePlayerRotation = true;
                HeartopiaComplete.PlayerOverrideRot = targetRotation;
                this.playerRotationFramesRemaining = 100; // Hold rotation for entire work cycle
                MelonLogger.Msg($"[Cooking Patrol] Setting rotation to {targetRotation.eulerAngles} at point {index}");

                // 3. WAIT
                yield return new WaitForSeconds(cookingWaitAtSpot);

                // 4. WORK LOOP - Keep refreshing rotation to ensure it sticks
                for (int i = 0; i < 15; i++)
                {
                    // Re-apply rotation continuously to fight against game trying to reset it
                    GameObject player = GetPlayer();
                    if (player != null)
                    {
                        player.transform.rotation = HeartopiaComplete.PlayerOverrideRot;
                    }
                    this.playerRotationFramesRemaining = 100; // Keep extending

                    RunSpamClicker();
                    yield return new WaitForSeconds(0.12f);
                }

                // 5. CLEANUP (Same as regular patrol)
                ForceCloseMenuIfOpen();

                // 6. Disable rotation override before moving to next point
                HeartopiaComplete.OverridePlayerRotation = false;
                this.playerRotationFramesRemaining = 0;

                // 7. NEXT POINT
                index++;
                if (index >= cookingPatrolPoints.Count) index = 0;
            }
            isCookingPatrolActive = false;
            HeartopiaComplete.OverridePlayerRotation = false;
        }

        private void RunSpamClicker()
        {
            // Click buttons by path
            foreach (string path in workPaths)
            {
                ClickButtonIfExists(path);
            }
            ClickCookingCleanupThrottled(0.45f);
        }

        // --- FORCE CLOSE MENU ---
        private void ForceCloseMenuIfOpen()
        {
            try
            {
                GameObject cookPanel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)");

                if (cookPanel != null && cookPanel.activeInHierarchy)
                {
                    // Method 1: Find UI Button
                    bool buttonFound = false;
                    Button[] buttons = cookPanel.GetComponentsInChildren<Button>(true);
                    if (buttons != null)
                    {
                        foreach (Button btn in buttons)
                        {
                            if (btn == null) continue;
                            try
                            {
                                string n = btn.name.ToLower();
                                if (n.Contains("close") || n.Contains("back") || n.Contains("exit") || n.Contains("return"))
                                {
                                    if (btn.interactable)
                                    {
                                        btn.onClick.Invoke();
                                        buttonFound = true;
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    // Method 2: Send ESC if button fail
                    if (!buttonFound)
                    {
                        SendEscMessage();
                    }
                }
            }
            catch { }
        }

        private void SendEscMessage()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd != IntPtr.Zero)
                {
                    PostMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_ESCAPE, IntPtr.Zero);
                    PostMessage(hWnd, WM_KEYUP, (IntPtr)VK_ESCAPE, IntPtr.Zero);
                }
            }
            catch { }
        }

        private void SendFMessage()
        {
            try
            {
                // Use SendInput for better compatibility
                INPUT[] inputs = new INPUT[2];
                inputs[0].type = 1; // INPUT_KEYBOARD
                inputs[0].u.ki.wVk = VK_F;
                inputs[0].u.ki.dwFlags = 0; // KEYEVENTF_KEYDOWN

                inputs[1].type = 1; // INPUT_KEYBOARD
                inputs[1].u.ki.wVk = VK_F;
                inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;

                SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
            catch { }
        }

        private void SendLeftClickMessage()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd != IntPtr.Zero)
                {
                    PostMessage(hWnd, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, IntPtr.Zero);
                    PostMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch { }
        }

        private void SendLeftClickInputTap()
        {
            try
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            }
            catch
            {
                this.SendLeftClickMessage();
            }
        }


        private GameObject GetPlayer() => GameObject.Find("p_player_skeleton(Clone)");

        private void TeleportTo(Vector3 targetPos)
        {
            OverridePosition = targetPos;
            OverridePlayerPosition = true;
            teleportFramesRemaining = 10;
            GameObject p = GetPlayer();
            if (p != null)
            {
                p.transform.position = targetPos;
                if (p.transform.root != null) p.transform.root.position = targetPos;
            }
        }

        private void SyncTeleportPosition()
        {
            if (OverridePlayerPosition && teleportFramesRemaining > 0)
            {
                teleportFramesRemaining--;
                GameObject p = GetPlayer();
                if (p != null)
                {
                    p.transform.position = OverridePosition;
                    if (p.transform.root != null) p.transform.root.position = OverridePosition;
                }
                if (teleportFramesRemaining <= 0) OverridePlayerPosition = false;
            }
        }

        // --- PATROL SAVE/LOAD ---
        private string GetPatrolPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "patrol_points.json");
        }

        private void SavePatrolPoints()
        {
            try
            {
                PatrolData data = new PatrolData();
                foreach (var p in patrolPoints) data.Points.Add(new SerializableVector3(p));
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"Points\": [");
                for (int i = 0; i < data.Points.Count; i++)
                {
                    var pt = data.Points[i];
                    sb.Append($"    {{ \"x\": {pt.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {pt.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"z\": {pt.z.ToString(System.Globalization.CultureInfo.InvariantCulture)} }}");
                    if (i < data.Points.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");
                File.WriteAllText(this.GetPatrolPath(), sb.ToString());
                MelonLogger.Msg("Patrol points saved!");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error saving patrol points: " + ex.Message);
            }
        }

        private void LoadPatrolPoints()
        {
            string path = this.GetPatrolPath();
            if (!File.Exists(path)) return;
            try
            {
                string json = File.ReadAllText(path);
                patrolPoints.Clear();
                string[] lines = json.Split('{');
                foreach (string line in lines)
                {
                    if (line.Contains("\"x\":"))
                    {
                        float x = float.Parse(ExtractJsonVal(line, "\"x\":"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                        float y = float.Parse(ExtractJsonVal(line, "\"y\":"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                        float z = float.Parse(ExtractJsonVal(line, "\"z\":"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                        patrolPoints.Add(new Vector3(x, y, z));
                    }
                }
                MelonLogger.Msg($"Loaded {patrolPoints.Count} patrol points.");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error loading patrol points: " + ex.Message);
            }
        }

        private string ExtractJsonVal(string src, string key)
        {
            int start = src.IndexOf(key) + key.Length;
            int end = src.IndexOf(",", start);
            if (end == -1) end = src.IndexOf("}", start);
            return src.Substring(start, end - start).Trim();
        }

        // --- COOKING PATROL SAVE/LOAD ---
        private string GetCookingPatrolPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "cooking_patrol_points.json");
        }

        private string GetTreeFarmPatrolPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "tree_farm_patrol_points.json");
        }

        private void SaveCookingPatrolPoints()
        {
            try
            {
                CookingPatrolData data = new CookingPatrolData();
                data.Points = cookingPatrolPoints;

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"Points\": [");
                for (int i = 0; i < data.Points.Count; i++)
                {
                    var pt = data.Points[i];
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"Position\": {{ \"x\": {pt.Position.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {pt.Position.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"z\": {pt.Position.z.ToString(System.Globalization.CultureInfo.InvariantCulture)} }},");
                    sb.Append($"      \"Rotation\": {{ \"x\": {pt.Rotation.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {pt.Rotation.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"z\": {pt.Rotation.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"w\": {pt.Rotation.w.ToString(System.Globalization.CultureInfo.InvariantCulture)} }}");
                    sb.AppendLine();
                    sb.Append("    }");
                    if (i < data.Points.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");
                File.WriteAllText(this.GetCookingPatrolPath(), sb.ToString());
                MelonLogger.Msg($"Cooking patrol points saved! ({cookingPatrolPoints.Count} points with rotations)");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error saving cooking patrol points: " + ex.Message);
            }
        }

        private void LoadCookingPatrolPoints()
        {
            try
            {
                string path = this.GetCookingPatrolPath();
                if (!File.Exists(path))
                {
                    MelonLogger.Msg("Cooking patrol points file not found.");
                    return;
                }
                cookingPatrolPoints.Clear();
                string json = File.ReadAllText(path);

                // Check if this is the new format (with Rotation) or old format (just Vector3)
                bool hasRotation = json.Contains("\"Rotation\"");

                // Parse JSON manually - find all coordinate blocks
                int pointsStart = json.IndexOf("[");
                int pointsEnd = json.LastIndexOf("]");
                if (pointsStart == -1 || pointsEnd == -1) return;

                string pointsSection = json.Substring(pointsStart + 1, pointsEnd - pointsStart - 1);

                if (hasRotation)
                {
                    // New format with Position and Rotation
                    string[] pointBlocks = pointsSection.Split(new string[] { "    }" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string block in pointBlocks)
                    {
                        if (!block.Contains("Position")) continue;

                        // Extract Position block (between "Position": { and })
                        int posStart = block.IndexOf("\"Position\"");
                        if (posStart == -1) continue;
                        int posObjStart = block.IndexOf("{", posStart);
                        int posObjEnd = block.IndexOf("}", posObjStart);
                        string posBlock = block.Substring(posObjStart, posObjEnd - posObjStart + 1);

                        float px = ExtractCoordinate(posBlock, "x");
                        float py = ExtractCoordinate(posBlock, "y");
                        float pz = ExtractCoordinate(posBlock, "z");

                        // Extract Rotation block (between "Rotation": { and })
                        int rotStart = block.IndexOf("\"Rotation\"");
                        Quaternion rotation = Quaternion.identity;
                        if (rotStart != -1)
                        {
                            int rotObjStart = block.IndexOf("{", rotStart);
                            int rotObjEnd = block.IndexOf("}", rotObjStart);
                            string rotBlock = block.Substring(rotObjStart, rotObjEnd - rotObjStart + 1);

                            float rx = ExtractCoordinate(rotBlock, "x");
                            float ry = ExtractCoordinate(rotBlock, "y");
                            float rz = ExtractCoordinate(rotBlock, "z");
                            float rw = ExtractCoordinate(rotBlock, "w");
                            rotation = new Quaternion(rx, ry, rz, rw);
                        }

                        cookingPatrolPoints.Add(new CookingPatrolPoint(new Vector3(px, py, pz), rotation));
                    }
                }
                else
                {
                    // Old format - just Vector3 positions, use default rotation
                    string[] pointBlocks = pointsSection.Split(new string[] { "}," }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string block in pointBlocks)
                    {
                        string cleanBlock = block.Trim().Trim('{').Trim('}').Trim(',');
                        if (string.IsNullOrEmpty(cleanBlock)) continue;

                        // Extract x, y, z values
                        float x = ExtractCoordinate(cleanBlock, "\"x\"");
                        float y = ExtractCoordinate(cleanBlock, "\"y\"");
                        float z = ExtractCoordinate(cleanBlock, "\"z\"");

                        cookingPatrolPoints.Add(new CookingPatrolPoint(new Vector3(x, y, z), Quaternion.identity));
                    }
                }
                MelonLogger.Msg($"Loaded {cookingPatrolPoints.Count} cooking patrol points (with rotations: {hasRotation}).");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error loading cooking patrol points: " + ex.Message);
            }
        }

        private void SaveTreeFarmPatrolPoints()
        {
            try
            {
                TreeFarmPatrolData data = new TreeFarmPatrolData();
                data.Points = treeFarmPoints;

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"Points\": [");
                for (int i = 0; i < data.Points.Count; i++)
                {
                    var pt = data.Points[i];
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"Position\": {{ \"x\": {pt.Position.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {pt.Position.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"z\": {pt.Position.z.ToString(System.Globalization.CultureInfo.InvariantCulture)} }},");
                    sb.Append($"      \"Rotation\": {{ \"x\": {pt.Rotation.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"y\": {pt.Rotation.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"z\": {pt.Rotation.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"w\": {pt.Rotation.w.ToString(System.Globalization.CultureInfo.InvariantCulture)} }}");
                    sb.AppendLine();
                    sb.Append("    }");
                    if (i < data.Points.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");
                File.WriteAllText(this.GetTreeFarmPatrolPath(), sb.ToString());
                MelonLogger.Msg($"Tree farm patrol points saved! ({treeFarmPoints.Count} points with rotations)");
                this.AddMenuNotification($"Tree farm points saved ({treeFarmPoints.Count})", new Color(0.55f, 0.88f, 1f));
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error saving tree farm patrol points: " + ex.Message);
                this.AddMenuNotification("Failed to save tree farm points", new Color(1f, 0.4f, 0.4f));
            }
        }

        private void LoadTreeFarmPatrolPoints()
        {
            try
            {
                string path = this.GetTreeFarmPatrolPath();
                if (!File.Exists(path))
                {
                    MelonLogger.Msg("Tree farm patrol points file not found.");
                    this.AddMenuNotification("No saved tree farm points found", new Color(1f, 0.55f, 0.55f));
                    return;
                }
                treeFarmPoints.Clear();
                string json = File.ReadAllText(path);

                // Check if this is the new format (with Rotation) or old format (just Vector3)
                bool hasRotation = json.Contains("\"Rotation\"");

                // Parse JSON manually - find all coordinate blocks
                int pointsStart = json.IndexOf("[");
                int pointsEnd = json.LastIndexOf("]");
                if (pointsStart == -1 || pointsEnd == -1) return;

                string pointsSection = json.Substring(pointsStart + 1, pointsEnd - pointsStart - 1);

                if (hasRotation)
                {
                    // New format with Position and Rotation
                    string[] pointBlocks = pointsSection.Split(new string[] { "    }" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string block in pointBlocks)
                    {
                        if (!block.Contains("Position")) continue;

                        // Extract Position block (between "Position": { and })
                        int posStart = block.IndexOf("\"Position\"");
                        if (posStart == -1) continue;
                        int posObjStart = block.IndexOf("{", posStart);
                        int posObjEnd = block.IndexOf("}", posObjStart);
                        string posBlock = block.Substring(posObjStart, posObjEnd - posObjStart + 1);

                        float px = ExtractCoordinate(posBlock, "x");
                        float py = ExtractCoordinate(posBlock, "y");
                        float pz = ExtractCoordinate(posBlock, "z");

                        // Extract Rotation block (between "Rotation": { and })
                        int rotStart = block.IndexOf("\"Rotation\"");
                        Quaternion rotation = Quaternion.identity;
                        if (rotStart != -1)
                        {
                            int rotObjStart = block.IndexOf("{", rotStart);
                            int rotObjEnd = block.IndexOf("}", rotObjStart);
                            string rotBlock = block.Substring(rotObjStart, rotObjEnd - rotObjStart + 1);

                            float rx = ExtractCoordinate(rotBlock, "x");
                            float ry = ExtractCoordinate(rotBlock, "y");
                            float rz = ExtractCoordinate(rotBlock, "z");
                            float rw = ExtractCoordinate(rotBlock, "w");
                            rotation = new Quaternion(rx, ry, rz, rw);
                        }

                        treeFarmPoints.Add(new TreeFarmPatrolPoint(new Vector3(px, py, pz), rotation));
                    }
                }
                else
                {
                    // Old format - just Vector3 positions, use default rotation
                    string[] pointBlocks = pointsSection.Split(new string[] { "}," }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string block in pointBlocks)
                    {
                        string cleanBlock = block.Trim().Trim('{').Trim('}').Trim(',');
                        if (string.IsNullOrEmpty(cleanBlock)) continue;

                        // Extract x, y, z values
                        float x = ExtractCoordinate(cleanBlock, "\"x\"");
                        float y = ExtractCoordinate(cleanBlock, "\"y\"");
                        float z = ExtractCoordinate(cleanBlock, "\"z\"");

                        treeFarmPoints.Add(new TreeFarmPatrolPoint(new Vector3(x, y, z), Quaternion.identity));
                    }
                }
                MelonLogger.Msg($"Loaded {treeFarmPoints.Count} tree farm patrol points (with rotations: {hasRotation}).");
                this.AddMenuNotification($"Tree farm points loaded ({treeFarmPoints.Count})", new Color(0.45f, 1f, 0.55f));
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error loading tree farm patrol points: " + ex.Message);
                this.AddMenuNotification("Failed to load tree farm points", new Color(1f, 0.4f, 0.4f));
            }
        }

        private float ExtractCoordinate(string block, string coord)
        {
            // Match both "x": value and x: value formats
            string pattern = $"\"?{coord}\"?:\\s*([+-]?\\d*\\.?\\d+([eE][+-]?\\d+)?)";
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(block, pattern);
            if (match.Success)
            {
                return float.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
            }
            throw new FormatException($"Could not find \"{coord}\" coordinate in block: {block}");
        }

        // --- AUTO REPAIR METHODS ---
        void StartRepair()
        {
            isRepairing = true;
            repairStep = 0;
            scrollAttempts = 0;
            stepTimer = Time.time;
        }

        void StartAutoEat()
        {
            isAutoEating = true;
            autoEatStep = 0;
            autoEatScrollAttempts = 0;
            autoEatStepTimer = Time.time;
        }

        void ExecuteRepairStep()
        {
            switch (repairStep)
            {
                case 0:
                    if (OpenInventory())
                    {
                        repairStep++;
                        stepTimer = Time.time + 0.5f;
                    }
                    else
                    {
                        isRepairing = false;
                    }
                    break;

                case 1:
                    if (IsRepairKitVisible())
                    {
                        repairStep++;
                        stepTimer = Time.time + 0.2f;
                    }
                    else if (scrollAttempts < MAX_SCROLL_ATTEMPTS)
                    {
                        ForceScrollDown();
                        scrollAttempts++;
                        stepTimer = Time.time + 0.15f;
                    }
                    else
                    {
                        CloseInventory();
                        isRepairing = false;
                    }
                    break;

                case 2:
                    if (ClickRepairKit())
                    {
                        repairStep++;
                        stepTimer = Time.time + 0.5f;
                    }
                    else
                    {
                        CloseInventory();
                        isRepairing = false;
                    }
                    break;

                case 3:
                    if (ClickUseButton())
                    {
                        repairStep++;
                        stepTimer = Time.time + 0.5f;
                    }
                    else
                    {
                        CloseInventory();
                        isRepairing = false;
                    }
                    break;

                case 4:
                    CloseInventory();
                    isRepairing = false;
                    repairStep = 0;
                    break;
            }
        }

        void ExecuteAutoEatStep()
        {
            switch (autoEatStep)
            {
                case 0:
                    if (OpenInventory())
                    {
                        autoEatStep++;
                        autoEatStepTimer = Time.time + 0.5f;
                    }
                    else
                    {
                        isAutoEating = false;
                    }
                    break;

                case 1:
                    if (IsFoodVisible())
                    {
                        autoEatStep++;
                        autoEatStepTimer = Time.time + 0.2f;
                    }
                    else if (autoEatScrollAttempts < MAX_SCROLL_ATTEMPTS)
                    {
                        ForceScrollDown();
                        autoEatScrollAttempts++;
                        autoEatStepTimer = Time.time + 0.15f;
                    }
                    else
                    {
                        CloseInventory();
                        isAutoEating = false;
                    }
                    break;

                case 2:
                    if (ClickFoodItem())
                    {
                        autoEatStep++;
                        autoEatStepTimer = Time.time + 0.5f;
                    }
                    else
                    {
                        CloseInventory();
                        isAutoEating = false;
                    }
                    break;

                case 3:
                    if (ClickUseButton())
                    {
                        autoEatStep++;
                        autoEatStepTimer = Time.time + 0.5f;
                    }
                    else
                    {
                        CloseInventory();
                        isAutoEating = false;
                    }
                    break;

                case 4:
                    CloseInventory();
                    isAutoEating = false;
                    autoEatStep = 0;
                    break;
            }
        }

        void ForceScrollDown()
        {
            var content = GameObject.Find(SCROLL_CONTENT_PATH);
            if (content != null)
            {
                Vector3 pos = content.transform.localPosition;
                pos.y += 200f;
                content.transform.localPosition = pos;
            }
        }

        bool IsRepairKitVisible()
        {
            return GameObject.FindObjectsOfType<Image>().Any(img =>
                img != null &&
                img.sprite != null &&
                img.gameObject.activeInHierarchy &&
                img.sprite.name == REPAIR_KIT_SPRITE);
        }

        bool ClickRepairKit()
        {
            var kit = GameObject.FindObjectsOfType<Image>().FirstOrDefault(img =>
                img != null &&
                img.sprite != null &&
                img.gameObject.activeInHierarchy &&
                img.sprite.name == REPAIR_KIT_SPRITE);

            if (kit == null) return false;

            Transform current = kit.transform;
            for (int i = 0; i < 5; i++)
            {
                if (current == null) break;
                if (SimulateClick(current.gameObject))
                    return true;
                current = current.parent;
            }
            return false;
        }

        bool IsFoodVisible()
        {
            return GameObject.FindObjectsOfType<Image>().Any(img =>
                img != null &&
                img.sprite != null &&
                img.gameObject.activeInHierarchy &&
                img.sprite.name.ToLowerInvariant().Contains(AUTO_EAT_FOOD_KEY));
        }

        bool ClickFoodItem()
        {
            var food = GameObject.FindObjectsOfType<Image>().FirstOrDefault(img =>
                img != null &&
                img.sprite != null &&
                img.gameObject.activeInHierarchy &&
                img.sprite.name.ToLowerInvariant().Contains(AUTO_EAT_FOOD_KEY));

            if (food == null) return false;

            Transform current = food.transform;
            for (int i = 0; i < 5; i++)
            {
                if (current == null) break;
                if (SimulateClick(current.gameObject))
                    return true;
                current = current.parent;
            }
            return false;
        }

        bool SimulateClick(GameObject target)
        {
            var eventTrigger = target.GetComponent<EventTrigger>();
            if (eventTrigger != null && eventTrigger.triggers.Count > 0)
            {
                PointerEventData eventData = new PointerEventData(EventSystem.current);
                foreach (var trigger in eventTrigger.triggers)
                {
                    if (trigger.eventID == EventTriggerType.PointerClick ||
                        trigger.eventID == EventTriggerType.PointerDown)
                    {
                        trigger.callback.Invoke(eventData);
                        return true;
                    }
                }
            }
            return ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler) ||
                   ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);
        }

        bool OpenInventory()
        {
            var btn = GameObject.Find(BAG_BUTTON_PATH)?.GetComponent<Button>();
            if (btn != null && btn.interactable)
            {
                btn.onClick.Invoke();
                return true;
            }
            return false;
        }

        bool ClickUseButton()
        {
            var btn = GameObject.Find(USE_BUTTON_PATH)?.GetComponent<Button>();
            if (btn != null && btn.interactable)
            {
                var txt = btn.GetComponentInChildren<Text>();
                if (txt == null)
                {
                    btn.onClick.Invoke();
                    return true;
                }
                string actionText = txt.text == null ? string.Empty : txt.text.Trim();
                if (actionText.Equals("Use", StringComparison.OrdinalIgnoreCase) ||
                    actionText.Equals("Eat", StringComparison.OrdinalIgnoreCase))
                {
                    btn.onClick.Invoke();
                    return true;
                }
            }
            return false;
        }

        void CloseInventory()
        {
            var btn = GameObject.Find(CLOSE_BUTTON_PATH)?.GetComponent<Button>();
            btn?.onClick?.Invoke();
        }

        // Token: 0x06000026B RID: 38B - Bulk Selector Tab
        private float DrawBulkSelectorTab(int startY)
        {
            int num = startY;

            // Custom Styles
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
            GUIStyle centeredGrayLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            centeredGrayLabel.normal.textColor = Color.white;
            
            // --- HEADER ---
            GUI.Label(new Rect(20f, (float)num, 260f, 25f), "INVENTORY BULK MANAGER", headerStyle);
            num += 30;
            
            // Refresh Button
            bool flagRefresh = this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), "REFRESH & SCAN");
            if (flagRefresh)
            {
                this.RefreshBulkSelectorCache();
            }
            num += 40;
            
            // --- ITEM LIST ---
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Detected Items: {HeartopiaComplete.discoveredItems.Count}");
            num += 20;
            
            // List Background Box
            GUI.Box(new Rect(20f, (float)num, 260f, 200f), "");
            
            // Scroll View
            Rect scrollViewRect = new Rect(22f, (float)num + 2f, 256f, 196f);
            Rect scrollContentRect = new Rect(0f, 0f, 235f, Mathf.Max(200f, HeartopiaComplete.discoveredItems.Count * 28f));
            this.bulkSelectorScrollPos = GUI.BeginScrollView(scrollViewRect, this.bulkSelectorScrollPos, scrollContentRect);
            
            int itemY = 0;
            if (HeartopiaComplete.discoveredItems.Count == 0)
            {
                GUI.Label(new Rect(0f, 80f, 235f, 40f), "No items found.\nOpen inventory to scan.", centeredGrayLabel);
            }
            else
            {
                foreach (string itemName in HeartopiaComplete.discoveredItems)
                {
                    bool isSelected = (this.selectedBulkItemID == itemName);
                    string displayName = itemName.Replace("ui_item_normal_p_", "").Replace("ui_item_normal_", "").ToUpper();
                    
                    string prefix = isSelected ? "▶ " : "  ";
                    if (GUI.Button(new Rect(2f, (float)itemY, 235f, 26f), prefix + displayName, isSelected ? (this.themeSidebarButtonActiveStyle ?? GUI.skin.button) : (this.themeSidebarButtonStyle ?? GUI.skin.button)))
                    {
                        this.selectedBulkItemID = itemName;
                    }
                    
                    itemY += 28;
                }
            }
            GUI.EndScrollView();
            num += 205;
            
            // --- ACTION PANEL ---
            bool hasSelection = !string.IsNullOrEmpty(this.selectedBulkItemID);
            
            // Status Box
            GUI.Box(new Rect(20f, (float)num, 260f, 160f), "");
            int panelY = num + 10;
            
            if (hasSelection)
            {
                int slotCount = HeartopiaComplete.slotCache.ContainsKey(this.selectedBulkItemID) ? 
                    HeartopiaComplete.slotCache[this.selectedBulkItemID].Count : 0;
                
                string displayName = this.selectedBulkItemID.Replace("ui_item_normal_p_", "").Replace("ui_item_normal_", "").ToUpper();
                
                GUI.Label(new Rect(30f, (float)panelY, 240f, 20f), "SELECTED ITEM:");
                panelY += 18;
                
                GUIStyle selectionStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                selectionStyle.normal.textColor = Color.cyan;
                GUI.Label(new Rect(30f, (float)panelY, 240f, 20f), displayName, selectionStyle);
                panelY += 22;
                
                GUI.Label(new Rect(30f, (float)panelY, 240f, 20f), $"Available Stacks: {slotCount}");
                panelY += 25;
                
                if (this.DrawPrimaryActionButton(new Rect(30f, (float)panelY, 240f, 30f), "QUICK SELECT ALL"))
                {
                    this.ExecuteBulkSelection();
                }
                panelY += 35;
                
                if (this.DrawPrimaryActionButton(new Rect(30f, (float)panelY, 240f, 30f), "QUICK SELL ALL"))
                {
                    this.ExecuteBulkSell();
                }
            }
            else
            {
                GUI.Label(new Rect(20f, (float)num, 260f, 160f), "Select an item from the list\nto enable bulk actions", centeredGrayLabel);
            }
            
            num += 165;
            
            // Footer
            GUIStyle footerStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            footerStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), "Auto-detects items when viewing bag", footerStyle);
            return (float)num + 40f;
        }
        
        // Token: 0x06000026C - Execute Bulk Selection
        private void ExecuteBulkSelection()
        {
            if (!HeartopiaComplete.slotCache.ContainsKey(this.selectedBulkItemID))
            {
                MelonLogger.Msg("[BULK SELECTOR] No slots found for selected item!");
                return;
            }
            
            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            int clickedSlots = 0;
            
            foreach (Transform slot in HeartopiaComplete.slotCache[this.selectedBulkItemID])
            {
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;
                
                Transform addBtn = slot.Find("Root/add@w");
                if (addBtn != null && addBtn.gameObject.activeInHierarchy)
                {
                    for (int i = 0; i < 200; i++)
                    {
                        ExecuteEvents.Execute(addBtn.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
                    }
                    clickedSlots++;
                }
            }
            
            string displayName = this.selectedBulkItemID.Replace("ui_item_normal_p_", "").Replace("ui_item_normal_", "");
            MelonLogger.Msg($"[BULK SELECTOR] Executed 200 clicks on {clickedSlots} slots for {displayName}");
        }
        
        // Token: 0x06000026D - Execute Bulk Sell
        private void ExecuteBulkSell()
        {
            // Find the sell confirm button in the ItemSellPanel
            GameObject sellButton = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Popup/ItemSellPanel(Clone)/commonSidePicker@w/bagPanel@go/bottom@go/confirmButton@btn");
            
            if (sellButton == null || !sellButton.activeInHierarchy)
            {
                MelonLogger.Msg("[BULK SELL] Sell panel not found or not active! Make sure sell panel is open.");
                return;
            }
            
            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            
            // Click the sell button
            ExecuteEvents.Execute(sellButton, pointerData, ExecuteEvents.pointerClickHandler);
            
            string displayName = this.selectedBulkItemID.Replace("ui_item_normal_p_", "").Replace("ui_item_normal_", "");
            MelonLogger.Msg($"[BULK SELL] Clicked sell button for {displayName}");
        }
        
        // Token: 0x06000026E - Refresh Bulk Selector Cache
        private void RefreshBulkSelectorCache()
        {
            HeartopiaComplete.discoveredItems.Clear();
            HeartopiaComplete.slotCache.Clear();
            this.selectedBulkItemID = "";
            
            // Find all currently active Image components and check for inventory items
            UnityEngine.UI.Image[] allImages = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Image>(true);
            int foundItems = 0;
            
            foreach (UnityEngine.UI.Image img in allImages)
            {
                if (img.sprite != null && img.sprite.name.Contains("ui_item_normal"))
                {
                    string spriteName = img.sprite.name;
                    if (!HeartopiaComplete.discoveredItems.Contains(spriteName))
                    {
                        HeartopiaComplete.discoveredItems.Add(spriteName);
                        foundItems++;
                    }
                    
                    // Find the slot transform (same logic as the Harmony patch)
                    Transform slot = img.transform.parent?.parent;
                    if (slot != null)
                    {
                        if (!HeartopiaComplete.slotCache.ContainsKey(spriteName))
                        {
                            HeartopiaComplete.slotCache[spriteName] = new List<Transform>();
                        }
                        if (!HeartopiaComplete.slotCache[spriteName].Contains(slot))
                        {
                            HeartopiaComplete.slotCache[spriteName].Add(slot);
                        }
                    }
                }
            }
            
            MelonLogger.Msg($"[BULK SELECTOR] Cache refreshed! Found {foundItems} unique items from {HeartopiaComplete.discoveredItems.Count} total detections.");
        }

        private float DrawSettingsTab(int startY)
        {
            if (this.settingsSubTab == 0)
            {
                return this.DrawSettingsMainTab(startY);
            }

            if (this.settingsSubTab == 2)
            {
                return this.DrawUiThemeTab(startY);
            }

            int num = startY;
            
            // Header
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
            GUI.Label(new Rect(20f, (float)num, 260f, 25f), "KEYBIND SETTINGS", headerStyle);
            num += 30;

            // Rebinding Mode
            if (!string.IsNullOrEmpty(this.keyBindingActive))
            {
            GUI.Box(new Rect(20f, (float)num, 260f, 60f), "");
            GUIStyle centerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(20f, (float)num + 10f, 260f, 20f), "PRESS ANY KEY FOR:", centerStyle);
            centerStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(20f, (float)num + 30f, 260f, 20f), this.keyBindingActive.ToUpper(), centerStyle);
                
                if (GUI.Button(new Rect(20f, (float)num + 60f, 260f, 30f), "CANCEL"))
                {
                    this.keyBindingActive = "";
                }

                // Capture Input
                Event e = Event.current;
                if (e.isKey && e.type == EventType.KeyDown)
                {
                    KeyCode newKey = e.keyCode;
                    if (newKey != KeyCode.None)
                    {
                        string bindingLabel = this.keyBindingActive;
                        if (newKey == KeyCode.Escape)
                        {
                            newKey = KeyCode.None;
                        }
                        switch (this.keyBindingActive)
                        {
                            case "Toggle Menu": this.keyToggleMenu = newKey; break;
                            case "Toggle Radar": this.keyToggleRadar = newKey; break;
                            case "Auto Farm": this.keyAutoFarm = newKey; break;
                            case "Auto Cook": this.keyAutoCook = newKey; break;
                            case "Bypass UI": this.keyBypassUI = newKey; break;
                            case "Disable All": this.keyDisableAll = newKey; break;
                            case "Inspect Player": this.keyInspectPlayer = newKey; break;
                            case "Inspect Move": this.keyInspectMove = newKey; break;
                            case "Jump Boost": this.keyJumpBoost = newKey; break;
                            case "Auto Repair": this.keyAutoRepair = newKey; break;
                            case "Auto Join Friend": this.keyAutoJoinFriend = newKey; break;
                        }
                        this.keyBindingActive = "";
                        this.SaveKeybinds();
                        string keyName = newKey == KeyCode.None ? "None" : newKey.ToString();
                        this.AddMenuNotification($"{bindingLabel}: {keyName}", new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB));
                    }
                }
                return (float)num + 450f;
            }

            // Bind List
            this.DrawKeybindRow(ref num, "Toggle Menu", ref this.keyToggleMenu);
            this.DrawKeybindRow(ref num, "Toggle Radar", ref this.keyToggleRadar);
            this.DrawKeybindRow(ref num, "Auto Farm", ref this.keyAutoFarm);
            this.DrawKeybindRow(ref num, "Auto Cook", ref this.keyAutoCook);
            this.DrawKeybindRow(ref num, "Bypass UI", ref this.keyBypassUI);
            this.DrawKeybindRow(ref num, "Disable All", ref this.keyDisableAll);
            this.DrawKeybindRow(ref num, "Inspect Player", ref this.keyInspectPlayer);
            this.DrawKeybindRow(ref num, "Inspect Move", ref this.keyInspectMove);
            this.DrawKeybindRow(ref num, "Jump Boost", ref this.keyJumpBoost);
            this.DrawKeybindRow(ref num, "Auto Repair", ref this.keyAutoRepair);
            this.DrawKeybindRow(ref num, "Auto Join Friend", ref this.keyAutoJoinFriend);
            this.DrawKeybindRow(ref num, "Join Public", ref this.keyJoinPublic);
            this.DrawKeybindRow(ref num, "Join My Town", ref this.keyJoinMyTown);
            num += 6;

            if (this.DrawDangerActionButton(new Rect(20f, (float)num, 260f, 30f), "RESET TO DEFAULTS"))
            {
                this.keyToggleMenu = KeyCode.Insert;
                this.keyToggleRadar = KeyCode.None;
                this.keyAutoFarm = KeyCode.None;
                this.keyAutoCook = KeyCode.None;
                this.keyBypassUI = KeyCode.None;
                this.keyDisableAll = KeyCode.None;
                this.keyInspectPlayer = KeyCode.None;
                this.keyInspectMove = KeyCode.None;
                this.keyJumpBoost = KeyCode.None;
                this.keyAutoRepair = KeyCode.None;
                this.keyAutoJoinFriend = KeyCode.None;
                this.keyJoinMyTown = KeyCode.None;
                this.notificationsEnabled = true;
                this.hideIdEnabled = false;
                this.SaveKeybinds();
                this.AddMenuNotification("Defaults restored (Toggle Menu: Insert)", new Color(1f, 0.75f, 0.75f));
            }

            return (float)num + 38f;
        }

        private float DrawSettingsMainTab(int startY)
        {
            int num = startY;
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
            GUI.Label(new Rect(20f, (float)num, 260f, 24f), "LOBBY AUTOMATION", headerStyle);
            num += 30;

            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 36f), "Join Friend"))
            {
                this.StartLobbyAutoJoinFriend("Manual button");
            }
            num += 44;

            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 36f), "Join My Town"))
            {
                this.StartLobbyAutoJoinMyTown("Manual button");
            }
            num += 44;

            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 36f), "Join Public"))
            {
                this.autoJoinFriendEnabled = false;
                this.autoClickStartEnabled = false;
                this.ClickButtonIfExistsReturn(START_GAME_BUTTON_PATH);
            }

            num += 48;
            GUI.Label(new Rect(20f, (float)num, 260f, 24f), "SETTINGS", headerStyle);
            num += 30;

            bool newNotificationsEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 24f), this.notificationsEnabled, "Enable Notifications");
            if (newNotificationsEnabled != this.notificationsEnabled)
            {
                this.notificationsEnabled = newNotificationsEnabled;
                this.SaveKeybinds();
                if (this.notificationsEnabled)
                {
                    this.AddMenuNotification("Notifications enabled", new Color(0.55f, 0.88f, 1f));
                }
            }

            num += 26;
            bool newHideIdEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 24f), this.hideIdEnabled, "Hide ID");
            if (newHideIdEnabled != this.hideIdEnabled)
            {
                this.hideIdEnabled = newHideIdEnabled;
                this.SaveKeybinds();
                if (this.hideIdEnabled)
                {
                    this.AddMenuNotification("ID display hidden", new Color(0.55f, 0.88f, 1f));
                }
                else
                {
                    this.AddMenuNotification("ID display shown", new Color(0.55f, 0.88f, 1f));
                }
            }

            return (float)num + 26f;
        }

        private float DrawUiThemeTab(int startY)
        {
            int num = startY;
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
            GUI.Label(new Rect(20f, (float)num, 260f, 25f), "UI THEME", headerStyle);
            num += 30;

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), "Theme Colors");
            num += 24;

            string[] colorTargets = new string[]
            {
                "Accent",
                "Text",
                "Main Tab Text",
                "Sub Tab Text",
                "Window Bg",
                "Panel Bg",
                "Content Bg"
            };

            for (int i = 0; i < colorTargets.Length; i++)
            {
                Rect rowRect = new Rect(20f, (float)num, 260f, 28f);
                GUI.Box(rowRect, "", this.themeTopTabStyle ?? GUI.skin.box);
                GUI.Label(new Rect(rowRect.x + 10f, rowRect.y + 4f, 180f, 20f), colorTargets[i]);

                Rect swatchRect = new Rect(rowRect.xMax - 30f, rowRect.y + 4f, 20f, 20f);
                Color targetColor = this.GetUiThemeColorTargetValue(i);
                GUI.color = targetColor;
                GUI.DrawTexture(swatchRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                if (this.uiThemeColorTarget == i && this.uiThemePickerOpen)
                {
                    GUI.DrawTexture(new Rect(swatchRect.x - 2f, swatchRect.y - 2f, swatchRect.width + 4f, 1f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(swatchRect.x - 2f, swatchRect.yMax + 1f, swatchRect.width + 4f, 1f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(swatchRect.x - 2f, swatchRect.y - 1f, 1f, swatchRect.height + 2f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(swatchRect.xMax + 1f, swatchRect.y - 1f, 1f, swatchRect.height + 2f), Texture2D.whiteTexture);
                }

                if (GUI.Button(rowRect, "", GUIStyle.none))
                {
                    if (this.uiThemeColorTarget == i && this.uiThemePickerOpen)
                    {
                        this.uiThemePickerOpen = false;
                    }
                    else
                    {
                        this.uiThemeColorTarget = i;
                        this.uiThemeHexInput = this.ColorToHex(this.GetUiThemeColorTargetValue(this.uiThemeColorTarget));
                        this.uiThemePickerOpen = true;
                    }
                }
                num += 32;
            }

            num += 6;

            bool changed = false;
            Color originalColor = this.GetUiThemeColorTargetValue(this.uiThemeColorTarget);
            float h;
            float s;
            float v;
            Color.RGBToHSV(originalColor, out h, out s, out v);
            Color pickedColor = originalColor;

            if (this.uiThemePickerOpen)
            {
                this.EnsureUiPickerTextures(h);
                Rect svRect = new Rect(20f, (float)num, 220f, 180f);
                Rect hueRect = new Rect(246f, (float)num, 18f, 180f);
                GUI.DrawTexture(svRect, this.uiSvTexture);
                GUI.DrawTexture(hueRect, this.uiHueTexture);

                Event e = Event.current;
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    if (svRect.Contains(e.mousePosition))
                    {
                        s = Mathf.Clamp01((e.mousePosition.x - svRect.x) / svRect.width);
                        v = 1f - Mathf.Clamp01((e.mousePosition.y - svRect.y) / svRect.height);
                        changed = true;
                        e.Use();
                    }
                    else if (hueRect.Contains(e.mousePosition))
                    {
                        h = 1f - Mathf.Clamp01((e.mousePosition.y - hueRect.y) / hueRect.height);
                        this.EnsureUiPickerTextures(h);
                        changed = true;
                        e.Use();
                    }
                }

                float svX = svRect.x + s * svRect.width;
                float svY = svRect.y + (1f - v) * svRect.height;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(svX - 1f, svY - 6f, 2f, 12f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(svX - 6f, svY - 1f, 12f, 2f), Texture2D.whiteTexture);
                float hueY = hueRect.y + (1f - h) * hueRect.height;
                GUI.DrawTexture(new Rect(hueRect.x - 2f, hueY - 1f, hueRect.width + 4f, 2f), Texture2D.whiteTexture);

                pickedColor = Color.HSVToRGB(h, s, v);
                if (changed)
                {
                    this.SetUiThemeColorTargetValue(this.uiThemeColorTarget, pickedColor);
                    this.uiThemeHexInput = this.ColorToHex(pickedColor);
                }

                Rect previewCurrent = new Rect(272f, (float)num, 50f, 84f);
                Rect previewOriginal = new Rect(272f, (float)num + 96f, 50f, 84f);
                GUI.Box(previewCurrent, "");
                GUI.Box(previewOriginal, "");
                GUI.color = pickedColor;
                GUI.DrawTexture(new Rect(previewCurrent.x + 4f, previewCurrent.y + 4f, 42f, 76f), Texture2D.whiteTexture);
                GUI.color = originalColor;
                GUI.DrawTexture(new Rect(previewOriginal.x + 4f, previewOriginal.y + 4f, 42f, 76f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(328f, (float)num + 10f, 100f, 18f), "Current");
                GUI.Label(new Rect(328f, (float)num + 106f, 100f, 18f), "Original");

                num += 188;

                int r = Mathf.RoundToInt(pickedColor.r * 255f);
                int g = Mathf.RoundToInt(pickedColor.g * 255f);
                int b = Mathf.RoundToInt(pickedColor.b * 255f);
                GUI.Label(new Rect(20f, (float)num, 220f, 18f), $"R:{r}  G:{g}  B:{b}");
                GUI.Label(new Rect(250f, (float)num, 220f, 18f), $"H:{Mathf.RoundToInt(h * 360f)}  S:{Mathf.RoundToInt(s * 100f)}  V:{Mathf.RoundToInt(v * 100f)}");
                num += 22;

                GUI.Label(new Rect(20f, (float)num, 40f, 20f), "Hex:");
                this.uiThemeHexInput = GUI.TextField(new Rect(60f, (float)num, 140f, 24f), this.uiThemeHexInput);
                if (GUI.Button(new Rect(208f, (float)num, 72f, 24f), "Apply"))
                {
                    Color parsed;
                    if (this.TryParseHexColor(this.uiThemeHexInput, out parsed))
                    {
                        this.SetUiThemeColorTargetValue(this.uiThemeColorTarget, parsed);
                        this.uiThemeHexInput = this.ColorToHex(parsed);
                        changed = true;
                    }
                }
                num += 34;
            }
            else
            {
                GUI.Label(new Rect(20f, (float)num, 360f, 20f), "Click any color swatch to open picker");
                num += 24;
            }

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), "Transparency");
            num += 24;

            GUI.Label(new Rect(20f, (float)num, 260f, 18f), $"Window Alpha: {this.uiWindowAlpha:F2}");
            num += 18;
            float newWindowA = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.uiWindowAlpha, 0.15f, 1f);
            if (Math.Abs(newWindowA - this.uiWindowAlpha) > 0.001f) { this.uiWindowAlpha = newWindowA; changed = true; }
            num += 26;

            GUI.Label(new Rect(20f, (float)num, 260f, 18f), $"Panel Alpha: {this.uiPanelAlpha:F2}");
            num += 18;
            float newPanelA = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.uiPanelAlpha, 0.15f, 1f);
            if (Math.Abs(newPanelA - this.uiPanelAlpha) > 0.001f) { this.uiPanelAlpha = newPanelA; changed = true; }
            num += 26;

            GUI.Label(new Rect(20f, (float)num, 260f, 18f), $"Content Alpha: {this.uiContentAlpha:F2}");
            num += 18;
            float newContentA = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.uiContentAlpha, 0.15f, 1f);
            if (Math.Abs(newContentA - this.uiContentAlpha) > 0.001f) { this.uiContentAlpha = newContentA; changed = true; }
            num += 32;

            if (changed)
            {
                this.uiAccentR = Mathf.Clamp01(this.uiAccentR);
                this.uiAccentG = Mathf.Clamp01(this.uiAccentG);
                this.uiAccentB = Mathf.Clamp01(this.uiAccentB);
                this.uiTextR = Mathf.Clamp01(this.uiTextR);
                this.uiTextG = Mathf.Clamp01(this.uiTextG);
                this.uiTextB = Mathf.Clamp01(this.uiTextB);
                this.uiMainTabTextR = Mathf.Clamp01(this.uiMainTabTextR);
                this.uiMainTabTextG = Mathf.Clamp01(this.uiMainTabTextG);
                this.uiMainTabTextB = Mathf.Clamp01(this.uiMainTabTextB);
                this.uiSubTabTextR = Mathf.Clamp01(this.uiSubTabTextR);
                this.uiSubTabTextG = Mathf.Clamp01(this.uiSubTabTextG);
                this.uiSubTabTextB = Mathf.Clamp01(this.uiSubTabTextB);
                this.uiWindowR = Mathf.Clamp01(this.uiWindowR);
                this.uiWindowG = Mathf.Clamp01(this.uiWindowG);
                this.uiWindowB = Mathf.Clamp01(this.uiWindowB);
                this.uiPanelR = Mathf.Clamp01(this.uiPanelR);
                this.uiPanelG = Mathf.Clamp01(this.uiPanelG);
                this.uiPanelB = Mathf.Clamp01(this.uiPanelB);
                this.uiContentR = Mathf.Clamp01(this.uiContentR);
                this.uiContentG = Mathf.Clamp01(this.uiContentG);
                this.uiContentB = Mathf.Clamp01(this.uiContentB);
                this.uiWindowAlpha = Mathf.Clamp(this.uiWindowAlpha, 0.15f, 1f);
                this.uiPanelAlpha = Mathf.Clamp(this.uiPanelAlpha, 0.15f, 1f);
                this.uiContentAlpha = Mathf.Clamp(this.uiContentAlpha, 0.15f, 1f);
                this.InvalidateThemeCache();
            }

            GUI.backgroundColor = new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB, 0.9f);
            if (GUI.Button(new Rect(20f, (float)num, 82f, 30f), "SAVE"))
            {
                this.SaveUiTheme();
            }
            GUI.backgroundColor = Color.white;

            if (GUI.Button(new Rect(110f, (float)num, 82f, 30f), "LOAD"))
            {
                this.LoadUiTheme();
            }

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f, 0.9f);
            if (GUI.Button(new Rect(198f, (float)num, 82f, 30f), "RESET"))
            {
                this.uiAccentR = 0.48f;
                this.uiAccentG = 0.42f;
                this.uiAccentB = 1f;
                this.uiTextR = 0.9f;
                this.uiTextG = 0.92f;
                this.uiTextB = 0.96f;
                this.uiMainTabTextR = 0.86f;
                this.uiMainTabTextG = 0.88f;
                this.uiMainTabTextB = 0.93f;
                this.uiSubTabTextR = 0.46f;
                this.uiSubTabTextG = 0.5f;
                this.uiSubTabTextB = 0.58f;
                this.uiWindowR = 0.016f;
                this.uiWindowG = 0.018f;
                this.uiWindowB = 0.028f;
                this.uiPanelR = 0.026f;
                this.uiPanelG = 0.03f;
                this.uiPanelB = 0.045f;
                this.uiContentR = 0.03f;
                this.uiContentG = 0.035f;
                this.uiContentB = 0.05f;
                this.uiWindowAlpha = 0.97f;
                this.uiPanelAlpha = 0.95f;
                this.uiContentAlpha = 0.96f;
                this.uiThemeHexInput = this.ColorToHex(new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB));
                this.InvalidateThemeCache();
            }
            GUI.backgroundColor = Color.white;
            num += 42;

            GUI.Box(new Rect(20f, (float)num, 260f, 36f), "");
            GUIStyle previewStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            previewStyle.normal.textColor = new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB, 1f);
            GUI.Label(new Rect(20f, (float)num, 260f, 36f), "Accent Preview", previewStyle);
            num += 44;

            GUI.Label(new Rect(20f, (float)num, 360f, 40f), "Tip: Use SAVE to persist theme.\nTheme file: UserData/ui_theme.json");
            return (float)num + 50f;
        }

        private void EnsureUiPickerTextures(float hue)
        {
            if (this.uiHueTexture == null)
            {
                this.uiHueTexture = this.CreateHueTexture(18, 180);
                this.themeTextures.Add(this.uiHueTexture);
            }

            if (this.uiSvTexture == null || Math.Abs(this.uiPickerHueCached - hue) > 0.001f)
            {
                if (this.uiSvTexture != null)
                {
                    Object.Destroy(this.uiSvTexture);
                    this.themeTextures.Remove(this.uiSvTexture);
                }
                this.uiSvTexture = this.CreateSvTexture(220, 180, hue);
                this.uiPickerHueCached = hue;
                this.themeTextures.Add(this.uiSvTexture);
            }
        }

        private Texture2D CreateHueTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < height; y++)
            {
                float h = (float)y / (height - 1);
                Color c = Color.HSVToRGB(h, 1f, 1f);
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private Texture2D CreateSvTexture(int width, int height, float hue)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                for (int x = 0; x < width; x++)
                {
                    float s = (float)x / (width - 1);
                    tex.SetPixel(x, y, Color.HSVToRGB(hue, s, v));
                }
            }
            tex.Apply();
            return tex;
        }

        private Color GetUiThemeColorTargetValue(int target)
        {
            if (target == 0) return new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB);
            if (target == 1) return new Color(this.uiTextR, this.uiTextG, this.uiTextB);
            if (target == 2) return new Color(this.uiMainTabTextR, this.uiMainTabTextG, this.uiMainTabTextB);
            if (target == 3) return new Color(this.uiSubTabTextR, this.uiSubTabTextG, this.uiSubTabTextB);
            if (target == 4) return new Color(this.uiWindowR, this.uiWindowG, this.uiWindowB);
            if (target == 5) return new Color(this.uiPanelR, this.uiPanelG, this.uiPanelB);
            return new Color(this.uiContentR, this.uiContentG, this.uiContentB);
        }

        private void SetUiThemeColorTargetValue(int target, Color color)
        {
            if (target == 0)
            {
                this.uiAccentR = color.r; this.uiAccentG = color.g; this.uiAccentB = color.b;
            }
            else if (target == 1)
            {
                this.uiTextR = color.r; this.uiTextG = color.g; this.uiTextB = color.b;
            }
            else if (target == 2)
            {
                this.uiMainTabTextR = color.r; this.uiMainTabTextG = color.g; this.uiMainTabTextB = color.b;
            }
            else if (target == 3)
            {
                this.uiSubTabTextR = color.r; this.uiSubTabTextG = color.g; this.uiSubTabTextB = color.b;
            }
            else if (target == 4)
            {
                this.uiWindowR = color.r; this.uiWindowG = color.g; this.uiWindowB = color.b;
            }
            else if (target == 5)
            {
                this.uiPanelR = color.r; this.uiPanelG = color.g; this.uiPanelB = color.b;
            }
            else
            {
                this.uiContentR = color.r; this.uiContentG = color.g; this.uiContentB = color.b;
            }
        }

        private string ColorToHex(Color color)
        {
            int r = Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
            int g = Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
            int b = Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private bool TryParseHexColor(string input, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(input)) return false;
            string hex = input.Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length != 6) return false;

            int r;
            int g;
            int b;
            if (!int.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out r)) return false;
            if (!int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out g)) return false;
            if (!int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out b)) return false;
            color = new Color(r / 255f, g / 255f, b / 255f, 1f);
            return true;
        }

        private void DrawKeybindRow(ref int y, string label, ref KeyCode key)
        {
            GUI.Label(new Rect(20f, (float)y, 140f, 25f), label);
            
            string btnText = key.ToString();
            // Shorten some long key names
            if (btnText.StartsWith("Joystick")) btnText = "Gamepad";
            
            if (GUI.Button(new Rect(160f, (float)y, 120f, 25f), btnText))
            {
                this.keyBindingActive = label;
            }
            y += 30;
        }

        private float DrawPatrolTab(int startY)
        {
            int num = startY;

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
            GUI.Label(new Rect(20f, (float)num, 260f, 25f), "PATROL SYSTEM", headerStyle);
            num += 30;

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Points: {patrolPoints.Count}");
            num += 25;

            if (GUI.Button(new Rect(20f, (float)num, 120f, 35f), "SAVE"))
            {
                SavePatrolPoints();
            }
            if (GUI.Button(new Rect(160f, (float)num, 120f, 35f), "LOAD"))
            {
                LoadPatrolPoints();
            }
            num += 45;

            if (GUI.Button(new Rect(20f, (float)num, 260f, 40f), "ADD CURRENT POSITION"))
            {
                GameObject p = GetPlayer();
                if (p != null)
                {
                    patrolPoints.Add(p.transform.position);
                    MelonLogger.Msg("Added patrol point at current position.");
                }
            }
            num += 50;

            if (GUI.Button(new Rect(20f, (float)num, 260f, 35f), "CLEAR ALL"))
            {
                patrolPoints.Clear();
                MelonLogger.Msg("Cleared all patrol points.");
            }
            num += 45;

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Wait Time: {waitAtSpot:F2}s");
            num += 22;
            waitAtSpot = Mathf.Round(this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), waitAtSpot, 0.1f, 2.0f) * 100f) / 100f;
            num += 40;

            GUI.color = isPatrolActive ? Color.red : Color.green;
            if (GUI.Button(new Rect(20f, (float)num, 260f, 50f), isPatrolActive ? "STOP PATROL" : "START PATROL"))
            {
                if (isPatrolActive)
                {
                    isPatrolActive = false;
                    if (patrolCoroutine != null)
                    {
                        MelonCoroutines.Stop(patrolCoroutine);
                        patrolCoroutine = null;
                    }
                    MelonLogger.Msg("Patrol stopped.");
                }
                else
                {
                    StartPatrol();
                }
            }
            GUI.color = Color.white;

            return (float)num + 60f;
        }

        // Token: 0x06000027 RID: 39 RVA: 0x00007014 File Offset: 0x00005214
        private void InspectMovementComponent()
        {
            GameObject gameObject = GameObject.Find("p_player_skeleton(Clone)");
            bool flag = gameObject == null;
            if (flag)
            {
                MelonLogger.Msg("Player not found!");
            }
            else
            {
                Component[] array = gameObject.GetComponents<Component>();
                Component component = null;
                foreach (Component component2 in array)
                {
                    bool flag2 = component2 == null;
                    if (!flag2)
                    {
                        Il2CppType il2CppType = component2.GetIl2CppType();
                        bool flag3 = il2CppType != null && il2CppType.Name == "DynamicMonoBehaviour";
                        if (flag3)
                        {
                            component = component2;
                            break;
                        }
                    }
                }
                bool flag4 = component == null;
                if (flag4)
                {
                    MelonLogger.Msg("DynamicMonoBehaviour not found!");
                }
                else
                {
                    MelonLogger.Msg($"=== DynamicMonoBehaviour INSPECTION ===");
                    FieldInfo[] fields = component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    MelonLogger.Msg($"Total fields: {fields.Length}");
                    foreach (FieldInfo fieldInfo in fields)
                    {
                        try
                        {
                            object value = fieldInfo.GetValue(component);
                            MelonLogger.Msg($"Field: {fieldInfo.Name} = {value} ({fieldInfo.FieldType.Name})");
                        }
                        catch
                        {
                            MelonLogger.Msg("Field: " + fieldInfo.Name + " (couldn't read value)");
                        }
                    }
                    MethodInfo[] methods = component.GetType().GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    MelonLogger.Msg($"\nTotal methods: {methods.Length}");
                    foreach (MethodInfo methodInfo in methods)
                    {
                        string value2 = string.Join(", ", from p in methodInfo.GetParameters()
                                                          select p.ParameterType.Name);
                        MelonLogger.Msg($"Method: {methodInfo.ReturnType.Name} {methodInfo.Name}({value2})");
                    }
                    MelonLogger.Msg("=== END ===");
                }
            }
        }

        // Token: 0x04000002 RID: 2
        private static HeartopiaComplete Instance;

        // Token: 0x04000003 RID: 3
        private static HarmonyLib.Harmony harmonyInstance;

        // Token: 0x04000004 RID: 4
        public static bool OverridePlayerPosition;

        // Token: 0x04000005 RID: 5
        public static Vector3 OverridePosition;

        // Token: 0x04000006 RID: 6
        public static bool OverrideCameraPosition;

        // Token: 0x04000007 RID: 7
        public static Vector3 CameraOverridePos;

        // Token: 0x04000008 RID: 8
        public static Quaternion CameraOverrideRot;

        // Token: 0x04000009 RID: 9
        private int cameraOverrideFramesRemaining = 0;

        // Player Rotation Override
        public static bool OverridePlayerRotation = false;
        public static Quaternion PlayerOverrideRot = Quaternion.identity;
        private int playerRotationFramesRemaining = 0;

        // IMGUI Theme
        private bool themeInitialized = false;
        private GUIStyle themeWindowStyle;
        private GUIStyle themePanelStyle;
        private GUIStyle themeContentStyle;
        private GUIStyle themeSidebarButtonStyle;
        private GUIStyle themeSidebarButtonActiveStyle;
        private GUIStyle themePrimaryButtonStyle;
        private GUIStyle themeDangerButtonStyle;
        private GUIStyle themeTopTabStyle;
        private GUIStyle themeTopTabActiveStyle;
        private List<Texture2D> themeTextures = new List<Texture2D>();
        private Texture2D uiCircleTexture;
        private Texture2D uiHueTexture;
        private Texture2D uiSvTexture;
        private float uiPickerHueCached = -1f;

        // Token: 0x0400000A RID: 10
        private bool showMenu = true;
        private bool notificationsEnabled = true;
        private bool hideIdEnabled = true;
        private List<HeartopiaComplete.MenuNotification> menuNotifications = new List<HeartopiaComplete.MenuNotification>();
        private bool eventSystemBlockedByMenu = false;
        private bool eventSystemPrevEnabled = true;
        private EventSystem blockedEventSystem = null;

        private List<GameObject> meteorList = new List<GameObject>();
        private bool showMeteorESP = false;

        // Token: 0x0400000B RID: 11
        // ========== GUI SIZE AND POSITION ==========
        // Format: new Rect(X, Y, Width, Height)
        // 
        // X: Position from left edge (50 = left side)
        // Y: Position from top edge (50 = top)
        // Width: How WIDE the menu is (300 = default, 400 = wider, 250 = narrower)
        // Height: How TALL the menu is (510 = default, 600 = taller, 400 = shorter)
        // 
        // EXAMPLES:
        // Bigger menu: new Rect(50f, 50f, 400f, 650f)
        // Smaller menu: new Rect(50f, 50f, 250f, 450f)
        // 
        private Rect windowRect = new Rect(120f, 50f, 1060f, 680f);
        private float targetWindowHeight = 680f;
        private float targetWindowWidth = 1060f;

        // Token: 0x0400000C RID: 12
        private int selectedTab = 0;

        // Token: 0x0400000D RID: 13
        private bool wasMouseOverMenuLastFrame = false;

        // Token: 0x0400000E RID: 14
        private int teleportFramesRemaining = 0;

        // Token: 0x0400000F RID: 15
        private Vector3 lastKnownPosition;

        // Token: 0x04000010 RID: 16
        private bool monitorPosition = false;

        // Token: 0x04000011 RID: 17
        private List<HeartopiaComplete.FarmLocation> farmLocations = new List<HeartopiaComplete.FarmLocation>
        {
            new HeartopiaComplete.FarmLocation("Black Truffle Spawn", new Vector3(272.1f, 12.7f, 98.2f), "mushroom"),
            new HeartopiaComplete.FarmLocation("Oyster Spawn", new Vector3(-139.8f, 21.3f, 205.2f), "mushroom"),
            new HeartopiaComplete.FarmLocation("Penny Bun Spawn", new Vector3(176.9f, 25.9f, 59.8f), "mushroom"),
            new HeartopiaComplete.FarmLocation("ShiiTake Spawn", new Vector3(57f, 18.3f, -131.5f), "mushroom"),
            new HeartopiaComplete.FarmLocation("Button Spawn", new Vector3(-156.3f, 18.8f, -115.2f), "mushroom"),
            new HeartopiaComplete.FarmLocation("Big Blueberry Field", new Vector3(-114.2f, 20.1f, 142f), "berry")
        };

        // House Slot Teleports
        private Vector3[] houseLocations = new Vector3[]
        {
            new Vector3(-96.76f, 19.40f, -69.73f),
            new Vector3(-117.00f, 20.04f, -36.88f),
            new Vector3(-114.41f, 23.10f, 2.27f),
            new Vector3(-125.37f, 22.55f, 57.64f),
            new Vector3(-89.48f, 20.37f, 113.60f),
            new Vector3(-51.02f, 20.50f, 111.90f),
            new Vector3(-1.32f, 23.96f, 91.48f),
            new Vector3(52.89f, 21.45f, 93.46f),
            new Vector3(88.98f, 22.17f, 58.11f),
            new Vector3(90.69f, 21.95f, 18.20f),
            new Vector3(86.92f, 22.03f, -38.49f)
        };

        private Vector3[] animalCareLocations = new Vector3[]
        {
            new Vector3(187.12148f, 25.393492f, 30.365686f),
            new Vector3(165.35306f, 24.739582f, -91.25646f),
            new Vector3(-0.40101618f, 13.28f, -149.19249f),
            new Vector3(-128.86902f, 18.01554f, -134.11234f),
            new Vector3(-186.06908f, 19.758656f, -40.702568f),
            new Vector3(-141.521f, 26.897f, 98.54287f),
            new Vector3(-124.4017f, 22.439999f, 209.72852f),
            new Vector3(-92.957405f, 23.451353f, -14.424409f)
        };

        // Token: 0x04000012 RID: 18
        private Dictionary<string, Vector3> fastTravelLocations = new Dictionary<string, Vector3>
        {
            {
                "Black Truffle Spawn",
                new Vector3(272.1f, 12.7f, 98.2f)
            },
            {
                "Oyster Spawn",
                new Vector3(-139.8f, 21.3f, 205.2f)
            },
            {
                "Penny Bun Spawn",
                new Vector3(176.9f, 25.9f, 59.8f)
            },
            {
                "ShiiTake Spawn",
                new Vector3(57f, 18.3f, -131.5f)
            },
            {
                "Button Spawn",
                new Vector3(-156.3f, 18.8f, -115.2f)
            },
            {
                "Big Blueberry Field",
                new Vector3(-114.2f, 20.1f, 142f)
            }
        };

        // Token: 0x04000013 RID: 19
        private Dictionary<string, Vector3> npcLocations = new Dictionary<string, Vector3>
        {
            {
                "Dorothee (Clothing)",
                new Vector3(-15.9f, 33.7f, 19.1f)
            },
            {
                "Bob (Furniture)",
                new Vector3(-6.5f, 33.5f, 17.9f)
            },
            {
                "Massimo (Town) (Cooking)",
                new Vector3(-12f, 31.6f, 43.2f)
            },
            {
                "Massimo [EVENT] (Cooking)",
                new Vector3(-55.6f, 10.7f, 254.2f)
            },
            {
                "Vanya (Fishing)",
                new Vector3(26.5f, 31.8f, 36.5f)
            },
            {
                "Naniwa (Insect Catching)",
                new Vector3(50f, 28.7f, 10.5f)
            },
            {
                "Blanc (Gardening)",
                new Vector3(33.8f, 30.7f, -18f)
            },
            {
                "Blanc [EVENT] (Gardening)",
                new Vector3(-43.2f, 10.8f, 253.2f)
            },
            {
                "Baily J (Bird Watching)",
                new Vector3(11f, 37.1f, 6.5f)
            },
            {
                "Mrs.Joan (Pet Caring)",
                new Vector3(2.7f, 33.7f, 8.9f)
            },
            {
                "Ka Ching (General Store)",
                new Vector3(-62.3f, 28f, 60.5f)
            },
            {
                "Doris (Rain/Snowfall)",
                new Vector3(-60f, 30.5f, 3.4f)
            },
            {
                "Doris (Meteor Shower)",
                new Vector3(6f, 22.9f, 199.4f)
            },
            {
                "Azure [Event] (Fashion Wave)",
                new Vector3(-33.6f, 12.6f, 239.1f)
            }
        };

        // Token: 0x04000014 RID: 20
        private Dictionary<string, Vector3> eventLocations = new Dictionary<string, Vector3>
        {
            {
                "Bug Catching",
                new Vector3(7.2f, 22.8f, 183.6f)
            },
            {
                "Fishing",
                new Vector3(-46.4f, 10.7f, -134.1f)
            },
            {
                "Bird Watching",
                new Vector3(-19.4f, 12.3f, -126.9f)
            },
            {
                "Yello Duck Jump Puzzle Challenge",
                new Vector3(-184f, 10.7f, -159.7f)
            },
            {
                "Bubble Machine Challenge",
                new Vector3(-155.8f, 10.8f, -162.4f)
            }
        };

        // Token: 0x04000015 RID: 21
        private Vector2 fastTravelScrollPosition;
        private Vector2 tabScrollPos = Vector2.zero;
        private int settingsSubTab = 0;

        // UI Theme Settings
        private float uiAccentR = 0.48f;
        private float uiAccentG = 0.42f;
        private float uiAccentB = 1f;
        private float uiTextR = 0.9f;
        private float uiTextG = 0.92f;
        private float uiTextB = 0.96f;
        private float uiMainTabTextR = 0.86f;
        private float uiMainTabTextG = 0.88f;
        private float uiMainTabTextB = 0.93f;
        private float uiSubTabTextR = 0.46f;
        private float uiSubTabTextG = 0.5f;
        private float uiSubTabTextB = 0.58f;
        private float uiWindowR = 0.016f;
        private float uiWindowG = 0.018f;
        private float uiWindowB = 0.028f;
        private float uiPanelR = 0.026f;
        private float uiPanelG = 0.03f;
        private float uiPanelB = 0.045f;
        private float uiContentR = 0.03f;
        private float uiContentG = 0.035f;
        private float uiContentB = 0.05f;
        private float uiWindowAlpha = 0.97f;
        private float uiPanelAlpha = 0.95f;
        private float uiContentAlpha = 0.96f;
        private int uiThemeColorTarget = 0;
        private bool uiThemePickerOpen = false;
        private string uiThemeHexInput = "#7B6BFF";

        // Token: 0x04000016 RID: 22
        private int teleportSubTab = 0;

        // Token: 0x04000017 RID: 23
        private Vector3 homePosition = Vector3.zero;

        // Token: 0x04000018 RID: 24
        private bool homePositionSet = false;

        // Token: 0x04000019 RID: 25
        private bool autoFarmEnabled;

        // Token: 0x0400001A RID: 26
        private bool autoCookEnabled;

        // Token: 0x0400001B RID: 27
        private bool bypassEnabled;

        // Token: 0x0400001C RID: 28
        private bool birdVacuumEnabled;

        // Token: 0x0400001D RID: 29
        private float gameSpeed = 1f;

        // NEW FEATURES: Jump Height and Camera FOV
        public float jumpHeightMultiplier = 1f;
        private Vector3 lastPlayerVelocity = Vector3.zero;
        private Vector3 jumpBoostStartPos = Vector3.zero;
        private Vector3 jumpBoostTargetPos = Vector3.zero;
        private float jumpBoostProgress = 0f;
        private bool isCurrentlyJumpBoosting = false;
        private float lastJumpBoostPressTime = 0f;
        private bool wasPlayerGrounded = true;
        private float cameraFOV = 60f;
        private float originalFOV = -1f;
        private Camera mainCamera = null;

        // Advanced Cooking Bot Variables
        private bool cookingCleanupMode = false;
        private const float cookingPlayerAlertRadius = 25f;
        private float cookingAutoSpeed = 7f;
        private bool cookingSpeedActive = false;
        private float lastCookingTimerSeenAt = -999f;
        private float cookingTakeoutSafetyDelay = 0.55f;
        private float lastCookRefreshClickAt = -999f;
        private float lastCookConfirmClickAt = -999f;
        private readonly List<Image> cookImageScanBuffer = new List<Image>(256);
        private float nextCookingCleanupScanAt = 0f;
        private bool lastCookingCleanupResult = false;
        private int autoFarmSubTab = 0; // 0 = Main, 1 = Tree Farm
        private int automationSubTab = 0; // 0 = Main, 1 = Bag, 2 = Tools
        private int pendingToolEquipType = 0; // 0 = none, 1 = axe, 2 = net, 3 = fishing rod
        private float pendingToolEquipUntil = 0f;
        private float nextToolEquipAttemptAt = 0f;
        private GameObject cachedPlayerObject = null;
        private bool enablePlayerDetection = false;
        private bool treeFarmEnabled = false;
        private List<TreeFarmPatrolPoint> treeFarmPoints = new List<TreeFarmPatrolPoint>();
        private int treeFarmCurrentIndex = 0;
        private TreeFarmState treeFarmState = TreeFarmState.Idle;
        private float treeFarmNextActionAt = 0f;
        private int treeFarmChopSent = 0;
        private int treeFarmNoPromptAttempts = 0;
        private int treeFarmChopPressCount = 3;
        private float treeFarmChopPressGap = 0.5f;
        private float treeFarmArrivalDelay = 3f;
        private float treeFarmNextLocationWait = 1.5f;
        private string treeFarmStatus = "Idle";
        private bool autoJoinFriendEnabled = false;
        private bool autoClickStartEnabled = false;
        private bool lobbyJoinInProgress = false;
        private bool lobbyJoinIsMyTown = false;
        private LobbyJoinState lobbyJoinState = LobbyJoinState.Idle;
        private float lobbyJoinNextActionAt = 0f;
        private int lobbyJoinRefreshAttempts = 0;
        private float lobbyNextAutoJoinAttemptAt = 0f;
        private float lobbyNextAutoStartClickAt = 0f;
        private string lobbyAutoJoinStatus = "Idle";
        // Token: 0x0400001E RID: 30
        private float nextFarmTime;

        // Token: 0x0400001F RID: 31
        private float nextCookTime;

        // Token: 0x04000020 RID: 32
        private readonly float farmPeriod = 1f;

        // Token: 0x04000021 RID: 33
        private readonly float cookPeriod = 0.5f;

        // Token: 0x04000022 RID: 34
        private GameObject cacheStatusAnim;

        // Token: 0x04000023 RID: 35
        private GameObject cacheCookUI;

        // Token: 0x04000024 RID: 36
        private GameObject cacheSkeletonBody;

        // Token: 0x04000025 RID: 37
        private bool collectMushrooms = true;

        // Token: 0x04000026 RID: 38
        private bool collectBerries = true;

        // Token: 0x04000027 RID: 39
        private bool collectOther = false;

        // Token: 0x04000028 RID: 40
        private GameObject radarContainer;

        // Token: 0x04000029 RID: 41
        private bool isRadarActive = false;

        // Token: 0x0400002A RID: 42
        private bool showMushroomRadar = false;

        // Token: 0x0400002B RID: 43
        private bool showBlueberryRadar = false;

        // Token: 0x0400002C RID: 44
        private bool showRaspberryRadar = false;

        // Token: 0x0400002D RID: 45
        private bool showBubbleRadar = false;

        // Token: 0x0400002E RID: 46
        private bool showInsectRadar = false;

        // Token: 0x0400002F RID: 47
        private bool showFishShadowRadar = false;

        // Token: 0x04000030 RID: 48
        private float lastScanTime = 0f;

        // Token: 0x04000031 RID: 49
        private const float scanInterval = 2f;

        // Token: 0x04000031 RID: 49
        private const float blueberryRadarRange = 80f;

        // Token: 0x04000032 RID: 50
        private const float raspberryRadarRange = 80f;

        // Token: 0x04000033 RID: 51
        private Dictionary<GameObject, GameObject> markerToTarget = new Dictionary<GameObject, GameObject>();

        // Token: 0x04000034 RID: 52
        private Dictionary<int, GameObject> trackedObjectMarkers = new Dictionary<int, GameObject>();

        // Bulk Selector Variables
        public static List<string> discoveredItems = new List<string>();
        public static Dictionary<string, List<Transform>> slotCache = new Dictionary<string, List<Transform>>();
        private string selectedBulkItemID = "";
        private Vector2 bulkSelectorScrollPos = Vector2.zero;

        // Token: 0x04000035 RID: 53
        private Dictionary<int, float> blueberryCooldowns = new Dictionary<int, float>();

        // Token: 0x04000036 RID: 54
        private Dictionary<int, float> blueberryHideUntil = new Dictionary<int, float>();

        // Token: 0x04000037 RID: 55
        private Dictionary<int, float> blueberryJustCollected = new Dictionary<int, float>();

        // Token: 0x04000038 RID: 56
        private float blueberryCooldownDuration = 125f;

        // Token: 0x04000039 RID: 57
        private const float blueberryHideDelay = 10f;

        // Token: 0x0400003A RID: 58
        private const float blueberryCollectDelay = 4f;

        // Token: 0x0400003B RID: 59
        private Button lastBlueberryButton = null;

        // Token: 0x0400003C RID: 60
        private Dictionary<int, float> raspberryCooldowns = new Dictionary<int, float>();

        // Token: 0x0400003D RID: 61
        private Dictionary<int, float> raspberryHideUntil = new Dictionary<int, float>();

        // Token: 0x0400003E RID: 62
        private Dictionary<int, float> raspberryJustCollected = new Dictionary<int, float>();

        // Token: 0x0400003F RID: 63
        private float raspberryCooldownDuration = 125f;

        // Token: 0x04000040 RID: 64
        private const float raspberryHideDelay = 10f;

        // Token: 0x04000041 RID: 65
        private const float raspberryCollectDelay = 4f;

        // Token: 0x04000042 RID: 66
        private Button lastRaspberryButton = null;

        // Token: 0x04000043 RID: 67
        private bool autoFarmActive = false;

        // Token: 0x04000044 RID: 68
        private string autoFarmStatus = "Idle";

        // Token: 0x04000045 RID: 69
        private float autoFarmTimer = 0f;

        // Token: 0x04000046 RID: 70
        private int currentLocationIndex = 0;

        // Token: 0x04000047 RID: 71
        private HeartopiaComplete.AutoFarmState farmState = HeartopiaComplete.AutoFarmState.Idle;

        // Token: 0x04000048 RID: 72
        private Vector3 lastNodePosition = Vector3.zero;

        // Token: 0x04000049 RID: 73
        private Dictionary<Vector3, float> recentlyVisitedNodes = new Dictionary<Vector3, float>();

        // Token: 0x0400004A RID: 74
        private const float nodeVisitCooldown = 15f;

        // Token: 0x0400004B RID: 75
        private bool autoCollectClickedSinceArrival = false;

        // Token: 0x0400004C RID: 76
        private int cameraRotationAttempts = 0;

        // Token: 0x0400004D RID: 77
        private const int maxCameraRotationAttempts = 3;

        // Token: 0x0400004E RID: 78
        private float cameraStuckDisplayTimer = 0f;

        // Token: 0x0400004F RID: 79
        private float areaLoadDelay = 4f;

        // --- AUTO FARM PRIORITIES ---
        private bool priorityOysterMushroom = false;
        private bool priorityButtonMushroom = false;
        private bool priorityPennyBun = false;
        private bool priorityShiitake = false;
        private bool priorityTruffle = false;
        private bool priorityBlueberry = false;
        private bool priorityRaspberry = false;
        private bool priorityBubble = false;
        private bool priorityInsect = false;

        // Priority farming state
        private List<Vector3> activePriorityLocations = new List<Vector3>();
        private Dictionary<Vector3, float> priorityLocationCooldowns = new Dictionary<Vector3, float>();
        private float priorityRecheckTimer = 0f;
        private Vector3? currentPriorityLocation = null;
        private Vector3? lastFoundPriorityNodeLocation = null;
        private bool lastTeleportWasPriorityLocation = false;
        private int consecutivePriorityNoCollectCycles = 0;
        private bool priorityCollectClickedSinceArrival = false;

        // --- STATIC LOOT LOCATIONS FOR PRIORITIES ---
        private Dictionary<string, Vector3> priorityLocations = new Dictionary<string, Vector3>()
        {
            { "Oyster Mushroom", new Vector3(36.603138f, 26.140745f, 212.39085f) },
            { "Button Mushroom", new Vector3(-219.81989f, 12.863783f, 6.995692f) },
            { "Penny Bun", new Vector3(175.89377f, 25.673292f, 55.985367f) },
            { "Shiitake", new Vector3(-66.63026f, 14.248707f, -169.89787f) },
            { "Black Truffle", new Vector3(258.11917f, 13.1247f, 95.18241f) },
            { "Blueberry", new Vector3(-135.8885f, 23.15401f, 86.081604f) },
            { "Raspberry", new Vector3(-124.78083f, 19.10137f, -113.568375f) }
        };

        // Token: 0x04000050 RID: 80
        private Vector3[] blueberryPositions = new Vector3[]
        {
            new Vector3(-5.86f, 23.17f, 99.33f),
            new Vector3(-14.15f, 22.26f, 105.27f),
            new Vector3(-28.76f, 20.56f, 113.32f),
            new Vector3(-40.26f, 20.28f, 115.89f),
            new Vector3(-58.97f, 20.68f, 117.67f),
            new Vector3(-65.91f, 20.14f, 123.47f),
            new Vector3(-78.43f, 20.29f, 121.14f),
            new Vector3(-93.45f, 20.52f, 118.26f),
            new Vector3(21.94f, 22.14f, 98.96f),
            new Vector3(37.23f, 20.74f, 100.19f),
            new Vector3(47.98f, 21.6f, 94.41f),
            new Vector3(64.27f, 21f, 94.7f),
            new Vector3(76.87f, 20.5f, 97.59f),
            new Vector3(80.03f, 20.47f, 108.47f),
            new Vector3(74.95f, 20.64f, 137.79f),
            new Vector3(45.61f, 20.67f, 141.06f),
            new Vector3(24.01f, 21.66f, 146.53f),
            new Vector3(9.77f, 21.48f, 147.94f),
            new Vector3(-23.05f, 20.09f, 158.1f),
            new Vector3(-44.83f, 19.96f, 164.72f),
            new Vector3(97.88f, 22.73f, 52.9f),
            new Vector3(100.22f, 22.88f, 43.81f),
            new Vector3(97.27f, 23.1f, 67.74f),
            new Vector3(121.26f, 23.08f, 84.34f),
            new Vector3(137.91f, 23.15f, 82.24f),
            new Vector3(142.78f, 22.95f, 63.66f),
            new Vector3(123.6f, 22.89f, 43.15f),
            new Vector3(96.94f, 22.82f, 28.91f),
            new Vector3(97.84f, 22.82f, 18.08f),
            new Vector3(97.85f, 22.83f, 7.06f),
            new Vector3(103.39f, 22.97f, 1.14f),
            new Vector3(110.07f, 21.81f, -11.87f),
            new Vector3(124.84f, 22.04f, -4.28f),
            new Vector3(93.88f, 21.75f, -22.81f),
            new Vector3(89.69f, 21.88f, -30.47f),
            new Vector3(93.98f, 21.57f, -41.11f),
            new Vector3(89.38f, 20.78f, -58.92f),
            new Vector3(82.51f, 20.87f, -63.06f),
            new Vector3(75.68f, 20.65f, -76.41f),
            new Vector3(76.12f, 20.53f, -86.39f),
            new Vector3(-102.05f, 19.36f, -70.15f),
            new Vector3(-96.11f, 19.69f, -77.12f),
            new Vector3(-91.35f, 19.47f, -92.45f),
            new Vector3(-111.74f, 19.18f, -60.99f),
            new Vector3(-117.23f, 19.84f, -54.52f),
            new Vector3(-118.64f, 19.95f, -42.71f),
            new Vector3(-120.71f, 20.37f, -28.66f),
            new Vector3(-123.18f, 19.91f, -26.54f),
            new Vector3(-127.92f, 21.14f, -3.41f),
            new Vector3(-129.59f, 20.66f, 27.85f),
            new Vector3(-132.93f, 21.48f, 48.36f),
            new Vector3(-131.08f, 21.88f, 66.78f),
            new Vector3(-132.79f, 21.7f, 75.47f),
            new Vector3(-135.89f, 23.12f, 86.09f),
            new Vector3(-16.93f, 20.17f, 140.75f),
            new Vector3(-68.31f, 20.36f, 143.23f),
            new Vector3(-82.89f, 20.23f, 159.33f),
            new Vector3(-97.04f, 20.14f, 165.01f),
            new Vector3(-116.84f, 20.05f, 148.01f),
            new Vector3(-112.68f, 20.21f, 124.94f),
            new Vector3(-105.49f, 20.18f, 119.81f),
            new Vector3(31.73f, 21.5f, 124.29f),
            new Vector3(141.92f, 23.06f, 24.95f),
            new Vector3(141.98f, 22.85f, 8.89f),
            new Vector3(149.34f, 20.9f, -25.04f),
            new Vector3(126.56f, 21.47f, -57.81f),
            new Vector3(109.28f, 21.17f, -61.6f),
            new Vector3(112.94f, 20.65f, -109.74f),
            new Vector3(-125.76f, 21.13f, 5.11f),
            new Vector3(-123.9f, 21.98f, 19.82f),
            new Vector3(5.07f, 23.09f, 97.91f),
            new Vector3(127.5f, 21.23f, -84.44f)
        };

        // Token: 0x04000051 RID: 81
        private Vector3[] raspberryPositions = new Vector3[]
        {
            new Vector3(-168.1f, 20.1f, -44.82f),
            new Vector3(-159.86f, 19.99f, -60.52f),
            new Vector3(-135.03f, 18.95f, -70.75f),
            new Vector3(-198.25f, 21.78f, -71.53f),
            new Vector3(-124.88f, 19.07f, -113.42f),
            new Vector3(-106.8f, 19.11f, -104.88f),
            new Vector3(-153.52f, 20.7f, -15.07f),
            new Vector3(-173.6f, 20.71f, -7.49f),
            new Vector3(-189.19f, 20.02f, 8.1f),
            new Vector3(-176.01f, 20.65f, 12.34f),
            new Vector3(-201.96f, 19.37f, -18.05f),
            new Vector3(-159.2f, 20.92f, 30.83f),
            new Vector3(-178.5f, 21.6f, 43.58f),
            new Vector3(-193.46f, 23.25f, 38.61f),
            new Vector3(-177.76f, 21.35f, 68.47f),
            new Vector3(-163.76f, 21.49f, 79.07f),
            new Vector3(-160.83f, 19.05f, -103.41f)
        };

        // --- Custom Teleport Logic ---
        [Serializable]
        public class CustomTeleportEntry
        {
            public string name;
            public Vector3 position;
        }

        // Removed Wrapper, using manual JSON handling
        private List<CustomTeleportEntry> customTeleportList = new List<CustomTeleportEntry>();
        private string customTeleportName = "My Place";
        private string customTPX = "0";
        private string customTPY = "0";
        private string customTPZ = "0";

        private string GetCustomTeleportPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "custom_teleports.json");
        }

        private void SaveCustomTeleports()
        {
            try
            {
                // Manual JSON Writer
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"entries\": [");
                for (int i = 0; i < this.customTeleportList.Count; i++)
                {
                    var entry = this.customTeleportList[i];
                    // Sanitize name
                    string cleanName = entry.name.Replace("\"", "").Replace("\\", "");
                    string x = entry.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string y = entry.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string z = entry.position.z.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    
                    sb.Append($"    {{ \"name\": \"{cleanName}\", \"x\": {x}, \"y\": {y}, \"z\": {z} }}");
                    if (i < this.customTeleportList.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");
                
                File.WriteAllText(this.GetCustomTeleportPath(), sb.ToString());
                MelonLogger.Msg("Custom Teleports Saved!");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error Saving Teleports: " + ex.Message);
            }
        }

        private void LoadCustomTeleports()
        {
            try
            {
                string path = this.GetCustomTeleportPath();
                if (File.Exists(path))
                {
                    this.customTeleportList.Clear();
                    string[] lines = File.ReadAllLines(path);
                    foreach (string line in lines)
                    {
                        if (line.Contains("\"name\":"))
                        {
                            try 
                            {
                                // Simple efficient parsing for flat structure
                                string name = GetJsonString(line, "\"name\":");
                                float x = GetJsonFloat(line, "\"x\":");
                                float y = GetJsonFloat(line, "\"y\":");
                                float z = GetJsonFloat(line, "\"z\":");
                                
                                this.customTeleportList.Add(new CustomTeleportEntry { name = name, position = new Vector3(x, y, z) });
                            } 
                            catch {}
                        }
                    }
                    MelonLogger.Msg($"Loaded {this.customTeleportList.Count} custom teleports.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error Loading Teleports: " + ex.Message);
            }
        }

        private string GetJsonString(string line, string key)
        {
            int startIdx = line.IndexOf(key);
            if (startIdx == -1) return "Unknown";
            startIdx += key.Length;
            
            // Find opening quote
            int quoteStart = line.IndexOf("\"", startIdx);
            if (quoteStart == -1) return "Unknown";
            
            // Find closing quote
            int quoteEnd = line.IndexOf("\"", quoteStart + 1);
            if (quoteEnd == -1) return "Unknown";
            
            return line.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        private float GetJsonFloat(string line, string key)
        {
            int startIdx = line.IndexOf(key);
            if (startIdx == -1) return 0f;
            startIdx += key.Length;
            
            // Find value start (skip spaces)
            while (startIdx < line.Length && (line[startIdx] == ' ' || line[startIdx] == ':')) startIdx++;
            
            // Find value end (comma or brace)
            int endIdx = startIdx;
            while (endIdx < line.Length && line[endIdx] != ',' && line[endIdx] != '}') endIdx++;
            
            string valStr = line.Substring(startIdx, endIdx - startIdx).Trim();
            float result;
            if (float.TryParse(valStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result))
            {
                return result;
            }
            return 0f;
        }

        public override void OnDeinitializeMelon()
        {
            if (this.eventSystemBlockedByMenu)
            {
                EventSystem restoreTarget = this.blockedEventSystem != null ? this.blockedEventSystem : EventSystem.current;
                if (restoreTarget != null)
                {
                    restoreTarget.enabled = this.eventSystemPrevEnabled;
                }
                this.eventSystemBlockedByMenu = false;
                this.blockedEventSystem = null;
            }

            if (patrolCoroutine != null)
            {
                MelonCoroutines.Stop(patrolCoroutine);
                patrolCoroutine = null;
            }
            isPatrolActive = false;

            foreach (Texture2D texture in this.themeTextures)
            {
                if (texture != null)
                {
                    Object.Destroy(texture);
                }
            }
            this.themeTextures.Clear();
            this.themeInitialized = false;
        }

        // Token: 0x02000008 RID: 8
        private class FarmLocation
        {
            // Token: 0x0600002E RID: 46 RVA: 0x00008437 File Offset: 0x00006637
            public FarmLocation(string name, Vector3 position, string type)
            {
                this.Name = name;
                this.Position = position;
                this.Type = type;
            }

            // Token: 0x04000052 RID: 82
            public string Name;

            // Token: 0x04000053 RID: 83
            public Vector3 Position;

            // Token: 0x04000054 RID: 84
            public string Type;
        }

        // Token: 0x02000009 RID: 9
        private enum AutoFarmState
        {
            // Token: 0x04000056 RID: 86
            Idle,
            // Token: 0x04000057 RID: 87
            ScanningForNodes,
            // Token: 0x04000058 RID: 88
            TeleportingToNode,
            // Token: 0x04000059 RID: 89
            Collecting,
            // Token: 0x0400005A RID: 90
            MovingToLocation,
            // Token: 0x0400005B RID: 91
            LoadingArea,
            // Token: 0x0400005C RID: 92
            WaitingForNodes,
            // Token: 0x0400005D RID: 93
            WaitingForPriorityArea
        }

        private enum TreeFarmState
        {
            Idle,
            EquipAxe,
            WaitAfterEquip,
            TeleportToPoint,
            WaitAfterTeleport,
            ChopAtPoint,
            WaitNextPoint
        }

        private bool CheckForDurabilityNotification()
        {
            try
            {
                GameObject toastText = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Tip/TipPanel(Clone)/ToastPanel(Clone)/toasts@t/ToastTextWidget(Clone)/AniRoot@ani/root_visible@go/root_visible/value@txt");
                if (toastText != null && toastText.activeInHierarchy)
                {
                    var text = toastText.GetComponent<Text>();
                    if (text != null && text.text.Contains("Tool durability depleted"))
                    {
                        return true;
                    }

                }
            }
            catch
            {
            }
            return false;
        }

        private void UpdateIdDisplay()
        {
            try
            {
                GameObject testIndex = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/TEST_INDEX");
                if (testIndex != null && testIndex.activeInHierarchy)
                {
                    var text = testIndex.GetComponent<Text>();
                    if (text != null)
                    {
                        string originalText = text.text;
                        if (this.hideIdEnabled && originalText.Contains("ID:"))
                        {
                            string[] parts = originalText.Split(new string[] { "     " }, StringSplitOptions.None);
                            string newText = "";
                            foreach (string part in parts)
                            {
                                if (!part.StartsWith("ID:"))
                                {
                                    if (newText != "") newText += "     ";
                                    newText += part;
                                }
                            }
                            if (newText != originalText)
                            {
                                text.text = newText;
                            }
                        }
                        else if (!this.hideIdEnabled && !originalText.Contains("ID:"))
                        {

                        }
                    }
                }
            }
            catch
            {
            }
        }

        private enum LobbyJoinState
        {
            Idle,
            OpenRoomPanel,
            SelectFriendTab,
            RefreshAndRetry,
            ClickFriendJoin,
            SelectMyTownTab,
            ClickMyTownJoin
        }
    }
}