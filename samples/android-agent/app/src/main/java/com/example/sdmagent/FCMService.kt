package com.example.sdmagent

import android.app.admin.DevicePolicyManager
import android.content.ComponentName
import android.content.Context
import android.util.Log
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.logging.HttpLoggingInterceptor
import org.json.JSONObject
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory
import java.io.File
import java.io.FileOutputStream

class FCMService : FirebaseMessagingService() {

    override fun onMessageReceived(remoteMessage: RemoteMessage) {
        super.onMessageReceived(remoteMessage)
        Log.d("FCMService", "Message Received from: ${remoteMessage.from}")
        Log.d("FCMService", "Data payload: ${remoteMessage.data}")

        val command = remoteMessage.data["command"] ?: remoteMessage.data["commandType"]
        if (command != null) {
            Log.d("FCMService", "Processing command: $command")
            handleCommand(command, remoteMessage.data)
        } else {
            Log.w("FCMService", "Message received but no 'command' or 'commandType' found in data")
        }
    }

    override fun onNewToken(token: String) {
        super.onNewToken(token)
        Log.d("FCMService", "Refreshed token: $token")
        sendTokenToBackend(token)
    }

    private fun sendTokenToBackend(token: String) {
        val jwt = getSavedValue("device_jwt")
        if (jwt == null) {
            Log.d("FCMService", "No JWT found, skipping token update")
            return
        }

        val baseUrl = getSavedValue("server_url") ?: determineBaseUrl()
        Log.d("FCMService", "Updating FCM token on backend: $baseUrl")
        
        CoroutineScope(Dispatchers.IO).launch {
            try {
                val logging = HttpLoggingInterceptor().apply { level = HttpLoggingInterceptor.Level.BODY }
                val client = OkHttpClient.Builder().addInterceptor(logging).build()
                val retrofit = Retrofit.Builder()
                    .baseUrl(baseUrl)
                    .client(client)
                    .addConverterFactory(MoshiConverterFactory.create())
                    .build()

                val api = retrofit.create(ApiService::class.java)
                val resp = api.updateFcmToken("Bearer $jwt", UpdateFcmTokenRequest(token))
                if (resp.isSuccessful) {
                    Log.d("FCMService", "FCM token updated successfully on backend")
                } else {
                    Log.e("FCMService", "Failed to update FCM token on backend: ${resp.code()}")
                }
            } catch (e: Exception) {
                Log.e("FCMService", "Error updating FCM token at $baseUrl", e)
                // If LAN IP failed, maybe try the emulator alias as a fallback?
                if (baseUrl.contains("192.168")) {
                    Log.i("FCMService", "Retrying with 10.0.2.2 fallback...")
                    // logic for retry could go here if needed
                }
            }
        }
    }

    private fun getSavedValue(key: String): String? {
        return try {
            val masterKeyAlias = MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC)
            val sharedPrefs = EncryptedSharedPreferences.create(
                "sdm_prefs",
                masterKeyAlias,
                this,
                EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
            )
            sharedPrefs.getString(key, null)
        } catch (e: Exception) {
            null
        }
    }

    private fun determineBaseUrl(): String {
        try {
            assets.open("config.json").bufferedReader().use { r ->
                val txt = r.readText()
                val jo = JSONObject(txt)
                if (jo.has("server")) {
                    val url = jo.getString("server")
                    return if (url.endsWith("/")) url else "$url/"
                }
            }
        } catch (_: Exception) { }
        return "http://10.0.2.2:5254/"
    }

    private fun handleCommand(command: String, data: Map<String, String>) {
        val dpm = getSystemService(Context.DEVICE_POLICY_SERVICE) as DevicePolicyManager
        val adminName = ComponentName(this, AdminReceiver::class.java)

        if (!dpm.isAdminActive(adminName)) {
            Log.w("FCMService", "Device Admin not active. Cannot execute command: $command")
            return
        }

        when (command) {
            "LockScreen", "force-lock" -> {
                try {
                    dpm.lockNow()
                    Log.d("FCMService", "Screen locked")
                } catch (e: SecurityException) {
                    Log.e("FCMService", "SecurityException locking screen", e)
                }
            }
            "DisableApp", "disable-app" -> {
                val packageName = data["packageName"]
                if (packageName != null && dpm.isDeviceOwnerApp(this.packageName)) {
                    dpm.setApplicationHidden(adminName, packageName, true)
                    Log.d("FCMService", "App disabled: $packageName")
                }
            }
            "EnableApp", "enable-app" -> {
                val packageName = data["packageName"]
                if (packageName != null && dpm.isDeviceOwnerApp(this.packageName)) {
                    dpm.setApplicationHidden(adminName, packageName, false)
                    Log.d("FCMService", "App enabled: $packageName")
                }
            }
            "LockApp", "lock-app" -> {
                val packageName = data["packageName"]
                if (packageName != null && dpm.isDeviceOwnerApp(this.packageName)) {
                    dpm.setLockTaskPackages(adminName, arrayOf(packageName))
                    Log.d("FCMService", "LockApp allowed for: $packageName")
                }
            }
            "InstallApp", "install-app" -> {
                val url = data["url"]
                val pkg = data["packageName"]
                if (url != null && pkg != null) {
                    downloadAndInstall(url, pkg)
                }
            }
            "WipeData", "wipe-data" -> {
                // dpm.wipeData(0)
                Log.d("FCMService", "WipeData command received (commented out for safety)")
            }
            else -> {
                Log.d("FCMService", "Unknown command: $command")
            }
        }
    }

    private fun downloadAndInstall(url: String, packageName: String) {
        CoroutineScope(Dispatchers.IO).launch {
            try {
                Log.d("FCMService", "Downloading APK from $url")
                val client = OkHttpClient()
                val request = Request.Builder().url(url).build()
                val response = client.newCall(request).execute()

                if (!response.isSuccessful) {
                    Log.e("FCMService", "Failed to download APK: ${response.code}")
                    return@launch
                }

                val apkFile = File(cacheDir, "temp.apk")
                val fos = FileOutputStream(apkFile)
                fos.write(response.body?.bytes() ?: return@launch)
                fos.close()

                Log.d("FCMService", "APK downloaded, starting installation")
                PackageInstallerHelper.installPackage(this@FCMService, apkFile, packageName)
            } catch (e: Exception) {
                Log.e("FCMService", "Error during download and install", e)
            }
        }
    }
}
