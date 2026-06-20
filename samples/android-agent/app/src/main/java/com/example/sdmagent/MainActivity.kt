package com.example.sdmagent

import android.app.admin.DevicePolicyManager
import android.content.Context
import android.net.Uri
import android.os.Bundle
import android.provider.Settings
import android.util.Log
import android.widget.Button
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import com.google.firebase.messaging.FirebaseMessaging
import kotlinx.coroutines.launch
import kotlinx.coroutines.tasks.await
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import org.json.JSONObject
import retrofit2.Response
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory
import retrofit2.http.Body
import retrofit2.http.POST
import java.util.UUID

data class DeviceRegisterWithTokenRequest(
    val token: String,
    val deviceIdentifier: String,
    val serialNumber: String,
    val manufacturer: String,
    val model: String,
    val androidVersion: String,
    val fcmToken: String? = null
)

data class DeviceRegisterWithTokenResponse(
    val deviceId: String,
    val deviceJwt: String,
    val expiresInSeconds: Int
)

data class UpdateFcmTokenRequest(
    val fcmToken: String
)

interface ApiService {
    @POST("api/devices/register-with-token")
    suspend fun register(@Body req: DeviceRegisterWithTokenRequest): Response<DeviceRegisterWithTokenResponse>

    @POST("api/devices/update-fcm-token")
    suspend fun updateFcmToken(@retrofit2.http.Header("Authorization") auth: String, @Body req: UpdateFcmTokenRequest): Response<Unit>
}

class MainActivity : AppCompatActivity() {

    private var enrollmentToken: String? = null
    private var serverUrl: String = ""

    private lateinit var tvDeviceId: TextView
    private lateinit var tvManufacturer: TextView
    private lateinit var tvModel: TextView
    private lateinit var tvAndroidVersion: TextView
    private lateinit var tvSerialNumber: TextView
    private lateinit var tvTokenStatus: TextView
    private lateinit var tvServerUrl: TextView
    private lateinit var tvMdmStatus: TextView
    private lateinit var btnEnroll: Button
    private lateinit var tvStatus: TextView

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        // Initialize UI components
        tvDeviceId = findViewById(R.id.tvDeviceId)
        tvManufacturer = findViewById(R.id.tvManufacturer)
        tvModel = findViewById(R.id.tvModel)
        tvAndroidVersion = findViewById(R.id.tvAndroidVersion)
        tvSerialNumber = findViewById(R.id.tvSerialNumber)
        tvTokenStatus = findViewById(R.id.tvTokenStatus)
        tvServerUrl = findViewById(R.id.tvServerUrl)
        tvMdmStatus = findViewById(R.id.tvMdmStatus)
        btnEnroll = findViewById(R.id.btnEnroll)
        tvStatus = findViewById(R.id.tvStatus)

        displayDeviceDetails()

        // handle deep link
        val data: Uri? = intent?.data
        enrollmentToken = data?.getQueryParameter("token")
        
        // Priority: QR code URL > config.json
        serverUrl = determineBaseUrl(data)
        
        updateUiWithEnrollmentData()
        checkMdmStatus()

