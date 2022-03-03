using System;
using System.Collections.Generic;
using Bounce.BlobAssets;
using Bounce.TaleSpire.AssetManagement;
using Bounce.Unmanaged;
using RPCPlugin.RPC;
using Talespire;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HolloFox
{
    internal class Network
    {
        public class AudioDataRequest
        {
            public string Guid { get; set; }
            public string AudioType { get; set; }
            
            public NGuid NGuid
            {
                get => new NGuid(Guid);
                set => Guid = value.ToString();
            }

            public string ToCSV()
            {
                var o = $"{Guid},{AudioType}";
                Debug.Log(o);
                return o;
            }

            public void FromCSV(string text)
            {
                Debug.Log(text);
                var x = text.Split(',');
                Guid = x[0];
                AudioType = x[1];
            }
        }

        public class AudioDataResponse
        {
            public string Name { get; set; }
            public string Source { get; set; }
            public string Guid { get; set; }
            public string AudioType { get; set; }

            public string ToCSV()
            {
                var o = $"{Guid},{AudioType},{Name},{Source}";
                Debug.Log(o);
                return o;
            }

            public void FromCSV(string text)
            {
                Debug.Log(text);
                var x = text.Split(',');
                Guid = x[0];
                AudioType = x[1];
                Name = x[2];
                Source = x[3];
            }

            public NGuid NGuid
            {
                get => new NGuid(Guid);
                set => Guid = value.ToString();
            }
        }

        internal static string RequestReceived(string message, string arg2, SourceRole arg3)
        {
            message = message.Replace($"{AudioPlugin.Guid}.TrackRequestSent", "");
            Debug.Log(message);
            if (!LocalPlayer.Rights.CanGm) return null;
            var request = new AudioDataRequest();
            request.FromCSV(message);
            if (!AudioPlugin.Audio.ContainsKey(request.AudioType)) return null;
            if (!AudioPlugin.Audio[request.AudioType].ContainsKey(request.NGuid)) return null;
            var response = new AudioDataResponse
            {
                AudioType = request.AudioType,
                Guid = request.Guid,
                Name = AudioPlugin.Audio[request.AudioType][request.NGuid].name,
                Source = AudioPlugin.Audio[request.AudioType][request.NGuid].source
            }.ToCSV();
            RPCManager.SendMessage($"{AudioPlugin.Guid}.TrackRequestReceived{response}", LocalPlayer.Id.Value);

            return null;
        }

        internal static string ResponseReceived(string message, string arg2, SourceRole arg3)
        {
            message = message.Replace($"{AudioPlugin.Guid}.TrackRequestReceived","");
            Debug.Log(message);
            var response = new AudioDataResponse();
            response.FromCSV(message);
            if (!AudioPlugin.Audio.ContainsKey(response.AudioType))
                AudioPlugin.Audio.Add(response.AudioType, new Dictionary<NGuid, AudioPlugin.AudioData>());
            if (!AudioPlugin.Audio[response.AudioType].ContainsKey(response.NGuid))
            {
                AudioPlugin.Audio[response.AudioType].Add(response.NGuid, new AudioPlugin.AudioData
                {
                    source = response.Source,
                    name = response.Name
                });
                if (!AssetDb.Music.TryGetValue(response.NGuid, out _))
                {
                    var builder = new BlobBuilder(Allocator.Persistent);
                    var kind = response.AudioType == "Music" ? MusicData.MusicKind.Music : MusicData.MusicKind.Ambient;
                    ref var root = ref builder.ConstructRoot<MusicData>();
                    MusicData.Construct(builder, ref root, response.NGuid, response.NGuid, response.Name, response.Name,
                        new string[] { }, "", response.Name, kind);
                    var x = builder.CreateBlobAssetReference<MusicData>(Allocator.Persistent);
                    AssetDb.Music.TryAdd(response.NGuid, x.TakeView());
                }
                AtmosphereManager.Instance.TryPlayMusic(response.NGuid);
            }
            return null;
        }
    }
}
