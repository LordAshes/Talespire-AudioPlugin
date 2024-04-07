using BepInEx;
using Bounce.TaleSpire.AssetManagement;
using Bounce.Unmanaged;
using Bounce.UnsafeViews;
using DataModel;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleSpire.Atmosphere;
using TaleSpire.ContentManagement;
using TMPro;
using Unity.Collections;

namespace LordAshes
{
    public partial class AudioPlugin : BaseUnityPlugin
    {
        public class MyCustomMusicContentProvider : ContentManager.IContentProvider, ContentManager.IProvidesMusicDataContent, ContentManager.IProvidesAudioClipContent
        {
            public static NativeHashMap<ContentGuid, (InternedContentAddress address, UnsafeView<MusicDataV0> data, string name)> Music;

            public MyCustomMusicContentProvider()
            {
                Music = new NativeHashMap<ContentGuid, (InternedContentAddress, UnsafeView<MusicDataV0>, string)>(128, Allocator.Persistent);

                NGuid.TryParse(Utility.GuidFromString(AudioPlugin.Guid).ToString(), out var packIdNguid);
                var packId = new ContentAddress.Packed.SemiInternedPackId(new SourceLocalPackId(packIdNguid.Data));
                var packSource = (InternedPackSource)typeof(InternedPackSource).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(PackSourceKind) }, null)!.Invoke(new object[] { PackSourceKind.Unknown });
                var internedPackId = new InternedPackId(packSource, packId.SourceLocalPackId, new MD5());

                using var md5 = System.Security.Cryptography.MD5.Create();
                foreach (MusicDataV0.MusicKind kind in Enum.GetValues(typeof(MusicDataV0.MusicKind)))
                {
                    for (var i = 0; i < customAudioSources[kind].Count(); i++)
                    {
                        var id = new NGuid(md5.ComputeHash(Encoding.Default.GetBytes(customAudioSources[kind][i])));
                        ref var singleTrack = ref BSR.Builder<MusicDataV0>.Create(Allocator.Persistent, out var builder);
                        var contentId = new ContentGuid(id);
                        var address = ContentAddress.CreateFromParts(PackSourceKind.Unknown, "".AsSpan(), packId.SourceLocalPackId, contentId);

                        MusicDataV0.Construct(contentId, ref builder, ref singleTrack, address, customAudioSources[kind][i], "", Array.Empty<string>(), kind);
                        var musicData = builder.Complete(Allocator.Persistent).TakeView();
                        Music.TryAdd(musicData.Value.Id, (new InternedContentAddress(internedPackId, musicData.Value.Address.ContentRef), musicData, customAudioSources[kind][i]));
                    }
                }
            }

            /// <inheritdoc />
            public void FetchAudioClip(ContentManager.Destination contentDestination, in InternedContentAddress contentAddress)
            {
                ProcessAudioClipLoad(Music, contentDestination, in contentAddress);
            }

            /// <inheritdoc />
            public void FetchMusicData(ContentManager.Destination contentDestination,
                in InternedContentAddress contentAddress)
            {
                if (Music.TryGetValue(contentAddress.ContentRef.AsContentId, out var data))
                {
                    ContentManager.TryDeliverContent(contentDestination, in data.data);
                }
                else
                {
                    ContentManager.TryDeliverFailure(contentDestination, ContentLoadFailureReason.ContentNotFoundInPack);
                }
            }
        }

        [HarmonyPatch(typeof(InternalPackManager), "OnInstanceSetup")]
        public class OnSetupPatches
        {
            static void Postfix()
            {
                ContentManager.RegisterContentProvider((ContentProviderKind)100, new MyCustomMusicContentProvider());
            }
        }

        [HarmonyPatch(typeof(ContentManager), "ProviderKindToPackSourceKind")]
        public class PatchProviderLookup
        {
            static bool Prefix(ref PackSourceKind __result, ContentProviderKind providerKind)
            {
                switch (providerKind)
                {
                    case ContentProviderKind.Internal:
                        __result = PackSourceKind.Internal;
                        break;
                    case ContentProviderKind.BrHeroForge:
                        __result = PackSourceKind.BrHeroForge;
                        break;
                    case ContentProviderKind.BrModIo:
                        __result = PackSourceKind.BrModIo;
                        break;
                    case ContentProviderKind.Repository:
                        __result = PackSourceKind.Repository;
                        break;
                    case ContentProviderKind.Unmanaged:
                        __result = PackSourceKind.Unmanaged;
                        break;
                    case ContentProviderKind.Https:
                        __result = PackSourceKind.Https;
                        break;
                    default:
                        if ((int)providerKind == 100)
                        {
                            __result = PackSourceKind.Unknown;
                        }

                        break;
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(UI_MusicSelectionDropdown), "OnEnable")]
        public class OnEnablePatches
        {
            static void Postfix(ref TMP_Dropdown ____dropdown, MusicDataV0.MusicKind ____type, ref List<InternedContentAddress> ____trackList)
            {
                ProcessDropdownCreation(ref ____dropdown, ____type, ref ____trackList);
            }
        }

        [HarmonyPatch(typeof(UI_MusicSelectionDropdown), "SelectIndex")]
        public class OnSelectIndex
        {
            static bool Prefix(int index, ref TMP_Dropdown ____dropdown, MusicDataV0.MusicKind ____type, ref List<InternedContentAddress> ____trackList)
            {
                return ProcessDropdownSelection(index, ref ____dropdown, ____type, ref ____trackList);
            }
        }

        [HarmonyPatch(typeof(AtmosphereReferenceData), nameof(AtmosphereReferenceData.SetAtmosphereData))]
        public class PatchReferenceData
        {
            static void Postfix(AtmosphereReferenceData __instance, in AtmosphereData data)
            {
                if (MyCustomMusicContentProvider.Music.TryGetValue(data.music, out var musicData))
                {
                    __instance.Music = musicData.address;
                }

                if (MyCustomMusicContentProvider.Music.TryGetValue(data.ambientMusic, out var ambientData))
                {
                    __instance.AmbientMusic = ambientData.address;
                }
            }
        }
    }
}
