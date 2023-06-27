using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;
using System.IO;
using SingularityGroup.HotReload.Newtonsoft.Json;


namespace SingularityGroup.HotReload.Editor {
    
    internal class HotReloadAboutTab : HotReloadTabBase {
        private readonly OpenURLButton _seeMore = new OpenURLButton("See More", Constants.ChangelogURL);
        private readonly List<IGUIComponent>[] _contactButtons;
        private readonly OpenURLButton _buyLicenseButton;
        private readonly OpenDialogueButton _manageLicenseButton;
        private readonly OpenDialogueButton _manageAccountButton;
        public readonly OpenDialogueButton reportIssueButton = new OpenDialogueButton("Report issue", Constants.ReportIssueURL, "Report issue", "Report issue in our public issue tracker. Requires gitlab.com account (if you don't have one and are not willing to make it, please contact us by other means such as our website).", "Open in browser", "Cancel");
        public readonly OpenURLButton documentationButton = new OpenURLButton("Documentation", Constants.DocumentationURL);

        private Vector2 _changelogScroll;
        private List<ChangelogVersion> _changelog = new List<ChangelogVersion>();
        private bool _requestedChangelog;
        private int _changelogRequestAttempt;
        private string _changelogDir = Path.Combine(PackageConst.LibraryCachePath, "changelog.json");
        
        private static bool LatestChangelogLoaded(List<ChangelogVersion> changelog) {
            return changelog.Any() && changelog[0].versionNum == PackageUpdateChecker.lastRemotePackageVersion;
        }
        
        void LoadChangelog() {
            var file = new FileInfo(_changelogDir);
            if (file.Exists) {
                var bytes = File.ReadAllText(_changelogDir);
                _changelog = JsonConvert.DeserializeObject<List<ChangelogVersion>>(bytes);
            }  
        }
 
        private async Task FetchChangelog() {
            if(!_changelog.Any()) {
                await Task.Run(() => LoadChangelog());
            }
            if (_requestedChangelog || LatestChangelogLoaded(_changelog)) {
                return;
            }
            _requestedChangelog = true;
            try {
                do {
                    var changelogRequestTimeout = ExponentialBackoff.GetTimeout(_changelogRequestAttempt);
                    _changelog = await RequestHelper.FetchChangelog() ?? _changelog;
                    if (LatestChangelogLoaded(_changelog)) {
                        await Task.Run(() => {
                            Directory.CreateDirectory(PackageConst.LibraryCachePath);
                            File.WriteAllText(_changelogDir, JsonConvert.SerializeObject(_changelog));
                        });
                        Repaint();
                        return;
                    }
                    await Task.Delay(changelogRequestTimeout);
                } while (_changelogRequestAttempt++ < 1000 && !LatestChangelogLoaded(_changelog));
            } catch {
                // ignore
            } finally {
                _requestedChangelog = false;    
            }
        }
        
        public HotReloadAboutTab(HotReloadWindow window) : base(window, "Help", "_Help", "Info and support for Hot Reload for Unity.") {
            _contactButtons = new[] {
                new List<IGUIComponent> {
                    new OpenURLButton("Unity Forum", Constants.ForumURL),
                    new OpenURLButton("Contact", Constants.ContactURL),
                    reportIssueButton,
                    new OpenURLButton("Join Discord", Constants.DiscordInviteUrl)
                }
            };
            _manageLicenseButton = new OpenDialogueButton("Manage License", Constants.ManageLicenseURL, "Manage License", "Upgrade/downgrade/edit your subscription and edit payment info.", "Open in browser", "Cancel");
            _manageAccountButton = new OpenDialogueButton("Manage Account", Constants.ManageAccountURL, "Manage License", "Login with company code 'naughtycult'. Use the email you signed up with. Your initial password was sent to you by email.", "Open in browser", "Cancel");
            _buyLicenseButton = new OpenURLButton("Get License      ", Constants.ProductPurchaseURL);
        }

        string GetRelativeDate(DateTime givenDate) {
            const int second = 1;
            const int minute = 60 * second;
            const int hour = 60 * minute;
            const int day = 24 * hour;
            const int month = 30 * day;

            var ts = new TimeSpan(DateTime.UtcNow.Ticks - givenDate.Ticks);
            var delta = Math.Abs(ts.TotalSeconds);

            if (delta < 1 * minute)
                return ts.Seconds == 1 ? "one second ago" : ts.Seconds + " seconds ago";

            if (delta < 2 * minute)
                return "a minute ago";

            if (delta < 45 * minute)
                return ts.Minutes + " minutes ago";

            if (delta < 90 * minute)
                return "an hour ago";

            if (delta < 24 * hour)
                return ts.Hours + " hours ago";

            if (delta < 48 * hour)
                return "yesterday";

            if (delta < 30 * day)
                return ts.Days + " days ago";

            if (delta < 12 * month) {
                var months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
                return months <= 1 ? "one month ago" : months + " months ago";
            }
            var years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
            return years <= 1 ? "one year ago" : years + " years ago";
        }

