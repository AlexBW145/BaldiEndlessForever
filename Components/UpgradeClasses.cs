using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.Reflection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace EndlessFloorsForever.Components;

class BrokenUpgrade : StandardUpgrade
{
    public BrokenUpgrade(string id, int weight) : base(id, weight) { }

    public override string GetLoca(int level) => "<b><color=red>BROKEN UPGRADE</color>\nDO NOT PURCHASE</b>";

    public override bool ShouldAppear(int currentLevel) => false;

    public override int GetCost(int level) => CoreGameManager.Instance.GetPoints(0) + 1;

    public override void OnPurchase() => MTM101BaldiDevAPI.CauseCrash(EndlessForeverPlugin.Instance.Info, new NotImplementedException("Attempted to buy Error upgrade!"));
}

class RerollUpgrade : StandardUpgrade
{
    public RerollUpgrade(string id, int weight) : base(id, weight) { }

    public override void OnPurchase()
    {
        base.OnPurchase();
        throw new NotImplementedException("WHY ARE YOU PURCHASING AN OLD REROLL??");
    }
}

class ExitUpgrade : StandardUpgrade
{
    public ExitUpgrade(string id, int weight) : base(id, weight) { }

    public override bool ShouldAppear(int currentLevel) => base.ShouldAppear(currentLevel) && (EndlessForeverPlugin.currentFloorData.exitCount > (currentLevel + 1));
}

class SlotUpgrade : StandardUpgrade
{
    public SlotUpgrade(string id, int weight) : base(id, weight) { }

    public override void OnPurchase()
    {
        base.OnPurchase();
        CoreGameManager.Instance.GetPlayer(0).itm.maxItem = EndlessForeverPlugin.Instance.gameSave.itemSlots - 1;
        CoreGameManager.Instance.GetPlayer(0).itm.UpdateItems();
        CoreGameManager.Instance.GetHud(0).UpdateInventorySize(EndlessForeverPlugin.Instance.gameSave.itemSlots);
    }

    public override bool ShouldAppear(int currentLevel) => base.ShouldAppear(currentLevel) && CoreGameManager.Instance.sceneObject.levelTitle == "PIT";
}

class ExtraLifeUpgrade : StandardUpgrade
{
    public ExtraLifeUpgrade(string id, int weight) : base(id, weight) { }

    static private FieldInfo _defaultLives = AccessTools.Field(typeof(BaseGameManager), "defaultLives");
    public override void OnPurchase()
    {
        CoreGameManager.Instance.SetLives(2 + EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount("bonuslife"), true);
        //ElevatorScreen.Instance?.Invoke("UpdateLives", 0f);
        base.OnPurchase();
        if (CoreGameManager.Instance.lifeMode == LifeMode.Arcade && EndlessForeverPlugin.Instance.Counters[id] >= levels.Length - 2)
            EndlessForeverPlugin.Instance.Counters[id]--;
    }
    public override bool ShouldAppear(int currentLevel) => base.ShouldAppear(currentLevel) && CoreGameManager.Instance.lifeMode != LifeMode.Intense
            && CoreGameManager.Instance.Lives < (int)_defaultLives.GetValue(BaseGameManager.Instance);
}

class BonusLifeUpgrade : StandardUpgrade
{
    public BonusLifeUpgrade(string id, int weight) : base(id, weight) { }

    static internal FieldInfo _defaultLives = AccessTools.Field(typeof(BaseGameManager), "defaultLives");
    public override void OnPurchase()
    {
        if (CoreGameManager.Instance.Lives >= (int)_defaultLives.GetValue(BaseGameManager.Instance))
        {
            CoreGameManager.Instance.SetLives(2 + EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount("bonuslife"), true);
        }
        //ElevatorScreen.Instance?.Invoke("UpdateLives", 0f);
        base.OnPurchase();
    }
    public override bool ShouldAppear(int currentLevel) => base.ShouldAppear(currentLevel) && CoreGameManager.Instance.sceneObject.levelTitle == "PIT" && CoreGameManager.Instance.lifeMode != LifeMode.Intense
            && CoreGameManager.Instance.GetPoints(0) >= Mathf.RoundToInt(GetCost(currentLevel) * 0.75f);
}
