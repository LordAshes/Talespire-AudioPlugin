using Bounce.BlobAssets;
using Bounce.TaleSpire.AssetManagement;
using Bounce.Unmanaged;
using HarmonyLib;
using System;
using System.Collections.Generic;
using RPCPlugin.RPC;
using TaleSpire.Atmosphere;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HolloFox
{
    [HarmonyPatch(typeof(AssetDb), "OnSetupInternals")]
    public class Patches
    {
        static void Postfix()
        {
            foreach(MusicData.MusicKind kind in Enum.GetValues(typeof(MusicData.MusicKind)))
            {
                foreach (var file in AudioPlugin.Audio[kind.ToString()])
                {
                    if (AssetDb.Music.TryGetValue(file.Key, out _)) continue;
                    var builder = new BlobBuilder(Allocator.Persistent);
                    ref var root = ref builder.ConstructRoot<MusicData>();
                    MusicData.Construct(builder, ref root, file.Key, file.Key, file.Value.name, file.Value.name,
                        new string[] { }, "", file.Value.name, kind);
                    var x = builder.CreateBlobAssetReference<MusicData>(Allocator.Persistent);
                    // Debug.Log($"{kind} Added {file.Value.name}:" + AssetDb.Music.TryAdd(file.Key, x.TakeView()));
                    AssetDb.Music.TryAdd(file.Key, x.TakeView());
                }
            }
        }
    }

    [HarmonyPatch(typeof(AtmosphereManager), "TryPlayMusic")]
    public class LoadedPlayIfNeededPatch
    {
        private static NGuid previousMusic;
        private static NGuid currentMusic;

        private static NGuid previousAmbient;
        private static NGuid currentAmbient;

        static bool Prefix(NGuid id, AtmosphereReferenceData ____transferRefData)
        {
            if (AssetDb.Music.TryGetValue(id, out var data))
            {
                ref MusicData local = ref data.Value;
                if (local.Kind == MusicData.MusicKind.Ambient)
                {
                    previousAmbient = currentAmbient;
                    currentAmbient = id;
                    return previousAmbient != currentAmbient;
                }

                previousMusic = currentMusic;
                currentMusic = id;
                return previousMusic != currentMusic;
            }
            var request = new Network.AudioDataRequest
            {
                NGuid = id,
                AudioType = ____transferRefData.AmbientMusic == id ? "Ambient" : "Music"
            };
            RPCManager.SendMessage($"{AudioPlugin.Guid}.TrackRequestSent{request.ToCSV()}", LocalPlayer.Id.Value);
            return true;
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
            foreach(Dictionary<NGuid,AudioPlugin.AudioData> audioSource in AudioPlugin.Audio.Values)
            {
                if (audioSource.ContainsKey(__instance.GUID))  { found = true; break; }
            }
            if((!found) || (____clip != null))
            {
                Debug.Log("Audio Plugin: Core Audio Requested");
                return true;
            }
            Debug.Log("Audio Plugin: Custom Audio Requested");
            AudioPlugin.LoadAudioCallback(new object[] {__instance,____clip,ClipLoaded});
            return false;
        }
    }
}
