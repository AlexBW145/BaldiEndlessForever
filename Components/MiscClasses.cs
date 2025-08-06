using MTM101BaldAPI.SaveSystem;
using MTM101BaldAPI;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using System.Runtime.InteropServices.ComTypes;
using System.Linq;
using UnityEngine.TextCore;
using MTM101BaldAPI.Reflection;

namespace EndlessFloorsForever.Components
{
    public class FloorPick : MonoBehaviour
    {
        internal int floorNum = 1;
        private TMP_Text text;
        private TextLocalizer localizer;
        public static SoundObject sliding, slideDone;
        private bool Sliding;
        private float delayedInput, delayedSpeed;
        private AudioManager audMan;
        private AnalogInputData movementAnalogData;
        private Vector2 _absoluteVector, _deltaVector;

        void Awake()
        {
            text = GetComponent<TMP_Text>();
            localizer = GetComponent<TextLocalizer>();
            if (!ModdedFileManager.Instance.saveData.saveAvailable)
            {
                EndlessForeverPlugin.Instance.gameSave.currentFloor = floorNum;
                EndlessForeverPlugin.Instance.gameSave.startingFloor = floorNum;
                InfGameManager.UpdateData();
            }
            else
                floorNum = EndlessForeverPlugin.Instance.gameSave.currentFloor;
            UpdateText();
        }

        private void Start()
        {
            audMan = MusicManager.Instance.ReflectionGetVariable("soundSource") as AudioManager;
            movementAnalogData = Resources.FindObjectsOfTypeAll<PlayerMovement>().Last().movementAnalogData;
        }

        private void UpdateText()
        {
            localizer.GetLocalizedText("Men_Floor");
            text.text += floorNum.ToString(); // Pointing this out that it's not my code...
        }

        internal void StartSliding()
        {
            delayedInput = 0.25f;
            Sliding = !Sliding;
            if (!Sliding)
            {
                InputManager.Instance.ActivateActionSet("Interface");
                CursorController.Instance.Hide(false);
                audMan.pitchModifier = 1f;
                MusicManager.Instance.PlaySoundEffect(slideDone);
                return;
            }
            CursorController.Instance.Hide(true);
            InputManager.Instance.ActivateActionSet("InGame");
        }

        private void Update()
        {
            if (Sliding)
            {
                audMan.pitchModifier = 1f + (float)((float)floorNum / (float)EndlessForeverPlugin.Instance.currentData.Item1); // AudioSource pitch value can go up to 3, which this only goes up to 2.
                if (movementAnalogData != null && delayedInput <= 0f)
                {
                    InputManager.Instance.GetAnalogInput(movementAnalogData, out _absoluteVector, out _deltaVector, 0.1f);
                    if (_absoluteVector.x == 0f)
                        delayedSpeed = 0.25f;
                    else
                        delayedSpeed -= Time.deltaTime;

                    if (_absoluteVector.x > 0.25f)
                        Increment(1);
                    else if (_absoluteVector.x < -0.25f)
                        Increment(-1);
                }
                if (delayedInput <= 0f && (InputManager.Instance.GetDigitalInput("UseItem", true) || InputManager.Instance.GetDigitalInput("Interact", true)))
                    StartSliding();
            }
            if (delayedInput > 0f)
                delayedInput -= Time.deltaTime;
        }

        public void Increment(int amount = 1)
        {
            delayedInput = Mathf.Max(0.05f, delayedSpeed);
            floorNum += amount;
            if (floorNum > EndlessForeverPlugin.Instance.currentData.Item1)
                floorNum = 1;
            else if (floorNum < 1)
                floorNum = EndlessForeverPlugin.Instance.currentData.Item1;
            if (!ModdedFileManager.Instance.saveData.saveAvailable)
            {
                EndlessForeverPlugin.Instance.gameSave.currentFloor = floorNum;
                EndlessForeverPlugin.Instance.gameSave.startingFloor = floorNum;
                InfGameManager.UpdateData();
            }
            MusicManager.Instance.PlaySoundEffect(sliding);
            UpdateText();
        }

        private void OnDestroy()
        {
            if (Sliding)
            {
                Sliding = false;
                InputManager.Instance.ActivateActionSet("Interface");
                CursorController.Instance?.Hide(false);
                audMan.pitchModifier = 1f;
            }
        }
    }

    [Serializable]
    public class FloorData
    {
        public int FloorID = 1;

        public static int GetYTPsAtFloor(int floor)
        {
            FloorData tempD = new FloorData();
            int YTPs = 0;
            tempD.FloorID = 1;
            for (int i = 0; i < floor; i++)
            {
                YTPs += (Mathf.Min(tempD.classRoomCount, 50) * 10);
                YTPs += Mathf.FloorToInt(tempD.unclampedScaleVar * 2);
                tempD.FloorID++;
            }
            YTPs *= 2;
            return YTPs;
        }

        public int CapIncrease => Mathf.FloorToInt(FloorID / 99) * 5;

        public float scaleVar
        {
            get
            {
                int mainV = Mathf.Min(FloorID, 42 + CapIncrease);
                return mainV * (mainV / 8f);
            }
        }

