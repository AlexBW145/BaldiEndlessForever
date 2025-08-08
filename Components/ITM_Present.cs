using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using BepInEx;
using System.Linq;
using UnityEngine.UIElements;

namespace EndlessFloorsForever.Components
{
    public class ITM_Present : Item
    {
        public static List<WeightedItemObject> potentialObjects { get; private set; } = new List<WeightedItemObject>();
        private static float[] LuckValues => [1f, 1.45f, 2f, 3f, 4.50f, 5.95f]; // [1f, 1.45f, 2f, 2.37f, 3f, 4f];

        public override bool Use(PlayerManager pm)
        {
            WeightedItemObject[] objects = potentialObjects.ToArray();
            int weightAverage = objects.Sum(x => x.weight) / objects.Length;
            Dictionary<WeightedItemObject, int> ogWeights = new Dictionary<WeightedItemObject, int>();
            objects.Do((WeightedItemObject obj) =>
            {
                ogWeights.Add(obj, obj.weight);
                obj.weight = 
                obj.weight > weightAverage ? Mathf.FloorToInt(obj.weight / LuckValues[Mathf.Min(EndlessForeverPlugin.Instance.GetUpgradeCount("luck"), LuckValues.Length)]) 
                : Mathf.CeilToInt(obj.weight * LuckValues[Mathf.Min(EndlessForeverPlugin.Instance.GetUpgradeCount("luck"), LuckValues.Length)]);
            });
            if (pm.itm.InventoryFull() && pm.itm.items[pm.itm.selectedItem].itemType != Items.None)
                pm.ec.CreateItem(pm.plm.Entity.CurrentRoom, pm.itm.items[pm.itm.selectedItem], new Vector2(pm.transform.position.x, pm.transform.position.z));
            pm.itm.AddItem(WeightedItemObject.RandomSelection(objects));
            objects.Do((WeightedItemObject obj) =>
            {
                obj.weight = ogWeights[obj];
            });
            Destroy(gameObject);
            return true;
        }
    }
}