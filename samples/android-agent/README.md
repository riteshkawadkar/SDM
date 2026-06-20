Android sample agent

- Intent-filter in AndroidManifest handles sdm://enroll?token=...
- MainActivity auto-collects ANDROID_ID, manufacturer, model and posts to /api/devices/register-with-token
- Uses EncryptedSharedPreferences to save deviceJwt

Notes:
- For emulator use baseUrl http://10.0.2.2:5254/
- For a real device set baseUrl to http://<PC_IP>:5254 or use ngrok HTTPS URL
- You need Android Studio to import the Gradle project
