plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

// Релизная подпись включается только если в окружении есть keystore.
// CI собирает debug APK, который можно поставить на телефон как есть.
val signingKeystore: String? = System.getenv("SIGNING_KEYSTORE")

android {
    namespace = "com.dezigwer.nothingmusic"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.dezigwer.nothingmusic"
        minSdk = 26
        targetSdk = 35
        versionCode = 1
        versionName = "1.0.0"
    }

    signingConfigs {
        if (signingKeystore != null) {
            create("release") {
                storeFile = rootProject.file(signingKeystore)
                storePassword = System.getenv("SIGNING_STORE_PASSWORD")
                keyAlias = System.getenv("SIGNING_KEY_ALIAS")
                keyPassword = System.getenv("SIGNING_KEY_PASSWORD")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
            if (signingKeystore != null) {
                signingConfig = signingConfigs.getByName("release")
            }
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
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.media:media:1.7.0")
}
