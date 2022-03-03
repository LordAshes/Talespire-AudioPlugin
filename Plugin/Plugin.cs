using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using BepInEx;
using Bounce.TaleSpire.AssetManagement;
using Bounce.Unmanaged;
using HarmonyLib;
using LordAshes;
using UnityEngine;
using UnityEngine.Networking;
using RPCPlugin;
using RPCPlugin.RPC;
using Talespire;

namespace HolloFox
{

    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(FileAccessPlugin.Guid)]
    [BepInDependency(RPCPlugin.RPCPlugin.Guid)]
    public partial class AudioPlugin : BaseUnityPlugin
    {
        static AudioPlugin _singleton;

        // Plugin info
        public const string Name = "Audio Plug-In";
        public const string Guid = "org.hollofox.plugins.audio";
        public const string Version = "1.1.0.0";

        public class AudioData
        {
            public string name { get; set; }
            public string source { get; set; }
        }

        // Variables
        internal static Dictionary<string,Dictionary<NGuid, AudioData>> Audio = new Dictionary<string,Dictionary<NGuid, AudioData>>();


        private void LoadAudio(string SubFolder)
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
                string songPath = $"file:///{file}";
                if (System.IO.Path.GetExtension(file).ToUpper()==".WWW")
                {
                    songPath = FileAccessPlugin.File.ReadAllText(file);
                }
                var name = System.IO.Path.GetFileNameWithoutExtension(file);
                var id = GenerateID(name.ToString());

                if (!Audio.ContainsKey(SubFolder)) { Audio.Add(SubFolder, new Dictionary<NGuid, AudioData>()); }
                Audio[SubFolder].Add(id, new AudioData() { name = name, source = songPath });
                Debug.Log("Audio Plugin: Registered '" + name + "' (" + songPath + ") in '"+ SubFolder+"'");
            }
        }

        /// <summary>
        /// Awake plugin
        /// </summary>
        void Awake()
        {
            Debug.Log("Audio Plugin: Active ("+this.GetType().AssemblyQualifiedName+")");
            
            _singleton = this;

            var harmony = new Harmony(Guid);
            harmony.PatchAll();

            foreach(MusicData.MusicKind folder in Enum.GetValues(typeof(MusicData.MusicKind)))
            {
                LoadAudio(folder.ToString());
            }

            Utility.PostOnMainPage(this.GetType());

            RPCManager.AddHandler($"{Guid}.TrackRequestSent",Network.RequestReceived);
            RPCManager.AddHandler($"{Guid}.TrackRequestReceived", Network.ResponseReceived);
        }

        internal static void LoadAudioCallback(object[] args) => _singleton.StartCoroutine("LoadAudioFromSource", args);

        internal static NGuid GenerateID(string id) => new NGuid(System.Guid.Parse(Utility.CreateMD5(id)));

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
    }
}
