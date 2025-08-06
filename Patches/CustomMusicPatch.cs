using BBPlusCustomMusics.Plugin.Public;
using EndlessFloorsForever.Components;
using HarmonyLib;
using MTM101BaldAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EndlessFloorsForever.Patches;

[ConditionalPatchMod("pixelguy.pixelmodding.baldiplus.custommusics"), HarmonyPatch]
internal class CustomMusicPatch
{
    [HarmonyPatch(typeof(UpgradeWarehouseRoomFunction), "Open")]
    [HarmonyPatch(typeof(BountyhouseRoomFunction), "Open")]
    [HarmonyPrefix]
    static void InsertCustomMusic(ref AudioManager ___alarmAudioManager)
    {
        List<SoundObjectHolder> allSounds = AccessTools.Field(typeof(MusicRegister), "allSounds").GetValue(null) as List<SoundObjectHolder>;
        _johnnyMusics ??= [.. allSounds
                .Where(x => x.soundDestiny == SoundDestiny.JohnnyStore)
                .Select(x => x.soundObject)];

        // If no custom Jhonny musics, do nothing
        if (_johnnyMusics.Count == 0)
            return;
        // Randomly select a custom Jhonny music
        int idx = UnityEngine.Random.Range(0, _johnnyMusics.Count);
        // Flush the queue and queue the new music
        ___alarmAudioManager.FlushQueue(true);
        ___alarmAudioManager.QueueAudio(_johnnyMusics[idx]);
        ___alarmAudioManager.SetLoop(true);
    }
    [HarmonyPatch(typeof(UpgradeWarehouseRoomFunction), "Close")]
    [HarmonyPatch(typeof(BountyhouseRoomFunction), "Close")]
    [HarmonyPrefix]
    static void StopCustomMusic(ref bool ___alarmStarted, ref AudioManager ___alarmAudioManager)
    {
        if (!___alarmStarted)
            ___alarmAudioManager.FlushQueue(true);
    }

    static List<SoundObject> _johnnyMusics = null;
}
