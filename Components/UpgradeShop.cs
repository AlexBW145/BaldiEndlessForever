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

namespace EndlessFloorsForever.Components
{
    public class UpgradeShop : MonoBehaviour
    {
        public static UpgradeShop Instance { get; private set; }

        public List<string> Upgrades = new List<string>();

        public bool[] Purchased = new bool[6];

        //public bool alwaysAddReroll = false;

        public void Awake()
        {
            Instance = this;
            /*var johnnyBase = gameObject.GetComponentInChildren<Canvas>().transform.Find("JohnnyBase").gameObject.GetComponent<Image>();
            var themouth = johnnyBase.transform.parent.GetComponentInChildren<Animator>();
            Destroy(themouth);
            johnnyBase.sprite = EndlessForeverPlugin.arcadeAssets.Get<Sprite>("DanielIdle");
            var animator = johnnyBase.gameObject.AddComponent<CustomImageAnimator>();
            var volumeAnimator = johnnyBase.gameObject.AddComponent<CustomVolumeAnimator>();
            animator.animations.Add("Idle", new CustomAnimation<Sprite>([EndlessForeverPlugin.arcadeAssets.Get<Sprite>("DanielIdle")], 1f));
            animator.animations.Add("Talk1", new CustomAnimation<Sprite>([EndlessForeverPlugin.arcadeAssets.Get<Sprite>("DanielTalk1")], 0.25f));
            animator.animations.Add("Talk2", new CustomAnimation<Sprite>([EndlessForeverPlugin.arcadeAssets.Get<Sprite>("DanielTalk2")], 0.25f));
            animator.animations.Add("Talk3", new CustomAnimation<Sprite>([EndlessForeverPlugin.arcadeAssets.Get<Sprite>("DanielTalk3")], 0.25f));
            animator.image = johnnyBase.GetComponent<Image>();
            volumeAnimator.animator = animator;
            volumeAnimator.audioSource = GetComponent<AudioSource>();
            volumeAnimator.animations = ["Idle", "Talk1", "Talk2", "Talk3"];*/
        }

        public StandardUpgrade GetUpgrade(int index)
        {
            return EndlessForeverPlugin.Upgrades[Upgrades[index]];
        }

        public void PopulateShop()
        {
            try
            {
                int randRang = 6;
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
                for (int i = 0; i < randRang; i++)
                {
                    if (weightedUpgrades.Length == 0)
                    {
                        Upgrades.Add("error");
                        continue;
                    }
                    StandardUpgrade gu = WeightedSelection<StandardUpgrade>.RandomSelection(weightedUpgrades);
                    Upgrades.Add(gu.id);
                    if (!gu.ShouldAppear(EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(gu.id) + Upgrades.Where(x => x == gu.id).Count()))
                    {
                        weightedUpgrades = weightedUpgrades.Where(x => x.selection.id != gu.id).ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                MTM101BaldiDevAPI.CauseCrash(EndlessForeverPlugin.Instance.Info, ex);
            }
        }
    }

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

        public static void UpdateShopItems(UpgradeShop shop, ref Image[] ___forSaleImage, ref TMP_Text[] ___itemPrice, ref ItemObject ___defaultItem)
        {
            for (int i = 0; i < ___forSaleImage.Length; i++)
            {
                ___itemPrice[i].color = Color.black;
                if (i < shop.Upgrades.Count)
                {
                    StandardUpgrade upg = shop.GetUpgrade(i);
                    int level = EndlessForeverPlugin.Instance.gameSave.GetUpgradeCount(shop.Upgrades[i]);
                    ___forSaleImage[i].sprite = upg.GetIcon(level);
                    ___itemPrice[i].text = upg.GetCost(level).ToString();
                    if (shop.Purchased[i])
                    {
                        ___itemPrice[i].text = "SOLD";
                        ___itemPrice[i].color = Color.red;
                        ___forSaleImage[i].sprite = ___defaultItem.itemSpriteSmall;
                    }
                }
                else
                {
                    ___itemPrice[i].text = "";
                    ___forSaleImage[i].sprite = ___defaultItem.itemSpriteSmall;
                }
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
        public virtual bool ShouldAppear(int currentLevel)
        {
            return currentLevel < levels.Length;
        }
        public virtual void OnPurchase()
        {

        }
    }
}