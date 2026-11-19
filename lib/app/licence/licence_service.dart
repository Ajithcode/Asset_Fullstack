import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/app/licence/app_module.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';

/// `--dart-define=LICENSED_MODULES=dashboard,assets,…` (module keys, comma-separated).
/// Empty / missing = [defaultLicensedModules].
const String kLicensedModules = String.fromEnvironment('LICENSED_MODULES');

/// Tests only: replaces the build setting (test/flutter_test_config.dart licenses every
/// module so the module tests see the full app; licence tests set their own list).
Set<AppModule>? debugLicensedModules;

/// Where the current licence came from.
enum LicenceSource { backend, buildSetting, defaults }

/// The modules from the `LICENSED_MODULES` value [raw]; unknown keys are ignored.
/// Returns null when [raw] names no known module (→ use the defaults).
Set<AppModule>? parseLicensedModules(String raw) {
  final found = <AppModule>{};
  for (final part in raw.split(',')) {
    if (part.trim().isEmpty) continue;
    final m = AppModule.fromKey(part);
    if (m == null) {
      debugPrint('LICENSED_MODULES: unknown module "${part.trim()}" ignored');
    } else {
      found.add(m);
    }
  }
  return found.isEmpty ? null : found;
}

/// The licence before the backend answers: build setting [raw], else the defaults.
LicenceState licenceFromBuildSetting(String raw) {
  final parsed = parseLicensedModules(raw);
  return parsed == null
      ? const LicenceState(defaultLicensedModules, LicenceSource.defaults)
      : LicenceState(parsed, LicenceSource.buildSetting);
}

@immutable
class LicenceState {
  const LicenceState(this.licensed, this.source);

  final Set<AppModule> licensed;
  final LicenceSource source;

  bool isLicensed(AppModule module) => licensed.contains(module);

  /// Whether [location] may show its screen: always-open screens (no module) or a licensed module.
  bool allowsLocation(String location) {
    final m = moduleForLocation(location);
    return m == null || isLicensed(m);
  }

  /// The locked module [location] belongs to, or null when it may open.
  AppModule? lockedModuleOf(String location) {
    final m = moduleForLocation(location);
    return m == null || isLicensed(m) ? null : m;
  }
}

// ---------------------------------------------------------------- backend (not available yet)

/// The backend licence endpoint.
///
/// TODO(licence-api): no licence endpoint exists yet. When the backend ships one,
/// implement this (GET the tenant's licensed module keys with the session token, using
/// the app's ApiClient) and return it from [licenceApiProvider]. [LicenceNotifier] then:
///   * fetches it after sign-in (session → loggedIn),
///   * caches the result ([LicenceCache]) so the next start uses it before the network answers,
///   * refreshes it on app start ([LicenceNotifier.refresh] from Splash / restore).
abstract interface class LicenceApi {
  /// The licensed module keys (same keys as `LICENSED_MODULES`).
  Future<List<String>> fetchLicensedModuleKeys();
}

/// Cache of the last backend licence (e.g. shared_preferences). Not wired until the API exists.
abstract interface class LicenceCache {
  Future<List<String>?> read();
  Future<void> write(List<String> keys);
}

/// TODO(licence-api): return the real implementation once the endpoint exists.
final licenceApiProvider = Provider<LicenceApi?>((_) => null);

/// TODO(licence-api): return a shared_preferences cache together with the API.
final licenceCacheProvider = Provider<LicenceCache?>((_) => null);

// ---------------------------------------------------------------- service

/// LicenceService: decides `isLicensed(module)`.
/// Order: (a) backend licence (TODO, [LicenceApi]) → (b) `LICENSED_MODULES` → (c) defaults.
class LicenceNotifier extends Notifier<LicenceState> {
  @override
  LicenceState build() {
    // Fetch the backend licence after every sign-in (no-op until the API exists).
    ref.listen(sessionProvider, (prev, next) {
      if (next.loggedIn && !(prev?.loggedIn ?? false)) refresh();
    });
    final debug = debugLicensedModules;
    if (debug != null) return LicenceState(debug, LicenceSource.buildSetting);
    return licenceFromBuildSetting(kLicensedModules);
  }

  bool isLicensed(AppModule module) => state.isLicensed(module);

  /// Loads the cached backend licence, then asks the backend. Without a [LicenceApi]
  /// (today) the build setting / defaults stay in force.
  Future<void> refresh() async {
    final api = ref.read(licenceApiProvider);
    if (api == null) return;
    final cache = ref.read(licenceCacheProvider);
    final cached = await cache?.read();
    if (cached != null && ref.mounted) _apply(cached);
    try {
      final keys = await api.fetchLicensedModuleKeys();
      if (!ref.mounted) return;
      _apply(keys);
      await cache?.write(keys);
    } catch (e) {
      debugPrint('Licence refresh failed, keeping ${state.source.name}: $e');
    }
  }

  void _apply(List<String> keys) {
    final modules = {for (final k in keys) ?AppModule.fromKey(k)};
    state = LicenceState(modules, LicenceSource.backend);
  }
}

final licenceProvider = NotifierProvider<LicenceNotifier, LicenceState>(LicenceNotifier.new);
