using HarmonyLib;
using MTM101BaldAPI.Registers;
using MTM101BaldAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using EndlessFloorsForever.Patches;
using MTM101BaldAPI.Reflection;
using System.Collections;

namespace EndlessFloorsForever.Components;

public class InfGameManager : MainGameManager
{
    protected override void AwakeFunction()
    {
        defaultLives = 2 + EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount("bonuslife");
    }
    protected new void Start() => UpdateData();

    [SerializeField] internal SoundObject F99AllNotebooks;

    public override void PrepareLevelGenerationData()
    {
        Start();
        base.PrepareLevelGenerationData();
        FloorData currentFD = EndlessForeverPlugin.currentFloorData;
        GeneratorData genData = new GeneratorData((CustomLevelGenerationParameters)levelObject);
        EndlessForeverPlugin.Instance.ExtendGenData(genData);
        var sceneObj = CoreGameManager.Instance.sceneObject;
        var lvlObj = levelObject;
        sceneObj.levelNo = currentFD.FloorID - 1;
        levelNo = currentFD.FloorID - 1;
        lvlObj.finalLevel = false;
        lvlObj.name = $"EndlessF{sceneObj.levelNo}";
        System.Random rng = new System.Random(CoreGameManager.Instance.Seed() + sceneObj.levelNo);

        // Standard Stuff
        lvlObj.potentialItems = genData.items.ToArray();
        sceneObj.shopItems = genData.items.Where(item => item.selection.price > 0 && item.selection.itemType != Items.Points && item.selection.price < int.MaxValue).ToArray();
        sceneObj.totalShopItems = Mathf.Clamp(Mathf.FloorToInt(currentFD.FloorID / 3) + 3, 3, 12);
        lvlObj.shopItems = genData.items.Where(item => item.selection.price > 0 && item.selection.itemType != Items.Points && item.selection.price < int.MaxValue).ToArray();
        lvlObj.totalShopItems = sceneObj.totalShopItems;
        lvlObj.forcedItems = ((currentFD.FloorID % 8) == 0) ? [ItemMetaStorage.Instance.FindByEnum(Items.BusPass).value] : [];
        lvlObj.maxItemValue = currentFD.maxItemValue;
        lvlObj.minSize = new IntVector2(currentFD.minSize, currentFD.minSize);
        lvlObj.maxSize = new IntVector2(currentFD.maxSize, currentFD.maxSize);
        if (lvlObj.type == LevelType.Laboratory && (lvlObj.minSize.x < 25 || lvlObj.minSize.z < 25 || lvlObj.maxSize.x < 25 || lvlObj.maxSize.z < 25)) // Zone teleporters are not created on room sizes this small.
        {
            lvlObj.minSize += new IntVector2(15, 15);
            lvlObj.maxSize += new IntVector2(15, 15);
        }
        //lvlObj.fieldTrip = ((currentFD.FloorID % 4 == 0) && currentFD.FloorID != 4) || currentFD.FloorID % 99 == 0;
        //lvlObj.fieldTrips = genData.fieldTrips.ToArray();
        int avgWeight = 0;
        int heighestWeight = 0;
        for (int i = 0; i < genData.items.Count; i++)
        {
            avgWeight += genData.items[i].weight;
            if (genData.items[i].weight > heighestWeight)
            {
                heighestWeight = genData.items[i].weight;
            }
        }
        avgWeight /= genData.items.Count;

        lvlObj.exitCount = currentFD.exitCount;
        lvlObj.additionTurnChance = (int)Mathf.Clamp((currentFD.unclampedScaleVar / 2), 0f, 35f);
        //lvlObj.windowChance = Mathf.Max((currentFD.FloorID * -1.2f) + 14, 2);
        lvlObj.maxPlots = currentFD.maxPlots;
        lvlObj.minPlots = currentFD.minPlots;
        lvlObj.outerEdgeBuffer = 3;
        lvlObj.bridgeTurnChance = Mathf.CeilToInt(Mathf.Clamp(currentFD.exitCount, 1f, 5f) * 3f);

        lvlObj.maxSideHallsToRemove = Mathf.FloorToInt(currentFD.classRoomCount / 5);
        lvlObj.minSideHallsToRemove = Mathf.CeilToInt(currentFD.classRoomCount / 7);

        lvlObj.minPostPlotSpecialHalls = 0;
        lvlObj.maxPostPlotSpecialHalls = 0;
        lvlObj.minPrePlotSpecialHalls = currentFD.minSpecialHalls;
        lvlObj.maxPrePlotSpecialHalls = currentFD.maxSpecialHalls;
        lvlObj.potentialPostPlotSpecialHalls = new WeightedRoomAsset[0];
        lvlObj.potentialPrePlotSpecialHalls = genData.hallInsertions.ToArray();
        lvlObj.prePlotSpecialHallChance = 0.5f;

        lvlObj.maxHallsToRemove = Mathf.Min(currentFD.FloorID / 2, 6);
        lvlObj.minHallsToRemove = Mathf.Max(lvlObj.maxHallsToRemove - 3, 0);

        lvlObj.forcedStructures = lvlObj.forcedStructures.AddRangeToArray(genData.forcedObjectBuilders.ToArray());
        lvlObj.potentialStructures = lvlObj.potentialStructures.AddRangeToArray(genData.potentialObjectBuilders.ToArray());


        lvlObj.maxSpecialBuilders = Mathf.Min(Mathf.FloorToInt(currentFD.maxSize / 24f), lvlObj.potentialStructures.Length);
        lvlObj.minSpecialBuilders = Mathf.Min(Mathf.FloorToInt((currentFD.maxSize / 24f) / 1.5f), lvlObj.potentialStructures.Length);

        System.Random stableRng = new System.Random(CoreGameManager.Instance.Seed());
        stableRng.Next();
        var potentialSets = ENDPatches.CreateWeightedShuffledListWithCount<WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>, Tuple<Texture2D, Texture2D, Texture2D>>(EndlessForeverPlugin.setTextures, EndlessForeverPlugin.setTextures.Count, stableRng).ToArray();
        potentialSets = potentialSets.AddToArray(new Tuple<Texture2D, Texture2D, Texture2D>(lvlObj.hallWallTexs[0].selection, lvlObj.hallCeilingTexs[0].selection, lvlObj.hallFloorTexs[0].selection));
        stableRng.Next();
        var potentialFSets = ENDPatches.CreateWeightedShuffledListWithCount<WeightedSelection<Tuple<Texture2D, Texture2D, Texture2D>>, Tuple<Texture2D, Texture2D, Texture2D>>(EndlessForeverPlugin.setFacultyTextures, EndlessForeverPlugin.setFacultyTextures.Count, stableRng).ToArray();
        RoomGroup factulyIdontcare = lvlObj.roomGroup.First(x => x.name == "Faculty");
        if (factulyIdontcare.wallTexture.Length > 0 && factulyIdontcare.ceilingTexture.Length > 0 && factulyIdontcare.floorTexture.Length > 0)
            potentialFSets = potentialFSets.AddToArray(new Tuple<Texture2D, Texture2D, Texture2D>(factulyIdontcare.wallTexture[0].selection, factulyIdontcare.ceilingTexture[0].selection, factulyIdontcare.floorTexture[0].selection));
        int SetVal = rng.Next(0, potentialSets.Length);
        int SetFVal = rng.Next(0, potentialFSets.Length);
        System.Random purerng = new System.Random(CoreGameManager.Instance.Seed() + sceneObj.levelNo);
        if (EndlessForeverPlugin.Instance.forceSets.Value || purerng.NextDouble() > 0.5)
        {
            lvlObj.hallWallTexs = [new WeightedTexture2D() { weight = 100, selection = potentialSets[SetVal].Item1 }];
            lvlObj.hallFloorTexs = [new WeightedTexture2D() { weight = 100, selection = potentialSets[SetVal].Item3 }];
            lvlObj.hallCeilingTexs = [new WeightedTexture2D() { weight = 100, selection = potentialSets[SetVal].Item2 }];
        }
        else if (EndlessForeverPlugin.wallTextures.Count > 0 && EndlessForeverPlugin.floorTextures.Count > 0 && EndlessForeverPlugin.ceilTextures.Count > 0)
        {
            lvlObj.hallWallTexs = [.. lvlObj.hallWallTexs, .. EndlessForeverPlugin.wallTextures.ToArray()];
            lvlObj.hallFloorTexs = [.. lvlObj.hallFloorTexs, .. EndlessForeverPlugin.floorTextures.ToArray()];
            lvlObj.hallCeilingTexs = [.. lvlObj.hallCeilingTexs, .. EndlessForeverPlugin.ceilTextures.ToArray()];
        }

        // Structures that is part of main gameplay
        var structures = ((StructureWithParameters[])lvlObj.forcedStructures.Clone()).ToList();
        foreach (var structure in structures)
        {
            var oldman = structure.parameters;
            structure.parameters = new StructureParameters()
            {
                prefab = oldman.prefab,
                chance = oldman.chance,
                minMax = oldman.minMax,
            };
        }
        if (structures.Exists(struc => struc?.prefab is Structure_StudentSpawner))
            structures.Find(struc => struc?.prefab is Structure_StudentSpawner).parameters.minMax = [new IntVector2(rng.Next(lvlObj.exitCount - 1, Mathf.RoundToInt(sceneObj.levelNo / 4)), rng.Next(lvlObj.exitCount - 1, Mathf.RoundToInt(sceneObj.levelNo / 2)))];
        lvlObj.forcedStructures = structures.ToArray();

        stableRng = new System.Random(CoreGameManager.Instance.Seed());
        stableRng.Next();

        // The Lights
        Color warmColor = new Color(255f / 255f, 202 / 255f, 133 / 255f);
        Color coldColor = new Color(133f / 255f, 161f / 255f, 255f / 255f);

        float coldLight = Mathf.Max(Mathf.Sin(((currentFD.FloorID / (1f + (float)(rng.NextDouble() * 15f))) + stableRng.Next(-50, 50))), 0f);
        float warmLight = Mathf.Max(Mathf.Sin(((currentFD.FloorID / (1f + (float)(rng.NextDouble() * 15f))) + stableRng.Next(-50, 50))), 0f);

        lvlObj.standardLightColor = Color.Lerp(Color.Lerp(Color.white, coldColor, coldLight), warmColor, warmLight);

        if (currentFD.FloorID % 99 == 0)
            lvlObj.standardLightColor = Color.Lerp(lvlObj.standardLightColor, Color.red, 0.3f);

        float rgb = Mathf.Max(16f, 255f - (currentFD.FloorID * 5));
        lvlObj.standardDarkLevel = new Color(rgb / 255, rgb / 255, rgb / 255);
        lvlObj.standardLightStrength = Mathf.Max(Mathf.RoundToInt(4f / (currentFD.FloorID / 24f)), 3);
        lvlObj.maxLightDistance = rng.Next(6, Mathf.Clamp(Mathf.FloorToInt(currentFD.FloorID / 2), 6, 12));

        // Events
        lvlObj.randomEvents = genData.randomEvents;
        lvlObj.maxEvents = Mathf.RoundToInt(currentFD.classRoomCount / 2f);
        lvlObj.minEvents = Mathf.FloorToInt(currentFD.classRoomCount / 3f);

        lvlObj.maxEventGap = currentFD.classRoomCount <= 19 ? 130f : 120f;
        lvlObj.minEventGap = currentFD.classRoomCount >= 14 ? 30f : 60f;

        // Rooms
        List<string> possibleStores = ["Store", "UpgradeStation", "BountyStation"];
        possibleStores.ControlledShuffle(rng);
        string[] stores = new string[Mathf.Min(((currentFD.FloorID % 4) == 0) ? Mathf.CeilToInt(currentFD.FloorID / 20f) : 0, possibleStores.Count)];
        if (stores.Length == 1)
            possibleStores.Remove("UpgradeStation"); // Make it not appear as the only shop.
        for (int i = 0; i < stores.Length; i++)
            stores[i] = possibleStores[i];
        foreach (var store in stores)
        {
            lvlObj.roomGroup.Last(x => x.name == store).maxRooms = 1;
            lvlObj.roomGroup.Last(x => x.name == store).minRooms = 1;
        }
        foreach (var possible in possibleStores)
        {
            if (stores.Contains(possible)) continue;
            lvlObj.roomGroup.Last(x => x.name == possible).maxRooms = 0;
            lvlObj.roomGroup.Last(x => x.name == possible).minRooms = 0;
        }

        // Getting the base game room group are done before pre-load, let mods add in their rooms before doing something stupid;
        lvlObj.roomGroup = lvlObj.roomGroup.Where(room => EndlessForeverPlugin.basegameRooms.Contains(room.name) || possibleStores.Contains(room.name)).ToArray();
        foreach (var roomgrp in genData.moddedRooms)
            lvlObj.roomGroup = lvlObj.roomGroup.AddToArray(roomgrp);

        //lvlObj.hallWallTexs = EndlessFloorsPlugin.wallTextures.ToArray();
        //lvlObj.hallFloorTexs = EndlessFloorsPlugin.floorTextures.ToArray();
        //lvlObj.hallCeilingTexs = EndlessFloorsPlugin.ceilTextures.ToArray();

        List<WeightedRoomAsset> wra = ENDPatches.CreateShuffledListWithCount(genData.classRoomAssets, 3 + Mathf.FloorToInt(currentFD.FloorID / 3), rng);

        wra.Do((x) =>
        {
            if (x.selection.hasActivity)
            {
                x.weight = (int)Math.Ceiling(x.weight * (currentFD.FloorID * 0.1));
            }
        });

        RoomGroup classRoomGroup = lvlObj.roomGroup.First(x => x.name == "Class");
        classRoomGroup.potentialRooms = wra.ToArray();
        classRoomGroup.minRooms = currentFD.classRoomCount;
        classRoomGroup.maxRooms = currentFD.classRoomCount;
        if (EndlessForeverPlugin.Instance.forceSets.Value || purerng.NextDouble() > 0.5)
        {
            classRoomGroup.floorTexture = [new WeightedTexture2D() { weight = 100, selection = potentialFSets[SetFVal].Item3 }];
            classRoomGroup.wallTexture = [new WeightedTexture2D() { weight = 100, selection = potentialSets[SetVal].Item1 }];
            classRoomGroup.ceilingTexture = [new WeightedTexture2D() { weight = 100, selection = potentialSets[SetVal].Item2 }];
        }
        else if (EndlessForeverPlugin.wallTextures.Count > 0 && EndlessForeverPlugin.profFloorTextures.Count > 0 && EndlessForeverPlugin.ceilTextures.Count > 0)
        {
            classRoomGroup.floorTexture = EndlessForeverPlugin.profFloorTextures.ToArray();
            classRoomGroup.wallTexture = EndlessForeverPlugin.wallTextures.ToArray();
            classRoomGroup.ceilingTexture = EndlessForeverPlugin.ceilTextures.ToArray();
        }

        RoomGroup facultyRoomGroup = lvlObj.roomGroup.First(x => x.name == "Faculty");
        facultyRoomGroup.minRooms = currentFD.minFacultyRoomCount;
        facultyRoomGroup.maxRooms = currentFD.maxFacultyRoomCount;
        facultyRoomGroup.potentialRooms = ENDPatches.CreateShuffledListWithCount(genData.facultyRoomAssets, 4 + Mathf.FloorToInt(currentFD.FloorID / 4), rng).ToArray();
        facultyRoomGroup.stickToHallChance = currentFD.facultyStickToHall;
        RoomGroup lockedRooms = lvlObj.roomGroup.First(x => x.name == "LockedRoom");
        lockedRooms.minRooms = Mathf.Min(Mathf.CeilToInt(currentFD.minFacultyRoomCount / 6f) - 1, 6);
        lockedRooms.maxRooms = Mathf.Min(Mathf.CeilToInt(currentFD.maxFacultyRoomCount / 8f) - 1, 6);
        if (EndlessForeverPlugin.Instance.forceSets.Value || purerng.NextDouble() > 0.5)
        {
            facultyRoomGroup.floorTexture = [new WeightedTexture2D() { weight = 100, selection = potentialFSets[SetFVal].Item3 }];
            facultyRoomGroup.wallTexture = [new WeightedTexture2D() { weight = 100, selection = potentialFSets[SetFVal].Item1 }];
            facultyRoomGroup.ceilingTexture = [new WeightedTexture2D() { weight = 100, selection = potentialFSets[SetFVal].Item2 }];
        }
        else
        {
            facultyRoomGroup.floorTexture = EndlessForeverPlugin.profFloorTextures.ToArray();
            facultyRoomGroup.wallTexture = EndlessForeverPlugin.facultyWallTextures.ToArray();
            facultyRoomGroup.ceilingTexture = EndlessForeverPlugin.ceilTextures.ToArray();
        }
        lockedRooms.floorTexture = facultyRoomGroup.floorTexture;
        lockedRooms.wallTexture = facultyRoomGroup.wallTexture;
        lockedRooms.ceilingTexture = facultyRoomGroup.ceilingTexture;

        RoomGroup officeRoomGroup = lvlObj.roomGroup.First(x => x.name == "Office");
        officeRoomGroup.maxRooms = Mathf.Max(currentFD.maxOffices, 1);
        officeRoomGroup.minRooms = 1;
        officeRoomGroup.potentialRooms = genData.officeRoomAssets.ToArray();
        if (EndlessForeverPlugin.Instance.forceSets.Value || purerng.NextDouble() > 0.5)
        {
            officeRoomGroup.floorTexture = [new WeightedTexture2D() { weight = 100, selection = potentialFSets[SetFVal].Item3 }];
            officeRoomGroup.wallTexture = [new WeightedTexture2D() { weight = 100, selection = potentialFSets[SetFVal].Item1 }];
            officeRoomGroup.ceilingTexture = [new WeightedTexture2D() { weight = 100, selection = potentialFSets[SetFVal].Item2 }];
        }
        else
        {
            officeRoomGroup.floorTexture = EndlessForeverPlugin.profFloorTextures.ToArray();
            officeRoomGroup.wallTexture = EndlessForeverPlugin.facultyWallTextures.ToArray();
            officeRoomGroup.ceilingTexture = EndlessForeverPlugin.ceilTextures.ToArray();
        }
        stableRng = new System.Random(CoreGameManager.Instance.Seed());
        stableRng.Next();
        stableRng.Next();
        stableRng.Next();

        lvlObj.potentialSpecialRooms = genData.specialRoomAssets.ToArray();
        if (lvlObj.potentialSpecialRooms.Length > 0)
        {
            lvlObj.minSpecialRooms = currentFD.minGiantRooms;
            lvlObj.maxSpecialRooms = currentFD.maxGiantRooms;
        }
        else
        {
            lvlObj.minSpecialRooms = 0;
            lvlObj.maxSpecialRooms = 0;
        }

        lvlObj.specialRoomsStickToEdge = ((currentFD.FloorID < 22) || (currentFD.FloorID % 24 == 0));

        // NPCs
        sceneObj.forcedNpcs = [];
        sceneObj.potentialNPCs = [];
        sceneObj.additionalNPCs = 0;
        int potNpcs = Mathf.Max(Mathf.Min(currentFD.npcCountUnclamped, genData.npcs.Count), 1);
        stableRng = new System.Random(CoreGameManager.Instance.Seed());
        stableRng.Next();
        sceneObj.forcedNpcs = ENDPatches.CreateWeightedShuffledListWithCount<WeightedNPC, NPC>(genData.npcs, potNpcs, stableRng).ToArray();
        sceneObj.forcedNpcs = sceneObj.forcedNpcs.AddRangeToArray<NPC>(genData.forcedNpcs.ToArray());
        stableRng = new System.Random(CoreGameManager.Instance.Seed());
        stableRng.Next();
        stableRng.Next();

        //int myFloorBaldi = currentFD.classRoomCount > 8 ? 3 : currentFD.classRoomCount > 5 ? 2 : 1;
        Baldi myBladi = (Baldi)MTM101BaldiDevAPI.npcMetadata.Get(Character.Baldi).prefabs["Baldi_Main" + 3]; //myFloorBaldi];

        lvlObj.potentialBaldis = [
            new WeightedNPC()
            {
                weight = 420,
                selection = myBladi
            }
            ];

        // I can call myself uncool, but ehh...
        lvlObj.timeBonusVal = 15 * currentFD.FloorID;
        lvlObj.timeBonusLimit = 90f * Mathf.Ceil(currentFD.maxSize / 24f);
        lvlObj.timeLimit = (180f * Mathf.Ceil(currentFD.maxSize / 24f)) + (60f * Mathf.Ceil(currentFD.classRoomCount / 3f));

        if (currentFD.FloorID % 99 == 0) // Oh no...
            lvlObj.timeLimit = float.PositiveInfinity;
    }

