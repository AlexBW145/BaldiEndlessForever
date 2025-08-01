using EndlessFloorsForever.Components;
using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.OptionsAPI;
using MTM101BaldAPI.PlusExtensions;
using MTM101BaldAPI.Reflection;
using MTM101BaldAPI.Registers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TMPro;
using UnityEngine;
using static Pickup;

namespace EndlessFloorsForever.Patches
{
    [HarmonyPatch]
    class GamePatches
    {
        // Made a game manager for the math, useless...
        /*[HarmonyPatch(typeof(BaseGameManager), nameof(BaseGameManager.CollectNotebooks)), HarmonyPostfix]
        static void TweakBaldiAnger(BaseGameManager __instance, int count, float ___notebookAngerVal)
        {
            int standardCount = (EndlessForeverPlugin.currentFloorData.myFloorBaldi == 1) ? 4 : (EndlessForeverPlugin.currentFloorData.myFloorBaldi == 2) ? 7 : 9;
            if (__instance.NotebookTotal > standardCount)
            {
                __instance.AngerBaldi(-(((float)count) * ___notebookAngerVal));
                float angerAdditive = 0f;
                float stretchedTotal = ((float)standardCount + angerAdditive) / (float)__instance.NotebookTotal;
                __instance.AngerBaldi((((float)count) * ___notebookAngerVal) * stretchedTotal);
            }
        }*/
        [HarmonyPatch(typeof(CoreGameManager), "Start"), HarmonyPostfix]
        static void SetGradeToF(CoreGameManager __instance)
        {
            if (EndlessForeverPlugin.Instance.InGameMode)
                __instance.GradeVal = EndlessForeverPlugin.Instance.gameSave.savedGrade; //less bonus YTPs until you truly DESERVE them!!
        }
        static MethodInfo checkF = AccessTools.Method(typeof(GamePatches), nameof(CheckForHungry));
        [HarmonyPatch(typeof(Bully), "StealItem"), HarmonyTranspiler]
        // Read the original repo's comments of this patch's origin
        static IEnumerable<CodeInstruction> BullyStealPatch(IEnumerable<CodeInstruction> instructions) // insert calling CheckForEnergy right after the toSteal for loop
        {
            bool didFirstFor = false;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Blt_S && !didFirstFor) //end of the for loop
                {
                    didFirstFor = true;
                    yield return new CodeInstruction(OpCodes.Ldarg_0); //this
                    yield return new CodeInstruction(OpCodes.Ldarg_1); //pm
                    yield return new CodeInstruction(OpCodes.Call, checkF); //BullyStealPatch.CheckForHungry
                }
            }
            yield break;
        }

        static FieldInfo slotsToSteal = AccessTools.Field(typeof(Bully), "slotsToSteal");
        static void CheckForHungry(Bully instance, PlayerManager pm)
        {
            if (!EndlessForeverPlugin.Instance.HasUpgrade("hungrybully")) return;
            List<int> toSteal = (List<int>)slotsToSteal.GetValue(instance);
            for (int i = 0; i < toSteal.Count; i++)
            {
                if (pm.itm.items[toSteal[i]].itemType == Items.Quarter)
                {
                    slotsToSteal.SetValue(instance, new List<int>()
                    {
                        toSteal[i]
                    });
                    return;
                }
            }
            /*for (int i = 0; i < toSteal.Count; i++)
            {
                if (pm.itm.items[toSteal[i]].itemType == Items.Bsoda)
                {
                    slotsToSteal.SetValue(instance, new List<int>()
                    {
                        toSteal[i]
                    });
                    return;
                }
            }
            for (int i = 0; i < toSteal.Count; i++)
            {
                if (pm.itm.items[toSteal[i]].GetMeta().tags.Contains("food"))
                {
                    slotsToSteal.SetValue(instance, new List<int>()
                    {
                        toSteal[i]
                    });
                    return;
                }
            }
            for (int i = 0; i < toSteal.Count; i++)
            {
                if (pm.itm.items[toSteal[i]].itemType == Items.Apple)
                {
                    slotsToSteal.SetValue(instance, new List<int>()
                    {
                        toSteal[i]
                    });
                    return;
                }
            }*/
        }
        [HarmonyPatch(typeof(PlayerManager), "Start"), HarmonyPostfix]
        static void IHideNow(ref float ___maxHideableLightLevel)
        {
            if (CoreGameManager.Instance.sceneObject == EndlessForeverPlugin.Instance.inflevel && ___maxHideableLightLevel < 0.1f)
                ___maxHideableLightLevel = 0.1f;
        }
        [HarmonyPatch(typeof(ItemManager), "Awake"), HarmonyPrefix]
        static void SlotAwakePatch(ItemManager __instance)
        {
            if (!EndlessForeverPlugin.Instance.gameSave.IsInfGamemode) return;
            __instance.maxItem = EndlessForeverPlugin.Instance.gameSave.itemSlots - 1;
            CoreGameManager.Instance.GetHud(__instance.pm.playerNumber).UpdateInventorySize(__instance.maxItem + 1);
        }

        [HarmonyPatch(typeof(NoLateTeacher), nameof(NoLateTeacher.PlayerCaught)), HarmonyPrefix]
        static void PompIncreasedTime(NoLateTeacher __instance, ref float ___classTime, ref int ___successPoints)
        {
            if (CoreGameManager.Instance.sceneObject != EndlessForeverPlugin.Instance.inflevel) return;
            float timeIncrease = Mathf.Floor((EndlessForeverPlugin.currentFloorData.classRoomCount * 5f) / 60f) * 60f;
            ___classTime = Mathf.Min(120f + timeIncrease, 540f);
        }

        [HarmonyPatch(typeof(BaseGameManager), nameof(BaseGameManager.BeginSpoopMode)), HarmonyPostfix]
        static void OnSpoopMode(BaseGameManager __instance)
        {
            if (EndlessForeverPlugin.Instance.HasUpgrade("autotag"))
            {
                ITM_Nametag theTag = GameObject.Instantiate(MTM101BaldiDevAPI.itemMetadata.FindByEnum(Items.Nametag).value.item) as ITM_Nametag;
                theTag.gameObject.SetActive(true);
                theTag.ReflectionSetVariable("gaugeSprite", EndlessForeverPlugin.Upgrades["autotag"].GetIcon(EndlessForeverPlugin.Instance.GetUpgradeCount("autotag") - 1));
                if (EndlessForeverPlugin.Instance.GetUpgradeCount("autotag") == 2)
                    theTag.ReflectionSetVariable("setTime", 60f);
                theTag.Use(__instance.Ec.Players[0]);
            }
            __instance.Ec.Players[0].plm.GetModifier().AddModifier("staminaMax", new MTM101BaldAPI.Components.ValueModifier(1, EndlessForeverPlugin.Instance.GetUpgradeCount("stamina") * 25));
        }

        static FieldInfo hasPlayer = AccessTools.Field(typeof(ColliderGroup), "hasPlayer");
        [HarmonyPatch(typeof(EnvironmentController), nameof(EnvironmentController.SetElevators)),HarmonyPostfix]
        static void OnElevator(EnvironmentController __instance, bool enable)
        {
            if (!enable) return;
            if (EndlessForeverPlugin.Instance.HasUpgrade("freeexit"))
            {
                List<int> possibleElevators = new List<int>();
                for (int i = 0; i < __instance.elevators.Count; i++)
                {
                    possibleElevators.Add(i);
                }
                int elevatorsToClose = EndlessForeverPlugin.Instance.GetUpgradeCount("freeexit");
                while (elevatorsToClose > 0)
                {
                    int random = UnityEngine.Random.Range(0, possibleElevators.Count);
                    int selectedId = possibleElevators[random];
                    possibleElevators.RemoveAt(random);
                    Elevator toClose = __instance.elevators[selectedId];
                    hasPlayer.SetValue(toClose.ColliderGroup, true); //there is totally a player here yes absolutely DO NOT QUESTION ANYTHING THERE IS A PLAYER HERE
                    elevatorsToClose--;
                }
            }
        }

        [HarmonyPatch(typeof(TimeOut), nameof(TimeOut.Begin)), HarmonyPostfix]
        static void Time99th()
        {
            if (EndlessForeverPlugin.Instance.gameSave.IsInfGamemode && EndlessForeverPlugin.currentFloorData.FloorID % 99 == 0)
                MusicManager.Instance.PlayMidi(EndlessForeverPlugin.arcadeAssets.Get<string>("F99Finale"), true);
        }
    }

    [HarmonyPatch(typeof(StoreRoomFunction))]
    class ShopPatches
    {
        static MethodInfo Close = AccessTools.Method(typeof(StoreRoomFunction), "Close");
        static MethodInfo Open = AccessTools.Method(typeof(StoreRoomFunction), "Open");
        [HarmonyPatch(nameof(StoreRoomFunction.OnGenerationFinished)), HarmonyPostfix]
        static void CloseStoreIfPossible(StoreRoomFunction __instance)
        {
            if (EndlessForeverPlugin.Instance.InGameMode && __instance.Room.ec.notebookTotal > 4)
                Close.Invoke(__instance, []);
        }
        [HarmonyPatch("Update"), HarmonyPrefix]
        static void OpenStoreByThat(StoreRoomFunction __instance, ref int ___notebooksAtLastReset, ref int ___notebooksPerReset, ref bool ___open)
        {
            if (EndlessForeverPlugin.Instance.InGameMode && !___open && BaseGameManager.Instance.FoundNotebooks - ___notebooksAtLastReset >= ___notebooksPerReset)
                Open.Invoke(__instance, []);
        }
    }

    [HarmonyPatch(typeof(Pickup))]
    class PickupPatches
    {
        [HarmonyPatch(typeof(Pickup), nameof(Pickup.AssignItem)), HarmonyPrefix]
        internal static bool PickupPatch(Pickup __instance, ItemObject item, ref bool ___stillHasItem)
        {
            if (item.itemType.ToStringExtended<Items>() == "EndlessUpgrade")
            {
                ___stillHasItem = true;
                __instance.item = item;
                StandardUpgrade upg;
                if (__instance.GetComponent<UpgradePickupMarker>())
                {
                    upg = __instance.GetComponent<UpgradePickupMarker>().upgrade;
                    if (!upg.ShouldAppear(EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(upg.id)))
                    {
                        GameObject.Destroy(__instance.GetComponent<UpgradePickupMarker>());
                        if (__instance.price != 0)
                        {
                            __instance.AssignItem(ItemMetaStorage.Instance.FindByEnum(Items.None).value);
                            ___stillHasItem = false;
                            return false;
                        }
                    }
                }
                else
                {
                    if (__instance.price != 0)
                    {
                        // shop logic goes here!
                        throw new NotImplementedException("shop logic no pre assign not implemented if it even should be idk man im just an error message.");
                    }
                    else
                    {
                        upg = UpgradeWarehouseRoomFunction.GetRandomValidUpgrade((Mathf.RoundToInt(__instance.gameObject.transform.position.x * __instance.gameObject.transform.position.z) * 9163) + Mathf.RoundToInt(__instance.gameObject.transform.position.z * 1.224f));
                    }
                }
                if (upg == null)
                {
                    __instance.AssignItem(ItemMetaStorage.Instance.GetPointsObject(500, false));
                    return false;
                }
                __instance.itemSprite.sprite = upg.GetIcon(EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(upg.id));
                __instance.transform.name = "Item_Upg" + upg.id;
                __instance.gameObject.GetOrAddComponent<UpgradePickupMarker>().upgrade = upg;
                __instance.showDescription = true;
                return false;
            }
            else if (item.itemType.ToStringExtended<Items>() == "EndlessBounty")
            {
                ___stillHasItem = true;
                __instance.item = item;
                ItemObject thechosenitem = CoreGameManager.Instance.NoneItem;
                if (CoreGameManager.Instance.sceneObject?.levelTitle == "PIT")
                    thechosenitem = WeightedItemObject.RandomSelection(CoreGameManager.Instance.nextLevel.GetCurrentCustomLevelObject().potentialItems.Where(item => item.selection.addToInventory && item.selection.price != 0 && item.selection.price < int.MaxValue).ToArray());
                else if (CoreGameManager.Instance.sceneObject?.extraAsset != null)
                    thechosenitem = WeightedItemObject.RandomSelection(CoreGameManager.Instance.sceneObject?.extraAsset?.potentialItems.Where(item => item.selection.addToInventory && item.selection.price != 0 && item.selection.price < int.MaxValue).ToArray());
                else if (BaseGameManager.Instance.levelObject != null)
                    thechosenitem = WeightedItemObject.RandomSelection(BaseGameManager.Instance.levelObject?.potentialItems.Where(item => item.selection.addToInventory && item.selection.price != 0 && item.selection.price < int.MaxValue).ToArray());
                __instance.gameObject.GetComponent<BountyPickup>().item = thechosenitem;
                __instance.transform.name = "Item_Bounty" + thechosenitem.name;
                
                __instance.showDescription = false;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Pickup), "Start"), HarmonyPostfix]
        static void PickupStartHack(Pickup __instance, ref bool ___stillHasItem) => PickupPatch(__instance, __instance.item, ref ___stillHasItem);

        [HarmonyPatch(typeof(Pickup), nameof(Pickup.Collect)), HarmonyPrefix]
        static bool PickupCollectPatch(Pickup __instance, int player, ref bool ___stillHasItem, ref PickupInteractionDelegate ___OnItemCollected)
        {
            if (__instance.GetComponent<UpgradePickupMarker>() != null)
            {
                StandardUpgrade upg = __instance.GetComponent<UpgradePickupMarker>().upgrade;
                GameObject.Destroy(__instance.GetComponent<UpgradePickupMarker>());
                __instance.AssignItem(CoreGameManager.Instance.NoneItem);
                ___stillHasItem = false;
                __instance.gameObject.SetActive(false);
                if (__instance.icon != null)
                    __instance.icon.spriteRenderer.enabled = false;
                if (!EndlessForeverPlugin.Instance.gameSave.PurchaseUpgrade(upg, upg.behavior, BaseGameManager.Instance is PitstopGameManager ? !EndlessForeverPlugin.Instance.gameSave.HasEmptyUpgrade() : true)) return false;
                upg.OnPurchase();
                CoreGameManager.Instance.GetHud(player).CloseTooltip();
                ___OnItemCollected?.Invoke(__instance, player);
                UpgradePickupMarker.UpdateAllUpgrades();
                return false;
            }
            else if (__instance.GetComponent<BountyPickup>() != null)
            {
                GameObject.Destroy(__instance.GetComponent<BountyPickup>());
                __instance.AssignItem(CoreGameManager.Instance.NoneItem);
                ___stillHasItem = false;
                __instance.gameObject.SetActive(false);
                if (__instance.icon != null)
                    __instance.icon.spriteRenderer.enabled = false;
                CoreGameManager.Instance.audMan.PlaySingle(__instance.sound);
                CoreGameManager.Instance.GetHud(player).CloseTooltip();
                ___OnItemCollected?.Invoke(__instance, player);
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Pickup), nameof(Pickup.Clicked)), HarmonyPrefix]
        static bool PickupClickedPatch(Pickup __instance, int player, ref PickupInteractionDelegate ___OnItemDenied, ref PickupInteractionDelegate ___OnItemPurchased)
        {
            if (__instance.GetComponent<UpgradePickupMarker>() != null)
            {
                StandardUpgrade upg = __instance.GetComponent<UpgradePickupMarker>().upgrade;
                if (EndlessForeverPlugin.Instance.gameSave.CanPurchaseUpgrade(upg, upg.behavior, BaseGameManager.Instance is PitstopGameManager ? !EndlessForeverPlugin.Instance.gameSave.HasEmptyUpgrade() : true))
                    return true;
                ___OnItemDenied?.Invoke(__instance, player);
                return false;
            }
            else if (__instance.GetComponent<BountyPickup>() != null)
            {
                var bounty = __instance.GetComponent<BountyPickup>();
                if ((CoreGameManager.Instance.GetPlayer(player).itm.Has(bounty.item.itemType) && !__instance.free) || __instance.free)
                {
                    CoreGameManager.Instance.AddPoints(bounty.value, player, true, false);
                    ___OnItemPurchased.Invoke(__instance, player);
                    __instance.Collect(player);
                    if (!__instance.free)
                        CoreGameManager.Instance.GetPlayer(player).itm.Remove(bounty.item.itemType);
                    return false;
                }
                ___OnItemDenied?.Invoke(__instance, player);
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Pickup), nameof(Pickup.ClickableSighted)), HarmonyPrefix]
        static bool PickupSightedPatch(Pickup __instance, int player)
        {
            if (!__instance.showDescription || __instance.GetComponent<UpgradePickupMarker>() == null) return true;
            StandardUpgrade upg = __instance.GetComponent<UpgradePickupMarker>().upgrade;
            CoreGameManager.Instance.GetHud(player).SetTooltip(upg.GetLoca(EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(upg.id)));
            return false;
        }

        [HarmonyPatch(typeof(Pickup), nameof(Pickup.Clicked)), HarmonyTranspiler]
        static IEnumerable<CodeInstruction> DoNotContributeToTotals(IEnumerable<CodeInstruction> instructions) // insert calling CheckForEnergy right after the toSteal for loop
        {
            bool didFirstFor = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Callvirt && ((MethodInfo)instruction.operand).Name == "AddPoints" && !didFirstFor) //end of the for loop
                {
                    didFirstFor = true;
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0); // false
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.DeclaredMethod(typeof(CoreGameManager), nameof(CoreGameManager.AddPoints), [typeof(int), typeof(int), typeof(bool), typeof(bool)])); // Patches that function to make sure purchased items does not contributed to total YTPs obtained
                }
                else
                    yield return instruction;
            }
            yield break;
        }
    }

    [HarmonyPatch(typeof(ElevatorScreen))]
    class ElevatorPatches
    {
        [HarmonyPatch("Start"), HarmonyPrefix]
        static void ElevatorScreenAddLives(ref Sprite[] ___lifeImages)
        {
            if (___lifeImages.Length > 4) return;
            ___lifeImages = ___lifeImages.AddRangeToArray([EndlessForeverPlugin.arcadeAssets.Get<Sprite>("LifeTubes4"), EndlessForeverPlugin.arcadeAssets.Get<Sprite>("LifeTubes5"), EndlessForeverPlugin.arcadeAssets.Get<Sprite>("LifeTubes6")]);
        }
        [HarmonyPatch(nameof(ElevatorScreen.Initialize)), HarmonyPostfix]
        static void MultiplierNonZero(ref int ___ytpMultiplier)
        {
            Mathf.Clamp(___ytpMultiplier, 1, ___ytpMultiplier);
        }
        [HarmonyPatch(nameof(ElevatorScreen.UpdateFloorDisplay)), HarmonyPostfix]
        static void ItNeedaFit(ref TMP_Text ___floorText)
        {
            if (___floorText.text.Length > 3)
                ___floorText.autoSizeTextContainer = true;
        }
    }
}
