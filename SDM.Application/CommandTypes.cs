namespace SDM.Application
{
    /// <summary>
    /// Canonical command type strings sent via POST /api/devices/{id}/commands.
    /// Each entry documents the expected Payload JSON shape.
    /// Commands marked [Device Owner] require the agent to be set as Device Owner
    /// (via adb dpm set-device-owner). Commands marked [Device Admin] only need
    /// Device Admin activation.
    /// </summary>
    public static class CommandTypes
    {
        // ── Lock / wipe ────────────────────────────────────────────────────────
        /// <summary>[Device Admin] Lock the screen immediately. Payload: (none)</summary>
        public const string LockScreen = "LockScreen";

        /// <summary>[Device Owner] Factory-reset the device. Payload: (none). Agent has this disabled by default for safety.</summary>
        public const string WipeData = "WipeData";

        // ── App visibility ─────────────────────────────────────────────────────
        /// <summary>[Device Owner] Hide an app. Payload: {"packageName":"com.example.app"}</summary>
        public const string DisableApp = "DisableApp";

        /// <summary>[Device Owner] Unhide an app. Payload: {"packageName":"com.example.app"}</summary>
        public const string EnableApp = "EnableApp";

        /// <summary>[Device Owner] Sideload an APK. Payload: {"packageName":"com.example.app","url":"https://…/app.apk"}</summary>
        public const string InstallApp = "InstallApp";

        // ── App installation control ───────────────────────────────────────────
        /// <summary>[Device Owner] Block all app installations by the user. Payload: (none)</summary>
        public const string DisableAppInstall = "DisableAppInstall";

        /// <summary>[Device Owner] Restore app installation. Payload: (none)</summary>
        public const string EnableAppInstall = "EnableAppInstall";

        // ── Kiosk mode ─────────────────────────────────────────────────────────
        /// <summary>[Device Owner] Pin a single app in kiosk / task-lock mode. Payload: {"packageName":"com.example.app"}</summary>
        public const string EnableKiosk = "EnableKiosk";

        /// <summary>[Device Owner] Exit kiosk mode (allow all apps). Payload: (none)</summary>
        public const string DisableKiosk = "DisableKiosk";

        // ── Camera ─────────────────────────────────────────────────────────────
        /// <summary>[Device Admin] Disable the device camera. Payload: (none)</summary>
        public const string DisableCamera = "DisableCamera";

        /// <summary>[Device Admin] Re-enable the device camera. Payload: (none)</summary>
        public const string EnableCamera = "EnableCamera";

        // ── USB ────────────────────────────────────────────────────────────────
        /// <summary>[Device Owner] Block USB file transfer. Payload: (none)</summary>
        public const string BlockUsb = "BlockUsb";

        /// <summary>[Device Owner] Restore USB file transfer. Payload: (none)</summary>
        public const string UnblockUsb = "UnblockUsb";

        // ── Wi-Fi ──────────────────────────────────────────────────────────────
        /// <summary>[Device Owner] Prevent user from changing Wi-Fi state. Payload: (none)</summary>
        public const string DisableWifi = "DisableWifi";

        /// <summary>[Device Owner] Restore Wi-Fi control to the user. Payload: (none)</summary>
        public const string EnableWifi = "EnableWifi";

        // ── Bluetooth ──────────────────────────────────────────────────────────
        /// <summary>[Device Owner] Disable Bluetooth. Payload: (none)</summary>
        public const string DisableBluetooth = "DisableBluetooth";

        /// <summary>[Device Owner] Re-enable Bluetooth. Payload: (none)</summary>
        public const string EnableBluetooth = "EnableBluetooth";

        // ── Password policy ────────────────────────────────────────────────────
        /// <summary>
        /// [Device Admin] Enforce a password policy.
        /// Payload: {"minLength":6,"quality":"ALPHANUMERIC"}
        /// quality values: SOMETHING | NUMERIC | NUMERIC_COMPLEX | ALPHABETIC | ALPHANUMERIC | COMPLEX
        /// </summary>
        public const string SetPasswordPolicy = "SetPasswordPolicy";

        // ── Web restrictions ───────────────────────────────────────────────────
        /// <summary>
        /// [Device Owner] Apply URL block/allow lists to Chrome via managed config.
        /// Payload: {"blockedUrls":["*.evil.com","adult.com"],"allowedUrls":["safe.com"]}
        /// Supports Chrome URL filter pattern syntax. Replaces any previous policy on each call.
        /// </summary>
        public const string SetWebRestrictions = "SetWebRestrictions";
    }
}
