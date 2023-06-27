using System;
using System.IO;
using SingularityGroup.HotReload.DTO;
using UnityEditor;
using UnityEngine;
#if UNITY_2019_4_OR_NEWER
using System.Reflection;
using Unity.CodeEditor;
#endif

namespace SingularityGroup.HotReload.Editor {
    static class InstallUtility {
        const string installFlagPath = PackageConst.LibraryCachePath + "/installFlag.txt";

        public static void DebugClearInstallState() {
            File.Delete(installFlagPath);
        }

        // HandleEditorStart is only called on editor start, not on domain reload
        public static void HandleEditorStart(string updatedFromVersion) {
            var showOnStartup = HotReloadPrefs.GetShowOnStartupEnum();
            if (showOnStartup == ShowOnStartupEnum.Always || (showOnStartup == ShowOnStartupEnum.OnNewVersion && !String.IsNullOrEmpty(updatedFromVersion))) {
                HotReloadWindow.Open();
            }
            if (HotReloadPrefs.LaunchOnEditorStart) {
                HotReloadWindow.Open();
                HotReloadWindow.Current.runTab.DownloadAndRun().Forget();
            }
            
            RequestHelper.RequestEditorEventWithRetry(new Stat(StatSource.Client, StatLevel.Debug, StatFeature.Editor, StatEventType.Start)).Forget();
        }

        public static void CheckForNewInstall() {
            if(File.Exists(installFlagPath)) {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(installFlagPath));
            using(File.Create(installFlagPath)) { }
            //Avoid opening the window on domain reload
            EditorApplication.delayCall += HandleNewInstall;
        }
        
        static void HandleNewInstall() {
            HotReloadWindow.Open();
            HotReloadPrefs.AllowDisableUnityAutoRefresh = true;
            HotReloadPrefs.AllAssetChanges = true;
        }
    }
}