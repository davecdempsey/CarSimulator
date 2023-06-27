using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SingularityGroup.HotReload.DTO;
using SingularityGroup.HotReload.Editor.Cli;
using UnityEditor;
using UnityEngine;
using Task = System.Threading.Tasks.Task;
#if UNITY_2019_4_OR_NEWER
using Unity.CodeEditor;
#endif

namespace SingularityGroup.HotReload.Editor {
    internal struct LicenseErrorData {
        public readonly string description;
        public bool showBuyButton;
        public string buyButtonText;
        public readonly bool showLoginButton;
        public readonly string loginButtonText;
        public readonly bool showSupportButton;
        public readonly string supportButtonText;
        public readonly bool showManageLicenseButton;
        public readonly string manageLicenseButtonText;

        public LicenseErrorData(string description, bool showManageLicenseButton = false, string manageLicenseButtonText = "", string loginButtonText = "", bool showSupportButton = false, string supportButtonText = "", bool showBuyButton = false, string buyButtonText = "", bool showLoginButton = false) {
            this.description = description;
            this.showManageLicenseButton = showManageLicenseButton;
            this.manageLicenseButtonText = manageLicenseButtonText;
            this.loginButtonText = loginButtonText;
            this.showSupportButton = showSupportButton;
            this.supportButtonText = supportButtonText;
            this.showBuyButton = showBuyButton;
            this.buyButtonText = buyButtonText;
            this.showLoginButton = showLoginButton;
        }
    }
    
    internal class HotReloadRunTab : HotReloadTabBase {
        [CanBeNull] public static LoginStatusResponse _status { get; private set; }

        private const int SERVER_POLL_FREQUENCY_ON_STARTUP_MS = 500;
        private const int SERVER_POLL_FREQUENCY_AFTER_STARTUP_MS = 2000;

        private string _pendingEmail;
        private string _pendingPassword;
        private string _pendingPromoCode;

        private bool _requestingServerInfo;
        private bool _requestingLoginInfo;
        private bool _requestingFlushErrors;
        private bool _requestingActivatePromoCode;

        private bool _running;
        public bool Running => _running;

        private long _lastServerPoll;
        private long _lastErrorFlush;

        private Tuple<string, MessageType> _activateInfoMessage;
        private Tuple<float, string> _startupProgress;
        private float DownloadProgress => EditorCodePatcher.serverDownloader.Progress;
        private bool DownloadRequired => DownloadProgress < 1f;
        private bool DownloadStarted => EditorCodePatcher.serverDownloader.Started;
        
        private DateTime? _serverStartedAt;
        private DateTime? _serverStoppedAt;
        private DateTime? _serverRestartedAt;
        bool createdAccount;

        public bool TrialExpired => _status?.lastLicenseError == "TrialLicenseExpiredException";
        
        // Has Indie or Pro license (even if not currenctly active)
        public bool HasPayedLicense => _status != null && (_status.isIndieLicense || _status.isBusinessLicense);
        public bool TrialLicense => _status != null && (_status?.isTrial == true);

        private Vector2 _patchedMethodsScrollPos;

        bool Starting => _startupProgress != null && _startupProgress.Item1 < 1f;
        
        private string promoCodeError;
        private MessageType promoCodeErrorType;
        private bool promoCodeActivatedThisSession;

        public HotReloadRunTab(HotReloadWindow window) : base(window, "Run", "forward", "Run and monitor the current Hot Reload session.") { }

        public override void OnGUI() {
            EditorGUILayout.Space();
            using(new EditorGUILayout.VerticalScope()) {
                OnGUICore();
            }
        }
        
        void OnGUICore() {
            // Migration for new versions
            if (TrialLicense || HasPayedLicense) {
                HotReloadPrefs.FirstLogin = false;
            }

            var loginNotRequired = EditorCodePatcher.LoginNotRequired;
            var loginRequired = !loginNotRequired;
            if (loginRequired && createdAccount && _status?.isLicensed == true) {
                RenderCreatedAccountInfo();
            }

            var renderConsumptions = _running && !Starting && _status?.isLicensed != true && _status?.isFree != true && loginRequired;
            if (renderConsumptions) {
                RenderConsumption();
            }

            var renderFirstLogin = HotReloadPrefs.FirstLogin && !_running && !Starting && loginRequired && !_requestingDownloadAndRun;
            if (renderFirstLogin) {
                RenderFirstLoginScreen();
            } else if (renderConsumptions) {
                RenderLicenseInfo();
                RenderLicenseButtons();
            }

            var renderStart = !_running && !Starting && !renderFirstLogin && !_requestingDownloadAndRun;
            if (renderStart) {
                RenderStart();
            }

            if (_requestingDownloadAndRun || _running || Starting) {
                RenderProgressBar();
            }
            if (_running && !Starting) {
                RenderChanges();
            }
            // At the end to not fuck up rendering https://answers.unity.com/questions/400454/argumentexception-getting-control-0s-position-in-a-1.html
            var e = Event.current;
            if (renderStart && e.type == EventType.KeyUp
                && (e.keyCode == KeyCode.Return
                    || e.keyCode == KeyCode.KeypadEnter)
            ) {
                DownloadAndRun().Forget();
            }
        }

        public void RenderConsumption() {
            if (_status == null || _status.isFree) {
                return;
            }
            EditorGUILayout.LabelField($"Using Free license", HotReloadWindowStyles.MediumMiddleCenterStyle);
            EditorGUILayout.Space();
            if (_status.consumptionsUnavailableReason == ConsumptionsUnavailableReason.NetworkUnreachable) {
                HotReloadGUIHelper.HelpBox("Free charges unavailabe. Please check your internet connection.", MessageType.Warning, fontSize: 11);
            } else if (_status.consumptionsUnavailableReason == ConsumptionsUnavailableReason.UnrecoverableError) {
                HotReloadGUIHelper.HelpBox("Free charges unavailabe. Please contact support if the issue persists.", MessageType.Error, fontSize: 11);
            } else if (_status.freeSessionFinished) {
                var now = DateTime.UtcNow;
                var sessionRefreshesAt = (now.AddDays(1).Date - now).Add(TimeSpan.FromMinutes(5));
                var sessionRefreshString = $"Next Free Session: {(sessionRefreshesAt.Hours > 0 ? $"{sessionRefreshesAt.Hours}h " : "")}{sessionRefreshesAt.Minutes}min";
                HotReloadGUIHelper.HelpBox(sessionRefreshString, MessageType.Warning, fontSize: 11);
            } else if (_status.freeSessionRunning && _status.freeSessionEndTime != null) {
                var sessionEndsAt = _status.freeSessionEndTime.Value - DateTime.Now;
                var sessionString = $"Daily Free Session: {(sessionEndsAt.Hours > 0 ? $"{sessionEndsAt.Hours}h " : "")}{sessionEndsAt.Minutes}min Left";
                EditorGUILayout.LabelField(sessionString, HotReloadWindowStyles.H3TitleStyle);
                EditorGUILayout.Space();
            } else if (_status.freeSessionEndTime == null) {
                EditorGUILayout.LabelField($"Daily Free Session: Make code changes to start", HotReloadWindowStyles.H3TitleStyle);
                EditorGUILayout.Space();
            }
        }