        btnEnroll.setOnClickListener {
            performEnrollment()
        }
    }

    private fun displayDeviceDetails() {
        val deviceId = Settings.Secure.getString(contentResolver, Settings.Secure.ANDROID_ID) ?: UUID.randomUUID().toString()
        val manufacturer = android.os.Build.MANUFACTURER ?: "Unknown"
        val model = android.os.Build.MODEL ?: "Unknown"
        val androidV = android.os.Build.VERSION.RELEASE ?: "Unknown"
        val serial = android.os.Build.SERIAL ?: "Unknown"

        tvDeviceId.text = deviceId
        tvManufacturer.text = manufacturer
        tvModel.text = model
        tvAndroidVersion.text = androidV
        tvSerialNumber.text = serial
    }

    private fun updateUiWithEnrollmentData() {
        if (enrollmentToken != null) {
            tvTokenStatus.text = "Token: Present"
            tvTokenStatus.setTextColor(resources.getColor(android.R.color.holo_green_dark, null))
            btnEnroll.isEnabled = true
        } else {
            tvTokenStatus.text = "Token: None (Scan QR to enroll)"
            tvTokenStatus.setTextColor(resources.getColor(android.R.color.holo_red_dark, null))
            btnEnroll.isEnabled = false
        }
        tvServerUrl.text = "Server: $serverUrl"
    }

    private fun checkMdmStatus() {
        val dpm = getSystemService(Context.DEVICE_POLICY_SERVICE) as DevicePolicyManager
        val isDeviceOwner = dpm.isDeviceOwnerApp(packageName)
        val isAdmin = dpm.isAdminActive(android.content.ComponentName(this, AdminReceiver::class.java))

        val statusText = when {
            isDeviceOwner -> "MDM Status: Device Owner (Active)"
            isAdmin -> "MDM Status: Device Admin (Active)"
            else -> "MDM Status: Not Active (Run adb command)"
        }
        tvMdmStatus.text = statusText
        
        if (isDeviceOwner) {
            tvMdmStatus.setTextColor(resources.getColor(android.R.color.holo_green_dark, null))
        } else {
            tvMdmStatus.setTextColor(resources.getColor(android.R.color.holo_orange_dark, null))
        }
    }

    private fun performEnrollment() {
        val token = enrollmentToken ?: return
        val deviceId = tvDeviceId.text.toString()
        val manufacturer = tvManufacturer.text.toString()
        val model = tvModel.text.toString()
        val androidV = tvAndroidVersion.text.toString()
        val serial = tvSerialNumber.text.toString()

        tvStatus.text = "Enrolling..."
        btnEnroll.isEnabled = false

        lifecycleScope.launch {
            try {
                val fcmToken = try {
                    FirebaseMessaging.getInstance().token.await()
                } catch (e: Exception) {
                    Log.e("MainActivity", "Failed to get FCM token", e)
                    null
                }

                val req = DeviceRegisterWithTokenRequest(
                    token = token,
                    deviceIdentifier = deviceId,
                    serialNumber = serial,
                    manufacturer = manufacturer,
                    model = model,
                    androidVersion = androidV,
                    fcmToken = fcmToken
                )

                val logging = HttpLoggingInterceptor().apply { level = HttpLoggingInterceptor.Level.BODY }
                val client = OkHttpClient.Builder().addInterceptor(logging).build()
                val retrofit = Retrofit.Builder()
                    .baseUrl(serverUrl)
                    .client(client)
                    .addConverterFactory(MoshiConverterFactory.create())
                    .build()

                val api = retrofit.create(ApiService::class.java)
                val resp = api.register(req)
                if (resp.isSuccessful && resp.body() != null) {
                    val body = resp.body()!!
                    saveJwt(body.deviceJwt)
                    saveServerUrl(serverUrl)
                    tvStatus.text = "Status: Successfully Enrolled!\nDevice ID: ${body.deviceId}"
                    Toast.makeText(this@MainActivity, "Enrollment Successful", Toast.LENGTH_LONG).show()
                } else {
                    val errorMsg = resp.errorBody()?.string() ?: "Unknown error"
                    tvStatus.text = "Status: Enrollment failed (${resp.code()})\n$errorMsg"
                    btnEnroll.isEnabled = true
                }
            } catch (e: Exception) {
                tvStatus.text = "Status: Error - ${e.message}"
                btnEnroll.isEnabled = true
            }
        }
    }

    private fun determineBaseUrl(uri: Uri?): String {
        // 1. Check if 'server' is provided in the QR code (deep link)
        uri?.getQueryParameter("server")?.let { return ensureTrailingSlash(it) }

        // 2. Fallback to assets/config.json
        try {
            assets.open("config.json").bufferedReader().use { r ->
                val txt = r.readText()
                val jo = JSONObject(txt)
                if (jo.has("server")) return ensureTrailingSlash(jo.getString("server"))
            }
        } catch (_: Exception) { }

        // 3. Last resort default
        return "http://10.0.2.2:5254/"
    }

    private fun ensureTrailingSlash(url: String): String {
        return if (url.endsWith("/")) url else "$url/"
    }

    private fun saveJwt(jwt: String) {
        getPrefs().edit().putString("device_jwt", jwt).apply()
    }

    private fun saveServerUrl(url: String) {
        getPrefs().edit().putString("server_url", url).apply()
    }

    private fun getPrefs(): android.content.SharedPreferences {
        val masterKeyAlias = MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC)
        return EncryptedSharedPreferences.create(
            "sdm_prefs",
            masterKeyAlias,
            this,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
        )
    }
}