    public override void Initialize()
    {
        levelNo = EndlessForeverPlugin.Instance.gameSave.currentFloor;
        if (CoreGameManager.Instance.lifeMode != LifeMode.Explorer && levelNo % 99 == 0)
        {
            AccessTools.Field(typeof(MainGameManager), "allNotebooksNotification").SetValue(this, F99AllNotebooks);
            var timeout = FindObjectOfType<TimeOut>();
            timeout.ReflectionSetVariable("baldiAngerRate", 9f);
            timeout.ReflectionSetVariable("baldiAngerAmount", 0.9f);
        }
        base.Initialize();
        notebookAngerVal = 9f / NotebookTotal;
    }

    public override void CollectNotebooks(int count)
    {
        notebookAngerVal = 9f / NotebookTotal;
        base.CollectNotebooks(count);
    }

    internal static void UpdateData()
    {
        EndlessForeverPlugin.Instance.inflevel.levelNo = EndlessForeverPlugin.Instance.gameSave.currentFloor - 1;
        EndlessForeverPlugin.Instance.inflevel.levelTitle = "F" + EndlessForeverPlugin.Instance.gameSave.currentFloor;
        EndlessForeverPlugin.Instance.inflevel.mapPrice = (EndlessForeverPlugin.Instance.gameSave.currentFloor * 25) * Mathf.CeilToInt(EndlessForeverPlugin.Instance.gameSave.currentFloor / 8f);
    }

