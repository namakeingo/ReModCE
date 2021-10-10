﻿using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using VRC.UI.Core;
using Object = UnityEngine.Object;

namespace ReModCE.Loader
{
    public static class BuildInfo
    {
        public const string Name = "ReModCE";
        public const string Author = "Requi, FenrixTheFox";
        public const string Company = null;
        public const string Version = "1.0.0.0";
        public const string DownloadLink = "https://github.com/RequiDev/ReModCE/releases/latest/";
    }

    internal static class GitHubInfo
    {
        public const string Author = "RequiDev";
        public const string Repository = "ReModCE";
        public const string Version = "latest";
    }

    public class ReLoader : MelonMod
    {
        private Action _onApplicationStart;
        private Action _onUiManagerInit;
        private Action _onFixedUpdate;
        private Action _onUpdate;
        private Action _onGUI;
        private Action _onApplicationQuit;
        private Action _onLateUpdate;
        private Action _onPreferencesLoaded;
        private Action _onPreferencesSaved;

        private Action<int, string> _onSceneWasLoaded;
        private Action<int, string> _onSceneWasInitialized;
        public override void OnApplicationStart()
        {
            var category = MelonPreferences.CreateCategory("ReModCE");
            var paranoidMode = category.CreateEntry("ParanoidMode", false, "Paranoid Mode",
                "If enabled ReModCE will not automatically download the latest version from GitHub. Manual update will be required.",
                true);

            using var sha256 = SHA256.Create();

            byte[] bytes = null;
            if (File.Exists("ReModCE.dll"))
            {
                bytes = File.ReadAllBytes("ReModCE.dll");
            }

            using var wc = new WebClient
            {
                Headers =
                {
                    ["User-Agent"] =
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:87.0) Gecko/20100101 Firefox/87.0"
                }
            };
            var latestBytes = wc.DownloadData($"https://github.com/{GitHubInfo.Author}/{GitHubInfo.Repository}/releases/{GitHubInfo.Version}/download/ReModCE.dll");
            if (bytes == null)
            {
                if (latestBytes == null)
                {
                    MelonLogger.Error($"No local file exists and unable to download latest version from GitHub. ReModCE will not load!");
                    return;
                }
                MelonLogger.Warning($"Couldn't find ReModCE.dll on disk. Downloading latest version from GitHub.");
                bytes = latestBytes;
                File.WriteAllBytes("ReModCE.dll", bytes);
            }

#if !DEBUG
            var latestHash = ComputeHash(sha256, latestBytes);
            var currentHash = ComputeHash(sha256, bytes);

            if (latestHash != currentHash)
            {
                if (paranoidMode.Value)
                {
                    MelonLogger.Msg(ConsoleColor.Cyan,
                        $"There is a new version of ReModCE available. You can either delete the \"ReModCE.dll\" from your VRChat directory or go to https://github.com/{GitHubInfo.Author}/{GitHubInfo.Repository}/releases/latest/ and download the latest version.");
                }
                else
                {
                    bytes = latestBytes;
                    File.WriteAllBytes("ReModCE.dll", bytes);
                    MelonLogger.Msg(ConsoleColor.Green, $"Updated ReModCE to latest version.");
                }
            }
#endif

            Assembly assembly;
            try
            {
                assembly = Assembly.Load(bytes);
            }
            catch (BadImageFormatException e)
            {
                MelonLogger.Error($"Couldn't load specified image: {e}");
                return;
            }

            IEnumerable<Type> types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null);
            }

            var remodClass = types.FirstOrDefault(type => type.Name == "ReModCE");
            if (remodClass == null)
            {
                MelonLogger.Error($"Couldn't find ReModCE class in assembly. ReModCE won't load.");
                return;
            }

            var methods = remodClass.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var m in methods)
            {
                var parameters = m.GetParameters();
                switch (m.Name)
                {
                    case nameof(OnApplicationStart) when parameters.Length == 0:
                        _onApplicationStart = (Action)Delegate.CreateDelegate(typeof(Action), m);
                        break;
                    case nameof(OnApplicationQuit) when parameters.Length == 0:
                        _onApplicationQuit = (Action)Delegate.CreateDelegate(typeof(Action), m);
                        break;
                    case nameof(OnSceneWasLoaded) when parameters.Length == 2 && parameters[0].ParameterType == typeof(int) && parameters[1].ParameterType == typeof(string):
                        _onSceneWasLoaded = (Action<int, string>)Delegate.CreateDelegate(typeof(Action<int, string>), m);
                        break;
                    case nameof(OnSceneWasInitialized) when parameters.Length == 2 && parameters[0].ParameterType == typeof(int) && parameters[1].ParameterType == typeof(string):
                        _onSceneWasInitialized = (Action<int, string>)Delegate.CreateDelegate(typeof(Action<int, string>), m);
                        break;
                    case nameof(OnUpdate) when parameters.Length == 0:
                        _onUpdate = (Action)Delegate.CreateDelegate(typeof(Action), m);
                        break;
                    case nameof(OnUiManagerInit) when parameters.Length == 0:
                        _onUiManagerInit = (Action)Delegate.CreateDelegate(typeof(Action), m);
                        break;
                    case nameof(OnGUI) when parameters.Length == 0:
                        _onGUI = (Action)Delegate.CreateDelegate(typeof(Action), m);
                        break;
                    case nameof(OnLateUpdate) when parameters.Length == 0:
                        _onLateUpdate = (Action)Delegate.CreateDelegate(typeof(Action), m);
                        break;
                    case nameof(OnFixedUpdate) when parameters.Length == 0:
                        _onFixedUpdate = (Action)Delegate.CreateDelegate(typeof(Action), m);
                        break;
                    case nameof(OnPreferencesLoaded) when parameters.Length == 0:
                        _onPreferencesLoaded = (Action)Delegate.CreateDelegate(typeof(Action), m);
                        break;
                    case nameof(OnPreferencesSaved) when parameters.Length == 0:
                        _onPreferencesSaved = (Action)Delegate.CreateDelegate(typeof(Action), m);
                        break;
                }
            }

            MelonCoroutines.Start(WaitForUiManager());
            _onApplicationStart();
        }

        public void OnUiManagerInit()
        {
            _onUiManagerInit();
        }

        public override void OnFixedUpdate()
        {
            _onFixedUpdate();
        }

        public override void OnUpdate()
        {
            _onUpdate();
        }

        public override void OnLateUpdate()
        {
            _onLateUpdate();
        }

        public override void OnGUI()
        {
            _onGUI();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            _onSceneWasLoaded(buildIndex, sceneName);
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            _onSceneWasInitialized(buildIndex, sceneName);
        }

        public override void OnApplicationQuit()
        {
            _onApplicationQuit();
        }

        public override void OnPreferencesLoaded()
        {
            _onPreferencesLoaded();
        }

        public override void OnPreferencesSaved()
        {
            _onPreferencesSaved();
        }

        private IEnumerator WaitForUiManager()
        {
            while (UIManager.Instance == null) yield return null;
            while (Object.FindObjectOfType<VRC.UI.Elements.QuickMenu>() == null) yield return null;

            OnUiManagerInit();
        }

        private static string ComputeHash(HashAlgorithm sha256, byte[] data)
        {
            var bytes = sha256.ComputeHash(data);
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
