import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/data/repositories/home_floor_repository.dart';
import 'package:asset360_mobile/features/home/drilldowns/drilldown_page.dart';
import 'package:asset360_mobile/features/home/sections/live_floor_preview.dart';

/// API mode Asset Location (Home "Asset Location (Live)" → View Map): the same live INDOOR floor plan as the Home
/// card (the floor plan with the most located assets, zones, gateways, assets in their zone grid), taller. Tap it (or
/// the button) to open Tracking → Real-Time Location on that floor. No GPS / outdoor map and no Dashboard
/// /mobile/asset-locations call.
class LiveAssetLocationScreen extends ConsumerWidget {
  const LiveAssetLocationScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) => DrilldownPage(
    title: 'Asset Location (Live)',
    subtitle: 'Live indoor floor plan',
    skeletonRows: 3,
    onRefresh: () => ref.invalidate(homeFloorProvider),
    children: [
      const LiveFloorPreview(mapHeight: 360),
      const SizedBox(height: 14),
      FilledButton.icon(
        key: const ValueKey('home-floor-open'),
        onPressed: () => openRealTimeLocation(context, floorPlanId: ref.read(homeFloorProvider).value?.plan.floorPlanId),
        icon: const Icon(Icons.my_location_rounded, size: 18),
        label: const Text('Open Real-Time Location'),
      ),
    ],
  );
}