    public override void LoadNextLevel()
    {
        EndlessForeverPlugin.Instance.gameSave.currentFloor++;
        if (EndlessForeverPlugin.Instance.gameSave.currentFloor > EndlessForeverPlugin.Instance.currentData.Item1)
        {
            if (CoreGameManager.Instance.lifeMode == LifeMode.Explorer)
                CoreGameManager.Instance.Quit();
            else
                EndlessForeverPlugin.Instance.SaveData();
        }
        CoreGameManager.Instance.levelMapHasBeenPurchasedFor = null;
        if ((EndlessForeverPlugin.Instance.gameSave.currentFloor % 8) == 0)
            CoreGameManager.Instance.tripAvailable = true;
        base.LoadNextLevel();
    }

    protected override void AllNotebooks()
    {
        if (!allNotebooksFound && CoreGameManager.Instance.lifeMode != LifeMode.Explorer && levelNo % 99 == 0)
        {
            var outage = FindObjectOfType<TimeOut>();
            AudioManager audMan = AccessTools.DeclaredField(typeof(EnvironmentController), "audMan").GetValue(ec) as AudioManager;
            audMan.PlaySingle(outage.EventJingleOverride);
            IEnumerator Doomsday()
            {
                float timer = 3f;
                while (timer > 0f)
                {
                    timer -= Time.deltaTime * ec.EnvironmentTimeScale;
                    yield return null;
                }
                outage.Begin();
            }
            ec.StartCoroutine(Doomsday());
        }
        base.AllNotebooks();
    }
}
