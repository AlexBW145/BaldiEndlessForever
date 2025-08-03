using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.Components;
using MTM101BaldAPI.Registers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace EndlessFloorsForever.Components
{
    public class UpgradeWarehouseRoomFunction : RoomFunction
    {
        [SerializeField]
        internal Transform roomBase;
        
        [SerializeField]
        internal new PriceTag[] tag = new PriceTag[0];
        private List<Pickup> pickups = new List<Pickup>();
        internal List<StandardUpgrade> upgrades = new List<StandardUpgrade>();
        private float minSaleDiscount = 0.5f;
        private float maxSaleDiscount = 0.9f;

        [SerializeField]
        private float saleChance = 0.05f;

        [Range(0f, 127f)]
        public int alarmNoiseValue = 127;

        [SerializeField]
        private int restockPrice = 250;

        [SerializeField]
        private float baldiStealAnger = 2.5f;

        [SerializeField]
        private int aidLimit = 100;

        private int notebooksAtLastReset;

        private int totalCustomers;

        private bool playerEntered;

        private bool playerLeft;

        private bool itemPurchased;

        private bool inGameMode;

        private bool open;

        private bool thief;

        private bool alarmStarted;

        [SerializeField]
        internal SoundObject audBell;
        [SerializeField]
        internal SoundObject audAlarm;
        [SerializeField]
        internal AudioManager alarmAudioManager;
        [SerializeField]
        internal TextMeshPro mapPriceText;

        [SerializeField]
        internal CustomSpriteAnimator animator;
        [SerializeField]
        internal Transform johnnyHotspot;

        private ItemObject upgradeObject => EndlessForeverPlugin.arcadeAssets["Store/UpgradeObject"] as ItemObject;

        public override void Initialize(RoomController room)
        {
            base.Initialize(room);
            johnnyHotspot.GetComponent<ItemAcceptor>().OnInsert = new UnityEvent();
            johnnyHotspot.GetComponent<ItemAcceptor>().OnInsert.AddListener(GivenBusPass);
            roomBase.SetParent(room.objectObject.transform);
            inGameMode = CoreGameManager.Instance.sceneObject.levelTitle == "PIT";

            for (int i = 0; i < room.itemSpawnPoints.Count; i++)
            {
                ItemSpawnPoint itemSpawnPoint = room.itemSpawnPoints[i];
                Pickup pickup = room.ec.CreateItem(room, CoreGameManager.Instance.NoneItem, itemSpawnPoint.position);
                pickup.OnItemPurchased += ItemPurchased;
                pickup.OnItemDenied += ItemDenied;
                pickup.OnItemCollected += ItemCollected;
                pickup.showDescription = true;
                pickups.Add(pickup);
            }

            Restock();

            room.itemSpawnPoints.Clear();
            var button = roomBase.gameObject.GetComponentsInChildren<GameButton>()[0];
            var uevent = new UnityEvent();
            uevent.AddListener(PurchaseRestock);
            AccessTools.DeclaredField(typeof(GameButton), "OnPress").SetValue(button.GetComponent<GameButton>(), uevent);
            button = roomBase.gameObject.GetComponentsInChildren<GameButton>()[1];
            var uevent2 = new UnityEvent();
            uevent2.AddListener(PurchaseMap);
            AccessTools.DeclaredField(typeof(GameButton), "OnPress").SetValue(button.GetComponent<GameButton>(), uevent2);
        }

        void Update()
        {
            if (room.ec.timeOut && totalCustomers <= 0 && open)
                Close();
        }

        private void Restock()
        {
            upgrades.Clear();
            PopulateShop();
            for (int i = 0; i < pickups.Count; i++)
            {
                Pickup pickup = pickups[i];
                pickup.gameObject.GetOrAddComponent<UpgradePickupMarker>().upgrade = upgrades[i];
                pickup.AssignItem(upgradeObject);

                pickup.gameObject.GetComponent<UpgradePickupMarker>().discounted = UnityEngine.Random.value < saleChance;
                pickup.gameObject.GetComponent<UpgradePickupMarker>().discountRange = UnityEngine.Random.Range(minSaleDiscount, maxSaleDiscount);
                pickup.showDescription = true;
                pickup.free = false;
                pickup.gameObject.SetActive(true);
            }
            UpdateAllTags();
        }

        public void PopulateShop()
        {
            try
            {
                List<WeightedSelection<StandardUpgrade>> upgradesTemp = new List<WeightedSelection<StandardUpgrade>>();
                EndlessForeverPlugin.Upgrades.Do(x =>
                {
                    if (x.Key == "none") return;
                    if (x.Value.weight == 0) return;
                    if (!x.Value.ShouldAppear(EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(x.Value.id))) return;
                    upgradesTemp.Add(new WeightedSelection<StandardUpgrade>()
                    {
                        selection = x.Value,
                        weight = x.Value.weight
                    });
                });
                WeightedSelection<StandardUpgrade>[] weightedUpgrades = upgradesTemp.ToArray();
                upgradesTemp = null;
                for (int i = 0; i < pickups.Count; i++)
                {
                    if (weightedUpgrades.Length == 0)
                    {
                        upgrades.Add(EndlessForeverPlugin.Upgrades["error"]);
                        continue;
                    }
                    StandardUpgrade gu = WeightedSelection<StandardUpgrade>.RandomSelection(weightedUpgrades);
                    upgrades.Add(gu);
                    if (!gu.ShouldAppear(EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(gu.id) + upgrades.Where(x => x == gu).Count()))
                        weightedUpgrades = weightedUpgrades.Where(x => x.selection.id != gu.id).ToArray();
                }
            }
            catch (Exception ex)
            {
                MTM101BaldiDevAPI.CauseCrash(EndlessForeverPlugin.Instance.Info, ex);
            }
        }

        public static StandardUpgrade GetRandomValidUpgrade(int seed)
        {
            List<WeightedSelection<StandardUpgrade>> upgradesTemp = new List<WeightedSelection<StandardUpgrade>>();
            EndlessForeverPlugin.Upgrades.Do(x =>
            {
                if (x.Key == "none") return;
                if (x.Value.weight == 0) return;
                if (!x.Value.ShouldAppear(EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(x.Value.id))) return;
                upgradesTemp.Add(new WeightedSelection<StandardUpgrade>()
                {
                    selection = x.Value,
                    weight = x.Value.weight
                });
            });
            WeightedSelection<StandardUpgrade>[] weightedUpgrades = upgradesTemp.ToArray();
            if (weightedUpgrades.Length == 0) return null;
            return WeightedSelection<StandardUpgrade>.ControlledRandomSelection(weightedUpgrades, new System.Random(seed));
        }

        public void UpdateAllTags()
        {
            for (int i = 0; i < pickups.Count; i++)
            {
                Pickup pickup = pickups[i];
                if (pickup.item.itemType == Items.None || upgrades[i].id == "error")
                {
                    pickup.gameObject.SetActive(false);
                    tag[i].SetText(LocalizationManager.Instance.GetLocalizedText(upgrades[i].id == "error" ? "TAG_Out" : "TAG_Sold"));
                    continue;
                }
                int originalPrice = upgrades[i].GetCost(EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(upgrades[i].id));
                int finalPrice = originalPrice;
                if (pickup.gameObject.GetComponent<UpgradePickupMarker>().discounted)
                {
                    float discount = pickup.gameObject.GetComponent<UpgradePickupMarker>().discountRange; // Why update it every time WHEN IT CAN REDO THE SALE ITSELF!
                    float discount2 = finalPrice * discount;
                    finalPrice = Mathf.RoundToInt(discount2 - discount2 % 10f);
                    tag[i].SetSale(originalPrice, finalPrice);
                }
                else
                    tag[i].SetText(finalPrice.ToString());
                pickup.price = finalPrice;
                pickup.free = false;
                pickup.gameObject.SetActive(true);
            }
        }
        
        public override void OnGenerationFinished()
        {
            base.OnGenerationFinished();
            if (inGameMode && !EndlessForeverPlugin.Instance.gameSave.IsInfGamemode)
                Close();
            else
                Open();
            if (inGameMode)
            {
                foreach (var door in room.GetPotentialDoorCells().FindAll(door => door.Tile.gameObject.GetComponentInChildren<Door>() != null))
                {
                    Door dor = door.Tile.gameObject.GetComponentInChildren<Door>();
                    dor.aTile.SetShape(0, TileShapeMask.Open);
                    dor.bTile.SetShape(0, TileShapeMask.Open);
                }
                mapPriceText.text = CoreGameManager.Instance.nextLevel.mapPrice.ToString();
            }
            else
            {
                mapPriceText.text = "";
                roomBase.gameObject.GetComponentsInChildren<GameButton>()[1].gameObject.SetActive(false);
            }
        }

        public override void OnPlayerEnter(PlayerManager player)
        {
            base.OnPlayerEnter(player);
            totalCustomers++;
            CoreGameManager.Instance.GetHud(player.playerNumber).PointsAnimator.ShowDisplay(val: true);
            if (open)
            {
                if (!playerEntered)
                    playerEntered = true;

                johnnyHotspot.gameObject.SetActive(!CoreGameManager.Instance.tripPlayed && CoreGameManager.Instance.tripAvailable && inGameMode);
            }
            else
            {
                foreach (Door door in room.doors)
                    door.Unlock();
            }
        }

        public override void OnNpcEnter(NPC npc)
        {
            base.OnNpcEnter(npc);
            totalCustomers++;
            if (open)
            {
                return;
            }

            foreach (Door door in room.doors)
            {
                door.Unlock();
            }
        }

        public override void OnPlayerStay(PlayerManager player)
        {
            base.OnPlayerStay(player);
            if (!open)
            {
                player.RuleBreak("Faculty", 1f, 0.25f);
            }
        }

        public override void OnPlayerExit(PlayerManager player)
        {
            base.OnPlayerExit(player);
            totalCustomers--;
            CoreGameManager.Instance.GetHud(player.playerNumber).PointsAnimator.ShowDisplay(val: false);
            if (open)
            {
                if (!playerLeft)
                {
                    playerLeft = true;
                }

                return;
            }

            player.RuleBreak("Faculty", 1f);
            if (totalCustomers > 0)
                return;

            totalCustomers = 0;
            foreach (Door door in room.doors)
            {
                door.Lock(true);
            }
        }

        public override void OnNpcExit(NPC npc)
        {
            base.OnNpcExit(npc);
            totalCustomers--;
            if (open || totalCustomers > 0)
            {
                return;
            }

            totalCustomers = 0;
            foreach (Door door in room.doors)
            {
                door.Lock(cancelTimer: true);
            }
        }

        private void ItemPurchased(Pickup pickup, int player)
        {
            if (open)
            {
                itemPurchased = true;
                playerLeft = false;
            }
        }

        private void ItemCollected(Pickup pickup, int player)
        {
            MarkItemAsSold(pickup);
            pickup.price = 0;
            pickup.showDescription = false;

            if (!open)
            {
                thief = true;
                CoreGameManager.Instance.johnnyHelped = true;
                BaseGameManager.Instance.AngerBaldi(baldiStealAnger);
                if (!alarmStarted)
                    SetOffAlarm();
            }
            UpdateAllTags();
        }

        private void ItemDenied(Pickup pickup, int player)
        {
            if (open)
            {
                if (pickup != null && !EndlessForeverPlugin.Instance.gameSave.upgradeStoreHelped && pickup.price - CoreGameManager.Instance.GetPoints(0) <= aidLimit)
                {
                    pickup.Collect(player);
                    EndlessForeverPlugin.Instance.gameSave.upgradeStoreHelped = true;
                    CoreGameManager.Instance.AddPoints(-CoreGameManager.Instance.GetPoints(player), player, playAnimation: true, false);
                    itemPurchased = true;
                    playerLeft = false;
                }
            }
        }

        internal void PurchaseRestock()
        {
            if (!open) return;
            if (CoreGameManager.Instance.GetPoints(0) >= restockPrice)
            {
                Restock();
                CoreGameManager.Instance.AddPoints(-restockPrice, 0, true, false);
                CoreGameManager.Instance.audMan.PlaySingle(audBell);

                itemPurchased = true;
                playerLeft = false;
            }
            else
            {
                ItemDenied(null, 0);
            }
        }

        internal void PurchaseMap()
        {
            if (CoreGameManager.Instance.levelMapHasBeenPurchasedFor == CoreGameManager.Instance.nextLevel || CoreGameManager.Instance.saveMapPurchased || !inGameMode || !open) return;
            bool purchased = false;
            if (CoreGameManager.Instance.GetPoints(0) >= CoreGameManager.Instance.nextLevel.mapPrice)
            {
                purchased = true;
                CoreGameManager.Instance.AddPoints(-CoreGameManager.Instance.nextLevel.mapPrice, 0, true, false);
            }
            else if (!CoreGameManager.Instance.johnnyHelped && CoreGameManager.Instance.nextLevel.mapPrice >= 1000 && CoreGameManager.Instance.nextLevel.mapPrice - CoreGameManager.Instance.GetPoints(0) <= 100)
            {
                CoreGameManager.Instance.johnnyHelped = true;
                purchased = true;
                CoreGameManager.Instance.AddPoints(-CoreGameManager.Instance.GetPoints(0), 0, true, false);
            }

            if (purchased)
            {
                CoreGameManager.Instance.levelMapHasBeenPurchasedFor = CoreGameManager.Instance.nextLevel;
                CoreGameManager.Instance.saveMapPurchased = true;
                
                CoreGameManager.Instance.audMan.PlaySingle(audBell);
                itemPurchased = true;
                playerLeft = false;
            }
            else
                ItemDenied(null, 0);
        }

        internal void GivenBusPass()
        {
            StartCoroutine(BusPassSequencer());
            itemPurchased = true;
            johnnyHotspot.gameObject.SetActive(value: false);
        }

        private IEnumerator BusPassSequencer()
        {
            yield return null;
            yield return new WaitForSecondsEnvironmentTimescale(room.ec, 1.7f);

            BaseGameManager.Instance.CallSpecialManagerFunction(3, gameObject);
        }

        private void MarkItemAsSold(Pickup pickup)
        {
            if (!open)
                return;

            if (pickup.price > 0)
                CoreGameManager.Instance.audMan.PlaySingle(audBell);

            for (int i = 0; i < pickups.Count; i++)
                if (pickups[i] == pickup)
                    tag[i].SetText(LocalizationManager.Instance.GetLocalizedText("TAG_Sold"));
        }

        private void SetOffAlarm()
        {
            alarmStarted = true;
            room.ec.MakeNoise(room.ec.RealRoomMid(room), alarmNoiseValue);
            alarmAudioManager.QueueAudio(audAlarm);
            alarmAudioManager.SetLoop(true);
            foreach (Cell cell in room.cells)
            {
                if (cell.hasLight)
                {
                    cell.lightColor = Color.red;
                    cell.SetLight(on: true);
                }
            }
        }

        private void Open()
        {
            open = true;
            foreach (Door door in room.doors)
                door.Unlock();

            foreach (Cell cell in room.cells)
                if (cell.hasLight)
                    cell.SetLight(on: true);

            UpdateAllTags();
        }

        private void Close()
        {
            open = false;
            foreach (Door door in room.doors)
                door.Lock(true);

            foreach (Cell cell in room.cells)
                if (cell.hasLight)
                    cell.SetLight(on: false);

            foreach (Pickup pickup in pickups)
            {
                pickup.AssignItem(CoreGameManager.Instance.NoneItem);
                pickup.free = true;
                pickup.gameObject.SetActive(false);
            }
            for (int i = 0; i < tag.Count(); i++)
                tag[i].SetText(LocalizationManager.Instance.GetLocalizedText("TAG_Out"));
            if (inGameMode)
                foreach (var door in room.GetPotentialDoorCells().FindAll(door => door.Tile.gameObject.GetComponentInChildren<Door>() != null))
                    door.Tile.gameObject.GetComponentInChildren<Door>().Lock(true);
        }
    }

    public class UpgradePickupMarker : MonoBehaviour
    {
        public StandardUpgrade upgrade;
        internal bool discounted;
        internal float discountRange;

        public static void UpdateAllUpgrades()
        {
            UpgradePickupMarker[] markers = GameObject.FindObjectsOfType<UpgradePickupMarker>();
            for (int i = 0; i < markers.Length; i++)
            {
                Pickup pickup = markers[i].gameObject.GetComponent<Pickup>();
                pickup.AssignItem(pickup.item); //refresh
            }
            if (BaseGameManager.Instance.Ec.rooms.Exists(store => store.functionObject.GetComponent<UpgradeWarehouseRoomFunction>() != null))
                BaseGameManager.Instance.Ec.rooms.Find(store => store.functionObject.GetComponent<UpgradeWarehouseRoomFunction>() != null).functionObject.GetComponent<UpgradeWarehouseRoomFunction>().UpdateAllTags();
        }
    }
}
