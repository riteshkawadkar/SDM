plugins {
	id("com.android.application")
	kotlin("android")
	id("com.google.gms.google-services")
}

android {
	namespace = "com.example.sdmagent"
	compileSdk = 34

	defaultConfig {
		applicationId = "com.example.sdmagent"
		minSdk = 24
		targetSdk = 34
		versionCode = 1
		versionName = "1.0"
	}

	buildTypes {
		release {
			isMinifyEnabled = false
		}
	}

	compileOptions {
		sourceCompatibility = JavaVersion.VERSION_17
		targetCompatibility = JavaVersion.VERSION_17
	}
	kotlinOptions {
		jvmTarget = "17"
	}
}

dependencies {
	implementation("org.jetbrains.kotlin:kotlin-stdlib:1.8.21")
	implementation("com.squareup.retrofit2:retrofit:2.9.0")
	implementation("com.squareup.retrofit2:converter-moshi:2.9.0")
	implementation("com.squareup.okhttp3:logging-interceptor:4.10.0")
	implementation("androidx.security:security-crypto:1.1.0-alpha03")
	implementation("androidx.core:core-ktx:1.10.1")
	implementation("androidx.appcompat:appcompat:1.6.1")
	implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.6.1")
	implementation("com.google.android.material:material:1.9.0")
	implementation(platform("com.google.firebase:firebase-bom:33.1.2"))
	implementation("com.google.firebase:firebase-messaging-ktx")
	implementation("org.jetbrains.kotlinx:kotlinx-coroutines-play-services:1.6.4")
}