        static void RenderCreatedAccountInfo() {
            var cachedColor = GUI.backgroundColor;
            try {
                GUI.backgroundColor = new Color(0, 0, 0, 0.1f);
                EditorGUILayout.LabelField($"Account Created successfully. You should receive an email with your password shortly.", accountCreatedStyle);
            } finally {
                GUI.backgroundColor = cachedColor;
            }
        }

        bool repaintedWithDownloadFinished;
        DateTime lastRepaint;
        public override void Update() {
            if (!_requestingServerInfo) {
                RequestServerInfo().Forget();
            }
            if(DownloadRequired && DateTime.UtcNow - lastRepaint > TimeSpan.FromMilliseconds(33)) {
                repaintedWithDownloadFinished = false;
                lastRepaint = DateTime.UtcNow;
                Repaint();
            }
            if(!DownloadRequired && !repaintedWithDownloadFinished) {
                repaintedWithDownloadFinished = true;
                Repaint();
            }
            if (!_requestingFlushErrors && _running) {
                RequestFlushErrors().Forget();
            }
        }

        bool PatchesCollapse => HotReloadPrefs.PatchesCollapse || HotReloadPrefs.PatchesGroupAll;
        bool PatchesGroupAll => HotReloadPrefs.PatchesCollapse && HotReloadPrefs.PatchesGroupAll;
        
        private void RenderChanges() {
            GUILayout.Space(12);
            using (new GUILayout.HorizontalScope()) {
                GUILayout.Label($"Patches ({CodePatcher.I.PatchesApplied})", HotReloadWindowStyles.H2TitleStyle);
                HotReloadPrefs.PatchesCollapse = GUILayout.Toggle(PatchesCollapse, "Collapse", "Button", GUILayout.MaxWidth(150));
                HotReloadPrefs.PatchesGroupAll = GUILayout.Toggle(PatchesGroupAll, "Group All", "Button", GUILayout.MaxWidth(150));
            }
            
            if (CodePatcher.I.PatchesApplied == 0) {
                GUILayout.Label("  Edit code to see changes in the game");
            }

            var patchedMethods = CodePatcher.I.PatchedMethods;
            if (patchedMethods.Count == 0) {
                return;
            }
            using (var scope = new EditorGUILayout.ScrollViewScope(_patchedMethodsScrollPos, GUIStyle.none, GUI.skin.verticalScrollbar)) {
                _patchedMethodsScrollPos.y = scope.scrollPosition.y;
                if (HotReloadPrefs.PatchesGroupAll) {
                    RenderPatchesGroupAll(patchedMethods);
                } else if (HotReloadPrefs.PatchesCollapse) {
                    RenderPatchesCollapse(patchedMethods);
                } else {
                    RenderPatches(patchedMethods);
                }
            }
        }

        static string GetMethodName(SMethod method) {
            var spaceIndex = method.displayName.IndexOf(" ", StringComparison.Ordinal);
            if (spaceIndex > 0) {
                return method.displayName.Substring(spaceIndex);
            }
            return method.displayName;
        }
        
        static void RenderPatches(IReadOnlyList<SMethod> patchedMethods) {
            for (var i = patchedMethods.Count - 1; i >= 0; i--) {
                var method = patchedMethods[i];
                GUILayout.Label(GetMethodName(method));
            }
        }

        static readonly Dictionary<string, int> groupAllBuffer = new Dictionary<string, int>();
        static void RenderPatchesGroupAll(IReadOnlyList<SMethod> patchedMethods) {
            foreach (var method in patchedMethods) {
                if (groupAllBuffer.ContainsKey(method.displayName)) {
                    groupAllBuffer[method.displayName]++;
                } else {
                    groupAllBuffer[method.displayName] = 1;
                }
            }
            for (var i = patchedMethods.Count - 1; i >= 0; i--) {
                var method = patchedMethods[i];
                int count;
                if (groupAllBuffer.TryGetValue(method.displayName, out count)) {
                    var str = count > 1 ? $"{GetMethodName(method)} ({count})" : GetMethodName(method);
                    GUILayout.Label(str);
                    groupAllBuffer.Remove(method.displayName);
                }
            }
        }

        static void RenderPatchesCollapse(IReadOnlyList<SMethod> patchedMethods) {
            string prevPatch = null;
            var currentPatchCount = 0;
            
            for (var index = patchedMethods.Count - 1; index >= 0; index--) {
                var method = patchedMethods[index];
                
                // process previous patch
                var patchChanged = prevPatch != null && GetMethodName(method) != prevPatch;
                if (patchChanged) {
                    var str = currentPatchCount > 1 ? $"{prevPatch} ({currentPatchCount})" : prevPatch;
                    GUILayout.Label(str);
                    currentPatchCount = 0;
                }
                
                // track patch
                currentPatchCount++;
                prevPatch = GetMethodName(method);
            }
            
            // process last patch
            var last = currentPatchCount > 1 ? $"{prevPatch} ({currentPatchCount})" : prevPatch;
            GUILayout.Label(last);
        }

        private async Task RequestFlushErrors() {
            _requestingFlushErrors = true;
            try {
                await RequestFlushErrorsCore();
            } finally {
                _requestingFlushErrors = false;
            }
        }

        private async Task RequestServerInfo() {
            _requestingServerInfo = true;
            try {
                await RequestServerInfoCore();
            } finally {
                _requestingServerInfo = false;
            }
        }
        
        private async Task RequestFlushErrorsCore() {
            var pollFrequency = 500;
            // Delay until we've hit the poll request frequency
            var waitMs = (int)Mathf.Clamp(pollFrequency - ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - _lastErrorFlush), 0, pollFrequency);
            await Task.Delay(waitMs);
            await FlushErrors();
            _lastErrorFlush = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }

        private async Task RequestServerInfoCore() {
            var pollFrequency = GetPollFrequency();
            // Delay until we've hit the poll request frequency
            var waitMs = (int)Mathf.Clamp(pollFrequency - ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - _lastServerPoll), 0, pollFrequency);
            await Task.Delay(waitMs);

            var oldRunning = _running;
            
            var newRunning = ServerHealthCheck.I.IsServerHealthy;
            _running = newRunning;