        void RenderChangelog() {

            EditorGUILayout.Space();
            FetchChangelog().Forget();

            HotReloadPrefs.ShowChangeLog = EditorGUILayout.Foldout(HotReloadPrefs.ShowChangeLog, "Changelog", true, HotReloadWindowStyles.FoldoutStyle);
            if (!HotReloadPrefs.ShowChangeLog) {
                return;
            }
            
            {
                var maxChangeLogs = 5;
                var index = 0;
                foreach (var version in _changelog) {
                    index++;
                    if (index > maxChangeLogs) {
                        break;
                    }
                    using (new GUILayout.HorizontalScope(HotReloadWindowStyles.BoxStyle)) {
                        using (new GUILayout.VerticalScope(HotReloadWindowStyles.BoxStyle)) {
                            var tempTextString = "";

                            //version number
                            EditorGUILayout.TextArea(version.versionNum, HotReloadWindowStyles.H1TitleStyle);

                            //general info
                            if (version.generalInfo != null) {
                                EditorGUILayout.TextArea(version.generalInfo, HotReloadWindowStyles.H3TitleStyle);
                            }

                            //features
                            if (version.features != null) {
                                EditorGUILayout.TextArea("Features:", HotReloadWindowStyles.H2TitleStyle);
                                tempTextString = "";
                                foreach (var feature in version.features) {
                                    tempTextString += "• " + feature + "\n";
                                }
                                EditorGUILayout.TextArea(tempTextString, HotReloadWindowStyles.ChangelogPointerStyle);
                            }

                            //improvements
                            if (version.improvements != null) {
                                EditorGUILayout.TextArea("Improvements:", HotReloadWindowStyles.H2TitleStyle);
                                tempTextString = "";
                                foreach (var improvement in version.improvements) {
                                    tempTextString += "• " + improvement + "\n";
                                }
                                EditorGUILayout.TextArea(tempTextString, HotReloadWindowStyles.ChangelogPointerStyle);
                            }

                            //fixes
                            if (version.fixes != null) {
                                EditorGUILayout.TextArea("Fixes:", HotReloadWindowStyles.H2TitleStyle);
                                tempTextString = "";
                                foreach (var fix in version.fixes) {
                                    tempTextString += "• " + fix + "\n";
                                }
                                EditorGUILayout.TextArea(tempTextString, HotReloadWindowStyles.ChangelogPointerStyle);
                            }

                            //date
                            DateTime date;
                            if (DateTime.TryParseExact(version.date, "dd/MM/yyyy", null, DateTimeStyles.None, out date)) {
                                var relativeDate = GetRelativeDate(date);
                                GUILayout.TextArea(relativeDate, HotReloadWindowStyles.H3TitleStyle);
                            }
                        }

                    }
                    GUILayout.Space(5);
                }
            }
            
            _seeMore.OnGUI();

            GUILayout.Space(9f);
        }

        public override void OnGUI() {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox($"You are running Hot Reload for Unity version {PackageConst.Version}. ", MessageType.Info);
            EditorGUILayout.Space();
            
            var licenseRequired = !EditorCodePatcher.LoginNotRequired;
            if (licenseRequired) {
                _buyLicenseButton.OnGUI();
            }
            
            documentationButton.OnGUI();
            
            foreach (var group in _contactButtons) {
                using (new EditorGUILayout.HorizontalScope()) {
                    foreach (var button in group) {
                        button.OnGUI();
                    }
                }
            }
            EditorGUILayout.Space();

            var hasTrial = _window.runTab.TrialLicense;
            var hasPaid = _window.runTab.HasPayedLicense;
            if (hasPaid || hasTrial) {
                using(new EditorGUILayout.HorizontalScope()) {
                    if (hasPaid) {
                        _manageLicenseButton.OnGUI();
                    }
                    _manageAccountButton.OnGUI();
                }
                EditorGUILayout.Space();
            }

            try {
                RenderChangelog();
            } catch {
                // ignore
            }
        }
    }
}
