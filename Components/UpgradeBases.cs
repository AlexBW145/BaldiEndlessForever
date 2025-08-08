using EndlessFloorsForever;
using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.Components;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessFloorsForever.Components;

public static class StorePatchHelpers
{
    public static void UpdateUpgradeBar(ref Image[] ___inventoryImage, ref ItemObject ___defaultItem)
    {
        for (int i = 0; i < 5; i++)
        {
            UpgradeSaveData saveData = EndlessForeverPlugin.Instance.gameSave.Upgrades[i];
            if (saveData.id == "none")
                ___inventoryImage[i].sprite = ___defaultItem.itemSpriteSmall;
            else
                ___inventoryImage[i].sprite = EndlessForeverPlugin.Upgrades[saveData.id].GetIcon(saveData.count - 1);
        }
    }

    public static void UpdateUpgradeBar(ref Image[] ___inventoryImage, ItemObject ___defaultItem) // FOR INBOX
    {
        for (int i = 0; i < 5; i++)
        {
            UpgradeSaveData saveData = EndlessForeverPlugin.Instance.gameSave.Upgrades[i];
            if (saveData.id == "none")
                ___inventoryImage[i].sprite = ___defaultItem.itemSpriteSmall;
            else
                ___inventoryImage[i].sprite = EndlessForeverPlugin.Upgrades[saveData.id].GetIcon(saveData.count - 1);
        }
    }
}

public enum UpgradePurchaseBehavior
{
    Nothing,
    FillUpgradeSlot,
    IncrementCounter
}

public struct UpgradeLevel
{
    public string icon;
    public int cost;
    public string descLoca;
}


public class StandardUpgrade
{
    public string id { internal set; get; }
    public UpgradeLevel[] levels = new UpgradeLevel[0];
    public int weight { internal set; get; } = 100;
    public UpgradePurchaseBehavior behavior = UpgradePurchaseBehavior.FillUpgradeSlot;

    public StandardUpgrade(string id, int weight) // Added this because WHY THE FUCK HIS PAST SELF NEVER KNEW ABOUT THE internal KEYWORD?!
    {
        this.id = id;
        this.weight = weight;
    }

    protected int ClampLvl(int level) => Mathf.Clamp(level, 0, levels.Length - 1);
    public virtual Sprite GetIcon(int level) => EndlessForeverPlugin.Instance.UpgradeIcons[GetIconKey(level)]; // You should read what UpgradeClasses.cs says...
    public virtual string GetIconKey(int level) => levels[ClampLvl(level)].icon;
    public virtual int GetCost(int level) => levels[ClampLvl(level)].cost;
    public virtual string GetLoca(int level) => levels[ClampLvl(level)].descLoca;
    public virtual int CalculateSellPrice(int level) => GetCost(ClampLvl(level)) / 4;
    public virtual bool ShouldAppear(int currentLevel) => currentLevel < levels.Length;
    public virtual void OnPurchase() { }
}