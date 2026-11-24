import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/repositories/home_floor_repository.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart' show planImageProvider;
import 'package:asset360_mobile/features/home/dash_ui.dart';
import 'package:asset360_mobile/features/more/tracking/live/floor_plan_map.dart' show frameFor;
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_map.dart';
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart' show LivePollingScope;

/// Opens Tracking → Real-Time Location on [floorPlanId]'s floor, or the licence sheet when Tracking is locked.
void openRealTimeLocation(BuildContext context, {String? floorPlanId}) =>
    LicenceGuard.open(context, Routes.trackingMap(view: 'rtl', floorPlan: floorPlanId));

/// The live INDOOR floor plan Home's "Asset Location (Live)" card and its drill-down show: the floor plan with
/// the most located assets (`homeFloorProvider`), its zones, gateways and assets in the same zone grid as the
/// Real-Time Location map (`RtlFloorMap` + `RtlAnimatedMarkerLayer`), kept live by the SAME [rtlProvider] (one tags
/// socket; it runs only while this widget is on screen: `LivePollingScope`). Header: floor name, "N assets · M
/// online" and a "Live" dot while the socket is connected. Tap = Real-Time Location on that floor. No GPS / outdoor map.
/// Reads Tracking data: show it only when Tracking is licensed ([LiveFloorPreview.locked] otherwise).
class LiveFloorPreview extends ConsumerWidget {
  const LiveFloorPreview({super.key, this.mapHeight = 150});

  final double mapHeight;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (!ref.watch(licenceProvider).isLicensed(AppModule.tracking)) return const _Locked();
    final floor = ref.watch(homeFloorProvider);
    if (floor.hasError && !floor.isLoading) {
      return LoadErrorState(error: floor.error!, onRetry: () => ref.invalidate(homeFloorProvider));
    }
    if (!floor.hasValue) return const ListSkeleton(rows: 2, card: false);
    final f = floor.requireValue;
    if (f == null) {
      return const DashEmpty(
        key: ValueKey('home-floor-empty'),
        icon: Icons.location_off_outlined,
        title: 'No assets on the floor plans yet',
        message: 'Assets appear here once a gateway on a floor plan hears their tag.',
      );
    }
    return _FloorBody(floor: f, mapHeight: mapHeight);
  }
}

/// Tracking not licensed: no Tracking calls; a tap shows the licence sheet.
class _Locked extends StatelessWidget {
  const _Locked();

  @override
  Widget build(BuildContext context) => InkWell(
    key: const ValueKey('home-floor-locked'),
    onTap: () => openRealTimeLocation(context),
    child: const DashEmpty(
      icon: Icons.lock_outline_rounded,
      title: 'Tracking is not licensed',
      message: 'Tap to see what is included in your plan.',
    ),
  );
}

class _FloorBody extends ConsumerStatefulWidget {
  const _FloorBody({required this.floor, required this.mapHeight});

  final HomeFloor floor;
  final double mapHeight;

  @override
  ConsumerState<_FloorBody> createState() => _FloorBodyState();
}

class _FloorBodyState extends ConsumerState<_FloorBody> {
  @override
  Widget build(BuildContext context) {
    final plan = widget.floor.plan;
    final rtl = ref.watch(rtlProvider);
    // The tags socket follows this floor while Home is the screen on show (the Tracking screen sets its own scope
    // when it is; after this frame: providers can't change while building).
    if (TickerMode.of(context) && (rtl.siteId != null || rtl.floorPlanId != plan.floorPlanId)) {
      scheduleMicrotask(() {
        if (mounted) ref.read(rtlProvider.notifier).setScope(floorPlanId: plan.floorPlanId);
      });
    }
    final image = plan.fileUrl == null ? null : ref.watch(planImageProvider(plan.fileUrl!));
    final zones = ref.watch(rtlZonesProvider(plan.floorPlanId)).value ?? const <MapZone>[];
    final plots = ref.watch(rtlDevicePlotsProvider(plan.floorPlanId)).value ?? const <MapPlot>[];
    final overview = rtl.overview;
    final live = overview == null
        ? null
        : [
            for (final a in overview.assets)
              if (a.location?.floorPlanId?.toLowerCase() == plan.floorPlanId.toLowerCase()) a,
          ];
    final assets = live ?? const <RtlAsset>[];
    final total = live?.length ?? widget.floor.assets;
    final online = assets.where((a) => a.freshness.toLowerCase() == 'live').length;
    final placement = placeAssets(assets, plots, zones: zones, frame: frameFor(plan, image?.value));
    final connected = rtl.polling && rtl.socketUp;
    void open() => openRealTimeLocation(context, floorPlanId: plan.floorPlanId);
    return LivePollingScope(
      poller: (ref) => ref.read(rtlProvider.notifier),
      child: InkWell(
        key: const ValueKey('home-floor-card'),
        onTap: open,
        borderRadius: BorderRadius.circular(10),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    widget.floor.floorName,
                    key: const ValueKey('home-floor-name'),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppText.bodyStrong,
                  ),
                ),
                if (connected) ...[
                  const Icon(Icons.circle, key: ValueKey('home-floor-live'), size: 9, color: AppColors.green),
                  const SizedBox(width: 4),
                  Text('Live', style: AppText.caption.copyWith(color: AppColors.green, fontWeight: FontWeight.w700)),
                ],
              ],
            ),
            const SizedBox(height: 2),
            Text(
              '$total ${total == 1 ? 'asset' : 'assets'} · $online online',
              key: const ValueKey('home-floor-counts'),
              style: AppText.caption,
            ),
            const SizedBox(height: 8),
            SizedBox(
              height: widget.mapHeight,
              child: image != null && image.isLoading
                  ? const Center(child: CircularProgressIndicator())
                  : IgnorePointer(
                      child: RtlFloorMap(
                        key: ValueKey('home-floor-map-${plan.floorPlanId}'),
                        plan: plan,
                        image: image?.value,
                        zones: zones,
                        placement: placement,
                        overview: overview,
                        assets: assets,
                        preview: true,
                      ),
                    ),
            ),
          ],
        ),
      ),
    );
  }
}
