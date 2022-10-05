using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace HolloFox
{
    public partial class AudioPlugin : BaseUnityPlugin
    {
        public static class Utility
        {
            public static string CreateMD5(string input)
            {
                using (var md5 = MD5.Create())
                {
                    var inputBytes = Encoding.ASCII.GetBytes(input);
                    var hashBytes = md5.ComputeHash(inputBytes);
                    var sb = new StringBuilder();
                    for (var i = 0; i < hashBytes.Length; i++) sb.Append(hashBytes[i].ToString("X2"));
                    return sb.ToString();
                }
            }

            public static JsonSerializerSettings options = new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture
            };

            /// <summary>
            /// Method to properly evaluate shortcut keys. 
            /// </summary>
            /// <param name="check"></param>
            /// <returns></returns>
            public static bool StrictKeyCheck(KeyboardShortcut check)
            {
                if (!check.IsUp())
                {
                    return false;
                }

                foreach (KeyCode modifier in new KeyCode[]
                         {
                             KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftControl, KeyCode.RightControl,
                             KeyCode.LeftShift, KeyCode.RightShift
                         })
                {
                    if (Input.GetKey(modifier) != check.Modifiers.Contains(modifier))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}