using BepInEx;
using BepInEx.Configuration;
using Bounce.TaleSpire.AssetManagement;
using Bounce.Unmanaged;
using Bounce.UnsafeViews;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using TaleSpire.ContentManagement;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace LordAshes
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(LordAshes.FileAccessPlugin.Guid)]
    public partial class AudioPlugin : BaseUnityPlugin
    {
        // Plugin info
        public const string Name = "Audio Plug-In";
        public const string Guid = "org.pluginmasters.plugins.audio";
        public const string Version = "4.0.0";
        public const string Author = "Plugin Masters";

        // Diagnostics
        public enum DiagnosticLevel
        {
            none,
            error,
            warning,
            info,
            debug,
            ultra
        }
        public enum CoreAudioInclusionSettings
        {
            first,
            alphabetical,
            last
        }

        // Configuration
        public static ConfigEntry<DiagnosticLevel> diagnostics { get; set; }
        
        public static ConfigEntry<CoreAudioInclusionSettings> coreInclusionSettings { get; set; }

        private static Dictionary<Bounce.TaleSpire.AssetManagement.MusicDataV0.MusicKind, string> virtualCoreFolders = new Dictionary<Bounce.TaleSpire.AssetManagement.MusicDataV0.MusicKind, string>();
        private static Dictionary<Bounce.TaleSpire.AssetManagement.MusicDataV0.MusicKind, string> currentFolder = new Dictionary<Bounce.TaleSpire.AssetManagement.MusicDataV0.MusicKind, string>();

        private static Dictionary<Bounce.TaleSpire.AssetManagement.MusicDataV0.MusicKind, List<string>> customAudioSources = new Dictionary<Bounce.TaleSpire.AssetManagement.MusicDataV0.MusicKind, List<string>>();
        private static Dictionary<Bounce.TaleSpire.AssetManagement.MusicDataV0.MusicKind, SortedDictionary<string, InternedContentAddress>> coreAudioSources = new Dictionary<MusicDataV0.MusicKind, SortedDictionary<string, InternedContentAddress>>();

        private static AudioPlugin _self = null;

        /// <summary>
        /// Function for initializing plugin
        /// This function is called once by TaleSpire
        /// </summary>
        void Awake()
        {
            _self = this;

            diagnostics = Config.Bind("Settings", "Diagnostic Level", DiagnosticLevel.info);
            coreInclusionSettings = Config.Bind("Settings", "Core Audio Placement", CoreAudioInclusionSettings.alphabetical);

            virtualCoreFolders[MusicDataV0.MusicKind.Ambient] = Config.Bind("Settings", "Virtual Core Ambient Files Location", "Ambient/Core").Value.ToString();
            virtualCoreFolders[MusicDataV0.MusicKind.Music] = Config.Bind("Settings", "Virtual Core Music Files Location", "Music/Core").Value.ToString();

            UnityEngine.Debug.Log(Name + ": Active. (Diagnostic Mode = " + diagnostics.Value.ToString() + ")");

            var harmony = new Harmony(Guid);
            harmony.PatchAll();

            currentFolder.Add(Bounce.TaleSpire.AssetManagement.MusicDataV0.MusicKind.Ambient, "Ambient");
            currentFolder.Add(Bounce.TaleSpire.AssetManagement.MusicDataV0.MusicKind.Music, "Music");

            coreAudioSources.Add(MusicDataV0.MusicKind.Ambient, new SortedDictionary<string, InternedContentAddress>());
            coreAudioSources.Add(MusicDataV0.MusicKind.Music, new SortedDictionary<string, InternedContentAddress>());

            GetAllCustomSongs(ref customAudioSources);

            Utility.PostOnMainPage(this.GetType());
        }

        public void GetAllCustomSongs(ref Dictionary<Bounce.TaleSpire.AssetManagement.MusicDataV0.MusicKind, List<string>> customAudioSources)
        {
            customAudioSources.Add(MusicDataV0.MusicKind.Ambient, FileAccessPlugin.File.Find("/Audio/Ambient/").Where(a => ".ACC|.MP3|.OGG|.WAV|.WWW".Contains(System.IO.Path.GetExtension(a).ToUpper())).ToList<string>());
            customAudioSources.Add(MusicDataV0.MusicKind.Music, FileAccessPlugin.File.Find("/Audio/Music/").Where(a => ".ACC|.MP3|.OGG|.WAV|.WWW".Contains(System.IO.Path.GetExtension(a).ToUpper())).ToList<string>());

            foreach (MusicDataV0.MusicKind kind in Enum.GetValues(typeof(MusicDataV0.MusicKind)))
            {
                int count = customAudioSources[kind].Count();
                for (var i = 0; i < count; i++)
                {
                    customAudioSources[kind][i] = customAudioSources[kind][i].Replace("\\", "/").Substring(customAudioSources[kind][i].IndexOf("/Audio/")+"/Audio/".Length);
                    customAudioSources[kind][i] = customAudioSources[kind][i].Substring(0, customAudioSources[kind][i].Replace("\\","/").LastIndexOf("/")) + "/" + System.IO.Path.GetFileNameWithoutExtension(customAudioSources[kind][i]) + "♫";
                    string[] path = customAudioSources[kind][i].Substring(0, customAudioSources[kind][i].Replace("\\","/").LastIndexOf("/")).Split('/');
                    string folder = path[0];
                    for (int p=1; p<path.Length; p++)
                    {
                        if (!customAudioSources[kind].Contains(folder + "/[" + path[p] + "]►")) { customAudioSources[kind].Add(folder + "/[" + path[p] + "]►"); }
                        if (!customAudioSources[kind].Contains(folder + "/" + path[p]+"/[Back]►")) { customAudioSources[kind].Add(folder + "/" + path[p] + "/[Back]►"); }
                        folder = folder + "/" + path[p];
                    }
                }
            }

            foreach (KeyValuePair<MusicDataV0.MusicKind, string> corePath in virtualCoreFolders)
            {
                string[] path = corePath.Value.Replace("\\","/").Split("/");
                string folder = path[0];
                for (int p = 1; p < path.Length; p++)
                {
                    if (!customAudioSources[corePath.Key].Contains(folder + "/[" + path[p] + "]►")) { customAudioSources[corePath.Key].Add(folder + "/[" + path[p] + "]►"); }
                    if (!customAudioSources[corePath.Key].Contains(folder + "/" + path[p] + "/[Back]►")) { customAudioSources[corePath.Key].Add(folder + "/" + path[p] + "/[Back]►"); }
                    folder = folder + "/" + path[p];
                }
            }

            if (diagnostics.Value >= DiagnosticLevel.debug)
            {
                foreach (MusicDataV0.MusicKind kind in Enum.GetValues(typeof(MusicDataV0.MusicKind)))
                {
                    for (var i = 0; i < customAudioSources[kind].Count(); i++)
                    {
                        Debug.Log(Name + ": Adding Audio File. Type '" + kind + "', Location '" + customAudioSources[kind][i] + "'");
                    }
                }
            }
        }

        public static void RebuildAudioMenu(ref TMP_Dropdown ____dropdown, MusicDataV0.MusicKind ____type, ref List<InternedContentAddress> ____trackList)
        {
            if (diagnostics.Value >= DiagnosticLevel.info) { Debug.Log(Name + ": Building Audio Options"); }

            var currentAtmo = AtmosphereManager.GetCurrentReferenceData();
            using var keyValueArrays = MyCustomMusicContentProvider.Music.GetKeyValueArrays(Allocator.TempJob);
            var names = new List<string>();
            NativeArray<ContentGuid> keys;
            NativeArray<(InternedContentAddress address, UnsafeView<MusicDataV0> data, string name)> vals;
            keyValueArrays.Deconstruct(out keys, out vals);
            SortedDictionary<string, InternedContentAddress> nameToTrackListMapping = new SortedDictionary<string, InternedContentAddress>();

            if (coreAudioSources[____type].Count()==0)
            {
                if (diagnostics.Value >= DiagnosticLevel.debug) { Debug.Log(Name + ": Storing " + (____dropdown.options.Count() - 1) + " Core " + ____type.ToString() + " Audio Options"); }
                for (int i = 1; i < ____dropdown.options.Count(); i++)
                {
                    if (diagnostics.Value >= DiagnosticLevel.ultra) {  Debug.Log(Name + ": Storing Item '" + ____dropdown.options[i].text + "'");}
                    coreAudioSources[____type].Add(____dropdown.options[i].text, ____trackList[i - 1]);
                }
            }

            if (diagnostics.Value >= DiagnosticLevel.debug) { Debug.Log(Name + ": Clear Audio Options"); }

            ____trackList.Clear();
            ____dropdown.ClearOptions();
            if (____type == MusicDataV0.MusicKind.Ambient) { ____dropdown.AddOptions(new List<string> { "Default" }); } else { ____dropdown.AddOptions(new List<string> { "Silence" }); }

            if (diagnostics.Value >= DiagnosticLevel.debug) { Debug.Log(Name + ": Adding Custom Links (" + (keys.Length > 0) + ") For " + currentFolder[____type]); }

            if (keys.Length > 0)
            {
                for (var i = 0; i < keys.Length; i++)
                {
                    var value = vals[i];
                    if (value.data.Value.Kind == ____type && value.name.Substring(0, value.name.Replace("\\", "/").LastIndexOf("/")) == currentFolder[____type] && value.name.EndsWith("►"))
                    {
                        if (diagnostics.Value >= DiagnosticLevel.ultra) { Debug.Log(Name + ": Adding Link '" + value.name + "' For " + currentFolder[____type]); }
                        nameToTrackListMapping.Add(value.name.Substring(value.name.Replace("\\", "/").LastIndexOf("/") + 1), value.address);
                    }
                }
            }

            if (diagnostics.Value >= DiagnosticLevel.debug) { Debug.Log(Name + ": Adding Core Audio Options (" + (currentFolder[____type] == virtualCoreFolders[____type]) + ")  For " + currentFolder[____type]); }

            string prefix = (coreInclusionSettings.Value == CoreAudioInclusionSettings.first) ? "♪" : "";
            if (currentFolder[____type] == virtualCoreFolders[____type])
            {
                for (int i = 0; i < coreAudioSources[____type].Keys.Count(); i++)
                {
                    if (diagnostics.Value >= DiagnosticLevel.ultra) { Debug.Log(Name + ": Adding Core Item '" + coreAudioSources[____type].Keys.ElementAt(i) + "' For " + currentFolder[____type]); }
                    nameToTrackListMapping.Add(prefix+coreAudioSources[____type].Keys.ElementAt(i), coreAudioSources[____type].Values.ElementAt(i));
                }
            }

            if (diagnostics.Value >= DiagnosticLevel.debug) { Debug.Log(Name + ": Adding Custom Audio Options (" + (keys.Length > 0) + ")"); }

            if (keys.Length > 0)
            {
                prefix = (coreInclusionSettings.Value == CoreAudioInclusionSettings.last) ? "♪" : "";
                for (var i = 0; i < keys.Length; i++)
                {
                    var value = vals[i];
                    if (value.data.Value.Kind == ____type && value.name.Substring(0, value.name.Replace("\\", "/").LastIndexOf("/")) == currentFolder[____type] && value.name.EndsWith("♫"))
                    {
                        if (diagnostics.Value >= DiagnosticLevel.ultra) { Debug.Log(Name + ": Adding Custom Item '" + value.name + "' For " + currentFolder[____type]); }
                        nameToTrackListMapping.Add(prefix+value.name.Substring(value.name.Replace("\\", "/").LastIndexOf("/") + 1), value.address);
                    }
                }
            }

            ____trackList.AddRange(nameToTrackListMapping.Values.ToList<InternedContentAddress>());
            ____dropdown.AddOptions(nameToTrackListMapping.Keys.ToList<string>());
        }


        [HarmonyPatch(typeof(UI_MusicSelectionDropdown), "OnEnable")]
        public class OnEnablePatches
        {
            static void Postfix(ref TMP_Dropdown ____dropdown, MusicDataV0.MusicKind ____type, ref List<InternedContentAddress> ____trackList)
            {
                RebuildAudioMenu(ref ____dropdown, ____type, ref ____trackList);   
            }
        }

        [HarmonyPatch(typeof(UI_MusicSelectionDropdown), "SelectIndex")]
        public class OnSelectIndex
        {
            static bool Prefix(int index, ref TMP_Dropdown ____dropdown, MusicDataV0.MusicKind ____type, ref List<InternedContentAddress> ____trackList)
            {
                if (diagnostics.Value >= DiagnosticLevel.info) { Debug.Log(Name + ": Selected '" + ____dropdown.options[index].text + "'"); }
                if (!____dropdown.options[index].text.EndsWith("►")) { return true; }
                if(____dropdown.options[index].text== "[Back]►")
                {
                    currentFolder[____type] = currentFolder[____type].Substring(0, currentFolder[____type].LastIndexOf("/"));
                }
                else
                {
                    currentFolder[____type] = currentFolder[____type] + "/" + ____dropdown.options[index].text.Substring(1, ____dropdown.options[index].text.Length-3);
                }
                currentFolder[____type] = currentFolder[____type].Replace("\\", "/");
                if (diagnostics.Value >= DiagnosticLevel.debug) { Debug.Log(Name + ": Current Folder Is '" + currentFolder[____type] + "'"); }
                RebuildAudioMenu(ref ____dropdown, ____type, ref ____trackList);
                return false;
            }
        }

        public class MyCustomMusicContentProvider : ContentManager.IContentProvider, ContentManager.IProvidesMusicDataContent, ContentManager.IProvidesAudioClipContent
        {
            public static NativeHashMap<ContentGuid, (InternedContentAddress address, UnsafeView<MusicDataV0> data,string name)> Music;

            /// <inheritdoc />
            public void FetchAudioClip(ContentManager.Destination contentDestination, in InternedContentAddress contentAddress)
            {
                if (Music.TryGetValue(contentAddress.ContentRef.AsContentId, out var data))
                {
                    if (diagnostics.Value >= DiagnosticLevel.ultra) { Debug.Log(data.Item2.Value.Kind.ToString() + ": " + data.Item2.Value.Name.GetString() + ": " + data.Item2.Value.Description.GetString() + ": " + data.Item2.Value.Address.ToUri()); }

                    string audio = FileAccessPlugin.File.Find("/Audio/"+data.Item2.Value.Name.GetString().Replace("♫", "").Replace("♪","")).ElementAt(0);
                    if(System.IO.Path.GetExtension(audio).ToUpper()==".WWW")
                    {
                        audio = FileAccessPlugin.File.ReadAllText(audio);
                    }
                    if (!audio.ToUpper().StartsWith("HTTP")) { audio = "file:/" + audio; }

                    using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(audio, AudioType.UNKNOWN))
                    {
                        www.SendWebRequest();
                        while (!www.isDone) { }
                        if (www.result == UnityWebRequest.Result.ConnectionError)
                        {
                            Debug.LogWarning(Name+": Failure To Load Clip '"+audio+"'");
                            Debug.LogWarning(Name+": "+www.error);
                        }
                        else
                        {
                            var clip = DownloadHandlerAudioClip.GetContent(www);
                            ContentManager.TryDeliverContent(contentDestination, clip);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning(Name+": Requested Item Address "+ contentAddress.ContentRef.AsContentId + " Not Registered.");
                    ContentManager.TryDeliverFailure(contentDestination, ContentLoadFailureReason.ContentNotFoundInPack);
                }
            }

            /// <inheritdoc />
            public void FetchMusicData(ContentManager.Destination contentDestination,
                in InternedContentAddress contentAddress)
            {
                if (Music.TryGetValue(contentAddress.ContentRef.AsContentId, out var data))
                    ContentManager.TryDeliverContent(contentDestination, in data.data);
                else
                    ContentManager.TryDeliverFailure(contentDestination,
                        ContentLoadFailureReason.ContentNotFoundInPack);
            }

            public MyCustomMusicContentProvider()
            {
                Music = new NativeHashMap<ContentGuid, (InternedContentAddress, UnsafeView<MusicDataV0>, string)>(128, Allocator.Persistent);
                NGuid.TryGenerateNewGuid(out var packIdNguid);
                var packId = new ContentAddress.Packed.SemiInternedPackId(new SourceLocalPackId(packIdNguid.Data));
                var packSource = (InternedPackSource)typeof(InternedPackSource).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(PackSourceKind) }, null)!.Invoke(new object[] { PackSourceKind.Unknown });
                var internedPackId = new InternedPackId(packSource, packId.SourceLocalPackId, new MD5());

                foreach (MusicDataV0.MusicKind kind in Enum.GetValues(typeof(MusicDataV0.MusicKind)))
                {
                    for (var i = 0; i < customAudioSources[kind].Count(); i++)
                    {
                        NGuid.TryGenerateNewGuid(out var id);
                        ref var singleTrack = ref BSR.Builder<MusicDataV0>.Create(Allocator.Persistent, out var builder);
                        var contentId = new ContentGuid(id);
                        var address = ContentAddress.CreateFromParts(PackSourceKind.Unknown, "".AsSpan(), packId.SourceLocalPackId, contentId);

                        MusicDataV0.Construct(contentId, ref builder, ref singleTrack, address, customAudioSources[kind][i], "", Array.Empty<string>(), kind);
                        var musicData = builder.Complete(Allocator.Persistent).TakeView();
                        Music.TryAdd(musicData.Value.Id, (new InternedContentAddress(internedPackId, musicData.Value.Address.ContentRef), musicData, customAudioSources[kind][i]));
                    }
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
    }
}