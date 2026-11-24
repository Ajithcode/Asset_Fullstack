import java.util.Base64

plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// The build's --dart-define / --dart-define-from-file values (Flutter passes them base64-encoded).
val dartDefines: Map<String, String> =
    (project.findProperty("dart-defines") as String?)
        ?.split(",")
        ?.mapNotNull { runCatching { String(Base64.getDecoder().decode(it)) }.getOrNull() }
        ?.mapNotNull { e -> e.indexOf('=').takeIf { it > 0 }?.let { e.substring(0, it) to e.substring(it + 1) } }
        ?.toMap() ?: emptyMap()

// Plain http only for development builds that use the office IPs: a plain-http service URL or the server
// switcher (it offers the .26 / .29 presets). Every service URL left unset defaults to the https demo cloud
// (lib/data/api/api_config.dart), so a plain `flutter build apk` gets the STRICT config (no cleartext).
val cleartextForOfficeIps: Boolean =
    dartDefines.values.any { it.trim().startsWith("http://", ignoreCase = true) } ||
        dartDefines["SHOW_SERVER_SWITCHER"] == "true"

android {
    namespace = "com.purpleiq.asset360_mobile"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        // flutter_local_notifications needs core library desugaring (alert notifications).
        isCoreLibraryDesugaringEnabled = true
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    defaultConfig {
        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).
        applicationId = "com.purpleiq.asset360_mobile"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = 24 // mobile_scanner needs >= 23; Flutter 3.38 needs 24
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
        manifestPlaceholders["networkSecurityConfig"] =
            if (cleartextForOfficeIps) "@xml/network_security_config_dev" else "@xml/network_security_config"
    }

    buildTypes {
        release {
            // TODO: Add your own signing config for the release build.
            // Signing with the debug keys for now, so `flutter run --release` works.
            signingConfig = signingConfigs.getByName("debug")
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }
}

dependencies {
    coreLibraryDesugaring("com.android.tools:desugar_jdk_libs:2.1.4")
}

flutter {
    source = "../.."
}