            if (_running) {
                var resp = await RequestHelper.GetLoginStatus(30);
                HandleStatus(resp);
            }

            if (!_running && !StartedServerRecently()) {
                // Reset startup progress
                _startupProgress = null;
            }

            // Repaint if the running status has changed since the layout changes quite a bit
            if (oldRunning != newRunning) {
                Repaint();
            }

            _lastServerPoll = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }

        void HandleStatus(LoginStatusResponse resp) {
            Attribution.RegisterLogin(resp);
            
            bool consumptionsChanged = _status?.freeSessionRunning != resp.freeSessionRunning || _status?.freeSessionEndTime != resp.freeSessionEndTime;
            bool expiresAtChanged = _status?.licenseExpiresAt != resp.licenseExpiresAt;
            if (resp.consumptionsUnavailableReason == ConsumptionsUnavailableReason.UnrecoverableError
                && _status?.consumptionsUnavailableReason != ConsumptionsUnavailableReason.UnrecoverableError
            ) {
                Log.Error("Free charges unavailabe. Please contact support if the issue persists.");
            }
            if (!_requestingLoginInfo && resp.requestError == null) {
                _status = resp;
            }
            if (resp.lastLicenseError == null) {
                // If we got success, we should always show an error next time it comes up
                HotReloadPrefs.ErrorHidden = false;
            }

            var oldStartupProgress = _startupProgress;
            var newStartupProgress = Tuple.Create(
                resp.startupProgress,
                string.IsNullOrEmpty(resp.startupStatus) ? "Starting Hot Reload" : resp.startupStatus);

            _startupProgress = newStartupProgress;

            // Repaint when the startup has finished
            if (oldStartupProgress == null
                || Math.Abs(oldStartupProgress.Item1 - newStartupProgress.Item1) > 0
                || oldStartupProgress.Item2 != newStartupProgress.Item2
                || consumptionsChanged
                || expiresAtChanged
            ) {
                Repaint();
                // Send project files state now that server can receive requests (only needed for player builds)
                EditorCodePatcher.TryPrepareBuildInfo();
            }
        }

        static async Task FlushErrors() {
            var response = await RequestHelper.RequestFlushErrors();
            if (response == null) {
                return;
            }
            foreach (var responseWarning in response.warnings) {
                Log.Warning(responseWarning);
            }
            foreach (var responseError in response.errors) {
                Log.Error(responseError);
            }
        }

        private int GetPollFrequency() {
            return (_startupProgress != null && _startupProgress.Item1 < 1) || StartedServerRecently()
                    ? SERVER_POLL_FREQUENCY_ON_STARTUP_MS
                    : SERVER_POLL_FREQUENCY_AFTER_STARTUP_MS;
        }

        static GUIStyle _startButtonStyle;
        static GUIStyle startButtonStyle => _startButtonStyle ?? (_startButtonStyle = new GUIStyle(GUI.skin.button) {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
        });
        
        static GUIStyle _signUpToStartStyle;
        static GUIStyle signUpToStartStyle {
            get {
                if (_signUpToStartStyle != null) {
                    return _signUpToStartStyle;
                }
                _signUpToStartStyle = new GUIStyle(GUI.skin.box) {
                    alignment = TextAnchor.MiddleCenter,
                    stretchWidth = true,
                    fontStyle = FontStyle.Bold,
                    fontSize = 16,
                };
                _signUpToStartStyle.normal.textColor = EditorGUIUtility.isProSkin ? EditorStyles.label.normal.textColor : GUI.skin.box.normal.textColor;
                return _signUpToStartStyle;
            }
        }


        static GUIStyle _accountCreatedStyle;
        static GUIStyle accountCreatedStyle {
            get {
                if (_accountCreatedStyle != null) {
                    return _accountCreatedStyle;
                }
                _accountCreatedStyle = new GUIStyle(GUI.skin.box) {
                    stretchWidth = true,
                    fontSize = 13,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(10, 10, 5, 5),
                    fontStyle = FontStyle.Bold,
                };
                _accountCreatedStyle.normal.textColor = EditorGUIUtility.isProSkin ? EditorStyles.label.normal.textColor : GUI.skin.box.normal.textColor;
                return _accountCreatedStyle;
            }
        }
        
        static GUIStyle _openSettingsStyle;
        static GUIStyle openSettingsStyle => _openSettingsStyle ?? (_openSettingsStyle = new GUIStyle(GUI.skin.button) {
            fontStyle = FontStyle.Normal,
            fixedHeight = 25,
        });
        
        static GUILayoutOption[] _bigButtonHeight;
        static GUILayoutOption[] bigButtonHeight => _bigButtonHeight ?? (_bigButtonHeight = new [] {GUILayout.Height(25)});
        
        private void RenderStart() {
            EditorGUILayout.Space();
            GUILayout.Label("Run Hot Reload", signUpToStartStyle);
            EditorGUILayout.Space();
            if (GUILayout.Button("Start", startButtonStyle)) {
                DownloadAndRun().Forget();
            }
            EditorGUILayout.Space();
        }

        private bool _requestingRestart;
        private async Task RestartCodePatcher() {
            if (_requestingRestart) {
                return;
            }
            try {
                _requestingRestart = true;
                await StopCodePatcher();
                await DownloadAndRun();
                _serverRestartedAt = DateTime.UtcNow;
            }
            finally {
                _requestingRestart = false;
            }
        }

        bool _requestingDownloadAndRun;
        internal async Task<bool> DownloadAndRun() {
            _requestingDownloadAndRun = true;
            try {
                if (DownloadRequired) {
                    var ok = await EditorCodePatcher.serverDownloader.PromptForDownload();
                    if (!ok) {
                        return false;
                    }
                }
                await StartCodePatcher();
                return true;
            } finally {
                _requestingDownloadAndRun = false;
            }
        }

        private bool _requestingStart;
        internal async Task StartCodePatcher() {
            if (_requestingStart || StartedServerRecently()) {
                return;
            }
            var exposeToNetwork = HotReloadPrefs.ExposeServerToLocalNetwork;
            var allAssetChanges = HotReloadPrefs.AllAssetChanges;
            CodePatcher.I.ClearPatchedMethods();
            try {
                _requestingStart = true;
                _startupProgress = Tuple.Create(0f, "Starting Hot Reload");
                Repaint();
                await HotReloadCli.StartAsync(exposeToNetwork, allAssetChanges).ConfigureAwait(false);
                _serverStartedAt = DateTime.UtcNow;
            }
            catch (Exception ex) {
                ThreadUtility.LogException(ex);
            }
            finally {
                _requestingStart = false;
            }
        }

        private bool _requestingStop;
        private async Task StopCodePatcher() {
            if (_requestingStop) {
                return;
            }
            CodePatcher.I.ClearPatchedMethods();
            try {
                _requestingStop = true;
                await HotReloadCli.StopAsync().ConfigureAwait(false);
                _serverStoppedAt = DateTime.UtcNow;
                await ThreadUtility.SwitchToMainThread();
                _startupProgress = null;
                Repaint();
            }
            catch (Exception ex) {
                ThreadUtility.LogException(ex);
            }
            finally {
                _requestingStop = false;
            }
        }

        private bool StartedServerRecently() {
            return DateTime.UtcNow - _serverStartedAt < TimeSpan.FromSeconds(2);
        }
        
        private bool StoppedServerRecently() {
            return DateTime.UtcNow - _serverStoppedAt < ServerHealthCheck.HeartBeatTimeout || (!StartedServerRecently() && (_startupProgress?.Item1 ?? 0) == 0);
        }
        
        private bool RestartedServerRecently() {
            return DateTime.UtcNow - _serverRestartedAt < ServerHealthCheck.HeartBeatTimeout;
        }

        private static GUIContent indieLicenseContent;
        private static GUIContent businessLicenseContent;

        internal void RenderLicenseStatusInfo(bool allowHide = true, bool verbose = false) {
            string message = null;
            MessageType messageType = default(MessageType);
            Action customGUI = null;
            GUIContent content = null;
            if (_status == null) {
                // no info
            } else if (_status.lastLicenseError != null) {
                messageType = !_status.freeSessionFinished ? MessageType.Warning : MessageType.Error;
                message = GetMessageFromError(_status.lastLicenseError);
            } else if (_status.isTrial) {
                message = $"Using Trial license, valid until {_status.licenseExpiresAt.ToShortDateString()}";
                messageType = MessageType.Info;
            } else if (_status.isIndieLicense) {
                if (verbose) {
                    message = " Indie license active";
                    messageType = MessageType.Info;
                    if (_status.licenseExpiresAt.Date != DateTime.MaxValue.Date) {
                        customGUI = () => {
                            EditorGUILayout.LabelField($"License will renew on {_status.licenseExpiresAt.ToShortDateString()}.");
                            EditorGUILayout.Space();
                        };
                    }
                    if (indieLicenseContent == null) {
                        indieLicenseContent = new GUIContent(message, EditorGUIUtility.FindTexture("TestPassed"));
                    }
                    content = indieLicenseContent;
                }
            } else if (_status.isBusinessLicense) {
                if (verbose) {
                    message = " Business license active";
                    messageType = MessageType.Info;
                    if (businessLicenseContent == null) {
                        businessLicenseContent = new GUIContent(message, EditorGUIUtility.FindTexture("TestPassed"));
                    }
                    content = businessLicenseContent;
                }
            }
            if (_status != null 
                && (_status.lastLicenseError == null || TrialExpired)
                && !_status.isBusinessLicense 
                && PackageConst.IsAssetStoreBuild
            ) {
                RenderAssetStoreProInfo(_status.isLicensed ? MessageType.Info : MessageType.Warning);
            }

            if (messageType != MessageType.Info && HotReloadPrefs.ErrorHidden && allowHide) {
                return;
            }
            if (message != null) {
                if (messageType != MessageType.Info) {
                    using(new EditorGUILayout.HorizontalScope()) {
                        HotReloadGUIHelper.HelpBox(message, messageType, fontSize: 11);
                        if (allowHide) {
                            if (GUILayout.Button("Hide", GUILayout.ExpandHeight(true))) {
                                HotReloadPrefs.ErrorHidden = true;
                            }
                        }
                    }
                } else if (content != null) {
                    EditorGUILayout.LabelField(content);
                    EditorGUILayout.Space();
                } else {
                    EditorGUILayout.LabelField(message);
                    EditorGUILayout.Space();
                }
                customGUI?.Invoke();
            }
        }
        
        public void RenderAssetStoreProInfo(MessageType messageType) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("It seems you're using Unity Pro/Enterprise after getting Hot Reload from the Asset Store. Unity Pro/Enterprise users require a Business license. Please upgrade your license on our website.", messageType);
            EditorGUILayout.Space();
            if (GUILayout.Button("More Info", HotReloadWindowOptions.ExpandHightOnly)) {
                Application.OpenURL(Constants.ProductPurchaseURL);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        const string GetLicense = "Get License";
        const string CreateAccount = "Create Account";
        const string ContactSupport = "Contact Support";
        const string UpgradeLicense = "Upgrade License";
        const string ManageLicense = "Manage License";
        internal Dictionary<string, LicenseErrorData> _licenseErrorData;
        internal Dictionary<string, LicenseErrorData> LicenseErrorData => _licenseErrorData ?? (_licenseErrorData = new Dictionary<string, LicenseErrorData> {
            { "DeviceNotLicensedException", new LicenseErrorData(description: "Another device is using your license. Please reach out to customer support for assistance.", showSupportButton: true, supportButtonText: ContactSupport) },
            { "DeviceBlacklistedException", new LicenseErrorData(description: "You device has been blacklisted.") },
            { "DateHeaderInvalidException", new LicenseErrorData(description: $"Your license is not working because your computer's clock is incorrect. Please set the clock to the correct time to restore your license.") },
            { "DateTimeCheatingException", new LicenseErrorData(description: $"Your license is not working because your computer's clock is incorrect. Please set the clock to the correct time to restore your license.") },
            { "LicenseActivationException", new LicenseErrorData(description: "An error has occured while activating your license. Please contact customer support for assistance.", showSupportButton: true, supportButtonText: ContactSupport) },
            { "LicenseDeletedException", new LicenseErrorData(description: $"Your license has been deleted. Please contact customer support for assistance.", showBuyButton: true, buyButtonText: GetLicense, showSupportButton: true, supportButtonText: ContactSupport) },
            { "LicenseDisabledException", new LicenseErrorData(description: $"Your license has been disabled. Please contact customer support for assistance.", showBuyButton: true, buyButtonText: GetLicense, showSupportButton: true, supportButtonText: ContactSupport) },
            { "LicenseExpiredException", new LicenseErrorData(description: $"Your license has expired. Please renew your license subscription using the 'Upgrade License' button below and login with your email/password to activate your license.", showBuyButton: true, buyButtonText: UpgradeLicense, showManageLicenseButton: true, manageLicenseButtonText: ManageLicense) },
            { "LicenseInactiveException", new LicenseErrorData(description: $"Your license is currenty inactive. Please login with your email/password to activate your license.") },
            { "LocalLicenseException", new LicenseErrorData(description: $"Your license file was damaged or corrupted. Please login with your email/password to refresh your license file.") },
            { "MissingParametersException", new LicenseErrorData(description: "An account already exists for this device. Please login with your existing email/password.", showBuyButton: true, buyButtonText: GetLicense) },
            { "NetworkException", new LicenseErrorData(description: "There is an issue connecting to our servers. Please check your internet connection or contact customer support if the issue persists.", showSupportButton: true, supportButtonText: ContactSupport) },
            { "TrialLicenseExpiredException", new LicenseErrorData(description: $"Your license trial has expired. Activate a license with unlimited usage or continue using the Free version. View available plans on our website.", showBuyButton: true, buyButtonText: UpgradeLicense) },
            { "InvalidCredentialException", new LicenseErrorData(description: "Incorrect email/password. You can find your initial password in the sign-up email.") },
            { "LicenseNotFoundException", new LicenseErrorData(description: "The account you're trying to access doesn't seem to exist yet. Please enter your email address to create a new account and receive a trial license.", showLoginButton: true, loginButtonText: CreateAccount) },
            { "LicenseIncompatibleException", new LicenseErrorData(description: "Please upgrade your license to continue using hotreload with Unity Pro.", showManageLicenseButton: true, manageLicenseButtonText: ManageLicense) },
        });
        internal LicenseErrorData defaultLicenseErrorData = new LicenseErrorData(description: "We apologize, an error happened while verifying your license. Please reach out to customer support for assistance.", showSupportButton: true, supportButtonText: ContactSupport);

        internal string GetMessageFromError(string error) {
            if (PackageConst.IsAssetStoreBuild && error == "TrialLicenseExpiredException") {
                return null;
            }
            return GetLicenseErrorDataOrDefault(error).description;
        }
        
        internal LicenseErrorData GetLicenseErrorDataOrDefault(string error) {
            if (_status?.isFree == true) {
                return default(LicenseErrorData);
            }
            if (_status == null || string.IsNullOrEmpty(error) && (!_status.isLicensed || _status.isTrial)) {
                return new LicenseErrorData(null, showBuyButton: true, buyButtonText: UpgradeLicense);
            }
            if (string.IsNullOrEmpty(error)) {
                return default(LicenseErrorData);
            }
            if (!LicenseErrorData.ContainsKey(error)) {
                return defaultLicenseErrorData;
            }
            return LicenseErrorData[error];
        }

        internal void RenderBuyLicenseButton(string buyLicenseButton) {
            OpenURLButton.Render(buyLicenseButton, Constants.ProductPurchaseURL);
        }

        private async Task RequestLogin(string email, string password) {
            try {
                int i = 0;
                while (!_running && i < 100) {
                    await Task.Delay(100);
                    i++;
                }

                _status = await RequestHelper.RequestLogin(email, password, 10);

                // set to false so new error is shown
                HotReloadPrefs.ErrorHidden = false;
                
                if (_status?.isLicensed == true) {
                    HotReloadPrefs.LicenseEmail = email;
                    HotReloadPrefs.LicensePassword = _status.initialPassword ?? password;
                    createdAccount = _status.initialPassword != null;

                } else if (_status != null && _status.lastLicenseError == "MissingParametersException") {
                    HotReloadPrefs.RenderAuthLogin = true;
                } else if (_status != null && _status.lastLicenseError == "LicenseNotFoundException") {
                    HotReloadPrefs.RenderAuthLogin = false;
                }
                
                HotReloadPrefs.FirstLogin = false;

                Repaint();
            } finally {
                _requestingLoginInfo = false;
            }
        }

        internal void RenderFirstLoginScreen(bool? overrideRenderFreeTrial = null) {
            var renderFreeTrial = !HotReloadPrefs.RenderAuthLogin;
            if (overrideRenderFreeTrial != null) {
                renderFreeTrial = overrideRenderFreeTrial.Value;
            }

            if (_status?.lastLicenseError != null) {
                RenderLicenseStatusInfo(allowHide: false);
            } else {
                EditorGUILayout.LabelField((renderFreeTrial ? "Sign up" : "Login") + " to start", signUpToStartStyle);
            }
            EditorGUILayout.Space();

            RenderLicenseInnerPanel(overrideRenderFreeTrial, renderLogout: false);

            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }

        void RenderLicenseActionButtons(LicenseErrorData errInfo) {
            if (errInfo.showBuyButton || errInfo.showManageLicenseButton) {
                using(new EditorGUILayout.HorizontalScope()) {
                    if (errInfo.showBuyButton) {
                        RenderBuyLicenseButton(errInfo.buyButtonText);
                    }
                    if (errInfo.showManageLicenseButton && !HotReloadPrefs.ErrorHidden) {
                        OpenURLButton.Render(errInfo.manageLicenseButtonText, Constants.ManageLicenseURL);
                    }
                }
            }
            if (errInfo.showLoginButton && GUILayout.Button(errInfo.loginButtonText, openSettingsStyle)) {
                // show license section
                _window.SelectTab(typeof(HotReloadAboutTab));
                _window.settingsTab.FocusLicenseFoldout();
            }
            if (errInfo.showSupportButton && !HotReloadPrefs.ErrorHidden) {
                OpenURLButton.Render(errInfo.supportButtonText, Constants.ContactURL);
            }
            if (_status?.lastLicenseError != null) {
                _window.aboutTab.reportIssueButton.OnGUI();
            }
        }

        internal void RenderLicenseInfo(bool verbose = false, bool allowHide = true, bool? overrideRenderFreeTrial = null, string overrideActionButton = null, bool showConsumptions = false) {
            HotReloadPrefs.ShowLogin = EditorGUILayout.Foldout(HotReloadPrefs.ShowLogin, "License", true, HotReloadWindowStyles.FoldoutStyle);
            if (HotReloadPrefs.ShowLogin) {
                if (_status?.isLicensed != true && showConsumptions) {
                    RenderConsumption();
                }
                RenderLicenseStatusInfo(allowHide: allowHide, verbose: verbose);

                RenderLicenseInnerPanel(overrideRenderFreeTrial: overrideRenderFreeTrial, overrideActionButton: overrideActionButton);
                
                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
        }

        internal void RenderPromoCodes() {
            HotReloadPrefs.ShowPromoCodes = EditorGUILayout.Foldout(HotReloadPrefs.ShowPromoCodes, "Promo Codes", true, HotReloadWindowStyles.FoldoutStyle);
            if (!HotReloadPrefs.ShowPromoCodes) {
                return;
            }
            if (promoCodeActivatedThisSession) {
                HotReloadGUIHelper.HelpBox($"Your promo code has been successfully activated. Free trial has been extended by 3 months.", MessageType.Info, 11);
            } else {
                if (promoCodeError != null && promoCodeErrorType != MessageType.None) {
                    HotReloadGUIHelper.HelpBox(promoCodeError, promoCodeErrorType, 11);
                }
                EditorGUILayout.LabelField("Promo code");
                _pendingPromoCode = EditorGUILayout.TextField(_pendingPromoCode);
                EditorGUILayout.Space();

                using (new EditorGUI.DisabledScope(_requestingActivatePromoCode)) {
                    if (GUILayout.Button("Activate promo code", HotReloadRunTab.bigButtonHeight)) {
                        RequestActivatePromoCode().Forget();
                    }
                }
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }
        
        private async Task RequestActivatePromoCode() {
            _requestingActivatePromoCode = true;
            try {
                var resp = await RequestHelper.RequestActivatePromoCode(_pendingPromoCode);
                if (resp != null && resp.error == null) {
                    promoCodeActivatedThisSession = true;
                } else {
                    var requestError = resp?.error ?? "Network error";
                    var errorType = ToErrorType(requestError);
                    promoCodeError = ToPrettyErrorMessage(errorType);
                    promoCodeErrorType = ToMessageType(errorType);
                }
            } finally {
                _requestingActivatePromoCode = false;
            }
        }

        PromoCodeErrorType ToErrorType(string error) {
            switch (error) {
                case "Input is missing":           return PromoCodeErrorType.MISSING_INPUT;
                case "only POST is supported":     return PromoCodeErrorType.INVALID_HTTP_METHOD;
                case "body is not a valid json":   return PromoCodeErrorType.BODY_INVALID;
                case "Promo code is not found":    return PromoCodeErrorType.PROMO_CODE_NOT_FOUND;
                case "Promo code already claimed": return PromoCodeErrorType.PROMO_CODE_CLAIMED;
                case "Promo code expired":         return PromoCodeErrorType.PROMO_CODE_EXPIRED;
                case "License not found":          return PromoCodeErrorType.LICENSE_NOT_FOUND;
                case "License is not a trial":     return PromoCodeErrorType.LICENSE_NOT_TRIAL;
                case "License already extended":   return PromoCodeErrorType.LICENSE_ALREADY_EXTENDED;
                case "conditionalCheckFailed":     return PromoCodeErrorType.CONDITIONAL_CHECK_FAILED;
            }
            if (error.Contains("Updating License Failed with error")) {
                return PromoCodeErrorType.UPDATING_LICENSE_FAILED;
            } else if (error.Contains("Unknown exception")) {
                return PromoCodeErrorType.UNKNOWN_EXCEPTION;
            } else if (error.Contains("Unsupported path")) {
                return PromoCodeErrorType.UNSUPPORTED_PATH;
            }
            return PromoCodeErrorType.NONE;
        }

        string ToPrettyErrorMessage(PromoCodeErrorType errorType) {
            var defaultMsg = "We apologize, an error happened while activating your promo code. Please reach out to customer support for assistance.";
            switch (errorType) {
                case PromoCodeErrorType.MISSING_INPUT:
                case PromoCodeErrorType.INVALID_HTTP_METHOD:
                case PromoCodeErrorType.BODY_INVALID:
                case PromoCodeErrorType.UNKNOWN_EXCEPTION:
                case PromoCodeErrorType.UNSUPPORTED_PATH:
                case PromoCodeErrorType.LICENSE_NOT_FOUND:
                case PromoCodeErrorType.UPDATING_LICENSE_FAILED:
                case PromoCodeErrorType.LICENSE_NOT_TRIAL:
                    return defaultMsg;
                case PromoCodeErrorType.PROMO_CODE_NOT_FOUND:     return "Your promo code is invalid. Please ensure that you have entered the correct promo code.";
                case PromoCodeErrorType.PROMO_CODE_CLAIMED:       return "Your promo code has already been used.";
                case PromoCodeErrorType.PROMO_CODE_EXPIRED:       return "Your promo code has expired.";
                case PromoCodeErrorType.LICENSE_ALREADY_EXTENDED: return "Your license has already been activated with a promo code. Only one promo code activation per license is allowed.";
                case PromoCodeErrorType.CONDITIONAL_CHECK_FAILED: return "We encountered an error while activating your promo code. Please try again. If the issue persists, please contact our customer support team for assistance.";
                case PromoCodeErrorType.NONE:                     return "There is an issue connecting to our servers. Please check your internet connection or contact customer support if the issue persists.";
                default:                                          return defaultMsg;
            }
        }

        MessageType ToMessageType(PromoCodeErrorType errorType) {
            switch (errorType) {
                case PromoCodeErrorType.MISSING_INPUT:            return MessageType.Error;
                case PromoCodeErrorType.INVALID_HTTP_METHOD:      return MessageType.Error;
                case PromoCodeErrorType.BODY_INVALID:             return MessageType.Error;
                case PromoCodeErrorType.PROMO_CODE_NOT_FOUND:     return MessageType.Warning;
                case PromoCodeErrorType.PROMO_CODE_CLAIMED:       return MessageType.Warning;
                case PromoCodeErrorType.PROMO_CODE_EXPIRED:       return MessageType.Warning;
                case PromoCodeErrorType.LICENSE_NOT_FOUND:        return MessageType.Error;
                case PromoCodeErrorType.LICENSE_NOT_TRIAL:        return MessageType.Error;
                case PromoCodeErrorType.LICENSE_ALREADY_EXTENDED: return MessageType.Warning;
                case PromoCodeErrorType.UPDATING_LICENSE_FAILED:  return MessageType.Error;
                case PromoCodeErrorType.CONDITIONAL_CHECK_FAILED: return MessageType.Error;
                case PromoCodeErrorType.UNKNOWN_EXCEPTION:        return MessageType.Error;
                case PromoCodeErrorType.UNSUPPORTED_PATH:         return MessageType.Error;
                case PromoCodeErrorType.NONE:                     return MessageType.Error;
                default:                                          return MessageType.Error;
            }
        }

        public void RenderLicenseButtons(bool alwaysShowBuy = false) {
            var errInfo = GetLicenseErrorDataOrDefault(_status?.lastLicenseError);
            if (alwaysShowBuy) {
                errInfo.showBuyButton = true;
                errInfo.buyButtonText = errInfo.buyButtonText ?? GetLicense;
            }
            RenderLicenseActionButtons(errInfo);
        }

        private void RenderLicenseInnerPanel(bool? overrideRenderFreeTrial = null, string overrideActionButton = null, bool renderLogout = true) {
            var renderFreeTrial = !HotReloadPrefs.RenderAuthLogin;
            if (overrideRenderFreeTrial != null) {
                renderFreeTrial = overrideRenderFreeTrial.Value;
            }

            EditorGUILayout.LabelField("Email");
            GUI.SetNextControlName("email");
            _pendingEmail = EditorGUILayout.TextField(string.IsNullOrEmpty(_pendingEmail) ? HotReloadPrefs.LicenseEmail : _pendingEmail);
            _pendingEmail = _pendingEmail.Trim();

            if (!renderFreeTrial) {
                EditorGUILayout.LabelField("Password");
                GUI.SetNextControlName("password");
                _pendingPassword = EditorGUILayout.PasswordField(string.IsNullOrEmpty(_pendingPassword) ? HotReloadPrefs.LicensePassword : _pendingPassword);
            }

            EditorGUILayout.Space();
            
            RenderSwitchAuthMode(allowSwitch: overrideRenderFreeTrial == null);
            
            var e = Event.current;
            using(new EditorGUI.DisabledScope(_requestingLoginInfo)) {
                var btnLabel = overrideActionButton;
                if (String.IsNullOrEmpty(overrideActionButton)) {
                    btnLabel = renderFreeTrial ? "Activate Free Trial" : "Login";
                }
                using (new EditorGUILayout.HorizontalScope()) {
                    var focusedControl = GUI.GetNameOfFocusedControl();
                    if (GUILayout.Button(btnLabel, bigButtonHeight)
                        || (focusedControl == "email" 
                            || focusedControl == "password") 
                        && e.type == EventType.KeyUp 
                        && (e.keyCode == KeyCode.Return 
                            || e.keyCode == KeyCode.KeypadEnter)
                    ) {
                        if (string.IsNullOrEmpty(_pendingEmail)) {
                            _activateInfoMessage = new Tuple<string, MessageType>("Please enter your email address.", MessageType.Warning);
                        } else if (!EditorWindowHelper.IsValidEmailAddress(_pendingEmail)) {
                            _activateInfoMessage = new Tuple<string, MessageType>("Please enter a valid email address.", MessageType.Warning);
                        } else if (_pendingEmail.Contains('+')) {
                            _activateInfoMessage = new Tuple<string, MessageType>("Mail extensions (in a form of 'username+suffix@example.com') are not supported yet. Please provide your original email address (such as 'username@example.com' without '+suffix' part) as we're working on resolving this issue.", MessageType.Warning);
                        } else if (string.IsNullOrEmpty(_pendingPassword) && !renderFreeTrial) {
                            _activateInfoMessage = new Tuple<string, MessageType>("Please enter your password.", MessageType.Warning);
                        } else {
                            if (_status?.lastLicenseError != null && !_status.isFree) {
                                if (_status.lastLicenseError == "MissingParametersException") {
                                    _activateInfoMessage = new Tuple<string, MessageType>("An account already exists for this device. Please login with your existing email/password.", MessageType.Info);
                                } else if (_status.lastLicenseError.Contains("InvalidCredentialException")) {
                                    _activateInfoMessage = new Tuple<string, MessageType>("Invalid email/password. You can find your initial password in the sign-up email.", MessageType.Error);
                                } else {
                                    _activateInfoMessage = new Tuple<string, MessageType>("Invalid license. Please get a valid license key and activate it to start using Hot Reload for Unity.", MessageType.Error);
                                }
                                HotReloadPrefs.RenderAuthLogin = true;
                            }

                            _activateInfoMessage = null;
                            _requestingLoginInfo = true;
                            var pass = renderFreeTrial ? null : _pendingPassword;
                            if (!_requestingDownloadAndRun && !_running) {
                                // when activating free trial, password is not required and must be null 
                                LoginOnDownloadAndRun(_pendingEmail, pass).Forget();
                            } else {
                                RequestLogin(_pendingEmail, pass).Forget();
                            }
                        }
                    }
                    if (renderLogout) {
                        RenderLogout();
                    }
                }
            }
            if (_activateInfoMessage != null && (e.type == EventType.Layout || e.type == EventType.Repaint)) {
                EditorGUILayout.HelpBox(_activateInfoMessage.Item1, _activateInfoMessage.Item2);
            }
        }

        public void RenderLogout() {
            if (_status?.isLicensed != true) {
                return;
            }
            if (GUILayout.Button("Logout", bigButtonHeight)) {
                if (!_requestingDownloadAndRun && !_running) {
                    LogoutOnDownloadAndRun().Forget();
                } else {
                    RequestLogout().Forget();
                }
            }
        }
        
        async Task LoginOnDownloadAndRun(string email, string password) {
            var ok = await DownloadAndRun();
            if (!ok) {
                _requestingLoginInfo = false;
                return;
            }
            await RequestLogin(email, password);
        }

        async Task LogoutOnDownloadAndRun() {
            var ok = await DownloadAndRun();
            if (!ok) {
                return;
            }
            await RequestLogout();
        }

        private async Task RequestLogout() {
            int i = 0;
            while (!_running && i < 100) {
                await Task.Delay(100);
                i++;
            }
            var resp = await RequestHelper.RequestLogout();
            if (!_requestingLoginInfo && resp != null) {
                HandleStatus(resp);
            }
        }

        private static void RenderSwitchAuthMode(bool allowSwitch = true) {
            using(new EditorGUILayout.HorizontalScope()) {
                var color = EditorGUIUtility.isProSkin ? new Color32(0x3F, 0x9F, 0xFF, 0xFF) : new Color32(0x0F, 0x52, 0xD7, 0xFF); 
                if (HotReloadGUIHelper.LinkLabel("Forgot password?", 12, FontStyle.Normal, TextAnchor.MiddleLeft, color)) {
                    if (EditorUtility.DisplayDialog("Recover password", "Use company code 'naughtycult' and the email you signed up with in order to recover your account.", "Open in browser", "Cancel")) {
                        Application.OpenURL(Constants.ForgotPasswordURL);
                    }
                }
                if (allowSwitch) {
                    GUILayout.FlexibleSpace();
                    if (HotReloadGUIHelper.LinkLabel(HotReloadPrefs.RenderAuthLogin ? "Sign up for free trial" : "Sign in", 12, FontStyle.Normal, TextAnchor.MiddleRight, color)) {
                        HotReloadPrefs.RenderAuthLogin = !HotReloadPrefs.RenderAuthLogin;
                    }
                }
            }
        }

        GUIStyle progressBarBarStyle;
        GUIStyle ProgressBarBarStyle {
            get {
                if (progressBarBarStyle != null) {
                    return progressBarBarStyle;
                }
                var styles = (EditorStyles)typeof(EditorStyles)
                    .GetField("s_Current", BindingFlags.Static | BindingFlags.NonPublic)
                    ?.GetValue(null);
                var style = styles?.GetType()
                    .GetField("m_ProgressBarBar", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(styles);
                progressBarBarStyle = style != null ? (GUIStyle)style : GUIStyle.none;
                return progressBarBarStyle;
            }
        }
        
        GUIStyle progressBarTextStyle;
        GUIStyle ProgressBarTextStyle {
            get {
                if (progressBarTextStyle != null) {
                    return progressBarTextStyle;
                }
                var styles = (EditorStyles)typeof(EditorStyles)
                    .GetField("s_Current", BindingFlags.Static | BindingFlags.NonPublic)
                    ?.GetValue(null);
                var style = styles?.GetType()
                    .GetField("m_ProgressBarText", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(styles);
                progressBarTextStyle = style != null ? (GUIStyle)style : GUIStyle.none;
                return progressBarTextStyle;
            }
        }

        Texture2D greenTextureLight;
        Texture2D greenTextureDark;
        Texture2D GreenTexture => EditorGUIUtility.isProSkin 
            ? greenTextureDark ? greenTextureDark : (greenTextureDark = MakeTexture(0.5f))
            : greenTextureLight ? greenTextureLight : (greenTextureLight = MakeTexture(0.85f));

        private void RenderProgressBar() {
            if(DownloadRequired && !DownloadStarted) {
                return;
            }
            using(var scope = new EditorGUILayout.VerticalScope(HotReloadWindowStyles.MiddleCenterStyle)) {
                float progress;
                string txt;
                if(DownloadRequired) {
                    txt = "Installing platform specific components"; 
                    progress = DownloadProgress;
                } else {
                    progress = Mathf.Clamp(_startupProgress?.Item1 ?? 0f, 0f, 1f);
                    txt = _startupProgress?.Item2 ?? "";
                    if (_startupProgress?.Item1 >= 1f) {
                        txt = "Hot Reload is running";
                    } else if (RestartedServerRecently() && _startupProgress == null) {
                        txt = "Restarting Hot Reload";
                    } else if (StoppedServerRecently() && !RestartedServerRecently() && _startupProgress == null) {
                        txt = "Stopping Hot Reload";
                        _startupProgress = null;
                    }
                }
                var bg = ProgressBarBarStyle.normal.background;
                var fs = ProgressBarTextStyle.fontStyle;
                try {
                    ProgressBarBarStyle.normal.background = GreenTexture;
                    ProgressBarTextStyle.fontStyle = FontStyle.Bold;
                    if (DownloadRequired) {
                        using (var hScore = new EditorGUILayout.HorizontalScope()) {
                            var barRect = hScore.rect;
                            barRect.width -= 120;
                            EditorGUI.ProgressBar(barRect, progress, txt);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button(new GUIContent("More info here", EditorGUIUtility.IconContent("console.infoicon").image), GUILayout.ExpandWidth(false), GUILayout.MaxHeight(23))) {
                                Application.OpenURL(Constants.AdditionalContentURL);
                            }
                        }
                    } else {
                        var barRect = scope.rect;
                        barRect.height = 23;
                        EditorGUI.ProgressBar(barRect, progress, txt);
                        EditorGUILayout.Space(30);
                        
                        if (_running && !Starting) {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button(new GUIContent("Stop", EditorGUIUtility.IconContent("PauseButton@2x").image), GUILayout.ExpandWidth(false), GUILayout.MaxHeight(23), GUILayout.MinWidth(90))) {
                                if (!StoppedServerRecently()) {
                                    StopCodePatcher().Forget();
                                }
                            }
                            if (GUILayout.Button(new GUIContent(" Restart", EditorGUIUtility.IconContent("Refresh").image), GUILayout.ExpandWidth(false), GUILayout.MaxHeight(23), GUILayout.MinWidth(90))) {
                                if (!RestartedServerRecently()) {
                                    RestartCodePatcher().Forget();   
                                }
                            }
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                } finally {
                    ProgressBarBarStyle.normal.background = bg;
                    ProgressBarTextStyle.fontStyle = fs;
                }
            }
        }

        private Texture2D MakeTexture(float maxHue) {
            var width = 11;
            var height = 11;
            Color[] pix = new Color[width * height];
            for (int y = 0; y < height; y++) {
                var middle = Math.Ceiling(height / (double)2);
                var maxGreen = maxHue;
                var yCoord = y + 1;
                var green = maxGreen - Math.Abs(yCoord - middle) * 0.02;
                for (int x = 0; x < width; x++) {
                    pix[y * width + x] = new Color(0.1f, (float)green, 0.1f, 1.0f);
                }
            }
            var result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
        
        
        /*
        [MenuItem("codepatcher/restart")]
        public static void TestRestart() {
            CodePatcherCLI.Restart(Application.dataPath, false);
        }
        */
        
    }

    internal static class HotReloadGUIHelper {
        public static bool LinkLabel(string labelText, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color? color = null) {
            var stl = EditorStyles.label;

            // copy
            var origSize = stl.fontSize;
            var origStyle = stl.fontStyle;
            var origAnchor = stl.alignment;
            var origColor = stl.normal.textColor;

            // temporarily modify the built-in style
            stl.fontSize = fontSize;
            stl.fontStyle = fontStyle;
            stl.alignment = alignment;
            stl.normal.textColor = color ?? origColor;
            stl.active.textColor = color ?? origColor;
            stl.focused.textColor = color ?? origColor;
            stl.hover.textColor = color ?? origColor;

            try {
                return GUILayout.Button(labelText, stl);
            }  finally{
                // set the editor style (stl) back to normal
                stl.fontSize = origSize;
                stl.fontStyle = origStyle;
                stl.alignment = origAnchor;
                stl.normal.textColor = origColor;
                stl.active.textColor = origColor;
                stl.focused.textColor = origColor;
                stl.hover.textColor = origColor;
            }
        }

        public static void HelpBox(string message, MessageType type, int fontSize) {
            var _fontSize = EditorStyles.helpBox.fontSize;
            try {
                EditorStyles.helpBox.fontSize = fontSize;
                EditorGUILayout.HelpBox(message, type);
            } finally {
                EditorStyles.helpBox.fontSize = _fontSize;
            }
        }
    }

    internal enum PromoCodeErrorType {
        NONE,
        MISSING_INPUT,
        INVALID_HTTP_METHOD,
        BODY_INVALID,
        PROMO_CODE_NOT_FOUND,
        PROMO_CODE_CLAIMED,
        PROMO_CODE_EXPIRED,
        LICENSE_NOT_FOUND,
        LICENSE_NOT_TRIAL,
        LICENSE_ALREADY_EXTENDED,
        UPDATING_LICENSE_FAILED,
        CONDITIONAL_CHECK_FAILED,
        UNKNOWN_EXCEPTION,
        UNSUPPORTED_PATH,
    }
}

