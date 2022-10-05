using BepInEx;
using Bounce.BlobAssets;
using Bounce.TaleSpire.AssetManagement;
using Bounce.Unmanaged;
using HarmonyLib;
using ModdingTales;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;


namespace HolloFox
{
    public partial class AudioPlugin : BaseUnityPlugin
    {
        [HarmonyPatch(typeof(AssetDb), "OnSetupInternals")]
        public class OnSetupInternalsPatches
        {
            static void Postfix()
            {
                if (LogLevel > ModdingUtils.LogLevel.Low)
                    _logger.LogInfo("Registering Audio From Collected Audio Library");
                foreach (MusicData.MusicKind kind in Enum.GetValues(typeof(MusicData.MusicKind)))
                {
                    foreach (var file in Audio[kind.ToString()])
                    {
                        if (AssetDb.Music.TryGetValue(file.Key, out _)) continue;
                        var builder = new BlobBuilder(Allocator.Persistent);
                        ref var root = ref builder.ConstructRoot<MusicData>();
                        MusicData.Construct(builder, ref root, file.Key, file.Key, file.Value.name, file.Value.name,
                            new string[] { }, "", file.Value.name, kind);
                        var x = builder.CreateBlobAssetReference<MusicData>(Allocator.Persistent);
                        AssetDb.Music.TryAdd(file.Key, x.TakeView());
                    }
                }
            }
        }

        [HarmonyPatch(typeof(AtmosphereManager.LoadedAudioClip), "Load")]
        public class LoadedAudioClipLoadPatch
        {
            static bool Prefix(Action<AtmosphereManager.LoadedAudioClip, AudioClip> ClipLoaded,
                ref AudioClip ____clip,
                ref AtmosphereManager.LoadedAudioClip __instance
            )
            {
                bool found = false;
                foreach (Dictionary<NGuid, AudioData> audioSource in Audio.Values)
                {
                    if (audioSource.ContainsKey(__instance.GUID))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found || ____clip != null)
                {
                    if (LogLevel > ModdingUtils.LogLevel.Medium)
                        _logger.LogInfo("Core Audio Requested");
                    return true;
                }

                if (LogLevel > ModdingUtils.LogLevel.Medium)
                    _logger.LogInfo("Custom Audio Requested");
                LoadAudioCallback(new object[] { __instance, ____clip, ClipLoaded });
                return false;
            }
        }
    }
}