using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using Bounce.BlobAssets;
using Bounce.TaleSpire.AssetManagement;
using Bounce.Unmanaged;
using HarmonyLib;
using LordAshes;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Networking;

namespace HolloFox
{

    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(FileAccessPlugin.Guid)]
    [BepInDependency("org.lordashes.plugins.assetdata", BepInDependency.DependencyFlags.SoftDependency)]
    public partial class AudioPlugin : BaseUnityPlugin
    {
        static AudioPlugin _singleton;

        // Plugin info
        public const string Name = "Audio Plug-In";
        public const string Guid = "org.hollofox.plugins.audio";
        public const string Version = "2.0.2.0";

        public enum ShareStyle
        {
            useOnly = 1,
            copyLocal = 3
        }

        public class AudioData
        {
            public string id { get; set; }
            public string name { get; set; }
            public string category { get; set; }
            public string source { get; set; }
        }

        public class ShareData
        {
            public ShareStyle Style = ShareStyle.useOnly;
            public List<AudioData> RemoteAudio = new List<AudioData>();
        }

        public static string pluginFolder = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        // Variables
        public static Dictionary<string, Dictionary<NGuid, AudioData>> Audio = new Dictionary<string, Dictionary<NGuid, AudioData>>{
            {"Music", new  Dictionary<NGuid, AudioData>()},
            {"Ambient", new  Dictionary<NGuid, AudioData>()}
        };
        public static ShareData Share = new ShareData();

        internal static bool subscribed = false;
        static ConfigEntry<KeyboardShortcut> triggerShare { get; set; }
        static ConfigEntry<ShareStyle> shareStyle { get; set; }

        /// <summary>
        /// Awake plugin
        /// </summary>
        void Awake()
        {
            Debug.Log("Audio Plugin: Active (" + this.GetType().AssemblyQualifiedName + ")");

            triggerShare = Config.Bind("Settings", "Share Remote Audio Library", new KeyboardShortcut(KeyCode.A, KeyCode.RightControl));
            shareStyle = Config.Bind("Settings", "Share Style", ShareStyle.useOnly);

            _singleton = this;

            var harmony = new Harmony(Guid);
            harmony.PatchAll();

            Share.Style = shareStyle.Value;
            foreach (MusicData.MusicKind folder in Enum.GetValues(typeof(MusicData.MusicKind)))
            {
                LoadAudio(folder.ToString(), ref Share.RemoteAudio);
            }

            SoftDependency.Invoke("LordAshes.AssetDataPlugin, AssetDataPlugin", "SubscribeViaReflection", new object[] { AudioPlugin.Guid, "HolloFox.AudioPlugin, AudioPlugin", "AudioRequestHandler" });
            SoftDependency.Invoke("LordAshes.AssetDataPlugin, AssetDataPlugin", "SubscribeViaReflection", new object[] { AudioPlugin.Guid + ".Register", "HolloFox.AudioPlugin, AudioPlugin", "AudioRequestHandler" });

            Utility.PostOnMainPage(this.GetType());
        }

        void Update()
        {
            if (Utility.StrictKeyCheck(triggerShare.Value))
            {
                if (Share.RemoteAudio.Count > 0)
                {
                    SoftDependency.InvokeEx("LordAshes.AssetDataPlugin, AssetDataPlugin", "SendInfo", new object[] { AudioPlugin.Guid + ".Register", JsonConvert.SerializeObject(Share) });
                }
            }
        }

        public static void AudioRequestHandler(string action, string identity, string key, object previous, object value)
        {
            switch(key)
            {
                case AudioPlugin.Guid + ".Register":
                  // Register Audio
                  Debug.Log("Audio Plugin: Registering Remote Library");
                  ShareData shareData = JsonConvert.DeserializeObject<ShareData>(value.ToString());
                  ShareStyle shareSetting = shareData.Style & shareStyle.Value;
                  Debug.Log("Audio Plugin: GM Set " + shareData.Style + ", Player Set " + shareStyle.Value+", Using "+shareSetting);
                  foreach (AudioData audio in shareData.RemoteAudio)
                  {
                      if (shareSetting == ShareStyle.copyLocal)
                      {
                          Debug.Log("Audio Plugin: Creating " + pluginFolder + "/CustomData/Audio/" + audio.category + "/" + audio.name + ".www");
                          FileAccessPlugin.File.WriteAllText(pluginFolder + "/CustomData/Audio/" + audio.category + "/" + audio.name + ".www", audio.source);
                      }
                      RegisterAudioSource(audio);
                  }
                  break;
                default:
                  // Play Audio
                  Debug.Log("Audio Plugin: Remote Audio Request For " + value);
                  string[] parts = value.ToString().Split('@');
                  if ((parts[0] == CampaignSessionManager.GetPlayerName(LocalPlayer.Id)) || (parts[0] == ""))
                  {
                    _singleton.StartCoroutine("PlayAudio", new object[] { parts[1] });
                  }
                  break;
            }
        }

        internal static void LoadAudioCallback(object[] args) => _singleton.StartCoroutine("LoadAudioFromSource", args);

        internal static NGuid GenerateID(string id) => new NGuid(System.Guid.Parse(Utility.CreateMD5(id)));

        private void LoadAudio(string SubFolder, ref List<AudioData> remoteAudio)
        {
            var files = FileAccessPlugin.File.Find($"CustomData\\Audio\\{SubFolder}")
                        .Where(f =>
                            f.EndsWith(".mp3") ||
                            f.EndsWith(".aif") ||
                            f.EndsWith(".wav") ||
                            f.EndsWith(".ogg") ||
                            f.EndsWith(".www")
                        );

            foreach (var file in files)
            {
                AudioData audio = new AudioData() 
                { 
                    id = GenerateID(System.IO.Path.GetFileNameWithoutExtension(file)).ToString(), 
                    name = System.IO.Path.GetFileNameWithoutExtension(file), 
                    category = SubFolder, 
                    source = ((System.IO.Path.GetExtension(file).ToUpper() == ".WWW") ? FileAccessPlugin.File.ReadAllText(file) : $"file:///{file}")  
                };

                if (!Audio.ContainsKey(SubFolder)) { Audio.Add(SubFolder, new Dictionary<NGuid, AudioData>()); }
                NGuid id = new NGuid(audio.id);
                if (!Audio[SubFolder].ContainsKey(id))
                {
                    Audio[SubFolder].Add(new NGuid(audio.id), audio);
                }
                else
                {
                    Debug.LogWarning("Audio Plugin: Duplicated Id '"+id.ToString()+"' From '"+Convert.ToString(audio.name)+"' ("+Convert.ToString(audio.source)+")");
                }
                if(System.IO.Path.GetExtension(file).ToUpper() == ".WWW") { remoteAudio.Add(audio); }
                Debug.Log("Audio Plugin: Registered '" + name + "' (" + audio.source + ") in '" + SubFolder + "'");
            }
        }

        private static void RegisterAudioSource(AudioData audio)
        {
            NGuid id = new NGuid(audio.id);
            if (!Audio.ContainsKey(audio.category)) { Audio.Add(audio.category, new Dictionary<NGuid, AudioData>()); }
            if (!Audio[audio.category].ContainsKey(id))
            {
                Audio[audio.category].Add(id, audio);

                MusicData.MusicKind kind = MusicData.MusicKind.Music;
                foreach (MusicData.MusicKind kindType in Enum.GetValues(typeof(MusicData.MusicKind)))
                {
                    if (kindType.ToString() == audio.category) { kind = kindType; break; }
                }
                var builder = new BlobBuilder(Allocator.Persistent);
                ref var root = ref builder.ConstructRoot<MusicData>();
                MusicData.Construct(builder, ref root, id, id, audio.name, audio.name, new string[] { }, "", audio.name, kind);
                var x = builder.CreateBlobAssetReference<MusicData>(Allocator.Persistent);
                AssetDb.Music.TryAdd(id, x.TakeView());

                Debug.Log("Audio Plugin: Registered '" + audio.name + "' (" + audio.source + ") in '" + audio.category + "'");
            }
            else
            {
                Debug.Log("Audio Plugin: Ignoring Duplicate '" + audio.name + "' (" + audio.source + ") in '" + audio.category + "'");
            }
        }

        IEnumerator LoadAudioFromSource(object[] args)
        {
            var __instance = (AtmosphereManager.LoadedAudioClip)args[0];
            var ____clip = (AudioClip)args[1];
            var ClipLoaded = (System.Action<AtmosphereManager.LoadedAudioClip, AudioClip>)args[2];

            AudioData source = null;
            foreach(Dictionary<NGuid,AudioData> audioSource in Audio.Values)
            {
                if (audioSource.ContainsKey(__instance.GUID)) { source = audioSource[__instance.GUID]; break; }
            }

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(source.source, AudioType.UNKNOWN))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.Log("Audio Plugin: Failure To Load ...");
                    Debug.Log(www.error);
                }
                else
                {
                    ____clip = DownloadHandlerAudioClip.GetContent(www);
                }
            }
            Debug.Log("Audio Plugin: Loaded Clip '" + source.name + "' (" + source.source + ")");
            ClipLoaded(__instance, ____clip);
        }

        IEnumerator PlayAudio(object[] inputs)
        {
            string sourceName = (string)inputs[0];
            if (!sourceName.Contains("://")) { sourceName = $"file:///" + pluginFolder + "/CustomData/Audio/" + sourceName; }
            Debug.Log($"Audio Plugin: Requested '{sourceName}'...");
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(sourceName, AudioType.UNKNOWN))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.Log($"Audio Plugin: Failure To Load '{sourceName}' ...");
                    Debug.Log(www.error);
                }
                else
                {
                    GameObject speaker = GameObject.Find("AudioSpeakerForRemoteRequests");
                    if (speaker == null)
                    {
                        speaker = new GameObject();
                        speaker.name = "AudioSpeakerForRemoteRequests";
                        speaker.AddComponent<AudioSource>();
                    }
                    AudioSource player = speaker.GetComponent<AudioSource>();
                    player.clip = DownloadHandlerAudioClip.GetContent(www);
                    Debug.Log($"Audio Plugin: Playing '{sourceName}'...");
                    player.Play();
                }
            }
        }
    }
}
