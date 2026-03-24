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
using System.Xml.Serialization;
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

    // Toast hook moved to ToastHook.cs

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
        private KeyCode keyAutoForaging = KeyCode.None;
        private KeyCode keyAutoFish = KeyCode.None;
        private KeyCode keyAutoFishingTeleport = KeyCode.None;
        private KeyCode keyAutoCook = KeyCode.None;
        private KeyCode keyBypassUI = KeyCode.None;
        private KeyCode keyDisableAll = KeyCode.None;
        private KeyCode keyInspectPlayer = KeyCode.None;
        private KeyCode keyInspectMove = KeyCode.None;
        private KeyCode keyAutoRepair = KeyCode.None;
        private KeyCode keyAutoJoinFriend = KeyCode.None;
        private KeyCode keyJoinPublic = KeyCode.None;
        private KeyCode keyJoinMyTown = KeyCode.None;
        private KeyCode keyNoclip = KeyCode.None;
        private KeyCode keyAutoEat = KeyCode.None;
        private KeyCode keyAntiAfk = KeyCode.None;
        private KeyCode keyBypassOverlap = KeyCode.None;
        private KeyCode keyBirdVacuum = KeyCode.None;
        private KeyCode keyGameSpeed1x = KeyCode.None;
        private KeyCode keyGameSpeed2x = KeyCode.None;
        private KeyCode keyGameSpeed5x = KeyCode.None;
        private KeyCode keyGameSpeed10x = KeyCode.None;
        private KeyCode keyEquipAxe = KeyCode.None;
        private KeyCode keyEquipNet = KeyCode.None;
        private KeyCode keyEquipRod = KeyCode.None;
        
        // Key Rebinding State
        private string keyBindingActive = "";
        private float keyBindAssignedAt = -999f;
        
        // Notification check for auto repair
        private float lastNotificationCheck = 0f;
        private const float NOTIFICATION_CHECK_INTERVAL = 2f; // Check every 2 seconds
        
        // --- WINDOWS API FOR ESC KEY ---
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
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
        // AutoFish integration instances
        private AutoFishLogic autoFishLogic;
        private AutoFishFarm autoFishFarm;
        // Auto-stop for Auto Fishing Farm (auto-teleport)
        private bool autoFishFarmAutoStopEnabled = false;
        private int autoFishFarmAutoStopHours = 0;
        private int autoFishFarmAutoStopMinutes = 0;
        private int autoFishFarmAutoStopSeconds = 0;
        private string autoFishFarmAutoStopHoursInput = "0";
        private string autoFishFarmAutoStopMinutesInput = "0";
        private string autoFishFarmAutoStopSecondsInput = "0";
        private float autoFishFarmAutoStopAt = -1f;
        // Toggle for teleport fishing
        private bool autoFishTeleportEnabled = false;

        public class CookingPatrolData
        {
            public List<CookingPatrolPoint> Points = new List<CookingPatrolPoint>();
        }

        public class TreeFarmPatrolData
        {
            public List<TreeFarmPatrolPoint> Points = new List<TreeFarmPatrolPoint>();
        }

        [Serializable]
        public class KeybindConfigData
        {
            public int keyToggleMenu;
            public int keyToggleRadar;
            public int keyAutoForaging;
            public int keyAutoFish;
            public int keyAutoFishingTeleport;
            public int keyAutoCook;
            public int keyBypassUI;
            public int keyDisableAll;
            public int keyInspectPlayer;
            public int keyInspectMove;
            public int keyAutoRepair;
            public int keyAutoJoinFriend;
            public int keyJoinPublic;
            public int keyJoinMyTown;
            public int keyNoclip;
            public int keyAutoEat;
            public int keyAntiAfk;
            public int keyBypassOverlap;
            public int keyBirdVacuum;
            public int keyAutoSnow;
            public int keyGameSpeed1x;
            public int keyGameSpeed2x;
            public int keyGameSpeed5x;
            public int keyGameSpeed10x;
            public int keyEquipAxe;
            public int keyEquipNet;
            public int keyEquipRod;
            public float noclipSpeed;
            public float noclipBoostMultiplier;
            public float areaLoadDelay;
            public float resourceTeleportCooldown;
            public float resourceClickDuration;
            public float resourceAutoRepairPauseSeconds;
            public float gameSpeed;
            public bool customCameraFOVEnabled;
            public float cameraFOV;
            public float snowClickInterval;
            public float sculptIconClickInterval;
            public float cookingAutoSpeed;
            public float cookingWaitAtSpot;
            public float autoFishScanTimeout = -1f;
            public float autoFishTeleportDelay = -1f;
            public float autoFishFishShadowDetectRange = -1f;
            public float autoFishReelMaxDuration = -1f;
            public float autoFishReelHoldDuration = -1f;
            public float autoFishReelPauseDuration = -1f;
            public float insectTeleportCooldown;
            public float insectScanTimeout;
            public float insectTeleportOffset;
            public bool notificationsEnabled;
            public bool blockGameUiWhenMenuOpen;
            public bool autoClickStartEnabled;
            public int maxAutoEatAttempts;
            public bool showStatusOverlay;
            public bool hideIdEnabled;
            public bool antiAfkEnabled;
            public float antiAfkInterval;
            public int autoRepairType;
            public int autoEatFoodType;
            public bool repairTeleportBackEnabled;
            public bool collectEventResources;
        }

        [Serializable]
        public class UiThemeConfigData
        {
            public float uiAccentR;
            public float uiAccentG;
            public float uiAccentB;
            public float uiTextR;
            public float uiTextG;
            public float uiTextB;
            public float uiMainTabTextR;
            public float uiMainTabTextG;
            public float uiMainTabTextB;
            public float uiSubTabTextR;
            public float uiSubTabTextG;
            public float uiSubTabTextB;
            public float uiWindowR;
            public float uiWindowG;
            public float uiWindowB;
            public float uiPanelR;
            public float uiPanelG;
            public float uiPanelB;
            public float uiContentR;
            public float uiContentG;
            public float uiContentB;
            public float uiWindowAlpha;
            public float uiPanelAlpha;
            public float uiContentAlpha;
        }

        [Serializable]
        public class RadarConfigData
        {
            public int radarMarkerStyle;
            public bool priorityFiddlehead;
            public bool priorityTallMustard;
            public bool priorityBurdock;
            public bool priorityMustardGreens;
        }

        [Serializable]
        public class NamedCookingPatrolSave
        {
            public string Name;
            public List<CookingPatrolPoint> Points = new List<CookingPatrolPoint>();
        }

        [Serializable]
        public class UnifiedConfigData
        {
            public KeybindConfigData Keybinds = new KeybindConfigData();
            public UiThemeConfigData UiTheme = new UiThemeConfigData();
            public RadarConfigData Radar = new RadarConfigData();
            public PatrolData Patrol = new PatrolData();
            public TreeFarmPatrolData TreeFarmPatrol = new TreeFarmPatrolData();
            public List<NamedCookingPatrolSave> CookingPatrolSaves = new List<NamedCookingPatrolSave>();
            public List<CustomTeleportEntry> CustomTeleports = new List<CustomTeleportEntry>();
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
        private string cookingPatrolSaveName = "";
        private Vector2 cookingPatrolSaveScrollPos = Vector2.zero;
        private bool antiAfkEnabled = false;
        private float antiAfkInterval = 25f;
        private float lastAntiAfkPulseAt = -999f;
        private float antiAfkMouseDownClearAt = 0f;
        private float antiAfkMouseHoldClearAt = 0f;

        // --- AUTO REPAIR VARIABLES ---
        private int autoRepairType = 0; // 0 = Repair Kit, 1 = Crafty Repair Kit
        private readonly string[] autoRepairOptions = { "Repair Kit", "Crafty Repair Kit" };
        private readonly string[] autoRepairKeys = { "toolrestorer_toolrestorer_1", "toolrestorer_toolrestorer_2" };
        private bool autoRepairDropdownOpen = false;
        private bool repairTeleportBackEnabled = true;
        private const string AUTO_EAT_FOOD_KEY = "food_bluejam";
        // Default to the newly added "Bad Food" option (index 0)
        private int autoEatFoodType = 0;
        private readonly string[] autoEatFoodOptions = {  "Bad Food", "Blue Jam", "Rasp Jam", "Mix Jam", "Bake Mushroom", "Salad", "Any Food"};
        private readonly string[] autoEatFoodKeys = { "food_badfood", "food_bluejam", "food_raspjam", "food_mixjam", "food_bakemushroom", "food_salad", "food_" };
        private bool autoEatFoodDropdownOpen = false;
        private const string BAG_BUTTON_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/top_right_layout@go@t/menu_bar@go/bag@go@btn@frame";
        private const string BAG_PANEL_PATH = "GameApp/startup_root(Clone)/XDUIRoot/Scene/BagPanel(Clone)";
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
        // Multi-use auto-repair state (controls repeated uses when auto-repairing)
        private bool isAutoRepairRunning = false;
        private int autoRepairUseCount = 0;
        private int repairUsesTarget = 1; // computed from selected repair item
        private bool autoRepairWaiting = false;
        private float autoRepairWaitTimer = 0f;
        private float autoRepairWaitDuration = 20f;
        private bool lastStartWasAutoRepair = false;
        private const int MAX_SCROLL_ATTEMPTS = 30;
        private bool isAutoEating = false;
        private int autoEatStep = 0;
        private float autoEatStepTimer = 0f;
        private int autoEatScrollAttempts = 0;
        private int autoEatAttempts = 0;
        // Resource-farm: pause when auto-repair triggered (seconds)
        private float resourceAutoRepairPauseSeconds = 20f;
        private float resourceRepairPauseUntil = 0f;
        // Timestamp of the last repair trigger to debounce repeated triggers
        private float lastRepairTriggerTime = -999f;
        // Distance to teleport player backward (meters) before starting repair
        private float repairTeleportBackDistance = 2.5f;
        private int maxAutoEatAttempts = 10;

        // --- TARGET PATHS FOR PATROL ACTIONS ---
        private readonly string[] workPaths = new string[]
        {
            "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_cook_danger@list/CommonIconForCookDanger(Clone)/root_visible@go/icon@img@btn",
            "GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)/AniRoot@queueanimation/detail@t/btnBar@go/confirm@swapbtn",
            "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn",
            "GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)/AniRoot@go@ani/exit@btn@go"
        };

        // Settings/Keybinds Persistence
        private string GetConfigPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "Config.xml");
        }

        private string GetKeybindsPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "keybinds.json");
        }

        private UnifiedConfigData LoadUnifiedConfig()
        {
            try
            {
                string path = this.GetConfigPath();
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                XmlSerializer serializer = new XmlSerializer(typeof(UnifiedConfigData));
                UnifiedConfigData data;
                using (StringReader reader = new StringReader(json))
                {
                    data = serializer.Deserialize(reader) as UnifiedConfigData;
                }
                if (data == null) return null;
                if (data.Keybinds == null) data.Keybinds = new KeybindConfigData();
                if (data.UiTheme == null) data.UiTheme = new UiThemeConfigData();
                if (data.Radar == null) data.Radar = new RadarConfigData();
                if (data.Patrol == null) data.Patrol = new PatrolData();
                if (data.Patrol.Points == null) data.Patrol.Points = new List<SerializableVector3>();
                if (data.TreeFarmPatrol == null) data.TreeFarmPatrol = new TreeFarmPatrolData();
                if (data.TreeFarmPatrol.Points == null) data.TreeFarmPatrol.Points = new List<TreeFarmPatrolPoint>();
                if (data.CookingPatrolSaves == null) data.CookingPatrolSaves = new List<NamedCookingPatrolSave>();
                foreach (NamedCookingPatrolSave save in data.CookingPatrolSaves)
                {
                    if (save != null && save.Points == null) save.Points = new List<CookingPatrolPoint>();
                }
                if (data.CustomTeleports == null) data.CustomTeleports = new List<CustomTeleportEntry>();
                return data;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error loading unified config: " + ex.Message);
                return null;
            }
        }

        private UnifiedConfigData LoadOrCreateUnifiedConfig()
        {
            return this.LoadUnifiedConfig() ?? new UnifiedConfigData();
        }

        private void SaveUnifiedConfig(UnifiedConfigData data)
        {
            if (data == null) data = new UnifiedConfigData();
            XmlSerializer serializer = new XmlSerializer(typeof(UnifiedConfigData));
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, data);
                File.WriteAllText(this.GetConfigPath(), writer.ToString());
            }
        }

        private void PopulateKeybindConfig(KeybindConfigData data)
        {
            data.keyToggleMenu = (int)this.keyToggleMenu;
            data.keyToggleRadar = (int)this.keyToggleRadar;
            data.keyAutoForaging = (int)this.keyAutoForaging;
            data.keyAutoFish = (int)this.keyAutoFish;
            data.keyAutoFishingTeleport = (int)this.keyAutoFishingTeleport;
            data.keyAutoCook = (int)this.keyAutoCook;
            data.keyBypassUI = (int)this.keyBypassUI;
            data.keyDisableAll = (int)this.keyDisableAll;
            data.keyInspectPlayer = (int)this.keyInspectPlayer;
            data.keyInspectMove = (int)this.keyInspectMove;
            data.keyAutoRepair = (int)this.keyAutoRepair;
            data.keyAutoJoinFriend = (int)this.keyAutoJoinFriend;
            data.keyJoinPublic = (int)this.keyJoinPublic;
            data.keyJoinMyTown = (int)this.keyJoinMyTown;
            data.keyNoclip = (int)this.keyNoclip;
            data.keyAutoEat = (int)this.keyAutoEat;
            data.keyAntiAfk = (int)this.keyAntiAfk;
            data.keyBypassOverlap = (int)this.keyBypassOverlap;
            data.keyBirdVacuum = (int)this.keyBirdVacuum;
            data.keyAutoSnow = (int)this.autoSnowHotkey;
            data.keyGameSpeed1x = (int)this.keyGameSpeed1x;
            data.keyGameSpeed2x = (int)this.keyGameSpeed2x;
            data.keyGameSpeed5x = (int)this.keyGameSpeed5x;
            data.keyGameSpeed10x = (int)this.keyGameSpeed10x;
            data.keyEquipAxe = (int)this.keyEquipAxe;
            data.keyEquipNet = (int)this.keyEquipNet;
            data.keyEquipRod = (int)this.keyEquipRod;
            data.noclipSpeed = this.noclipSpeed;
            data.noclipBoostMultiplier = this.noclipBoostMultiplier;
            data.areaLoadDelay = this.areaLoadDelay;
            data.resourceTeleportCooldown = this.resourceTeleportCooldown;
            data.resourceClickDuration = this.resourceClickDuration;
            data.resourceAutoRepairPauseSeconds = this.resourceAutoRepairPauseSeconds;
            data.gameSpeed = this.gameSpeed;
            data.customCameraFOVEnabled = this.customCameraFOVEnabled;
            data.cameraFOV = this.cameraFOV;
            data.snowClickInterval = this.snowClickInterval;
            data.sculptIconClickInterval = this.sculptIconClickInterval;
            data.cookingAutoSpeed = this.cookingAutoSpeed;
            data.cookingWaitAtSpot = this.cookingWaitAtSpot;
            data.autoFishScanTimeout = this.autoFishFarm?.scanTimeout ?? -1f;
            data.autoFishTeleportDelay = this.autoFishFarm?.teleportDelay ?? -1f;
            data.autoFishFishShadowDetectRange = this.autoFishLogic?.fishShadowDetectRange ?? -1f;
            data.autoFishReelMaxDuration = this.autoFishLogic?.reelMaxDuration ?? -1f;
            data.autoFishReelHoldDuration = this.autoFishLogic?.reelHoldDuration ?? -1f;
            data.autoFishReelPauseDuration = this.autoFishLogic?.reelPauseDuration ?? -1f;
            data.insectTeleportCooldown = InsectFarm.GetTeleportCooldown();
            data.insectScanTimeout = InsectFarm.GetScanTimeout();
            data.insectTeleportOffset = InsectFarm.GetTeleportOffset();
            data.notificationsEnabled = this.notificationsEnabled;
            data.blockGameUiWhenMenuOpen = this.blockGameUiWhenMenuOpen;
            data.autoClickStartEnabled = this.autoClickStartEnabled;
            data.maxAutoEatAttempts = this.maxAutoEatAttempts;
            data.showStatusOverlay = this.showStatusOverlay;
            data.hideIdEnabled = this.hideIdEnabled;
            data.antiAfkEnabled = this.antiAfkEnabled;
            data.antiAfkInterval = this.antiAfkInterval;
            data.autoRepairType = this.autoRepairType;
            data.autoEatFoodType = this.autoEatFoodType;
            data.repairTeleportBackEnabled = this.repairTeleportBackEnabled;
            data.collectEventResources = this.collectEventResources;
        }

        private void ApplyKeybindConfig(KeybindConfigData data)
        {
            if (data == null) return;
            this.keyToggleMenu = (KeyCode)data.keyToggleMenu;
            this.keyToggleRadar = (KeyCode)data.keyToggleRadar;
            this.keyAutoForaging = (KeyCode)data.keyAutoForaging;
            this.keyAutoFish = (KeyCode)data.keyAutoFish;
            this.keyAutoFishingTeleport = (KeyCode)data.keyAutoFishingTeleport;
            this.keyAutoCook = (KeyCode)data.keyAutoCook;
            this.keyBypassUI = (KeyCode)data.keyBypassUI;
            this.keyDisableAll = (KeyCode)data.keyDisableAll;
            this.keyInspectPlayer = (KeyCode)data.keyInspectPlayer;
            this.keyInspectMove = (KeyCode)data.keyInspectMove;
            this.keyAutoRepair = (KeyCode)data.keyAutoRepair;
            this.keyAutoJoinFriend = (KeyCode)data.keyAutoJoinFriend;
            this.keyJoinPublic = (KeyCode)data.keyJoinPublic;
            this.keyJoinMyTown = (KeyCode)data.keyJoinMyTown;
            this.keyNoclip = (KeyCode)data.keyNoclip;
            this.keyAutoEat = (KeyCode)data.keyAutoEat;
            this.keyAntiAfk = (KeyCode)data.keyAntiAfk;
            this.keyBypassOverlap = (KeyCode)data.keyBypassOverlap;
            this.keyBirdVacuum = (KeyCode)data.keyBirdVacuum;
            this.autoSnowHotkey = (KeyCode)data.keyAutoSnow;
            this.keyGameSpeed1x = (KeyCode)data.keyGameSpeed1x;
            this.keyGameSpeed2x = (KeyCode)data.keyGameSpeed2x;
            this.keyGameSpeed5x = (KeyCode)data.keyGameSpeed5x;
            this.keyGameSpeed10x = (KeyCode)data.keyGameSpeed10x;
            this.keyEquipAxe = (KeyCode)data.keyEquipAxe;
            this.keyEquipNet = (KeyCode)data.keyEquipNet;
            this.keyEquipRod = (KeyCode)data.keyEquipRod;
            this.noclipSpeed = data.noclipSpeed;
            this.noclipBoostMultiplier = data.noclipBoostMultiplier;
            this.areaLoadDelay = data.areaLoadDelay;
            this.resourceTeleportCooldown = data.resourceTeleportCooldown;
            this.resourceClickDuration = data.resourceClickDuration;
            this.resourceAutoRepairPauseSeconds = data.resourceAutoRepairPauseSeconds;
            this.gameSpeed = data.gameSpeed;
            this.customCameraFOVEnabled = data.customCameraFOVEnabled;
            this.cameraFOV = data.cameraFOV;
            this.snowClickInterval = data.snowClickInterval;
            this.sculptIconClickInterval = data.sculptIconClickInterval;
            this.cookingAutoSpeed = data.cookingAutoSpeed;
            this.cookingWaitAtSpot = data.cookingWaitAtSpot;
            this.saved_autoFishScanTimeout = data.autoFishScanTimeout;
            this.saved_autoFishTeleportDelay = data.autoFishTeleportDelay;
            this.saved_autoFishFishShadowDetectRange = data.autoFishFishShadowDetectRange;
            this.saved_autoFishReelMaxDuration = data.autoFishReelMaxDuration;
            this.saved_autoFishReelHoldDuration = data.autoFishReelHoldDuration;
            this.saved_autoFishReelPauseDuration = data.autoFishReelPauseDuration;
            InsectFarm.SetTeleportCooldown(data.insectTeleportCooldown);
            InsectFarm.SetScanTimeout(data.insectScanTimeout);
            InsectFarm.SetTeleportOffset(data.insectTeleportOffset);
            this.notificationsEnabled = data.notificationsEnabled;
            this.blockGameUiWhenMenuOpen = data.blockGameUiWhenMenuOpen;
            this.autoClickStartEnabled = data.autoClickStartEnabled;
            this.maxAutoEatAttempts = data.maxAutoEatAttempts;
            this.showStatusOverlay = data.showStatusOverlay;
            this.hideIdEnabled = data.hideIdEnabled;
            this.antiAfkEnabled = data.antiAfkEnabled;
            this.antiAfkInterval = Mathf.Clamp(data.antiAfkInterval, 5f, 120f);
            this.autoRepairType = Mathf.Clamp(data.autoRepairType, 0, this.autoRepairOptions.Length - 1);
            this.autoEatFoodType = Mathf.Clamp(data.autoEatFoodType, 0, this.autoEatFoodOptions.Length - 1);
            this.repairTeleportBackEnabled = data.repairTeleportBackEnabled;
            this.collectEventResources = data.collectEventResources;
        }

        private void PopulateUiThemeConfig(UiThemeConfigData data)
        {
            data.uiAccentR = this.uiAccentR;
            data.uiAccentG = this.uiAccentG;
            data.uiAccentB = this.uiAccentB;
            data.uiTextR = this.uiTextR;
            data.uiTextG = this.uiTextG;
            data.uiTextB = this.uiTextB;
            data.uiMainTabTextR = this.uiMainTabTextR;
            data.uiMainTabTextG = this.uiMainTabTextG;
            data.uiMainTabTextB = this.uiMainTabTextB;
            data.uiSubTabTextR = this.uiSubTabTextR;
            data.uiSubTabTextG = this.uiSubTabTextG;
            data.uiSubTabTextB = this.uiSubTabTextB;
            data.uiWindowR = this.uiWindowR;
            data.uiWindowG = this.uiWindowG;
            data.uiWindowB = this.uiWindowB;
            data.uiPanelR = this.uiPanelR;
            data.uiPanelG = this.uiPanelG;
            data.uiPanelB = this.uiPanelB;
            data.uiContentR = this.uiContentR;
            data.uiContentG = this.uiContentG;
            data.uiContentB = this.uiContentB;
            data.uiWindowAlpha = this.uiWindowAlpha;
            data.uiPanelAlpha = this.uiPanelAlpha;
            data.uiContentAlpha = this.uiContentAlpha;
        }

        private void ApplyUiThemeConfig(UiThemeConfigData data)
        {
            if (data == null) return;
            this.uiAccentR = Mathf.Clamp01(data.uiAccentR);
            this.uiAccentG = Mathf.Clamp01(data.uiAccentG);
            this.uiAccentB = Mathf.Clamp01(data.uiAccentB);
            this.uiTextR = Mathf.Clamp01(data.uiTextR);
            this.uiTextG = Mathf.Clamp01(data.uiTextG);
            this.uiTextB = Mathf.Clamp01(data.uiTextB);
            this.uiMainTabTextR = Mathf.Clamp01(data.uiMainTabTextR);
            this.uiMainTabTextG = Mathf.Clamp01(data.uiMainTabTextG);
            this.uiMainTabTextB = Mathf.Clamp01(data.uiMainTabTextB);
            this.uiSubTabTextR = Mathf.Clamp01(data.uiSubTabTextR);
            this.uiSubTabTextG = Mathf.Clamp01(data.uiSubTabTextG);
            this.uiSubTabTextB = Mathf.Clamp01(data.uiSubTabTextB);
            this.uiWindowR = Mathf.Clamp01(data.uiWindowR);
            this.uiWindowG = Mathf.Clamp01(data.uiWindowG);
            this.uiWindowB = Mathf.Clamp01(data.uiWindowB);
            this.uiPanelR = Mathf.Clamp01(data.uiPanelR);
            this.uiPanelG = Mathf.Clamp01(data.uiPanelG);
            this.uiPanelB = Mathf.Clamp01(data.uiPanelB);
            this.uiContentR = Mathf.Clamp01(data.uiContentR);
            this.uiContentG = Mathf.Clamp01(data.uiContentG);
            this.uiContentB = Mathf.Clamp01(data.uiContentB);
            this.uiWindowAlpha = Mathf.Clamp(data.uiWindowAlpha, 0.15f, 1f);
            this.uiPanelAlpha = Mathf.Clamp(data.uiPanelAlpha, 0.15f, 1f);
            this.uiContentAlpha = Mathf.Clamp(data.uiContentAlpha, 0.15f, 1f);
        }

        private void PopulateRadarConfig(RadarConfigData data)
        {
            data.radarMarkerStyle = this.radarMarkerStyle;
            data.priorityFiddlehead = this.priorityFiddlehead;
            data.priorityTallMustard = this.priorityTallMustard;
            data.priorityBurdock = this.priorityBurdock;
            data.priorityMustardGreens = this.priorityMustardGreens;
        }

        private void ApplyRadarConfig(RadarConfigData data)
        {
            if (data == null) return;
            this.radarMarkerStyle = data.radarMarkerStyle;
            this.priorityFiddlehead = data.priorityFiddlehead;
            this.priorityTallMustard = data.priorityTallMustard;
            this.priorityBurdock = data.priorityBurdock;
            this.priorityMustardGreens = data.priorityMustardGreens;
        }

        private void PopulateAllConfigSections(UnifiedConfigData data)
        {
            if (data == null) return;
            this.PopulateKeybindConfig(data.Keybinds);
            this.PopulateUiThemeConfig(data.UiTheme);
            this.PopulateRadarConfig(data.Radar);

            data.Patrol = new PatrolData();
            foreach (Vector3 p in patrolPoints)
            {
                data.Patrol.Points.Add(new SerializableVector3(p));
            }

            data.TreeFarmPatrol = new TreeFarmPatrolData();
            data.TreeFarmPatrol.Points = new List<TreeFarmPatrolPoint>(treeFarmPoints);

            if (data.CookingPatrolSaves == null) data.CookingPatrolSaves = new List<NamedCookingPatrolSave>();
            string currentCookingName = this.SanitizeCookingPatrolSaveName(this.cookingPatrolSaveName);
            if (!string.IsNullOrEmpty(currentCookingName))
            {
                data.CookingPatrolSaves.RemoveAll(s => string.Equals(this.SanitizeCookingPatrolSaveName(s?.Name), currentCookingName, StringComparison.OrdinalIgnoreCase));
                data.CookingPatrolSaves.Add(new NamedCookingPatrolSave
                {
                    Name = currentCookingName,
                    Points = new List<CookingPatrolPoint>(cookingPatrolPoints)
                });
            }

            data.CustomTeleports = new List<CustomTeleportEntry>();
            foreach (CustomTeleportEntry entry in this.customTeleportList)
            {
                if (entry == null) continue;
                data.CustomTeleports.Add(new CustomTeleportEntry
                {
                    name = (entry.name ?? "").Replace("\"", "").Replace("\\", ""),
                    position = entry.position
                });
            }
        }

        private void SaveKeybinds(bool showNotification = true)
        {
            try
            {
                UnifiedConfigData data = this.LoadOrCreateUnifiedConfig();
                this.PopulateAllConfigSections(data);
                this.SaveUnifiedConfig(data);
                MelonLogger.Msg("Keybinds Saved!");
                if (showNotification)
                {
                    this.AddMenuNotification("Keybinds saved", new Color(0.55f, 0.88f, 1f));
                }
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
                UnifiedConfigData config = this.LoadUnifiedConfig();
                if (config != null)
                {
                    this.ApplyKeybindConfig(config.Keybinds);
                    MelonLogger.Msg("Keybinds Loaded.");
                    this.AddMenuNotification("Keybinds loaded", new Color(0.55f, 0.88f, 1f));
                    return;
                }
                string path = this.GetKeybindsPath();
                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (string line in lines)
                    {
                        if (line.Contains("keyToggleMenu")) this.keyToggleMenu = (KeyCode)GetJsonInt(line, "\"keyToggleMenu\":");
                        else if (line.Contains("keyToggleRadar")) this.keyToggleRadar = (KeyCode)GetJsonInt(line, "\"keyToggleRadar\":");
                        else if (line.Contains("keyAutoFarm") || line.Contains("keyAutoForaging")) this.keyAutoForaging = (KeyCode)GetJsonInt(line, line.Contains("keyAutoFarm") ? "\"keyAutoFarm\":" : "\"keyAutoForaging\":");
                        else if (line.Contains("keyAutoFishFarm") || line.Contains("keyAutoFishingTeleport")) this.keyAutoFishingTeleport = (KeyCode)GetJsonInt(line, line.Contains("keyAutoFishFarm") ? "\"keyAutoFishFarm\":" : "\"keyAutoFishingTeleport\":");
                        else if (line.Contains("keyAutoFish")) this.keyAutoFish = (KeyCode)GetJsonInt(line, "\"keyAutoFish\":");
                        else if (line.Contains("keyAutoCook")) this.keyAutoCook = (KeyCode)GetJsonInt(line, "\"keyAutoCook\":");
                        else if (line.Contains("keyBypassUI")) this.keyBypassUI = (KeyCode)GetJsonInt(line, "\"keyBypassUI\":");
                        else if (line.Contains("keyDisableAll")) this.keyDisableAll = (KeyCode)GetJsonInt(line, "\"keyDisableAll\":");
                        else if (line.Contains("keyInspectPlayer")) this.keyInspectPlayer = (KeyCode)GetJsonInt(line, "\"keyInspectPlayer\":");
                        else if (line.Contains("keyInspectMove")) this.keyInspectMove = (KeyCode)GetJsonInt(line, "\"keyInspectMove\":");
                        else if (line.Contains("keyAutoRepair")) this.keyAutoRepair = (KeyCode)GetJsonInt(line, "\"keyAutoRepair\":");
                        else if (line.Contains("keyAutoJoinFriend")) this.keyAutoJoinFriend = (KeyCode)GetJsonInt(line, "\"keyAutoJoinFriend\":");
                        else if (line.Contains("keyAutoSnow")) this.autoSnowHotkey = (KeyCode)GetJsonInt(line, "\"keyAutoSnow\":");
                        else if (line.Contains("keyJoinPublic")) this.keyJoinPublic = (KeyCode)GetJsonInt(line, "\"keyJoinPublic\":");
                        else if (line.Contains("keyJoinMyTown")) this.keyJoinMyTown = (KeyCode)GetJsonInt(line, "\"keyJoinMyTown\":");
                        else if (line.Contains("keyNoclip")) this.keyNoclip = (KeyCode)GetJsonInt(line, "\"keyNoclip\":");
                        else if (line.Contains("noclipSpeed")) this.noclipSpeed = GetJsonFloat(line, "\"noclipSpeed\":");
                        else if (line.Contains("noclipBoostMultiplier")) this.noclipBoostMultiplier = GetJsonFloat(line, "\"noclipBoostMultiplier\":");
                        else if (line.Contains("areaLoadDelay")) this.areaLoadDelay = GetJsonInt(line, "\"areaLoadDelay\":");
                        else if (line.Contains("resourceTeleportCooldown")) this.resourceTeleportCooldown = GetJsonFloat(line, "\"resourceTeleportCooldown\":");
                        else if (line.Contains("resourceClickDuration")) this.resourceClickDuration = GetJsonFloat(line, "\"resourceClickDuration\":");
                        else if (line.Contains("resourceAutoRepairPauseSeconds")) this.resourceAutoRepairPauseSeconds = GetJsonFloat(line, "\"resourceAutoRepairPauseSeconds\":");
                        else if (line.Contains("gameSpeed")) this.gameSpeed = GetJsonFloat(line, "\"gameSpeed\":");
                        else if (line.Contains("customCameraFOVEnabled")) this.customCameraFOVEnabled = GetJsonInt(line, "\"customCameraFOVEnabled\":") != 0;
                        else if (line.Contains("cameraFOV")) this.cameraFOV = GetJsonFloat(line, "\"cameraFOV\":");
                        else if (line.Contains("snowClickInterval")) this.snowClickInterval = GetJsonFloat(line, "\"snowClickInterval\":");
                        else if (line.Contains("sculptIconClickInterval")) this.sculptIconClickInterval = GetJsonFloat(line, "\"sculptIconClickInterval\":");
                        else if (line.Contains("cookingAutoSpeed")) this.cookingAutoSpeed = GetJsonFloat(line, "\"cookingAutoSpeed\":");
                        else if (line.Contains("cookingWaitAtSpot")) this.cookingWaitAtSpot = GetJsonFloat(line, "\"cookingWaitAtSpot\":");
                        else if (line.Contains("autoFishScanTimeout")) this.saved_autoFishScanTimeout = GetJsonFloat(line, "\"autoFishScanTimeout\":");
                        else if (line.Contains("autoFishTeleportDelay")) this.saved_autoFishTeleportDelay = GetJsonFloat(line, "\"autoFishTeleportDelay\":");
                        else if (line.Contains("autoFishFishShadowDetectRange")) this.saved_autoFishFishShadowDetectRange = GetJsonFloat(line, "\"autoFishFishShadowDetectRange\":");
                        else if (line.Contains("autoFishReelMaxDuration")) this.saved_autoFishReelMaxDuration = GetJsonFloat(line, "\"autoFishReelMaxDuration\":");
                        else if (line.Contains("autoFishReelHoldDuration")) this.saved_autoFishReelHoldDuration = GetJsonFloat(line, "\"autoFishReelHoldDuration\":");
                        else if (line.Contains("autoFishReelPauseDuration")) this.saved_autoFishReelPauseDuration = GetJsonFloat(line, "\"autoFishReelPauseDuration\":");
                        else if (line.Contains("insectTeleportCooldown")) InsectFarm.SetTeleportCooldown(GetJsonFloat(line, "\"insectTeleportCooldown\":"));
                        else if (line.Contains("insectScanTimeout")) InsectFarm.SetScanTimeout(GetJsonFloat(line, "\"insectScanTimeout\":"));
                        else if (line.Contains("insectTeleportOffset")) InsectFarm.SetTeleportOffset(GetJsonFloat(line, "\"insectTeleportOffset\":"));
                        else if (line.Contains("keyAutoEat")) this.keyAutoEat = (KeyCode)GetJsonInt(line, "\"keyAutoEat\":");
                        else if (line.Contains("keyAntiAfk")) this.keyAntiAfk = (KeyCode)GetJsonInt(line, "\"keyAntiAfk\":");
                        else if (line.Contains("keyBypassOverlap")) this.keyBypassOverlap = (KeyCode)GetJsonInt(line, "\"keyBypassOverlap\":");
                        else if (line.Contains("keyBirdVacuum")) this.keyBirdVacuum = (KeyCode)GetJsonInt(line, "\"keyBirdVacuum\":");
                        else if (line.Contains("notificationsEnabled")) this.notificationsEnabled = GetJsonInt(line, "\"notificationsEnabled\":") != 0;
                        else if (line.Contains("blockGameUiWhenMenuOpen")) this.blockGameUiWhenMenuOpen = GetJsonInt(line, "\"blockGameUiWhenMenuOpen\":") != 0;
                        else if (line.Contains("showStatusOverlay")) this.showStatusOverlay = GetJsonInt(line, "\"showStatusOverlay\":") != 0;
                        else if (line.Contains("maxAutoEatAttempts")) this.maxAutoEatAttempts = GetJsonInt(line, "\"maxAutoEatAttempts\":");
                        else if (line.Contains("autoClickStartEnabled")) this.autoClickStartEnabled = GetJsonInt(line, "\"autoClickStartEnabled\":") != 0;
                        else if (line.Contains("hideIdEnabled")) this.hideIdEnabled = GetJsonInt(line, "\"hideIdEnabled\":") != 0;
                        else if (line.Contains("antiAfkEnabled")) this.antiAfkEnabled = GetJsonInt(line, "\"antiAfkEnabled\":") != 0;
                        else if (line.Contains("antiAfkInterval")) this.antiAfkInterval = Mathf.Clamp(GetJsonFloat(line, "\"antiAfkInterval\":"), 5f, 120f);
                        else if (line.Contains("autoRepairType")) this.autoRepairType = Mathf.Clamp(GetJsonInt(line, "\"autoRepairType\":"), 0, this.autoRepairOptions.Length - 1);
                        else if (line.Contains("autoEatFoodType")) this.autoEatFoodType = Mathf.Clamp(GetJsonInt(line, "\"autoEatFoodType\":"), 0, this.autoEatFoodOptions.Length - 1);
                        else if (line.Contains("repairTeleportBackEnabled")) this.repairTeleportBackEnabled = GetJsonInt(line, "\"repairTeleportBackEnabled\":") != 0;
                        else if (line.Contains("collectEventResources")) this.collectEventResources = GetJsonInt(line, "\"collectEventResources\":") != 0;
                        else if (line.Contains("collectFiddlehead")) this.collectEventResources = this.collectEventResources || (GetJsonInt(line, "\"collectFiddlehead\":") != 0);
                        else if (line.Contains("collectTallMustard")) this.collectEventResources = this.collectEventResources || (GetJsonInt(line, "\"collectTallMustard\":") != 0);
                        else if (line.Contains("collectBurdock")) this.collectEventResources = this.collectEventResources || (GetJsonInt(line, "\"collectBurdock\":") != 0);
                        else if (line.Contains("collectMustardGreens")) this.collectEventResources = this.collectEventResources || (GetJsonInt(line, "\"collectMustardGreens\":") != 0);
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
                UnifiedConfigData data = this.LoadOrCreateUnifiedConfig();
                this.PopulateAllConfigSections(data);
                this.SaveUnifiedConfig(data);
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
                UnifiedConfigData config = this.LoadUnifiedConfig();
                if (config != null)
                {
                    this.ApplyUiThemeConfig(config.UiTheme);
                    this.InvalidateThemeCache();
                    this.uiThemeHexInput = this.ColorToHex(this.GetUiThemeColorTargetValue(this.uiThemeColorTarget));
                    MelonLogger.Msg("UI Theme Loaded.");
                    this.AddMenuNotification("UI theme loaded", new Color(0.55f, 0.88f, 1f));
                    return;
                }
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

        private string GetRadarSettingsPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "radar_settings.json");
        }

        private void SaveRadarSettings()
        {
            try
            {
                UnifiedConfigData data = this.LoadOrCreateUnifiedConfig();
                this.PopulateAllConfigSections(data);
                this.SaveUnifiedConfig(data);
                MelonLogger.Msg("Radar settings saved.");
                this.AddMenuNotification("Radar settings saved", new Color(0.55f, 0.88f, 1f));
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error Saving Radar Settings: " + ex.Message);
                this.AddMenuNotification("Failed to save radar settings", new Color(1f, 0.4f, 0.4f));
            }
        }

        private void LoadRadarSettings()
        {
            try
            {
                UnifiedConfigData config = this.LoadUnifiedConfig();
                if (config != null)
                {
                    this.ApplyRadarConfig(config.Radar);
                    MelonLogger.Msg("Radar settings loaded.");
                    return;
                }
                string path = this.GetRadarSettingsPath();
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    if (line.Contains("radarMarkerStyle"))
                    {
                        int v = (int)GetJsonFloat(line, "\"radarMarkerStyle\":");
                        this.radarMarkerStyle = v;
                    }
                    else if (line.Contains("priorityFiddlehead"))
                    {
                        this.priorityFiddlehead = GetJsonFloat(line, "\"priorityFiddlehead\":") != 0f;
                    }
                    else if (line.Contains("priorityTallMustard"))
                    {
                        this.priorityTallMustard = GetJsonFloat(line, "\"priorityTallMustard\":") != 0f;
                    }
                    else if (line.Contains("priorityBurdock"))
                    {
                        this.priorityBurdock = GetJsonFloat(line, "\"priorityBurdock\":") != 0f;
                    }
                    else if (line.Contains("priorityMustardGreens"))
                    {
                        this.priorityMustardGreens = GetJsonFloat(line, "\"priorityMustardGreens\":") != 0f;
                    }
                }
                MelonLogger.Msg("Radar settings loaded.");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error Loading Radar Settings: " + ex.Message);
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
            this.LoadRadarSettings();
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

            try
            {
                Action<string, Type[], Type, string> patchInputPostfix = (methodName, args, patchType, label) =>
                {
                    MethodInfo target = typeof(Input).GetMethod(methodName, args);
                    MethodInfo patch = patchType.GetMethod("Postfix", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (target != null && patch != null)
                    {
                        HeartopiaComplete.harmonyInstance.Patch(target, null, new HarmonyMethod(patch), null, null, null);
                        MelonLogger.Msg($"✓ Patched {label}");
                    }
                    else
                    {
                        MelonLogger.Msg($"✗ Failed to patch {label} - target: {target != null}, patch: {patch != null}");
                    }
                };

                patchInputPostfix("GetKey", new Type[] { typeof(KeyCode) }, typeof(AutoFishGetKeyPatch), "Input.GetKey(KeyCode) via AutoFishGetKeyPatch");
                patchInputPostfix("GetKey", new Type[] { typeof(string) }, typeof(AutoFishGetKeyStringPatch), "Input.GetKey(string) via AutoFishGetKeyStringPatch");
                patchInputPostfix("GetKeyDown", new Type[] { typeof(KeyCode) }, typeof(AutoFishGetKeyDownPatch), "Input.GetKeyDown(KeyCode) via AutoFishGetKeyDownPatch");
                patchInputPostfix("GetKeyDown", new Type[] { typeof(string) }, typeof(AutoFishGetKeyDownStringPatch), "Input.GetKeyDown(string) via AutoFishGetKeyDownStringPatch");
                patchInputPostfix("GetKeyUp", new Type[] { typeof(KeyCode) }, typeof(AutoFishGetKeyUpPatch), "Input.GetKeyUp(KeyCode) via AutoFishGetKeyUpPatch");
                patchInputPostfix("GetKeyUp", new Type[] { typeof(string) }, typeof(AutoFishGetKeyUpStringPatch), "Input.GetKeyUp(string) via AutoFishGetKeyUpStringPatch");
                patchInputPostfix("GetMouseButton", new Type[] { typeof(int) }, typeof(AutoFishGetMouseButtonPatch), "Input.GetMouseButton(int) via AutoFishGetMouseButtonPatch");
                patchInputPostfix("GetMouseButtonDown", new Type[] { typeof(int) }, typeof(AutoFishGetMouseButtonDownPatch), "Input.GetMouseButtonDown(int) via AutoFishGetMouseButtonDownPatch");
                patchInputPostfix("GetMouseButtonUp", new Type[] { typeof(int) }, typeof(AutoFishGetMouseButtonUpPatch), "Input.GetMouseButtonUp(int) via AutoFishGetMouseButtonUpPatch");
                patchInputPostfix("GetAxis", new Type[] { typeof(string) }, typeof(AutoFishGetAxisPatch), "Input.GetAxis(string) via AutoFishGetAxisPatch");
                patchInputPostfix("GetAxisRaw", new Type[] { typeof(string) }, typeof(AutoFishGetAxisRawPatch), "Input.GetAxisRaw(string) via AutoFishGetAxisRawPatch");

                MethodInfo mousePosGetter = typeof(Input).GetProperty("mousePosition")?.GetGetMethod();
                MethodInfo mousePosPatch = typeof(AutoFishMousePositionPatch).GetMethod("Postfix", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (mousePosGetter != null && mousePosPatch != null)
                {
                    HeartopiaComplete.harmonyInstance.Patch(mousePosGetter, null, new HarmonyMethod(mousePosPatch), null, null, null);
                    MelonLogger.Msg("✓ Patched Input.mousePosition getter via AutoFishMousePositionPatch");
                }
                else
                {
                    MelonLogger.Msg($"✗ Failed to patch Input.mousePosition getter - target: {mousePosGetter != null}, patch: {mousePosPatch != null}");
                }
            }
            catch (Exception ex5)
            {
                MelonLogger.Msg("✗ AutoFish input patch registration failed: " + ex5.Message);
            }
            MelonLogger.Msg("=== Patch Attempt Complete ===");

            try
            {
                this.autoFishLogic = new AutoFishLogic(() => GameObject.Find("p_player_skeleton(Clone)"));
                this.autoFishFarm = new AutoFishFarm(this.autoFishLogic, (Vector3 pos) => { this.TeleportToLocation(pos, Quaternion.identity); });
                MelonLogger.Msg("✓ AutoFish subsystem instantiated.");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("✗ AutoFish init failed: " + ex.Message);
            }
            // Apply any loaded auto-fish settings
            try
            {
                if (this.autoFishFarm != null)
                {
                    if (this.saved_autoFishScanTimeout > 0f) this.autoFishFarm.scanTimeout = this.saved_autoFishScanTimeout;
                    if (this.saved_autoFishTeleportDelay > 0f) this.autoFishFarm.teleportDelay = this.saved_autoFishTeleportDelay;
                }
                if (this.autoFishLogic != null)
                {
                    if (this.saved_autoFishFishShadowDetectRange > 0f) this.autoFishLogic.fishShadowDetectRange = this.saved_autoFishFishShadowDetectRange;
                    if (this.saved_autoFishReelMaxDuration > 0f) this.autoFishLogic.reelMaxDuration = this.saved_autoFishReelMaxDuration;
                    if (this.saved_autoFishReelHoldDuration > 0f) this.autoFishLogic.reelHoldDuration = this.saved_autoFishReelHoldDuration;
                    if (this.saved_autoFishReelPauseDuration > 0f) this.autoFishLogic.reelPauseDuration = this.saved_autoFishReelPauseDuration;
                }
            }
            catch { }
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

            // Only force FOV while the custom override is enabled.
            if (this.customCameraFOVEnabled)
            {
                this.ApplyCameraFOV();
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

            // Periodic toast-panel scan fallback (in case UIManager hook isn't available)
            try { this.CheckToastPanel(); } catch { }

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
            
            // Handle Noclip Flying
            if (this.noclipEnabled)
            {
                GameObject player = GetPlayer();
                if (player != null)
                {
                    Vector3 moveDirection = Vector3.zero;
                    
                    // Get camera for movement directions
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        // Horizontal movement relative to camera (ignoring Y rotation for standard WASD)
                        Vector3 cameraForward = mainCamera.transform.forward;
                        Vector3 cameraRight = mainCamera.transform.right;
                        
                        // Flatten to horizontal plane
                        cameraForward.y = 0;
                        cameraRight.y = 0;
                        cameraForward.Normalize();
                        cameraRight.Normalize();
                        
                        if (Input.GetKey(KeyCode.W)) moveDirection += cameraForward;
                        if (Input.GetKey(KeyCode.S)) moveDirection -= cameraForward;
                        if (Input.GetKey(KeyCode.A)) moveDirection -= cameraRight;
                        if (Input.GetKey(KeyCode.D)) moveDirection += cameraRight;
                    }
                    else
                    {
                        // Fallback to world directions if no camera
                        if (Input.GetKey(KeyCode.W)) moveDirection += Vector3.forward;
                        if (Input.GetKey(KeyCode.S)) moveDirection += Vector3.back;
                        if (Input.GetKey(KeyCode.A)) moveDirection += Vector3.left;
                        if (Input.GetKey(KeyCode.D)) moveDirection += Vector3.right;
                    }
                    
                    // Vertical movement (Space/Ctrl)
                    if (Input.GetKey(KeyCode.Space)) moveDirection += Vector3.up;
                    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) moveDirection -= Vector3.up;
                    
                    // Calculate speed with boost
                    float currentSpeed = this.noclipSpeed;
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        currentSpeed *= this.noclipBoostMultiplier;
                    }
                    
                    // Normalize and apply speed
                    if (moveDirection != Vector3.zero)
                    {
                        moveDirection.Normalize();
                        Vector3 newPosition = player.transform.position + moveDirection * currentSpeed * Time.deltaTime;
                        HeartopiaComplete.OverridePlayerPosition = true;
                        HeartopiaComplete.OverridePosition = newPosition;
                    }
                }
            }
            
            // Check for keybinds (Only if not currently rebinding and not just assigned)
            if (string.IsNullOrEmpty(this.keyBindingActive) && Time.unscaledTime - this.keyBindAssignedAt >= 0.2f)
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
                if (Input.GetKeyDown(this.keyAutoForaging))
                {
                    this.autoFarmEnabled = !this.autoFarmEnabled;
                    MelonLogger.Msg("Auto Collect " + (this.autoFarmEnabled ? "Enabled" : "Disabled"));
                    this.AddMenuNotification($"Auto Foraging {(this.autoFarmEnabled ? "Enabled" : "Disabled")}", this.autoFarmEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyAutoFish))
                {
                    if (this.autoFishLogic != null)
                    {
                        this.autoFishLogic.ToggleAutoFish();
                        this.AddMenuNotification($"Auto Fish {(this.autoFishLogic.autoFishEnabled ? "Enabled" : "Disabled")}", this.autoFishLogic.autoFishEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                    }
                    else
                    {
                        this.AddMenuNotification("Auto Fish subsystem not initialized", new Color(1f, 0.55f, 0.55f));
                    }
                }
                if (Input.GetKeyDown(this.keyAutoFishingTeleport))
                {
                    if (this.autoFishFarm != null)
                    {
                        this.autoFishFarm.ToggleFarm();
                        this.AddMenuNotification($"Auto Fishing (Teleport) {(this.autoFishFarm.farmEnabled ? "Enabled" : "Disabled")}", this.autoFishFarm.farmEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                    }
                    else
                    {
                        this.AddMenuNotification("Auto Fishing (Teleport) subsystem not initialized", new Color(1f, 0.55f, 0.55f));
                    }
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
                if (Input.GetKeyDown(this.keyBypassOverlap))
                {
                    this.bypassOverlapEnabled = !this.bypassOverlapEnabled;
                    HeartopiaComplete.bypassOverlapEnabledStatic = this.bypassOverlapEnabled;
                    if (this.bypassOverlapEnabled && !this.bypassOverlapPatched)
                        this.EnsureBypassPatched();
                    this.AddMenuNotification($"Bypass Overlap {(this.bypassOverlapEnabled ? "Enabled" : "Disabled")}", this.bypassOverlapEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyBirdVacuum))
                {
                    this.birdVacuumEnabled = !this.birdVacuumEnabled;
                    this.AddMenuNotification($"Bird Vacuum {(this.birdVacuumEnabled ? "Enabled" : "Disabled")}", this.birdVacuumEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyDisableAll))
                {
                    this.autoFarmEnabled = false;
                    this.bypassEnabled = false;
                    this.antiAfkEnabled = false;
                    this.StopAutoCookInternal("Disabled");
                    this.pendingToolEquipType = 0;
                    this.isAutoEating = false;
                    this.StopTreeFarm("Stopped");
                    this.noclipEnabled = false;
                    HeartopiaComplete.OverridePlayerPosition = false;
                    this.noclipBoostMultiplier = 2f;
                    this.gameSpeed = 1f;
                    Time.timeScale = 1f;
                    // Disable fishing features
                    if (this.autoFishLogic != null) this.autoFishLogic.ToggleAutoFish();
                    if (this.autoFishFarm != null) this.autoFishFarm.ToggleFarm();
                    this.showFishShadowRadar = false;
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
                        MelonLogger.Msg("[AutoRepair] Hotkey requested StartRepair");
                        this.StartRepair();
                        this.AddMenuNotification("Auto Repair started", new Color(0.45f, 1f, 0.55f));
                    }
                    else
                    {
                        this.AddMenuNotification("Auto Repair already running", new Color(1f, 0.55f, 0.55f));
                    }
                }
                if (Input.GetKeyDown(this.keyAutoEat))
                {
                    if (!this.isRepairing && !this.isAutoEating)
                    {
                        this.StartAutoEat();
                        this.AddMenuNotification($"Auto Eat started ({this.autoEatFoodOptions[this.autoEatFoodType]})", new Color(0.45f, 1f, 0.55f));
                    }
                    else
                    {
                        this.AddMenuNotification("Auto Eat already running", new Color(1f, 0.55f, 0.55f));
                    }
                }
                if (Input.GetKeyDown(this.keyAntiAfk))
                {
                    this.antiAfkEnabled = !this.antiAfkEnabled;
                    this.lastAntiAfkPulseAt = Time.unscaledTime;
                    AutoFishLogic.SimulateMouseButton0 = false;
                    AutoFishLogic.SimulateMouseButton0Down = false;
                    this.antiAfkMouseDownClearAt = 0f;
                    this.antiAfkMouseHoldClearAt = 0f;
                    this.SaveKeybinds(false);
                    this.AddMenuNotification($"Anti AFK {(this.antiAfkEnabled ? "Enabled" : "Disabled")}", this.antiAfkEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                }
                // Auto Snow hotkey handling
                if (this.isListeningForAutoSnowHotkey)
                {
                    foreach (object k in Enum.GetValues(typeof(KeyCode)))
                    {
                        KeyCode kc = (KeyCode)k;
                        if (Input.GetKeyDown(kc) && kc != KeyCode.Escape)
                        {
                            this.autoSnowHotkey = kc;
                            this.isListeningForAutoSnowHotkey = false;
                            this.AddMenuNotification($"Auto Snow Hotkey set: {kc}", new Color(0.45f, 1f, 0.55f));
                            break;
                        }
                    }
                }
                else
                {
                    if (Input.GetKeyDown(this.autoSnowHotkey))
                    {
                        this.autoSnowEnabled = !this.autoSnowEnabled;
                        if (!this.autoSnowEnabled) this.snowWidgetQueue.Clear();
                        this.AddMenuNotification($"Auto Snow Sculpture {(this.autoSnowEnabled ? "Enabled" : "Disabled")}", this.autoSnowEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
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
                if (Input.GetKeyDown(this.keyNoclip))
                {
                    this.noclipEnabled = !this.noclipEnabled;
                    if (this.noclipEnabled)
                    {
                        // Set override position to current position to prevent unwanted movement
                        GameObject player = GetPlayer();
                        if (player != null)
                        {
                            HeartopiaComplete.OverridePosition = player.transform.position;
                        }
                        HeartopiaComplete.OverridePlayerPosition = true;
                        this.AddMenuNotification("Noclip: ENABLED", new Color(0.45f, 1f, 0.55f));
                    }
                    else
                    {
                        HeartopiaComplete.OverridePlayerPosition = false;
                        this.AddMenuNotification("Noclip: DISABLED", new Color(1f, 0.55f, 0.55f));
                    }
                }
                if (Input.GetKeyDown(this.keyGameSpeed1x))
                {
                    this.gameSpeed = 1f;
                    Time.timeScale = 1f;
                    this.AddMenuNotification("Game Speed: 1x", new Color(0.45f, 1f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyGameSpeed2x))
                {
                    this.gameSpeed = 2f;
                    Time.timeScale = 2f;
                    this.AddMenuNotification("Game Speed: 2x", new Color(0.45f, 1f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyGameSpeed5x))
                {
                    this.gameSpeed = 5f;
                    Time.timeScale = 5f;
                    this.AddMenuNotification("Game Speed: 5x", new Color(0.45f, 1f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyGameSpeed10x))
                {
                    this.gameSpeed = 10f;
                    Time.timeScale = 10f;
                    this.AddMenuNotification("Game Speed: 10x", new Color(0.45f, 1f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyEquipAxe))
                {
                    this.StartToolEquipRequest(1);
                    this.AddMenuNotification("Equipping Axe", new Color(0.45f, 1f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyEquipNet))
                {
                    this.StartToolEquipRequest(2);
                    this.AddMenuNotification("Equipping Net", new Color(0.45f, 1f, 0.55f));
                }
                if (Input.GetKeyDown(this.keyEquipRod))
                {
                    this.StartToolEquipRequest(3);
                    this.AddMenuNotification("Equipping Rod", new Color(0.45f, 1f, 0.55f));
                }
            }

            this.RunAntiAfkTick();
            if (this.antiAfkMouseDownClearAt > 0f && Time.unscaledTime >= this.antiAfkMouseDownClearAt)
            {
                AutoFishLogic.SimulateMouseButton0Down = false;
                this.antiAfkMouseDownClearAt = 0f;
            }
            if (this.antiAfkMouseHoldClearAt > 0f && Time.unscaledTime >= this.antiAfkMouseHoldClearAt)
            {
                AutoFishLogic.SimulateMouseButton0 = false;
                this.antiAfkMouseHoldClearAt = 0f;
            }

            // Check for durability notification
            if (Time.time - lastNotificationCheck > NOTIFICATION_CHECK_INTERVAL)
            {
                lastNotificationCheck = Time.time;
                if (CheckForDurabilityNotification())
                {
                    if (!this.isRepairing && !this.isAutoEating)
                    {
                            MelonLogger.Msg("[AutoRepair] Durability toast requested StartRepair");
                            // mark this start as an auto-repair request so StartRepair knows
                            // to run the multi-use auto logic
                            this.lastStartWasAutoRepair = true;
                            this.StartRepair();
                            // Pause resource farm teleports for configured seconds
                            this.resourceRepairPauseUntil = Time.time + this.resourceAutoRepairPauseSeconds;
                            this.AddMenuNotification($"Auto Repair triggered by durability notification — pausing farm for {this.resourceAutoRepairPauseSeconds:F0}s", new Color(0.45f, 1f, 0.55f));
                    }
                }
                if (CheckForEnergyNotification())
                {
                    if (!this.isRepairing && !this.isAutoEating)
                    {
                        this.StartAutoEat();
                        this.AddMenuNotification($"Auto Eat triggered by energy low toast ({this.autoEatFoodOptions[this.autoEatFoodType]})", new Color(0.45f, 1f, 0.55f));
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

            // Meteor Scan (Every 60 frames, only when ESP is active)
            if (this.showMeteorESP && Time.frameCount % 60 == 0)
            {
                this.ScanMeteorites();
            }
            // Camera FOV will be applied in OnLateUpdate to avoid competing with game camera updates

            // Clear scheduled simulated F-key states
            if (this.nextSimulatedFKeyClearAt > 0f && Time.unscaledTime >= this.nextSimulatedFKeyClearAt)
            {
                HeartopiaComplete.SimulateFKeyDown = false;
                HeartopiaComplete.SimulateFKeyHeld = false;
                HeartopiaComplete.SimulateFKeyUp = true;
                this.nextSimulatedFKeyClearAt = 0f;
                this.nextSimulatedFKeyUpClearAt = Time.unscaledTime + 0.05f;
            }
            if (this.nextSimulatedFKeyUpClearAt > 0f && Time.unscaledTime >= this.nextSimulatedFKeyUpClearAt)
            {
                HeartopiaComplete.SimulateFKeyUp = false;
                this.nextSimulatedFKeyUpClearAt = 0f;
            }

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
                // Player alert: throttled to every 3s to avoid per-frame FindObjectsOfType crash
                if (this.enablePlayerDetection && !this.cookingCleanupMode
                    && Time.unscaledTime - this.lastPlayerDetectionCheckAt >= 3f)
                {
                    this.lastPlayerDetectionCheckAt = Time.unscaledTime;
                    float nearestPlayer = this.GetNearestPlayerDistance();
                    if (nearestPlayer < cookingPlayerAlertRadius)
                    {
                        this.cookingCleanupMode = true;
                        MelonLogger.Msg($"[Cooking] ⚠️ PLAYER DETECTED ({nearestPlayer:F0}m) - Starting cleanup!");
                    }
                }

                // Run the original cook logic on a timer (teleport patrol runs independently as a coroutine)
                if (Time.time >= this.nextCookTime)
                {
                    this.RunAutoCookLogic();
                    this.nextCookTime = Time.time + 0.2f;
                }
                // Auto-stop timer for Auto Cook
                if (this.autoCookEnabled && this.autoCookAutoStopEnabled && this.autoCookAutoStopAt > 0f && Time.unscaledTime >= this.autoCookAutoStopAt)
                {
                    this.StopAutoCookInternal("auto-stopped (timer)");
                    this.AddMenuNotification("Auto Cook auto-stopped (timer)", new Color(1f, 0.75f, 0.45f));
                }
            }
            if (this.autoSnowEnabled)
            {
                this.RunAutoSnowLogic();
            }
            if (this.autoSculptureIconRapidEnabled)
            {
                this.RunSculptIconRapid();
            }
            if (this.autoCatPlayEnabled)
            {
                this.RunAutoCatPlayLogic();
            }
            if (this.autoBuyEnabled)
            {
                this.RunAutoBuyLogic();
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
                if (this.autoFarmAutoStopEnabled && this.autoFarmAutoStopAt > 0f && Time.unscaledTime >= this.autoFarmAutoStopAt)
                {
                    this.ToggleAutoFarm();
                    this.AddMenuNotification("Auto Farm auto-stopped (timer)", new Color(1f, 0.75f, 0.45f));
                }
            }
            // AutoFish updates
            try
            {
                this.autoFishLogic?.Update();
                this.autoFishFarm?.Update();
                // Auto-stop check for Auto Fishing Farm (auto-teleport)
                try
                {
                    if (this.autoFishFarm != null && this.autoFishFarm.farmEnabled && this.autoFishFarmAutoStopEnabled && this.autoFishFarmAutoStopAt > 0f && Time.unscaledTime >= this.autoFishFarmAutoStopAt)
                    {
                        this.autoFishFarm.ToggleFarm();
                        this.AddMenuNotification("Auto Fishing Farm auto-stopped (timer)", new Color(1f, 0.75f, 0.45f));
                        this.autoFishFarmAutoStopAt = -1f;
                    }
                }
                catch { }
                // Insect farm periodic update (from separate module)
                try { InsectFarm.Update(this); } catch { }
                // Resource farm periodic update
                try { this.UpdateResourceFarm(); } catch { }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("✗ AutoFish update error: " + ex.Message);
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

            // Radar GUI overlay removed: drawing of on-screen loot markers suppressed.
            // This prevents labels/boxes appearing as an overlay during gameplay.

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

            // Draw optional on-screen status overlay (left-center)
            if (this.showStatusOverlay)
            {
                float ow = 320f;
                float oh = 420f;
                float ox = 20f;
                float oy = (float)Screen.height * 0.5f - oh * 0.5f;
                Rect overlayRect = new Rect(ox, oy, ow, oh);

                // Background card (semi-transparent using theme colors)
                Color prevColor = GUI.color;
                Color bg = new Color(this.uiPanelR, this.uiPanelG, this.uiPanelB, Mathf.Clamp01(this.uiPanelAlpha * 0.85f));
                GUI.color = bg;
                GUI.DrawTexture(overlayRect, Texture2D.whiteTexture);
                GUI.color = prevColor;

                // Inner padding for content
                Rect inner = new Rect(overlayRect.x + 12f, overlayRect.y + 12f, overlayRect.width - 24f, overlayRect.height - 24f);
                this.DrawStatusOverlay(inner);
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
                this.DrawSidebarTabButton("Self", 0);
                this.DrawSidebarTabButton("Resource Gathering", 2);
                this.DrawSidebarTabButton("Features", 3);
                this.DrawSidebarTabButton("Radar", 4);
                this.DrawSidebarTabButton("Teleport", 5);
                this.DrawSidebarTabButton("Items Selector", 6);
                this.DrawSidebarTabButton("Settings", 7);
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
                if (this.selectedTab == 0) calculatedHeight = this.DrawSelfTab(contentY);
                else if (this.selectedTab == 2) calculatedHeight = this.DrawAutoFarmTab(contentY);
                else if (this.selectedTab == 3) calculatedHeight = this.DrawAutomationTab(contentY);
                else if (this.selectedTab == 4) calculatedHeight = this.DrawRadarTab(contentY);
                else if (this.selectedTab == 5) calculatedHeight = this.DrawTeleportTab(contentY);
                else if (this.selectedTab == 6) calculatedHeight = this.DrawBulkSelectorTab(contentY);
                else if (this.selectedTab == 7) calculatedHeight = this.DrawSettingsTab(contentY);
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
                if (this.selfSubTab == 0) return 400f; // Self - Main
                if (this.selfSubTab == 1) return 300f; // Self - Building
                return 400f;
            }
            if (this.selectedTab == 1)
            {
                // Auto Draw: simple default height
                return 740f;
            }
            if (this.selectedTab == 2)
            {
                if (this.autoFarmSubTab == 1) return 780f;
                if (this.autoFarmSubTab == 2) return 780f; // Fish Farm now matches Resource Farm scroll height
                if (this.autoFarmSubTab == 3) return 320f;
                return 1200f;
            }
            if (this.selectedTab == 3) return 760f;
            if (this.selectedTab == 4) return 900f;
            if (this.selectedTab == 5)
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
            if (this.selectedTab == 6) return 780f;
            if (this.selectedTab == 7 && this.settingsSubTab == 2)
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

        // Public wrappers for external UI modules
        public bool UI_DrawPrimaryActionButton(Rect rect, string label)
        {
            return this.DrawPrimaryActionButton(rect, label);
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

        public float UI_DrawAccentSlider(Rect rect, float value, float min, float max)
        {
            return this.DrawAccentSlider(rect, value, min, max);
        }

        public bool UI_DrawSwitchToggle(Rect rect, bool value, string label)
        {
            return this.DrawSwitchToggle(rect, value, label);
        }

        // Public helpers for external modules
        public List<Vector3> GetTrackedInsectPositions()
        {
            List<Vector3> res = new List<Vector3>();
            foreach (KeyValuePair<int, GameObject> kv in this.trackedObjectMarkers)
            {
                if (kv.Value != null)
                {
                    try { res.Add(kv.Value.transform.position); } catch { }
                }
            }
            return res;
        }

        // Allow external modules to request a settings save (autosave helper)
        public void UI_SaveKeybinds(bool showNotification = false)
        {
            try { this.SaveKeybinds(showNotification); } catch { }
        }

        public void TeleportToLocationWithOffset(Vector3 targetPos, float offset)
        {
            GameObject p = GetPlayer();
            Vector3 final = targetPos;
            if (p != null)
            {
                Vector3 forward = p.transform.forward;
                final = targetPos - forward * offset;
            }
            this.TeleportToLocation(final);
        }

        public GameObject GetPlayerObject()
        {
            return GetPlayer();
        }

        // Expose DirectClickInteractButton for external modules
        public void UI_DirectClickInteractButton()
        {
            this.DirectClickInteractButton();
        }

        // Resource repair pause helpers for external modules
        public float GetResourceAutoRepairPauseSeconds()
        {
            return this.resourceAutoRepairPauseSeconds;
        }

        public void SetResourceAutoRepairPauseSeconds(float seconds)
        {
            this.resourceAutoRepairPauseSeconds = seconds;
        }

        public bool IsResourceRepairPaused()
        {
            return Time.time < this.resourceRepairPauseUntil;
        }

        // Public wrappers to allow other modules to trigger repair/eat flows
        public void StartRepairPublic()
        {
            try { this.StartRepair(); } catch { }
        }

        public void StartAutoEatPublic()
        {
            try { this.StartAutoEat(); } catch { }
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
            if (this.selectedTab == 0) return "Self";
            if (this.selectedTab == 2) return "Resource Gathering";
            if (this.selectedTab == 3) return "Features";
            if (this.selectedTab == 4) return "Radar";
            if (this.selectedTab == 5) return "Teleport";
            if (this.selectedTab == 6) return "Items Selector";
            if (this.selectedTab == 7) return "Settings";
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

            GUIStyle none = new GUIStyle(GUI.skin.label);
            none.fontSize = 12;
            none.fontStyle = FontStyle.Italic;
            none.normal.textColor = new Color(this.uiSubTabTextR, this.uiSubTabTextG, this.uiSubTabTextB, 0.55f);

            float x = panelRect.x + 14f;
            float w = panelRect.width - 28f;
            float y = panelRect.y + 46f;
            bool anyActive = false;

            // Helper to draw a feature row
            void Row(string label, string detail)
            {
                GUI.Label(new Rect(x, y, w, 22f), label, title);
                y += 19f;
                GUI.Label(new Rect(x, y, w, 20f), detail, value);
                y += 26f;
                anyActive = true;
            }

            if (this.isRadarActive)
                Row("Radar", "Active");

            if (this.autoFarmActive)
                Row("Auto Farm", "Running");

            if (this.autoCookEnabled)
                Row("Auto Cook", "Running");

            if (this.gameSpeed != 1.0f)
                Row("Speed", $"{this.gameSpeed:F1}x");

            if (this.noclipEnabled)
                Row("Noclip", "Active");

            if (this.bypassOverlapEnabled)
                Row("Bypass Overlap", "Active");

            if (this.birdVacuumEnabled)
                Row("Bird Vacuum", "Active");

            if (this.autoSnowEnabled)
                Row("Auto Snow", "Active");

            if (this.autoJoinFriendEnabled)
                Row("Auto Join Friend", "Active");

            if (InsectFarm.IsEnabled)
                Row("Insect Farm", "Running");

            bool fishFarmRunning = this.autoFishFarm != null && this.autoFishFarm.farmEnabled;
            bool manualFishRunning = this.autoFishLogic != null && this.autoFishLogic.autoFishEnabled;
            if (fishFarmRunning || manualFishRunning)
            {
                GUI.Label(new Rect(x, y, w, 22f), "Fish Farm", title);
                y += 19f;
                if (fishFarmRunning)
                {
                    GUI.Label(new Rect(x, y, w, 20f), this.autoFishFarm.GetFarmStatusString(), value);
                    y += 18f;
                }
                if (manualFishRunning)
                {
                    GUI.Label(new Rect(x, y, w, 20f), this.autoFishLogic.GetStatusString(), value);
                    y += 18f;
                    GUI.Label(new Rect(x, y, w, 20f), "Target: " + this.autoFishLogic.GetTargetInfo(), value);
                    y += 18f;
                    GUI.Label(new Rect(x, y, w, 20f), "Caught: " + this.autoFishLogic.fishCaughtCount.ToString(), value);
                    y += 18f;
                }
                y += 8f;
                anyActive = true;
            }

            if (!anyActive)
                GUI.Label(new Rect(x, y, w, 24f), "No active features", none);
        }

        // Enhanced overlay-specific renderer for better contrast, spacing and shadowed text
        private void DrawStatusOverlay(Rect panelRect)
        {
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = false };
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = false };
            GUIStyle valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            GUIStyle smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };

            headerStyle.normal.textColor = Color.white;
            titleStyle.normal.textColor = Color.white;
            valueStyle.normal.textColor = new Color(0.95f, 0.95f, 0.98f, 0.98f);
            smallStyle.normal.textColor = new Color(0.9f, 0.9f, 0.95f, 0.95f);

            float x = panelRect.x;
            float y = panelRect.y;
            float w = panelRect.width;

            // Column widths (more even columns)
            float leftW = Mathf.Floor(w * 0.48f);
            float gap = 8f;
            float rightW = w - leftW - gap;
            float xLeft = x;
            float xRight = x + leftW + gap;

            // Header centered across the top
            headerStyle.alignment = TextAnchor.MiddleCenter;
            float headerH = headerStyle.CalcHeight(new GUIContent("Status"), w);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.Label(new Rect(x + 1f, y + 1f, w, headerH), "Status Overlay", headerStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, headerH), "Status Overlay", headerStyle);
            y += headerH + 8f;

            // Track vertical position for each column
            float yLeft = y;
            float yRight = y;

            // Helper for left column
            Action<string, string, GUIStyle, GUIStyle> drawLeft = (t, v, tStyle, vStyle) =>
            {
                float th = tStyle.CalcHeight(new GUIContent(t), leftW);
                GUI.color = new Color(0f, 0f, 0f, 0.48f);
                GUI.Label(new Rect(xLeft + 1f, yLeft + 1f, leftW, th), t, tStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(xLeft, yLeft, leftW, th), t, tStyle);
                yLeft += th + 4f;

                float vh = vStyle.CalcHeight(new GUIContent(v), leftW);
                GUI.color = new Color(0f, 0f, 0f, 0.36f);
                GUI.Label(new Rect(xLeft + 1f, yLeft + 1f, leftW, vh), v, vStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(xLeft, yLeft, leftW, vh), v, vStyle);
                yLeft += vh + 8f;
            };

            // Helper for right column (fish farm details)
            Action<string, string, GUIStyle, GUIStyle> drawRight = (t, v, tStyle, vStyle) =>
            {
                float th = tStyle.CalcHeight(new GUIContent(t), rightW);
                GUI.color = new Color(0f, 0f, 0f, 0.48f);
                GUI.Label(new Rect(xRight + 1f, yRight + 1f, rightW, th), t, tStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(xRight, yRight, rightW, th), t, tStyle);
                yRight += th + 4f;

                float vh = vStyle.CalcHeight(new GUIContent(v), rightW);
                GUI.color = new Color(0f, 0f, 0f, 0.36f);
                GUI.Label(new Rect(xRight + 1f, yRight + 1f, rightW, vh), v, vStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(xRight, yRight, rightW, vh), v, vStyle);
                yRight += vh + 8f;
            };

            // Left column content
            drawLeft("Tab", this.GetSelectedTabHeader(), titleStyle, valueStyle);
            drawLeft("Radar", this.isRadarActive ? "Enabled" : "Disabled", titleStyle, valueStyle);
            drawLeft("Auto Farm", this.autoFarmActive ? "Running" : "Idle", titleStyle, valueStyle);
            drawLeft("Auto Cook", this.autoCookEnabled ? "Running" : "Idle", titleStyle, valueStyle);
            drawLeft("Speed", string.Format("{0:F1}x", this.gameSpeed), titleStyle, valueStyle);

            // Vertical separator between left and right columns (centered)
            float sepX = x + w * 0.5f - 1f;
            float sepTop = panelRect.y + headerH + 8f;
            float sepHeight = panelRect.height - headerH - 20f;
            if (sepHeight > 8f)
            {
                GUI.color = new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB, 0.65f);
                GUI.DrawTexture(new Rect(sepX, sepTop, 2f, sepHeight), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // Right column: Fish Farm details (add inner padding so text doesn't touch separator)
            float rightInset = 30f;
            float xRightPad = xRight + rightInset;
            float rightWPad = Mathf.Max(40f, rightW - rightInset - 4f);

            string farmState = (this.autoFishFarm != null) ? (this.autoFishFarm.farmEnabled ? "Running" : "Idle") : "N/A";
            // small wrapper to use padded right column
            Action<string, string, GUIStyle, GUIStyle> drawRightPadded = (t, v, tStyle, vStyle) =>
            {
                float th = tStyle.CalcHeight(new GUIContent(t), rightWPad);
                GUI.color = new Color(0f, 0f, 0f, 0.48f);
                GUI.Label(new Rect(xRightPad + 1f, yRight + 1f, rightWPad, th), t, tStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(xRightPad, yRight, rightWPad, th), t, tStyle);
                yRight += th + 4f;

                float vh = vStyle.CalcHeight(new GUIContent(v), rightWPad);
                GUI.color = new Color(0f, 0f, 0f, 0.36f);
                GUI.Label(new Rect(xRightPad + 1f, yRight + 1f, rightWPad, vh), v, vStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(xRightPad, yRight, rightWPad, vh), v, vStyle);
                yRight += vh + 8f;
            };

            drawRightPadded("Fish Farm", farmState, titleStyle, valueStyle);
            if (this.autoFishFarm != null)
            {
                drawRightPadded("Farm Status", this.autoFishFarm.GetFarmStatusString(), smallStyle, smallStyle);
            }
            if (this.autoFishLogic != null)
            {
                drawRightPadded("Fish Status", this.autoFishLogic.GetStatusString(), smallStyle, smallStyle);
                drawRightPadded("Target", this.autoFishLogic.GetTargetInfo(), smallStyle, smallStyle);
                drawRightPadded("Caught", this.autoFishLogic.fishCaughtCount.ToString(), smallStyle, smallStyle);
            }

            GUI.color = Color.white;
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

        public void UI_AddMenuNotification(string message, Color color, float duration = 5f)
        {
            this.AddMenuNotification(message, color, duration);
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
            bool shouldBlock = this.showMenu && this.blockGameUiWhenMenuOpen;
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
                tabs.Add(("Main", () => this.selfSubTab == 0, () => this.SetSelfSubTab(0)));
                tabs.Add(("Building", () => this.selfSubTab == 1, () => this.SetSelfSubTab(1)));
            }
            else if (this.selectedTab == 2)
            {
                tabs.Add(("Foraging", () => this.autoFarmSubTab == 0, () => this.SetAutoFarmSubTab(0)));
                tabs.Add(("Chop & Mine", () => this.autoFarmSubTab == 1, () => this.SetAutoFarmSubTab(1)));
                tabs.Add(("Fishing", () => this.autoFarmSubTab == 2, () => this.SetAutoFarmSubTab(2)));
                tabs.Add(("Insects", () => this.autoFarmSubTab == 3, () => this.SetAutoFarmSubTab(3)));
                // Auto Draw quick link removed
            }
            else if (this.selectedTab == 3)
            {
                tabs.Add(("Main", () => this.automationSubTab == 0, () => this.SetAutomationSubTab(0)));
                tabs.Add(("Food & Repair", () => this.automationSubTab == 1, () => this.SetAutomationSubTab(1)));
                tabs.Add(("Snow Sculpting", () => this.automationSubTab == 2, () => this.SetAutomationSubTab(2)));
                tabs.Add(("Cat Play", () => this.automationSubTab == 3, () => this.SetAutomationSubTab(3)));
                tabs.Add(("Auto Buy", () => this.automationSubTab == 4, () => this.SetAutomationSubTab(4)));
                tabs.Add(("Auto Cook", () => this.automationSubTab == 5, () => this.SetAutomationSubTab(5)));
            }
            else if (this.selectedTab == 4)
            {
                tabs.Add(("Main", () => this.radarSubTab == 0, () => this.SetRadarSubTab(0)));
                tabs.Add(("Settings", () => this.radarSubTab == 1, () => this.SetRadarSubTab(1)));
            }
            else if (this.selectedTab == 5)
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
            else if (this.selectedTab == 6)
            {
                // No sub-tabs for Items Selector
            }
            else if (this.selectedTab == 7)
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

        private void SetRadarSubTab(int subTab)
        {
            if (this.radarSubTab != subTab)
            {
                this.radarSubTab = subTab;
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

        private void SetSelfSubTab(int subTab)
        {
            if (this.selfSubTab != subTab)
            {
                this.selfSubTab = subTab;
                this.tabScrollPos = Vector2.zero;
            }
        }

        private void EnsureBypassPatched()
        {
            if (this.bypassOverlapPatched) return;

            var t = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("XDT.Physics.PhysicsManager") ?? a.GetType("Il2CppXDT.Physics.PhysicsManager"))
                .FirstOrDefault(x => x != null);

            if (t != null)
            {
                try
                {
                    this.bypassHarmony = new HarmonyLib.Harmony("HeartopiaMod.bypass");
                    var p = new HarmonyMethod(typeof(HeartopiaComplete).GetMethod(nameof(BypassPrefix), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public));
                    string[] methods = { "OverlapBoxNonAlloc", "OverlapSphereNonAlloc" };
                    foreach (var m in t.GetMethods().Where(x => methods.Contains(x.Name))) this.bypassHarmony.Patch(m, p);
                    this.bypassOverlapPatched = true;
                    MelonLoader.MelonLogger.Msg("Bypass overlap patch applied.");
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Msg("Bypass patch failed: " + ex.Message);
                }
            }
        }

        private static bool BypassPrefix(ref int __result)
        {
            if (bypassOverlapEnabledStatic)
            {
                __result = 0;
                return false;
            }
            return true;
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
            if (this.autoFarmSubTab == 2)
            {
                return this.DrawNewSubTab(startY);
            }
            if (this.autoFarmSubTab == 3)
            {
                return InsectFarm.DrawTab(this, startY);
            }

            bool flag = this.AnyRadarLootToggleEnabled();
            string text = this.autoFarmActive ? "DISABLE AUTO FORAGING" : "ENABLE AUTO FORAGING";
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
                    this.autoFarmAutoStopAt = -1f;
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
                num += 8;
                float prevAreaLoad = this.areaLoadDelay;
                this.areaLoadDelay = Mathf.Round(this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.areaLoadDelay, 1f, 10f));
                if (this.areaLoadDelay != prevAreaLoad) { try { this.SaveKeybinds(false); } catch { } }
            num += 30;

            // --- AUTO STOP TIMER ---
            this.autoFarmAutoStopEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.autoFarmAutoStopEnabled, "Auto Stop Timer");
            num += 30;

            if (this.autoFarmAutoStopEnabled)
            {
                GUIStyle timerSmall = new GUIStyle(GUI.skin.label) { fontSize = 12 };

                GUI.Label(new Rect(20f, (float)num, 260f, 18f), "Timer (HH:MM:SS)", timerSmall);
                num += 20;
                GUI.Label(new Rect(20f, (float)num, 45f, 20f), "H", timerSmall);
                this.autoFarmAutoStopHoursInput = GUI.TextField(new Rect(35f, (float)num, 55f, 22f), this.autoFarmAutoStopHoursInput, 2);
                GUI.Label(new Rect(95f, (float)num, 10f, 20f), ":", timerSmall);

                GUI.Label(new Rect(108f, (float)num, 45f, 20f), "M", timerSmall);
                this.autoFarmAutoStopMinutesInput = GUI.TextField(new Rect(123f, (float)num, 55f, 22f), this.autoFarmAutoStopMinutesInput, 2);
                GUI.Label(new Rect(183f, (float)num, 10f, 20f), ":", timerSmall);

                GUI.Label(new Rect(196f, (float)num, 45f, 20f), "S", timerSmall);
                this.autoFarmAutoStopSecondsInput = GUI.TextField(new Rect(211f, (float)num, 55f, 22f), this.autoFarmAutoStopSecondsInput, 2);
                num += 28;

                int parsed;
                if (int.TryParse(this.autoFarmAutoStopHoursInput, out parsed))
                {
                    this.autoFarmAutoStopHours = Mathf.Clamp(parsed, 0, 23);
                    this.autoFarmAutoStopHoursInput = this.autoFarmAutoStopHours.ToString();
                }
                if (int.TryParse(this.autoFarmAutoStopMinutesInput, out parsed))
                {
                    this.autoFarmAutoStopMinutes = Mathf.Clamp(parsed, 0, 59);
                    this.autoFarmAutoStopMinutesInput = this.autoFarmAutoStopMinutes.ToString();
                }
                if (int.TryParse(this.autoFarmAutoStopSecondsInput, out parsed))
                {
                    this.autoFarmAutoStopSeconds = Mathf.Clamp(parsed, 0, 59);
                    this.autoFarmAutoStopSecondsInput = this.autoFarmAutoStopSeconds.ToString();
                }

                int autoStopSeconds = this.GetAutoFarmAutoStopSeconds();
                if (autoStopSeconds <= 0)
                {
                    Color prev = GUI.color;
                    GUI.color = new Color(1f, 0.45f, 0.45f);
                    GUI.Label(new Rect(20f, (float)num, 300f, 20f), "Set at least 1 second to enable auto-stop.", timerSmall);
                    GUI.color = prev;
                    num += 24;
                }
                else
                {
                    GUI.Label(new Rect(20f, (float)num, 320f, 20f), "Auto-stop after: " + this.FormatDurationHms(autoStopSeconds), timerSmall);
                    num += 22;

                    if (this.autoFarmActive && this.autoFarmAutoStopAt > 0f)
                    {
                        int remaining = Mathf.Max(0, Mathf.CeilToInt(this.autoFarmAutoStopAt - Time.unscaledTime));
                        GUI.Label(new Rect(20f, (float)num, 320f, 20f), "Time remaining: " + this.FormatDurationHms(remaining), timerSmall);
                        num += 22;
                    }
                }
            }

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
            leftY += 28;

            GUI.Label(new Rect((float)leftX, (float)leftY, (float)colW, 20f), "Events:");
            leftY += 22;
            this.priorityFiddlehead = this.DrawSwitchToggle(new Rect((float)(leftX + 20), (float)leftY, (float)(colW - 10), 20f), this.priorityFiddlehead, "Fiddlehead");
            leftY += 22;
            this.priorityTallMustard = this.DrawSwitchToggle(new Rect((float)(leftX + 20), (float)leftY, (float)(colW - 10), 20f), this.priorityTallMustard, "Tall Mustard");
            leftY += 22;
            this.priorityBurdock = this.DrawSwitchToggle(new Rect((float)(leftX + 20), (float)leftY, (float)(colW - 10), 20f), this.priorityBurdock, "Burdock");
            leftY += 22;
            this.priorityMustardGreens = this.DrawSwitchToggle(new Rect((float)(leftX + 20), (float)leftY, (float)(colW - 10), 20f), this.priorityMustardGreens, "Mustard Greens");
            leftY += 22;

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
            rightY += 30;

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

            GUI.Label(new Rect(20f, (float)num, 260f, 180f), "Auto Foraging will:\n• Enable Auto Collect\n• Enable x5.0 GameSpeed\n• Teleport to closest node\n• Auto-rotate camera if stuck\n• Respect radar toggles for foraging\n• Skip nodes on cooldown\n• Cycle through locations if nothing is found\n• PRIORITIZE selected loot types first\n• SEEK priority locations FIRST when enabled");
            return (float)num + 190f;
        }

        private float DrawTreeFarmTab(int startY)
        {
            int num = startY;
            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), "Equip Axe"))
            {
                this.StartToolEquipRequest(1);
            }
            num += 45;
            string toggleText = this.autoResourceFarmEnabled ? "DISABLE CHOP & MINE" : "ENABLE CHOP & MINE";
            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 40f), toggleText))
            {
                this.ToggleResourceFarm();
            }
            num += 50;

            GUI.Label(new Rect(20f, (float)num, 320f, 24f), "Status: " + this.GetResourceFarmStatus());
            num += 28;
            GUI.Label(new Rect(20f, (float)num, 320f, 24f), $"Available: {this.GetTotalAvailableResources()}");
            num += 28;
            GUI.Label(new Rect(20f, (float)num, 320f, 24f), $"Markers: {this.resourceMarkerPositions.Count}");
            num += 32;

            // --- AUTO STOP TIMER for Resource Farm (moved above Teleport Cooldown) ---
            this.autoResourceFarmAutoStopEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.autoResourceFarmAutoStopEnabled, "Auto Stop Timer");
            num += 30;

            if (this.autoResourceFarmAutoStopEnabled)
            {
                GUIStyle timerSmall = new GUIStyle(GUI.skin.label) { fontSize = 12 };

                GUI.Label(new Rect(20f, (float)num, 260f, 18f), "Timer (HH:MM:SS)", timerSmall);
                num += 20;

                GUI.Label(new Rect(20f, (float)num, 45f, 20f), "H", timerSmall);
                this.autoResourceFarmAutoStopHoursInput = GUI.TextField(new Rect(35f, (float)num, 55f, 22f), this.autoResourceFarmAutoStopHoursInput, 2);
                GUI.Label(new Rect(95f, (float)num, 10f, 20f), ":", timerSmall);

                GUI.Label(new Rect(108f, (float)num, 45f, 20f), "M", timerSmall);
                this.autoResourceFarmAutoStopMinutesInput = GUI.TextField(new Rect(123f, (float)num, 55f, 22f), this.autoResourceFarmAutoStopMinutesInput, 2);
                GUI.Label(new Rect(183f, (float)num, 10f, 20f), ":", timerSmall);

                GUI.Label(new Rect(196f, (float)num, 45f, 20f), "S", timerSmall);
                this.autoResourceFarmAutoStopSecondsInput = GUI.TextField(new Rect(211f, (float)num, 55f, 22f), this.autoResourceFarmAutoStopSecondsInput, 2);
                num += 28;

                int parsed;
                if (int.TryParse(this.autoResourceFarmAutoStopHoursInput, out parsed))
                {
                    this.autoResourceFarmAutoStopHours = Mathf.Clamp(parsed, 0, 23);
                    this.autoResourceFarmAutoStopHoursInput = this.autoResourceFarmAutoStopHours.ToString();
                }
                if (int.TryParse(this.autoResourceFarmAutoStopMinutesInput, out parsed))
                {
                    this.autoResourceFarmAutoStopMinutes = Mathf.Clamp(parsed, 0, 59);
                    this.autoResourceFarmAutoStopMinutesInput = this.autoResourceFarmAutoStopMinutes.ToString();
                }
                if (int.TryParse(this.autoResourceFarmAutoStopSecondsInput, out parsed))
                {
                    this.autoResourceFarmAutoStopSeconds = Mathf.Clamp(parsed, 0, 59);
                    this.autoResourceFarmAutoStopSecondsInput = this.autoResourceFarmAutoStopSeconds.ToString();
                }

                int asSeconds = this.GetAutoResourceFarmAutoStopSeconds();
                if (asSeconds <= 0)
                {
                    Color prev = GUI.color;
                    GUI.color = new Color(1f, 0.45f, 0.45f);
                    GUI.Label(new Rect(20f, (float)num, 300f, 20f), "Set at least 1 second to enable auto-stop.", timerSmall);
                    GUI.color = prev;
                    num += 24;
                }
                else
                {
                    GUI.Label(new Rect(20f, (float)num, 320f, 20f), "Auto-stop after: " + this.FormatDurationHms(asSeconds), timerSmall);
                    num += 22;

                    if (this.autoResourceFarmEnabled && this.autoResourceFarmAutoStopAt > 0f)
                    {
                        int remaining = Mathf.Max(0, Mathf.CeilToInt(this.autoResourceFarmAutoStopAt - Time.unscaledTime));
                        GUI.Label(new Rect(20f, (float)num, 320f, 20f), "Time remaining: " + this.FormatDurationHms(remaining), timerSmall);
                        num += 22;
                    }
                }
            }

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Teleport Cooldown: {this.resourceTeleportCooldown:F1}s");
            num += 22;
            float prevResourceTp = this.resourceTeleportCooldown;
            this.resourceTeleportCooldown = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.resourceTeleportCooldown, 0f, 10f);
            if (Math.Abs(this.resourceTeleportCooldown - prevResourceTp) > 0.0001f) { try { this.SaveKeybinds(false); } catch { } }
            num += 30;

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Click Duration: {this.resourceClickDuration:F1}s");
            num += 22;
            float prevResourceClick = this.resourceClickDuration;
            this.resourceClickDuration = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.resourceClickDuration, 0.1f, 5f);
            if (Math.Abs(this.resourceClickDuration - prevResourceClick) > 0.0001f) { try { this.SaveKeybinds(false); } catch { } }
            num += 30;
            // Auto Repair pause slider: how long to pause teleports after a repair toast
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Auto-Repair Tool (Paused TP FARM): {this.resourceAutoRepairPauseSeconds:F0}s");
            num += 22;
            float prevResourcePause = this.resourceAutoRepairPauseSeconds;
            this.resourceAutoRepairPauseSeconds = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.resourceAutoRepairPauseSeconds, 0f, 60f);
            if (Math.Abs(this.resourceAutoRepairPauseSeconds - prevResourcePause) > 0.0001f) { try { this.SaveKeybinds(false); } catch { } }
            num += 30;

            

            GUI.Label(new Rect(20f, (float)num, 200f, 20f), "Farm Rocks");
            this.farmRocks = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.farmRocks, "Farm Rocks");
            num += 25;
            this.farmOres = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.farmOres, "Farm Ores");
            num += 25;
            this.farmTrees = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.farmTrees, "Farm Trees");
            num += 25;
            this.farmRareTrees = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.farmRareTrees, "Farm Rare Trees");
            num += 25;
            this.farmAppleTrees = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.farmAppleTrees, "Farm Apple Trees");
            num += 25;
            this.farmOrangeTrees = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.farmOrangeTrees, "Farm Mandarin Trees");
            num += 28;

            if (this.DrawDangerActionButton(new Rect(20f, (float)num, 260f, 35f), "Reset Cooldowns"))
            {
                this.ResetAllCooldowns();
            }
            num += 45;
            

            GUI.Label(new Rect(20f, (float)num, 360f, 120f), "Chop & Mine flow:\n• Build list of available markers\n• Shuffle and teleport to markers\n• Simulate F key for configured duration\n• Mark resource collected and set cooldowns");
            return (float)num + 120f;
        }

        private float DrawNewSubTab(int startY)
        {
            int num = startY;

            // Simple, consistent layout: Farm toggle, sliders, Manual toggle
            GUIStyle small = new GUIStyle(GUI.skin.label) { fontSize = 12 };

            // Equip Rod button
            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), "Equip Rod"))
            {
                this.StartToolEquipRequest(3);
            }
            num += 45;

            // Teleport toggle
            bool isAutoFishingEnabled = (this.autoFishLogic != null && this.autoFishLogic.autoFishEnabled) || (this.autoFishFarm != null && this.autoFishFarm.farmEnabled);
            GUI.enabled = !isAutoFishingEnabled;
            this.autoFishTeleportEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.autoFishTeleportEnabled, "Teleport Fishing");
            GUI.enabled = true;
            num += 30;

            // Single enable button
            string buttonText = isAutoFishingEnabled ? "Disable Auto Fishing" : "Enable Auto Fishing";
            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), buttonText))
            {
                if (isAutoFishingEnabled)
                {
                    // Disable both
                    if (this.autoFishLogic != null) this.autoFishLogic.ToggleAutoFish();
                    if (this.autoFishFarm != null) this.autoFishFarm.ToggleFarm();
                    this.showFishShadowRadar = false;
                }
                else
                {
                    // Enable based on teleport setting
                    if (this.autoFishTeleportEnabled)
                    {
                        if (this.autoFishFarm != null) this.autoFishFarm.ToggleFarm();
                    }
                    else
                    {
                        if (this.autoFishLogic != null) this.autoFishLogic.ToggleAutoFish();
                    }
                    this.showFishShadowRadar = true;
                    this.isRadarActive = true;
                    this.RunRadar();
                }
            }
            num += 45;

            // Settings (shown if auto fishing is enabled or subsystems available)
            if (isAutoFishingEnabled || this.autoFishFarm != null || this.autoFishLogic != null)
            {
                // Auto-stop timer UI (for teleport mode)
                if (this.autoFishTeleportEnabled && this.autoFishFarm != null)
                {
                    this.autoFishFarmAutoStopEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.autoFishFarmAutoStopEnabled, "Auto Stop Timer");
                    num += 30;
                    if (this.autoFishFarmAutoStopEnabled)
                    {
                        GUIStyle timerSmall = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                        GUI.Label(new Rect(20f, (float)num, 260f, 18f), "Timer (HH:MM:SS)", timerSmall);
                        num += 20;
                        GUI.Label(new Rect(20f, (float)num, 45f, 20f), "H", timerSmall);
                        this.autoFishFarmAutoStopHoursInput = GUI.TextField(new Rect(35f, (float)num, 55f, 22f), this.autoFishFarmAutoStopHoursInput, 2);
                        GUI.Label(new Rect(95f, (float)num, 10f, 20f), ":", timerSmall);
                        GUI.Label(new Rect(108f, (float)num, 45f, 20f), "M", timerSmall);
                        this.autoFishFarmAutoStopMinutesInput = GUI.TextField(new Rect(123f, (float)num, 55f, 22f), this.autoFishFarmAutoStopMinutesInput, 2);
                        GUI.Label(new Rect(183f, (float)num, 10f, 20f), ":", timerSmall);
                        GUI.Label(new Rect(196f, (float)num, 45f, 20f), "S", timerSmall);
                        this.autoFishFarmAutoStopSecondsInput = GUI.TextField(new Rect(211f, (float)num, 55f, 22f), this.autoFishFarmAutoStopSecondsInput, 2);
                        num += 28;
                        int parsedF;
                        if (int.TryParse(this.autoFishFarmAutoStopHoursInput, out parsedF)) { this.autoFishFarmAutoStopHours = Mathf.Clamp(parsedF, 0, 23); this.autoFishFarmAutoStopHoursInput = this.autoFishFarmAutoStopHours.ToString(); }
                        if (int.TryParse(this.autoFishFarmAutoStopMinutesInput, out parsedF)) { this.autoFishFarmAutoStopMinutes = Mathf.Clamp(parsedF, 0, 59); this.autoFishFarmAutoStopMinutesInput = this.autoFishFarmAutoStopMinutes.ToString(); }
                        if (int.TryParse(this.autoFishFarmAutoStopSecondsInput, out parsedF)) { this.autoFishFarmAutoStopSeconds = Mathf.Clamp(parsedF, 0, 59); this.autoFishFarmAutoStopSecondsInput = this.autoFishFarmAutoStopSeconds.ToString(); }
                        int afSecs = this.GetAutoFishFarmAutoStopSeconds();
                        if (afSecs <= 0) { Color prev = GUI.color; GUI.color = new Color(1f, 0.45f, 0.45f); GUI.Label(new Rect(20f, (float)num, 300f, 20f), "Set at least 1 second to enable auto-stop.", timerSmall); GUI.color = prev; num += 24; }
                        else { GUI.Label(new Rect(20f, (float)num, 320f, 20f), "Auto-stop after: " + this.FormatDurationHms(afSecs), timerSmall); num += 22; if (this.autoFishFarm.farmEnabled && this.autoFishFarmAutoStopAt > 0f) { int remaining = Mathf.Max(0, Mathf.CeilToInt(this.autoFishFarmAutoStopAt - Time.unscaledTime)); GUI.Label(new Rect(20f, (float)num, 320f, 20f), "Time remaining: " + this.FormatDurationHms(remaining), timerSmall); num += 22; } }
                    }

                    // Sliders for farm settings
                    GUI.Label(new Rect(20f, (float)num, 120f, 18f), "Scan Timeout", small);
                    GUI.Label(new Rect(150f, (float)num, 120f, 18f), this.autoFishFarm.scanTimeout.ToString("F0") + "s", small);
                    num += 18;
                    float prevFishScan = this.autoFishFarm.scanTimeout;
                    this.autoFishFarm.scanTimeout = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 18f), this.autoFishFarm.scanTimeout, 3f, 20f);
                    if (Math.Abs(this.autoFishFarm.scanTimeout - prevFishScan) > 0.001f) { try { this.SaveKeybinds(false); } catch { } }
                    num += 26;

                    GUI.Label(new Rect(20f, (float)num, 120f, 18f), "Teleport Delay", small);
                    GUI.Label(new Rect(150f, (float)num, 120f, 18f), this.autoFishFarm.teleportDelay.ToString("F1") + "s", small);
                    num += 18;
                    float prevFishTp = this.autoFishFarm.teleportDelay;
                    this.autoFishFarm.teleportDelay = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 18f), this.autoFishFarm.teleportDelay, 1f, 5f);
                    if (Math.Abs(this.autoFishFarm.teleportDelay - prevFishTp) > 0.001f) { try { this.SaveKeybinds(false); } catch { } }
                    num += 28;
                }
                else if (!this.autoFishTeleportEnabled && this.autoFishLogic != null)
                {
                    // Settings for standing fishing
                    GUI.Label(new Rect(20f, (float)num, 150f, 18f), "Fish Detect Range", small);
                    GUI.Label(new Rect(170f, (float)num, 110f, 18f), this.autoFishLogic.fishShadowDetectRange.ToString("F1") + "m", small);
                    num += 18;
                    float prevFishRange = this.autoFishLogic.fishShadowDetectRange;
                    this.autoFishLogic.fishShadowDetectRange = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 18f), this.autoFishLogic.fishShadowDetectRange, 1f, 20f);
                    if (Math.Abs(this.autoFishLogic.fishShadowDetectRange - prevFishRange) > 0.001f) { try { this.SaveKeybinds(false); } catch { } }

                    num += 26;
                    GUI.Label(new Rect(20f, (float)num, 150f, 18f), "Max Reel", small);
                    GUI.Label(new Rect(170f, (float)num, 110f, 18f), this.autoFishLogic.reelMaxDuration.ToString("F0") + "s", small);
                    num += 18;
                    this.autoFishLogic.reelMaxDuration = Mathf.Round(this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 18f), this.autoFishLogic.reelMaxDuration, 10f, 120f));

                    num += 26;
                    GUI.Label(new Rect(20f, (float)num, 120f, 18f), "Hold Time", small);
                    GUI.Label(new Rect(120f, (float)num, 60f, 18f), this.autoFishLogic.reelHoldDuration.ToString("F1") + "s", small);
                    GUI.Label(new Rect(185f, (float)num, 95f, 18f), "Pause", small);
                    GUI.Label(new Rect(255f, (float)num, 60f, 18f), this.autoFishLogic.reelPauseDuration.ToString("F1") + "s", small);
                    num += 18;
                    float prevReelHold = this.autoFishLogic.reelHoldDuration;
                    float prevReelPause = this.autoFishLogic.reelPauseDuration;
                    this.autoFishLogic.reelHoldDuration = this.DrawAccentSlider(new Rect(20f, (float)num, 125f, 18f), this.autoFishLogic.reelHoldDuration, 1f, 8f);
                    this.autoFishLogic.reelPauseDuration = this.DrawAccentSlider(new Rect(155f, (float)num, 125f, 18f), this.autoFishLogic.reelPauseDuration, 0.2f, 2f);
                    if (Math.Abs(this.autoFishLogic.reelHoldDuration - prevReelHold) > 0.001f || Math.Abs(this.autoFishLogic.reelPauseDuration - prevReelPause) > 0.001f) { try { this.SaveKeybinds(false); } catch { } }
                    num += 26;
                }
            }

            return (float)num + 20f;
        }

        // Auto Draw tab removed

        // Insect farm UI moved to InsectFarm.cs

        // Token: 0x06000009 RID: 9 RVA: 0x00002E24 File Offset: 0x00001024
        private float DrawAutomationTab(int startY)
        {
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
                    num += 26;
                    this.collectEventResources = this.DrawSwitchToggle(new Rect(40f, (float)num, 240f, 20f), this.collectEventResources, "  Event Resources");
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
                float prevGameSpeed = this.gameSpeed;
                this.gameSpeed = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.gameSpeed, 1f, 10f);
                if (Math.Abs(this.gameSpeed - prevGameSpeed) > 0.0001f) { try { this.SaveKeybinds(false); } catch { } }

                num += 30;
                bool prevCustomCameraFOVEnabled = this.customCameraFOVEnabled;
                this.customCameraFOVEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.customCameraFOVEnabled, "Custom Camera FOV");
                if (this.customCameraFOVEnabled != prevCustomCameraFOVEnabled)
                {
                    if (this.customCameraFOVEnabled)
                    {
                        this.ApplyCameraFOV();
                    }
                    else
                    {
                        this.RestoreCameraFOV();
                    }
                    try { this.SaveKeybinds(false); } catch { }
                }
                num += 30;
                Rect rectFOV = new Rect(20f, (float)num, 260f, 20f);
                GUI.Label(rectFOV, string.Format("Camera FOV: {0:F0}°", this.cameraFOV));
                num += 22;
                float newFOV = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.cameraFOV, 30f, 120f);
                if (newFOV != this.cameraFOV)
                {
                    this.cameraFOV = newFOV;
                    if (this.customCameraFOVEnabled)
                    {
                        this.ApplyCameraFOV();
                    }
                    try { this.SaveKeybinds(false); } catch { }
                }
                num += 40;
                
                bool flag2 = this.DrawDangerActionButton(new Rect(20f, (float)num, 260f, 35f), "DISABLE ALL");
                if (flag2)
                {
                    this.autoFarmEnabled = false;
                    this.bypassEnabled = false;
                    this.birdVacuumEnabled = false;
                    this.antiAfkEnabled = false;
                    AutoFishLogic.SimulateMouseButton0 = false;
                    AutoFishLogic.SimulateMouseButton0Down = false;
                    this.antiAfkMouseDownClearAt = 0f;
                    this.antiAfkMouseHoldClearAt = 0f;
                    this.StopAutoCookInternal("Disabled");
                    this.pendingToolEquipType = 0;
                    this.isAutoEating = false;
                    this.StopTreeFarm("Stopped");
                    this.cookingCleanupMode = false;
                    this.gameSpeed = 1f;
                    this.customCameraFOVEnabled = false;
                    this.cameraFOV = 60f;
                    this.noclipEnabled = false;
                    HeartopiaComplete.OverridePlayerPosition = false;
                    this.noclipBoostMultiplier = 2f;
                    this.RestoreCameraFOV();
                }
                num += 45;
                return (float)num + 170f;
            }

            if (this.automationSubTab == 1)
            {
                GUI.Label(new Rect(20f, (float)num, 260f, 24f), "Bag Automation");
                num += 35;

                // Refresh Button
                bool flagRefresh = this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 35f), "REFRESH & SCAN");
                if (flagRefresh)
                {
                    this.RefreshBulkSelectorCache();
                }
                num += 40;

                // Status section
                string repairStatus = this.isRepairing ? $"Running (step {this.repairStep})" : "Idle";
                string autoEatStatus = this.isAutoEating ? $"Running (step {this.autoEatStep}) - {this.autoEatFoodOptions[this.autoEatFoodType]}" : "Idle";

                GUI.Label(new Rect(20f, (float)num, 160f, 24f), "Repair Status: " + repairStatus);
                GUI.Label(new Rect(180f, (float)num, 160f, 24f), "Eat Status: " + autoEatStatus);
                num += 35;

                // Action buttons
                if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 120f, 35f), "Auto Repair"))
                {
                    if (!this.isRepairing && !this.isAutoEating)
                    {
                        MelonLogger.Msg("[AutoRepair] UI button requested StartRepair");
                        this.StartRepair();
                        this.AddMenuNotification("Auto Repair started", new Color(0.45f, 1f, 0.55f));
                    }
                    else
                    {
                        this.AddMenuNotification("Bag automation already running", new Color(1f, 0.85f, 0.35f));
                    }
                }

                if (GUI.Button(new Rect(160f, (float)num, 125f, 35f), "Eat Selected Food", this.themePrimaryButtonStyle))
                {
                    if (!this.isRepairing && !this.isAutoEating)
                    {
                        this.StartAutoEat();
                        this.AddMenuNotification($"Auto Eat started ({this.autoEatFoodOptions[this.autoEatFoodType]})", new Color(0.45f, 1f, 0.55f));
                    }
                    else
                    {
                        this.AddMenuNotification("Auto Eat already running", new Color(1f, 0.55f, 0.55f));
                    }
                }
                num += 45;
                // Max eat attempts slider
                GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Max Eat Attempts: {this.maxAutoEatAttempts}");
                num += 22;
                int newMaxEat = Mathf.RoundToInt(this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), (float)this.maxAutoEatAttempts, 1f, 10f));
                if (newMaxEat != this.maxAutoEatAttempts)
                {
                    this.maxAutoEatAttempts = newMaxEat;
                    this.SaveKeybinds(false);
                }
                num += 26;

                bool newRepairTeleportBackEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.repairTeleportBackEnabled, "Repair Teleport Backward");
                if (newRepairTeleportBackEnabled != this.repairTeleportBackEnabled)
                {
                    this.repairTeleportBackEnabled = newRepairTeleportBackEnabled;
                    this.SaveKeybinds(false);
                }
                num += 30;

                // Configuration section
                GUIStyle bagFieldLabelStyle = new GUIStyle(GUI.skin.label);
                bagFieldLabelStyle.fontSize = 13;
                bagFieldLabelStyle.fontStyle = FontStyle.Bold;
                bagFieldLabelStyle.normal.textColor = new Color(this.uiTextR, this.uiTextG, this.uiTextB);

                GUIStyle dropdownValueStyle = new GUIStyle(GUI.skin.label);
                dropdownValueStyle.fontSize = 13;
                dropdownValueStyle.fontStyle = FontStyle.Bold;
                dropdownValueStyle.alignment = TextAnchor.MiddleLeft;
                dropdownValueStyle.normal.textColor = Color.white;

                GUIStyle dropdownArrowStyle = new GUIStyle(GUI.skin.label);
                dropdownArrowStyle.fontSize = 12;
                dropdownArrowStyle.fontStyle = FontStyle.Bold;
                dropdownArrowStyle.alignment = TextAnchor.MiddleCenter;
                dropdownArrowStyle.normal.textColor = new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB);

                GUIStyle dropdownOptionStyle = new GUIStyle(GUI.skin.label);
                dropdownOptionStyle.fontSize = 13;
                dropdownOptionStyle.fontStyle = FontStyle.Bold;
                dropdownOptionStyle.alignment = TextAnchor.MiddleCenter;
                dropdownOptionStyle.normal.textColor = new Color(this.uiTextR, this.uiTextG, this.uiTextB);

                GUIStyle dropdownOptionActiveStyle = new GUIStyle(dropdownOptionStyle);
                dropdownOptionActiveStyle.normal.textColor = Color.white;

                float fieldLabelX = 20f;
                float fieldLabelWidth = 78f;
                float fieldX = 110f;
                float fieldWidth = 160f;
                float fieldHeight = 28f;
                float rowHeight = 36f;
                float repairPanelHeight = this.autoRepairDropdownOpen ? (this.autoRepairOptions.Length * 30f + 8f + 6f) : 0f;
                float repairRowY = (float)num;
                float foodRowY = repairRowY + rowHeight + repairPanelHeight;

                Rect repairDropdownRect = new Rect(fieldX, repairRowY, fieldWidth, fieldHeight);
                Rect foodDropdownRect = new Rect(fieldX, foodRowY, fieldWidth, fieldHeight);

                GUI.Label(new Rect(fieldLabelX, repairRowY + 3f, fieldLabelWidth, 22f), "Repair Kit", bagFieldLabelStyle);
                GUI.Label(new Rect(fieldLabelX, foodRowY + 3f, fieldLabelWidth, 22f), "Food Type", bagFieldLabelStyle);

                GUI.Box(repairDropdownRect, "", this.themeTopTabStyle ?? this.themePanelStyle ?? GUI.skin.box);
                this.DrawCardOutline(repairDropdownRect, 1f);
                if (GUI.Button(repairDropdownRect, "", GUIStyle.none))
                {
                    this.autoRepairDropdownOpen = !this.autoRepairDropdownOpen;
                    if (this.autoRepairDropdownOpen)
                    {
                        this.autoEatFoodDropdownOpen = false;
                    }
                }
                GUI.Label(new Rect(repairDropdownRect.x + 12f, repairDropdownRect.y + 1f, repairDropdownRect.width - 34f, repairDropdownRect.height - 2f), this.autoRepairOptions[this.autoRepairType], dropdownValueStyle);
                GUI.Label(new Rect(repairDropdownRect.xMax - 24f, repairDropdownRect.y + 1f, 16f, repairDropdownRect.height - 2f), this.autoRepairDropdownOpen ? "▲" : "▼", dropdownArrowStyle);

                GUI.Box(foodDropdownRect, "", this.themeTopTabStyle ?? this.themePanelStyle ?? GUI.skin.box);
                this.DrawCardOutline(foodDropdownRect, 1f);
                if (GUI.Button(foodDropdownRect, "", GUIStyle.none))
                {
                    this.autoEatFoodDropdownOpen = !this.autoEatFoodDropdownOpen;
                    if (this.autoEatFoodDropdownOpen)
                    {
                        this.autoRepairDropdownOpen = false;
                    }
                }
                GUI.Label(new Rect(foodDropdownRect.x + 12f, foodDropdownRect.y + 1f, foodDropdownRect.width - 34f, foodDropdownRect.height - 2f), this.autoEatFoodOptions[this.autoEatFoodType], dropdownValueStyle);
                GUI.Label(new Rect(foodDropdownRect.xMax - 24f, foodDropdownRect.y + 1f, 16f, foodDropdownRect.height - 2f), this.autoEatFoodDropdownOpen ? "▲" : "▼", dropdownArrowStyle);

                if (this.autoRepairDropdownOpen)
                {
                    float panelHeight = this.autoRepairOptions.Length * 30f + 8f;
                    Rect panelRect = new Rect(repairDropdownRect.x, repairDropdownRect.yMax + 4f, repairDropdownRect.width, panelHeight);
                    GUI.Box(panelRect, "", this.themeContentStyle ?? this.themePanelStyle ?? GUI.skin.box);
                    this.DrawCardOutline(panelRect, 1f);

                    for (int i = 0; i < this.autoRepairOptions.Length; i++)
                    {
                        Rect optionRect = new Rect(panelRect.x + 4f, panelRect.y + 4f + i * 30f, panelRect.width - 8f, 26f);
                        bool isSelected = i == this.autoRepairType;
                        GUI.Box(optionRect, "", isSelected ? (this.themeTopTabActiveStyle ?? this.themePrimaryButtonStyle ?? GUI.skin.box) : (this.themeTopTabStyle ?? this.themePanelStyle ?? GUI.skin.box));
                        if (GUI.Button(optionRect, "", GUIStyle.none))
                        {
                            this.autoRepairType = i;
                            this.autoRepairDropdownOpen = false;
                            this.SaveKeybinds(false);
                        }
                        GUI.Label(optionRect, this.autoRepairOptions[i], isSelected ? dropdownOptionActiveStyle : dropdownOptionStyle);
                    }
                }

                if (this.autoEatFoodDropdownOpen)
                {
                    float panelHeight = this.autoEatFoodOptions.Length * 30f + 8f;
                    Rect panelRect = new Rect(foodDropdownRect.x, foodDropdownRect.yMax + 4f, foodDropdownRect.width, panelHeight);
                    GUI.Box(panelRect, "", this.themeContentStyle ?? this.themePanelStyle ?? GUI.skin.box);
                    this.DrawCardOutline(panelRect, 1f);

                    for (int i = 0; i < this.autoEatFoodOptions.Length; i++)
                    {
                        Rect optionRect = new Rect(panelRect.x + 4f, panelRect.y + 4f + i * 30f, panelRect.width - 8f, 26f);
                        bool isSelected = i == this.autoEatFoodType;
                        GUI.Box(optionRect, "", isSelected ? (this.themeTopTabActiveStyle ?? this.themePrimaryButtonStyle ?? GUI.skin.box) : (this.themeTopTabStyle ?? this.themePanelStyle ?? GUI.skin.box));
                        if (GUI.Button(optionRect, "", GUIStyle.none))
                        {
                            this.autoEatFoodType = i;
                            this.autoEatFoodDropdownOpen = false;
                            this.SaveKeybinds(false);
                        }
                        GUI.Label(optionRect, this.autoEatFoodOptions[i], isSelected ? dropdownOptionActiveStyle : dropdownOptionStyle);
                    }
                }

                num = Mathf.RoundToInt(foodDropdownRect.y + fieldHeight + 10f);
                if (this.autoEatFoodDropdownOpen)
                {
                    num += Mathf.RoundToInt(this.autoEatFoodOptions.Length * 30f + 8f + 6f);
                }

                // Description
                string foodDescription = this.autoEatFoodType == 5 ? "any food item" : this.autoEatFoodOptions[this.autoEatFoodType];
                string repairDescription = this.autoRepairType == 2 ? "any repair kit" : this.autoRepairOptions[this.autoRepairType];
                GUI.Label(new Rect(20f, (float)num, 340f, 48f), $"Auto Repair: open bag → find {repairDescription} → Use → close bag\nAuto Eat: open bag → find {foodDescription} → Use → close bag");
                return (float)num + 60f;
            }

            if (this.automationSubTab == 2)
            {
                float left = 20f;
                GUI.Label(new Rect(left, (float)num, 360f, 30f), "AUTO SNOW SCULPTURE");
                num += 40;
                bool prevAutoSnow = this.autoSnowEnabled;
                this.autoSnowEnabled = this.DrawSwitchToggle(new Rect(left, (float)num, 360f, 30f), this.autoSnowEnabled, "❄️ Auto Snow Sculpture");
                if (this.autoSnowEnabled != prevAutoSnow)
                {
                    this.AddMenuNotification($"Auto Snow Sculpture {(this.autoSnowEnabled ? "Enabled" : "Disabled")}", this.autoSnowEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                }
                num += 40;
                if (this.autoSnowEnabled)
                {
                    GUI.Box(new Rect(left, (float)num, 520f, 80f), "");
                    GUIStyle header = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = 12 };
                    GUI.Label(new Rect(left + 10f, (float)num + 8f, 260f, 22f), $"Click Count: {this.snowClickCount}", header);
                    GUI.Label(new Rect(left + 10f, (float)num + 30f, 260f, 22f), $"Queue: {this.snowWidgetQueue.Count}", header);
                    num += 100;
                }
                else
                {
                    num += 20;
                }

                GUI.Label(new Rect(left, (float)num, 360f, 20f), $"Click Interval: {(this.snowClickInterval * 1000f):F0}ms");
                num += 22;
                float prevSnow = this.snowClickInterval;
                this.snowClickInterval = this.UI_DrawAccentSlider(new Rect(left, (float)num, 360f, 20f), this.snowClickInterval, 0.01f, 0.1f);
                if (Math.Abs(this.snowClickInterval - prevSnow) > 0.000001f) { try { this.SaveKeybinds(false); } catch { } }
                num += 34;

                bool prevRapid = this.autoSculptureIconRapidEnabled;
                this.autoSculptureIconRapidEnabled = this.DrawSwitchToggle(new Rect(left, (float)num, 360f, 26f), this.autoSculptureIconRapidEnabled, "Auto Click Icon");
                if (this.autoSculptureIconRapidEnabled != prevRapid)
                {
                    this.AddMenuNotification($"Auto Click Icon {(this.autoSculptureIconRapidEnabled ? "Enabled" : "Disabled")}", this.autoSculptureIconRapidEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                    if (!this.autoSculptureIconRapidEnabled) this.lastSculptIconClickTime = 0f;
                }
                num += 30;
                GUI.Label(new Rect(left, (float)num, 360f, 20f), $"Interval: {(this.sculptIconClickInterval * 1000f):F0}ms");
                num += 22;
                float prevSculpt = this.sculptIconClickInterval;
                this.sculptIconClickInterval = this.UI_DrawAccentSlider(new Rect(left, (float)num, 360f, 20f), this.sculptIconClickInterval, 0.01f, 0.5f);
                if (Math.Abs(this.sculptIconClickInterval - prevSculpt) > 0.000001f) { try { this.SaveKeybinds(false); } catch { } }
                num += 30;

                

                return (float)num;
            }

            if (this.automationSubTab == 3)
            {
                float left = 20f;
                GUI.Label(new Rect(left, (float)num, 360f, 30f), "CAT PLAY (Auto Answer)");
                num += 40;
                bool prevAutoCat = this.autoCatPlayEnabled;
                this.autoCatPlayEnabled = this.DrawSwitchToggle(new Rect(left, (float)num, 360f, 30f), this.autoCatPlayEnabled, "🐱 Auto Cat Play");
                if (this.autoCatPlayEnabled != prevAutoCat)
                {
                    this.AddMenuNotification($"Auto Cat Play {(this.autoCatPlayEnabled ? "Enabled" : "Disabled")}", this.autoCatPlayEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                }
                num += 40;
                if (this.autoCatPlayEnabled)
                {
                    GUI.Box(new Rect(left, (float)num, 520f, 50f), "");
                    GUIStyle header = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = 12 };
                    GUI.Label(new Rect(left + 10f, (float)num + 8f, 260f, 22f), $"Click Count: {this.catClickCount}", header);
                    num += 70;
                }
                else
                {
                    num += 20;
                }

                GUI.Label(new Rect(left, (float)num, 360f, 20f), $"Click Interval: {(this.catClickInterval * 1000f):F0}ms");
                num += 22;
                float prevCat = this.catClickInterval;
                this.catClickInterval = this.UI_DrawAccentSlider(new Rect(left, (float)num, 360f, 20f), this.catClickInterval, 0.02f, 0.5f);
                if (Math.Abs(this.catClickInterval - prevCat) > 0.000001f) { try { this.SaveKeybinds(false); } catch { } }
                num += 30;

                return (float)num;
            }

            if (this.automationSubTab == 4)
            {
                float left = 20f;
                GUI.Label(new Rect(left, (float)num, 360f, 30f), "AUTO BUY (Cooking Store)");
                num += 40;
                bool prevAuto = this.autoBuyEnabled;
                this.autoBuyEnabled = this.DrawSwitchToggle(new Rect(left, (float)num, 360f, 30f), this.autoBuyEnabled, "Auto Buy: Teleport → Buy → Return");
                if (this.autoBuyEnabled != prevAuto)
                {
                    if (this.autoBuyEnabled) { this.StartAutoBuy(); }
                    else { this.StopAutoBuy("Disabled"); }
                }
                num += 40;
                if (this.autoBuyEnabled)
                {
                    GUI.Box(new Rect(left, (float)num, 520f, 80f), "");
                    GUIStyle header = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = 12 };
                    GUI.Label(new Rect(left + 10f, (float)num + 8f, 260f, 22f), $"State: {this.autoBuySubState}", header);
                    GUI.Label(new Rect(left + 10f, (float)num + 30f, 260f, 22f), $"Current Ingredient: {this.autoBuyCurrentIngredientIndex}", header);
                    num += 100;
                }
                else
                {
                    num += 20;
                }

                GUI.Label(new Rect(left, (float)num, 360f, 20f), $"Max per ingredient: {this.autoBuyMaxPerIngredient}");
                num += 22;
                int prevMax = this.autoBuyMaxPerIngredient;
                this.autoBuyMaxPerIngredient = Mathf.RoundToInt(this.UI_DrawAccentSlider(new Rect(left, (float)num, 360f, 20f), (float)this.autoBuyMaxPerIngredient, 1f, 50f));
                if (this.autoBuyMaxPerIngredient != prevMax) { try { this.SaveKeybinds(false); } catch { } }
                num += 30;

                return (float)num;
            }

            if (this.automationSubTab == 5)
            {
                return this.DrawAutoCookTab(startY);
            }

            return (float)num;
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

            // --- AUTO STOP TIMER ---
            this.autoCookAutoStopEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.autoCookAutoStopEnabled, "Auto Stop Timer");
            num += 30;

            if (this.autoCookAutoStopEnabled)
            {
                GUIStyle timerSmall = new GUIStyle(GUI.skin.label) { fontSize = 12 };

                GUI.Label(new Rect(20f, (float)num, 260f, 18f), "Timer (HH:MM:SS)", timerSmall);
                num += 20;

                GUI.Label(new Rect(20f, (float)num, 45f, 20f), "H", timerSmall);
                this.autoCookAutoStopHoursInput = GUI.TextField(new Rect(35f, (float)num, 55f, 22f), this.autoCookAutoStopHoursInput, 2);
                GUI.Label(new Rect(95f, (float)num, 10f, 20f), ":", timerSmall);

                GUI.Label(new Rect(108f, (float)num, 45f, 20f), "M", timerSmall);
                this.autoCookAutoStopMinutesInput = GUI.TextField(new Rect(123f, (float)num, 55f, 22f), this.autoCookAutoStopMinutesInput, 2);
                GUI.Label(new Rect(183f, (float)num, 10f, 20f), ":", timerSmall);

                GUI.Label(new Rect(196f, (float)num, 45f, 20f), "S", timerSmall);
                this.autoCookAutoStopSecondsInput = GUI.TextField(new Rect(211f, (float)num, 55f, 22f), this.autoCookAutoStopSecondsInput, 2);
                num += 28;

                int parsed;
                if (int.TryParse(this.autoCookAutoStopHoursInput, out parsed))
                {
                    this.autoCookAutoStopHours = Mathf.Clamp(parsed, 0, 23);
                    this.autoCookAutoStopHoursInput = this.autoCookAutoStopHours.ToString();
                }
                if (int.TryParse(this.autoCookAutoStopMinutesInput, out parsed))
                {
                    this.autoCookAutoStopMinutes = Mathf.Clamp(parsed, 0, 59);
                    this.autoCookAutoStopMinutesInput = this.autoCookAutoStopMinutes.ToString();
                }
                if (int.TryParse(this.autoCookAutoStopSecondsInput, out parsed))
                {
                    this.autoCookAutoStopSeconds = Mathf.Clamp(parsed, 0, 59);
                    this.autoCookAutoStopSecondsInput = this.autoCookAutoStopSeconds.ToString();
                }

                int autoStopSeconds = this.GetAutoCookAutoStopSeconds();
                if (autoStopSeconds <= 0)
                {
                    Color prev = GUI.color;
                    GUI.color = new Color(1f, 0.45f, 0.45f);
                    GUI.Label(new Rect(20f, (float)num, 300f, 20f), "Set at least 1 second to enable auto-stop.", timerSmall);
                    GUI.color = prev;
                    num += 24;
                }
                else
                {
                    GUI.Label(new Rect(20f, (float)num, 320f, 20f), "Auto-stop after: " + this.FormatDurationHms(autoStopSeconds), timerSmall);
                    num += 22;

                    if (this.autoCookEnabled && this.autoCookAutoStopAt > 0f)
                    {
                        int remaining = Mathf.Max(0, Mathf.CeilToInt(this.autoCookAutoStopAt - Time.unscaledTime));
                        GUI.Label(new Rect(20f, (float)num, 320f, 20f), "Time remaining: " + this.FormatDurationHms(remaining), timerSmall);
                        num += 22;
                    }
                }
            }

            GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Cooking Speed: {this.cookingAutoSpeed:F1}x");
            num += 22;
            float newCookingSpeed = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.cookingAutoSpeed, 1f, 10f);
            if (newCookingSpeed != this.cookingAutoSpeed)
            {
                this.cookingAutoSpeed = newCookingSpeed;
                try { this.SaveKeybinds(false); } catch { }
                // If Auto Cook is currently enabled, update the game speed immediately
                if (this.autoCookEnabled)
                {
                    this.gameSpeed = this.cookingAutoSpeed;
                }
            }
            num += 30;

            // --- Teleport Patrol Toggle ---
            bool newPatrolEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 240f, 25f), this.cookingPatrolEnabled, "Enable Teleport Patrol");
            if (newPatrolEnabled != this.cookingPatrolEnabled)
            {
                this.cookingPatrolEnabled = newPatrolEnabled;
                if (newPatrolEnabled && autoCookEnabled && cookingPatrolPoints.Count > 0 && !isCookingPatrolActive)
                {
                    isCookingPatrolActive = true;
                    cookingPatrolCoroutine = MelonCoroutines.Start(CookingPatrolRoutine());
                    MelonLogger.Msg("[Cooking Patrol] STARTED (toggle)");
                }
                else if (!newPatrolEnabled && isCookingPatrolActive)
                {
                    isCookingPatrolActive = false;
                    if (cookingPatrolCoroutine != null)
                    {
                        MelonCoroutines.Stop(cookingPatrolCoroutine);
                        cookingPatrolCoroutine = null;
                        MelonLogger.Msg("[Cooking Patrol] STOPPED (toggle)");
                    }
                }
            }
            num += 30;

            if (this.cookingPatrolEnabled)
            {
                GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Cooking Points: {cookingPatrolPoints.Count}");
                num += 25;

                GUI.Label(new Rect(20f, (float)num, 85f, 22f), "Save Name:");
                this.cookingPatrolSaveName = GUI.TextField(new Rect(105f, (float)num - 2f, 175f, 25f), this.cookingPatrolSaveName ?? "");
                num += 30;

                if (GUI.Button(new Rect(20f, (float)num, 82f, 35f), "SAVE"))
                {
                    SaveCookingPatrolPoints(this.cookingPatrolSaveName);
                }
                if (GUI.Button(new Rect(109f, (float)num, 82f, 35f), "LOAD"))
                {
                    LoadCookingPatrolPoints(this.cookingPatrolSaveName);
                }
                if (GUI.Button(new Rect(198f, (float)num, 82f, 35f), "DELETE"))
                {
                    DeleteCookingPatrolSave(this.cookingPatrolSaveName);
                }
                num += 45;

                List<string> cookingSaves = this.GetCookingPatrolSaveNames();
                GUI.Label(new Rect(20f, (float)num, 260f, 18f), $"Saved Slots: {cookingSaves.Count}");
                num += 20;

                Rect saveListRect = new Rect(20f, (float)num, 260f, 104f);
                Rect saveListContent = new Rect(0f, 0f, 240f, Mathf.Max(104f, cookingSaves.Count * 26f));
                this.cookingPatrolSaveScrollPos = GUI.BeginScrollView(saveListRect, this.cookingPatrolSaveScrollPos, saveListContent);
                int saveRowY = 0;
                foreach (string saveName in cookingSaves)
                {
                    bool selected = string.Equals(this.SanitizeCookingPatrolSaveName(this.cookingPatrolSaveName), saveName, StringComparison.OrdinalIgnoreCase);
                    string display = selected ? ("> " + saveName) : saveName;
                    if (GUI.Button(new Rect(0f, (float)saveRowY, 124f, 24f), display))
                    {
                        this.cookingPatrolSaveName = saveName;
                    }
                    if (GUI.Button(new Rect(128f, (float)saveRowY, 52f, 24f), "Load"))
                    {
                        LoadCookingPatrolPoints(saveName);
                    }
                    if (GUI.Button(new Rect(184f, (float)saveRowY, 52f, 24f), "Del"))
                    {
                        DeleteCookingPatrolSave(saveName);
                        if (string.Equals(this.cookingPatrolSaveName, saveName, StringComparison.OrdinalIgnoreCase))
                        {
                            this.cookingPatrolSaveName = "";
                        }
                        break;
                    }
                    saveRowY += 26;
                }
                GUI.EndScrollView();
                num += 114;

                if (GUI.Button(new Rect(20f, (float)num, 260f, 40f), "ADD CURRENT POSITION + ROTATION"))
                {
                    GameObject p = GetPlayer();
                    if (p != null)
                    {
                        Vector3 position = p.transform.position;
                        Quaternion rotation = p.transform.rotation;
                        cookingPatrolPoints.Add(new CookingPatrolPoint(position, rotation));
                        MelonLogger.Msg($"Added cooking patrol point at {position} facing {rotation.eulerAngles}");

                        // Start patrol if Auto Cook is enabled, patrol toggle is on, and this is the first point
                        if (autoCookEnabled && cookingPatrolEnabled && cookingPatrolPoints.Count == 1 && !isCookingPatrolActive)
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
                float prevCookingWait = cookingWaitAtSpot;
                cookingWaitAtSpot = Mathf.Round(this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), cookingWaitAtSpot, 0.1f, 2.0f) * 100f) / 100f;
                if (Math.Abs(cookingWaitAtSpot - prevCookingWait) > 0.0001f) { try { this.SaveKeybinds(false); } catch { } }
                num += 40;
            }

            return (float)num + 20f;
        }

        private float DrawRadarSettingsTab(int startY)
        {
            int num = startY;
            GUIStyle header = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(20f, (float)num, 520f, 24f), "RADAR SETTINGS", header);
            num += 30;

            GUI.Label(new Rect(20f, (float)num, 200f, 20f), "Marker Style:");

            if (GUI.Button(new Rect(20f, (float)num + 22f, 120f, 28f), this.radarMarkerStyle == 0 ? "Default ✓" : "Default"))
            {
                if (this.radarMarkerStyle != 0)
                {
                    this.radarMarkerStyle = 0;
                    this.AddMenuNotification("Radar markers: Default", new Color(0.45f, 0.88f, 1f));
                    this.SaveRadarSettings();
                }
            }
            // Simple Text button
            if (GUI.Button(new Rect(150f, (float)num + 22f, 160f, 28f), this.radarMarkerStyle == 1 ? "Simple Text ✓" : "Simple Text"))
            {
                if (this.radarMarkerStyle != 1)
                {
                    this.radarMarkerStyle = 1;
                    this.AddMenuNotification("Radar markers: Simple Text", new Color(0.45f, 0.88f, 1f));
                    this.SaveRadarSettings();
                }
            }
            num += 60;

            // Note: other radar configuration moved to future updates.
            return (float)num;
        }

        private void StartAutoCookInternal()
        {
            this.autoCookEnabled = true;
            this.cookingCleanupMode = false;
            this.gameSpeed = this.cookingAutoSpeed;
            MelonLogger.Msg($"[Cooking] Bot STARTED (Auto Speed x{this.cookingAutoSpeed:F1})");
            this.AddMenuNotification("Auto Cook Enabled", new Color(0.45f, 1f, 0.55f));

            if (cookingPatrolEnabled && cookingPatrolPoints.Count > 0 && !isCookingPatrolActive)
            {
                isCookingPatrolActive = true;
                cookingPatrolCoroutine = MelonCoroutines.Start(CookingPatrolRoutine());
                MelonLogger.Msg("[Cooking Patrol] STARTED");
            }
            // Setup auto-stop timer if enabled
            int autoStopSeconds = this.GetAutoCookAutoStopSeconds();
            if (this.autoCookAutoStopEnabled && autoStopSeconds > 0)
            {
                this.autoCookAutoStopAt = Time.unscaledTime + autoStopSeconds;
            }
            else
            {
                this.autoCookAutoStopAt = -1f;
            }
        }

        private void StopAutoCookInternal(string reason)
        {
            bool wasEnabled = this.autoCookEnabled || this.isCookingPatrolActive;
            this.autoCookEnabled = false;
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
            if (this.radarSubTab == 1)
            {
                return this.DrawRadarSettingsTab(startY);
            }
            string text = this.isRadarActive ? "DISABLE RADAR" : "ENABLE RADAR";
            bool flag = this.DrawPrimaryActionButton(new Rect(20f, (float)num, 260f, 40f), text);
            if (flag)
            {
                this.ToggleRadar();
            }
            num += 50;

            // Select / Clear shortcuts
            if (this.DrawPrimaryActionButton(new Rect(20f, (float)num, 125f, 30f), "Select All Loots"))
            {
                this.showMushroomRadar = true;
                this.showFiddleheadRadar = true;
                this.showTallMustardRadar = true;
                this.showBurdockRadar = true;
                this.showMustardGreensRadar = true;
                this.showBlueberryRadar = true;
                this.showRaspberryRadar = true;
                this.showStoneRadar = true;
                this.showOreRadar = true;
                this.showTreeRadar = true;
                this.showRareTreeRadar = true;
                this.showAppleTreeRadar = true;
                this.showOrangeTreeRadar = true;
                this.showBubbleRadar = true;
                this.showInsectRadar = true;
                this.showFishShadowRadar = true;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            if (this.DrawPrimaryActionButton(new Rect(155f, (float)num, 125f, 30f), "Clear All Loots"))
            {
                this.showMushroomRadar = false;
                this.showFiddleheadRadar = false;
                this.showTallMustardRadar = false;
                this.showBurdockRadar = false;
                this.showMustardGreensRadar = false;
                this.showBlueberryRadar = false;
                this.showRaspberryRadar = false;
                this.showStoneRadar = false;
                this.showOreRadar = false;
                this.showTreeRadar = false;
                this.showRareTreeRadar = false;
                this.showAppleTreeRadar = false;
                this.showOrangeTreeRadar = false;
                this.showBubbleRadar = false;
                this.showInsectRadar = false;
                this.showFishShadowRadar = false;
                this.CheckRadarAutoToggle();
                this.Cleanup();
            }
            num += 45;
            // --- Berries & Mushrooms ---
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), "== Berries & Mushrooms ==");
            num += 25;
            bool flagMush = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showMushroomRadar, "Mushrooms");
            bool flagMushChanged = flagMush != this.showMushroomRadar;
            if (flagMushChanged)
            {
                this.showMushroomRadar = flagMush;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 30;
            bool flagBlue = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showBlueberryRadar, "Blueberries");
            bool flagBlueChanged = flagBlue != this.showBlueberryRadar;
            if (flagBlueChanged)
            {
                this.showBlueberryRadar = flagBlue;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 30;
            bool flagRasp = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showRaspberryRadar, "Raspberries");
            bool flagRaspChanged = flagRasp != this.showRaspberryRadar;
            if (flagRaspChanged)
            {
                this.showRaspberryRadar = flagRasp;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 40;

            // --- Events ---
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), "== Events ==");
            num += 25;
            bool flagFiddle = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showFiddleheadRadar, "Fiddlehead");
            if (flagFiddle != this.showFiddleheadRadar)
            {
                this.showFiddleheadRadar = flagFiddle;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 30;
            bool flagTallMustard = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showTallMustardRadar, "Tall Mustard");
            if (flagTallMustard != this.showTallMustardRadar)
            {
                this.showTallMustardRadar = flagTallMustard;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 30;
            bool flagBurdock = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showBurdockRadar, "Burdock");
            if (flagBurdock != this.showBurdockRadar)
            {
                this.showBurdockRadar = flagBurdock;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 30;
            bool flagMustardGreens = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showMustardGreensRadar, "Mustard Greens");
            if (flagMustardGreens != this.showMustardGreensRadar)
            {
                this.showMustardGreensRadar = flagMustardGreens;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 40;

            // --- Resources ---
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), "== Resources ==");
            num += 25;
            bool flagRock = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showStoneRadar, "Stones");
            bool flagRockChanged = flagRock != this.showStoneRadar;
            if (flagRockChanged)
            {
                this.showStoneRadar = flagRock;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 30;
            bool flagOre = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showOreRadar, "Ores");
            bool flagOreChanged = flagOre != this.showOreRadar;
            if (flagOreChanged)
            {
                this.showOreRadar = flagOre;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 40;

            // --- Trees ---
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), "== Trees ==");
            num += 25;
            bool flagTreeUI = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showTreeRadar, "Trees");
            bool flagTreeUIChanged = flagTreeUI != this.showTreeRadar;
            if (flagTreeUIChanged)
            {
                this.showTreeRadar = flagTreeUI;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 30;
            bool flagRareTreeUI = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showRareTreeRadar, "Rare Trees");
            bool flagRareTreeUIChanged = flagRareTreeUI != this.showRareTreeRadar;
            if (flagRareTreeUIChanged)
            {
                this.showRareTreeRadar = flagRareTreeUI;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 30;
            bool flagAppleTreeUI = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showAppleTreeRadar, "Apple Trees");
            bool flagAppleTreeUIChanged = flagAppleTreeUI != this.showAppleTreeRadar;
            if (flagAppleTreeUIChanged)
            {
                this.showAppleTreeRadar = flagAppleTreeUI;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 30;
            bool flagOrangeTreeUI = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showOrangeTreeRadar, "Mandarin Trees");
            bool flagOrangeTreeUIChanged = flagOrangeTreeUI != this.showOrangeTreeRadar;
            if (flagOrangeTreeUIChanged)
            {
                this.showOrangeTreeRadar = flagOrangeTreeUI;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
            }
            num += 40;

            // --- Misc ---
            GUI.Label(new Rect(20f, (float)num, 260f, 20f), "== Misc ==");
            num += 25;
            bool flag11 = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.showBubbleRadar, "Bubbles");
            bool flag12 = flag11 != this.showBubbleRadar;
            if (flag12)
            {
                this.showBubbleRadar = flag11;
                this.CheckRadarAutoToggle();
                if (this.isRadarActive) this.RunRadar();
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
                if (this.isRadarActive) this.RunRadar();
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
                    bool flag3 = component2 != null;
                    if (flag3)
                    {
                        if (this.blueberryCollectListener == null)
                        {
                            this.blueberryCollectListener = new System.Action(this.MarkNearestBlueberryCollected);
                        }
                        component2.onClick.RemoveListener(this.blueberryCollectListener);
                        component2.onClick.AddListener(this.blueberryCollectListener);
                        this.lastBlueberryButton = component2;
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
                        bool flag4 = component2 != null;
                        if (flag4)
                        {
                            if (this.raspberryCollectListener == null)
                            {
                                this.raspberryCollectListener = new System.Action(this.MarkNearestRaspberryCollected);
                            }
                            component2.onClick.RemoveListener(this.raspberryCollectListener);
                            component2.onClick.AddListener(this.raspberryCollectListener);
                            this.lastRaspberryButton = component2;
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
                    if (this.ShouldAutoCollectBySprite(text))
                    {
                        Button component2 = gameObject.GetComponent<Button>();
                        bool flag3 = component2 != null && component2.interactable;
                        if (flag3)
                        {
                            component2.onClick.Invoke();
                            this.autoCollectClickedSinceArrival = true;
                            this.ClickButtonIfExists("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)/AniRoot@go@ani/exit@btn@go");
                        }
                    }
                }
            }
        }

        private void DebugLogCurrentInteractSprite()
        {
            if (Time.unscaledTime < this.nextInteractSpriteDebugAt)
            {
                return;
            }

            this.nextInteractSpriteDebugAt = Time.unscaledTime + 0.2f;

            GameObject interactObj = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn");
            if (interactObj == null || !interactObj.activeInHierarchy)
            {
                return;
            }

            Image image = interactObj.GetComponent<Image>();
            if (image == null || image.sprite == null)
            {
                return;
            }

            string spriteName = image.sprite.name ?? string.Empty;
            if (string.IsNullOrEmpty(spriteName) || spriteName == this.lastLoggedInteractSpriteName)
            {
                return;
            }

            this.lastLoggedInteractSpriteName = spriteName;
            MelonLogger.Msg("[AutoCollectDebug] Interact sprite: " + spriteName);
        }

        private bool ShouldAutoCollectBySprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
            {
                return false;
            }

            string text = spriteName.ToLowerInvariant();
            if (text.Contains("mushroom"))
            {
                return this.collectMushrooms;
            }

            if (text.Contains("interaction_8"))
            {
                string nearestLabel = this.GetNearestRadarNodeLabel(6f);
                if (!string.IsNullOrEmpty(nearestLabel))
                {
                    if (nearestLabel.Contains("Fiddlehead") || nearestLabel.Contains("Tall Mustard") || nearestLabel.Contains("Burdock") || nearestLabel.Contains("Mustard Greens"))
                    {
                        return this.collectEventResources;
                    }
                }

                return this.collectBerries;
            }

            if (text.Contains("wildvegetables"))
            {
                return this.collectEventResources;
            }

            return this.collectOther;
        }

        private string GetNearestRadarNodeLabel(float maxDistance)
        {
            if (this.radarContainer == null)
            {
                return string.Empty;
            }

            GameObject player = GetPlayer();
            Vector3 origin = player != null ? player.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            float nearestDistance = maxDistance;
            string nearestLabel = string.Empty;

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

                float dist = Vector3.Distance(origin, child.position);
                if (dist > nearestDistance)
                {
                    continue;
                }

                string[] lines = text.Split(new char[] { '\n' }, StringSplitOptions.None);
                nearestDistance = dist;
                nearestLabel = lines.Length > 0 ? lines[0] : text;
            }

            return nearestLabel;
        }

        // Token: 0x06000011 RID: 17 RVA: 0x00003D94 File Offset: 0x00001F94
        // Token: 0x0600001E RID: 30 RVA: 0x00009D74 File Offset: 0x00007F74
        private void RunAutoCookLogic()
        {
            bool flag = !this.cookingCleanupMode && this.IsAddButtonVisible();
            if (flag)
            {
                this.cookingCleanupMode = true;
                this.cookingPanelClosed = false;
            }
            bool flag2 = this.cookingCleanupMode;
            if (flag2)
            {
                bool flag3 = !this.cookingPanelClosed;
                if (flag3)
                {
                    GameObject gameObject = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)");
                    bool flag4 = gameObject != null;
                    if (flag4)
                    {
                        string[] array = new string[]
                        {
                            "back@w",
                            "back@btn",
                            "close@btn",
                            "closeBtn@w",
                            "BackBtn",
                            "back_btn"
                        };
                        foreach (string text in array)
                        {
                            Transform transform = gameObject.transform.Find(text);
                            bool flag5 = transform == null;
                            if (flag5)
                            {
                                Button[] array3 = gameObject.GetComponentsInChildren<Button>(true);
                                foreach (Button button in array3)
                                {
                                    bool flag6 = button.name == text && button.gameObject.activeInHierarchy && button.interactable;
                                    if (flag6)
                                    {
                                        button.onClick.Invoke();
                                        this.cookingPanelClosed = true;
                                        this.cookingPanelClosedTime = Time.unscaledTime;
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                Button component = transform.GetComponent<Button>();
                                bool flag7 = component != null && transform.gameObject.activeInHierarchy;
                                if (flag7)
                                {
                                    component.onClick.Invoke();
                                    this.cookingPanelClosed = true;
                                    this.cookingPanelClosedTime = Time.unscaledTime;
                                    break;
                                }
                            }
                        }
                        return;
                    }
                    this.cookingPanelClosed = true;
                    this.cookingPanelClosedTime = Time.unscaledTime;
                }
                bool flag8 = this.ClickCookingCleanup();
                bool flag9 = flag8;
                if (!flag9)
                {
                    float num = Time.unscaledTime - this.cookingPanelClosedTime;
                    bool flag10 = num < 15f;
                    if (!flag10)
                    {
                        this.cookingCleanupMode = false;
                        this.cookingPanelClosed = false;
                        this.autoCookEnabled = false;
                        bool flag11 = this.cookMoveKeyIndex >= 0;
                        if (flag11)
                        {
                            this.TrySendMove(Vector2.zero);
                            this.cookMoveKeyIndex = -1;
                        }
                    }
                }
            }
            else
            {
                bool flag12 = false;
                string text2 = "GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)/AniRoot@queueanimation/detail@t/btnBar@go/confirm@swapbtn";
                GameObject gameObject2 = GameObject.Find(text2);
                bool flag13 = gameObject2 != null;
                if (flag13)
                {
                    Button component2 = gameObject2.GetComponent<Button>();
                    bool flag14 = component2 != null && gameObject2.activeInHierarchy && component2.interactable;
                    if (flag14)
                    {
                        component2.onClick.Invoke();
                        this.lastConfirmClickTime = Time.unscaledTime;
                        flag12 = true;
                    }
                }
                this.ClickButtonIfExists("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn");
                this.ClickButtonIfExists("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_cook_danger@list/CommonIconForCookDanger(Clone)/root_visible@go/icon@img@btn");
                bool flag15 = !flag12 && Time.unscaledTime - this.lastConfirmClickTime > 5f;
                if (flag15)
                {
                    GameObject gameObject3 = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/CookPanel(Clone)");
                    bool flag16 = gameObject3 != null;
                    if (flag16)
                    {
                        Button[] array5 = gameObject3.GetComponentsInChildren<Button>(true);
                        foreach (Button button2 in array5)
                        {
                            bool flag17 = !button2.gameObject.activeInHierarchy || !button2.interactable;
                            if (!flag17)
                            {
                                string text3 = button2.name.ToLower();
                                bool flag18 = text3.Contains("confirm") || text3.Contains("queue") || text3.Contains("cook") || text3.Contains("start");
                                if (flag18)
                                {
                                    button2.onClick.Invoke();
                                    this.lastConfirmClickTime = Time.unscaledTime;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool IsAddButtonVisible()
        {
            // This was an original native-side check we cannot replicate exactly.
            // Returning false so cookingCleanupMode is only triggered by player detection.
            return false;
        }

        private void TrySendMove(Vector2 direction)
        {
            // Stub: reserved for simulated movement during cooking patrol.
            // cookMoveKeyIndex tracks which directional key is active (-1 = none).
        }

        // Token: 0x06000012 RID: 18 RVA: 0x00003DE0 File Offset: 0x00001FE0
        private void ClickButtonIfExists(string path)
        {
            try
            {
                GameObject gameObject = GameObject.Find(path);
                if (gameObject == null) return;
                Button component = gameObject.GetComponent<Button>();
                if (component != null && gameObject.activeInHierarchy && component.interactable)
                {
                    component.onClick.Invoke();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ClickButtonIfExists] Error clicking path '{path}': {ex.Message}");
                this.lastAutoCookException = ex.Message;
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
            return this.IsToolsPanelOpen();
        }

        private bool IsToolsPanelOpen()
        {
            GameObject toolsPanel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/ToolsPanel(Clone)");
            return toolsPanel != null && toolsPanel.activeInHierarchy;
        }

        public void StartToolEquipRequest(int toolType)
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

            // Try clicking the tool directly from the status bar first (preferred)
            bool clicked = this.TryEquipStatusBarItemBySpriteAny(needles);
            if (clicked)
            {
                MelonLogger.Msg("[Tools] Equip click sent from status bar for " + friendlyName);
                this.AddMenuNotification($"Equip click: {friendlyName}", new Color(0.45f, 1f, 0.55f));
                this.pendingToolEquipType = 0;
                return;
            }


            // If status-bar didn't work, only attempt toolbox-based equip when ToolsPanel is already open.
            if (!this.IsToolsPanelOpen())
            {
                // Open the tools panel using the handable opener button (preferred path)
                this.ClickButtonIfExistsWithParent("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/bottom_right_layout@go/handable_bar@go/handable@btn@ani");
                return;
            }

            // Fallback: try equipping from the opened ToolsPanel
            clicked = this.TryEquipToolboxItemBySpriteAny(needles);

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
            GameObject toolsPanel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/ToolsPanel(Clone)");
            if (toolsPanel == null || !toolsPanel.activeInHierarchy)
            {
                return false;
            }

            Image[] images = toolsPanel.GetComponentsInChildren<Image>(true);
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

        private bool TryEquipStatusBarItemBySpriteAny(string[] spriteNeedles)
        {
            GameObject statusRoot = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/bottom_right_layout@go/handable_bar@go");
            if (statusRoot == null || !statusRoot.activeInHierarchy)
            {
                return false;
            }

            Image[] images = statusRoot.GetComponentsInChildren<Image>(true);
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
            // If using hardcoded positions, populate the patrol points list from static arrays
            if (this.treeFarmUseHardcoded)
            {
                this.treeFarmPoints.Clear();
                foreach (Vector3 v in TreePositions) this.treeFarmPoints.Add(new TreeFarmPatrolPoint(v, Quaternion.identity));
                foreach (Vector3 v2 in RareTreePositions) this.treeFarmPoints.Add(new TreeFarmPatrolPoint(v2, Quaternion.identity));
                foreach (Vector3 v3 in AppleTreePositions) this.treeFarmPoints.Add(new TreeFarmPatrolPoint(v3, Quaternion.identity));
                foreach (Vector3 v4 in OrangeTreePositions) this.treeFarmPoints.Add(new TreeFarmPatrolPoint(v4, Quaternion.identity));
                // Shuffle the points to avoid predictable order
                int n = this.treeFarmPoints.Count;
                while (n > 1)
                {
                    n--;
                    int k = this.instanceRng.Next(n + 1);
                    TreeFarmPatrolPoint tmp = this.treeFarmPoints[k];
                    this.treeFarmPoints[k] = this.treeFarmPoints[n];
                    this.treeFarmPoints[n] = tmp;
                }
                this.treeFarmCurrentIndex = 0;
                this.AddMenuNotification($"Tree farm points populated ({this.treeFarmPoints.Count})", new Color(0.45f, 1f, 0.55f));
            }

            this.AddMenuNotification("Tree Farm enabled", new Color(0.45f, 1f, 0.55f));
        }

        // Resource-farm helpers
        private void ToggleResourceFarm()
        {
            this.autoResourceFarmEnabled = !this.autoResourceFarmEnabled;
            if (this.autoResourceFarmEnabled)
            {
                MelonLogger.Msg("[ResourceFarm] ENABLED! Make sure you're holding an axe/pickaxe!");
                this.ResetResourceFarmState();
                int autoStopSeconds = this.GetAutoResourceFarmAutoStopSeconds();
                if (this.autoResourceFarmAutoStopEnabled && autoStopSeconds > 0)
                {
                    this.autoResourceFarmAutoStopAt = Time.unscaledTime + autoStopSeconds;
                    this.AddMenuNotification("Resource Farm auto-stop set: " + this.FormatDurationHms(autoStopSeconds), new Color(0.55f, 0.88f, 1f));
                }
                else
                {
                    this.autoResourceFarmAutoStopAt = -1f;
                }
            }
            else
            {
                MelonLogger.Msg("[ResourceFarm] DISABLED!");
                this.ResetResourceFarmState();
                this.resourceJustArrived = false;
                SimulateFKeyHeld = false;
                SimulateFKeyDown = false;
                SimulateFKeyUp = false;
                this.fKeySimFrame = 0;
                this.autoResourceFarmAutoStopAt = -1f;
            }
        }

        private void ResetResourceFarmState()
        {
            this.hasResourceStartPosition = false;
            this.currentResourceMarkerIndex = 0;
            this.isResourceReturningToStart = false;
            this.visitedResourceMarkerIndices.Clear();
            this.resourceMarkersNeedShuffle = true;
        }

        public void ResetAllCooldowns()
        {
            this.rockCooldowns.Clear();
            this.rockHideUntil.Clear();
            this.oreCooldowns.Clear();
            this.oreHideUntil.Clear();
            this.treeCooldowns.Clear();
            this.treeHideUntil.Clear();
            this.rareTreeCooldowns.Clear();
            this.rareTreeHideUntil.Clear();
            this.appleTreeCooldowns.Clear();
            this.appleTreeHideUntil.Clear();
            this.orangeTreeCooldowns.Clear();
            this.orangeTreeHideUntil.Clear();
            this.resourceMarkersNeedShuffle = true;
            this.visitedResourceMarkerIndices.Clear();
            MelonLogger.Msg("[ResourceFarm] All cooldowns reset!");
        }

        public void OnTeleportArrivedResource()
        {
            this.isResourceFarmTeleport = false;
            this.resourceJustArrived = true;
            this.resourceArrivalTime = Time.unscaledTime;
            MelonLogger.Msg($"[ResourceFarm] Arrived! Will press F for {this.resourceClickDuration}s (after {this.resourceArrivalDelay}s delay)");
        }

        public void UpdateResourceFarm()
        {
            if (!this.autoResourceFarmEnabled) return;
            this.UpdateResourceMarkerPositions();
            // Auto-stop check
            if (this.autoResourceFarmAutoStopEnabled && this.autoResourceFarmAutoStopAt > 0f && Time.unscaledTime >= this.autoResourceFarmAutoStopAt)
            {
                MelonLogger.Msg("[ResourceFarm] Auto-stop timer reached. Stopping resource farm.");
                this.AddMenuNotification("Resource Farm auto-stopped", new Color(0.9f, 0.55f, 0.55f));
                this.autoResourceFarmEnabled = false;
                this.ResetResourceFarmState();
                this.resourceJustArrived = false;
                SimulateFKeyHeld = false;
                SimulateFKeyDown = false;
                SimulateFKeyUp = false;
                this.fKeySimFrame = 0;
                this.autoResourceFarmAutoStopAt = -1f;
                return;
            }
            if (!OverridePlayerPosition && !this.resourceJustArrived && Time.unscaledTime - this.lastResourceTeleportTime > this.resourceTeleportCooldown)
            {
                if (this.resourceMarkerPositions.Count > 0)
                {
                    // If paused due to auto-repair, skip starting a teleport until pause expires
                    if (Time.time < this.resourceRepairPauseUntil)
                    {
                        return;
                    }
                    this.TeleportToNextResource();
                    this.lastResourceTeleportTime = Time.unscaledTime;
                }
            }
            if (this.resourceJustArrived)
            {
                float dt = Time.unscaledTime - this.resourceArrivalTime;
                if (dt > this.resourceClickDuration)
                {
                    this.resourceJustArrived = false;
                    SimulateFKeyHeld = false;
                    SimulateFKeyDown = false;
                    SimulateFKeyUp = false;
                    this.fKeySimFrame = 0;
                    GameObject player = this.FindPlayerRoot();
                    if (player != null)
                    {
                        this.MarkResourceCollected(player.transform.position);
                    }
                    MelonLogger.Msg("[ResourceFarm] Done pressing F, ready for next resource");
                }
                else if (dt > this.resourceArrivalDelay)
                {
                    // Wait until the gather UI is present before attempting interaction
                    if (!this.IsGatherWidgetVisible())
                    {
                        this.autoFarmStatus = "Waiting for gather UI...";
                        return;
                    }
                    this.fKeySimFrame++;
                    int m = this.fKeySimFrame % 6;
                    if (m == 0)
                    {
                        SimulateFKeyDown = true;
                        SimulateFKeyHeld = true;
                        SimulateFKeyUp = false;
                        this.resourceClickCount++;
                    }
                    else if (m <= 3)
                    {
                        SimulateFKeyDown = false;
                        SimulateFKeyHeld = true;
                        SimulateFKeyUp = false;
                    }
                    else if (m == 4)
                    {
                        SimulateFKeyDown = false;
                        SimulateFKeyHeld = false;
                        SimulateFKeyUp = true;
                    }
                    else
                    {
                        SimulateFKeyDown = false;
                        SimulateFKeyHeld = false;
                        SimulateFKeyUp = false;
                    }
                    this.DirectClickInteractButton();
                }
            }
            else
            {
                if (SimulateFKeyHeld || SimulateFKeyDown)
                {
                    SimulateFKeyHeld = false;
                    SimulateFKeyDown = false;
                    SimulateFKeyUp = false;
                    this.fKeySimFrame = 0;
                }
            }
        }

        private void TeleportToNextResource()
        {
            if (this.resourceMarkerPositions.Count == 0) return;
            if (OverridePlayerPosition) return;
            GameObject p = this.FindPlayerRoot();
            if (p == null)
            {
                MelonLogger.Warning("[ResourceFarm] Cannot find player!");
                return;
            }
            Vector3 pos = p.transform.position;
            if (!this.hasResourceStartPosition)
            {
                this.resourceStartPosition = pos;
                this.hasResourceStartPosition = true;
            }
            Vector3 targetPos;
            if (this.isResourceReturningToStart)
            {
                targetPos = this.resourceStartPosition;
                this.isResourceReturningToStart = false;
                this.visitedResourceMarkerIndices.Clear();
                this.resourceMarkersNeedShuffle = true;
                this.currentResourceMarkerIndex = -1;
                MelonLogger.Msg("[ResourceFarm] Returning to start position...");
            }
            else
            {
                List<int> notVisited = new List<int>();
                for (int i=0;i<this.resourceMarkerPositions.Count;i++) if (!this.visitedResourceMarkerIndices.Contains(i)) notVisited.Add(i);
                if (notVisited.Count == 0)
                {
                    this.isResourceReturningToStart = true;
                    this.visitedResourceMarkerIndices.Clear();
                    this.resourceMarkersNeedShuffle = true;
                    this.currentResourceMarkerIndex = -1;
                    MelonLogger.Msg($"[ResourceFarm] All {this.resourceMarkerPositions.Count} resources visited! Returning to start...");
                    return;
                }
                int idx = this.instanceRng.Next(0, notVisited.Count);
                int chosen = notVisited[idx];
                this.visitedResourceMarkerIndices.Add(chosen);
                this.currentResourceMarkerIndex = chosen;
                targetPos = this.resourceMarkerPositions[chosen];
                MelonLogger.Msg($"[ResourceFarm] Teleporting to resource {this.visitedResourceMarkerIndices.Count}/{this.resourceMarkerPositions.Count} (index:{chosen})");
            }

            this.isResourceFarmTeleport = true;
            this.TeleportToLocation(targetPos);
            this.teleportFramesRemaining = 10;
        }

        private bool IsGatherWidgetVisible()
        {
            try
            {
                GameObject g = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/middle_center_layout@go/gather@go/GatherSelectWidget(Clone)");
                return g != null && g.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateResourceMarkerPositions()
        {
            List<Vector3> list = new List<Vector3>();
            float time = Time.time;
            if (this.farmRocks)
            {
                // RockPositions assumed present in file (ported arrays)
                for (int i=0;i<HeartopiaComplete.RockPositions.Length;i++)
                {
                    float until;
                    if (this.rockCooldowns.TryGetValue(i, out until) && until > time) continue;
                    float hu;
                    if (this.rockHideUntil.TryGetValue(i, out hu) && hu > time) continue;
                    list.Add(HeartopiaComplete.RockPositions[i]);
                }
            }
            if (this.farmOres)
            {
                for (int j=0;j<HeartopiaComplete.OrePositions.Length;j++)
                {
                    float until;
                    if (this.oreCooldowns.TryGetValue(j, out until) && until > time) continue;
                    float hu;
                    if (this.oreHideUntil.TryGetValue(j, out hu) && hu > time) continue;
                    list.Add(HeartopiaComplete.OrePositions[j]);
                }
            }
            if (this.farmTrees)
            {
                for (int k=0;k<HeartopiaComplete.TreePositions.Length;k++)
                {
                    float until;
                    if (this.treeCooldowns_res.TryGetValue(k, out until) && until > time) continue;
                    float hu;
                    if (this.treeHideUntil_res.TryGetValue(k, out hu) && hu > time) continue;
                    list.Add(HeartopiaComplete.TreePositions[k]);
                }
            }
            if (this.farmRareTrees)
            {
                for (int l=0;l<HeartopiaComplete.RareTreePositions.Length;l++)
                {
                    float until;
                    if (this.rareTreeCooldowns_res.TryGetValue(l, out until) && until > time) continue;
                    float hu;
                    if (this.rareTreeHideUntil_res.TryGetValue(l, out hu) && hu > time) continue;
                    list.Add(HeartopiaComplete.RareTreePositions[l]);
                }
            }
            if (this.farmAppleTrees)
            {
                for (int m=0;m<HeartopiaComplete.AppleTreePositions.Length;m++)
                {
                    float until;
                    if (this.appleTreeCooldowns_res.TryGetValue(m, out until) && until > time) continue;
                    float hu;
                    if (this.appleTreeHideUntil_res.TryGetValue(m, out hu) && hu > time) continue;
                    list.Add(HeartopiaComplete.AppleTreePositions[m]);
                }
            }
            if (this.farmOrangeTrees)
            {
                for (int n=0;n<HeartopiaComplete.OrangeTreePositions.Length;n++)
                {
                    float until;
                    if (this.orangeTreeCooldowns_res.TryGetValue(n, out until) && until > time) continue;
                    float hu;
                    if (this.orangeTreeHideUntil_res.TryGetValue(n, out hu) && hu > time) continue;
                    list.Add(HeartopiaComplete.OrangeTreePositions[n]);
                }
            }
            if (list.Count != this.lastResourceMarkerCount || this.resourceMarkersNeedShuffle)
            {
                this.resourceMarkerPositions = list;
                this.lastResourceMarkerCount = this.resourceMarkerPositions.Count;
                int num = this.resourceMarkerPositions.Count;
                while (num > 1)
                {
                    num--;
                    int idx = this.instanceRng.Next(num + 1);
                    Vector3 tmp = this.resourceMarkerPositions[idx];
                    this.resourceMarkerPositions[idx] = this.resourceMarkerPositions[num];
                    this.resourceMarkerPositions[num] = tmp;
                }
                if (this.resourceMarkerPositions.Count > 0 && this.currentResourceMarkerIndex <= 0)
                {
                    this.currentResourceMarkerIndex = this.instanceRng.Next(0, this.resourceMarkerPositions.Count) - 1;
                }
                this.resourceMarkersNeedShuffle = false;
                this.visitedResourceMarkerIndices.Clear();
                MelonLogger.Msg($"[ResourceFarm] Shuffled markers: {this.resourceMarkerPositions.Count} available");
            }
        }

        public int GetResourceAvailableCount(Dictionary<int,float> cooldowns, int total)
        {
            int c = 0;
            float t = Time.time;
            for (int i=0;i<total;i++)
            {
                float until;
                if (cooldowns.TryGetValue(i,out until) && until > t) continue;
                c++;
            }
            return c;
        }

        public int GetTotalAvailableResources()
        {
            return GetResourceAvailableCount(this.rockCooldowns, HeartopiaComplete.RockPositions.Length)
                + GetResourceAvailableCount(this.oreCooldowns, HeartopiaComplete.OrePositions.Length)
                + GetResourceAvailableCount(this.treeCooldowns_res, HeartopiaComplete.TreePositions.Length)
                + GetResourceAvailableCount(this.rareTreeCooldowns_res, HeartopiaComplete.RareTreePositions.Length)
                + GetResourceAvailableCount(this.appleTreeCooldowns_res, HeartopiaComplete.AppleTreePositions.Length)
                + GetResourceAvailableCount(this.orangeTreeCooldowns_res, HeartopiaComplete.OrangeTreePositions.Length);
        }

        public string GetResourceFarmStatus()
        {
            if (!this.autoResourceFarmEnabled) return "DISABLED";
            if (this.resourceJustArrived) return "GATHERING...";
            if (this.isResourceFarmTeleport) return "TELEPORTING...";
            return "IDLE";
        }

        private void MarkResourceCollected(Vector3 playerPos)
        {
            float hide = 10f;
            if (this.farmRocks)
            {
                int idx = this.FindClosestItemIndexLocal(playerPos, HeartopiaComplete.RockPositions);
                if (idx >= 0)
                {
                    this.rockCooldowns[idx] = Time.time + this.rockCooldownDuration;
                    this.rockHideUntil[idx] = Time.time + hide;
                    MelonLogger.Msg($"[ResourceFarm] Rock #{idx} collected, cooldown {this.rockCooldownDuration}s");
                }
            }
            if (this.farmOres)
            {
                int idx = this.FindClosestItemIndexLocal(playerPos, HeartopiaComplete.OrePositions);
                if (idx >= 0)
                {
                    this.oreCooldowns[idx] = Time.time + this.oreCooldownDuration;
                    this.oreHideUntil[idx] = Time.time + hide;
                    MelonLogger.Msg($"[ResourceFarm] Ore #{idx} collected, cooldown {this.oreCooldownDuration}s");
                }
            }
            if (this.farmTrees)
            {
                int idx = this.FindClosestItemIndexLocal(playerPos, HeartopiaComplete.TreePositions);
                if (idx >= 0)
                {
                    this.treeCooldowns_res[idx] = Time.time + this.treeCooldownDuration_res;
                    this.treeHideUntil_res[idx] = Time.time + hide;
                    MelonLogger.Msg($"[ResourceFarm] Tree #{idx} collected, cooldown {this.treeCooldownDuration_res}s");
                }
            }
            if (this.farmRareTrees)
            {
                int idx = this.FindClosestItemIndexLocal(playerPos, HeartopiaComplete.RareTreePositions);
                if (idx >= 0)
                {
                    this.rareTreeCooldowns_res[idx] = Time.time + this.rareTreeCooldownDuration_res;
                    this.rareTreeHideUntil_res[idx] = Time.time + hide;
                    MelonLogger.Msg($"[ResourceFarm] Rare Tree #{idx} collected, cooldown {this.rareTreeCooldownDuration_res}s");
                }
            }
            if (this.farmAppleTrees)
            {
                int idx = this.FindClosestItemIndexLocal(playerPos, HeartopiaComplete.AppleTreePositions);
                if (idx >= 0)
                {
                    this.appleTreeCooldowns_res[idx] = Time.time + this.appleTreeCooldownDuration_res;
                    this.appleTreeHideUntil_res[idx] = Time.time + hide;
                    MelonLogger.Msg($"[ResourceFarm] Apple Tree #{idx} collected, cooldown {this.appleTreeCooldownDuration_res}s");
                }
            }
            if (this.farmOrangeTrees)
            {
                int idx = this.FindClosestItemIndexLocal(playerPos, HeartopiaComplete.OrangeTreePositions);
                if (idx >= 0)
                {
                    this.orangeTreeCooldowns_res[idx] = Time.time + this.orangeTreeCooldownDuration_res;
                    this.orangeTreeHideUntil_res[idx] = Time.time + hide;
                    MelonLogger.Msg($"[ResourceFarm] Mandarin Tree #{idx} collected, cooldown {this.orangeTreeCooldownDuration_res}s");
                }
            }
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
            // If we're waiting for a recent swing attempt to be confirmed, poll for confirmation non-blocking
            if (this.awaitingSwingConfirm)
            {
                try
                {
                    bool confirmed = false;
                    // Check animator change
                    // Animator checks removed (not available in this build); rely on swing button state only

                    // Check swing button change
                    if (!confirmed)
                    {
                        GameObject swingBtn = GameObject.Find(this.swingButtonPath);
                        if (swingBtn != null)
                        {
                            Button b = swingBtn.GetComponent<Button>();
                            bool nowInteract = (b != null) ? b.interactable : swingBtn.activeInHierarchy;
                            if (nowInteract != this.swingConfirmStartBtnInteract)
                            {
                                confirmed = true;
                                MelonLogger.Msg("[TreeFarm] Swing confirmed by button interactable change (async)");
                            }
                        }
                    }

                    if (confirmed)
                    {
                        this.treeFarmChopSent++;
                        this.treeFarmNoPromptAttempts = 0;
                        this.awaitingSwingConfirm = false;
                        this.treeFarmStatus = $"Chopping {this.treeFarmChopSent}/{this.treeFarmChopPressCount}...";
                        this.treeFarmNextActionAt = Time.time + this.treeFarmChopPressGap;
                        return;
                    }

                    if (Time.time > this.swingConfirmDeadline)
                    {
                        // confirmation timed out
                        this.awaitingSwingConfirm = false;
                        this.treeFarmNoPromptAttempts++;
                        this.treeFarmNextActionAt = Time.time + 0.15f;
                        MelonLogger.Msg("[TreeFarm] Swing confirmation timed out (async)");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg("[TreeFarm] Async confirm error: " + ex.Message);
                    this.awaitingSwingConfirm = false;
                }
            }
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
                    // If hardcoded resource-style mode is active, skip points that are on cooldown
                    if (this.treeFarmUseHardcoded)
                    {
                        int attempts = 0;
                        bool found = false;
                        while (attempts < this.treeFarmPoints.Count)
                        {
                            Vector3 checkPos = this.treeFarmPoints[this.treeFarmCurrentIndex].Position.ToVector3();
                            int tIdx = this.FindClosestItemIndexLocal(checkPos, TreePositions);
                            if (tIdx >= 0)
                            {
                                float until;
                                if (this.treeCooldowns.TryGetValue(tIdx, out until) && until > Time.time)
                                {
                                    // skip
                                    this.treeFarmCurrentIndex++;
                                    if (this.treeFarmCurrentIndex >= this.treeFarmPoints.Count) this.treeFarmCurrentIndex = 0;
                                    attempts++;
                                    continue;
                                }
                            }
                            int rIdx = this.FindClosestItemIndexLocal(checkPos, RareTreePositions);
                            if (rIdx >= 0)
                            {
                                float until2;
                                if (this.rareTreeCooldowns.TryGetValue(rIdx, out until2) && until2 > Time.time)
                                {
                                    this.treeFarmCurrentIndex++;
                                    if (this.treeFarmCurrentIndex >= this.treeFarmPoints.Count) this.treeFarmCurrentIndex = 0;
                                    attempts++;
                                    continue;
                                }
                            }
                            int aIdx = this.FindClosestItemIndexLocal(checkPos, AppleTreePositions);
                            if (aIdx >= 0)
                            {
                                float until3;
                                if (this.appleTreeCooldowns.TryGetValue(aIdx, out until3) && until3 > Time.time)
                                {
                                    this.treeFarmCurrentIndex++;
                                    if (this.treeFarmCurrentIndex >= this.treeFarmPoints.Count) this.treeFarmCurrentIndex = 0;
                                    attempts++;
                                    continue;
                                }
                            }
                            int oIdx = this.FindClosestItemIndexLocal(checkPos, OrangeTreePositions);
                            if (oIdx >= 0)
                            {
                                float until4;
                                if (this.orangeTreeCooldowns.TryGetValue(oIdx, out until4) && until4 > Time.time)
                                {
                                    this.treeFarmCurrentIndex++;
                                    if (this.treeFarmCurrentIndex >= this.treeFarmPoints.Count) this.treeFarmCurrentIndex = 0;
                                    attempts++;
                                    continue;
                                }
                            }
                            found = true;
                            break;
                        }
                        if (!found)
                        {
                            this.StopTreeFarm("No available tree positions");
                            return;
                        }
                        point = this.treeFarmPoints[this.treeFarmCurrentIndex];
                        targetPos = point.Position.ToVector3();
                        targetRot = point.Rotation.ToQuaternion();
                    }
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
                    bool chopped = false;
                    // Respect a cooldown so we don't spam triggers too quickly
                    if (Time.time - this.lastAutoSwingTime >= this.swingCooldown)
                    {
                        bool attempted = false;
                        // Prefer direct trigger activation
                        if (this.PerformAutoSwing())
                        {
                            attempted = true;
                            this.lastAutoSwingTime = Time.time;
                            // Start async confirmation window; actual counting happens in the async poll above
                            this.awaitingSwingConfirm = true;
                            this.swingConfirmDeadline = Time.time + 0.9f;
                            // clear anim-hash baseline (anim not relied upon)
                            this.swingConfirmStartAnimHash = 0;
                            GameObject swingBtnObj = GameObject.Find(this.swingButtonPath);
                            if (swingBtnObj != null)
                            {
                                Button bb = swingBtnObj.GetComponent<Button>();
                                this.swingConfirmStartBtnInteract = (bb != null) ? bb.interactable : swingBtnObj.activeInHierarchy;
                            }
                        }
                        else
                        {
                            // Fallback to existing TryClickInteractPrompt
                            if (this.TryClickInteractPrompt())
                            {
                                attempted = true;
                                this.lastAutoSwingTime = Time.time;
                                this.awaitingSwingConfirm = true;
                                this.swingConfirmDeadline = Time.time + 0.9f;
                                this.swingConfirmStartAnimHash = 0;
                                GameObject swingBtnObj2 = GameObject.Find(this.swingButtonPath);
                                if (swingBtnObj2 != null)
                                {
                                    Button bb2 = swingBtnObj2.GetComponent<Button>();
                                    this.swingConfirmStartBtnInteract = (bb2 != null) ? bb2.interactable : swingBtnObj2.activeInHierarchy;
                                }
                            }
                        }

                        if (!attempted)
                        {
                            this.treeFarmNoPromptAttempts++;
                        }
                        else if (!chopped)
                        {
                            // Attempted but no confirmed swing
                            this.treeFarmNoPromptAttempts++;
                        }
                    }

                    MelonLogger.Msg($"[TreeFarm] Chop attempt {this.treeFarmChopSent}/{this.treeFarmChopPressCount} - Success: {chopped}, NoPromptAttempts: {this.treeFarmNoPromptAttempts}");
                    this.treeFarmStatus = chopped
                        ? $"Chopping {this.treeFarmChopSent}/{this.treeFarmChopPressCount}..."
                        : "Waiting for chop prompt...";

                    if (this.treeFarmChopSent >= Math.Max(1, this.treeFarmChopPressCount))
                    {
                        MelonLogger.Msg($"[TreeFarm] Finished chopping at point {this.treeFarmCurrentIndex + 1}, moving to next");
                        // If using hardcoded resource-style mode, mark the closest tree as collected so cooldowns apply
                        if (this.treeFarmUseHardcoded)
                        {
                            try
                            {
                                GameObject playerObj = GameObject.Find("p_player_skeleton(Clone)");
                                if (playerObj != null)
                                {
                                    this.MarkTreeCollected(playerObj.transform.position);
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Msg("[TreeFarm] MarkTreeCollected error: " + ex.Message);
                            }
                        }
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

        // Find the closest index in a positions array within a reasonable radius (squared)
        private int FindClosestItemIndexLocal(Vector3 playerPos, Vector3[] positions)
        {
            int result = -1;
            float bestSqr = 25f; // 5 units
            for (int i = 0; i < positions.Length; i++)
            {
                float sq = (positions[i] - playerPos).sqrMagnitude;
                if (sq < bestSqr)
                {
                    bestSqr = sq;
                    result = i;
                }
            }
            return result;
        }

        // Mark nearest tree (of any type) as collected and start its cooldown/hide timers
        private void MarkTreeCollected(Vector3 playerPos)
        {
            float hideDelay = this.treeHideDelay;

            int idx = this.FindClosestItemIndexLocal(playerPos, TreePositions);
            if (idx >= 0)
            {
                this.treeCooldowns[idx] = Time.time + this.treeCooldownDuration;
                this.treeHideUntil[idx] = Time.time + hideDelay;
                MelonLogger.Msg($"[TreeFarm] Tree #{idx} collected, cooldown {this.treeCooldownDuration}s");
            }

            int idx2 = this.FindClosestItemIndexLocal(playerPos, RareTreePositions);
            if (idx2 >= 0)
            {
                this.rareTreeCooldowns[idx2] = Time.time + this.rareTreeCooldownDuration;
                this.rareTreeHideUntil[idx2] = Time.time + hideDelay;
                MelonLogger.Msg($"[TreeFarm] Rare Tree #{idx2} collected, cooldown {this.rareTreeCooldownDuration}s");
            }

            int idx3 = this.FindClosestItemIndexLocal(playerPos, AppleTreePositions);
            if (idx3 >= 0)
            {
                this.appleTreeCooldowns[idx3] = Time.time + this.appleTreeCooldownDuration;
                this.appleTreeHideUntil[idx3] = Time.time + hideDelay;
                MelonLogger.Msg($"[TreeFarm] Apple Tree #{idx3} collected, cooldown {this.appleTreeCooldownDuration}s");
            }

            int idx4 = this.FindClosestItemIndexLocal(playerPos, OrangeTreePositions);
            if (idx4 >= 0)
            {
                this.orangeTreeCooldowns[idx4] = Time.time + this.orangeTreeCooldownDuration;
                this.orangeTreeHideUntil[idx4] = Time.time + hideDelay;
                MelonLogger.Msg($"[TreeFarm] Mandarin Tree #{idx4} collected, cooldown {this.orangeTreeCooldownDuration}s");
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
                        fullPath.Contains("/CommonIconForTool(Clone)/") &&
                        fullPath.Contains("/icon@img@btn"))
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
                                fullPath.Contains("/CommonIconForTool(Clone)/") &&
                                fullPath.Contains("/icon@img@btn"))
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
            // Try to trigger known in-game joystick/trigger objects first (most reliable)
            try
            {
                string[] triggerCandidates = new string[] { "GatherSelectWidget", "skill_main_hold@go@w", "main_joy@go@w" };
                foreach (string candidate in triggerCandidates)
                {
                    if (this.TryActivateTriggerByName(candidate))
                    {
                        MelonLogger.Msg($"[TreeFarm] Activated trigger '{candidate}'");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[TreeFarm] Trigger scan error: {ex.Message}");
            }

            // Fallback: try the tracking panel interact button
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
                                continue;
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
                MelonLogger.Msg($"[TreeFarm] Error searching tracking panel: {ex.Message}");
            }

            // Try fixed path interact button
            if (this.ClickButtonIfExistsReturn(INTERACT_PROMPT_BUTTON_PATH))
            {
                MelonLogger.Msg("[TreeFarm] Clicked interact button via path");
                return true;
            }

            // Try swing button
            if (this.ClickButtonIfExistsReturn("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_sand_swing@go@w/root_visible@go/swing@btn"))
            {
                MelonLogger.Msg("[TreeFarm] Clicked swing button for interaction");
                return true;
            }

            // Last resort: send the F key simulation
            MelonLogger.Msg("[TreeFarm] No UI trigger found, sending F key");
            this.SendFMessage();
            return true;
        }

        private bool TryActivateTriggerByName(string partialName)
        {
            // Use EventSystem.current; if none exists, we cannot simulate UI clicks safely
            if (EventSystem.current == null)
            {
                MelonLogger.Msg("[Trigger] EventSystem.current is null; cannot activate UI triggers.");
                return false;
            }

            try
            {
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj == null) continue;
                    if (!obj.activeInHierarchy) continue;
                    if (obj.name == null) continue;
                    if (!obj.name.Contains(partialName)) continue;

                    MelonLogger.Msg($"[Trigger] Found object matching '{partialName}': {obj.name} - attempting activation");

                    // If object has a Button component, invoke it directly
                    Button btn = obj.GetComponent<Button>();
                    if (btn == null) btn = obj.GetComponentInParent<Button>();
                    if (btn != null && btn.interactable && btn.gameObject.activeInHierarchy)
                    {
                        try { btn.onClick.Invoke(); }
                        catch { }
                        return true;
                    }

                    // Otherwise simulate pointer events
                    var pointer = new PointerEventData(EventSystem.current);
                    ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerEnterHandler);
                    ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerClickHandler);
                    ExecuteEvents.Execute(obj, pointer, ExecuteEvents.beginDragHandler);
                    return true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[Trigger] Error activating '{partialName}': {ex.Message}");
            }
            return false;
        }

        private bool CanHarvestTree()
        {
            // Removed world-object checks — allow auto-chop unconditionally
            // This will let the bot attempt swings at each point regardless of unknown in-game object names
            return true;
        }

        private bool PerformAutoSwing()
        {
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (var obj in all)
                {
                    if (obj == null) continue;
                    if (!obj.activeInHierarchy) continue;
                    if (string.IsNullOrEmpty(obj.name)) continue;
                    string n = obj.name.ToLowerInvariant();
                    if (!(n.Contains("main_joy@go@w") || n.Contains("skill_main_hold@go@w") || n.Contains("gatherselectwidget"))) continue;

                    MelonLogger.Msg($"[TreeFarm] PerformAutoSwing found trigger object: {obj.name}");

                    Button btn = obj.GetComponent<Button>();
                    if (btn == null) btn = obj.GetComponentInParent<Button>();
                    if (btn != null && btn.interactable && btn.gameObject.activeInHierarchy)
                    {
                        try { btn.onClick.Invoke(); } catch { }
                        return true;
                    }

                    if (EventSystem.current != null)
                    {
                        var pointer = new PointerEventData(EventSystem.current);
                        ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerEnterHandler);
                        ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerDownHandler);
                        ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerUpHandler);
                        ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerClickHandler);
                        ExecuteEvents.Execute(obj, pointer, ExecuteEvents.beginDragHandler);
                        return true;
                    }
                }

                // If no UI trigger was found, always try swing button or F key as fallback
                // Try swing button path
                if (this.ClickButtonIfExistsReturn(this.swingButtonPath))
                {
                    MelonLogger.Msg("[TreeFarm] Performed fallback swing by clicking swing button");
                    return true;
                }
                // Last resort: send F
                MelonLogger.Msg("[TreeFarm] No UI trigger — sending F key as fallback");
                this.SendFMessage();
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("[TreeFarm] PerformAutoSwing error: " + ex.Message);
            }
            return false;
        }

        // Removed blocking WaitForSwingConfirm in favor of non-blocking polling handled in RunTreeFarmLogic

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
                            bool flagEventFiddlehead = this.showFiddleheadRadar;
                            bool flagEventTallMustard = this.showTallMustardRadar;
                            bool flagEventBurdock = this.showBurdockRadar;
                            bool flagEventMustardGreens = this.showMustardGreensRadar;
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
                                            else if (farmLocation2.Type == "event_fiddlehead" && flagEventFiddlehead)
                                            {
                                                flag13 = true;
                                            }
                                            else if (farmLocation2.Type == "event_tall_mustard" && flagEventTallMustard)
                                            {
                                                flag13 = true;
                                            }
                                            else if (farmLocation2.Type == "event_burdock" && flagEventBurdock)
                                            {
                                                flag13 = true;
                                            }
                                            else if (farmLocation2.Type == "event_mustard_greens" && flagEventMustardGreens)
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
                                    bool flag10 = (this.showMushroomRadar && (text.Contains("Mushroom") || text.Contains("Oyster") || text.Contains("Button") || text.Contains("Penny Bun") || text.Contains("Shiitake") || text.Contains("Truffle")))
                                        || (this.showFiddleheadRadar && text.Contains("Fiddlehead"))
                                        || (this.showTallMustardRadar && text.Contains("Tall Mustard"))
                                        || (this.showBurdockRadar && text.Contains("Burdock"))
                                        || (this.showMustardGreensRadar && text.Contains("Mustard Greens"));
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
                                            else if (text.Contains("Stone") && this.showStoneRadar)
                                            {
                                                flag9 = true;
                                            }
                                            else if (text.Contains("Ore") && this.showOreRadar)
                                            {
                                                flag9 = true;
                                            }
                                            else if (text.Contains("Tree") && this.showTreeRadar)
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
                                this.priorityShiitake || this.priorityTruffle || this.priorityFiddlehead || this.priorityTallMustard || this.priorityBurdock || this.priorityMustardGreens || this.priorityBlueberry ||
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
                else if (this.priorityFiddlehead && text.Contains("Fiddlehead"))
                    isPriorityMatch = true;
                else if (this.priorityTallMustard && (text.Contains("Tall Mustard") || text.Contains("Mustard")))
                    isPriorityMatch = true;
                else if (this.priorityBurdock && text.Contains("Burdock"))
                    isPriorityMatch = true;
                else if (this.priorityMustardGreens && text.Contains("Mustard Greens"))
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
            if (text.Contains("Fiddlehead")) return this.priorityLocations["Fiddlehead"];
            if (text.Contains("Tall Mustard")) return this.priorityLocations["Tall Mustard"];
            if (text.Contains("Burdock")) return this.priorityLocations["Burdock"];
            if (text.Contains("Mustard Greens")) return this.priorityLocations["Mustard Greens"];
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
                    this.priorityLocationCooldowns.Remove(this.currentPriorityLocation.Value);
                }
                else
                {
                    this.priorityLocationCooldowns[this.currentPriorityLocation.Value] = Time.unscaledTime;
                    this.currentPriorityLocation = null;
                }
            }

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
            if (loc == this.priorityLocations["Fiddlehead"]) return "Fiddlehead";
            if (loc == this.priorityLocations["Tall Mustard"]) return "Tall Mustard";
            if (loc == this.priorityLocations["Burdock"]) return "Burdock";
            if (loc == this.priorityLocations["Mustard Greens"]) return "Mustard Greens";
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
            if (this.priorityFiddlehead) newActive.Add(this.priorityLocations["Fiddlehead"]);
            if (this.priorityTallMustard) newActive.Add(this.priorityLocations["Tall Mustard"]);
            if (this.priorityBurdock) newActive.Add(this.priorityLocations["Burdock"]);
            if (this.priorityMustardGreens) newActive.Add(this.priorityLocations["Mustard Greens"]);
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

        private bool AnyRadarLootToggleEnabled()
        {
            return this.showMushroomRadar || this.showFiddleheadRadar || this.showTallMustardRadar || this.showBurdockRadar || this.showMustardGreensRadar
                || this.showBlueberryRadar || this.showRaspberryRadar || this.showStoneRadar || this.showOreRadar
                || this.showTreeRadar || this.showRareTreeRadar || this.showAppleTreeRadar || this.showOrangeTreeRadar
                || this.showBubbleRadar || this.showInsectRadar || this.showFishShadowRadar;
        }

        private bool ShouldShowForageMesh(string forageText)
        {
            if (forageText.Contains("pleurotus") || forageText.Contains("tricholoma") || forageText.Contains("boletus") || forageText.Contains("shiitake") || forageText.Contains("truffle"))
            {
                return this.showMushroomRadar;
            }

            if (forageText.Contains("fiddlehead") || forageText.Contains("fiddle") || forageText.Contains("fern") || forageText.Contains("pterid") || forageText.Contains("bracken"))
            {
                return this.showFiddleheadRadar;
            }

            if (forageText.Contains("burdock"))
            {
                return this.showBurdockRadar;
            }

            if (forageText.Contains("shepherdspurse") || ((forageText.Contains("mustard") && forageText.Contains("green"))) || forageText.Contains("mustard greens") || forageText.Contains("mustardgreens") || forageText.Contains("mustard_green") || forageText.Contains("mustardgreen") || forageText.Contains("greens"))
            {
                return this.showMustardGreensRadar;
            }

            if (forageText.Contains("tall mustard") || forageText.Contains("tallmustard") || forageText.Contains("mustard"))
            {
                return this.showTallMustardRadar;
            }

            return this.showMushroomRadar;
        }

        // Token: 0x06000018 RID: 24 RVA: 0x00004964 File Offset: 0x00002B64
        private void CheckRadarAutoToggle()
        {
            bool flag = this.AnyRadarLootToggleEnabled();
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
            bool flag = this.AnyRadarLootToggleEnabled();
            
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
                this.priorityRecheckTimer = 0f; // Reset recheck timer

                int autoStopSeconds = this.GetAutoFarmAutoStopSeconds();
                if (this.autoFarmAutoStopEnabled && autoStopSeconds > 0)
                {
                    this.autoFarmAutoStopAt = Time.unscaledTime + autoStopSeconds;
                    this.AddMenuNotification("Auto Farm auto-stop set: " + this.FormatDurationHms(autoStopSeconds), new Color(0.55f, 0.88f, 1f));
                }
                else
                {
                    this.autoFarmAutoStopAt = -1f;
                }

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
                this.autoFarmAutoStopAt = -1f;
                MelonLogger.Msg("[AUTO FARM] Disabled");
            }
        }

        private int GetAutoFarmAutoStopSeconds()
        {
            return Math.Max(0, this.autoFarmAutoStopHours) * 3600
                + Math.Max(0, this.autoFarmAutoStopMinutes) * 60
                + Math.Max(0, this.autoFarmAutoStopSeconds);
        }

        private int GetAutoCookAutoStopSeconds()
        {
            return Math.Max(0, this.autoCookAutoStopHours) * 3600
                + Math.Max(0, this.autoCookAutoStopMinutes) * 60
                + Math.Max(0, this.autoCookAutoStopSeconds);
        }

        private int GetAutoResourceFarmAutoStopSeconds()
        {
            return Math.Max(0, this.autoResourceFarmAutoStopHours) * 3600
                + Math.Max(0, this.autoResourceFarmAutoStopMinutes) * 60
                + Math.Max(0, this.autoResourceFarmAutoStopSeconds);
        }

        private int GetAutoFishFarmAutoStopSeconds()
        {
            return Math.Max(0, this.autoFishFarmAutoStopHours) * 3600
                + Math.Max(0, this.autoFishFarmAutoStopMinutes) * 60
                + Math.Max(0, this.autoFishFarmAutoStopSeconds);
        }

        public string FormatDurationHms(int totalSeconds)
        {
            totalSeconds = Math.Max(0, totalSeconds);
            int h = totalSeconds / 3600;
            int m = (totalSeconds % 3600) / 60;
            int s = totalSeconds % 60;
            return h.ToString("00") + ":" + m.ToString("00") + ":" + s.ToString("00");
        }

        // Token: 0x0600001A RID: 26 RVA: 0x00004B54 File Offset: 0x00002D54
        public void RunRadar()
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
                }
            }
            bool flagRockScan = this.showStoneRadar;
            if (flagRockScan)
            {
                float unscaledRock = Time.unscaledTime;
                for (int r = 0; r < HeartopiaComplete.RockPositions.Length; r++)
                {
                    Vector3 rockPos = HeartopiaComplete.RockPositions[r];
                    if (Vector3.Distance(position, rockPos) <= 80f)
                    {
                        bool onCD = this.rockCooldowns.ContainsKey(r) && unscaledRock < this.rockCooldowns[r];
                        bool hidden = this.rockHideUntil.ContainsKey(r) && unscaledRock < this.rockHideUntil[r];
                        if (onCD && (!this.rockHideUntil.ContainsKey(r) || unscaledRock >= this.rockHideUntil[r]))
                        {
                            this.CreateMarker(rockPos, "stone_cooldown", material, material2, null);
                        }
                        else if (!onCD && !hidden)
                        {
                            this.CreateMarker(rockPos, "stone", material, material2, null);
                        }
                    }
                }
            }
            bool flagTreeScan = this.showTreeRadar;
            if (flagTreeScan)
            {
                float unscaledTree = Time.unscaledTime;
                for (int tIdx = 0; tIdx < HeartopiaComplete.TreePositions.Length; tIdx++)
                {
                    Vector3 treePos = HeartopiaComplete.TreePositions[tIdx];
                    if (Vector3.Distance(position, treePos) <= 80f)
                    {
                        bool onCDt = this.treeCooldowns_res.ContainsKey(tIdx) && unscaledTree < this.treeCooldowns_res[tIdx];
                        bool hiddent = this.treeHideUntil_res.ContainsKey(tIdx) && unscaledTree < this.treeHideUntil_res[tIdx];
                        if (onCDt && (!this.treeHideUntil_res.ContainsKey(tIdx) || unscaledTree >= this.treeHideUntil_res[tIdx]))
                        {
                            this.CreateMarker(treePos, "tree_cooldown", material, material2, null);
                        }
                        else if (!onCDt && !hiddent)
                        {
                            this.CreateMarker(treePos, "tree", material, material2, null);
                        }
                    }
                }
            }
            bool flagRareTreeScan = this.showRareTreeRadar;
            if (flagRareTreeScan)
            {
                float unscaledRare = Time.unscaledTime;
                for (int rt = 0; rt < HeartopiaComplete.RareTreePositions.Length; rt++)
                {
                    Vector3 rarePos = HeartopiaComplete.RareTreePositions[rt];
                    if (Vector3.Distance(position, rarePos) <= 80f)
                    {
                        bool onCD = this.rareTreeCooldowns_res.ContainsKey(rt) && unscaledRare < this.rareTreeCooldowns_res[rt];
                        bool hidden = this.rareTreeHideUntil_res.ContainsKey(rt) && unscaledRare < this.rareTreeHideUntil_res[rt];
                        if (onCD && (!this.rareTreeHideUntil_res.ContainsKey(rt) || unscaledRare >= this.rareTreeHideUntil_res[rt]))
                        {
                            this.CreateMarker(rarePos, "rare_tree_cooldown", material, material2, null);
                        }
                        else if (!onCD && !hidden)
                        {
                            this.CreateMarker(rarePos, "rare_tree", material, material2, null);
                        }
                    }
                }
            }
            bool flagAppleScan = this.showAppleTreeRadar;
            if (flagAppleScan)
            {
                float unscaledApple = Time.unscaledTime;
                for (int a = 0; a < HeartopiaComplete.AppleTreePositions.Length; a++)
                {
                    Vector3 applePos = HeartopiaComplete.AppleTreePositions[a];
                    if (Vector3.Distance(position, applePos) <= 80f)
                    {
                        bool onCDa = this.appleTreeCooldowns_res.ContainsKey(a) && unscaledApple < this.appleTreeCooldowns_res[a];
                        bool hid = this.appleTreeHideUntil_res.ContainsKey(a) && unscaledApple < this.appleTreeHideUntil_res[a];
                        if (onCDa && (!this.appleTreeHideUntil_res.ContainsKey(a) || unscaledApple >= this.appleTreeHideUntil_res[a]))
                        {
                            this.CreateMarker(applePos, "apple_tree_cooldown", material, material2, null);
                        }
                        else if (!onCDa && !hid)
                        {
                            this.CreateMarker(applePos, "apple_tree", material, material2, null);
                        }
                    }
                }
            }
            bool flagOrangeScan = this.showOrangeTreeRadar;
            if (flagOrangeScan)
            {
                float unscaledOrange = Time.unscaledTime;
                for (int oT = 0; oT < HeartopiaComplete.OrangeTreePositions.Length; oT++)
                {
                    Vector3 orangePos = HeartopiaComplete.OrangeTreePositions[oT];
                    if (Vector3.Distance(position, orangePos) <= 80f)
                    {
                        bool onCDo = this.orangeTreeCooldowns_res.ContainsKey(oT) && unscaledOrange < this.orangeTreeCooldowns_res[oT];
                        bool hidO = this.orangeTreeHideUntil_res.ContainsKey(oT) && unscaledOrange < this.orangeTreeHideUntil_res[oT];
                        if (onCDo && (!this.orangeTreeHideUntil_res.ContainsKey(oT) || unscaledOrange >= this.orangeTreeHideUntil_res[oT]))
                        {
                            this.CreateMarker(orangePos, "orange_tree_cooldown", material, material2, null);
                        }
                        else if (!onCDo && !hidO)
                        {
                            this.CreateMarker(orangePos, "orange_tree", material, material2, null);
                        }
                    }
                }
            }
            bool flagOreScan = this.showOreRadar;
            if (flagOreScan)
            {
                float unscaledOre = Time.unscaledTime;
                for (int o = 0; o < HeartopiaComplete.OrePositions.Length; o++)
                {
                    Vector3 orePos = HeartopiaComplete.OrePositions[o];
                    if (Vector3.Distance(position, orePos) <= 80f)
                    {
                        bool onCD = this.oreCooldowns.ContainsKey(o) && unscaledOre < this.oreCooldowns[o];
                        bool hidden = this.oreHideUntil.ContainsKey(o) && unscaledOre < this.oreHideUntil[o];
                        if (onCD && (!this.oreHideUntil.ContainsKey(o) || unscaledOre >= this.oreHideUntil[o]))
                        {
                            this.CreateMarker(orePos, "ore_cooldown", material, material2, null);
                        }
                        else if (!onCD && !hidden)
                        {
                            this.CreateMarker(orePos, "ore", material, material2, null);
                        }
                    }
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
                }
            }
            // Single combined scan for bubble / insect / fish-shadow radar
            if (this.showBubbleRadar || this.showInsectRadar || this.showFishShadowRadar)
            {
                GameObject[] radarScan = Object.FindObjectsOfType<GameObject>();

                if (this.showBubbleRadar)
                {
                    foreach (GameObject gameObject3 in radarScan)
                    {
                        if (gameObject3 == null || gameObject3.name == null) continue;
                        string text = gameObject3.name.ToLower();
                        if (text.Contains("p_bubble_bubble_") && text.Contains("(clone)"))
                        {
                            int instanceID = gameObject3.GetInstanceID();
                            if (!this.trackedObjectMarkers.ContainsKey(instanceID))
                            {
                                this.CreateMarker(gameObject3.transform.position, "bubble", material, material2, gameObject3);
                                this.trackedObjectMarkers[instanceID] = this.markerToTarget.Keys.Last<GameObject>();
                            }
                        }
                    }
                }

                if (this.showInsectRadar)
                {
                    List<int> list3 = new List<int>();
                    foreach (KeyValuePair<int, GameObject> keyValuePair2 in this.trackedObjectMarkers)
                    {
                        if (keyValuePair2.Value == null) list3.Add(keyValuePair2.Key);
                    }
                    foreach (int key2 in list3) this.trackedObjectMarkers.Remove(key2);

                    foreach (GameObject gameObject4 in radarScan)
                    {
                        if (gameObject4 == null || gameObject4.name == null) continue;
                        string text2 = gameObject4.name.ToLower();
                        if (text2.Contains("p_insect_insect") && text2.Contains("(clone)"))
                        {
                            int instanceID2 = gameObject4.GetInstanceID();
                            if (!this.trackedObjectMarkers.ContainsKey(instanceID2))
                            {
                                this.CreateMarker(gameObject4.transform.position, "insect", material, material2, gameObject4);
                                this.trackedObjectMarkers[instanceID2] = this.markerToTarget.Keys.Last<GameObject>();
                            }
                        }
                    }
                }

                if (this.showFishShadowRadar)
                {
                    List<int> list5 = new List<int>();
                    foreach (KeyValuePair<int, GameObject> keyValuePair3 in this.trackedObjectMarkers)
                    {
                        if (keyValuePair3.Value == null) list5.Add(keyValuePair3.Key);
                    }
                    foreach (int key3 in list5) this.trackedObjectMarkers.Remove(key3);

                    foreach (GameObject gameObject5 in radarScan)
                    {
                        if (gameObject5 == null || gameObject5.name == null) continue;
                        string text3 = gameObject5.name.ToLower();
                        if (text3.Contains("fishshadow") && text3.Contains("(clone)"))
                        {
                            int instanceID3 = gameObject5.GetInstanceID();
                            if (!this.trackedObjectMarkers.ContainsKey(instanceID3))
                            {
                                this.CreateMarker(gameObject5.transform.position, "fishshadow", material, material2, gameObject5);
                                this.trackedObjectMarkers[instanceID3] = this.markerToTarget.Keys.Last<GameObject>();
                            }
                        }
                    }
                }
            }
            bool flag34 = object2 != null;
            if (this.showMushroomRadar || this.showFiddleheadRadar || this.showTallMustardRadar || this.showBurdockRadar || this.showMustardGreensRadar)
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
                                    string forageText = meshName.ToLower();
                                    bool flag31 = forageText.Contains("dynamicbush");
                                    if (flag31)
                                    {
                                        if (!this.ShouldShowForageMesh(forageText))
                                        {
                                            continue;
                                        }
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
            bool flag = meshName.Contains("step0") || meshName.Contains("_cooldown") || meshName == "blueberry_cooldown" || meshName == "raspberry_cooldown";
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
                            bool flagRareTree = meshName == "rare_tree" || meshName == "rare_tree_cooldown";
                            if (flagRareTree)
                            {
                                text2 = "Rare Tree";
                                icon = "★";
                                endColor = new Color(1f, 0.84f, 0f);
                                bgColor = new Color(0.6f, 0.45f, 0.05f, 0.86f);
                            }
                            bool flagApple = meshName == "apple_tree" || meshName == "apple_tree_cooldown";
                            if (flagApple)
                            {
                                text2 = "Apple Tree";
                                icon = "🍎";
                                endColor = new Color(1f, 0.45f, 0.45f);
                                bgColor = new Color(0.15f, 0.35f, 0.1f, 0.85f);
                            }
                            bool flagOrange = meshName == "orange_tree" || meshName == "orange_tree_cooldown";
                            if (flagOrange)
                            {
                                text2 = "Mandarin Tree";
                                icon = "🍊";
                                endColor = new Color(1f, 0.7f, 0.35f);
                                bgColor = new Color(0.35f, 0.18f, 0.05f, 0.85f);
                            }
                            bool flagTree = meshName == "tree" || meshName == "tree_cooldown";
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
                                bool flagRockType = meshName == "stone" || meshName == "stone_cooldown";
                                if (flagRockType)
                                {
                                    text2 = "Stone";
                                    icon = "◯";
                                    endColor = new Color(0.7f, 0.7f, 0.7f);
                                    bgColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);
                                }
                                bool flagOreType = meshName == "ore" || meshName == "ore_cooldown";
                                if (flagOreType)
                                {
                                    text2 = "Ore";
                                    icon = "◆";
                                    endColor = new Color(0.8f, 0.6f, 0.4f); // brownish
                                    bgColor = new Color(0.25f, 0.18f, 0.1f, 0.85f);
                                }
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
                                                else if (text.Contains("fiddlehead") || text.Contains("fiddle") || text.Contains("fern") || text.Contains("pterid") || text.Contains("bracken"))
                                                {
                                                    text2 = "Fiddlehead";
                                                    icon = "🌿";
                                                    endColor = new Color(0.6f, 0.95f, 0.6f);
                                                    bgColor = new Color(0.15f, 0.45f, 0.15f, 0.85f);
                                                }
                                                else if (text.Contains("burdock"))
                                                {
                                                    text2 = "Burdock";
                                                    icon = "◆";
                                                    endColor = new Color(0.86f, 0.72f, 0.48f);
                                                    bgColor = new Color(0.38f, 0.26f, 0.12f, 0.85f);
                                                }
                                                else if (text.Contains("shepherdspurse") || (text.Contains("mustard") && text.Contains("green")) || text.Contains("mustard greens") || text.Contains("mustardgreens") || text.Contains("mustard_green") || text.Contains("mustardgreen") || text.Contains("greens"))
                                                {
                                                    text2 = "Mustard Greens";
                                                    icon = "🍃";
                                                    endColor = new Color(0.58f, 0.95f, 0.52f);
                                                    bgColor = new Color(0.14f, 0.42f, 0.14f, 0.85f);
                                                }
                                                else if (text.Contains("tall mustard") || text.Contains("tallmustard") || text.Contains("mustard"))
                                                {
                                                    text2 = "Tall Mustard";
                                                    icon = "🍃";
                                                    endColor = new Color(0.7f, 1f, 0.5f);
                                                    bgColor = new Color(0.2f, 0.45f, 0.12f, 0.85f);
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
            if (text2 == "Mushroom" && text.Contains("dynamicbush") && !this.loggedUnknownForageMeshNames.Contains(meshName))
            {
                this.loggedUnknownForageMeshNames.Add(meshName);
                MelonLogger.Msg("[RadarDebug] Unmapped forage mesh: " + meshName);
            }
            bool flag11 = flag;
            if (flag11)
            {
                endColor = new Color(1f, 0.3f, 0.3f); // Red for cooldown
                bgColor = new Color(0.5f, 0.05f, 0.05f, 0.9f); // Dark red background
            }
            // If user selected simple text markers, create a minimal label-only marker with a circle
            if (this.radarMarkerStyle == 1)
            {
                GameObject simpleMarker = new GameObject("ItemMarker");
                simpleMarker.transform.position = pos;
                simpleMarker.transform.SetParent(this.radarContainer.transform);
                if (targetObject != null)
                {
                    simpleMarker.name = "TrackedMarker_" + targetObject.GetInstanceID().ToString();
                    this.markerToTarget[simpleMarker] = targetObject;
                }

                // Draw a simple circular ground marker using LineRenderer
                LineRenderer circle = simpleMarker.AddComponent<LineRenderer>();
                circle.material = xRay;
                circle.useWorldSpace = false;
                circle.startWidth = (circle.endWidth = 0.08f);
                int segments = 48;
                circle.positionCount = segments + 1;
                Color circleColor = endColor;
                circleColor.a = 0.85f;
                circle.startColor = (circle.endColor = circleColor);
                float radius = 0.8f;
                for (int s = 0; s <= segments; s++)
                {
                    float t = (float)s / (float)segments * 2f * 3.1415927f;
                    float x = Mathf.Cos(t) * radius;
                    float z = Mathf.Sin(t) * radius;
                    circle.SetPosition(s, new Vector3(x, 0.1f, z));
                }

                GameObject anchorSimple = new GameObject("LabelAnchor");
                anchorSimple.transform.SetParent(simpleMarker.transform);
                anchorSimple.transform.localPosition = new Vector3(0f, 1.8f, 0f);
                GameObject textGoSimple = new GameObject("Text");
                TextMesh textMeshSimple = textGoSimple.AddComponent<TextMesh>();
                textGoSimple.transform.SetParent(anchorSimple.transform);
                textGoSimple.transform.localPosition = Vector3.zero;
                textMeshSimple.text = text2; // name only; UpdateMarkers will append distance
                textMeshSimple.color = endColor;
                textMeshSimple.fontStyle = (FontStyle)1;
                textMeshSimple.fontSize = 85;
                textMeshSimple.characterSize = 0.06f;
                textMeshSimple.anchor = (TextAnchor)4;
                return;
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
                    this.liveCameraFOVBase = this.originalFOV;
                }
            }

            if (this.mainCamera != null)
            {
                float currentFOV = this.mainCamera.fieldOfView;
                if (this.liveCameraFOVBase < 0f)
                {
                    this.liveCameraFOVBase = currentFOV;
                }

                if (this.lastAppliedCustomCameraFOV < 0f || Mathf.Abs(currentFOV - this.lastAppliedCustomCameraFOV) > 0.05f)
                {
                    this.liveCameraFOVBase = currentFOV;
                }

                float customOffset = (this.originalFOV >= 0f) ? (this.cameraFOV - this.originalFOV) : 0f;
                float targetFOV = this.liveCameraFOVBase + customOffset;
                if (Mathf.Abs(currentFOV - targetFOV) > 0.01f)
                {
                    this.mainCamera.fieldOfView = targetFOV;
                }

                this.lastAppliedCustomCameraFOV = targetFOV;
            }
        }

        private void RestoreCameraFOV()
        {
            if (this.mainCamera == null || !this.mainCamera)
            {
                this.mainCamera = Camera.main;
            }

            if (this.mainCamera != null)
            {
                float restoreFOV = this.liveCameraFOVBase >= 0f ? this.liveCameraFOVBase : this.originalFOV;
                if (restoreFOV >= 0f)
                {
                    this.mainCamera.fieldOfView = restoreFOV;
                }
            }

            this.lastAppliedCustomCameraFOV = -1f;
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

                    // Copy position button: copies to clipboard and populates XYZ inputs
                    if (GUI.Button(new Rect(20f, (float)num, 140f, 28f), "Copy Position"))
                    {
                        GUIUtility.systemCopyBuffer = string.Format("{0:F3},{1:F3},{2:F3}", p.x, p.y, p.z);
                        this.customTPX = p.x.ToString("F3");
                        this.customTPY = p.y.ToString("F3");
                        this.customTPZ = p.z.ToString("F3");
                        this.AddMenuNotification("Current position copied to clipboard and XYZ fields", new Color(0.55f, 0.88f, 1f));
                    }

                    num += 34;
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
            while (isCookingPatrolActive && cookingPatrolEnabled)
            {
                if (cookingPatrolPoints.Count == 0) break;

                CookingPatrolPoint point = cookingPatrolPoints[index];

                // 1. TELEPORT to location
                TeleportTo(point.Position.ToVector3());

                // 2. APPLY CHARACTER ROTATION
                Quaternion targetRotation = point.Rotation.ToQuaternion();
                HeartopiaComplete.OverridePlayerRotation = true;
                HeartopiaComplete.PlayerOverrideRot = targetRotation;
                this.playerRotationFramesRemaining = 100;

                // 3. WAIT at spot
                yield return new WaitForSecondsRealtime(cookingWaitAtSpot);

                // 4. Disable rotation override before moving to next point
                HeartopiaComplete.OverridePlayerRotation = false;
                this.playerRotationFramesRemaining = 0;

                // 5. NEXT POINT
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
            // Prefer using simulated F-key flags (safer for in-game automation); fallback to native input if needed
            try
            {
                this.SimulateFKeyPulse(0.12f);
                return;
            }
            catch { }
            try
            {
                // Use SendInput for better compatibility if simulation fails
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

        private void SimulateFKeyPulse(float holdSeconds = 0.12f)
        {
            try
            {
                HeartopiaComplete.SimulateFKeyDown = true;
                HeartopiaComplete.SimulateFKeyHeld = true;
                HeartopiaComplete.SimulateFKeyUp = false;
                this.nextSimulatedFKeyClearAt = Time.unscaledTime + Mathf.Max(0.02f, holdSeconds);
            }
            catch { }
        }

        // Directly simulate an interact (F) press and try to click in-game interact buttons
        private void DirectClickInteractButton()
        {
            try
            {
                try
                {
                    IntPtr hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                    if (hwnd == IntPtr.Zero)
                    {
                        hwnd = FindWindow("UnityWndClass", null);
                    }
                    if (hwnd != IntPtr.Zero)
                    {
                        IntPtr lParamDown = new IntPtr(2162689);
                        IntPtr lParamUp = new IntPtr(-1071579135);
                        PostMessage(hwnd, 256U, new IntPtr(70), lParamDown);
                        PostMessage(hwnd, 257U, new IntPtr(70), lParamUp);
                    }
                }
                catch {}

                string[] paths = new string[] {
                    "GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/middle_right_layout@go/skill_bar@w@go/skill_bar@go/main_joy@go@w/Joy@ani",
                    "GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/middle_right_layout@go/skill_bar@w@go/skill_bar@go/main_joy@go@w/Joy@ani/stick@frame/normal",
                    "GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/right_layout@ani/middle_right_layout@go/skill_bar@w@go/skill_bar@go/main_joy@go@w",
                    "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_chop@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn",
                    "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_mine@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn",
                    "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn",
                    "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/quick_action@btn",
                    "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_harvest@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn"
                };
                foreach (string p in paths)
                {
                    GameObject btn = GameObject.Find(p);
                    if (btn != null && btn.activeInHierarchy)
                    {
                        DirectClickGameButton(btn);
                    }
                }
            }
            catch {}
        }

        private void DirectClickGameButton(GameObject buttonObj)
        {
            try
            {
                RectTransform rt = buttonObj.GetComponent<RectTransform>();
                Vector2 pos = Vector2.zero;
                if (rt != null)
                {
                    Vector3 worldPos = rt.position;
                    pos = new Vector2(worldPos.x, worldPos.y);
                }
                var pointer = new PointerEventData(EventSystem.current);
                pointer.button = PointerEventData.InputButton.Left;
                pointer.position = pos;
                pointer.pressPosition = pos;
                pointer.pointerPress = buttonObj;
                pointer.rawPointerPress = buttonObj;
                pointer.pointerEnter = buttonObj;
                pointer.clickCount = 1;
                pointer.eligibleForClick = true;
                ExecuteEvents.Execute<IPointerEnterHandler>(buttonObj, pointer, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute<IPointerDownHandler>(buttonObj, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute<IPointerUpHandler>(buttonObj, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute<IPointerClickHandler>(buttonObj, pointer, ExecuteEvents.pointerClickHandler);
                Button b = buttonObj.GetComponent<Button>();
                if (b != null && b.interactable)
                {
                    try { b.onClick.Invoke(); } catch {}
                }
            }
            catch {}
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

        private void RunAntiAfkTick()
        {
            if (!this.antiAfkEnabled) return;
            if (this.showMenu) return;
            if (Time.unscaledTime - this.lastAntiAfkPulseAt < Mathf.Max(5f, this.antiAfkInterval)) return;

            this.lastAntiAfkPulseAt = Time.unscaledTime;
            AutoFishLogic.SimulateMouseButton0Down = true;
            AutoFishLogic.SimulateMouseButton0 = true;
            this.antiAfkMouseDownClearAt = Time.unscaledTime + 0.05f;
            this.antiAfkMouseHoldClearAt = Time.unscaledTime + 0.12f;
        }


        // Cached local player lookup to avoid expensive per-frame scans.
        private static GameObject cachedLocalPlayer = null;
        private static float lastLocalPlayerCheckTime = -999f;
        private const float LOCAL_PLAYER_CACHE_INTERVAL = 1f; // seconds

        // Return what appears to be the local player's skeleton GameObject.
        // In multiplayer there may be multiple "p_player_skeleton(Clone)" objects; prefer the one that has a Camera in its children (local player).
        public static GameObject GetLocalPlayer()
        {
            // Quick return if cached and valid
            try
            {
                if (cachedLocalPlayer != null && cachedLocalPlayer.activeInHierarchy)
                {
                    return cachedLocalPlayer;
                }
            }
            catch
            {
                cachedLocalPlayer = null;
            }

            // Throttle full-scans to once per interval
            if (Time.unscaledTime - lastLocalPlayerCheckTime < LOCAL_PLAYER_CACHE_INTERVAL && cachedLocalPlayer != null)
            {
                return cachedLocalPlayer;
            }

            lastLocalPlayerCheckTime = Time.unscaledTime;

            try
            {
                GameObject[] all = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (GameObject obj in all)
                {
                    if (obj == null) continue;
                    if (obj.name != null && obj.name.Contains("p_player_skeleton"))
                    {
                        if (obj.GetComponentInChildren<Camera>() != null)
                        {
                            cachedLocalPlayer = obj;
                            return cachedLocalPlayer;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            // Fallback: return the first matching name (existing behavior)
            cachedLocalPlayer = GameObject.Find("p_player_skeleton(Clone)");
            return cachedLocalPlayer;
        }

        private GameObject GetPlayer() => GetLocalPlayer();

        // Returns the player's root GameObject if available (fallback to GetPlayer)
        private GameObject FindPlayerRoot()
        {
            try
            {
                GameObject p = GetPlayer();
                if (p == null) return null;
                if (p.transform == null) return p;
                Transform root = p.transform.root;
                if (root != null && root.gameObject != null) return root.gameObject;
                return p;
            }
            catch
            {
                return GetPlayer();
            }
        }

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
                if (teleportFramesRemaining <= 0)
                {
                    OverridePlayerPosition = false;
                    try
                    {
                        if (this.isResourceFarmTeleport)
                        {
                            this.OnTeleportArrivedResource();
                        }
                    }
                    catch { }
                }
            }
        }

        // --- Auto Snow Sculpture logic (scans SnowSculpturePanel and clicks lit buttons) ---
        private void RunAutoSnowLogic()
        {
            float unscaledTime = Time.unscaledTime;
            bool hasQueue = this.snowWidgetQueue.Count > 0;
            if (hasQueue)
            {
                if (unscaledTime - this.lastSnowClickTime >= this.snowClickInterval)
                {
                    this.lastSnowClickTime = unscaledTime;
                    GameObject widget = this.snowWidgetQueue.Dequeue();
                    if (widget != null && widget.activeInHierarchy)
                    {
                        this.DirectClickSnowWidget(widget);
                        this.snowClickCount++;
                    }
                }
            }
            else
            {
                if (unscaledTime - this.lastSnowClickTime < this.snowClickInterval) return;
                this.lastSnowClickTime = unscaledTime;
                try
                {
                    GameObject panel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/SnowSculpturePanel(Clone)");
                    if (panel == null || !panel.activeInHierarchy) return;
                    Transform[] all = panel.GetComponentsInChildren<Transform>(true);
                    foreach (Transform t in all)
                    {
                        if (!t.name.Contains("SnowSculptureButtonWidget")) continue;
                        if (!t.gameObject.activeInHierarchy) continue;
                        Transform ani = t.Find("AniRoot@queuegroup");
                        if (ani == null) continue;
                        Transform btn = ani.Find("Button");
                        if (btn == null || !btn.gameObject.activeInHierarchy) continue;
                        Transform light = btn.Find("Light@go");
                        Transform correct = btn.Find("Correct@go");
                        bool hasLight = light != null && light.gameObject.activeInHierarchy;
                        bool hasCorrect = correct != null && correct.gameObject.activeInHierarchy;
                        if (hasLight && !hasCorrect)
                        {
                            this.snowWidgetQueue.Enqueue(t.gameObject);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg("[AutoSnow] Error: " + ex.Message);
                }
            }
        }

        private void DirectClickSnowWidget(GameObject widgetObj)
        {
            try
            {
                RectTransform rt = widgetObj.GetComponent<RectTransform>();
                Vector2 pos = Vector2.zero;
                if (rt != null)
                {
                    Vector3 p = rt.position;
                    pos = new Vector2(p.x, p.y);
                }
                PointerEventData pd = new PointerEventData(EventSystem.current);
                pd.button = PointerEventData.InputButton.Left;
                pd.position = pos;
                pd.pressPosition = pos;
                pd.pointerPress = widgetObj;
                pd.rawPointerPress = widgetObj;
                pd.pointerEnter = widgetObj;
                pd.clickCount = 1;
                pd.eligibleForClick = true;
                GraphicRaycaster gr = widgetObj.GetComponentInParent<GraphicRaycaster>();
                pd.pointerCurrentRaycast = new RaycastResult { gameObject = widgetObj, module = gr, screenPosition = pos };
                pd.pointerPressRaycast = pd.pointerCurrentRaycast;
                ExecuteEvents.Execute<IPointerEnterHandler>(widgetObj, pd, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute<IPointerDownHandler>(widgetObj, pd, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute<IPointerUpHandler>(widgetObj, pd, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute<IPointerClickHandler>(widgetObj, pd, ExecuteEvents.pointerClickHandler);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("[AutoSnow] Widget click error: " + ex.Message);
            }
        }

        // Rapid click routine for the tracking icon in the bottom tracking bar
        private void RunSculptIconRapid()
        {
            try
            {
                float unscaled = Time.unscaledTime;
                if (unscaled - this.lastSculptIconClickTime < this.sculptIconClickInterval) return;
                this.lastSculptIconClickTime = unscaled;
                GameObject icon = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list/CommonIconForInteract(Clone)/root_visible@go/icon@img@btn");
                if (icon == null || !icon.activeInHierarchy) return;
                this.DirectClickSnowWidget(icon);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("[SculptRapid] Error: " + ex.Message);
            }
        }

        // --- Auto Cat Play logic (detect requested skill & click matching slot) ---
        private void RunAutoCatPlayLogic()
        {
            try
            {
                float unscaled = Time.unscaledTime;
                if (unscaled - this.lastCatClickTime < this.catClickInterval) return;
                this.lastCatClickTime = unscaled;

                // 1) Prefer direct tracking icon (question icon on bottom bar)
                GameObject trackingIcon = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/petPlay@go/CommonIconForCatPlayQuestion(Clone)/AniRoot/root_visible@go/GameObject/icon@img@btn");
                string requestedSprite = null;
                if (trackingIcon != null && trackingIcon.activeInHierarchy)
                {
                    var img = trackingIcon.GetComponent<Image>();
                    if (img != null && img.sprite != null) requestedSprite = img.sprite.name;
                    if (trackingIcon.GetComponent<Button>() != null)
                    {
                        trackingIcon.GetComponent<Button>().onClick.Invoke();
                    }
                }

                // 2) Find the Cat Play status panel with skill slots
                GameObject statusPanel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/CatPlayStatusPanel(Clone)");
                if (statusPanel == null || !statusPanel.activeInHierarchy) return;

                // Helper: inspect a skill node and return its icon sprite name and its root GameObject to click
                (string spriteName, GameObject root) InspectSkillNode(string nodePath)
                {
                    Transform t = statusPanel.transform.Find(nodePath);
                    if (t == null) return (null, null);
                    Transform icon = t.Find("AniRoot@ani/icon@img");
                    if (icon == null) icon = t.Find("icon@img");
                    if (icon != null)
                    {
                        Image img = icon.GetComponent<Image>();
                        if (img != null && img.sprite != null) return (img.sprite.name, t.gameObject);
                    }
                    Image anyImg = t.GetComponentInChildren<Image>(true);
                    if (anyImg != null && anyImg.sprite != null) return (anyImg.sprite.name, t.gameObject);
                    return (null, t.gameObject);
                }

                var candidates = new List<(string path, string label)> {
                    ("skill_bar@go/skills/skill_main_hold@go@w", "main"),
                    ("skill_bar@go/skills/skill_2@go@w", "skill2"),
                    ("skill_bar@go/skills/skill_3@go@w", "skill3")
                };

                if (!string.IsNullOrEmpty(requestedSprite))
                {
                    foreach (var c in candidates)
                    {
                        var (spriteName, root) = InspectSkillNode(c.path);
                        if (!string.IsNullOrEmpty(spriteName) && spriteName == requestedSprite)
                        {
                            // Ensure the requested sprite is actually present/visible in the scene (above-cat icon or similar)
                            bool spriteVisible = false;
                            try
                            {
                                var allImages = Object.FindObjectsOfType<Image>();
                                foreach (var im in allImages)
                                {
                                    if (im == null || im.sprite == null) continue;
                                    if (!im.gameObject.activeInHierarchy) continue;
                                    if (im.sprite.name == spriteName) { spriteVisible = true; break; }
                                }
                            }
                            catch { }

                            if (!spriteVisible)
                            {
                                // No visible above-cat icon found for this sprite; skip clicking
                                continue;
                            }

                            if (root != null)
                            {
                                var btn = root.GetComponentInChildren<Button>(true);
                                if (btn != null && btn.interactable) { btn.onClick.Invoke(); this.catClickCount++; return; }
                                if (SimulateClick(root)) { this.catClickCount++; return; }
                            }
                        }
                    }
                }

                foreach (var c in candidates)
                {
                    Transform node = statusPanel.transform.Find(c.path);
                    if (node == null) continue;
                    Transform vfxCircle = node.Find("AniRoot@ani/HUD_vfx/circle");
                    Transform vfxColor = node.Find("AniRoot@ani/HUD_vfx/color");
                    bool activeCue = (vfxCircle != null && vfxCircle.gameObject.activeInHierarchy)
                                     || (vfxColor != null && vfxColor.gameObject.activeInHierarchy);
                    if (activeCue)
                    {
                        // Before clicking, verify that the corresponding icon sprite is visible somewhere (above the cat)
                        var (nodeSprite, nodeRoot) = InspectSkillNode(c.path);
                        bool spriteVisible = false;
                        try
                        {
                            var allImages = Object.FindObjectsOfType<Image>();
                            foreach (var im in allImages)
                            {
                                if (im == null || im.sprite == null) continue;
                                if (!im.gameObject.activeInHierarchy) continue;
                                if (!string.IsNullOrEmpty(nodeSprite) && im.sprite.name == nodeSprite) { spriteVisible = true; break; }
                            }
                        }
                        catch { }

                        if (!spriteVisible)
                        {
                            // no visible above-cat icon for this active cue, skip
                            continue;
                        }

                        var btn = node.GetComponentInChildren<Button>(true);
                        if (btn != null && btn.interactable) { btn.onClick.Invoke(); this.catClickCount++; return; }
                        if (SimulateClick(node.gameObject)) { this.catClickCount++; return; }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("[AutoCatPlay] Error: " + ex.Message);
            }
        }

        // --- Auto Buy helpers + logic ---
        private void StartAutoBuy()
        {
            try
            {
                GameObject p = GameObject.Find("p_player_skeleton(Clone)");
                if (p != null) this.autoBuySavedPosition = p.transform.position;
                this.autoBuySubState = 1; // teleporting
                this.autoBuyStepTimer = Time.unscaledTime + 0.1f;
                this.autoBuyShopWaitStartedAt = 0f;
                this.autoBuyStoreSelectRetryCount = 0;
                this.autoBuyPreviousGameSpeed = this.gameSpeed;
                this.gameSpeed = 5f;
                this.autoBuyForcedGameSpeed = true;
                this.autoBuyCurrentIngredientIndex = 0;
                this.autoBuyPurchasedCount = 0;
                this.TeleportToLocation(this.autoBuyNearbyPos);
                MelonLogger.Msg("[AutoBuy] Started: teleporting to nearby position first (Game Speed x5.0)");
            }
            catch (Exception ex) { MelonLogger.Msg("[AutoBuy] Start error: " + ex.Message); this.StopAutoBuy("Start error"); }
        }

        private void StopAutoBuy(string reason)
        {
            MelonLogger.Msg("[AutoBuy] Stopped: " + reason);
            this.CloseAutoBuyPanels();
            this.autoBuyEnabled = false;
            this.autoBuySubState = 0;
            if (this.autoBuyForcedGameSpeed)
            {
                this.gameSpeed = Mathf.Max(1f, this.autoBuyPreviousGameSpeed);
                this.autoBuyForcedGameSpeed = false;
            }
            // teleport back if we have a saved position
            if (this.autoBuySavedPosition != Vector3.zero)
            {
                this.TeleportToLocation(this.autoBuySavedPosition);
                this.autoBuySavedPosition = Vector3.zero;
            }
        }

        private void CloseAutoBuyPanels()
        {
            try
            {
                // Dialogue panel close/back
                this.ClickButtonIfExists("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)/AniRoot@go@ani/back@btn");
                this.ClickButtonIfExists("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)/AniRoot@go@ani/exit@btn@go");

                // Some dialogue steps require clicking content/background to advance before close works.
                // Use exact inspector-confirmed paths first, then generic dialogue advance fallback.
                string[] dialogueTapPaths = new string[]
                {
                    "GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)/AniRoot@go@ani/main@go/DialogMsgWidget@go@w/content@go/text@list@t/Viewport/textContent@t/DialogueTextWidget(Clone)/content@txt",
                    "GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)/AniRoot@go@ani/main@go/DialogMsgWidget@go@w/content@go/bg",
                    "GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)/AniRoot@go@ani/main@go/DialogMsgWidget@go@w/content@go",
                    "GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)/AniRoot@go@ani/main@go/DialogMsgWidget@go@w"
                };

                for (int i = 0; i < dialogueTapPaths.Length; i++)
                {
                    GameObject go = GameObject.Find(dialogueTapPaths[i]);
                    if (go != null && go.activeInHierarchy)
                    {
                        SimulateClick(go);
                    }
                }

                this.TryAdvanceDialogueText();

                // Try closing SalePanel and ShopPanel using generic close/back buttons.
                string[] panelPaths = new string[]
                {
                    "GameApp/startup_root(Clone)/XDUIRoot/Top/SalePanel(Clone)",
                    "GameApp/startup_root(Clone)/XDUIRoot/Full/ShopPanel(Clone)"
                };

                for (int p = 0; p < panelPaths.Length; p++)
                {
                    GameObject panel = GameObject.Find(panelPaths[p]);
                    if (panel == null || !panel.activeInHierarchy) continue;

                    Button[] buttons = panel.GetComponentsInChildren<Button>(true);
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        Button b = buttons[i];
                        if (b == null || !b.interactable || !b.gameObject.activeInHierarchy) continue;
                        string n = (b.name ?? string.Empty).ToLowerInvariant();
                        if (n.Contains("close") || n.Contains("back") || n.Contains("return") || n.Contains("exit") || n.Contains("cancel"))
                        {
                            b.onClick.Invoke();
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private bool TryClickNpcChatIcon()
        {
            try
            {
                // Preferred: use IconsBarWidget candidates and score by known interaction sprite IDs.
                // From user logs this UI exposes two candidates: ui_dynamic_interaction_301 and ui_dynamic_interaction_3010.
                try
                {
                    var listRoot = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_common@list/IconsBarWidget(Clone)/root_visible@go/cells@t/cells@list");
                    if (listRoot != null)
                    {
                        Transform bestCell = null;
                        string bestSprite = string.Empty;
                        int bestScore = int.MinValue;

                        int childCount = listRoot.transform.childCount;
                        for (int ci = 0; ci < childCount; ci++)
                        {
                            var cell = listRoot.transform.GetChild(ci);
                            if (cell == null || !cell.gameObject.activeInHierarchy) continue;

                            var cellImgs = cell.GetComponentsInChildren<Image>(true);
                            if (cellImgs == null || cellImgs.Length == 0) continue;

                            string iconSprite = string.Empty;
                            for (int ii = 0; ii < cellImgs.Length; ii++)
                            {
                                var im = cellImgs[ii];
                                if (im == null || im.sprite == null) continue;

                                string imgName = (im.name ?? string.Empty).ToLowerInvariant();
                                string sp = (im.sprite.name ?? string.Empty).ToLowerInvariant();
                                if (imgName.Contains("icon@img@btn") || sp.Contains("ui_dynamic_interaction_"))
                                {
                                    iconSprite = sp;
                                    break;
                                }

                                if (string.IsNullOrEmpty(iconSprite))
                                {
                                    iconSprite = sp;
                                }
                            }

                            if (string.IsNullOrEmpty(iconSprite)) continue;

                            int score = 0;
                            // Prefer the exact observed NPC interaction icon first.
                            if (iconSprite.Contains("ui_dynamic_interaction_301(clone)") || iconSprite.EndsWith("_301")) score += 120;
                            else if (iconSprite.Contains("ui_dynamic_interaction_3010(clone)") || iconSprite.EndsWith("_3010")) score += 90;

                            if (iconSprite.Contains("chat") || iconSprite.Contains("talk") || iconSprite.Contains("bubble") || iconSprite.Contains("dialog")) score += 40;

                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestCell = cell;
                                bestSprite = iconSprite;
                            }
                        }

                        if (bestCell != null && bestScore > 0)
                        {
                            var iconBtnNode = bestCell.Find("root_visible@go/icon@img@btn");
                            if (iconBtnNode != null)
                            {
                                var iconBtn = iconBtnNode.GetComponent<Button>();
                                if (iconBtn != null && iconBtn.interactable)
                                {
                                    iconBtn.onClick.Invoke();
                                    MelonLogger.Msg("[AutoBuy] Clicked interaction icon: " + bestSprite);
                                    return true;
                                }

                                if (SimulateClick(iconBtnNode.gameObject))
                                {
                                    MelonLogger.Msg("[AutoBuy] SimClicked interaction icon: " + bestSprite);
                                    return true;
                                }
                            }

                            var btn = bestCell.GetComponentInChildren<Button>(true);
                            if (btn != null && btn.interactable)
                            {
                                btn.onClick.Invoke();
                                MelonLogger.Msg("[AutoBuy] Clicked best IconsBarWidget cell: " + bestSprite);
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex) { MelonLogger.Msg("[AutoBuy] IconsBarWidget search error: " + ex.Message); }
                var imgs = Object.FindObjectsOfType<Image>();
                foreach (var img in imgs)
                {
                    if (img == null || img.sprite == null || !img.gameObject.activeInHierarchy) continue;
                    string s = img.sprite.name.ToLowerInvariant();
                    if (s.Contains("chat") || s.Contains("talk") || s.Contains("bubble") || s.Contains("dialog") || s.Contains("petplay"))
                    {
                        var btn = img.GetComponentInParent<Button>();
                        if (btn != null && btn.interactable) { btn.onClick.Invoke(); MelonLogger.Msg("[AutoBuy] Clicked chat Button"); return true; }
                        Transform t = img.transform;
                        for (int i = 0; i < 6 && t != null; i++)
                        {
                            if (SimulateClick(t.gameObject)) { MelonLogger.Msg("[AutoBuy] Simulated click on chat UI"); return true; }
                            t = t.parent;
                        }
                    }
                }
            }
            catch (Exception ex) { MelonLogger.Msg("[AutoBuy] TryClickNpcChatIcon error: " + ex.Message); }
            // Prefer dialog-specific icon GameObjects by name to avoid global player-chat icon
            try
            {
                string[] preferredNames = new string[] { "CommonIconForDialog", "CommonIconForCookNormalDialog", "CommonIconForCookDangerDialog", "CommonIconForRecycleDialog", "CommonIconForDialog" };
                foreach (var pref in preferredNames)
                {
                    foreach (Transform tr in Resources.FindObjectsOfTypeAll<Transform>())
                    {
                        try
                        {
                            if (tr == null || !tr.gameObject.activeInHierarchy) continue;
                            if (!tr.name.Contains(pref)) continue;
                            // find a Button or Image child to click
                            var btn = tr.GetComponentInChildren<Button>(true);
                            if (btn != null && btn.interactable) { btn.onClick.Invoke(); MelonLogger.Msg($"[AutoBuy] Clicked preferred icon '{tr.name}'");
                                // verify dialogue opened
                                var dlg = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)");
                                if (dlg != null && dlg.activeInHierarchy) return true;
                            }
                            var img = tr.GetComponentInChildren<Image>(true);
                            if (img != null)
                            {
                                if (img.GetComponentInParent<Button>() is Button b && b.interactable) { b.onClick.Invoke(); MelonLogger.Msg($"[AutoBuy] Clicked preferred image button '{tr.name}'"); var dlg2 = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)"); if (dlg2 != null && dlg2.activeInHierarchy) return true; }
                                if (SimulateClick(img.gameObject)) { MelonLogger.Msg($"[AutoBuy] SimClicked preferred image '{tr.name}'"); var dlg3 = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)"); if (dlg3 != null && dlg3.activeInHierarchy) return true; }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex) { MelonLogger.Msg("[AutoBuy] Preferred icon search error: " + ex.Message); }

            // Fallback: find NPC name Texts and click nearby Image UI (chat bubble above NPC)
            try
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    var texts = Object.FindObjectsOfType<Text>();
                    foreach (var t in texts)
                    {
                        try
                        {
                            if (t == null || !t.gameObject.activeInHierarchy) continue;
                            string txt = t.text ?? "";
                            if (string.IsNullOrWhiteSpace(txt)) continue;
                            // avoid UI menu texts by ignoring DialoguePanel and other known panels
                            if (t.transform.IsChildOf(GameObject.Find("GameApp/startup_root(Clone)")?.transform) == false) continue;
                            Vector3 worldPos = t.transform.position;
                            Vector3 screen = cam.WorldToScreenPoint(worldPos);
                            if (screen.z < 0) continue;
                            // search images near this screen point
                            var allImgs = Object.FindObjectsOfType<Image>();
                            foreach (var im in allImgs)
                            {
                                if (im == null || !im.gameObject.activeInHierarchy) continue;
                                Vector3 imScreen = cam.WorldToScreenPoint(im.transform.position);
                                if (imScreen.z < 0) continue;
                                float dist = Vector2.Distance(new Vector2(screen.x, screen.y), new Vector2(imScreen.x, imScreen.y));
                                if (dist < 140f)
                                {
                                    // likely the bubble icon above this NPC
                                    var btn = im.GetComponentInParent<Button>();
                                    if (btn != null && btn.interactable) { btn.onClick.Invoke(); MelonLogger.Msg($"[AutoBuy] Clicked nearby UI '{im.name}' near name '{txt}'"); return true; }
                                    if (SimulateClick(im.gameObject)) { MelonLogger.Msg($"[AutoBuy] SimClicked nearby UI '{im.name}' near name '{txt}'"); return true; }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex) { MelonLogger.Msg("[AutoBuy] Fallback detection error: " + ex.Message); }

            return false;
        }

        private bool ClickDialogueOptionByTitle(string title)
        {
            try
            {
                GameObject panel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)");
                if (panel == null || !panel.activeInHierarchy) return false;
                Transform viewportContent = panel.transform.Find("AniRoot@go@ani/option@w/option@list/Viewport/Content");
                if (viewportContent == null)
                {
                    var allChildren = panel.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < allChildren.Length; i++)
                    {
                        var child = allChildren[i];
                        if (child != null && child.name != null && child.name.Contains("ImageTextBtnWidget")) { viewportContent = child.parent; break; }
                    }
                    if (viewportContent == null) return false;
                }
                string lower = title.ToLowerInvariant();
                int childCount = viewportContent.childCount;
                for (int ci = 0; ci < childCount; ci++)
                {
                    Transform cell = viewportContent.GetChild(ci);
                    if (cell == null) continue;
                    var titleTxtTransform = cell.Find("AniRoot@go@ani/cell@btn/title@txt");
                    Text titleTxt = null;
                    if (titleTxtTransform != null) titleTxt = titleTxtTransform.GetComponent<Text>();
                    if (titleTxt == null) titleTxt = cell.GetComponentInChildren<Text>(true);
                    if (titleTxt != null && titleTxt.text != null && titleTxt.text.ToLowerInvariant().Contains(lower))
                    {
                        var btn = cell.GetComponentInChildren<Button>(true);
                        if (btn != null && btn.interactable) { btn.onClick.Invoke(); MelonLogger.Msg($"[AutoBuy] Selected dialog option \"{titleTxt.text}\""); return true; }
                        if (SimulateClick(cell.gameObject)) { MelonLogger.Msg($"[AutoBuy] SimClicked dialog option \"{titleTxt.text}\""); return true; }
                    }
                }
            }
            catch (Exception ex) { MelonLogger.Msg("[AutoBuy] ClickDialogueOptionByTitle error: " + ex.Message); }
            return false;
        }

        private bool ClickDialogueOptionByKeywords(string[] keywords)
        {
            try
            {
                GameObject panel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)");
                if (panel == null || !panel.activeInHierarchy) return false;

                Transform viewportContent = panel.transform.Find("AniRoot@go@ani/option@w/option@list/Viewport/Content");
                if (viewportContent == null || viewportContent.childCount <= 0) return false;

                int bestIndex = -1;
                int bestScore = int.MinValue;

                for (int ci = 0; ci < viewportContent.childCount; ci++)
                {
                    Transform cell = viewportContent.GetChild(ci);
                    if (cell == null || !cell.gameObject.activeInHierarchy) continue;

                    string textLower = string.Empty;
                    Text titleTxt = cell.Find("AniRoot@go@ani/cell@btn/title@txt")?.GetComponent<Text>() ?? cell.GetComponentInChildren<Text>(true);
                    if (titleTxt != null && !string.IsNullOrEmpty(titleTxt.text))
                    {
                        textLower = titleTxt.text.ToLowerInvariant();
                    }

                    int score = 0;
                    if (!string.IsNullOrEmpty(textLower))
                    {
                        for (int k = 0; k < keywords.Length; k++)
                        {
                            string kw = keywords[k];
                            if (string.IsNullOrEmpty(kw)) continue;
                            string kwLower = kw.ToLowerInvariant();
                            if (textLower.Contains(kwLower)) score += 20;
                        }

                        if (textLower.Contains("cooking store")) score += 100;
                        else if (textLower.Contains("cook") && textLower.Contains("store")) score += 80;
                    }

                    // Icon fallback: shopping/cart-like option often has a cart/store icon.
                    Image[] imgs = cell.GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < imgs.Length; i++)
                    {
                        Image im = imgs[i];
                        if (im == null || im.sprite == null) continue;
                        string sp = im.sprite.name.ToLowerInvariant();
                        if (sp.Contains("shop") || sp.Contains("store") || sp.Contains("cart")) score += 25;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = ci;
                    }
                }

                if (bestIndex < 0 || bestScore <= 0) return false;

                Transform bestCell = viewportContent.GetChild(bestIndex);
                if (bestCell == null) return false;

                Button btn = bestCell.GetComponentInChildren<Button>(true);
                if (btn != null && btn.interactable)
                {
                    btn.onClick.Invoke();
                    MelonLogger.Msg("[AutoBuy] Selected dialog option by keyword score=" + bestScore);
                    return true;
                }

                if (SimulateClick(bestCell.gameObject))
                {
                    MelonLogger.Msg("[AutoBuy] SimClicked dialog option by keyword score=" + bestScore);
                    return true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("[AutoBuy] ClickDialogueOptionByKeywords error: " + ex.Message);
            }

            return false;
        }

        private bool HasDialogueOptionsVisible()
        {
            try
            {
                GameObject panel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)");
                if (panel == null || !panel.activeInHierarchy) return false;

                Transform viewportContent = panel.transform.Find("AniRoot@go@ani/option@w/option@list/Viewport/Content");
                if (viewportContent == null) return false;
                if (viewportContent.childCount <= 0) return false;

                for (int i = 0; i < viewportContent.childCount; i++)
                {
                    Transform cell = viewportContent.GetChild(i);
                    if (cell == null || !cell.gameObject.activeInHierarchy) continue;
                    Text t = cell.GetComponentInChildren<Text>(true);
                    if (t != null && !string.IsNullOrWhiteSpace(t.text)) return true;
                }
            }
            catch { }

            return false;
        }

        private bool TryAdvanceDialogueText()
        {
            try
            {
                GameObject panel = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)");
                if (panel == null || !panel.activeInHierarchy) return false;

                // First try explicit next/continue/skip style buttons.
                Button[] buttons = panel.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    Button b = buttons[i];
                    if (b == null || !b.interactable || !b.gameObject.activeInHierarchy) continue;
                    string n = (b.name ?? string.Empty).ToLowerInvariant();
                    if (n.Contains("next") || n.Contains("continue") || n.Contains("skip") || n.Contains("content"))
                    {
                        b.onClick.Invoke();
                        return true;
                    }
                }

                // Fallback: click likely dialogue content regions to finish typewriter text.
                string[] clickPaths = new string[]
                {
                    // Exact paths from UI inspector (most reliable for skipping typewriter text)
                    "AniRoot@go@ani/main@go/DialogMsgWidget@go@w/content@go/text@list@t/Viewport/textContent@t/DialogueTextWidget(Clone)/content@txt",
                    "AniRoot@go@ani/main@go/DialogMsgWidget@go@w/content@go/bg",
                    "AniRoot@go@ani/content@w",
                    "AniRoot@go@ani/content@w/content@txt",
                    "AniRoot@go@ani/content@w/content@go",
                    "AniRoot@go@ani/main@go/DialogMsgWidget@go@w/content@go",
                    "AniRoot@go@ani"
                };

                bool clickedAny = false;
                for (int i = 0; i < clickPaths.Length; i++)
                {
                    Transform t = panel.transform.Find(clickPaths[i]);
                    if (t == null || !t.gameObject.activeInHierarchy) continue;
                    if (SimulateClick(t.gameObject)) clickedAny = true;
                }

                if (clickedAny) return true;

                // Broad fallback: find the first interactable button that is NOT inside the options
                // list AND not a back/exit/close button. Catches transparent click-to-advance buttons
                // used by simple farewell dialogues like "Thank you for your patronage."
                Transform optionContent = panel.transform.Find("AniRoot@go@ani/option@w/option@list/Viewport/Content");
                Button[] allButtons = panel.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < allButtons.Length; i++)
                {
                    Button b = allButtons[i];
                    if (b == null || !b.interactable || !b.gameObject.activeInHierarchy) continue;
                    if (optionContent != null && b.transform.IsChildOf(optionContent)) continue;
                    string n = (b.name ?? string.Empty).ToLowerInvariant();
                    if (n.Contains("back") || n.Contains("exit") || n.Contains("close") || n.Contains("cancel")) continue;
                    b.onClick.Invoke();
                    return true;
                }

                return SimulateClick(panel);
            }
            catch { }

            return false;
        }

        private bool ClickCookingStoreItemByMatch(string match)
        {
            try
            {
                GameObject shop = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/ShopPanel(Clone)");
                if (shop == null) return false;
                Transform content = shop.transform.Find("goods@scroll/Content");
                if (content == null) return false;
                // if content exists but not populated yet, log and return false so caller can retry
                if (content.childCount == 0)
                {
                    MelonLogger.Msg("[AutoBuy] Shop content empty - will retry shortly");
                    // debug dump immediate children of shop for diagnosis
                    try
                    {
                        for (int s = 0; s < shop.transform.childCount; s++)
                        {
                            var ch = shop.transform.GetChild(s);
                            MelonLogger.Msg($"[AutoBuy] Shop child[{s}] = { (ch != null ? ch.name : "<null>") }");
                        }
                    }
                    catch { }
                    return false;
                }
                for (int i = 0; i < content.childCount; i++)
                {
                    Transform gw = content.GetChild(i);
                    if (gw == null) continue;
                    // primary reliable name path
                    Transform nameT = gw.Find("AniRoot@ani/info@group/titleName/name@txt") ?? gw.Find("AniRoot@ani/info@group/titleName/titleNameNormal_img");
                    string txtVal = null;
                    if (nameT != null)
                    {
                        Text t = nameT.GetComponent<Text>();
                        if (t != null) txtVal = t.text;
                    }
                    // fallback: any child Text
                    if (string.IsNullOrEmpty(txtVal))
                    {
                        var any = gw.GetComponentInChildren<Text>(true);
                        if (any != null) txtVal = any.text;
                    }
                    if (!string.IsNullOrEmpty(txtVal) && txtVal.IndexOf(match, StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        Transform card = gw.Find("AniRoot@ani/card@btn") ?? gw.Find("card@btn");
                        if (card != null)
                        {
                            var b = card.GetComponent<Button>() ?? card.GetComponentInChildren<Button>(true);
                            if (b != null && b.interactable) { b.onClick.Invoke(); MelonLogger.Msg($"[AutoBuy] Clicked item {txtVal}"); return true; }
                            if (SimulateClick(card.gameObject)) { MelonLogger.Msg($"[AutoBuy] SimClicked item {txtVal}"); return true; }
                        }
                    }
                }
            }
            catch (Exception ex) { MelonLogger.Msg("[AutoBuy] ClickCookingStoreItemByMatch error: " + ex.Message); }
            return false;
        }

        // SalePanel helpers
        private int GetSalePanelCurrentCount(GameObject sale)
        {
            try
            {
                var countControl = sale.transform.Find("AniRoot/popup/content/bottom/countControl@w@go/countControl@go");
                if (countControl == null) countControl = sale.transform.Find("AniRoot/popup/content/bottom/countControl@go");
                if (countControl != null)
                {
                    var texts = countControl.GetComponentsInChildren<Text>(true);
                    foreach (var t in texts)
                    {
                        if (t == null || string.IsNullOrEmpty(t.text)) continue;
                        string s = t.text.Trim();
                        // try parse int
                        int val;
                        if (int.TryParse(s, out val)) return val;
                        // sometimes label shows '1' with spaces
                        string digits = new string(s.Where(c => char.IsDigit(c)).ToArray());
                        if (digits.Length > 0 && int.TryParse(digits, out val)) return val;
                    }
                }
            }
            catch { }
            return -1;
        }

        private bool ClickSaleAddMore(GameObject sale)
        {
            try
            {
                var btn = sale.transform.Find("AniRoot/popup/content/bottom/countControl@w@go/countControl@go/addMore@btn")
                          ?? sale.transform.Find("AniRoot/popup/content/bottom/countControl@go/addMore@btn");
                if (btn != null)
                {
                    var b = btn.GetComponent<Button>();
                    if (b != null && b.interactable) { b.onClick.Invoke(); MelonLogger.Msg("[AutoBuy] Clicked +10"); return true; }
                    if (SimulateClick(btn.gameObject)) { MelonLogger.Msg("[AutoBuy] SimClicked +10"); return true; }
                }
            }
            catch { }
            return false;
        }

        private bool ClickSalePurchase(GameObject sale)
        {
            try
            {
                var buy = sale.transform.Find("AniRoot/popup/operators/buy@btn") ?? sale.transform.Find("AniRoot/popup/operators/buy@btn/buy@btn");
                if (buy != null)
                {
                    var b = buy.GetComponent<Button>() ?? buy.GetComponentInChildren<Button>(true);
                    if (b != null && b.interactable) { b.onClick.Invoke(); MelonLogger.Msg("[AutoBuy] Clicked Purchase"); return true; }
                    if (SimulateClick(buy.gameObject)) { MelonLogger.Msg("[AutoBuy] SimClicked Purchase"); return true; }
                }
            }
            catch { }
            return false;
        }

        private void RunAutoBuyLogic()
        {
            try
            {
                if (!this.autoBuyEnabled)
                {
                    if (this.autoBuyForcedGameSpeed)
                    {
                        this.gameSpeed = Mathf.Max(1f, this.autoBuyPreviousGameSpeed);
                        this.autoBuyForcedGameSpeed = false;
                    }
                    return;
                }
                float now = Time.unscaledTime;
                switch (this.autoBuySubState)
                {
                    case 1: // teleporting to nearby position
                        // wait for teleport to finish (teleportFramesRemaining decreases)
                        if (this.teleportFramesRemaining <= 0)
                        {
                            this.autoBuySubState = 12; // wait 3s then teleport to NPC front
                            this.autoBuyStepTimer = now + 3f;
                            MelonLogger.Msg("[AutoBuy] Arrived at nearby position, waiting 3s before approaching NPC");
                        }
                        break;
                    case 12: // waiting at nearby pos, then teleport to NPC front
                        if (now < this.autoBuyStepTimer) break;
                        this.TeleportToLocation(this.autoBuyTargetPos);
                        this.autoBuySubState = 2; // waiting for dialogue
                        this.autoBuyStepTimer = now + 0.8f;
                        MelonLogger.Msg("[AutoBuy] Teleporting to NPC front position");
                        break;
                    case 2: // waiting for dialogue - click chat icon until dialogue shows
                        if (now < this.autoBuyStepTimer) break;
                        // try to click chat icon
                        if (TryClickNpcChatIcon()) { this.autoBuyStepTimer = now + 0.5f; }
                        else { this.autoBuyStepTimer = now + 0.12f; }
                        // check if dialogue panel present
                        GameObject dlg = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Scene/DialoguePanel(Clone)");
                        if (dlg != null && dlg.activeInHierarchy)
                        {
                            this.autoBuySubState = 3; this.autoBuyStepTimer = now + 0.2f; MelonLogger.Msg("[AutoBuy] Dialogue opened");
                        }
                        break;
                    case 3: // select cooking store
                        if (now < this.autoBuyStepTimer) break;
                        // NPC text may still be typing; options often appear only after advancing dialogue.
                        if (!HasDialogueOptionsVisible())
                        {
                            if (TryAdvanceDialogueText())
                            {
                                MelonLogger.Msg("[AutoBuy] Advanced dialogue text, waiting for options");
                            }
                            this.autoBuyStepTimer = now + 0.12f;
                            break;
                        }
                        if (ClickDialogueOptionByKeywords(new string[] { "cooking store", "cook", "store" }))
                        {
                            // go to a waiting-for-shop state so we only attempt purchases after shop content is populated
                            this.autoBuySubState = 31;
                            this.autoBuyStepTimer = now + 0.25f;
                            this.autoBuyShopWaitStartedAt = now;
                            this.autoBuyStoreSelectRetryCount++;
                            MelonLogger.Msg("[AutoBuy] Selected Cooking Store, waiting for shop content");
                        }
                        else { this.autoBuyStepTimer = now + 0.15f; }
                        break;
                    case 31: // wait for ShopPanel to appear and be populated
                        if (now < this.autoBuyStepTimer) break;
                        GameObject shopChk = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/ShopPanel(Clone)");
                        if (shopChk != null && shopChk.activeInHierarchy)
                        {
                            Transform contentChk = shopChk.transform.Find("goods@scroll/Content");
                            if (contentChk != null && contentChk.childCount > 0)
                            {
                                this.autoBuySubState = 4; this.autoBuyStepTimer = now + 0.12f; MelonLogger.Msg("[AutoBuy] ShopPanel populated, proceeding to buy");
                                break;
                            }
                            else
                            {
                                // content not yet populated, retry a few times
                                MelonLogger.Msg("[AutoBuy] Waiting for ShopPanel content to populate...");
                                this.autoBuyStepTimer = now + 0.25f;
                                break;
                            }
                        }
                        // ShopPanel not yet present. Retry the option click while dialogue is visible.
                        if (ClickDialogueOptionByKeywords(new string[] { "cooking store", "cook", "store" }))
                        {
                            this.autoBuyStoreSelectRetryCount++;
                            this.autoBuyStepTimer = now + 0.25f;
                            MelonLogger.Msg("[AutoBuy] Retried Cooking Store option while waiting for shop");
                            break;
                        }

                        if (TryAdvanceDialogueText())
                        {
                            this.autoBuyStepTimer = now + 0.12f;
                            break;
                        }

                        if (this.autoBuyShopWaitStartedAt <= 0f)
                        {
                            this.autoBuyShopWaitStartedAt = now;
                        }

                        if ((now - this.autoBuyShopWaitStartedAt) > 2.5f)
                        {
                            MelonLogger.Msg("[AutoBuy] Shop panel did not open yet, returning to Cooking Store selection");
                            this.autoBuySubState = 3;
                            this.autoBuyStepTimer = now + 0.1f;
                            this.autoBuyShopWaitStartedAt = 0f;
                            break;
                        }

                        this.autoBuyStepTimer = now + 0.25f;
                        break;
                    case 4: // buying items
                        if (now < this.autoBuyStepTimer) break;
                        if (this.autoBuyCurrentIngredientIndex >= this.autoBuyIngredientsMatch.Length)
                        {
                            this.autoBuySubState = 5;
                            this.autoBuyStepTimer = now + 3f;
                            MelonLogger.Msg("[AutoBuy] Finished ingredient loop, waiting 3s before return");
                            break;
                        }
                        string match = this.autoBuyIngredientsMatch[this.autoBuyCurrentIngredientIndex];
                        // attempt to click the item up to max; we assume each click buys one
                        if (this.autoBuyPurchasedCount >= this.autoBuyMaxPerIngredient)
                        {
                            this.autoBuyPurchasedCount = 0; this.autoBuyCurrentIngredientIndex++; this.autoBuyStepTimer = now + 0.1f; break;
                        }
                        bool clicked = ClickCookingStoreItemByMatch(match);
                        if (clicked)
                        {
                            // go to purchase dialog handling substate
                            this.autoBuySubState = 41; // purchase dialog
                            this.autoBuyStepTimer = now + 0.12f;
                            MelonLogger.Msg($"[AutoBuy] Opened purchase dialog for {match}");
                        }
                        else
                        {
                            // item not found / sold out; but if the shop panel exists and content is empty, retry a few times
                            GameObject shopProbe = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Full/ShopPanel(Clone)");
                            Transform contentProbe = shopProbe != null ? shopProbe.transform.Find("goods@scroll/Content") : null;
                            if (shopProbe != null && contentProbe != null && contentProbe.childCount == 0)
                            {
                                // shop present but not populated yet — retry this ingredient shortly
                                MelonLogger.Msg($"[AutoBuy] Shop content empty for item {match}, retrying shortly");
                                this.autoBuyStepTimer = now + 0.25f;
                            }
                            else
                            {
                                // truly not found or sold out; move to next
                                this.autoBuyPurchasedCount = 0; this.autoBuyCurrentIngredientIndex++; this.autoBuyStepTimer = now + 0.2f; MelonLogger.Msg($"[AutoBuy] Item {match} not found or sold out, skipping");
                            }
                        }
                        break;
                    case 41: // handle purchase dialog: press +10 until target then Purchase
                        if (now < this.autoBuyStepTimer) break;
                        GameObject sale = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Top/SalePanel(Clone)");
                        if (sale == null || !sale.activeInHierarchy)
                        {
                            // Sale panel not present, abort this item
                            this.autoBuyPurchasedCount = 0; this.autoBuyCurrentIngredientIndex++; this.autoBuyStepTimer = now + 0.2f; this.autoBuySubState = 4; MelonLogger.Msg("[AutoBuy] Sale panel not found, skipping"); break;
                        }
                        // read current count (try to find numeric Text inside countControl)
                        int currentCount = GetSalePanelCurrentCount(sale);
                        if (currentCount < 0) currentCount = 1; // default if cannot read
                        if (currentCount >= this.autoBuyMaxPerIngredient)
                        {
                            // directly click purchase
                            if (ClickSalePurchase(sale)) { this.autoBuyPurchasedCount = this.autoBuyMaxPerIngredient; MelonLogger.Msg($"[AutoBuy] Purchased {this.autoBuyPurchasedCount} items"); }
                            // after purchase, proceed to next ingredient
                            this.autoBuyPurchasedCount = 0; this.autoBuyCurrentIngredientIndex++; this.autoBuySubState = 4; this.autoBuyStepTimer = now + 0.25f;
                            break;
                        }
                        // need to increase: press +10 button
                        int needed = this.autoBuyMaxPerIngredient - currentCount;
                        int clicks = Mathf.CeilToInt((float)needed / 10f);
                        // limit clicks this tick to avoid long blocking
                        int doClicks = Mathf.Min(clicks, 3);
                        bool anyClicked = false;
                        for (int i = 0; i < doClicks; i++)
                        {
                            if (ClickSaleAddMore(sale)) { anyClicked = true; }
                        }
                        this.autoBuyStepTimer = now + 0.12f;
                        if (!anyClicked)
                        {
                            // couldn't click addMore, try purchase or skip
                            if (ClickSalePurchase(sale)) { this.autoBuyPurchasedCount = this.autoBuyMaxPerIngredient; }
                            this.autoBuyPurchasedCount = 0; this.autoBuyCurrentIngredientIndex++; this.autoBuySubState = 4; this.autoBuyStepTimer = now + 0.25f;
                        }
                        break;
                    case 5: // return
                        // Keep closing lingering panels (dialogue/shop/sale) during delay window.
                        this.CloseAutoBuyPanels();
                        if (now < this.autoBuyStepTimer) break;
                        this.StopAutoBuy("Done, returning");
                        break;
                }
            }
            catch (Exception ex) { MelonLogger.Msg("[AutoBuy] Run error: " + ex.Message); this.StopAutoBuy("Error"); }
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
                UnifiedConfigData config = this.LoadOrCreateUnifiedConfig();
                this.PopulateAllConfigSections(config);
                this.SaveUnifiedConfig(config);
                MelonLogger.Msg("Patrol points saved!");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error saving patrol points: " + ex.Message);
            }
        }

        private void LoadPatrolPoints()
        {
            try
            {
                UnifiedConfigData config = this.LoadUnifiedConfig();
                if (config != null)
                {
                    patrolPoints.Clear();
                    foreach (SerializableVector3 point in config.Patrol.Points)
                    {
                        if (point != null) patrolPoints.Add(point.ToVector3());
                    }
                    MelonLogger.Msg($"Loaded {patrolPoints.Count} patrol points.");
                    return;
                }
                string path = this.GetPatrolPath();
                if (!File.Exists(path)) return;
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
        private string GetCookingPatrolSaveDirectory()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "cooking_patrol_saves");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        private string SanitizeCookingPatrolSaveName(string name)
        {
            string candidate = string.IsNullOrWhiteSpace(name) ? "" : name.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                candidate = candidate.Replace(c, '_');
            }
            return string.IsNullOrWhiteSpace(candidate) ? "" : candidate;
        }

        private string GetCookingPatrolPath(string saveName = null)
        {
            string safeName = this.SanitizeCookingPatrolSaveName(saveName ?? this.cookingPatrolSaveName);
            return Path.Combine(this.GetCookingPatrolSaveDirectory(), safeName + ".json");
        }

        private List<string> GetCookingPatrolSaveNames()
        {
            UnifiedConfigData config = this.LoadUnifiedConfig();
            if (config != null)
            {
                List<string> configSaves = new List<string>();
                foreach (NamedCookingPatrolSave save in config.CookingPatrolSaves)
                {
                    string name = this.SanitizeCookingPatrolSaveName(save?.Name);
                    if (!string.IsNullOrWhiteSpace(name) && !configSaves.Contains(name))
                    {
                        configSaves.Add(name);
                    }
                }
                configSaves.Sort(StringComparer.OrdinalIgnoreCase);
                return configSaves;
            }
            List<string> saves = new List<string>();
            try
            {
                string dir = this.GetCookingPatrolSaveDirectory();
                string[] files = Directory.GetFiles(dir, "*.json");
                foreach (string file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrWhiteSpace(name) && !saves.Contains(name))
                    {
                        saves.Add(name);
                    }
                }
            }
            catch
            {
            }
            saves.Sort(StringComparer.OrdinalIgnoreCase);
            return saves;
        }

        private bool DeleteCookingPatrolSave(string saveName)
        {
            string safeName = this.SanitizeCookingPatrolSaveName(saveName);
            if (string.IsNullOrEmpty(safeName))
            {
                this.AddMenuNotification("Enter a save name to delete", new Color(1f, 0.5f, 0.5f));
                return false;
            }

            try
            {
                UnifiedConfigData config = this.LoadUnifiedConfig();
                if (config != null)
                {
                    int removed = config.CookingPatrolSaves.RemoveAll(s => string.Equals(this.SanitizeCookingPatrolSaveName(s?.Name), safeName, StringComparison.OrdinalIgnoreCase));
                    if (removed <= 0)
                    {
                        this.AddMenuNotification($"Save not found: {safeName}", new Color(1f, 0.5f, 0.5f));
                        return false;
                    }
                    this.SaveUnifiedConfig(config);
                    MelonLogger.Msg($"Deleted cooking patrol save '{safeName}'.");
                    this.AddMenuNotification($"Deleted: {safeName}", new Color(1f, 0.75f, 0.45f));
                    return true;
                }
                string path = this.GetCookingPatrolPath(safeName);
                if (!File.Exists(path))
                {
                    this.AddMenuNotification($"Save not found: {safeName}", new Color(1f, 0.5f, 0.5f));
                    return false;
                }
                File.Delete(path);
                MelonLogger.Msg($"Deleted cooking patrol save '{safeName}'.");
                this.AddMenuNotification($"Deleted: {safeName}", new Color(1f, 0.75f, 0.45f));
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error deleting cooking patrol save: " + ex.Message);
                this.AddMenuNotification("Failed to delete patrol save", new Color(1f, 0.4f, 0.4f));
                return false;
            }
        }

        private string GetTreeFarmPatrolPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "tree_farm_patrol_points.json");
        }

        private void SaveCookingPatrolPoints(string saveName = null)
        {
            try
            {
                string safeName = this.SanitizeCookingPatrolSaveName(saveName ?? this.cookingPatrolSaveName);
                if (string.IsNullOrEmpty(safeName))
                {
                    this.AddMenuNotification("Enter a save name before saving", new Color(1f, 0.5f, 0.5f));
                    return;
                }
                this.cookingPatrolSaveName = safeName;
                UnifiedConfigData config = this.LoadOrCreateUnifiedConfig();
                this.PopulateAllConfigSections(config);
                this.SaveUnifiedConfig(config);
                MelonLogger.Msg($"Cooking patrol points saved to '{safeName}'! ({cookingPatrolPoints.Count} points with rotations)");
                this.AddMenuNotification($"Cooking patrol saved: {safeName}", new Color(0.55f, 0.88f, 1f));
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error saving cooking patrol points: " + ex.Message);
                this.AddMenuNotification("Failed to save cooking patrol", new Color(1f, 0.4f, 0.4f));
            }
        }

        private void LoadCookingPatrolPoints(string saveName = null)
        {
            try
            {
                string safeName = this.SanitizeCookingPatrolSaveName(saveName ?? this.cookingPatrolSaveName);
                if (string.IsNullOrEmpty(safeName))
                {
                    this.AddMenuNotification("Enter a save name before loading", new Color(1f, 0.5f, 0.5f));
                    return;
                }
                this.cookingPatrolSaveName = safeName;
                UnifiedConfigData config = this.LoadUnifiedConfig();
                if (config != null)
                {
                    NamedCookingPatrolSave save = config.CookingPatrolSaves.FirstOrDefault(s => string.Equals(this.SanitizeCookingPatrolSaveName(s?.Name), safeName, StringComparison.OrdinalIgnoreCase));
                    if (save == null)
                    {
                        MelonLogger.Msg($"Cooking patrol slot '{safeName}' not found.");
                        this.AddMenuNotification($"Patrol save not found: {safeName}", new Color(1f, 0.5f, 0.5f));
                        return;
                    }
                    cookingPatrolPoints.Clear();
                    foreach (CookingPatrolPoint point in save.Points)
                    {
                        if (point != null) cookingPatrolPoints.Add(point);
                    }
                    MelonLogger.Msg($"Loaded {cookingPatrolPoints.Count} cooking patrol points from '{safeName}' (with rotations: true).");
                    this.AddMenuNotification($"Cooking patrol loaded: {safeName}", new Color(0.55f, 0.88f, 1f));
                    return;
                }
                string path = this.GetCookingPatrolPath(safeName);
                if (!File.Exists(path))
                {
                    MelonLogger.Msg($"Cooking patrol slot '{safeName}' not found.");
                    this.AddMenuNotification($"Patrol save not found: {safeName}", new Color(1f, 0.5f, 0.5f));
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
                MelonLogger.Msg($"Loaded {cookingPatrolPoints.Count} cooking patrol points from '{safeName}' (with rotations: {hasRotation}).");
                this.AddMenuNotification($"Cooking patrol loaded: {safeName}", new Color(0.55f, 0.88f, 1f));
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("Error loading cooking patrol points: " + ex.Message);
                this.AddMenuNotification("Failed to load cooking patrol", new Color(1f, 0.4f, 0.4f));
            }
        }

        private void SaveTreeFarmPatrolPoints()
        {
            try
            {
                UnifiedConfigData config = this.LoadOrCreateUnifiedConfig();
                this.PopulateAllConfigSections(config);
                this.SaveUnifiedConfig(config);
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
                UnifiedConfigData config = this.LoadUnifiedConfig();
                if (config != null)
                {
                    treeFarmPoints.Clear();
                    foreach (TreeFarmPatrolPoint point in config.TreeFarmPatrol.Points)
                    {
                        if (point != null) treeFarmPoints.Add(point);
                    }
                    MelonLogger.Msg($"Loaded {treeFarmPoints.Count} tree farm patrol points (with rotations: true).");
                    return;
                }
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
            // Debounce: ignore triggers that happen within the configured auto-repair pause window
            if (Time.time - this.lastRepairTriggerTime < this.resourceAutoRepairPauseSeconds)
            {
                MelonLogger.Msg($"[StartRepair] Ignored trigger due to debounce. Now={Time.time} LastTrigger={this.lastRepairTriggerTime} Wait={this.resourceAutoRepairPauseSeconds}");
                return;
            }

            // Prevent re-entrancy: if a repair is already running, ignore subsequent starts
            if (isRepairing)
            {
                MelonLogger.Msg("[StartRepair] ignored re-entry (already repairing)");
                return;
            }

            this.lastRepairTriggerTime = Time.time;
            MelonLogger.Msg($"[StartRepair] invoked at Time.time={Time.time}");

            // Teleport player backward a short distance so the repair kit has valid throwing/using space
            if (this.repairTeleportBackEnabled)
            {
                try
                {
                    GameObject p = GetPlayer();
                    if (p != null)
                    {
                        Vector3 cur = p.transform.position;
                        Vector3 back = Vector3.zero;
                        try { back = p.transform.forward; } catch { back = new Vector3(0f, 0f, 1f); }
                        Vector3 target = cur - back.normalized * this.repairTeleportBackDistance;
                        target.y = cur.y; // preserve vertical position
                        MelonLogger.Msg($"[StartRepair] Teleporting player backward from {cur} to {target}");
                        TeleportTo(target);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg("[StartRepair] Teleport backward failed: " + ex.Message);
                }
            }

            // Determine whether this was triggered by durability detection (auto)
            // and initialize auto-repair counters accordingly. We use the
            // `lastStartWasAutoRepair` flag (set by the caller) to detect auto
            // starts; clear it immediately after reading.
            isAutoRepairRunning = this.lastStartWasAutoRepair;
            this.lastStartWasAutoRepair = false;
            autoRepairUseCount = 0;
            autoRepairWaiting = false;
            // compute repairUsesTarget based on selected repair item key
            try
            {
                string key = (autoRepairType >= 0 && autoRepairType < autoRepairKeys.Length) ? autoRepairKeys[autoRepairType] : autoRepairKeys[0];
                repairUsesTarget = (key == "toolrestorer_toolrestorer_1") ? 2 : 1;
            }
            catch
            {
                repairUsesTarget = 1;
            }

            isRepairing = true;
            repairStep = 0;
            scrollAttempts = 0;
            stepTimer = Time.time;

            // Pause resource farm teleports while repairing to avoid overlapping actions
            this.resourceRepairPauseUntil = Time.time + this.resourceAutoRepairPauseSeconds;
        }

        void StartAutoEat()
        {
            isAutoEating = true;
            autoEatStep = 0;
            autoEatScrollAttempts = 0;
            autoEatAttempts = 0;
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
                        stepTimer = Time.time;
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
                        stepTimer = Time.time;
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
                        // Count this use and decide whether to perform another use
                        autoRepairUseCount++;
                        CloseInventory();
                        // If this was an auto-repair run and we haven't reached the target, wait then retry
                        if (isAutoRepairRunning && autoRepairUseCount < repairUsesTarget)
                        {
                            autoRepairWaiting = true;
                            autoRepairWaitTimer = Time.time + autoRepairWaitDuration;
                            stepTimer = autoRepairWaitTimer;
                            // set to a waiting state (4) — when timer expires we'll restart from step 0
                            repairStep = 4;
                        }
                        else
                        {
                            isRepairing = false;
                            repairStep = 0;
                        }
                    }
                    else
                    {
                        CloseInventory();
                        isRepairing = false;
                        repairStep = 0;
                    }
                    break;

                case 4:
                    // Waiting between repeated auto-repair uses
                    if (autoRepairWaiting && Time.time >= autoRepairWaitTimer)
                    {
                        autoRepairWaiting = false;
                        // start the next repair cycle by reopening the inventory
                        repairStep = 0;
                        stepTimer = Time.time;
                    }
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
                        autoEatStepTimer = Time.time;
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
                        // Count this successful use immediately so attempts match actual uses
                        autoEatAttempts++;
                        autoEatStep++;
                        autoEatStepTimer = Time.time;
                    }
                    else
                    {
                        CloseInventory();
                        isAutoEating = false;
                    }
                    break;

                case 4:
                    // Check if we need to eat more
                    if (IsEnergyLow() && autoEatAttempts < this.maxAutoEatAttempts)
                    {
                        autoEatStep = 1;
                        autoEatScrollAttempts = 0;
                        autoEatStepTimer = Time.time;
                        MelonLogger.Msg($"[Auto Eat] Energy still low ({GetCurrentEnergy()*100:F0}%), eating another {this.autoEatFoodOptions[this.autoEatFoodType]}... (attempt {autoEatAttempts}/{this.maxAutoEatAttempts})");
                    }
                    else
                    {
                        // Energy is full or max attempts reached, stop eating and close inventory
                        CloseInventory();
                        isAutoEating = false;
                        autoEatStep = 0;
                        if (IsEnergyFull())
                        {
                            MelonLogger.Msg("[Auto Eat] Energy restored to maximum!");
                        }
                        else
                        {
                            MelonLogger.Msg($"[Auto Eat] Stopped after {autoEatAttempts} attempts - energy at {GetCurrentEnergy()*100:F0}%");
                        }
                    }
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

        // Returns Images scoped to the open bag panel — avoids scene-wide FindObjectsOfType allocation
        private Image[] GetBagPanelImages()
        {
            GameObject bag = GameObject.Find(BAG_PANEL_PATH);
            if (bag == null || !bag.activeInHierarchy) return System.Array.Empty<Image>();
            return bag.GetComponentsInChildren<Image>(true);
        }

        bool IsRepairKitVisible()
        {
            string target = "ui_item_normal_p_" + GetCurrentRepairKey();
            foreach (Image img in GetBagPanelImages())
            {
                if (img != null && img.sprite != null && img.gameObject.activeInHierarchy
                    && img.sprite.name == target) return true;
            }
            return false;
        }

        bool ClickRepairKit()
        {
            string target = "ui_item_normal_p_" + GetCurrentRepairKey();
            Image kit = null;
            foreach (Image img in GetBagPanelImages())
            {
                if (img != null && img.sprite != null && img.gameObject.activeInHierarchy
                    && img.sprite.name == target) { kit = img; break; }
            }

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

        private string GetCurrentFoodKey()
        {
            return this.autoEatFoodKeys[this.autoEatFoodType];
        }

        private string GetCurrentRepairKey()
        {
            return this.autoRepairKeys[this.autoRepairType];
        }

        bool IsFoodVisible()
        {
            string foodKey = GetCurrentFoodKey();
            foreach (Image img in GetBagPanelImages())
            {
                if (img != null && img.sprite != null && img.gameObject.activeInHierarchy
                    && img.sprite.name.ToLowerInvariant().Contains(foodKey)) return true;
            }
            return false;
        }

        bool ClickFoodItem()
        {
            string foodKey = GetCurrentFoodKey();
            Image food = null;
            foreach (Image img in GetBagPanelImages())
            {
                if (img != null && img.sprite != null && img.gameObject.activeInHierarchy
                    && img.sprite.name.ToLowerInvariant().Contains(foodKey)) { food = img; break; }
            }

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
            
            // Header - At top of main content card
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };
            GUI.Label(new Rect(113f, (float)num, 260f, 25f), "KEYBIND SETTINGS", headerStyle);
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
                            case "Auto Foraging": this.keyAutoForaging = newKey; break;
                            case "Auto Fish Farm (Auto Teleport)": this.keyAutoFishingTeleport = newKey; break;
                            case "Auto Fish (No Teleport)": this.keyAutoFish = newKey; break;
                            case "Auto Cook": this.keyAutoCook = newKey; break;
                            case "Bypass UI": this.keyBypassUI = newKey; break;
                            case "Disable All": this.keyDisableAll = newKey; break;
                            case "Inspect Player": this.keyInspectPlayer = newKey; break;
                            case "Inspect Move": this.keyInspectMove = newKey; break;
                            case "Auto Repair": this.keyAutoRepair = newKey; break;
                            case "Auto Join Friend": this.keyAutoJoinFriend = newKey; break;
                            case "Auto Snow Sculpture": this.autoSnowHotkey = newKey; break;
                            case "Noclip": this.keyNoclip = newKey; break;
                            case "Join Public": this.keyJoinPublic = newKey; break;
                            case "Join My Town": this.keyJoinMyTown = newKey; break;
                            case "Auto Eat": this.keyAutoEat = newKey; break;
                            case "Anti AFK": this.keyAntiAfk = newKey; break;
                            case "Bypass Overlap": this.keyBypassOverlap = newKey; break;
                            case "Bird Vacuum": this.keyBirdVacuum = newKey; break;
                            case "Game Speed 1x": this.keyGameSpeed1x = newKey; break;
                            case "Game Speed 2x": this.keyGameSpeed2x = newKey; break;
                            case "Game Speed 5x": this.keyGameSpeed5x = newKey; break;
                            case "Game Speed 10x": this.keyGameSpeed10x = newKey; break;
                            case "Equip Axe": this.keyEquipAxe = newKey; break;
                            case "Equip Net": this.keyEquipNet = newKey; break;
                            case "Equip Rod": this.keyEquipRod = newKey; break;
                        }
                        this.keyBindingActive = "";
                        this.keyBindAssignedAt = Time.unscaledTime;
                        this.SaveKeybinds(false);
                        string keyName = newKey == KeyCode.None ? "None" : newKey.ToString();
                        this.AddMenuNotification($"{bindingLabel}: {keyName}", new Color(this.uiAccentR, this.uiAccentG, this.uiAccentB));
                    }
                }
                return (float)num + 450f;
            }

            // Bind List
            this.DrawKeybindRow(ref num, "Toggle Menu", ref this.keyToggleMenu);
            this.DrawKeybindRow(ref num, "Toggle Radar", ref this.keyToggleRadar);
            this.DrawKeybindRow(ref num, "Auto Foraging", ref this.keyAutoForaging);
            this.DrawKeybindRow(ref num, "Auto Fish (No Teleport)", ref this.keyAutoFish);
            this.DrawKeybindRow(ref num, "Auto Fishing (Teleport)", ref this.keyAutoFishingTeleport);
            this.DrawKeybindRow(ref num, "Auto Cook", ref this.keyAutoCook);
            this.DrawKeybindRow(ref num, "Bypass UI", ref this.keyBypassUI);
            this.DrawKeybindRow(ref num, "Disable All", ref this.keyDisableAll);
            this.DrawKeybindRow(ref num, "Inspect Player", ref this.keyInspectPlayer);
            this.DrawKeybindRow(ref num, "Inspect Move", ref this.keyInspectMove);
            this.DrawKeybindRow(ref num, "Auto Repair", ref this.keyAutoRepair);
            this.DrawKeybindRow(ref num, "Auto Join Friend", ref this.keyAutoJoinFriend);
            this.DrawKeybindRow(ref num, "Noclip", ref this.keyNoclip);
            this.DrawKeybindRow(ref num, "Join Public", ref this.keyJoinPublic);
            this.DrawKeybindRow(ref num, "Join My Town", ref this.keyJoinMyTown);
            this.DrawKeybindRow(ref num, "Auto Eat", ref this.keyAutoEat);
            this.DrawKeybindRow(ref num, "Anti AFK", ref this.keyAntiAfk);
            this.DrawKeybindRow(ref num, "Auto Snow Sculpture", ref this.autoSnowHotkey);
            this.DrawKeybindRow(ref num, "Bypass Overlap", ref this.keyBypassOverlap);
            this.DrawKeybindRow(ref num, "Bird Vacuum", ref this.keyBirdVacuum);
            this.DrawKeybindRow(ref num, "Game Speed 1x", ref this.keyGameSpeed1x);
            this.DrawKeybindRow(ref num, "Game Speed 2x", ref this.keyGameSpeed2x);
            this.DrawKeybindRow(ref num, "Game Speed 5x", ref this.keyGameSpeed5x);
            this.DrawKeybindRow(ref num, "Game Speed 10x", ref this.keyGameSpeed10x);
            this.DrawKeybindRow(ref num, "Equip Axe", ref this.keyEquipAxe);
            this.DrawKeybindRow(ref num, "Equip Net", ref this.keyEquipNet);
            this.DrawKeybindRow(ref num, "Equip Rod", ref this.keyEquipRod);
            num += 6;

            if (this.DrawDangerActionButton(new Rect(20f, (float)num, 260f, 30f), "RESET TO DEFAULTS"))
            {
                this.keyToggleMenu = KeyCode.Insert;
                this.keyToggleRadar = KeyCode.None;
                this.keyAutoForaging = KeyCode.None;
                this.keyAutoFish = KeyCode.None;
                this.keyAutoFishingTeleport = KeyCode.None;
                this.keyAutoCook = KeyCode.None;
                this.keyBypassUI = KeyCode.None;
                this.keyDisableAll = KeyCode.None;
                this.keyInspectPlayer = KeyCode.None;
                this.keyInspectMove = KeyCode.None;
                this.keyAutoRepair = KeyCode.None;
                this.keyAutoJoinFriend = KeyCode.None;
                this.keyNoclip = KeyCode.None;
                this.keyJoinPublic = KeyCode.None;
                this.keyJoinMyTown = KeyCode.None;
                this.keyAutoEat = KeyCode.None;
                this.keyAntiAfk = KeyCode.None;
                this.autoSnowHotkey = KeyCode.None;
                this.keyBypassOverlap = KeyCode.None;
                this.keyBirdVacuum = KeyCode.None;
                this.keyGameSpeed1x = KeyCode.None;
                this.keyGameSpeed2x = KeyCode.None;
                this.keyGameSpeed5x = KeyCode.None;
                this.keyGameSpeed10x = KeyCode.None;
                this.keyEquipAxe = KeyCode.None;
                this.keyEquipNet = KeyCode.None;
                this.keyEquipRod = KeyCode.None;
                this.notificationsEnabled = true;
                this.hideIdEnabled = false;
                this.showStatusOverlay = false;
                this.SaveKeybinds(false);
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
                this.SaveKeybinds(false);
                if (this.notificationsEnabled)
                {
                    this.AddMenuNotification("Notifications enabled", new Color(0.55f, 0.88f, 1f));
                }
            }
            
            num += 26;
            bool newAutoClickStart = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 24f), this.autoClickStartEnabled, "Auto Start on Lobby");
            if (newAutoClickStart != this.autoClickStartEnabled)
            {
                this.autoClickStartEnabled = newAutoClickStart;
                this.SaveKeybinds(false);
                if (this.autoClickStartEnabled)
                {
                    this.AddMenuNotification("Auto Start enabled", new Color(0.55f, 0.88f, 1f));
                }
                else
                {
                    this.AddMenuNotification("Auto Start disabled", new Color(0.88f, 0.6f, 0.6f));
                }
            }

            num += 26;
            bool newHideIdEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 24f), this.hideIdEnabled, "Hide ID");
            if (newHideIdEnabled != this.hideIdEnabled)
            {
                this.hideIdEnabled = newHideIdEnabled;
                this.SaveKeybinds(false);
                if (this.hideIdEnabled)
                {
                    this.AddMenuNotification("ID display hidden", new Color(0.55f, 0.88f, 1f));
                }
                else
                {
                    this.AddMenuNotification("ID display shown", new Color(0.55f, 0.88f, 1f));
                }
            }

            num += 26;
            bool newShowOverlay = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 24f), this.showStatusOverlay, "Show Status Overlay");
            if (newShowOverlay != this.showStatusOverlay)
            {
                this.showStatusOverlay = newShowOverlay;
                this.SaveKeybinds(false);
                if (this.showStatusOverlay)
                {
                    this.AddMenuNotification("Status overlay enabled", new Color(0.55f, 0.88f, 1f));
                }
                else
                {
                    this.AddMenuNotification("Status overlay disabled", new Color(0.88f, 0.6f, 0.6f));
                }
            }

            num += 26;
            bool newBlockGameUi = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 24f), this.blockGameUiWhenMenuOpen, "Block Input");
            if (newBlockGameUi != this.blockGameUiWhenMenuOpen)
            {
                this.blockGameUiWhenMenuOpen = newBlockGameUi;
                this.SaveKeybinds(false);
                if (this.blockGameUiWhenMenuOpen)
                {
                    this.AddMenuNotification("Block Input Enabled", new Color(0.55f, 0.88f, 1f));
                }
                else
                {
                    this.AddMenuNotification("Block Input Disabled", new Color(0.88f, 0.6f, 0.6f));
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
                // Auto-save theme when any picker/transparency changed
                try { this.SaveUiTheme(); this.AddMenuNotification("UI theme auto-saved", new Color(0.55f, 0.88f, 1f)); } catch { }
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

            GUI.Label(new Rect(20f, (float)num, 360f, 40f), "Tip: Use SAVE to persist theme.\nConfig file: UserData/Config.json");
            return (float)num + 50f;
        }

        private float DrawSelfTab(int startY)
        {
            int num = startY + 25;

            if (this.selfSubTab == 0)
            {
                // Noclip Toggle
                this.noclipEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.noclipEnabled, "Noclip");
                if (this.noclipEnabled)
                {
                    // Set override position to current position when enabled
                    GameObject player = GetPlayer();
                    if (player != null)
                    {
                        HeartopiaComplete.OverridePosition = player.transform.position;
                    }
                    HeartopiaComplete.OverridePlayerPosition = true;
                }
                else
                {
                    HeartopiaComplete.OverridePlayerPosition = false;
                }
                num += 30;

                GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Noclip Speed: {this.noclipSpeed:F1}");
                num += 22;
                float prevNoclipSpeed = this.noclipSpeed;
                this.noclipSpeed = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.noclipSpeed, 5f, 50f);
                if (Math.Abs(this.noclipSpeed - prevNoclipSpeed) > 0.0001f)
                {
                    try { this.SaveKeybinds(false); } catch {}
                }
                num += 30;

                GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"Noclip Boost: {this.noclipBoostMultiplier:F1}x");
                num += 22;
                float prevNoclipBoost = this.noclipBoostMultiplier;
                this.noclipBoostMultiplier = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.noclipBoostMultiplier, 1f, 5f);
                if (Math.Abs(this.noclipBoostMultiplier - prevNoclipBoost) > 0.0001f)
                {
                    try { this.SaveKeybinds(false); } catch {}
                }
                num += 30;

                bool newAntiAfk = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.antiAfkEnabled, "Anti AFK (Auto Click)");
                if (newAntiAfk != this.antiAfkEnabled)
                {
                    this.antiAfkEnabled = newAntiAfk;
                    this.lastAntiAfkPulseAt = Time.unscaledTime;
                    AutoFishLogic.SimulateMouseButton0 = false;
                    AutoFishLogic.SimulateMouseButton0Down = false;
                    this.antiAfkMouseDownClearAt = 0f;
                    this.antiAfkMouseHoldClearAt = 0f;
                    this.SaveKeybinds(false);
                    this.AddMenuNotification($"Anti AFK {(this.antiAfkEnabled ? "Enabled" : "Disabled")}", this.antiAfkEnabled ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f));
                }
                num += 26;

                GUI.Label(new Rect(20f, (float)num, 260f, 20f), $"AFK Click Interval: {this.antiAfkInterval:F0}s");
                num += 22;
                float newAfkInterval = this.DrawAccentSlider(new Rect(20f, (float)num, 260f, 20f), this.antiAfkInterval, 5f, 120f);
                if (Math.Abs(newAfkInterval - this.antiAfkInterval) > 0.01f)
                {
                    this.antiAfkInterval = newAfkInterval;
                    this.SaveKeybinds(false);
                }
                num += 30;

                GUI.Label(new Rect(20f, (float)num, 260f, 120f), $"Noclip: WASD + Space/Ctrl\nShift = Speed Boost");
                return (float)num + 160f;
            }

            // Building sub-tab: Bypass overlap toggle
            if (this.selfSubTab == 1)
            {
                GUI.Label(new Rect(20f, (float)num, 260f, 20f), "Building - Bypass Overlap");
                num += 26;

                this.bypassOverlapEnabled = this.DrawSwitchToggle(new Rect(20f, (float)num, 260f, 25f), this.bypassOverlapEnabled, "Bypass Overlap");
                // reflect into static flag used by prefix
                HeartopiaComplete.bypassOverlapEnabledStatic = this.bypassOverlapEnabled;
                if (this.bypassOverlapEnabled && !this.bypassOverlapPatched)
                {
                    this.EnsureBypassPatched();
                }
                num += 36;

                GUI.Label(new Rect(20f, (float)num, 260f, 120f), $"Credits: • evermoreee12 for Bypass Overlap");
                return (float)num + 50f;
            }

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
            // Allow long labels to wrap and keep the bind button readable.
            GUIStyle lblStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            float labelWidth = 140f;
            GUIContent content = new GUIContent(label);
            float labelH = lblStyle.CalcHeight(content, labelWidth);
            labelH = Mathf.Max(labelH, 20f);

            GUI.Label(new Rect(20f, (float)y, labelWidth, labelH), label, lblStyle);

            string btnText = key.ToString();
            // Shorten some long key names
            if (btnText.StartsWith("Joystick")) btnText = "Gamepad";

            // Keep button at fixed width to the right of label area
            float btnX = 160f;
            float btnW = 120f;
            float btnY = y + (labelH > 25f ? (labelH - 25f) * 0.5f : 0f);
            if (GUI.Button(new Rect(btnX, btnY, btnW, 25f), btnText))
            {
                this.keyBindingActive = label;
            }

            y += (int)(labelH + 10f);
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
        public static HeartopiaComplete Instance;

        // Token: 0x04000003 RID: 3
        private new static HarmonyLib.Harmony harmonyInstance;

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
        private bool blockGameUiWhenMenuOpen = true;
        private bool showStatusOverlay = false;
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
            new HeartopiaComplete.FarmLocation("Fiddlehead Event Area", new Vector3(229.782f, 11.404f, 48.837f), "event_fiddlehead"),
            new HeartopiaComplete.FarmLocation("Tall Mustard Event Area", new Vector3(-125.213f, 11.729f, 290.797f), "event_tall_mustard"),
            new HeartopiaComplete.FarmLocation("Mustard Greens Event Area", new Vector3(-58.984f, 11.035f, -155.413f), "event_mustard_greens"),
            new HeartopiaComplete.FarmLocation("Burdock Event Area", new Vector3(-211.599f, 29.916f, 35.416f), "event_burdock"),
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
        // Radar subtab (0 = Main, 1 = Settings)
        private int radarSubTab = 0;
        // Radar marker visual style: 0 = Default (existing), 1 = Simple Text
        private int radarMarkerStyle = 0;

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
        private Vector3 lastPlayerVelocity = Vector3.zero;
        private Vector3 jumpBoostStartPos = Vector3.zero;
        private Vector3 jumpBoostTargetPos = Vector3.zero;
        private bool customCameraFOVEnabled = false;
        private float cameraFOV = 60f;
        private float originalFOV = -1f;
        private float liveCameraFOVBase = -1f;
        private float lastAppliedCustomCameraFOV = -1f;
        private Camera mainCamera = null;

        // Advanced Cooking Bot Variables
        private bool cookingCleanupMode = false;
        private const float cookingPlayerAlertRadius = 25f;
        private float lastPlayerDetectionCheckAt = -999f;
        private float cookingAutoSpeed = 7f;
        private bool cookingPatrolEnabled = false;
        private bool cookingPanelClosed = false;
        private float cookingPanelClosedTime = 0f;
        private int cookMoveKeyIndex = -1;
        private float lastConfirmClickTime = -999f;
        private float lastCookingTimerSeenAt = -999f;
        private float cookingTakeoutSafetyDelay = 0.55f;
        private float lastCookRefreshClickAt = -999f;
        private float lastCookConfirmClickAt = -999f;
        private readonly List<Image> cookImageScanBuffer = new List<Image>(256);
        private float nextCookingCleanupScanAt = 0f;
        private bool lastCookingCleanupResult = false;
        // Simulated F-key helper scheduling
        private float nextSimulatedFKeyClearAt = 0f;
        private float nextSimulatedFKeyUpClearAt = 0f;
        // Auto-cook diagnostics
        private int autoCookLoopTicks = 0;
        private float lastAutoCookHeartbeatAt = -999f;
        private string lastAutoCookException = null;
        // Auto Cook auto-stop timer fields
        private bool autoCookAutoStopEnabled = false;
        private int autoCookAutoStopHours = 0;
        private int autoCookAutoStopMinutes = 0;
        private int autoCookAutoStopSeconds = 0;
        private string autoCookAutoStopHoursInput = "0";
        private string autoCookAutoStopMinutesInput = "0";
        private string autoCookAutoStopSecondsInput = "0";
        private float autoCookAutoStopAt = -1f;
        private int autoFarmSubTab = 0; // 0 = Main, 1 = Tree Farm, 2 = Fish Farm, 3 = Insect Farm
        private int automationSubTab = 0; // 0 = Main, 1 = Bag, 2 = Tools, 3 = Sculpture, 4 = Cat Play
        private int selfSubTab = 0; // 0 = Main
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
        private float lastAutoSwingTime = 0f;
        private float swingCooldown = 1.2f;
        // Non-blocking swing confirmation state
        private bool awaitingSwingConfirm = false;
        private float swingConfirmDeadline = 0f;
        private int swingConfirmStartAnimHash = 0;
        private bool swingConfirmStartBtnInteract = false;
        private readonly string swingButtonPath = "GameApp/startup_root(Clone)/XDUIRoot/Bottom/TrackingPanel(Clone)/tracking_bar@w/tracking_sand_swing@go@w/root_visible@go/swing@btn";
        private float treeFarmArrivalDelay = 3f;
        private float treeFarmNextLocationWait = 1.5f;
        private string treeFarmStatus = "Idle";
        // If true, tree farm will use hardcoded map positions (like Resource farm)
        private bool treeFarmUseHardcoded = false;

        // Auto Snow Sculpture fields (ported from decompiled helper)
        private bool autoSnowEnabled = false;
        private float snowClickInterval = 0.02f;
        private float lastSnowClickTime = 0f;
        private int snowClickCount = 0;
        private KeyCode autoSnowHotkey = KeyCode.None;
        private bool isListeningForAutoSnowHotkey = false;
        private Queue<GameObject> snowWidgetQueue = new Queue<GameObject>();
        // Rapid tracking icon click (Sculpture subtab)
        private bool autoSculptureIconRapidEnabled = false;
        private float sculptIconClickInterval = 0.05f;
        private float lastSculptIconClickTime = 0f;
        // Auto Cat Play fields
        private bool autoCatPlayEnabled = false;
        private float catClickInterval = 0.08f; // 80ms default
        private float lastCatClickTime = 0f;
        private int catClickCount = 0;
        // Auto Buy fields
        private bool autoBuyEnabled = false;
        private int autoBuySubState = 0; // 0=idle,1=teleporting,2=waiting_dialogue,3=selecting_store,4=buying,5=returning
        private Vector3 autoBuySavedPosition = Vector3.zero;
        private int autoBuyCurrentIngredientIndex = 0;
        private int autoBuyPurchasedCount = 0;
        private int autoBuyMaxPerIngredient = 50;
        private float autoBuyStepTimer = 0f;
        private float autoBuyShopWaitStartedAt = 0f;
        private int autoBuyStoreSelectRetryCount = 0;
        private float autoBuyPreviousGameSpeed = 1f;
        private bool autoBuyForcedGameSpeed = false;
        private readonly Vector3 autoBuyTargetPos = new Vector3(-12.574f, 31.572f, 43.554f);
        private readonly Vector3 autoBuyNearbyPos = new Vector3(-12.692f, 31.599f, 39.133f);
        private readonly string[] autoBuyIngredientsMatch = new string[] {
            // Exact display names from shop UI (used for reliable matching)
            "Egg",
            "Meat",
            "Red Bean",
            "Egg",
            "Milk",
            "Rice Flour",
            "Tea Leaves",
            "Oil",
            "Matcha Powder",
            "Cheese",
            "Butter",
            "Coffee Beans"
        };

        // Hardcoded resource position arrays (ported from decompiled map data)
        private static readonly Vector3[] RockPositions = new Vector3[]
        {
            new Vector3(-31.7f, 20.1f, 115.2f),
            new Vector3(-10.3f, 21.4f, 107.6f),
            new Vector3(15.3f, 21.5f, 102.1f),
            new Vector3(47.9f, 21f, 96.7f),
            new Vector3(66.9f, 21.8f, 91.5f),
            new Vector3(66.7f, 20.5f, 138.9f),
            new Vector3(16.4f, 21.4f, 146.2f),
            new Vector3(-47.1f, 20f, 166.1f),
            new Vector3(-133.4f, 21.5f, 63.9f),
            new Vector3(-83.9f, 20.4f, 120.6f),
            new Vector3(-130.2f, 22.4f, 41.2f),
            new Vector3(-177f, 21.4f, 47.4f),
            new Vector3(-123.9f, 21.8f, -2f),
            new Vector3(-125.6f, 20f, -17.9f),
            new Vector3(-119.1f, 19.9f, -45.2f),
            new Vector3(-109.2f, 19.1f, -64f),
            new Vector3(-95.9f, 20f, -79.4f),
            new Vector3(95.9f, 23.4f, 70.6f),
            new Vector3(142.1f, 23f, 66f),
            new Vector3(97.4f, 22.7f, 50.6f)
        };

        private static readonly Vector3[] OrePositions = new Vector3[]
        {
            new Vector3(-152.3f, 21f, -86.1f),
            new Vector3(-131.3f, 19.2f, -70.4f),
            new Vector3(-169.3f, 19.9f, -34.2f),
            new Vector3(-145.2f, 20.2f, -16.8f),
            new Vector3(-175f, 20.4f, 6.4f),
            new Vector3(-144.1f, 20.7f, 28f),
            new Vector3(-178.4f, 21.4f, 66.3f),
            new Vector3(-98.6f, 20.3f, 171f),
            new Vector3(-74.1f, 20.1f, 149.6f),
            new Vector3(-26.9f, 20f, 161.5f),
            new Vector3(-22.6f, 20.1f, 131.4f),
            new Vector3(21.2f, 21.6f, 146.2f),
            new Vector3(35.3f, 21.2f, 127.2f),
            new Vector3(80.8f, 20.5f, 135.8f),
            new Vector3(141.5f, 23.1f, 78.5f),
            new Vector3(142f, 23.2f, 27.8f),
            new Vector3(130.8f, 21.6f, -6.6f),
            new Vector3(141.6f, 21.9f, -37.9f),
            new Vector3(110.5f, 20.9f, -63.4f),
            new Vector3(115.4f, 20.6f, -106.7f)
        };

        // Hardcoded tree position arrays (ported from decompiled map data)
        private static readonly Vector3[] TreePositions = new Vector3[]
        {
            new Vector3(139.7f, 21.7f, -44.3f),
            new Vector3(144.5f, 22.2f, -42.1f),
            new Vector3(145.2f, 22.3f, -44.1f),
            new Vector3(141.8f, 21.8f, -49.3f),
            new Vector3(152.2f, 21.2f, -50f),
            new Vector3(156.8f, 21f, -38.4f),
            new Vector3(161.7f, 21f, -35.7f),
            new Vector3(160.8f, 21.1f, -32.6f),
            new Vector3(178.4f, 21.7f, -6.1f),
            new Vector3(183.2f, 22.5f, -13.3f),
            new Vector3(198.2f, 17.5f, -23.4f),
            new Vector3(200.2f, 17.3f, -22f),
            new Vector3(198.9f, 17.6f, -17.1f),
            new Vector3(171.6f, 21.3f, 6.5f),
            new Vector3(183.9f, 24.2f, 16f),
            new Vector3(190.4f, 24.1f, 22.4f),
            new Vector3(190.4f, 25.9f, 30.1f),
            new Vector3(170.449f,20.501f,-39.699f),
            new Vector3(174.855f,20.990f,-36.675f),
            new Vector3(173.392f,20.410f,-46.537f),
            new Vector3(173.176f,20.615f,-48.654f),
            new Vector3(179.145f,20.299f,-46.644f),
            new Vector3(183.923f,19.960f,-37.363f),
            new Vector3(191.932f,17.834f,-46.321f),
            new Vector3(195.248f,17.604f,-38.141f),
            new Vector3(197.057f,17.686f,-41.829f),
            new Vector3(198.403f,17.369f,-44.197f),
            new Vector3(198.888f,17.338f,-39.695f),
            new Vector3(203.974f,15.249f,-39.667f),
            new Vector3(209.787f,12.042f,-49.710f),
            new Vector3(190.390f,19.568f,-19.386f),
            new Vector3(196.372f,19.753f,7.629f),
            new Vector3(197.307f,23.185f,14.063f),
            new Vector3(198.850f,23.215f,12.024f),
            new Vector3(206.653f,22.930f,12.314f),
            new Vector3(211.267f,22.288f,15.947f),
            new Vector3(209.340f,22.750f,19.160f),
            new Vector3(204.159f,23.431f,21.673f),
            new Vector3(207.259f,23.441f,33.904f),
            new Vector3(204.171f,23.436f,36.241f),
            new Vector3(207.511f,23.013f,40.964f),
            new Vector3(197.897f,27.291f,32.530f),
            new Vector3(196.316f,26.262f,38.628f),
            new Vector3(191.472f,24.962f,48.498f),
            new Vector3(194.644f,24.920f,52.629f),
            new Vector3(193.433f,25.541f,54.969f),
            new Vector3(190.077f,25.667f,55.608f)
        };

        private static readonly Vector3[] RareTreePositions = new Vector3[]
        {
            new Vector3(-91.5f, 20.4f, -83.1f),
            new Vector3(-64.1f, 20.4f, -68.5f),
            new Vector3(-123.1f, 27.3f, 100.8f),
            new Vector3(56f, 23.7f, -70.4f),
            new Vector3(-106.8f, 25.4f, 60.7f),
            new Vector3(55.8f, 25.4f, 73.9f),
            new Vector3(75.3f, 22.5f, 87.5f),
            new Vector3(84.2f, 18.5f, -114.4f)
        };

        private static readonly Vector3[] AppleTreePositions = new Vector3[]
        {
            new Vector3(141.7f, 22.9f, 74.7f),
            new Vector3(113.1f, 22.9f, 40.1f),
            new Vector3(141.4f, 22.9f, 18f),
            new Vector3(106f, 22.9f, 0.9f),
            new Vector3(105.5f, 21.5f, -14.9f),
            new Vector3(92.8f, 21.8f, -20.3f),
            new Vector3(131.1f, 21.4f, -41.9f),
            new Vector3(92.4f, 21.7f, -44.3f),
            new Vector3(109.2f, 21.4f, -52.8f),
            new Vector3(95.1f, 20.6f, -85.7f),
            new Vector3(73.2f, 20.6f, -78f),
            new Vector3(75.1f, 20.6f, -90.3f),
            new Vector3(18.3f, 21.6f, 146.7f),
            new Vector3(36.3f, 20.6f, 115.9f),
            new Vector3(43.7f, 20.5f, 98.5f),
            new Vector3(67.2f, 20.9f, 94.4f),
            new Vector3(-16.1f, 22.9f, 103.0f),
            new Vector3(8.5f, 23.2f, 97.2f),
            new Vector3(28.7f, 22.7f, 96.4f),
            new Vector3(75.5f, 20.7f, 93.4f),
            new Vector3(63.2f, 20.6f, 139.7f),
            new Vector3(94.2f, 22.3f, 55.6f),
            new Vector3(98.1f, 23.1f, 75.3f),
            new Vector3(97.4f, 22.9f, 46.3f),
            new Vector3(94.2f, 22.4f, 29.1f),
            new Vector3(94.7f, 22.6f, 6.3f),
            new Vector3(84.7f, 20.8f, -65.5f),
            new Vector3(125.1f, 21.2f, -89.6f)
        };

        private static readonly Vector3[] OrangeTreePositions = new Vector3[]
        {
            new Vector3(-107.4f, 19.1f, -67f),
            new Vector3(-139.3f, 19.4f, -105.9f),
            new Vector3(-159.4f, 20.2f, -64.3f),
            new Vector3(-120.4f, 20.2f, -29.9f),
            new Vector3(-123.6f, 20.2f, -22.8f),
            new Vector3(-125.4f, 21.4f, -8.9f),
            new Vector3(-121.1f, 22.2f, -6.1f),
            new Vector3(-174f, 20.4f, 9.2f),
            new Vector3(-155.7f, 20.8f, 30.5f),
            new Vector3(-127.4f, 23f, 40.5f),
            new Vector3(-131.6f, 22f, 69.8f),
            new Vector3(-131.2f, 22.3f, 76.4f),
            new Vector3(-95.8f, 20.6f, 118.4f),
            new Vector3(-81.6f, 20.7f, 118.4f),
            new Vector3(-67.1f, 20.4f, 139.4f),
            new Vector3(-32.8f, 20.2f, 160.3f),
            new Vector3(-36.1f, 20.2f, 115.5f),
            new Vector3(-56.5f, 20.2f, 120.4f),
            new Vector3(-65.5f, 21.0f, 120.2f),
            new Vector3(-93.7f, 20.1f, 166.9f),
            new Vector3(-114.2f, 20.3f, 128.9f),
            new Vector3(-177.1f, 21.6f, 38.8f),
            new Vector3(-126.4f, 22f, 23.5f),
            new Vector3(-117.3f, 19.9f, -50.6f),
            new Vector3(-134.5f, 19.2f, -66.4f),
            new Vector3(-94.2f, 19.6f, -89.1f),
            new Vector3(-92.8f, 19.3f, -95.2f)
        };

        // Tree cooldown bookkeeping for hardcoded resource-style mode
        private Dictionary<int, float> treeCooldowns = new Dictionary<int, float>();
        private Dictionary<int, float> rareTreeCooldowns = new Dictionary<int, float>();
        private Dictionary<int, float> appleTreeCooldowns = new Dictionary<int, float>();
        private Dictionary<int, float> orangeTreeCooldowns = new Dictionary<int, float>();

        private Dictionary<int, float> treeHideUntil = new Dictionary<int, float>();
        private Dictionary<int, float> rareTreeHideUntil = new Dictionary<int, float>();
        private Dictionary<int, float> appleTreeHideUntil = new Dictionary<int, float>();
        private Dictionary<int, float> orangeTreeHideUntil = new Dictionary<int, float>();

        private float treeCooldownDuration = 300f;
        private float rareTreeCooldownDuration = 600f;
        private float appleTreeCooldownDuration = 300f;
        private float orangeTreeCooldownDuration = 300f;

        private float treeHideDelay = 10f;

        private System.Random instanceRng = new System.Random();
        // Auto Resource Farm fields (ported and adapted)
        private bool autoResourceFarmEnabled = false;
        private bool farmRocks = false;
        private bool farmOres = false;
        private bool farmTrees = true;
        private bool farmRareTrees = false;
        private bool farmAppleTrees = false;
        private bool farmOrangeTrees = false;

        public Dictionary<int, float> rockCooldowns = new Dictionary<int, float>();
        public Dictionary<int, float> oreCooldowns = new Dictionary<int, float>();
        public Dictionary<int, float> treeCooldowns_res = new Dictionary<int, float>();
        public Dictionary<int, float> rareTreeCooldowns_res = new Dictionary<int, float>();
        public Dictionary<int, float> appleTreeCooldowns_res = new Dictionary<int, float>();
        public Dictionary<int, float> orangeTreeCooldowns_res = new Dictionary<int, float>();

        public Dictionary<int, float> rockHideUntil = new Dictionary<int, float>();
        public Dictionary<int, float> oreHideUntil = new Dictionary<int, float>();
        public Dictionary<int, float> treeHideUntil_res = new Dictionary<int, float>();
        public Dictionary<int, float> rareTreeHideUntil_res = new Dictionary<int, float>();
        public Dictionary<int, float> appleTreeHideUntil_res = new Dictionary<int, float>();
        public Dictionary<int, float> orangeTreeHideUntil_res = new Dictionary<int, float>();

        public float rockCooldownDuration = 300f;
        public float oreCooldownDuration = 300f;
        public float treeCooldownDuration_res = 300f;
        public float rareTreeCooldownDuration_res = 600f;
        public float appleTreeCooldownDuration_res = 300f;
        public float orangeTreeCooldownDuration_res = 300f;

        private List<Vector3> resourceMarkerPositions = new List<Vector3>();
        private int currentResourceMarkerIndex = 0;
        private bool resourceMarkersNeedShuffle = true;
        private HashSet<int> visitedResourceMarkerIndices = new HashSet<int>();
        private int lastResourceMarkerCount = 0;
        private bool isResourceFarmTeleport = false;
        // Auto Resource Farm auto-stop fields
        private bool autoResourceFarmAutoStopEnabled = false;
        private int autoResourceFarmAutoStopHours = 0;
        private int autoResourceFarmAutoStopMinutes = 0;
        private int autoResourceFarmAutoStopSeconds = 0;
        private string autoResourceFarmAutoStopHoursInput = "0";
        private string autoResourceFarmAutoStopMinutesInput = "0";
        private string autoResourceFarmAutoStopSecondsInput = "0";
        private float autoResourceFarmAutoStopAt = -1f;
        private bool resourceJustArrived = false;
        private float resourceArrivalTime = 0f;
        private float lastResourceTeleportTime = 0f;
        private Vector3 resourceStartPosition = Vector3.zero;
        private bool hasResourceStartPosition = false;
        private bool isResourceReturningToStart = false;
        private float resourceTeleportCooldown = 3f;
        private float resourceClickDuration = 1.0f;
        private float resourceArrivalDelay = 0.5f;
        private int fKeySimFrame = 0;
        private int resourceClickCount = 0;
        // When enabling resource farm, wait for axe equip if needed
        private bool resourceWaitingForEquip = false;
        private float resourceWaitingForEquipUntil = 0f;
        private int resourceEquipAttempts = 0;
        private const int resourceEquipMaxAttempts = 3;
        public static bool SimulateFKeyHeld = false;
        public static bool SimulateFKeyDown = false;
        public static bool SimulateFKeyUp = false;
        
        // Noclip/Flying Variables
        private bool noclipEnabled = false;
        private float noclipSpeed = 10f;
        private float noclipBoostMultiplier = 2f;
        // Persisted slider backups for cross-instance load
        private float saved_autoFishScanTimeout = -1f;
        private float saved_autoFishTeleportDelay = -1f;
        private float saved_autoFishFishShadowDetectRange = -1f;
        private float saved_autoFishReelMaxDuration = -1f;
        private float saved_autoFishReelHoldDuration = -1f;
        private float saved_autoFishReelPauseDuration = -1f;
        private GameObject cachedToastTextObj = null;
        private string lastDetectedToast = "";
        private float lastToastCheckAt = 0f;
        private const float TOAST_CHECK_INTERVAL = 0.25f;

        // Bypass overlap building state
        private bool bypassOverlapEnabled = false;
        private bool bypassOverlapPatched = false;
        private HarmonyLib.Harmony bypassHarmony = null;
        private static bool bypassOverlapEnabledStatic = false;

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

        // Token: 0x04000022 RID: 34
        private GameObject cacheStatusAnim;

        // Token: 0x04000023 RID: 35
        private GameObject cacheCookUI;

        // Token: 0x04000024 RID: 36
        private GameObject cacheSkeletonBody;
        private string lastLoggedInteractSpriteName = string.Empty;
        private float nextInteractSpriteDebugAt = 0f;

        // Token: 0x04000025 RID: 37
        private bool collectMushrooms = true;

        // Token: 0x04000026 RID: 38
        private bool collectBerries = true;

        private bool collectEventResources = true;

        // Token: 0x04000027 RID: 39
        private bool collectOther = false;

        // Token: 0x04000028 RID: 40
        private GameObject radarContainer;

        // Token: 0x04000029 RID: 41
        public bool isRadarActive = false;

        // Token: 0x0400002A RID: 42
        private bool showMushroomRadar = false;
        private bool showFiddleheadRadar = false;
        private bool showTallMustardRadar = false;
        private bool showBurdockRadar = false;
        private bool showMustardGreensRadar = false;

        // Token: 0x0400002B RID: 43
        private bool showBlueberryRadar = false;

        // Token: 0x0400002C RID: 44
        private bool showRaspberryRadar = false;

        // Token: 0x0400002D RID: 45
        private bool showStoneRadar = false;

        // Token: 0x0400002E RID: 46
        private bool showOreRadar = false;

        // Token: 0x0400002E RID: 46
        private bool showBubbleRadar = false;

        // Token: 0x0400002E RID: 46
        public bool showInsectRadar = false;

        // Token: 0x0400002F RID: 47
        public bool showFishShadowRadar = false;
        // Draw radar objects as a GUI overlay (like meteors) regardless of world distance
        private bool showRadarGuiOverlay = false;
        
        // Token: 0x04000030 RID: 48
        private bool showTreeRadar = false;
        private bool showRareTreeRadar = false;
        private bool showAppleTreeRadar = false;
        private bool showOrangeTreeRadar = false;

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
        private HashSet<string> loggedUnknownForageMeshNames = new HashSet<string>();

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
        private System.Action blueberryCollectListener = null;

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
        private System.Action raspberryCollectListener = null;

        // Token: 0x04000043 RID: 67
        private bool autoFarmActive = false;

        // Token: 0x04000044 RID: 68
        private string autoFarmStatus = "Idle";

        // Token: 0x04000045 RID: 69
        private float autoFarmTimer = 0f;

        private bool autoFarmAutoStopEnabled = false;
        private int autoFarmAutoStopHours = 0;
        private int autoFarmAutoStopMinutes = 0;
        private int autoFarmAutoStopSeconds = 0;
        private string autoFarmAutoStopHoursInput = "0";
        private string autoFarmAutoStopMinutesInput = "0";
        private string autoFarmAutoStopSecondsInput = "0";
        private float autoFarmAutoStopAt = -1f;

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
        private bool priorityFiddlehead = false;
        private bool priorityTallMustard = false;
        private bool priorityBurdock = false;
        private bool priorityMustardGreens = false;
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

        // --- STATIC LOOT LOCATIONS FOR PRIORITIES ---
        private Dictionary<string, Vector3> priorityLocations = new Dictionary<string, Vector3>()
        {
            { "Oyster Mushroom", new Vector3(36.603138f, 26.140745f, 212.39085f) },
            { "Button Mushroom", new Vector3(-219.81989f, 12.863783f, 6.995692f) },
            { "Penny Bun", new Vector3(175.89377f, 25.673292f, 55.985367f) },
            { "Shiitake", new Vector3(-66.63026f, 14.248707f, -169.89787f) },
            { "Black Truffle", new Vector3(258.11917f, 13.1247f, 95.18241f) },
            { "Fiddlehead", new Vector3(229.782f, 11.404f, 48.837f) },
            { "Tall Mustard", new Vector3(-125.213f, 11.729f, 290.797f) },
            { "Burdock", new Vector3(-211.599f, 29.916f, 35.416f) },
            { "Mustard Greens", new Vector3(-58.984f, 11.035f, -155.413f) },
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
                UnifiedConfigData config = this.LoadOrCreateUnifiedConfig();
                this.PopulateAllConfigSections(config);
                this.SaveUnifiedConfig(config);
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
                UnifiedConfigData config = this.LoadUnifiedConfig();
                if (config != null)
                {
                    this.customTeleportList.Clear();
                    foreach (CustomTeleportEntry entry in config.CustomTeleports)
                    {
                        if (entry != null) this.customTeleportList.Add(entry);
                    }
                    MelonLogger.Msg($"Loaded {this.customTeleportList.Count} custom teleports.");
                    return;
                }
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

        private bool CheckForEnergyNotification()
        {
            try
            {
                GameObject toastText = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Tip/TipPanel(Clone)/ToastPanel(Clone)/toasts@t/ToastTextWidget(Clone)/AniRoot@ani/root_visible@go/root_visible/value@txt");
                if (toastText != null && toastText.activeInHierarchy)
                {
                    var text = toastText.GetComponent<Text>();
                    if (text != null && !string.IsNullOrEmpty(text.text))
                    {
                        string s = text.text.Trim();
                        // Match several possible toast variants that request eating.
                        if (s.Contains("Energy low") && s.Contains("Eat something"))
                        {
                            return true;
                        }
                        // Also accept short variant "Energy low. Eat something!"
                        if (s.StartsWith("Energy low") && s.IndexOf("Eat") >= 0)
                        {
                            return true;
                        }
                    }

                }
            }
            catch
            {
            }
            return false;
        }

        // Called by Harmony postfix when UIManager.ShowToast is invoked in-game.
        public void OnToastDetected(string msg)
        {
            try
            {
                if (string.IsNullOrEmpty(msg)) return;
                string s = msg.Trim();
                // Avoid duplicate handling when both the UIManager hook and panel scanner fire
                if (s == this.lastDetectedToast) return;
                this.lastDetectedToast = s;

                // Durability notification (exact text observed in game)
                if (s.IndexOf("Tool durability depleted", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!this.isRepairing && !this.isAutoEating)
                    {
                        MelonLogger.Msg("[AutoRepair] Durability toast requested StartRepair (hook)");
                        this.lastStartWasAutoRepair = true;
                        this.StartRepair();
                        this.resourceRepairPauseUntil = Time.time + this.resourceAutoRepairPauseSeconds;
                        this.AddMenuNotification($"Auto Repair triggered by durability notification — pausing farm for {this.resourceAutoRepairPauseSeconds:F0}s", new Color(0.45f, 1f, 0.55f));
                    }
                    return;
                }

                // Energy / Eat prompts
                if (s.IndexOf("Energy low", StringComparison.OrdinalIgnoreCase) >= 0 && s.IndexOf("Eat", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!this.isRepairing && !this.isAutoEating)
                    {
                        MelonLogger.Msg("[AutoEat] Energy toast requested StartAutoEat (hook)");
                        this.StartAutoEat();
                        // Pause farm teleports while auto-eat runs (reuse same pause setting)
                        this.resourceRepairPauseUntil = Time.time + this.resourceAutoRepairPauseSeconds;
                        this.AddMenuNotification($"Auto Eat triggered by energy low toast ({this.autoEatFoodOptions[this.autoEatFoodType]}) — pausing farm for {this.resourceAutoRepairPauseSeconds:F0}s", new Color(0.45f, 1f, 0.55f));
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("[OnToastDetected] Error: " + ex.Message);
            }
        }

        private void CheckToastPanel()
        {
            if (Time.unscaledTime - this.lastToastCheckAt < TOAST_CHECK_INTERVAL) return;
            this.lastToastCheckAt = Time.unscaledTime;
            try
            {
                GameObject toastsRoot = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Tip/TipPanel(Clone)/ToastPanel(Clone)/toasts@t");
                if (toastsRoot == null) { this.cachedToastTextObj = null; return; }
                int childCount = toastsRoot.transform.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    Transform child = toastsRoot.transform.GetChild(i);
                    if (child == null || !child.gameObject.activeInHierarchy) continue;
                    Transform txtTransform = child.Find("AniRoot@ani/root_visible@go/root_visible/value@txt");
                    if (txtTransform == null) continue;
                    GameObject txtObj = txtTransform.gameObject;
                    if (txtObj == null || !txtObj.activeInHierarchy) continue;

                    string text = null;
                    var uiText = txtObj.GetComponent<UnityEngine.UI.Text>();
                    if (uiText != null) text = uiText.text;
                    else
                    {
                        foreach (Component comp in txtObj.GetComponents<Component>())
                        {
                            if (comp == null) continue;
                            try
                            {
                                var ilType = comp.GetIl2CppType();
                                if (ilType != null && ilType.Name == "XDText")
                                {
                                    var prop = ilType.GetProperty("text");
                                    if (prop != null)
                                    {
                                        var val = prop.GetValue(comp);
                                        text = (val != null) ? val.ToString() : null;
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    if (!string.IsNullOrEmpty(text))
                    {
                        string s = text.Trim();
                        if (s != this.lastDetectedToast)
                        {
                            this.lastDetectedToast = s;
                            this.OnToastDetected(s);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("[CheckToastPanel] error: " + ex.Message);
            }
        }

        private float GetCurrentEnergy()
        {
            try
            {
                GameObject energyText = GameObject.Find("GameApp/startup_root(Clone)/XDUIRoot/Status/StatusPanel(Clone)/AniRoot@ani@queueanimation/top_left_layout@go/energy_bar@go@w/root/energy_progress@go/energy_more@slider/energy_progress@txt");
                if (energyText != null && energyText.activeInHierarchy)
                {
                    var text = energyText.GetComponent<Text>();
                    if (text != null && !string.IsNullOrEmpty(text.text))
                    {
                        // Parse energy value (format like "45/100")
                        string energyStr = text.text.Trim();
                        if (energyStr.Contains("/"))
                        {
                            string[] parts = energyStr.Split('/');
                            if (parts.Length >= 2 && float.TryParse(parts[0], out float current) && float.TryParse(parts[1], out float max))
                            {
                                return current / max; // Return as percentage (0.0 to 1.0)
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return 1.0f; // Default to full if can't read
        }

        private bool IsEnergyLow()
        {
            float energyPercent = GetCurrentEnergy();
            return energyPercent < 0.95f; // Consider low if below 95%
        }

        private bool IsEnergyFull()
        {
            float energyPercent = GetCurrentEnergy();
            return energyPercent >= 1.0f; // Consider full at 100%
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
