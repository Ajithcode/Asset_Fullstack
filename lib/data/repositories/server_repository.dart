import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:asset360_mobile/data/api/api_config.dart';

/// The backend server chosen in the server switcher (dev builds with SHOW_SERVER_SWITCHER=true).
/// Saved on the device; null = the build's own URLs (--dart-define). The app applies it once at
/// start-up ([loadSavedServer]) and on every switch ([ServerNotifier.select]).
class ServerNotifier extends Notifier<String?> {
  static const storageKey = 'backend.serverOrigin';

  @override
  String? build() => serverOverride;

  /// Saves [origin] (null or the build's own server = back to the build default) and points every
  /// service at it. Callers sign out and restart first: see `switchServer` (core/server_switch.dart).
  Future<void> select(String? origin) async {
    var o = normalizeServerOrigin(origin);
    if (o == buildServerOrigin) o = null;
    applyServerOverride(o);
    state = o;
    try {
      final p = await SharedPreferences.getInstance();
      if (o == null) {
        await p.remove(storageKey);
      } else {
        await p.setString(storageKey, o);
      }
    } catch (_) {
      // Not saved: the app uses the build's server after the next restart.
    }
  }
}

/// Watched by every service Dio, so a new server builds new clients.
final serverProvider = NotifierProvider<ServerNotifier, String?>(ServerNotifier.new);

/// Applies the saved server before the app starts. Builds without the switcher flag ignore a saved
/// choice (client builds always use their own URLs).
Future<void> loadSavedServer() async {
  if (!showServerSwitcher) {
    applyServerOverride(null);
    return;
  }
  try {
    final p = await SharedPreferences.getInstance();
    applyServerOverride(p.getString(ServerNotifier.storageKey));
  } catch (e) {
    debugPrint('Saved server not read: $e');
  }
}
