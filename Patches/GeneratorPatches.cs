using EndlessFloorsForever.Components;
using HarmonyLib;
using MonoMod.Utils;
using MTM101BaldAPI;
using MTM101BaldAPI.Registers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace EndlessFloorsForever.Patches
{
    class ENDPatches
    {
        internal static List<T2> CreateWeightedShuffledListWithCount<T, T2>(List<T> list, int count, System.Random rng) where T : WeightedSelection<T2>
        {
            List<T2> newL = new List<T2>();
            List<T> selections = list.ToList();
            for (int i = 0; i < count; i++)
            {
                // this is literally the worst fucking thing that i think anyone has written
                // AND I LITERALLY ONLY END UP USING THIS FUCKING ONCE MEANING MAKING IT GENERICALLY TYPED SERVERS LITERALLY NO PURPOSE. FUCK.
                T2 selectedValue = (T2)AccessTools.Method(typeof(T), "ControlledRandomSelection").Invoke(null, new object[] { selections.ToArray(), rng });//.ControlledRandomSelectionList(selections, rng);
                selections.RemoveAll(x => object.Equals(x.selection, selectedValue)); //thank you stack overflow for saving my ass
                newL.Add(selectedValue);
            }

            return newL;
        }

        internal static List<T> CreateShuffledListWithCount<T>(List<T> list, int count, System.Random rng)
        {
            count = Math.Min(list.Count, count);
            List<T> newList = new List<T>();
            List<T> copiedList = list.ToList(); // create a duplicate list
            for (int i = 0; i < count; i++)
            {
                int selectedIndex = rng.Next(0, copiedList.Count);
                newList.Add(copiedList[selectedIndex]);
                copiedList.RemoveAt(selectedIndex);
            }
            return newList;
        }
    }

    [HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.Generate), MethodType.Enumerator)]
    class MoarExits
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var matcher = new CodeMatcher(instructions) // If it throws invalid, some mod already patched that or the code has changed. But this exists just in case...
                .Start()
                .MatchForward(false,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, AccessTools.Method(typeof(Directions), nameof(Directions.All))),
                new(x => x.opcode == OpCodes.Stfld && ((FieldInfo)x.operand).Name == "<potentailExitDirections>5__36"))
                .Advance(1)
                .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldloc_2))
                .InsertAndAdvance(Transpilers.EmitDelegate<Func<LevelGenerator, List<Direction>>>((levelg) =>
                {
                    List<Direction> directions = new List<Direction>
                    {
                        Direction.North,
                        Direction.East,
                        Direction.South,
                        Direction.West,
                    };
                    if (levelg.scene != EndlessForeverPlugin.Instance.inflevel)
                        return directions;
                    var result = new List<Direction>
                    {
                        Direction.North,
                        Direction.East,
                        Direction.South,
                        Direction.West,
                    };
                    for (int i = 0; i < levelg.ld.exitCount; i++)
                        result.Add(directions[i % 4]);
                    return result;
                }))
                .InstructionEnumeration();
            return matcher;
        }
    }

    [HarmonyPatch(typeof(LockedRoomFunction), nameof(LockedRoomFunction.AfterRoomValuesCalculated))]
    class BusPassed
    {
        static WeightedItemObject presentWeighted => new WeightedItemObject()
        {
            selection = EndlessForeverPlugin.arcadeAssets.Get<ItemObject>("RandomPresent"),
            weight = 70
        };
        static void Prefix(ref WeightedItemObject[] ___potentialHighEndItems)
        {
            if (EndlessForeverPlugin.Instance.InGameMode)
                ___potentialHighEndItems.DoIf(x => x.selection.itemType == Items.BusPass, x => x = presentWeighted);
        }
    }
}