        public float unclampedScaleVar => FloorID * (FloorID / 8f);

        public float facultyStickToHall => Mathf.Clamp(unclampedScaleVar / 16f, 0.05f, 0.95f);

        public int maxItemValue => Mathf.FloorToInt(maxFacultyRoomCount * maxFacultyRoomCount) + (25 * Mathf.CeilToInt(FloorID / 2f));

        public int classRoomCount => Mathf.CeilToInt(Mathf.Max(Mathf.Sqrt(unclampedScaleVar * 3f), 3f));

        public int maxPlots => Mathf.Clamp(classRoomCount / 2, 4, 12);

        public int npcCountUnclamped => Mathf.FloorToInt((FloorID / 3f) - 1);
        public int minPlots => Mathf.Max(Mathf.CeilToInt(maxPlots * 0.7f), 4);

        public int maxGiantRooms => Mathf.Max(Mathf.FloorToInt(maxSize / 30f), minSize >= 37 ? 1 : 0);

        public int maxOffices => Mathf.RoundToInt(maxSize / 22f);

        public int minGiantRooms => Mathf.Max(maxGiantRooms - 1, maxSize > 43 ? 1 : 0);

        public int maxSpecialHalls => Mathf.FloorToInt(maxSize / 28f);

        public int minSpecialHalls => Mathf.FloorToInt(maxSpecialHalls / 3f);

        public float itemChance => (scaleVar / 3f) + 2f;

        public int maxFacultyRoomCount
        {
            get
            {
                float subCount = Mathf.Clamp(32 - FloorID, 24f, 32f);
                return Mathf.Max(Mathf.CeilToInt((maxSize * 1.2f) - subCount), 3);
            }
        }

        public int minFacultyRoomCount => Mathf.CeilToInt(maxFacultyRoomCount * 0.88f);

        public int exitCount => (FloorID % 99 == 0) ? 16 : Mathf.Clamp(Mathf.CeilToInt(FloorID / 9) + 1, 1, 8);

        public int maxSize
        {
            get
            {
                float addend = Mathf.Clamp(FloorID * 8f, 16f, 24f);
                return Mathf.Max(Mathf.Min(256, Mathf.CeilToInt((scaleVar / 7f) + addend)), 19);
            }
        }

        public int minSize => Mathf.Clamp(Mathf.CeilToInt(maxSize / 1.24f), 19, maxSize);

        public LevelObject myLevelType
        {
            get
            {
                if (FloorID <= 4)
                    return EndlessForeverPlugin.Instance.inflevel.randomizedLevelObject.First(x => x.selection.type == LevelType.Schoolhouse).selection;
                System.Random cRNG = new System.Random(CoreGameManager.Instance.Seed() + (FloorID - 1));
                List<WeightedLevelObject> list = new List<WeightedLevelObject>(EndlessForeverPlugin.Instance.inflevel.randomizedLevelObject);

                list.ControlledShuffle(cRNG);
                return WeightedSelection<LevelObject>.ControlledRandomSelectionList(WeightedLevelObject.Convert(list), cRNG);
            }
        }
    }

    public class GeneratorData
    {
        public List<WeightedRandomEvent> randomEvents = new List<WeightedRandomEvent>();
        public List<WeightedNPC> npcs = new List<WeightedNPC>();
        public List<NPC> forcedNpcs = new List<NPC>();
        public List<WeightedItemObject> items = new List<WeightedItemObject>();
        public List<WeightedStructureWithParameters> objectBuilders = new List<WeightedStructureWithParameters>();
        public List<StructureWithParameters> forcedObjectBuilders = new List<StructureWithParameters>();
        public List<WeightedStructureWithParameters> potentialObjectBuilders = new List<WeightedStructureWithParameters>();
        public Dictionary<RoomCategory, List<WeightedRoomAsset>> roomAssets = new Dictionary<RoomCategory, List<WeightedRoomAsset>>();
        public HashSet<RoomGroup> moddedRooms = new HashSet<RoomGroup>();
        public List<WeightedRoomAsset> classRoomAssets => roomAssets[RoomCategory.Class];
        public List<WeightedRoomAsset> facultyRoomAssets => roomAssets[RoomCategory.Faculty];
        public List<WeightedRoomAsset> specialRoomAssets => roomAssets[RoomCategory.Special];
        public List<WeightedRoomAsset> officeRoomAssets => roomAssets[RoomCategory.Office];
        public List<WeightedRoomAsset> hallInsertions => roomAssets[RoomCategory.Hall];

        public CustomLevelGenerationParameters lvlObj { get; private set; }

        public GeneratorData(CustomLevelGenerationParameters lvlObj)
        {
            this.lvlObj = lvlObj;
            foreach (RoomCategory cat in EnumExtensions.GetValues<RoomCategory>())
                roomAssets.Add(cat, new List<WeightedRoomAsset>());
        }

        internal GeneratorData()
        {
            foreach (RoomCategory cat in EnumExtensions.GetValues<RoomCategory>())
                roomAssets.Add(cat, new List<WeightedRoomAsset>());
        }
    }
}
