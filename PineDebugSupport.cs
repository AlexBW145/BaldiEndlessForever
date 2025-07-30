using EndlessFloorsForever.Components;
using HarmonyLib;
using PineDebug;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EndlessFloorsForever
{
    internal class PineDebugSupport
    {
        public static IEnumerator PineDebugStuff()
        {
            yield return "Loading PineDebug addon: Arcade Endless Forever";
            var buttonlist = new PineDebugManager.PineButtonList("Upgrades");
            var menu = PineDebugManager.PineButtonList.Get("Menu");
            menu.Add(PineDebugManager.CreateButton("Upgrades", () =>
            {
                PineDebugManager.Instance.ChangePage(menu, buttonlist);
                PineDebugManager.Instance.audMan.PlaySingle(PineDebugManager.pinedebugAssets.Get<SoundObject>("Button1"));
            }, PineDebugManager.pinedebugAssets.Get<Texture2D>("BorderDebugMenu")));
            buttonlist.Add(PineDebugManager.CreateButton("Clear All Upgrades", () =>
            {
                UpgradeSaveData[] upgrades = EndlessForeverPlugin.Instance.gameSave.Upgrades;
                for (int i = 0; i < 5; i++)
                    while (upgrades[i].id != "none")
                        EndlessForeverPlugin.Instance.gameSave.SellUpgrade(upgrades[i].id);
                foreach (var upgrade in EndlessForeverPlugin.Upgrades.Where(x => x.Value.behavior == UpgradePurchaseBehavior.IncrementCounter))
                {
                    if (upgrade.Value.id == "slots")
                    {
                        EndlessForeverPlugin.Instance.gameSave.Counters["slots"] = 1;
                        upgrade.Value.OnPurchase();
                        continue;
                    }
                    EndlessForeverPlugin.Instance.gameSave.Counters.Remove(upgrade.Value.id);
                }
                PineDebugManager.Instance.audMan.PlaySingle(PineDebugManager.pinedebugAssets.Get<SoundObject>("Button2"));
            }, PineDebugManager.pinedebugAssets.Get<Texture2D>("BorderEmptyBackpack")));
            foreach (var upgrade in EndlessForeverPlugin.Upgrades.Values)
            {
                if (upgrade.id == "reroll" || upgrade.weight == 0 || upgrade.id == "error") continue;
                var _string = LocalizationManager.Instance.GetLocalizedText(upgrade.GetLoca(0));
                int index = _string.IndexOf('\n');
                if (index >= 0)
                    _string = _string.Substring(0, index);
                buttonlist.Add(PineDebugManager.CreateButton(_string, () =>
                {
                    if (!EndlessForeverPlugin.Instance.gameSave.Counters.ContainsKey(upgrade.id) ||  EndlessForeverPlugin.Instance.gameSave.Counters[upgrade.id] < upgrade.levels.Length)
                    {
                        EndlessForeverPlugin.Instance.gameSave.PurchaseUpgrade(upgrade, upgrade.behavior, false);
                        upgrade.OnPurchase();
                    }
                    PineDebugManager.Instance.audMan.PlaySingle(PineDebugManager.pinedebugAssets.Get<SoundObject>("Button2"));
                }, upgrade.GetIcon(0).texture));
            }
            var floorPicker = PineDebugManager.CreateTextInput("Arcade Mode Floor Picker", InputType._int, (text) =>
            {
                int value = Convert.ToInt32(text);
                if (value < 1)
                    value = 1;
                else if (value > EndlessForeverPlugin.Instance.currentData.Item1)
                    value = EndlessForeverPlugin.Instance.currentData.Item1;
                EndlessForeverPlugin.Instance.gameSave.currentFloor = value;
                if (!GlobalCam.Instance.TransitionActive)
                    PineDebugManager.Instance.audMan.PlaySingle(PineDebugManager.pinedebugAssets.Get<SoundObject>("Button2"));

            }, (text) => {
                text = "1";
            }, "1");
            // I will do something about this in future PineDebug updates...
            PineDebugManager.PineButtonList.Get("Levels").Add(floorPicker);
            PineDebugManager.PineButtonList.Get("Levels").Remove(floorPicker);
            PineDebugManager.PineButtonList.Get("Levels").Insert(0, floorPicker);
        }
    }
}
