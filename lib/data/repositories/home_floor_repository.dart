import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/data/api/api_config.dart' show showServerSwitcher;
import 'package:asset360_mobile/data/repositories/live_locations_repository.dart' show visibleFloorPlansProvider;
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';

/// The floor Home's "Asset Location (Live)" card shows.
class HomeFloor {
  const HomeFloor({required this.plan, required this.assets, required this.floorName});

  final MapFloorPlan plan;

  /// Located assets on it (GET /overview, all floors).
  final int assets;

  /// The Master Data floor name, else the plan's name.
  final String floorName;
}

/// The busiest floor plan: GET /api/real-time-location/overview (every floor, the same call and rows the Real-Time
/// Location map is built on) counted per floor plan (`busiestFloorPlan`). Null = nothing located on a floor plan yet.
/// Tracking calls only: read it only when Tracking is licensed.
final homeFloorProvider = FutureProvider.autoDispose<HomeFloor?>((ref) async {
  final assets = await ref.watch(rtlAllAssetsProvider(null).future);
  final plans = await ref.watch(visibleFloorPlansProvider.future);
  final best = busiestFloorPlan(assets, plans, dev: showServerSwitcher);
  if (best == null) return null;
  var name = best.plan.name;
  try {
    final floors = await ref.watch(rtlFloorOptionsProvider.future);
    final floor = floors.where((f) => f.floorId == best.plan.locationId).firstOrNull;
    if (floor != null && floor.floor.name.isNotEmpty) name = floor.floor.name;
  } catch (_) {
    // The plan's own name stays.
  }
  return HomeFloor(plan: best.plan, assets: best.assets, floorName: name.isEmpty ? 'Floor plan' : name);
});
