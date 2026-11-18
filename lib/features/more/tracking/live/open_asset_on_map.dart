import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/api/api_config.dart' show apiMode;
import 'package:asset360_mobile/data/repositories/asset_map_repository.dart';

/// THE "View on Map" / "Show on map" action (asset detail, More Actions, scan result, Asset
/// Tracking). API mode opens the Live Map focused on the asset ([resolveAssetMapTarget]):
/// - on a floor plan (its plot, or its live zone via its tag) → that plan, zoomed + highlighted;
/// - else coordinates → the outdoor map centred on its marker, card open;
/// - else a SnackBar "This asset has no map location yet".
/// Demo (mock) mode keeps the demo Asset Tracking map.
Future<void> openAssetOnMap(BuildContext context, String assetId) async {
  if (!apiMode) {
    context.push(Routes.trackAsset(assetId));
    return;
  }
  final c = ProviderScope.containerOf(context, listen: false);
  final target = await resolveAssetMapTarget(c, assetId);
  if (!context.mounted) return;
  switch (target) {
    case FloorPlanTarget(:final locationId, :final zoneId, :final plotKey):
      context.push(
        Routes.trackingMap(view: 'floor', location: locationId, zone: zoneId, asset: assetId, plot: plotKey),
      );
    case OutdoorTarget():
      context.push(Routes.trackingMap(view: 'outdoor', asset: assetId));
    case NoMapTarget():
      context.showSnack('This asset has no map location yet');
  }
}
