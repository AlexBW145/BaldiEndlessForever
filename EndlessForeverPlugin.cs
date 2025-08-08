using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using EndlessFloorsForever.Components;
using HarmonyLib;
using MidiPlayerTK;
using MonoMod.Utils;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.Components;
using MTM101BaldAPI.ObjectCreation;
using MTM101BaldAPI.Reflection;
using MTM101BaldAPI.Registers;
using MTM101BaldAPI.SaveSystem;
using MTM101BaldAPI.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting.Messaging;
using System.Text;
using TMPro;
using UnityCipher;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Events;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.TextCore;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace EndlessFloorsForever;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInDependency("mtm101.rulerp.bbplus.baldidevapi", "8.1.0.0")]
[BepInIncompatibility("mtm101.rulerp.baldiplus.endlessfloors")]
[BepInDependency("alexbw145.baldiplus.pinedebug", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("pixelguy.pixelmodding.baldiplus.custommainmenusapi", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("pixelguy.pixelmodding.baldiplus.custommusics", BepInDependency.DependencyFlags.SoftDependency)]
[BepInProcess("BALDI.exe")]
public class EndlessForeverPlugin : BaseUnityPlugin
{
    internal const string PLUGIN_GUID = "alexbw145.baldiplus.arcadeendlessforever";
    private const string PLUGIN_NAME = "Arcade Endless Forever";
    private const string PLUGIN_VERSION = "0.0.6.0";

    public static EndlessForeverPlugin Instance { get; private set; }
    public static readonly AssetManager arcadeAssets = new AssetManager();
    public readonly Dictionary<string, Sprite> UpgradeIcons = new Dictionary<string, Sprite>(); // Todo: make this associate with the asset manager??
    internal static ManualLogSource Log = new ManualLogSource("Arcade Endless Forever Logger");
    internal ArcadeEndlessForeverSave gameSave = new ArcadeEndlessForeverSave();
    public Dictionary<string, byte> Counters => gameSave.Counters;
    public bool HasUpgrade(string type) => gameSave.HasUpgrade(type);
    public int GetUpgradeCount(string type) => gameSave.GetUpgradeCount(type);
    public bool InGameMode => gameSave.IsInfGamemode;

    internal ConfigEntry<bool> forceSets;

    internal Tuple<int, bool, bool> currentData { get; private set; } = new Tuple<int, bool, bool>(1, false, false);
    internal Tuple<bool, bool, bool> firstEncounters = new Tuple<bool, bool, bool>(false, false, false);
    internal SceneObject inflevel { get; private set; }
    internal SceneObject InfPitstop { get; private set; }

    internal static readonly Dictionary<BepInEx.PluginInfo, Action<GeneratorData>> genActions = new Dictionary<BepInEx.PluginInfo, Action<GeneratorData>>();
    internal static readonly Dictionary<ArcadeParameterOrder, Dictionary<BepInEx.PluginInfo, Action<CustomLevelGenerationParameters, System.Random>>> managerActions = new Dictionary<ArcadeParameterOrder, Dictionary<BepInEx.PluginInfo, Action<CustomLevelGenerationParameters, System.Random>>>()
    {
        { ArcadeParameterOrder.Structures, new Dictionary<PluginInfo, Action<CustomLevelGenerationParameters, System.Random>>() },
        { ArcadeParameterOrder.Rooms, new Dictionary<PluginInfo, Action<CustomLevelGenerationParameters, System.Random>>() },
        { ArcadeParameterOrder.Post, new Dictionary<PluginInfo,Action<CustomLevelGenerationParameters, System.Random>>() }
    };
    internal static readonly HashSet<string> basegameRooms = new HashSet<string>();
    public static FloorData currentFloorData => Instance.gameSave.myFloorData;

    internal List<WeightedFieldTrip> fieldTrips { get; private set; } = new List<WeightedFieldTrip>();
    public static readonly Dictionary<string, StandardUpgrade> Upgrades = new Dictionary<string, StandardUpgrade>();

    public static readonly List<WeightedTexture2D> wallTextures = new List<WeightedTexture2D>();
    public static readonly List<WeightedTexture2D> facultyWallTextures = new List<WeightedTexture2D>();
    public static readonly List<WeightedTexture2D> ceilTextures = new List<WeightedTexture2D>();
    public static readonly List<WeightedTexture2D> floorTextures = new List<WeightedTexture2D>();
    public static readonly List<WeightedTexture2D> profFloorTextures = new List<WeightedTexture2D>();
    public static readonly List<WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>> setTextures = new List<WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>>();
    public static readonly List<WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>> setFacultyTextures = new List<WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>>();

    public static void AddGeneratorAction(BepInEx.PluginInfo info, Action<GeneratorData> data)
    {
        if (genActions.ContainsKey(info))
            throw new Exception("Can't add already existing generator action!");
        genActions.Add(info, data);
    }

    public static void AddParameterAction(BepInEx.PluginInfo info, ArcadeParameterOrder order, Action<CustomLevelGenerationParameters, System.Random> data)
    {
        if (managerActions[order].ContainsKey(info))
            throw new Exception("Can't add already existing generator action!");
        managerActions[order].Add(info, data);
    }

    public void AddFieldTrip(WeightedFieldTrip fieldtrip) => fieldTrips.Add(fieldtrip);
    public static void RegisterUpgrade(StandardUpgrade upgrade) => Upgrades.Add(upgrade.id, upgrade);

    private void Awake()
    {
        Instance = this;
        new Harmony(PLUGIN_GUID).PatchAllConditionals();
        Log = Logger;
        forceSets = Config.Bind("Cosmetic Settings", "Force environment textures as sets", false, "Setting this \"true\" will disable randomly picked environment textures and will use environment texture sets instead.");

        LoadingEvents.RegisterOnAssetsLoaded(Info, StartLoad(), LoadingEventOrder.Start);
        LoadingEvents.RegisterOnAssetsLoaded(Info, PreLoad(), LoadingEventOrder.Pre);
        LoadingEvents.RegisterOnAssetsLoaded(Info, PostLoad(), LoadingEventOrder.Final);

        ModdedSaveGame.AddSaveHandler(gameSave);
#if RELEASE
        MTM101BaldiDevAPI.AddWarningScreen(@"This build of Infinite Floors Forever is unfinished,
meaning that everything is subject to change!
Current pre-release version: " + PLUGIN_VERSION, false);
#endif
    }

    IEnumerator PostLoad()
    {
        yield return 2 + (Chainloader.PluginInfos.ContainsKey("alexbw145.baldiplus.pinedebug") ? 1 : 0);
        yield return "Setting tags n' stuff...";
        ItemMetaStorage items = MTM101BaldiDevAPI.itemMetadata;
        ITM_Present.potentialObjects.AddRange(new WeightedItemObject[]
        {
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.Quarter).value,
                    weight = 60
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.AlarmClock).value,
                    weight = 55
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.Apple).value,
                    weight = 10
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.Boots).value,
                    weight = 55
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.ChalkEraser).value,
                    weight = 80
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.DetentionKey).value,
                    weight = 40
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.GrapplingHook).value,
                    weight = 25
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.Nametag).value,
                    weight = 45
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.Wd40).value,
                    weight = 60
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.PortalPoster).value,
                    weight = 20
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.PrincipalWhistle).value,
                    weight = 50
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.Scissors).value,
                    weight = 80
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.DoorLock).value,
                    weight = 42
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.Tape).value,
                    weight = 40
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.Teleporter).value,
                    weight = 25
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.ZestyBar).value,
                    weight = 70
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.NanaPeel).value,
                    weight = 66
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.ReachExtender).value,
                    weight = 70
                },
                new WeightedItemObject()
                {
                    selection = items.FindByEnum(Items.InvisibilityElixir).value,
                    weight = 40
                }
        });
        NPCMetaStorage.Instance.Get(Character.Crafters).tags.Add("infarcade_favoritisminvulnerable");
        NPCMetaStorage.Instance.FindAll(x => x.value.GetType().Equals(typeof(Principal)) || x.value.GetType().IsSubclassOf(typeof(Principal))).Do(npc => npc.tags.Add("infarcade_favoritisminvulnerable"));
        yield return "Getting compats...";
        if (Chainloader.PluginInfos.ContainsKey("alexbw145.baldiplus.pinedebug"))
            yield return PineDebugSupport.PineDebugStuff();
    }

    IEnumerator StartLoad()
    {
        yield return 2;
        yield return "Grabbing old data..."; // Make it fair because I'VE ENCRYPTED THE SAVE AND ANYONE CAN CHEAT THE SYSTEM!!
        foreach (var player in FindObjectOfType<NameManager>(false).nameList)
        {
            string path = Path.Combine(Application.persistentDataPath, "Modded", player, "mtm101.rulerp.baldiplus.endlessfloors", "high.txt");
            if (File.Exists(path))
            {
                Log.LogInfo("Endless Floors save found for " + player);
                Log.LogDebug(path);
                string otherpath = Path.Combine(ModdedSaveSystem.GetSaveFolder(this, player), "arcadedata.dat");
                if (File.Exists(otherpath))
                {
                    try
                    {
                        var data = JsonUtility.FromJson<ENDFloorsData>(RijndaelEncryption.Decrypt(File.ReadAllText(otherpath), "ArcadeEndlessForever_" + player));
                        int oldtop = Convert.ToInt32(File.ReadAllText(path));
                        if (data.topFloors < oldtop && !data.DoneEndlessFloorsMigration)
                            data.topFloors = oldtop;
                        data.DoneEndlessFloorsMigration = true;
                        File.WriteAllText(otherpath, RijndaelEncryption.Encrypt(JsonUtility.ToJson(data), "ArcadeEndlessForever_" + player));

                    }
                    catch (Exception e) {
                        Log.LogError("Failed to migrate Infinite Floors data to Arcade Endless Forever data for " + player);
                        Log.LogError(e);
                    }
                }
                else
                {
                    var data = new ENDFloorsData();
                    data.topFloors = Convert.ToInt32(File.ReadAllText(path));
                    data.DoneEndlessFloorsMigration = true;
                    Directory.CreateDirectory(Directory.GetParent(otherpath).ToString());
                    File.WriteAllText(otherpath, RijndaelEncryption.Encrypt(JsonUtility.ToJson(data), "ArcadeEndlessForever_" + player));
                }
            }
        }

        yield return "Instantiating LevelObject and SceneObject";
        InfPitstop = Instantiate(SceneObjectMetaStorage.Instance.Find(scene => scene.title == "PIT").value);
        InfPitstop.name = "Pitstop_INF";
        InfPitstop.levelAsset = Instantiate(InfPitstop.levelAsset);
        InfPitstop.levelAsset.name = "Pitstop_INF";
        var endscene = Instantiate(Resources.FindObjectsOfTypeAll<SceneObject>().ToList().Find(x => x.name == "MainLevel_3"));
        var endlevel = Instantiate(endscene.levelObject); // For reference, this was cloned because I don't want the level objects to contain dupes of stuff when queueing this scene object to the management.
        endlevel.name = "InfLevel_Schoolhouse";
        var lvl5 = Resources.FindObjectsOfTypeAll<SceneObject>().ToList().Find(x => x.name == "MainLevel_5");
        endscene.randomizedLevelObject = [new() { selection = endlevel, weight = 250 }];
        endscene.levelObject = null;
        foreach (var room in endlevel.roomGroup)
            basegameRooms.Add(room.name);
        foreach (var level in lvl5.randomizedLevelObject)
        {
            var endtype = Instantiate(level.selection);
            endtype.name = $"InfLevel_{endtype.type.ToStringExtended()}";
            endscene.randomizedLevelObject = endscene.randomizedLevelObject.AddToArray(new() { selection = endtype, weight = 50 });
            foreach (var room in endtype.roomGroup)
                basegameRooms.Add(room.name);
        }
        endscene.name = "InfScene";
        endscene.nextLevel = endscene;
        endscene.nameKey = "Level_Arcade";
        endscene.levelTitle = "INF";
        endscene.levelNo = 99;
        endscene.previousLevels = [];
        endscene.forcedNpcs = [];
        endscene.potentialNPCs = [];
        endscene.shopItems = [];
        endscene.AddMeta(this, ["arcade"]);
        GeneratorManagement.EnqueueGeneratorChanges(endscene);
        InfGameManager manager = new MainGameManagerBuilder<InfGameManager>()
            .SetCustomPitstop(InfPitstop)
            .SetAllNotebooksSound(Resources.FindObjectsOfTypeAll<SoundObject>().Last(snd => snd.name == "BAL_AllNotebooks_9"))
            .SetObjectName("InfManager")
            .SetNameKey("Mode_Arcade")
            .SetLevelNumber(0)
            .Build(); // Don't tell PixelGuy about the inspiration.
        /*FieldInfo[] fields = typeof(MainGameManager).GetFields(BindingFlags.Default | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (FieldInfo fieldInfo in fields)
            fieldInfo.SetValue(manager, fieldInfo.GetValue(manager.gameObject.GetComponent<MainGameManager>()));*/
        endscene.manager = manager;
        manager.ReflectionSetVariable("destroyOnLoad", true);

        /*RoomGroup[] oldrg = endlevel.roomGroup.Clone() as RoomGroup[];
        RoomGroup[] lvl3rg = Resources.FindObjectsOfTypeAll<SceneObject>().ToList().Find(x => x.name == "MainLevel_3").levelObject.roomGroup;
        FieldInfo[] fields = typeof(RoomGroup).GetFields();
        endlevel.roomGroup = [];
        RoomGroup[] newrg = new RoomGroup[lvl3rg.Length];
        for (int i = 0; i < newrg.Count(); i++)
        {
            newrg[i] = new RoomGroup();
            fields = typeof(RoomGroup).GetFields();
            foreach (FieldInfo fieldInfo in fields)
                fieldInfo.SetValue(newrg[i], fieldInfo.GetValue(lvl3rg[i]));
        }

        endlevel.roomGroup = endlevel.roomGroup.AddRangeToArray([.. newrg]);*/

        inflevel = endscene;
        endscene.MarkAsNeverUnload();
        //endMode = EnumExtensions.ExtendEnum<Mode>("Arcade");

        /*var roomgrp = endlevel.roomGroup.ToList();
        roomgrp.Insert(0, Resources.FindObjectsOfTypeAll<LevelObject>().First(x => x.name == "Endless1").roomGroup.First(x => x.name == "Store"));
        endlevel.roomGroup = roomgrp.ToArray();
        fieldTrips.Add(new WeightedFieldTrip()
        {
            weight = 150,
            selection = Resources.FindObjectsOfTypeAll<FieldTripObject>().Where(x => x.trip == FieldTrips.Camp).First()
        });*/
    }

    private void AddWeightedTextures(List<WeightedTexture2D> tex, string folder, string path)
    {
        string wallsPath = Path.Combine(path, "Textures", folder);
        foreach (string p in Directory.GetFiles(wallsPath))
        {
            string standardName = Path.GetFileNameWithoutExtension(p);
            Texture2D texx = AssetLoader.TextureFromFile(p);
            string[] splitee = standardName.Split('!');
            tex.Add(new WeightedTexture2D()
            {
                selection = texx,
                weight = int.Parse(splitee[1])
            });
        }
    }
    private void AddWeightedTextures(List<WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>> set, string folder)
    {
        string wallsPath = Path.Combine(AssetLoader.GetModPath(this), "Textures", folder);
        foreach (string p in Directory.GetDirectories(wallsPath))
        {
            Texture2D wall = AssetLoader.TextureFromFile(Path.Combine(p, "Wall.png"));
            Texture2D floor = AssetLoader.TextureFromFile(Path.Combine(p, "Floor.png"));
            Texture2D ceilin = AssetLoader.TextureFromFile(Path.Combine(p, "Ceil.png"));
            if (wall == null || floor == null || ceilin == null)
                continue;
            string standardName = Path.GetFileName(p);
            wall.name = standardName + "_wall";
            floor.name = standardName + "_floor";
            ceilin.name = standardName + "_ceil";
            string[] splitee = standardName.Split('!');
            set.Add(new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(wall, ceilin, floor),
                weight = int.Parse(splitee[1])
            });
        }
    }

    IEnumerator PreLoad()
    {
        yield return 5;
        yield return "Creating important assets...";
        if (Chainloader.PluginInfos.ContainsKey("pixelguy.pixelmodding.baldiplus.custommainmenusapi"))
            CustomMainMenuSupport.InitSupport();
        AssetLoader.LocalizationFromMod(this);
        AssetLoader.LocalizationFromFunction((lang) =>
        {
            return new Dictionary<string, string>()
            {
                { "Level_Arcade", "Level INF" },
                { "But_NewGameINFPlus", "Start New Game+" },
                { "Men_NewGameINFPlusDesc", "A fresh restart for a new endless run!\nStart back into the floor you began in your previous run\nwith your upgrades and inboxbeing carried straight to your new beginning\nbut at the cost of YTPs and Items staying in your previous game!\n\n<b><color=red>THIS WILL ERASE YOUR PREVIOUSLY SAVED GAME.</color></b>" },
                { "Mode_Arcade", "Infinite Floors" },
                { "TAG_Boosted", "WANTED!" },
                { "men_ArcadeForever", "Infinite Floors Forever" },
                { "Vfx_Juan_Aid", "I'll take care of that!" },
                { "Vfx_Juan_Welcome", "Oh hello there, my name is Juan!" },
                { "Vfx_Juan_Welcome2", "And welcome to my upgrade warehouse." },
                { "Vfx_Juan_Intro", "First time dialogue" },
                { "Vfx_Juan_Buy1", "Thanks for the purchase!" },
                { "Vfx_Juan_Buy2", "Thank you for the purchase, as I can now finally pay my rent." },
                { "Vfx_Juan_Denied1", "Awh I'm sorry but you cannot afford that." },
                { "Vfx_Juan_Denied2", "No window shopping!" },
                { "Vfx_Juan_Denied3", "You can't afford that yet!" },
                { "Vfx_Juan_ExitSuccess1", "See ya' in the next floor!" },
                { "Vfx_Juan_ExitSuccess2", "Later!" },
                { "Vfx_Juan_Exit1", "Urrhh, I think I've already paid my rent by now..." },
                { "Vfx_Juan_Exit2", "Oh I guess maybe next time?" },
                { "Vfx_Juan_BusPass_1", "Oh a bus pass, for me??" },
                { "Vfx_Juan_BusPass_2", "Thank you so much. I'm gonna have a blast with this one!" },
                { "Vfx_Juan_FieldTrip", "Welcome! Since you've already came back from my field trip, I've brought back some items for you." },
                { "Vfx_Juan_FillMap", "Your map is filled and ready!" },
            };
        });
        FloorPick.sliding = Resources.FindObjectsOfTypeAll<SoundObject>().Last(x => x.name == "Slap");
        FloorPick.slideDone = Resources.FindObjectsOfTypeAll<SoundObject>().Last(x => x.name == "TapeInsert");
        arcadeAssets.Add("F99Finale", AssetLoader.MidiFromMod("99start", this, "Midi", "TimeOut_F99.mid"));
        arcadeAssets.AddRange([AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromMod(this, "Tubes4.png"), 1f), AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromMod(this, "Tubes5.png"), 1f), AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromMod(this, "Tubes6.png"), 1f)], ["LifeTubes4", "LifeTubes5", "LifeTubes6"]);
        arcadeAssets.Add("TimeSlow", ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "Effects", "TimeSlow.wav"), "Sfx_TimeSlow", SoundType.Effect, Color.blue));
        arcadeAssets.Add("TimeFast", ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "Effects", "TimeFastFast.wav"), "Sfx_TimeFast", SoundType.Effect, Color.blue));
        var green = (Color)Resources.FindObjectsOfTypeAll<Baldi>().Last().AudMan.ReflectionGetVariable("subtitleColor");
        arcadeAssets.Add("AllNotebooksRegular", ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "BAL_AllNotebooks_INF.wav"), "Vfx_BAL_Tutorial_AllNotebooks_0", SoundType.Voice, green));
        var allRegular = arcadeAssets.Get<SoundObject>("AllNotebooksRegular");
        allRegular.additionalKeys = [
            new SubtitleTimedKey()
            {
                time = 5.197f,
                key = "Vfx_BAL_AllNotebooks_3"
            },
            new SubtitleTimedKey()
            {
                time = 8.407f,
                key = "Vfx_BAL_AllNotebooks_4"
            },
            new SubtitleTimedKey()
            {
                time = 15.121f,
                key = "Vfx_BAL_AllNotebooks_5"
            },
            ];
        arcadeAssets.Add("AllNotebooksF99", ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "BAL_AllNotebooks_INFF99.wav"), "Vfx_BAL_Tutorial_AllNotebooks_0", SoundType.Voice, green));
        var allF99 = arcadeAssets.Get<SoundObject>("AllNotebooksF99");
        allF99.additionalKeys = [
            new SubtitleTimedKey()
            {
                time = 5.198f,
                key = "Vfx_BAL_TimeOut_2"
            }
            ];
        AccessTools.Field(typeof(MainGameManager), "allNotebooksNotification").SetValue(inflevel.manager, allRegular);
        ((InfGameManager)inflevel.manager).F99AllNotebooks = allF99;
        string iconPath = Path.Combine(AssetLoader.GetModPath(this), "UpgradeIcons");
        foreach (string p in Directory.GetFiles(iconPath))
        {
            Texture2D tex = AssetLoader.TextureFromFile(p);
            Sprite spr = AssetLoader.SpriteFromTexture2D(tex, Vector2.one / 2f, 25f);
            UpgradeIcons.Add(Path.GetFileNameWithoutExtension(p), spr);
        }
        arcadeAssets.AddRange<Texture2D>([
            AssetLoader.TextureFromMod(this, "UpgradeSlot5.png"),
            ],
            [
                "UpgradeSlot5",
                ]);
        arcadeAssets.AddRange<Sprite>([
            AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromMod(this, "MissingSlot.png"), 50f)
            ],
            [
                "OutOfOrderSlot"
                ]);

        var placeholdcardboard = AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 28f, "PlaceholdShopkeeper.png");
        var placeholdPoster = new PosterObject()
        {
            baseTexture = AssetLoader.TextureFromMod(this, "unfinishednotice.png"),
        };
        arcadeAssets.Add("PlaceholdShopkeeper", placeholdcardboard);
        // Upgrade Warehouse Storekeeper (The Store where when you purchase upgrades, it'll go into your inbox from the Pitstop.
        arcadeAssets.AddRange<Texture2D>([
            AssetLoader.TextureFromMod(this, "UpgradeWares", "WarehouseWall.png"),
            AssetLoader.TextureFromMod(this, "UpgradeWares", "WarehouseCeil.png"),
            AssetLoader.TextureFromMod(this, "UpgradeWares", "WarehouseFloor.png")
            ],
            [
                "Store/WarehouseWall",
                "Store/WarehouseCeil",
                "Store/WarehouseFloor"
                ]);
        var upgradeObject = new ItemBuilder(Info)
            .SetNameAndDescription("UPGRADE, ERROR", "ERROR ERROR ERROR! SHOULD NOT BE SEEING THIS!")
            .SetSprites(arcadeAssets.Get<Sprite>("OutOfOrderSlot"), arcadeAssets.Get<Sprite>("OutOfOrderSlot"))
            .SetAsInstantUse()
            .SetItemComponent<Item>()
            .SetGeneratorCost(50)
            .SetShopPrice(999)
            .SetEnum("EndlessUpgrade")
            .Build();
        arcadeAssets.Add<ItemObject>("Store/UpgradeObject", upgradeObject);
        var storeRoomFunction = Resources.FindObjectsOfTypeAll<StoreRoomFunction>().Last(x => !x.name.ToLower().Contains("tutorial"));
        GameObject shopcontainer = new GameObject("UpgradeWarehouseRoom", typeof(RoomFunctionContainer), typeof(UpgradeWarehouseRoomFunction), typeof(DoorAssignerRoomFunction));
        shopcontainer.ConvertToPrefab(true);
        {
            //shopcontainer.layer = LayerMask.NameToLayer("Ignore Raycast");
            shopcontainer.GetComponent<RoomFunctionContainer>().ReflectionSetVariable("functions", new List<RoomFunction>()); // Had to manually fix it.
            shopcontainer.GetComponent<RoomFunctionContainer>().AddFunction(shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>());
            shopcontainer.GetComponent<RoomFunctionContainer>().AddFunction(shopcontainer.GetComponent<DoorAssignerRoomFunction>());
        }
        shopcontainer.GetComponent<DoorAssignerRoomFunction>().ReflectionSetVariable("doorPre", Resources.FindObjectsOfTypeAll<SwingDoor>().Last(d => d.name == "Door_Auto"));
        shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>().audBell = Resources.FindObjectsOfTypeAll<SoundObject>().Last(s => s.name == "CashBell");
        var roombase = new GameObject("RoomBase");
        roombase.transform.SetParent(shopcontainer.transform, false);
        shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>().roomBase = roombase.transform;

        var alarm = Instantiate(Resources.FindObjectsOfTypeAll<PropagatedAudioManager>().Last(a => a.name == "AlarmSource"), roombase.transform, true);
        shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>().alarmAudioManager = alarm;
        shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>().audAlarm = Resources.FindObjectsOfTypeAll<SoundObject>().Last(s => s.name == "Elv_Buzz");

        var storeroombase = storeRoomFunction.ReflectionGetVariable("roomBase") as Transform;
        var warekeeper = Instantiate(storeroombase.Find("JohnnyBase"), roombase.transform, true);
        DestroyImmediate(warekeeper.GetComponentInChildren<Animator>());
        DestroyImmediate(warekeeper.GetComponent<PropagatedAudioManagerAnimator>());
        var volumeanim = warekeeper.gameObject.AddComponent<CustomVolumeAnimator>();
        shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>().animator = warekeeper.GetChild(0).gameObject.AddComponent<CustomSpriteAnimator>();
        var spriteanimator = shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>().animator;
        volumeanim.animator = spriteanimator;
        volumeanim.audioSource = spriteanimator.GetComponent<AudioSource>();
        var propagatedWare = warekeeper.gameObject.AddComponent<PropagatedAudioManager>();
        propagatedWare.audioDevice = volumeanim.audioSource;
        propagatedWare.ReflectionSetVariable("maxDistance", 200f); // I forever am using this to waste ram.
        shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>().storekeeperAudMan = propagatedWare;
        spriteanimator.spriteRenderer = spriteanimator.GetComponent<SpriteRenderer>();
        volumeanim.GetComponent<SpriteRenderer>().sprite = AssetLoader.SpriteFromMod(this, Vector2.one/2f, 28f, "UpgradeWares", "Juan_Base.png");
        var hotspot = Instantiate(storeroombase.Find("JohnnyHotspot"), roombase.transform, true);
        shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>().johnnyHotspot = hotspot;
        warekeeper.transform.localPosition += (Vector3.down * 0.2f) + (Vector3.forward * 1.5f);
        warekeeper.transform.localRotation = Quaternion.Euler(0f, 0f, 358f);
        shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>().mouthAnimator = volumeanim;
        UpgradeWarehouseRoomFunction.anims.AddRange(new Dictionary<string, CustomAnimation<Sprite>>()
        {
            { "Idle", new CustomAnimation<Sprite>([Resources.FindObjectsOfTypeAll<Sprite>().Last(x => x.name == "JohnnyMouthSheet_2")], 1f) },
            { "Talk1", new CustomAnimation<Sprite>([Resources.FindObjectsOfTypeAll<Sprite>().Last(x => x.name == "JohnnyMouthSheet_0")], 0.25f) },
            { "Talk2", new CustomAnimation<Sprite>([Resources.FindObjectsOfTypeAll<Sprite>().Last(x => x.name == "JohnnyMouthSheet_4")], 0.25f) },
            { "IdleFrown", new CustomAnimation<Sprite>([Resources.FindObjectsOfTypeAll<Sprite>().Last(x => x.name == "JohnnyMouthSheet_3")], 1f) },
            { "Talk3", new CustomAnimation<Sprite>([Resources.FindObjectsOfTypeAll<Sprite>().Last(x => x.name == "JohnnyMouthSheet_1")], 0.25f) }
        });
        UpgradeWarehouseRoomFunction.animDefines.AddRange([
            ["Idle", "Talk2", "Talk1"],
            ["IdleFrown", "Talk2", "Talk3"],
            ["IdleFrown", "Talk3", "Talk2"],
            ["Idle", "Talk1", "Talk2"],
        ]);
        Instantiate(storeroombase.Find("CashRegister"), roombase.transform, true);
        var unlazywayWare = shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>();
        unlazywayWare.audAid = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_Help.wav"), "Vfx_Juan_Aid", SoundType.Voice, Color.white);
        unlazywayWare.audWelcome = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_Welcome.wav"), "Vfx_Juan_Welcome", SoundType.Voice, Color.white);
        unlazywayWare.audWelcome.additionalKeys = [new SubtitleTimedKey() {
            time = 2.2f,
            key = "Vfx_Juan_Welcome2"
        }];
        unlazywayWare.audFirstTime = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_FirstTime.wav"), "Vfx_Juan_Intro", SoundType.Voice, Color.white);
        unlazywayWare.audBuy1 = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_Buy1.wav"), "Vfx_Juan_Buy1", SoundType.Voice, Color.white);
        unlazywayWare.audBuy2 = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_Buy2.wav"), "Vfx_Juan_Buy2", SoundType.Voice, Color.white);
        unlazywayWare.audDenied1 = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_Fail1.wav"), "Vfx_Juan_Denied1", SoundType.Voice, Color.white);
        unlazywayWare.audDenied2 = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_Fail2.wav"), "Vfx_Juan_Denied2", SoundType.Voice, Color.white);
        unlazywayWare.audDenied3 = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_Fail3.wav"), "Vfx_Juan_Denied3", SoundType.Voice, Color.white);
        unlazywayWare.audBuyExit1 = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_SuccessExit1.wav"), "Vfx_Juan_ExitSuccess1", SoundType.Voice, Color.white);
        unlazywayWare.audBuyExit2 = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_SuccessExit2.wav"), "Vfx_Juan_ExitSuccess2", SoundType.Voice, Color.white);
        unlazywayWare.audExit1 = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_NoneExit1.wav"), "Vfx_Juan_Exit1", SoundType.Voice, Color.white);
        unlazywayWare.audExit2 = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_NoneExit2.wav"), "Vfx_Juan_Exit2", SoundType.Voice, Color.white);
        unlazywayWare.audBusPass = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_BusPass.wav"), "Vfx_Juan_BusPass_1", SoundType.Voice, Color.white);
        unlazywayWare.audBusPass.additionalKeys = [new SubtitleTimedKey()
        {
            time = 2.2f,
            key = "Vfx_Juan_BusPass_2"
        }];
        unlazywayWare.audFieldTrip = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_FieldTrip.wav"), "Vfx_Juan_FieldTrip", SoundType.Voice, Color.white);
        unlazywayWare.audMap = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "UpgradeWares", "JUN_Map.wav"), "Vfx_Juan_FillMap", SoundType.Voice, Color.white);

        var ceilContainerWarehouse = Instantiate(Resources.FindObjectsOfTypeAll<GameObject>().Last(x => x.name == "JohnnySign"), MTM101BaldiDevAPI.prefabTransform, true);
        ceilContainerWarehouse.name = "WarehouseHangar";
        ceilContainerWarehouse.GetComponent<SpriteRenderer>().sprite = AssetLoader.SpriteFromMod(this, new(0.5f, 1f), 28f, "UpgradeWares", "WarehouseHangars.png");

        var button = Instantiate(storeroombase.Find("GameButton_1"), new Vector3(65f, 0f, 35f), Quaternion.Euler(0f, 90f, 0f), roombase.transform);
        button = Instantiate(storeroombase.Find("GameButton_1"), new Vector3(65f, 0f, 25f), Quaternion.Euler(0f, 90f, 0f), roombase.transform);
        var text = new GameObject("TEEXT", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        text.transform.SetParent(button, false);
        text.color = Color.black;
        text.font = BaldiFonts.ComicSans12.FontAsset();
        text.fontSize = BaldiFonts.ComicSans12.FontSize();
        text.text = "99";
        text.alignment = TextAlignmentOptions.Center;
        text.transform.localPosition = new Vector3(0f, 1.2f, 4.99f);
        shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>().mapPriceText = text;

        var storeRoom = Resources.FindObjectsOfTypeAll<RoomAsset>().Last(x => x.category == RoomCategory.Store);
        var warehouse = Instantiate(storeRoom);
        warehouse.name = "Room_UpgradeWarehouse";
        warehouse.roomFunctionContainer = shopcontainer.GetComponent<RoomFunctionContainer>();
        warehouse.posterDatas = [];
        warehouse.potentialDoorPositions = [new(0, 2)];
        warehouse.forcedDoorPositions = [new(0, 2), new(6, 2)];
        warehouse.requiredDoorPositions = [new(0, 2), new(6, 2)];
        warehouse.cells.Find(cell => cell.pos == new IntVector2(3, 0)).type = 4;
        //warehouse.cells.DoIf(cell => cell.pos == new IntVector2(0, 2) || cell.pos == new IntVector2(6, 2), (cell) => cell.type = 0);
        warehouse.entitySafeCells = [new(0, 1),
        new(0,2), new(6,2),
        new(0,3), new(1, 3), new(2, 3), new(3, 3), new(4, 3), new(5, 3), new(6, 3),
        new (3, 4)];
        warehouse.eventSafeCells = [new(0, 1),
        new(0,2), new(6,2),
        new(0,3), new(1, 3), new(2, 3), new(3, 3), new(4, 3), new(5, 3), new(6, 3),
        new (3, 4)];
        warehouse.blockedWallCells = [new(0, 0), new (0, 4),
        new (1, 0), new (1, 4),
        new (2, 0), new (2, 4), new (2, 5),
        new (3, 5),
        new (4, 0), new (4, 4), new (4, 5),
        new (5, 0), new (5, 4),
        new (6, 0), new (6, 4)];
        var mapposter = new PosterObject()
        {
            name = "MapPosterUpgrade",
            baseTexture = AssetLoader.TextureFromMod(this, "UpgradeWares", "UpgradeMapPortrait.png")
        };
        var restock = new PosterObject()
        {
            name = "RestockPosterUpgrade",
            baseTexture = AssetLoader.TextureFromMod(this, "UpgradeWares", "UpgradeRestockPortrait.png")
        };
        warehouse.posterDatas.AddRange([new PosterData()
        {
            position = new IntVector2(6, 2),
            direction = Direction.East,
            poster = mapposter
        },
        new PosterData()
        {
            position = new IntVector2(6, 3),
            direction = Direction.East,
            poster = restock
        }]);
        for (int i = 0; i < warehouse.basicObjects.Count; i++)
        {
            switch (i)
            {
                case 1 or 4:
                    warehouse.basicObjects[i].rotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
                case 2 or 3:
                    warehouse.basicObjects[i].rotation = Quaternion.Euler(0f, 90f, 0f);
                    warehouse.basicObjects[i].position = new Vector3(warehouse.basicObjects[i].position.x, warehouse.basicObjects[i].position.y, 3.2f);
                    break;
                case 11:
                    warehouse.basicObjects.Add(new BasicObjectData()
                    {
                        prefab = warehouse.basicObjects[i].prefab,
                        position = warehouse.basicObjects[i].position + new Vector3(40f, 0f, 0f),
                        rotation = warehouse.basicObjects[i].rotation,
                        replaceable = true
                    });
                    break;
                default:
                    if (warehouse.basicObjects[i].prefab.name == "JohnnySign")
                        warehouse.basicObjects[i].prefab = ceilContainerWarehouse.transform;
                    break;
            }
        }
        for (int item = 0; item < 6; item++)
            warehouse.itemSpawnPoints.Add(new ItemSpawnPoint());
        for (int i = 0; i < warehouse.itemSpawnPoints.Count; i++) // This feels like doing a base game edit...
        {
            switch (i)
            {
                case 0:
                    warehouse.itemSpawnPoints[i].position = new Vector2(23f, 3f);
                    break;
                case 1:
                    warehouse.itemSpawnPoints[i].position = new Vector2(17f, 3f);
                    break;
                case 2:
                    warehouse.itemSpawnPoints[i].position = new Vector2(11f, 3f);
                    break;
                case 3:
                    warehouse.itemSpawnPoints[i].position = new Vector2(47f, 3f);
                    break;
                case 4:
                    warehouse.itemSpawnPoints[i].position = new Vector2(53f, 3f);
                    break;
                case 17:
                    warehouse.itemSpawnPoints[i].position = new Vector2(58f, 3f);
                    break;
                case 5:
                    warehouse.itemSpawnPoints[i].position = new Vector2(18f, 20f);
                    break;
                case 6:
                    warehouse.itemSpawnPoints[i].position = new Vector2(23f, 20f);
                    break;
                case 7:
                    warehouse.itemSpawnPoints[i].position = new Vector2(28f, 20f);
                    break;
                case 8:
                    warehouse.itemSpawnPoints[i].position = new Vector2(52f, 20f);
                    break;
                case 9:
                    warehouse.itemSpawnPoints[i].position = new Vector2(47f, 20f);
                    break;
                case 10:
                    warehouse.itemSpawnPoints[i].position = new Vector2(42f, 20f);
                    break;
                case 11:
                    warehouse.itemSpawnPoints[i].position = new Vector2(50f, 47f);
                    break;
                case 12:
                    warehouse.itemSpawnPoints[i].position = new Vector2(55f, 47f);
                    break;
                case 13:
                    warehouse.itemSpawnPoints[i].position = new Vector2(60f, 47f);
                    break;
                case 14:
                    warehouse.itemSpawnPoints[i].position = new Vector2(20f, 47f);
                    break;
                case 15:
                    warehouse.itemSpawnPoints[i].position = new Vector2(15f, 47f);
                    break;
                case 16:
                    warehouse.itemSpawnPoints[i].position = new Vector2(10f, 47f);
                    break;
            }
        }
        var pricetagPrefab = Resources.FindObjectsOfTypeAll<PriceTag>().Last(x => x.name == "PriceTag");
        List<PriceTag> tags = new List<PriceTag>();
        for (int i = 0; i < warehouse.itemSpawnPoints.Count; i++) // This is dumb...
        {
            var pricetag = Instantiate(pricetagPrefab, roombase.transform, false);
            switch (i)
            {
                default:
                    pricetag.transform.localPosition = new(25f, 2.65f, 14f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 0:
                    pricetag.transform.localPosition = new(23f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 1:
                    pricetag.transform.localPosition = new(17f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 2:
                    pricetag.transform.localPosition = new(11f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 3:
                    pricetag.transform.localPosition = new(47f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 4:
                    pricetag.transform.localPosition = new(53f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 17:
                    pricetag.transform.localPosition = new(58f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 5:
                    pricetag.transform.localPosition = new(18f, 2.65f, 22f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 6:
                    pricetag.transform.localPosition = new(23f, 2.65f, 22f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 7:
                    pricetag.transform.localPosition = new(28f, 2.65f, 22f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 8:
                    pricetag.transform.localPosition = new(52f, 2.65f, 22f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 9:
                    pricetag.transform.localPosition = new(47f, 2.65f, 22f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 10:
                    pricetag.transform.localPosition = new(42f, 2.65f, 22f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 11:
                    pricetag.transform.localPosition = new(50f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
                case 12:
                    pricetag.transform.localPosition = new(55f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
                case 13:
                    pricetag.transform.localPosition = new(60f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
                case 14:
                    pricetag.transform.localPosition = new(20f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
                case 15:
                    pricetag.transform.localPosition = new(15f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
                case 16:
                    pricetag.transform.localPosition = new(10f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
            }
            tags.Add(pricetag);
        }
        shopcontainer.GetComponent<UpgradeWarehouseRoomFunction>().tags = tags.ToArray();
        warehouse.wallTex = arcadeAssets.Get<Texture2D>("Store/WarehouseWall");
        warehouse.ceilTex = arcadeAssets.Get<Texture2D>("Store/WarehouseCeil");
        warehouse.florTex = arcadeAssets.Get<Texture2D>("Store/WarehouseFloor");
        warehouse.windowChance = 0.6f;

        // YTP Bountyhouse Storekeeper (The store where you can trade in items for more YTPs.)
        arcadeAssets.AddRange<Texture2D>([
            AssetLoader.TextureFromMod(this, "BountyHouse", "BountyhouseWall.png"),
            AssetLoader.TextureFromMod(this, "BountyHouse", "BountyhouseCeil.png"),
            AssetLoader.TextureFromMod(this, "BountyHouse", "BountyhouseFloor.png")
            ],
            [
                "Store/BountyhouseWall",
                "Store/BountyhouseCeil",
                "Store/BountyhouseFloor"
                ]);
        var ytpBounty = new ItemBuilder(Info)
            .SetNameAndDescription("BOUNTY, ERROR", "ERROR ERROR ERROR! SHOULD NOT BE SEEING THIS!")
            .SetSprites(arcadeAssets.Get<Sprite>("OutOfOrderSlot"), arcadeAssets.Get<Sprite>("OutOfOrderSlot"))
            .SetAsInstantUse()
            .SetPickupSound(ItemMetaStorage.Instance.FindByEnum(Items.Points).value.audPickupOverride)
            .SetItemComponent<Item>()
            .SetGeneratorCost(50)
            .SetShopPrice(999)
            .SetEnum("EndlessBounty")
            .Build();
        var bountyItemPrefab = new GameObject("BountyFrame");
        bountyItemPrefab.ConvertToPrefab(true);
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.name = "Model";
        model.transform.SetParent(bountyItemPrefab.transform, false);
        model.GetComponent<MeshRenderer>().SetMaterial(pricetagPrefab.GetComponentsInChildren<MeshRenderer>().Last().GetMaterial());
        model.transform.localScale = new Vector3(2f, 2f, 1f);
        Destroy(model.GetComponent<BoxCollider>());
        var itemFrame = new GameObject("Frame", typeof(SpriteRenderer));
        itemFrame.transform.SetParent(bountyItemPrefab.transform, false);
        itemFrame.transform.localPosition = Vector3.forward * -0.5001f;
        itemFrame.transform.localScale = Vector3.one * 1.4f;
        itemFrame.GetComponent<SpriteRenderer>().material = Resources.FindObjectsOfTypeAll<Material>().Last(mat => mat.name == "SpriteStandard_NoBillboard");
        arcadeAssets.Add<ItemObject>("Store/YTPBountyObject", ytpBounty);
        shopcontainer = new GameObject("ItemBountyhouseRoom", typeof(RoomFunctionContainer), typeof(BountyhouseRoomFunction));
        shopcontainer.ConvertToPrefab(true);
        {
            //shopcontainer.layer = LayerMask.NameToLayer("Ignore Raycast");
            shopcontainer.GetComponent<RoomFunctionContainer>().ReflectionSetVariable("functions", new List<RoomFunction>()); // Had to manually fix it.
            shopcontainer.GetComponent<RoomFunctionContainer>().AddFunction(shopcontainer.GetComponent<BountyhouseRoomFunction>());
        }
        shopcontainer.GetComponent<BountyhouseRoomFunction>().audBell = Resources.FindObjectsOfTypeAll<SoundObject>().Last(s => s.name == "CashBell");
        roombase = new GameObject("RoomBase");
        roombase.transform.SetParent(shopcontainer.transform, false);
        shopcontainer.GetComponent<BountyhouseRoomFunction>().roomBase = roombase.transform;

        alarm = Instantiate(Resources.FindObjectsOfTypeAll<PropagatedAudioManager>().Last(a => a.name == "AlarmSource"), roombase.transform, true);
        shopcontainer.GetComponent<BountyhouseRoomFunction>().alarmAudioManager = alarm;
        shopcontainer.GetComponent<BountyhouseRoomFunction>().audAlarm = Resources.FindObjectsOfTypeAll<SoundObject>().Last(s => s.name == "Elv_Buzz");
        BountyhouseRoomFunction.audRing = Resources.FindObjectsOfTypeAll<SoundObject>().Last(s => s.name == "Activity_Correct");
        var bountykeeper = Instantiate(storeroombase.Find("JohnnyBase"), roombase.transform, true);
        DestroyImmediate(bountykeeper.GetComponentInChildren<Animator>());
        DestroyImmediate(bountykeeper.GetComponent<PropagatedAudioManagerAnimator>());
        bountykeeper.localRotation = Quaternion.Euler(0f, 90f, 0f);
        bountykeeper.localPosition = new Vector3(63.8f, bountykeeper.localPosition.y, 24.42f);
        volumeanim = bountykeeper.gameObject.AddComponent<CustomVolumeAnimator>();
        shopcontainer.GetComponent<BountyhouseRoomFunction>().animator = bountykeeper.GetChild(0).gameObject.AddComponent<CustomSpriteAnimator>();
        spriteanimator = shopcontainer.GetComponent<BountyhouseRoomFunction>().animator;
        volumeanim.animator = spriteanimator;
        volumeanim.audioSource = spriteanimator.GetComponent<AudioSource>();
        spriteanimator.spriteRenderer = spriteanimator.GetComponent<SpriteRenderer>();
        spriteanimator.gameObject.SetActive(false);
        volumeanim.GetComponent<SpriteRenderer>().sprite = placeholdcardboard;
        volumeanim.enabled = false;
        var register = Instantiate(storeroombase.Find("CashRegister"), roombase.transform, true);
        register.localRotation = Quaternion.Euler(0f, 90f, 0f);
        register.localPosition = new Vector3(60.6f, register.localPosition.y, 27.5f);

        var ceilContainerBountyhouse = Instantiate(Resources.FindObjectsOfTypeAll<GameObject>().Last(x => x.name == "JohnnySign"), MTM101BaldiDevAPI.prefabTransform, true);
        ceilContainerBountyhouse.name = "BountyhouseHangar";
        ceilContainerBountyhouse.GetComponent<SpriteRenderer>().sprite = AssetLoader.SpriteFromMod(this, new(0.5f, 1f), 28f, "BountyHouse", "BountyhouseHangars.png");

        var bountyhouse = Instantiate(storeRoom);
        bountyhouse.name = "Room_ItemBountyhouse";
        bountyhouse.roomFunctionContainer = shopcontainer.GetComponent<RoomFunctionContainer>();
        bountyhouse.posterDatas = [];
        bountyhouse.potentialDoorPositions = [new(0, 2)];
        bountyhouse.forcedDoorPositions = [new(0, 2)];
        bountyhouse.requiredDoorPositions = [new(0, 2)];
        bountyhouse.secretCells = [new(6, 1), new(6, 3)];
        bountyhouse.cells.Find(cell => cell.pos == new IntVector2(3, 0)).type = 4;
        bountyhouse.cells.RemoveAll(cell => cell.pos.x == 6 && cell.pos.z != 1 && cell.pos.z != 2 && cell.pos.z != 3);
        bountyhouse.cells.RemoveAll(cell => cell.pos.z == 5);
        bountyhouse.cells.DoIf(cell => cell.pos.x == 6 && cell.pos.z == 2, (cell) => cell.type = 7);
        bountyhouse.cells.DoIf(cell => cell.pos.x == 5, (cell) => cell.type = cell.pos.z == 4 ? 3 : cell.pos.z == 0 ? 6 : 2);
        bountyhouse.cells.DoIf(cell => cell.pos.x == 3 && cell.pos.z == 4, (cell) => cell.type = 1);
        bountyhouse.cells.DoIf(cell => (cell.pos.x == 6 && (cell.pos.z == 1 || cell.pos.z == 3)) || (cell.pos.x == 5 && cell.pos.z == 2), (cell) => cell.type = 0);
        bountyhouse.entitySafeCells = [new(0, 1), new(0, 2), new(0, 3),
        new(1, 1), new(1, 2), new(1, 3),
        new(2, 1), new(2, 2), new(2, 3),
        new(3, 1), new(3, 3),
        new(4, 1), new(4, 3),
        new(5, 1), new(5, 2), new(5, 3)];
        bountyhouse.eventSafeCells = [new(0, 1), new(0, 2), new(0, 3),
        new(1, 1), new(1, 2), new(1, 3),
        new(2, 1), new(2, 2), new(2, 3),
        new(3, 1), new(3, 3),
        new(4, 1), new(4, 3),
        new(5, 1), new(5, 2), new(5, 3)];
        bountyhouse.blockedWallCells = [new(0, 0), new(0, 4),
        new(1, 0), new(1, 4),
        new(2, 0), new(2, 4),
        new(3, 0), new(3, 4),
        new(4, 0), new(4, 4),
        new(5, 0), new(5, 4),
        new(6, 1), new(6, 2), new(6, 3),];
        //bountyhouse.cells.DoIf(cell => cell.pos == new IntVector2(0, 2) || cell.pos == new IntVector2(6, 2), (cell) => cell.type = 0);
        for (int i = 0; i < bountyhouse.basicObjects.Count; i++)
        {
            switch (i)
            {
                case 0:
                    bountyhouse.basicObjects[i].rotation = Quaternion.Euler(0f, 0f, 0f);
                    bountyhouse.basicObjects[i].position = new Vector3(63f, -1.38f, 25f);
                    break;
                case 1:
                    bountyhouse.basicObjects[i].rotation = Quaternion.Euler(0f, 90f, 0f);
                    bountyhouse.basicObjects[i].position = new Vector3(30f, -1.38f, 23f);
                    break;
                case 4:
                    bountyhouse.basicObjects[i].rotation = Quaternion.Euler(0f, 90f, 0f);
                    bountyhouse.basicObjects[i].position = new Vector3(30f, -1.38f, 26f);
                    break;
                case 2 or 3:
                    bountyhouse.basicObjects[i].rotation = Quaternion.Euler(0f, 90f, 0f);
                    bountyhouse.basicObjects[i].position = new Vector3(bountyhouse.basicObjects[i].position.x - (i == 3 ? 10f : 0f), bountyhouse.basicObjects[i].position.y, 3.2f);
                    break;
                case 10:
                    bountyhouse.basicObjects[i].position = new Vector3(bountyhouse.basicObjects[i].position.x - 10f, bountyhouse.basicObjects[i].position.y, bountyhouse.basicObjects[i].position.z);
                    bountyhouse.basicObjects[i].prefab = ceilContainerBountyhouse.transform;
                    break;
                case 11:
                    bountyhouse.basicObjects.Add(new BasicObjectData()
                    {
                        prefab = bountyhouse.basicObjects[i].prefab,
                        position = bountyhouse.basicObjects[i].position + new Vector3(40f - 10f, 0f, 0f),
                        rotation = bountyhouse.basicObjects[i].rotation,
                        replaceable = true
                    });
                    break;
                default:
                    if (bountyhouse.basicObjects[i].prefab.name == "JohnnySign")
                        bountyhouse.basicObjects[i].prefab = ceilContainerBountyhouse.transform;
                    break;
            }
        }
        for (int i = 0; i < bountyhouse.itemSpawnPoints.Count; i++) // This feels like doing a base game edit...
        {
            switch (i)
            {
                case 0:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(23f, 3f);
                    break;
                case 1:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(17f, 3f);
                    break;
                case 2:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(11f, 3f);
                    break;
                case 3:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(47f - 10f, 3f);
                    break;
                case 4:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(53f - 10f, 3f);
                    break;
                case 5:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(58f - 10f, 3f);
                    break;
                case 6:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(50f - 10f, 47f);
                    break;
                case 7:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(55f - 10f, 47f);
                    break;
                case 8:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(60f - 10f, 47f);
                    break;
                case 9:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(20f, 47f);
                    break;
                case 10:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(15f, 47f);
                    break;
                case 11:
                    bountyhouse.itemSpawnPoints[i].position = new Vector2(10f, 47f);
                    break;
            }
        }
        tags = new List<PriceTag>();
        var spriteFrames = new List<SpriteRenderer>();
        for (int i = 0; i < bountyhouse.itemSpawnPoints.Count; i++) // This is dumb...
        {
            var pricetag = Instantiate(pricetagPrefab, roombase.transform, false);
            var itemframe = Instantiate(bountyItemPrefab, roombase.transform, false);
            switch (i)
            {
                default:
                    pricetag.transform.localPosition = new(25f, 2.65f, 14f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    itemframe.transform.localPosition = new(25f, 1f, 14f);
                    itemframe.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 0:
                    pricetag.transform.localPosition = new(23f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    itemframe.transform.localPosition = new(23f, 1.3f, 6f);
                    itemframe.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 1:
                    pricetag.transform.localPosition = new(17f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    itemframe.transform.localPosition = new(17f, 1.3f, 6f);
                    itemframe.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 2:
                    pricetag.transform.localPosition = new(11f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    itemframe.transform.localPosition = new(11f, 1.3f, 6f);
                    itemframe.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 3:
                    pricetag.transform.localPosition = new(47f - 10f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    itemframe.transform.localPosition = new(47f - 10f, 1.3f, 6f);
                    itemframe.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 4:
                    pricetag.transform.localPosition = new(53f - 10f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    itemframe.transform.localPosition = new(53f - 10f, 1.3f, 6f);
                    itemframe.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 5:
                    pricetag.transform.localPosition = new(58f - 10f, 2.65f, 5f);
                    pricetag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    itemframe.transform.localPosition = new(58f - 10f, 1.3f, 6f);
                    itemframe.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 6:
                    pricetag.transform.localPosition = new(50f - 10f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    itemframe.transform.localPosition = new(50f - 10f, 1.3f, 44f);
                    itemframe.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
                case 7:
                    pricetag.transform.localPosition = new(55f - 10f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    itemframe.transform.localPosition = new(55f - 10f, 1.3f, 44f);
                    itemframe.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
                case 8:
                    pricetag.transform.localPosition = new(60f - 10f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    itemframe.transform.localPosition = new(60f - 10f, 1.3f, 44f);
                    itemframe.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
                case 9:
                    pricetag.transform.localPosition = new(20f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    itemframe.transform.localPosition = new(20f, 1.3f, 44f);
                    itemframe.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
                case 10:
                    pricetag.transform.localPosition = new(15f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    itemframe.transform.localPosition = new(15f, 1.3f, 44f);
                    itemframe.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
                case 11:
                    pricetag.transform.localPosition = new(10f, 2.65f, 45f);
                    pricetag.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    itemframe.transform.localPosition = new(10f, 1.3f, 44f);
                    itemframe.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    break;
            }
            tags.Add(pricetag);
            spriteFrames.Add(itemframe.GetComponentInChildren<SpriteRenderer>());
        }
        shopcontainer.GetComponent<BountyhouseRoomFunction>().tag = tags.ToArray();
        shopcontainer.GetComponent<BountyhouseRoomFunction>().itemframe = spriteFrames.ToArray();
        bountyhouse.wallTex = arcadeAssets.Get<Texture2D>("Store/BountyhouseWall");
        bountyhouse.ceilTex = arcadeAssets.Get<Texture2D>("Store/BountyhouseCeil");
        bountyhouse.florTex = arcadeAssets.Get<Texture2D>("Store/BountyhouseFloor");
        bountyhouse.windowChance = 0f;
        bountyhouse.doorMats = Resources.FindObjectsOfTypeAll<StandardDoorMats>().Last(x => x.name == "DefaultDoorSet"); // Placeholder for now...

        /*warehouse.posterDatas.Add(new PosterData()
        {
            poster = placeholdPoster,
            direction = Direction.West,
            position = new(0, 3)
        });*/
        bountyhouse.posterDatas.Add(new PosterData()
        {
            poster = placeholdPoster,
            direction = Direction.East,
            position = new(5, 3)
        });

        var roomgrp = new List<RoomGroup>();
        var store = new RoomGroup();
        var refstore = Resources.FindObjectsOfTypeAll<EndlessGameManager>().Last().levelGenerationModifier.additionalRoomGroup.First(x => x.name == "Store");
        store.stickToHallChance = 1f;
        store.maxRooms = 0;
        store.minRooms = 0;
        store.name = "Store";
        store.ceilingTexture = refstore.ceilingTexture;
        store.wallTexture = refstore.wallTexture;
        store.floorTexture = refstore.floorTexture;
        store.light = refstore.light;
        store.potentialRooms = refstore.potentialRooms.Clone() as WeightedRoomAsset[];
        roomgrp.AddRange([
        store,
        new RoomGroup()
        {
            stickToHallChance = 1f,
            maxRooms = 0,
            minRooms = 0,
            name = "UpgradeStation",
            light = store.light,
            wallTexture = [new() { selection = arcadeAssets.Get<Texture2D>("Store/WarehouseWall"), weight = 100 }],
            ceilingTexture = [new() { selection = arcadeAssets.Get<Texture2D>("Store/WarehouseCeil"), weight = 100 }],
            floorTexture = [new() { selection = arcadeAssets.Get<Texture2D>("Store/WarehouseFloor"), weight = 100 }],
            potentialRooms = [new WeightedRoomAsset()
            {
                weight = 100,
                selection = warehouse
            }]
        },
        new RoomGroup()
        {
            stickToHallChance = 1f,
            maxRooms = 0,
            minRooms = 0,
            name = "BountyStation",
            light = store.light,
            wallTexture = [new() { selection = arcadeAssets.Get<Texture2D>("Store/BountyhouseWall"), weight = 100 }],
            ceilingTexture = [new() { selection = arcadeAssets.Get<Texture2D>("Store/BountyhouseCeil"), weight = 100 }],
            floorTexture = [new() { selection = arcadeAssets.Get<Texture2D>("Store/BountyhouseFloor"), weight = 100 }],
            potentialRooms = [new WeightedRoomAsset()
            {
                weight = 100,
                selection = bountyhouse
            }]
        }]);
        inflevel.manager.levelGenerationModifier = new LevelGenerationModifier()
        {
            additionalRoomGroup = roomgrp.ToArray()
        };
        // Inbox System
        GameObject inboxPrefab = new GameObject("InfInbox", typeof(Inbox));
        inboxPrefab.ConvertToPrefab(true);
        model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.transform.SetParent(inboxPrefab.transform, false);
        model.transform.localScale = new Vector3(3f, 7f, 3f);
        model.GetComponent<MeshRenderer>().SetMaterial(Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "LockerBlue"));
        Destroy(model.GetComponent<BoxCollider>());
        inboxPrefab.AddComponent<BoxCollider>().size = new Vector3(3f, 5f, 3f);
        var inbox = inboxPrefab.GetComponent<Inbox>();
        Canvas inboxCanvas = Instantiate(Resources.FindObjectsOfTypeAll<StoreScreen>().Last().GetComponentInChildren<Canvas>(), inboxPrefab.transform, false);
        inboxCanvas.gameObject.SetActive(false);
        inbox.ReflectionSetVariable("canvas", inboxCanvas);
        inbox.ReflectionSetVariable("description", inboxCanvas.transform.Find("ItemDescription").GetComponent<TMP_Text>());
        Destroy(inboxCanvas.transform.Find("JohnnyBase").gameObject);
        Destroy(inboxCanvas.transform.Find("JohnnyMouth").gameObject);
        Destroy(inboxCanvas.transform.Find("Total").gameObject);
        Destroy(inboxCanvas.transform.Find("Hotspots").gameObject);
        Destroy(inboxCanvas.transform.Find("BG").gameObject);
        for (int i = 0; i <= 7; i++)
            Destroy(inboxCanvas.transform.Find(string.Format("Amount ({0})", i)).gameObject);
        //Destroy(inboxCanvas.transform.Find("ItemImages").gameObject);
        var itembox = inboxCanvas.transform.Find("ItemImages").GetComponentInChildren<Image>();
        for (int i = 6; i < 8; i++)
        {
            var newitembox = Instantiate(itembox, inboxCanvas.transform.Find("ItemImages"), false);
            newitembox.name = string.Format("Image ({0})", i);
            Destroy(inboxCanvas.transform.Find("BoughtImages").gameObject);
            Destroy(inboxCanvas.transform.Find("DraggableAnchor").gameObject);
        }
        inboxCanvas.transform.Find("Back").gameObject.GetComponent<StandardMenuButton>().OnPress = new UnityEngine.Events.UnityEvent();
        inboxCanvas.transform.Find("Back").gameObject.GetComponent<StandardMenuButton>().OnPress.AddListener(inbox.CloseUI);

        var Pitstop = InfPitstop.levelAsset;
        Pitstop.rooms[0].basicObjects.Add(new BasicObjectData()
        {
            position = new Vector3(367f, 3f, 57f),
            prefab = inboxPrefab.transform,
            replaceable = true,
            rotation = Quaternion.Euler(0f, 90f, 0f)
        });

        // And the part where the two stores come in...
        Pitstop.roomAssetPlacements[1].room = warehouse;
        Pitstop.roomAssetPlacements[1].position += new IntVector2(-3, 2);
        Pitstop.doors.Add(new DoorData(0, Resources.FindObjectsOfTypeAll<SwingDoor>().Last(d => d.name == "Door_Auto"), new(33, 9), Direction.North));
        Pitstop.tbos.RemoveAll(x => x.prefab == Resources.FindObjectsOfTypeAll<SwingDoor>().Last(d => d.name == "Door_Auto"));
        /*Pitstop.roomAssetPlacements.AddRange([new RoomAssetPlacementData()
        {
            room = warehouse,
            doorSpawnId = 0,
            direction = Direction.West, // Direction.North,
            position = new(33,10) // new(37,12)
        },
        new RoomAssetPlacementData()
        {
            room = bountyhouse,
            doorSpawnId = 0,
            direction = Direction.North, // Direction.East,
            position = new(36,13) // new(40,9)
        }]);*/
        /*Pitstop.tbos.Add(
            new TileBasedObjectData()
            {
                direction = Direction.West,
                position = new(37, 12),
                prefab = Resources.FindObjectsOfTypeAll<SwingDoor>().Last(d => d.name == "Door_Auto")
            });*/
        /*new TileBasedObjectData()
        {
            direction = Direction.North,
            position = new(40,9),
            prefab = Resources.FindObjectsOfTypeAll<SwingDoor>().Last(d => d.name == "Door_Auto")
        }*/
        //Pitstop.doors.Add(new DoorData(0, Resources.FindObjectsOfTypeAll<Door>().Last(x => x.name == "ClassDoor_Standard"), new(35, 13), Direction.East));

        yield return "Creating base upgrades...";
        Texture2D presentTex = AssetLoader.TextureFromMod(this, "PresentIcon_Large.png");
        Sprite presentSprite = AssetLoader.SpriteFromTexture2D(presentTex, Vector2.one / 2, 50f);
        var presentObject = new ItemBuilder(Info)
            .SetNameAndDescription("Itm_Present", "Itm_Present")
            .SetSprites(presentSprite, presentSprite) // I can't blame ya, creator of Crispy Plus did not follow the size implementation.
            .SetAsInstantUse()
            .SetItemComponent<ITM_Present>()
            .SetGeneratorCost(26)
            .SetShopPrice(int.MaxValue)
            .SetEnum("Present")
            .Build();
        arcadeAssets.Add("RandomPresent", presentObject);
        EndlessUpgradeRegisters.RegisterDefaults();
        
        yield return "Grabbing local textures...";
        var ogendPath = Path.Combine(Application.streamingAssetsPath, "Modded", "mtm101.rulerp.baldiplus.endlessfloors");
        string wallsPath;
        if (Directory.Exists(ogendPath))
        {
            wallsPath = Path.Combine(ogendPath, "Textures", "Walls");
            foreach (string p in Directory.GetFiles(wallsPath))
            {
                string standardName = Path.GetFileNameWithoutExtension(p);
                if (standardName.StartsWith("F_")) continue;
                Texture2D tex = AssetLoader.TextureFromFile(p);
                string[] splitee = standardName.Split('!');
                wallTextures.Add(new WeightedTexture2D()
                {
                    selection = tex,
                    weight = int.Parse(splitee[1])
                });
                string facultyEquiv = Path.Combine(wallsPath, "F_" + splitee[0] + ".png");
                if (File.Exists(facultyEquiv))
                {
                    Texture2D texf = AssetLoader.TextureFromFile(facultyEquiv);
                    facultyWallTextures.Add(new WeightedTexture2D()
                    {
                        selection = texf,
                        weight = int.Parse(splitee[1])
                    });
                }
                else
                {
                    facultyWallTextures.Add(new WeightedTexture2D()
                    {
                        selection = tex,
                        weight = int.Parse(splitee[1])
                    });
                }
            }
            AddWeightedTextures(ceilTextures, "Ceilings", ogendPath);
            AddWeightedTextures(floorTextures, "Floors", ogendPath);
            AddWeightedTextures(profFloorTextures, "ProfFloors", ogendPath);
        }
        wallsPath = Path.Combine(AssetLoader.GetModPath(this), "Textures", "Walls");
        foreach (string p in Directory.GetFiles(wallsPath))
        {
            string standardName = Path.GetFileNameWithoutExtension(p);
            if (standardName.StartsWith("F_")) continue; // no.
            Texture2D tex = AssetLoader.TextureFromFile(p);
            string[] splitee = standardName.Split('!');
            wallTextures.Add(new WeightedTexture2D()
            {
                selection = tex,
                weight = int.Parse(splitee[1])
            });
            string facultyEquiv = Path.Combine(wallsPath, "F_" + splitee[0] + ".png");
            if (File.Exists(facultyEquiv))
            {
                Texture2D texf = AssetLoader.TextureFromFile(facultyEquiv);
                facultyWallTextures.Add(new WeightedTexture2D()
                {
                    selection = texf,
                    weight = int.Parse(splitee[1])
                });
            }
            else
            {
                facultyWallTextures.Add(new WeightedTexture2D()
                {
                    selection = tex,
                    weight = int.Parse(splitee[1])
                });
            }
        }
        AddWeightedTextures(ceilTextures, "Ceilings", AssetLoader.GetModPath(this));
        AddWeightedTextures(floorTextures, "Floors", AssetLoader.GetModPath(this));
        AddWeightedTextures(profFloorTextures, "ProfFloors", AssetLoader.GetModPath(this));
        AddWeightedTextures(setTextures, "Sets");
        AddWeightedTextures(setFacultyTextures, "F_Sets");
        yield return "Grabbing base game textures...";
        /*float baseGameMultiplier = 1.5f;
        wallTextures.AddRange(inflevel.Item1.levelObject.roomGroup.First(x => x.name == "Class").wallTexture.Select(x => new WeightedTexture2D() { weight = Mathf.RoundToInt(x.weight * baseGameMultiplier), selection = x.selection }));
        wallTextures.AddRange(inflevel.Item1.levelObject.hallWallTexs.Select(x => new WeightedTexture2D() { weight = Mathf.RoundToInt(x.weight * baseGameMultiplier), selection = x.selection }));
        facultyWallTextures.AddRange(inflevel.Item1.levelObject.roomGroup.First(x => x.name == "Faculty").wallTexture.Select(x => new WeightedTexture2D() { weight = Mathf.RoundToInt(x.weight * baseGameMultiplier), selection = x.selection }));
        ceilTextures.AddRange(inflevel.Item1.levelObject.hallCeilingTexs.Select(x => new WeightedTexture2D() { weight = Mathf.RoundToInt(x.weight * baseGameMultiplier), selection = x.selection }));
        ceilTextures.AddRange(inflevel.Item1.levelObject.roomGroup.First(x => x.name == "Class").ceilingTexture.Select(x => new WeightedTexture2D() { weight = Mathf.RoundToInt(x.weight * baseGameMultiplier), selection = x.selection }));
        ceilTextures.AddRange(inflevel.Item1.levelObject.roomGroup.First(x => x.name == "Faculty").ceilingTexture.Select(x => new WeightedTexture2D() { weight = Mathf.RoundToInt(x.weight * baseGameMultiplier), selection = x.selection }));
        floorTextures.AddRange(inflevel.Item1.levelObject.hallFloorTexs.Select(x => new WeightedTexture2D() { weight = Mathf.RoundToInt(x.weight * baseGameMultiplier), selection = x.selection }));
        profFloorTextures.AddRange(inflevel.Item1.levelObject.roomGroup.First(x => x.name == "Class").floorTexture.Select(x => new WeightedTexture2D() { weight = Mathf.RoundToInt(x.weight * baseGameMultiplier), selection = x.selection }));
        profFloorTextures.AddRange(inflevel.Item1.levelObject.roomGroup.First(x => x.name == "Faculty").ceilingTexture.Select(x => new WeightedTexture2D() { weight = Mathf.RoundToInt(x.weight * baseGameMultiplier), selection = x.selection }));
        setTextures.AddRange([ // What's the alternative way again??
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[0].selection, inflevel.Item1.levelObject.hallCeilingTexs[0].selection, inflevel.Item1.levelObject.hallFloorTexs[0].selection)
            },
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[0].selection, inflevel.Item1.levelObject.hallCeilingTexs[1].selection, inflevel.Item1.levelObject.hallFloorTexs[0].selection)
            },
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[0].selection, inflevel.Item1.levelObject.hallCeilingTexs[0].selection, inflevel.Item1.levelObject.hallFloorTexs[1].selection)
            },
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[0].selection, inflevel.Item1.levelObject.hallCeilingTexs[1].selection, inflevel.Item1.levelObject.hallFloorTexs[1].selection)
            },
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[0].selection, inflevel.Item1.levelObject.hallCeilingTexs[0].selection, inflevel.Item1.levelObject.hallFloorTexs[2].selection)
            },
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[0].selection, inflevel.Item1.levelObject.hallCeilingTexs[1].selection, inflevel.Item1.levelObject.hallFloorTexs[2].selection)
            },
            /*new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[1].selection, inflevel.Item1.levelObject.hallCeilingTexs[0].selection, inflevel.Item1.levelObject.hallFloorTexs[0].selection)
            },
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[1].selection, inflevel.Item1.levelObject.hallCeilingTexs[1].selection, inflevel.Item1.levelObject.hallFloorTexs[0].selection)
            },
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[1].selection, inflevel.Item1.levelObject.hallCeilingTexs[0].selection, inflevel.Item1.levelObject.hallFloorTexs[1].selection)
            },
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[1].selection, inflevel.Item1.levelObject.hallCeilingTexs[1].selection, inflevel.Item1.levelObject.hallFloorTexs[1].selection)
            },
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[1].selection, inflevel.Item1.levelObject.hallCeilingTexs[0].selection, inflevel.Item1.levelObject.hallFloorTexs[2].selection)
            },
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.hallWallTexs[1].selection, inflevel.Item1.levelObject.hallCeilingTexs[1].selection, inflevel.Item1.levelObject.hallFloorTexs[2].selection)
            },

        ]);
        setFacultyTextures.AddRange([
            new WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>()
            {
                weight = Mathf.RoundToInt(100 * baseGameMultiplier),
                selection = new Tuple<Texture2D, Texture2D, Texture2D>(inflevel.Item1.levelObject.roomGroup.First(x => x.name == "Faculty").wallTexture[0].selection, inflevel.Item1.levelObject.roomGroup.First(x => x.name == "Faculty").ceilingTexture[0].selection, inflevel.Item1.levelObject.roomGroup.First(x => x.name == "Faculty").floorTexture[0].selection)
            }
            ]);*/

        yield return "Grabbing saves...";
        ModdedSaveSystem.AddSaveLoadAction(this, (isSave, path) =>
        {
            ENDFloorsData filedata;
            if (isSave)
            {
                filedata = new ENDFloorsData();
                if (File.Exists(Path.Combine(path, "arcadedata.dat")))
                    filedata = JsonUtility.FromJson<ENDFloorsData>(RijndaelEncryption.Decrypt(File.ReadAllText(Path.Combine(path, "arcadedata.dat")), "ArcadeEndlessForever_" + PlayerFileManager.Instance.fileName));
                filedata.topFloors = currentData.Item1;
                filedata.defeated99F = currentData.Item2;
                filedata.reached99F = currentData.Item3;
                filedata.firstTimeQuickShop = firstEncounters.Item1;
                filedata.firstTimeWares = firstEncounters.Item2;
                filedata.firstTimeBounty = firstEncounters.Item3;
                File.WriteAllText(Path.Combine(path, "arcadedata.dat"), RijndaelEncryption.Encrypt(JsonUtility.ToJson(filedata), "ArcadeEndlessForever_" + PlayerFileManager.Instance.fileName));
            }
            else if (File.Exists(Path.Combine(path, "arcadedata.dat")))
            {
                bool flag = true;
                try
                {
                    filedata = JsonUtility.FromJson<ENDFloorsData>(RijndaelEncryption.Decrypt(File.ReadAllText(Path.Combine(path, "arcadedata.dat")), "ArcadeEndlessForever_" + PlayerFileManager.Instance.fileName));
                }
                catch
                {
                    filedata = new ENDFloorsData();
                    flag = false;
                }

                if (!flag)
                    return;

                currentData = new Tuple<int, bool, bool>(filedata.topFloors, filedata.defeated99F, filedata.reached99F);
                firstEncounters = new Tuple<bool, bool, bool>(filedata.firstTimeQuickShop, filedata.firstTimeWares, filedata.firstTimeBounty);
            }
        });
    }

    internal void SaveData()
    {
        if (!(MTM101BaldiDevAPI.SaveGamesEnabled && CoreGameManager.Instance?.sceneObject == inflevel) || CoreGameManager.Instance?.lifeMode == LifeMode.Explorer) return;
        currentData = new Tuple<int, bool, bool>(
            gameSave.currentFloor > currentData.Item1 ? gameSave.currentFloor : currentData.Item1,
            gameSave.currentFloor - 1 == 99 ? true : currentData.Item2,
            gameSave.currentFloor - 1 == 99 && gameSave.startingFloor == 1 ? true : currentData.Item3);
        ModdedSaveSystem.CallSaveLoadAction(Instance, true, ModdedSaveSystem.GetCurrentSaveFolder(Instance));
    }

    internal void ExtendGenData(GeneratorData genData)
    {
        var alllevels = SceneObjectMetaStorage.Instance.FindAll(x => x.value != inflevel && ((x.title == "F1" && x.number == 0) || (x.title == "F2" && x.number == 1) || (x.title == "F3" && x.number == 2)
        || (x.title == "F4" && x.number == 3) || (x.title == "F5" && x.number == 4)));
        var schoolhouselevels = SceneObjectMetaStorage.Instance.FindAll(x => x.value != inflevel && ((x.title == "F1" && x.number == 0) || (x.title == "F2" && x.number == 1) || (x.title == "F3" && x.number == 2)));
        NPCMetaStorage npcs = MTM101BaldiDevAPI.npcMetadata;
        genData.npcs.AddRange([
            new WeightedNPC() {
                weight = 90,
                selection = npcs.Get(Character.Playtime).value
            },
            new WeightedNPC() {
                weight = 100,
                selection = npcs.Get(Character.Sweep).value
            },
            new WeightedNPC() {
                weight = 110,
                selection = npcs.Get(Character.Beans).value
            },
            new WeightedNPC() {
                weight = 85,
                selection = npcs.Get(Character.Bully).value
            },
            new WeightedNPC() {
                weight = 80,
                selection = npcs.Get(Character.Crafters).value
            },
            new WeightedNPC() {
                weight = 80,
                selection = npcs.Get(Character.Chalkles).value
            },
            new WeightedNPC() {
                weight = 10,
                selection = npcs.Get(Character.LookAt).value
            },
            new WeightedNPC() {
                weight = 90,
                selection = npcs.Get(Character.Pomp).value
            },
            new WeightedNPC() {
                weight = 95,
                selection = npcs.Get(Character.Cumulo).value
            },
            new WeightedNPC() {
                weight = 70,
                selection = npcs.Get(Character.Prize).value
            },
            new WeightedNPC() {
                weight = 90,
                selection = npcs.Get(Character.DrReflex).value
            },
        ]);
        genData.forcedNpcs.Add(npcs.Get(Character.Principal).value);
        Dictionary<NPC, WeightedNPC> othrNPCs = new Dictionary<NPC, WeightedNPC>();
        foreach (var npc in alllevels.SelectMany(x => x.value.potentialNPCs))
        {
            if (!othrNPCs.ContainsKey(npc.selection) && !genData.npcs.Exists(x => x.selection == npc.selection))
                othrNPCs.Add(npc.selection, npc);
        }
        genData.npcs.AddRange(othrNPCs.Values);
        genData.forcedNpcs.AddRange(alllevels.SelectMany(x => x.value.forcedNpcs).Where(x => !genData.forcedNpcs.Contains(x)));
        RandomEventMetaStorage rngs = MTM101BaldiDevAPI.randomEventStorage;
        genData.randomEvents.AddRange([
            new WeightedRandomEvent()
            {
                selection = rngs.Get(RandomEventType.Fog).value,
                weight = 150
            },
            new WeightedRandomEvent()
            {
                selection = rngs.Get(RandomEventType.Party).value,
                weight = 125
            },
            new WeightedRandomEvent()
            {
                selection = rngs.Get(RandomEventType.Snap).value,
                weight = 70
            },
            new WeightedRandomEvent()
            {
                selection = rngs.Get(RandomEventType.Flood).value,
                weight = 90
            },
            new WeightedRandomEvent()
            {
                selection = rngs.Get(RandomEventType.Lockdown).value,
                weight = 65
            },
            new WeightedRandomEvent()
            {
                selection = rngs.Get(RandomEventType.Gravity).value,
                weight = 55
            },
            new WeightedRandomEvent()
            {
                selection = rngs.Get(RandomEventType.MysteryRoom).value,
                weight = 50
            }
        ]);
        Dictionary<RandomEvent, WeightedRandomEvent> events = new Dictionary<RandomEvent, WeightedRandomEvent>();
        foreach (var otherevent in alllevels.SelectMany(x => x.value.GetCustomLevelObjects()).SelectMany(x => x.randomEvents))
        {
            if (!events.ContainsKey(otherevent.selection) && !genData.randomEvents.Exists(x => x.selection == otherevent.selection))
                events.Add(otherevent.selection, otherevent);
        }
        genData.randomEvents.AddRange(events.Values);
        foreach (var room in Resources.FindObjectsOfTypeAll<RoomAsset>().Where(rm => rm.category == RoomCategory.Class))
            genData.classRoomAssets.Add(new()
            {
                selection = room,
                weight = room.hasActivity ? 100 : 85
            });
        foreach (var faculty in Resources.FindObjectsOfTypeAll<RoomAsset>().Where(rm => rm.category == RoomCategory.Faculty && rm.roomFunctionContainer?.GetComponent<LockedRoomFunction>() == null))
            genData.facultyRoomAssets.Add(new()
            {
                selection = faculty,
                weight = 99,
            });
        var rooms = Resources.FindObjectsOfTypeAll<RoomAsset>();
        switch (genData.lvlObj.type)
        {
            case LevelType.Schoolhouse:
                Dictionary<RoomAsset, WeightedRoomAsset> assets = new Dictionary<RoomAsset, WeightedRoomAsset>();
                foreach (var asset in schoolhouselevels
                    .Select(x => x.value).SelectMany(x => x.GetCustomLevelObjects()).Where(x => x.type == LevelType.Schoolhouse).Select(x => x.potentialSpecialRooms).SelectMany(x => x))
                {
                    if (!assets.ContainsKey(asset.selection))
                        assets.Add(asset.selection, asset);
                }
                genData.specialRoomAssets.AddRange(assets.Values/*[
            new WeightedRoomAsset()
            {
                weight = 190,
                selection = rooms.Last(rm => rm.name == "Cafeteria_1")
            },
            new WeightedRoomAsset()
            {
                weight = 190,
                selection = rooms.Last(rm => rm.name == "Cafeteria_2")
            },
            new WeightedRoomAsset()
            {
                weight = 190,
                selection = rooms.Last(rm => rm.name == "Cafeteria_3")
            },
            new WeightedRoomAsset()
            {
                weight = 115,
                selection = rooms.Last(rm => rm.name == "Cafeteria_Hard_1")
            },
            new WeightedRoomAsset()
            {
                weight = 115,
                selection = rooms.Last(rm => rm.name == "Cafeteria_Hard_2")
            },
            new WeightedRoomAsset()
            {
                weight = 200,
                selection = rooms.Last(rm => rm.name == "Library_1")
            },
            new WeightedRoomAsset()
            {
                weight = 200,
                selection = rooms.Last(rm => rm.name == "Library_2")
            },
            new WeightedRoomAsset()
            {
                weight = 200,
                selection = rooms.Last(rm => rm.name == "Library_3")
            },
            new WeightedRoomAsset()
            {
                weight = 200,
                selection = rooms.Last(rm => rm.name == "Playground1")
            },
            new WeightedRoomAsset()
            {
                weight = 200,
                selection = rooms.Last(rm => rm.name == "Playground_2")
            },
            new WeightedRoomAsset()
            {
                weight = 200,
                selection = rooms.Last(rm => rm.name == "Playground_3")
            }
        ]*/);
                break;
            case LevelType.Laboratory or LevelType.Factory:
                if (currentFloorData.minSize < 74 || currentFloorData.maxSize < 74) break; // Avoiding interruptions towards forced special rooms.
                genData.specialRoomAssets.AddRange([
            new WeightedRoomAsset()
            {
                weight = 100,
                selection = rooms.Last(rm => rm.name == "Cafeteria_1")
            },
            new WeightedRoomAsset()
            {
                weight = 100,
                selection = rooms.Last(rm => rm.name == "Cafeteria_2")
            },
            new WeightedRoomAsset()
            {
                weight = 100,
                selection = rooms.Last(rm => rm.name == "Cafeteria_3")
            },
            new WeightedRoomAsset()
            {
                weight = 45,
                selection = rooms.Last(rm => rm.name == "Cafeteria_Hard_1")
            },
            new WeightedRoomAsset()
            {
                weight = 45,
                selection = rooms.Last(rm => rm.name == "Cafeteria_Hard_2")
            },
        ]);
                break;
            case LevelType.Maintenance:
                genData.specialRoomAssets.AddRange([new WeightedRoomAsset()
                {
                    weight = 100,
                    selection = rooms.Last(rm => rm.name == "LightbulbTesting_0")
                },
            new WeightedRoomAsset()
            {
                weight = 35,
                selection = rooms.Last(rm => rm.name == "Library_1")
            },
            new WeightedRoomAsset()
            {
                weight = 35,
                selection = rooms.Last(rm => rm.name == "Library_2")
            },
            new WeightedRoomAsset()
            {
                weight = 35,
                selection = rooms.Last(rm => rm.name == "Library_3")
            }]);
                break;
        }
        foreach (var office in Resources.FindObjectsOfTypeAll<RoomAsset>().Where(rm => rm.category == RoomCategory.Office))
            genData.officeRoomAssets.Add(new()
            {
                selection = office,
                weight = 99,
            });
        ItemMetaStorage items = MTM101BaldiDevAPI.itemMetadata;
        genData.items.AddRange([
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.Quarter).value,
                weight = genData.lvlObj.type == LevelType.Schoolhouse ? 111 : 101
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.AlarmClock).value,
                weight = 125
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.Apple).value,
                weight = 15
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.Boots).value,
                weight = genData.lvlObj.type == LevelType.Factory ? 125 : 100
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.ChalkEraser).value,
                weight = 125
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.DetentionKey).value,
                weight = 102
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.GrapplingHook).value,
                weight = 50
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.Nametag).value,
                weight = genData.lvlObj.type == LevelType.Laboratory ? 65 : 101
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.Wd40).value,
                weight = 103
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.PortalPoster).value,
                weight = 50
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.PrincipalWhistle).value,
                weight = 102
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.Scissors).value,
                weight = 103
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.DoorLock).value,
                weight = 100
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.Tape).value,
                weight = 102
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.Teleporter).value,
                weight = 15
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.ZestyBar).value,
                weight = 105
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.DietBsoda).value,
                weight = 120
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.Bsoda).value,
                weight = 88
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.NanaPeel).value,
                weight = 103
            },
            new WeightedItemObject()
            {
                selection = items.GetPointsObject(100, true),
                weight = 15
            },
            new WeightedItemObject()
            {
                selection = items.GetPointsObject(50, true),
                weight = 30
            },
            new WeightedItemObject()
            {
                selection = items.GetPointsObject(25, true),
                weight = 60
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.InvisibilityElixir).value,
                weight = 25
            },
            new WeightedItemObject()
            {
                selection = items.FindByEnum(Items.ReachExtender).value,
                weight = 100
            },
            new WeightedItemObject()
            {
                selection = arcadeAssets.Get<ItemObject>("RandomPresent"),
                weight = 80
            }
        ]);
        Dictionary<ItemObject, WeightedItemObject> itms = new Dictionary<ItemObject, WeightedItemObject>();
        foreach (var item in alllevels.SelectMany(x => x.value.GetCustomLevelObjects()).SelectMany(x => x.potentialItems))
        {
            if (!itms.ContainsKey(item.selection) && !genData.items.Exists(x => x.selection == item.selection))
                itms.Add(item.selection, item);
        }
        genData.items.AddRange(itms.Values);
        genData.hallInsertions.AddRange([
            new WeightedRoomAsset()
            {
                weight = 100,
                selection = rooms.Last(rm => rm.name == "HallStructure_0")
            },
            new WeightedRoomAsset()
            {
                weight = 100,
                selection = rooms.Last(rm => rm.name == "HallFormation_0")
            },
            new WeightedRoomAsset()
            {
                weight = 100,
                selection = rooms.Last(rm => rm.name == "HallFormation_2")
            }
        ]);
        Dictionary<RoomAsset, WeightedRoomAsset> hallinserts = new Dictionary<RoomAsset, WeightedRoomAsset>();
        foreach (var hall in alllevels.SelectMany(x => x.value.GetCustomLevelObjects()).SelectMany(x => x.potentialPrePlotSpecialHalls))
        {
            if (!hallinserts.ContainsKey(hall.selection) && !genData.hallInsertions.Exists(x => x.selection == hall.selection))
                hallinserts.Add(hall.selection, hall);
        }
        genData.potentialObjectBuilders = [.. genData.lvlObj.potentialStructures];
        foreach (var structure in alllevels.SelectMany(x => x.value.GetCustomLevelObjects()).Where(x => x.type == genData.lvlObj.type).SelectMany(x => x.potentialStructures))
        {
            if (!genData.potentialObjectBuilders.Exists(x => x.selection.prefab == structure.selection.prefab) || 
                (structure.selection.prefab is Structure_EnvironmentObjectPlacer && !genData.potentialObjectBuilders.Contains(structure) && !genData.potentialObjectBuilders.Exists(x => x.selection.parameters?.prefab?.ToList().Exists(pre => structure.selection.parameters?.prefab?.ToList().Exists(a => a.selection == pre.selection) == true) == true)))
                genData.potentialObjectBuilders.Add(structure);
        }
        genData.forcedObjectBuilders = [.. genData.lvlObj.forcedStructures];
        foreach (var structure in alllevels.SelectMany(x => x.value.GetCustomLevelObjects()).Where(x => x.type == genData.lvlObj.type).SelectMany(x => x.forcedStructures))
        {
            if (!genData.forcedObjectBuilders.Exists(x => x.prefab == structure.prefab) || 
                (structure.prefab is Structure_EnvironmentObjectPlacer && !genData.forcedObjectBuilders.Contains(structure) && !genData.forcedObjectBuilders.Exists(x => x.parameters?.prefab?.ToList().Exists(pre => structure.parameters?.prefab?.ToList().Exists(a => a.selection == pre.selection) == true) == true)))
                genData.forcedObjectBuilders.Add(structure);
        }
        genData.hallInsertions.AddRange(hallinserts.Values);
        foreach (KeyValuePair<BepInEx.PluginInfo, Action<GeneratorData>> kvp in genActions)
        {
            try
            {
                kvp.Value.Invoke(genData);
            }
            catch (Exception e)
            {
                MTM101BaldiDevAPI.CauseCrash(kvp.Key, e);
            }
        }
    }
}

public struct UpgradeSaveData
{
    public string id { private set; get; }
    public byte count;

    public UpgradeSaveData(string id, byte count)
    {
        this.id = id;
        this.count = count;
    }
}

internal class ArcadeEndlessForeverSave : ModdedSaveGameIOBinary
{
    public override BepInEx.PluginInfo pluginInfo => EndlessForeverPlugin.Instance.Info;
    public int currentFloor
    {
        get
        {
            return myFloorData.FloorID;
        }
        set
        {
            myFloorData.FloorID = value;
        }
    }
    public int startingFloor = 1;
    public bool IsInfGamemode = false;
    public bool upgradeStoreHelped = false;
    public FloorData myFloorData = new FloorData();
    public UpgradeSaveData[] Upgrades = [new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0)];
    public UpgradeSaveData[] inbox = [new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0),
    new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0)];
    public Dictionary<string, byte> Counters = new Dictionary<string, byte>();
    public byte itemSlots => Counters["slots"];
    public byte savedGrade = 16;

    public ArcadeEndlessForeverSave()
    {
        Counters.Add("slots", 1);
    }

    public override void Save(BinaryWriter writer)
    {
        writer.Write((byte)3); //format version
        writer.Write(currentFloor);
        writer.Write(startingFloor);
        writer.Write((byte)Upgrades.Length);
        for (int i = 0; i < Upgrades.Length; i++)
        {
            writer.Write(Upgrades[i].id);
            writer.Write(Upgrades[i].count);
        }
        writer.Write(Counters.Count);
        foreach (KeyValuePair<string, byte> item in Counters)
        {
            writer.Write(item.Key);
            writer.Write(item.Value);
        }
        if (CoreGameManager.Instance)
            savedGrade = (byte)CoreGameManager.Instance.GradeVal;
        writer.Write(savedGrade);
        writer.Write((byte)inbox.Length);
        for (int i = 0; i < inbox.Length; i++)
        {
            writer.Write(inbox[i].id);
            writer.Write(inbox[i].count);
        }
        writer.Write(IsInfGamemode);
        writer.Write(upgradeStoreHelped);
    }

    public override void Load(BinaryReader reader)
    {
        byte version = reader.ReadByte();
        currentFloor = reader.ReadInt32();
        startingFloor = reader.ReadInt32();
        Counters.Clear();
        int upgradeLength = reader.ReadByte();
        for (int i = 0; i < upgradeLength; i++)
            Upgrades[i] = new UpgradeSaveData(reader.ReadString(), reader.ReadByte());
        int counterCount = reader.ReadInt32();
        for (int i = 0; i < counterCount; i++)
            Counters.Add(reader.ReadString(), reader.ReadByte());
        savedGrade = reader.ReadByte();
        int inboxLength = reader.ReadByte();
        for (int i = 0; i < inboxLength; i++)
            inbox[i] = new UpgradeSaveData(reader.ReadString(), reader.ReadByte());
        if (version == 1) return;
        IsInfGamemode = reader.ReadBoolean();
        if (version == 2) return;
        upgradeStoreHelped = reader.ReadBoolean();
    }

    public override void OnCGMCreated(CoreGameManager instance, bool isFromSavedGame)
    {
        if (!isFromSavedGame)
            Reset();
        IsInfGamemode = IsINF(instance);
    }

    private bool IsINF(CoreGameManager instance) => instance.sceneObject == EndlessForeverPlugin.Instance.inflevel || instance.nextLevel == EndlessForeverPlugin.Instance.inflevel
        || instance.sceneObject == EndlessForeverPlugin.Instance.InfPitstop;

    public override void Reset()
    {
        IsInfGamemode = CoreGameManager.Instance != null ? IsINF(CoreGameManager.Instance) : false;
        Upgrades = [new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0)];
        Counters = new Dictionary<string, byte>() { { "slots", 1 } };
        savedGrade = 16;
        inbox = [new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0),
    new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0), new UpgradeSaveData("none", 0)];
        upgradeStoreHelped = false;
    }

    // For Upgrades
    public bool HasUpgrade(string type) => GetUpgradeCount(type) > 0;
    public bool HasEmptyUpgrade()
    {
        for (int i = 0; i < Upgrades.Length; i++)
        {
            if (Upgrades[i].id == "none")
                return true;
        }
        return false;
    }

    public int GetUpgradeCount(string type)
    {
        if (Counters.ContainsKey(type)) return Counters[type];
        for (int i = 0; i < Upgrades.Length; i++)
        {
            if (Upgrades[i].id == type)
            {
                return Upgrades[i].count;
            }
        }
        return 0;
    }

    public void SellUpgrade(string id)
    {
        for (int i = 0; i < Upgrades.Length; i++)
        {
            if (Upgrades[i].id == id)
            {
                if (Upgrades[i].count == 1)
                {
                    Upgrades[i] = new UpgradeSaveData("none", 0);
                    return;
                }
                Upgrades[i].count--;
                return;
            }
        }
    }

    public void RemoveFromInbox(string id)
    {
        for (int i = 0; i < inbox.Length; i++)
        {
            if (inbox[i].id == id)
            {
                if (inbox[i].count == 1)
                {
                    inbox[i] = new UpgradeSaveData("none", 0);
                    return;
                }
                inbox[i].count--;
                return;
            }
        }
    }

    public bool AddToInbox(StandardUpgrade upgrade)
    {
        for (int i = 0; i < inbox.Length; i++)
        {
            if (inbox[i].id == "none")
            {
                inbox[i] = new UpgradeSaveData(upgrade.id, 1);
                return true;
            }
        }
        return false;
    }

    public bool CanPurchaseUpgrade(StandardUpgrade upgrade, UpgradePurchaseBehavior behavior, bool Inbox = false)
    {
        switch (behavior)
        {
            case UpgradePurchaseBehavior.IncrementCounter:
                if (!Counters.ContainsKey(upgrade.id))
                    return true;
                if (Counters[upgrade.id] == byte.MaxValue)
                    return false;
                return true;
            case UpgradePurchaseBehavior.Nothing:
                return true;
            case UpgradePurchaseBehavior.FillUpgradeSlot:
                for (int i = 0; i < Upgrades.Length; i++)
                {
                    if (Upgrades[i].id == upgrade.id)
                        return Upgrades[i].count < upgrade.levels.Length;
                    else if (Upgrades[i].id == "none" && !Inbox)
                        return true;
                }
                for (int i = 0; i < inbox.Length; i++)
                {
                    if (inbox[i].id == "none" && Inbox)
                        return true;
                }
                return false;
        }
        throw new NotImplementedException("Not Implemented:" + behavior.ToString());
    }

    public bool PurchaseUpgrade(StandardUpgrade upgrade, UpgradePurchaseBehavior behavior, bool Inbox = false)
    {
        switch (behavior)
        {
            case UpgradePurchaseBehavior.IncrementCounter:
                if (!Counters.ContainsKey(upgrade.id))
                    Counters.Add(upgrade.id, 0);
                if (Counters[upgrade.id] == byte.MaxValue)
                    return false;
                Counters[upgrade.id]++;
                return true;
            case UpgradePurchaseBehavior.Nothing:
                return true;
            case UpgradePurchaseBehavior.FillUpgradeSlot:
                for (int i = 0; i < Upgrades.Length; i++)
                {
                    if (Upgrades[i].id == upgrade.id)
                    {
                        Upgrades[i].count++;
                        return true;
                    }
                    else if (Upgrades[i].id == "none" && !Inbox)
                    {
                        Upgrades[i] = new UpgradeSaveData(upgrade.id, 1);
                        return true;
                    }
                }
                for (int i = 0; i < inbox.Length; i++)
                {
                    if (inbox[i].id == "none" && Inbox)
                        return AddToInbox(upgrade);
                }
                return false;
        }
        throw new NotImplementedException("Not Implemented:" + behavior.ToString());
    }
}

/*[Serializable]
internal class ENDFloorsSaveJSON
{
    public int CurrentFloor = 1;
    public int StartingFloor = 1;
    public bool IsInfGamemode;
    public UpgradeSaveData[] Upgrades = new UpgradeSaveData[5];
    public Dictionary<string, byte> Counters = new Dictionary<string, byte>();
    public UpgradeSaveData[] Inbox = new UpgradeSaveData[8];
    public byte savedGrade = 16;
}*/

[Serializable]
internal class ENDFloorsData
{
    internal bool DoneEndlessFloorsMigration;
    public bool firstTimeQuickShop;
    public bool firstTimeWares;
    public bool firstTimeBounty;
    public int topFloors = 1;
    public bool defeated99F;
    public bool reached99F;
}
