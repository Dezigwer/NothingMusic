plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

// Релизная подпись включается, если в окружении есть keystore (CI-секреты).
// Локально без keystore release собирается debug-ключом, чтобы APK всё равно
// можно было поставить на телефон.
val signingKeystore: String? = System.getenv("SIGNING_KEYSTORE")
val hasSigningKey = signingKeystore != null

android {
    namespace = "com.dezigwer.nothingmusic"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.dezigwer.nothingmusic"
        minSdk = 26
        targetSdk = 35
        versionCode = 2
        versionName = "1.0.1"
    }

    signingConfigs {
        if (hasSigningKey) {
            create("release") {
                storeFile = rootProject.file(signingKeystore!!)
                storePassword = System.getenv("SIGNING_STORE_PASSWORD")
                keyAlias = System.getenv("SIGNING_KEY_ALIAS")
                keyPassword = System.getenv("SIGNING_KEY_PASSWORD")
                // Ключ сгенерирован как PKCS#12 (.p12), а не JKS.
                storeType = "PKCS12"
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
            signingConfig = if (hasSigningKey) {
                signingConfigs.getByName("release")
            } else {
                signingConfigs.getByName("debug")
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
