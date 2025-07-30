using EndlessFloorsForever.Components;
using HarmonyLib;
using MTM101BaldAPI.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessFloorsForever.Patches
{
    [HarmonyPatch(typeof(PitstopGameManager))]
    class PitStopPatches
    {
        [HarmonyPatch(nameof(PitstopGameManager.PrepareLevelData)), HarmonyPrefix]
        static void WhatFieldTrip(ref int ___tierOneTripLevel, ref WeightedFieldTrip[] ___tierOneTrips)
        {
            if (CoreGameManager.Instance.nextLevel != EndlessForeverPlugin.Instance.inflevel)
                return;
            ___tierOneTripLevel = Mathf.Max(4, EndlessForeverPlugin.Instance.inflevel.levelNo);
            //___tierOneTrips = EndlessForeverPlugin.Instance.fieldTrips.ToArray();
        }
    }

    // I may uncomment this if MTM101DevAPI's patch is not working.
    /*[HarmonyPatch(typeof(PlayerFileManager), nameof(PlayerFileManager.Find))]
    class INotFoundThat
    {
        static bool Prefix(bool[] type, int value, ref bool[] ___clearedLevels)
        {
            if (type == ___clearedLevels && (CoreGameManager.Instance.sceneObject == EndlessForeverPlugin.Instance.inflevel || CoreGameManager.Instance.nextLevel == EndlessForeverPlugin.Instance.inflevel))
                return false;
            return true;
        }
    }*/

    [HarmonyPatch]
    class MiscPatches
    {
        [HarmonyPatch(typeof(StandardDoor), nameof(StandardDoor.OpenTimed)), HarmonyPrefix]
        static void RingRing(StandardDoor __instance)
        {
            if (!__instance.locked && !__instance.open && __instance.makesNoise && (__instance.aTile.room.functionObject.GetComponent<BountyhouseRoomFunction>() != null || __instance.bTile.room.functionObject.GetComponent<BountyhouseRoomFunction>() != null))
                __instance.audMan.PlaySingle(BountyhouseRoomFunction.audRing);
        }

        [HarmonyPatch(typeof(GameInitializer), "GetControlledRandomLevelData"), HarmonyPrefix]
        static bool AlwaysSchoolhouse(ref LevelObject __result)
        {
            if ((CoreGameManager.Instance.sceneObject != EndlessForeverPlugin.Instance.inflevel && CoreGameManager.Instance.nextLevel != EndlessForeverPlugin.Instance.inflevel) || EndlessForeverPlugin.currentFloorData.FloorID >= 5) return true;
            __result = CoreGameManager.Instance.sceneObject.randomizedLevelObject[0].selection;
            return false;
        }
    }

    [HarmonyPatch(typeof(ElevatorScreen), "LoadDelay")]
    class OldShopIsUpgradeShop
    {
        /*static IEnumerator Postfix(IEnumerator result, ElevatorScreen __instance)
        {
            while (result.MoveNext())
                yield return result.Current;
            if (!(CoreGameManager.Instance.sceneObject != EndlessForeverPlugin.Instance.inflevel
                || CoreGameManager.Instance.GetPoints(0) <= 0))
            {
                var skip = __instance.ReflectionGetVariable("skipButton") as GameObject;
                skip.SetActive(false);
                __instance?.QueueShop();
            }
        }*/
        /*[HarmonyPatch(typeof(StoreScreen), "Start"), HarmonyPrefix]
        static bool ChangeAllOfIt(StoreScreen __instance, ref ItemObject[] ___itemForSale, ref ItemObject[] ___defaultItems, ref TMP_Text[] ___itemPrice, ref Image[] ___forSaleImage, ref ItemObject ___defaultItem,
            ref GameObject[] ___forSaleHotSpots, ref ItemObject[] ___inventory, ref Image[] ___inventoryImage,
            ref int ___mapPrice, ref TMP_Text ___mapPriceText, ref int ___ytps, ref TMP_Text ___totalPoints,
            ref AudioManager ___audMan, ref SoundObject[] ___audIntroP2)
        {
            for (int i = 0; i < ___itemForSale.Length; i++)
            {
                if (i < ___defaultItems.Length)
                {
                    ___forSaleImage[i].sprite = ___defaultItems[i].itemSpriteSmall;
                    ___itemPrice[i].text = ___defaultItems[i].price.ToString();
                    ___itemForSale[i] = ___defaultItems[i];
                }
                else if (CoreGameManager.Instance != null && i < CoreGameManager.Instance.sceneObject.totalShopItems)
                {
                    ItemObject[] array = ___itemForSale;
                    int num = i;
                    WeightedSelection<ItemObject>[] shopItems = CoreGameManager.Instance.sceneObject.shopItems;
                    array[num] = WeightedSelection<ItemObject>.RandomSelection(shopItems);
                    ___forSaleImage[i].sprite = ___itemForSale[i].itemSpriteSmall;
                    ___itemPrice[i].text = ___itemForSale[i].price.ToString();
                }
                else
                {
                    ___forSaleImage[i].sprite = ___defaultItem.itemSpriteSmall;
                    ___itemPrice[i].text = "";
                    ___itemForSale[i] = ___defaultItem;
                    ___forSaleHotSpots[i].SetActive(value: false);
                }
            }

            if (CoreGameManager.Instance == null || CoreGameManager.Instance.GetPlayer(0) == null)
            {
                for (int j = 0; j < ___inventory.Length; j++)
                    ___inventory[j] = ___defaultItem;
            }
            else
            {
                for (int k = 0; k < ___inventory.Length; k++)
                {
                    if (k < CoreGameManager.Instance.GetPlayer(0).itm.items.Length)
                    {
                        ___inventory[k] = CoreGameManager.Instance.GetPlayer(0).itm.items[k];
                        ___inventoryImage[k].sprite = ___inventory[k].itemSpriteSmall;
                    }
                    else
                        ___inventory[k] = ___defaultItem;
                }
            }

            if (CoreGameManager.Instance != null)
            {
                ___mapPrice = CoreGameManager.Instance.sceneObject.mapPrice;
                ___mapPriceText.text = ___mapPrice.ToString();
                ___ytps = CoreGameManager.Instance.GetPoints(0);
                ___totalPoints.text = ___ytps.ToString();
            }
            else
            {
                ___mapPrice = 300;
                ___mapPriceText.text = ___mapPrice.ToString();
                ___ytps = 500;
                ___totalPoints.text = ___ytps.ToString();
            }
            ___audMan.QueueRandomAudio(___audIntroP2);
            __instance.StandardDescription();
            ___audMan.audioDevice.ignoreListenerPause = true;
            return false;
        }*/

        [HarmonyPatch(typeof(StoreScreen), "Start"), HarmonyPrefix]
        static bool StoreOverhaul(StoreScreen __instance, ref GameObject ___mapHotSpot, ref GameObject ___banHotSpot, ref Image[] ___inventoryImage, ref ItemObject ___defaultItem, ref int ___mapPrice, ref TMP_Text ___banPriceText, ref Image[] ___forSaleImage, ref TMP_Text[] ___itemPrice, ref TMP_Text ___mapPriceText, ref int ___ytps, ref TMP_Text ___totalPoints, ref AudioManager ___audMan, ref SoundObject ___audJonIntro, ref SoundObject[] ___audIntroP2)
        {
            ___ytps = CoreGameManager.Instance.GetPoints(0);
            ___totalPoints.text = ___ytps.ToString();
            UpgradeShop shop = __instance.gameObject.AddComponent<UpgradeShop>(); //add the upgrade shop
            shop.PopulateShop();

            ___mapPriceText.text = "500"; // Yes the reroll upgrade is going at the position of the fulfill map.
            ___banPriceText.gameObject.SetActive(false);
            ___banHotSpot.SetActive(false);

            if (!EndlessForeverPlugin.Instance.firstEncounters.Item1)
            {
                ___audMan.QueueAudio(___audJonIntro);
                ___audMan.QueueRandomAudio(___audIntroP2);
                EndlessForeverPlugin.Instance.firstEncounters = new Tuple<bool, bool, bool>(true, EndlessForeverPlugin.Instance.firstEncounters.Item2, EndlessForeverPlugin.Instance.firstEncounters.Item3);
            }
            else
                ___audMan.QueueRandomAudio(___audIntroP2);
            __instance.StandardDescription();
            ___audMan.audioDevice.ignoreListenerPause = true;

            for (int i = 0; i < ___inventoryImage.Count(); i++) // Myst has removed that soo...
            {
                if (!___inventoryImage[i].transform.parent.gameObject.activeSelf) continue;
                var but = ___inventoryImage[i].transform.parent.gameObject.AddComponent<StandardMenuButton>();
                but.OnPress = new UnityEngine.Events.UnityEvent();
                but.OnHighlight = new UnityEngine.Events.UnityEvent();
                but.OffHighlight = new UnityEngine.Events.UnityEvent();
                but.OnPress.AddListener(() => __instance.ClickInventory(i));
                but.OnHighlight.AddListener(() => __instance.InventoryDescription(i));
                but.OffHighlight.AddListener(__instance.StandardDescription);
            }
            ___inventoryImage[0].transform.parent.parent.Find("Mask").gameObject.SetActive(false);

            StorePatchHelpers.UpdateShopItems(shop, ref ___forSaleImage, ref ___itemPrice, ref ___defaultItem);

            // The parent has changed, useless...
            //GameObject obj = ___inventoryImage[0].transform.parent.parent.Find("ItemsCover").gameObject;
            //obj.GetComponent<RawImage>().texture = EndlessForeverPlugin.arcadeAssets.Get<Texture2D>("UpgradeSlot5");

            StorePatchHelpers.UpdateUpgradeBar(ref ___inventoryImage, ref ___defaultItem);
            return false;
        }

        [HarmonyPatch(typeof(StoreScreen), "Update"), HarmonyPrefix] // Todo: Learn transpilers to transform this into a transpiler. Which I already learned...
        static bool Smokin(StoreScreen __instance, ref bool ___dragging, ref Image ___draggedItemImage, ref int ___slotDragging, ref AudioManager ___audMan, ref SoundObject ___audCough)
        {
            return false;
        }

        [HarmonyPatch(typeof(StoreScreen), nameof(StoreScreen.UpdateDescription)), HarmonyPrefix]
        static bool DescriptionUpdates(StoreScreen __instance, int val, ref bool ___dragging, ref TMP_Text ___itemDescription, ref AudioManager ___audMan, ref SoundObject ___audMapInfo)
        {
            if (!___dragging)
            {
                if (val <= 5)
                {
                    UpgradeShop shop = __instance.GetComponent<UpgradeShop>();
                    if (val >= shop.Upgrades.Count || shop.Purchased[val])
                        __instance.StandardDescription();
                    else
                        ___itemDescription.text = LocalizationManager.Instance.GetLocalizedText(shop.GetUpgrade(val).GetLoca(EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(shop.Upgrades[val])));
                    return false;
                }
                else if (val == 6)
                {
                    ___itemDescription.text = LocalizationManager.Instance.GetLocalizedText("Upg_Reroll");
                    return false;
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(StoreScreen), nameof(StoreScreen.InventoryDescription)), HarmonyPrefix]
        static bool InventoryDescriptionUpdates(StoreScreen __instance, int val, ref bool ___dragging, ref TMP_Text ___itemDescription, ref AudioManager ___audMan, ref SoundObject ___audMapInfo)
        {
            UpgradeSaveData data = EndlessForeverPlugin.Instance.gameSave.Upgrades[val];
            StandardUpgrade upg = EndlessForeverPlugin.Upgrades[data.id];
            if (data.count == 0)
                ___itemDescription.text = LocalizationManager.Instance.GetLocalizedText("Upg_None");
            else
            {
                string baseDesc = LocalizationManager.Instance.GetLocalizedText(upg.GetLoca(data.count - 1));
                string removeDesc = LocalizationManager.Instance.GetLocalizedText("Upg_Remove");
                ___itemDescription.text = baseDesc + removeDesc;
            }
            return false;
        }

        [HarmonyPatch(typeof(StoreScreen), nameof(StoreScreen.BuyItem)), HarmonyPrefix]
        static bool BuyItemReplace(StoreScreen __instance, int val, ref Image[] ___inventoryImage, ref AudioManager ___audMan, ref bool ___purchaseMade, ref SoundObject[] ___audUnafforable, ref SoundObject[] ___audBuy, ref int ___ytps, ref int ___pointsSpent, ref Image[] ___forSaleImage, ref TMP_Text[] ___itemPrice, ref ItemObject ___defaultItem, ref TMP_Text ___totalPoints)
        {
            if (val == -1)
            {
                UpgradeShop shop = __instance.GetComponent<UpgradeShop>();
                if (!___audMan.QueuedUp)
                    ___audMan.QueueRandomAudio(___audBuy);
                StorePatchHelpers.UpdateShopItems(shop, ref ___forSaleImage, ref ___itemPrice, ref ___defaultItem);
                return false;
            }
            else if (val <= 5)
            {
                UpgradeShop shop = __instance.GetComponent<UpgradeShop>();
                if (shop.Purchased[val]) return false;
                if (val <= shop.Upgrades.Count)
                {
                    StandardUpgrade curUpgrade = shop.GetUpgrade(val);
                    int level = EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(curUpgrade.id);
                    if (___ytps >= curUpgrade.GetCost(level))
                    {
                        if (!EndlessForeverPlugin.Instance.gameSave.PurchaseUpgrade(curUpgrade, curUpgrade.behavior, false)) return false;

                        ___ytps -= curUpgrade.GetCost(level) * 1;
                        ___pointsSpent += curUpgrade.GetCost(level) * 1;
                        ___totalPoints.text = ___ytps.ToString();
                        ___purchaseMade = true;
                        if (!___audMan.QueuedUp)
                            ___audMan.QueueRandomAudio(___audBuy);
                        __instance.StandardDescription();
                        shop.Purchased[val] = true;
                        StorePatchHelpers.UpdateShopItems(shop, ref ___forSaleImage, ref ___itemPrice, ref ___defaultItem);
                        StorePatchHelpers.UpdateUpgradeBar(ref ___inventoryImage, ref ___defaultItem);
                        curUpgrade.OnPurchase();
                        return false;
                    }
                }
                if (!___audMan.QueuedUp)
                {
                    ___audMan.QueueRandomAudio(___audUnafforable);
                    return false;
                }
                return false;
            }
            else if (val == 6)
            {
                if (___ytps >= 500)
                {
                    ___ytps -= 500 * 1;
                    ___pointsSpent += 500 * 1;
                    ___totalPoints.text = ___ytps.ToString();
                    ___purchaseMade = true;
                    if (!___audMan.QueuedUp)
                        ___audMan.QueueRandomAudio(___audBuy);
                    UpgradeShop shop = __instance.GetComponent<UpgradeShop>();
                    shop.Purchased = new bool[8];
                    shop.Upgrades.Clear();
                    shop.PopulateShop();
                    shop.GetComponent<StoreScreen>().BuyItem(-1);
                    return false;
                }
                if (!___audMan.QueuedUp)
                {
                    ___audMan.QueueRandomAudio(___audUnafforable);
                    return false;
                }
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(StoreScreen), nameof(StoreScreen.ClickInventory)), HarmonyPrefix]
        static bool ClickInventoryReplace(StoreScreen __instance, int val, ref AudioManager ___audMan, ref int ___pointsSpent, ref int ___ytps, ref Image[] ___inventoryImage, ref ItemObject ___defaultItem, ref TMP_Text ___totalPoints)
        {
            UpgradeSaveData data = EndlessForeverPlugin.Instance.gameSave.Upgrades[val];
            if (data.id == "none")
                return false;
            else
            {
                StandardUpgrade myG = EndlessForeverPlugin.Upgrades[data.id];
                int level = EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(myG.id);
                int sellPrice = myG.CalculateSellPrice(level);
                ___pointsSpent -= sellPrice;
                ___ytps += sellPrice;
                EndlessForeverPlugin.Instance.gameSave.SellUpgrade(data.id);
                ___totalPoints.text = ___ytps.ToString();
                StorePatchHelpers.UpdateUpgradeBar(ref ___inventoryImage, ref ___defaultItem);
                UpgradeShop.Instance.GetComponent<StoreScreen>().InventoryDescription(val);
                UpgradeShop.Instance.GetComponent<StoreScreen>().BuyItem(-1);
            }
            return false;
        }

        [HarmonyPatch(typeof(StoreScreen), nameof(StoreScreen.TryExit)), HarmonyPrefix]
        static bool TryExitAlways(StoreScreen __instance)
        {
            __instance.Exit();
            return false;
        }

        [HarmonyPatch(typeof(StoreScreen), nameof(StoreScreen.Exit)), HarmonyPrefix]
        static bool ExitAlways(StoreScreen __instance, ref bool ___purchaseMade, ref AudioManager ___audMan, ref int ___pointsSpent, ref SoundObject[] ___audLeaveHappy, ref SoundObject[] ___audLeaveSad)
        {
            if (___purchaseMade)
                ___audMan.QueueRandomAudio(___audLeaveHappy);
            else
                ___audMan.QueueRandomAudio(___audLeaveSad);
            if (CoreGameManager.Instance.GetPlayer(0) != null)
            {
                CoreGameManager.Instance.BackupPlayers();
                CoreGameManager.Instance.AddPoints(___pointsSpent * -1, 0, false, false);
            }
            UnityEngine.Object.Destroy(CursorController.Instance.gameObject);
            __instance.StartCoroutine("WaitForAudio");
            return false;
        }
    }
}
