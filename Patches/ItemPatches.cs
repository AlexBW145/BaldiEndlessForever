using HarmonyLib;
using MTM101BaldAPI.Registers;
using MTM101BaldAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using MTM101BaldAPI.PlusExtensions;
using MTM101BaldAPI.Components;
using System.Reflection.Emit;

namespace EndlessFloorsForever.Patches
{
    [HarmonyPatch]
    internal class ItemPatches
    {
        static readonly float[] Percentages = [0f, 1f, 2f, 3f, 5f, 6f, 10f];
        static MethodInfo RemoveItem = AccessTools.Method(typeof(ItemManager), nameof(ItemManager.RemoveItem));
        [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.UseItem)), HarmonyTranspiler]
        static IEnumerable<CodeInstruction> LuckyQuarterPatch(IEnumerable<CodeInstruction> instructions) // Using an item has chances of quarter with the Lucky Quarter upgrade.
        {
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Call && instruction.OperandIs(RemoveItem)) //end of the for loop
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0); //this
                    yield return Transpilers.EmitDelegate<Action<ItemManager>>((__instance) =>
                    {
                        if (EndlessForeverPlugin.Instance.HasUpgrade("bank"))
                            if (Percentages[Mathf.Min(EndlessForeverPlugin.Instance.GetUpgradeCount("bank"), Percentages.Length)] >= UnityEngine.Random.Range(0f, 100f))
                                __instance.SetItem(MTM101BaldiDevAPI.itemMetadata.FindByEnum(Items.Quarter).value, __instance.selectedItem);
                    });
                }
            }
            yield break;
        }

        [HarmonyPatch(typeof(WaterFountain), "Clicked"), HarmonyPostfix]
        static void FountainIncreasePatch(WaterFountain __instance, int playerNumber)
        {
            float maxStam = CoreGameManager.Instance.GetPlayer(playerNumber).plm.staminaMax;
            if (CoreGameManager.Instance.GetPlayer(playerNumber).plm.stamina != maxStam) return;
            CoreGameManager.Instance.GetPlayer(playerNumber).plm.stamina = maxStam + (EndlessForeverPlugin.Instance.GetUpgradeCount("drink") * 10);
        }

        [HarmonyPatch(typeof(ITM_BSODA), "Use"), HarmonyPrefix]
        static void SlowBSODAPatch(ITM_BSODA __instance, ref float ___speed, ref float ___time)
        {
            ___speed *= 1f - (EndlessForeverPlugin.Instance.GetUpgradeCount("slowsoda") * 0.15f);
            ___time += 2f * EndlessForeverPlugin.Instance.GetUpgradeCount("slowsoda");
        }

        static FieldInfo _finished = AccessTools.Field(typeof(ITM_AlarmClock), "finished");
        static FieldInfo _audMan = AccessTools.Field(typeof(EnvironmentController), "audMan");
        static MethodInfo _Timer = AccessTools.Method(typeof(ITM_AlarmClock), "Timer");
        [HarmonyPatch(typeof(ITM_AlarmClock), nameof(ITM_AlarmClock.Use)), HarmonyPrefix]
        static bool AlarmClockPatch(ITM_AlarmClock __instance, ref EnvironmentController ___ec, PlayerManager pm, Entity ___entity, float[] ___setTime, int ___initSetTime, ref bool __result)
        {
            if (!EndlessForeverPlugin.Instance.HasUpgrade("timeclock")) return true;
            // i hate doing this.
            __instance.StartCoroutine(WaitForCompletion(__instance));
            ___ec = pm.ec;
            __instance.transform.position = pm.transform.position;
            ___entity.Initialize(___ec, __instance.transform.position);
            __instance.StartCoroutine("Timer", ___setTime[___initSetTime]);
            __result = true;
            return false;
        }

        static IEnumerator WaitForCompletion(ITM_AlarmClock clock)
        {
            int clickablelayer = LayerMask.NameToLayer("ClickableEntities"); // Do not affect them
            int clickablecollidablelayer = LayerMask.NameToLayer("ClickableCollidableEntities"); // and also them because math balloons.
            int disabledLayer = LayerMask.NameToLayer("Disabled"); // What in the culling??
            while (!(bool)_finished.GetValue(clock))
            {
                yield return null;
            }
            clock.StopCoroutine("Timer"); //dont die until WE are ready!
            TimeScaleModifier timeMod = new TimeScaleModifier()
            {
                environmentTimeScale = 0f,
                npcTimeScale = 0f
            };
            foreach (var entity in Entity.allEntities)
            {
                if (!entity.gameObject.CompareTag("Player") && entity.gameObject.layer != clickablelayer && entity.gameObject.layer != clickablecollidablelayer && entity.gameObject.layer != disabledLayer)
                {
                    entity.SetBlinded(true);
                    entity.SetTrigger(false);
                    entity.SetInteractionState(false);
                }
            }
            AudioManager audMan = (AudioManager)_audMan.GetValue(BaseGameManager.Instance.Ec);
            audMan.PlaySingle(EndlessForeverPlugin.arcadeAssets.Get<SoundObject>("TimeSlow"));
            BaseGameManager.Instance.Ec.AddTimeScale(timeMod);
            float timeToWait = (EndlessForeverPlugin.Instance.GetUpgradeCount("timeclock") * 10f);
            while (timeToWait > 0f)
            {
                timeToWait -= Time.deltaTime;
                yield return null;
            }
            foreach (var entity in Entity.allEntities)
            {
                if (!entity.gameObject.CompareTag("Player") && entity.gameObject.layer == disabledLayer)
                {
                    entity.SetBlinded(false);
                    entity.SetTrigger(true);
                    entity.SetInteractionState(true);
                }
            }
            BaseGameManager.Instance.Ec.RemoveTimeScale(timeMod);
            audMan.PlaySingle(EndlessForeverPlugin.arcadeAssets.Get<SoundObject>("TimeFast"));
            UnityEngine.Object.Destroy(clock.gameObject);
            yield break;
        }

        [HarmonyPatch(typeof(ITM_Boots), "Use"), HarmonyPrefix]
        static void GaugeMeBoots(ref Sprite ___gaugeSprite)
        {
            if (EndlessForeverPlugin.Instance.HasUpgrade("speedyboots"))
                ___gaugeSprite = EndlessForeverPlugin.Upgrades["speedyboots"].GetIcon(EndlessForeverPlugin.Instance.GetUpgradeCount("speedyboots") - 1);
        }

        [HarmonyPatch(typeof(ITM_Boots), "Use"), HarmonyPostfix]
        static void BootsPatch(ITM_Boots __instance, PlayerManager pm, float ___setTime)
        {
            if (EndlessForeverPlugin.Instance.HasUpgrade("speedyboots"))
            {
                __instance.StartCoroutine(BootsNumerator(pm, ___setTime));
            }
        }

        static IEnumerator BootsNumerator(PlayerManager pm, float startTime)
        {
            float time = startTime;
            BootsSpeedManager myChecker = pm.gameObject.GetOrAddComponent<BootsSpeedManager>();
            while (time > 0f)
            {
                time -= Time.deltaTime * pm.PlayerTimeScale;
                yield return null;
            }
            myChecker.RemoveBoot();
            yield break;
        }

        class BootsSpeedManager : MonoBehaviour
        {
            private int bootsActive = 0;
            MovementModifier speedMod = new MovementModifier(Vector3.zero, 1f + EndlessForeverPlugin.Instance.GetUpgradeCount("speedyboots") * 0.2f);
            ValueModifier staminaMod = new ValueModifier(0.8f);
            private PlayerMovement player;

            void Start()
            {
                player = GetComponent<PlayerMovement>();
                player.Entity.ExternalActivity.moveMods.Add(speedMod);
                if (EndlessForeverPlugin.Instance.GetUpgradeCount("speedyboots") > 1)
                    player.pm.GetMovementStatModifier().AddModifier("staminaDrop", staminaMod);
            }

            public void AddBoot()
            {
                bootsActive++;
            }

            public void RemoveBoot()
            {
                bootsActive--;
                if (bootsActive <= 0)
                {
                    player.Entity.ExternalActivity.moveMods.Remove(speedMod);
                    player.pm.GetMovementStatModifier().RemoveModifier(staminaMod);
                    Destroy(this);
                }
            }
        }

        private static MethodInfo _setGuilt = AccessTools.Method(typeof(NPC), "SetGuilt");
        [HarmonyPatch(typeof(ITM_PrincipalWhistle), "Use"), HarmonyPrefix]
        static void WhistlePatch(ITM_PrincipalWhistle __instance, PlayerManager pm)
        {
            if (!EndlessForeverPlugin.Instance.HasUpgrade("favor")) return;
            List<NPC> elligableNPCs = new List<NPC>();
            foreach (NPC npc in pm.ec.Npcs)
            {
                if (Vector3.Distance(npc.transform.position, pm.transform.position) <= pm.pc.reach * 5)
                {
                    if (!elligableNPCs.Contains(npc))
                    {
                        elligableNPCs.Add(npc);
                    }
                }
            }
            RaycastHit hit;
            LayerMask clickMask = new LayerMask() { value = 131073 }; //copied from ITM_Scissors
            if (Physics.Raycast(pm.transform.position, CoreGameManager.Instance.GetCamera(pm.playerNumber).transform.forward, out hit, pm.pc.reach * 7, clickMask))
            {
                NPC hitNPC = hit.transform.GetComponent<NPC>();
                if (hitNPC)
                {
                    if (!elligableNPCs.Contains(hitNPC))
                    {
                        elligableNPCs.Add(hitNPC);
                    }
                }
            }
            elligableNPCs.Do(x =>
            {
                var meta = x.GetMeta();
                if (x.Character == Character.Principal || x.GetType().Equals(typeof(Principal)) || !meta.flags.HasFlag(NPCFlags.HasTrigger) || meta.tags.Contains("infarcade_favoritisminvulnerable")) return;
                _setGuilt.Invoke(x, [10f, "Bullying"]);
            });
        }
    }
}
