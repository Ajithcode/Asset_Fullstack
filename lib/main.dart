import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker_android/image_picker_android.dart';
import 'package:image_picker_platform_interface/image_picker_platform_interface.dart';

import 'package:asset360_mobile/app/app.dart';
import 'package:asset360_mobile/app/app_restart.dart';
import 'package:asset360_mobile/app/theme/app_theme.dart';
import 'package:asset360_mobile/core/scanning/scan_service.dart';
import 'package:asset360_mobile/data/repositories/server_repository.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // Gallery picks use the system photo picker (no storage permission; backported below Android 13).
  final picker = ImagePickerPlatform.instance;
  if (picker is ImagePickerAndroid) picker.useAndroidPhotoPicker = true;
  SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
  SystemChrome.setSystemUIOverlayStyle(AppTheme.headerOverlay);
  loadDeviceLabel(); // model + OS saved with each scan (Local Data)
  await loadSavedServer(); // dev builds: the Backend server chosen in Settings → Developer
  runApp(const AppRestartScope(child: Asset360App()));
}
