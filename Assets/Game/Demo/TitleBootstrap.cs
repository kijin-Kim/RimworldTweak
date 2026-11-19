using System;
using System.IO;
using RimworldTweak.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RimworldTweak.Demo
{
    public sealed class TitleBootstrap : MonoBehaviour
    {
        public const string TitleScene = "Title";
        public const string PlayScene = "SimulationSandbox";
        public VisualCatalog Visuals;

        // Single-scene loads destroy the caller. Only the user's choice crosses the boundary.
        internal sealed class Entry
        {
            public string StorageRoot;
            public bool DevelopmentUi;
            public string SlotId;
            public string LoadError;
        }

        private static Entry nextPlay, nextTitle;
        private bool initialized, transitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEntries()
        {
            nextTitle = null;
            nextPlay = null;
        }

        private void Start()
        {
            if (initialized) return;
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--smoke-output") >= 0 || Array.IndexOf(args, "--ux-output") >= 0)
            {
                SceneManager.LoadScene(PlayScene, LoadSceneMode.Single);
                return;
            }
            try { StartInteractive(args); }
            catch (Exception error)
            {
                Debug.LogError("Title startup failed: " + error.Message);
                Application.Quit(2);
            }
        }

        internal static string StorageRootFrom(string[] args)
        {
            int index = Array.IndexOf(args, "--storage-root");
            if (index < 0) return null;
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) ||
                args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("--storage-root requires a directory path.");
            return Path.GetFullPath(args[index + 1]);
        }

        public void StartInteractive(string[] args)
        {
            if (initialized) return;
            var entry = nextTitle ?? new Entry {
                StorageRoot = StorageRootFrom(args),
                DevelopmentUi = Array.IndexOf(args, "--development-ui") >= 0
            };
            nextTitle = null;
            var menu = gameObject.AddComponent<TitleMenu>();
            menu.Initialize(entry.StorageRoot ?? Application.persistentDataPath, Visuals, slotId => {
                if (transitioning) return;
                transitioning = true;
                nextPlay = new Entry {
                    StorageRoot = entry.StorageRoot, DevelopmentUi = entry.DevelopmentUi, SlotId = slotId
                };
                SceneManager.LoadScene(PlayScene, LoadSceneMode.Single);
            }, () => Application.Quit());
            initialized = true;
            if (!string.IsNullOrEmpty(entry.LoadError)) menu.ShowLoadError(entry.LoadError);
        }

        internal static Entry TakePlayEntry()
        {
            var entry = nextPlay;
            nextPlay = null;
            return entry;
        }

        internal static void ReturnToTitle(Entry entry, string loadError = null)
        {
            if (nextTitle != null) return;
            nextTitle = new Entry {
                StorageRoot = entry.StorageRoot, DevelopmentUi = entry.DevelopmentUi, LoadError = loadError
            };
            SceneManager.LoadScene(TitleScene, LoadSceneMode.Single);
        }
    }
}
