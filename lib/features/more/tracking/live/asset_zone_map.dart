import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/more/tracking/live/floor_plan_map.dart';
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';

String _n(String s) => s.trim().toLowerCase();

/// The zone of a plan with this name or code (case-insensitive), as location events name zones
/// by text only.
MapZone? zoneNamed(List<MapZone> zones, String? name) {
  if (name == null || name.trim().isEmpty) return null;
  final k = _n(name);
  return zones.where((z) => _n(z.name) == k || _n(z.code) == k).firstOrNull;
}

/// Zone id → the 1-based numbers of the trail events (oldest first) that happened in it.
Map<String, List<int>> trailNumbers(List<MapZone> zones, List<TrkLocationEvent> oldestFirst) {
  final out = <String, List<int>>{};
  for (final (i, e) in oldestFirst.indexed) {
    final z = zoneNamed(zones, e.zoneName);
    if (z != null) (out[z.zoneId] ??= []).add(i + 1);
  }
  return out;
}

/// One asset on the published plan of its registered location ([locationId] = the asset's floor,
/// else building, else site): its CURRENT zone ([zoneName]) highlighted, the zones of its [trail]
/// numbered in order, and its plotted marker (if an administrator plotted it). Positions are
/// zone-level: nothing is drawn at an invented x / y.
class AssetZoneMap extends ConsumerWidget {
  const AssetZoneMap({
    super.key,
    required this.locationId,
    required this.asset,
    this.zoneName,
    this.trail = const [],
    this.height = 300,
  });

  final String? locationId;
  final TrackedAsset? asset;
  final String? zoneName;

  /// Oldest first.
  final List<TrkLocationEvent> trail;
  final double height;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final id = locationId;
    if (id == null) {
      return const AppCard(
        child: EmptyState(
          icon: Icons.map_outlined,
          title: 'No location on the asset record',
          message: 'Set the asset\'s site / building / floor to see it on a floor plan.',
        ),
      );
    }
    final planV = ref.watch(publishedPlanProvider(id));
    return asyncBody(
      planV,
      onRetry: () => ref.invalidate(publishedPlanProvider(id)),
      builder: (plan) {
        if (plan == null) {
          return const AppCard(
            child: EmptyState(icon: Icons.map_outlined, title: 'No floor plan published for this floor'),
          );
        }
        final zonesV = ref.watch(planZonesProvider(plan.floorPlanId));
        final plotsV = ref.watch(planPlotsProvider(plan.floorPlanId));
        final url = plan.fileUrl;
        final imageV = plan.isImage && url != null ? ref.watch(planImageProvider(url)) : null;
        if (zonesV.hasError && !zonesV.hasValue) {
          return LoadErrorState(
            error: zonesV.error!,
            onRetry: () => ref.invalidate(planZonesProvider(plan.floorPlanId)),
          );
        }
        if (!zonesV.hasValue || (imageV != null && imageV.isLoading && !imageV.hasValue)) {
          return SizedBox(height: height, child: const ListSkeleton(rows: 3, card: false));
        }
        final zones = zonesV.requireValue;
        final a = asset;
        final ids = {
          if (a?.assetRegistrationId != null) _n(a!.assetRegistrationId!),
          for (final d in a?.devices ?? const <TrkDevice>[]) _n(d.deviceId),
        };
        final plots = [
          for (final p in plotsV.value ?? const <MapPlot>[])
            if (ids.contains(_n(p.itemId))) p,
        ];
        final current = zoneNamed(zones, zoneName);
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(
              height: height,
              child: FloorPlanMap(
                plan: plan,
                image: imageV?.value,
                zones: zones,
                plots: plots,
                layers: const MapLayerToggles(),
                snapshot: ref.watch(trkLiveProvider).snapshot,
                now: trackingNow(),
                highlightZones: {?current?.zoneId},
                trailZones: trailNumbers(zones, trail),
              ),
            ),
            if (zoneName != null && current == null) ...[
              const SizedBox(height: 6),
              Text('Zone "$zoneName" is not drawn on this plan (${plan.name}).', style: AppText.caption),
            ],
          ],
        );
      },
    );
  }
}
