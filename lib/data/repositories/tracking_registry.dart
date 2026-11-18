import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/core/scanning/live_asset_lookup.dart';
import 'package:asset360_mobile/data/api/assets/asset_api_models.dart' show LookupCodeType;
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';

/// API mode: which Tracking service records belong to a registered asset (Asset service).
/// The Tracking → Asset List shows Asset service assets only; records that match none are
/// counted as "Unregistered tracking records".

/// What a tracking record is looked up by: its registration id (device / tag / health row named
/// one) and its asset code / location-event asset text.
typedef TrkLookupKey = ({String? id, String? code});

TrkLookupKey trkLookupKeyOf(TrackedAsset a) => (id: a.assetRegistrationId, code: a.code ?? a.key);

/// The registration id a tracking record belongs to, or null when the Asset service knows no asset
/// for it. GET /api/asset-registrations/lookup with the registration id first, then the code (asset
/// tag / barcode / RFID / NFC). A code matching several assets counts as registered (first id).
final trkRegistrationOfProvider = FutureProvider.autoDispose.family<String?, TrkLookupKey>((ref, k) async {
  final lookup = ref.watch(liveAssetLookupProvider);
  final attempts = [
    if (k.id case final id? when id.trim().isNotEmpty) (id, LookupCodeType.assetRegistrationId),
    if (k.code case final code? when code.trim().isNotEmpty && code != k.id) (code, null),
  ];
  for (final (code, type) in attempts) {
    final r = await lookup.find(code, type: type);
    switch (r.status) {
      case LiveLookupStatus.found:
        return r.id;
      case LiveLookupStatus.ambiguous:
        return r.ids.firstOrNull ?? code;
      case LiveLookupStatus.notFound:
        continue;
    }
  }
  return null;
});

/// The tracking records sorted into registered / unregistered.
class TrackingRegistry {
  const TrackingRegistry({this.registrationOf = const {}, this.unregistered = const [], this.unchecked = 0});

  /// Tracked asset key → assetRegistrationId, for every record the Asset service knows.
  final Map<String, String> registrationOf;

  /// Records no registered asset matches (read-only list).
  final List<TrackedAsset> unregistered;

  /// Records whose lookup failed (network / server error): neither registered nor unregistered.
  final int unchecked;

  /// The tracking record of a registered asset: direct match on registration id / asset tag, else
  /// the record whose lookup resolved to [assetRegistrationId] (e.g. matched by barcode).
  TrackedAsset? recordFor(TrackingSnapshot s, String assetRegistrationId, String? assetTag) {
    final direct = s.forRegistered(assetRegistrationId, assetTag);
    if (direct != null) return direct;
    for (final MapEntry(:key, :value) in registrationOf.entries) {
      if (value == assetRegistrationId) return s.byKey(key);
    }
    return null;
  }
}

/// Resolves every record of the live snapshot (re-run only when the set of records changes; each
/// lookup is cached per record while the list is open).
final trackingRegistryProvider = FutureProvider.autoDispose<TrackingRegistry?>((ref) async {
  ref.watch(
    trkLiveProvider.select(
      (s) => s.snapshot?.assets.map((a) => '${a.key}|${a.assetRegistrationId}|${a.code}').join('\n'),
    ),
  );
  final s = ref.read(trkLiveProvider).snapshot;
  if (s == null) return null;
  // Every watch before the first await.
  final pending = [
    for (final a in s.assets)
      ref
          .watch(trkRegistrationOfProvider(trkLookupKeyOf(a)).future)
          .then<(TrackedAsset, String?, bool)>((id) => (a, id, true), onError: (Object _) => (a, null, false)),
  ];
  final results = await Future.wait(pending);
  return TrackingRegistry(
    registrationOf: {for (final (a, id, _) in results) a.key: ?id},
    unregistered: [
      for (final (a, id, ok) in results)
        if (ok && id == null) a,
    ],
    unchecked: results.where((r) => !r.$3).length,
  );
});
