using MTM101BaldAPI;
using MTM101BaldAPI.Components;
using MTM101BaldAPI.Registers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EndlessFloorsForever.Components
{
    public class BountyhouseRoomFunction : RoomFunction
    {
        [SerializeField]
        internal Transform roomBase;

        [SerializeField]
        internal new PriceTag[] tag = new PriceTag[0];
        [SerializeField]
        internal SpriteRenderer[] itemframe = new SpriteRenderer[0];
        internal List<Pickup> pickups = new List<Pickup>();

        [SerializeField]
        private float boostChance = 0.1f;
        private float minBoostDiscount = 0.7f;
        private float maxBoostDiscount = 0.9f;

        [Range(0f, 127f)]
        public int alarmNoiseValue = 127;

        [SerializeField]
        private float baldiStealAnger = 2.5f;

        private int notebooksAtLastReset;

        [SerializeField]
        private int notebooksPerReset = 5;

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
        static internal SoundObject audRing;

        [SerializeField]
        internal CustomSpriteAnimator animator;

        private ItemObject bountyObject => EndlessForeverPlugin.arcadeAssets["Store/YTPBountyObject"] as ItemObject;

        public override void Initialize(RoomController room)
        {
            base.Initialize(room);
            roomBase.SetParent(room.objectObject.transform);
            inGameMode = CoreGameManager.Instance.sceneObject.levelTitle == "PIT";

            for (int i = 0; i < room.itemSpawnPoints.Count; i++)
            {
                ItemSpawnPoint itemSpawnPoint = room.itemSpawnPoints[i];
                Pickup pickup = room.ec.CreateItem(room, CoreGameManager.Instance.NoneItem, itemSpawnPoint.position);
                pickup.OnItemPurchased += ItemPurchased;
                pickup.OnItemDenied += ItemDenied;
                pickup.OnItemCollected += ItemCollected;
                pickup.showDescription = false;
                pickups.Add(pickup);
            }

            Restock();
            room.itemSpawnPoints.Clear();
        }

        void Update()
        {
            if (BaseGameManager.Instance.FoundNotebooks >= BaseGameManager.Instance.NotebookTotal - Mathf.CeilToInt(BaseGameManager.Instance.NotebookTotal / 7) + 1 && totalCustomers <= 0 && open)
                Close();
            if (BaseGameManager.Instance.FoundNotebooks - notebooksAtLastReset >= notebooksPerReset)
            {
                notebooksAtLastReset = BaseGameManager.Instance.FoundNotebooks;
                if (open)
                    Restock();
            }
        }

        private void Restock()
        {
            for (int i = 0; i < pickups.Count; i++)
            {
                Pickup pickup = pickups[i];
                var bounty = pickup.gameObject.GetOrAddComponent<BountyPickup>();
                pickup.AssignItem(bountyObject);

                pickup.price = Mathf.RoundToInt(bounty.item.price / 1.5f);
                bounty.value = pickup.price;
                bounty.boosted = UnityEngine.Random.value < boostChance;
                itemframe[i].sprite = bounty.item.itemSpriteLarge;
                int originalPrice = bounty.value;
                int finalPrice = originalPrice;
                if (bounty.boosted)
                {
                    float discount = UnityEngine.Random.Range(minBoostDiscount, maxBoostDiscount);
                    float discount2 = finalPrice * discount;
                    finalPrice = Mathf.RoundToInt(discount2 + discount2 * 2f);
                    tag[i].SetText(string.Format("{0}\n <s> {1}  </s> {2}", LocalizationManager.Instance.GetLocalizedText("TAG_Boosted"), originalPrice.ToString(), finalPrice.ToString())); // No prefix patch??
                }
                else
                    tag[i].SetText(finalPrice.ToString());
                pickup.price = finalPrice;
                bounty.value = finalPrice;
                var ytpIcon = ItemMetaStorage.Instance.GetPointsObject(finalPrice >= 1000 ? 100 : finalPrice >= 500 ? 50 : 25, true);
                pickup.itemSprite.sprite = ytpIcon.itemSpriteLarge;
                pickup.sound = ytpIcon.audPickupOverride;
                pickup.showDescription = false;
                pickup.free = false;
                pickup.gameObject.SetActive(true);
            }
        }

        private void UpdateBounties()
        {
            for (int i = 0; i < pickups.Count; i++)
            {
                Pickup pickup = pickups[i];
                if (!pickup.gameObject.activeSelf) continue;
                var bounty = pickup.gameObject.GetComponent<BountyPickup>();

                pickup.price = Mathf.RoundToInt(bounty.item.price / 1.5f);
                bounty.value = pickup.price;
                itemframe[i].sprite = bounty.item.itemSpriteLarge;
                int originalPrice = bounty.value;
                int finalPrice = originalPrice;
                if (bounty.boosted)
                {
                    float discount = UnityEngine.Random.Range(minBoostDiscount, maxBoostDiscount);
                    float discount2 = finalPrice * discount;
                    finalPrice = Mathf.RoundToInt(discount2 + discount2 * 2f);
                    tag[i].SetText(string.Format("{0}\n <s> {1}  </s> {2}", LocalizationManager.Instance.GetLocalizedText("TAG_Boosted"), originalPrice.ToString(), finalPrice.ToString())); // No prefix patch??
                }
                else
                    tag[i].SetText(finalPrice.ToString());
                pickup.price = finalPrice;
                bounty.value = finalPrice;
                var ytpIcon = ItemMetaStorage.Instance.GetPointsObject(finalPrice >= 1000 ? 100 : finalPrice >= 500 ? 50 : 25, true);
                pickup.itemSprite.sprite = ytpIcon.itemSpriteLarge;
                pickup.sound = ytpIcon.audPickupOverride;
                pickup.showDescription = false;
            }
        }

        public override void OnGenerationFinished()
        {
            base.OnGenerationFinished();
            if (inGameMode && !EndlessForeverPlugin.Instance.gameSave.IsInfGamemode)
                Close();
            else
            {
                Open();
                UpdateBounties();
            }
            if (inGameMode)
                foreach (var door in room.GetPotentialDoorCells().FindAll(door => door.Tile.gameObject.GetComponentInChildren<Door>() != null))
                {
                    Door dor = door.Tile.gameObject.GetComponentInChildren<Door>();
                    dor.aTile.SetShape(0, TileShapeMask.Open);
                    dor.bTile.SetShape(0, TileShapeMask.Open);
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
                {
                    playerEntered = true;
                }
            }
            else
            {
                foreach (Door door in room.doors)
                {
                    door.Unlock();
                }
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
        }

        private void ItemDenied(Pickup pickup, int player)
        {
        }

        private void MarkItemAsSold(Pickup pickup)
        {
            if (!open)
                return;

            if (pickup.price > 0)
                CoreGameManager.Instance.audMan.PlaySingle(audBell);

            for (int i = 0; i < pickups.Count; i++)
                if (pickups[i] == pickup)
                    tag[i].SetText(LocalizationManager.Instance.GetLocalizedText("TAG_Out"));
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
            foreach (Pickup pickup in pickups)
            {
                pickup.AssignItem(CoreGameManager.Instance.NoneItem);
                pickup.free = false;
                pickup.gameObject.SetActive(false);
            }
        }

        private void Open()
        {
            if (!open)
            {
            }

            open = true;
            foreach (Door door in room.doors)
                door.Unlock();

            foreach (Cell cell in room.cells)
                if (cell.hasLight)
                    cell.SetLight(on: true);

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
                pickup.free = true;

            if (inGameMode)
            {
                foreach (Pickup pickup in pickups)
                {
                    pickup.AssignItem(CoreGameManager.Instance.NoneItem);
                    pickup.free = false;
                    pickup.gameObject.SetActive(false);
                }
                foreach (var door in room.GetPotentialDoorCells().FindAll(door => door.Tile.gameObject.GetComponentInChildren<Door>() != null))
                    door.Tile.gameObject.GetComponentInChildren<Door>().Lock(true);
            }
        }
    }

    public class BountyPickup : MonoBehaviour
    {
        public int value = 25;
        public ItemObject item;
        internal bool boosted;
    }
}
