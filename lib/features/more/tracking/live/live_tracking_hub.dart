import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_hub_lists.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_map_tab.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_tracking_assets_tab.dart';
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';
import 'package:asset360_mobile/features/more/tracking/tracking_hub_screen.dart' show HubTab;
import 'package:asset360_mobile/features/more/tracking/tracking_widgets.dart';

/// Online / Offline / Low Battery quick filters (Alerts opens the alerts list).
enum LiveCounter { online, offline, lowBattery, alerts }

/// API mode Tracking & IoT hub (design 06, left screen): Live Map / Asset List / Sensors /
/// Geofencing over the Tracking service and the Administration map system. No mock data.
/// Live data polls every 5 s while this screen is visible ([LivePollingScope]).
class LiveTrackingHubScreen extends ConsumerStatefulWidget {
  const LiveTrackingHubScreen({super.key, this.initialTab, this.view, this.location, this.zone, this.geofence});

  final String? initialTab;

  /// Live Map deep link (see [Routes.trackingMap]).
  final String? view, location, zone, geofence;

  @override
  ConsumerState<LiveTrackingHubScreen> createState() => _LiveTrackingHubScreenState();
}

class _LiveTrackingHubScreenState extends ConsumerState<LiveTrackingHubScreen> {
  late HubTab _tab = _tabOf(widget.initialTab) ?? HubTab.map;
  String _query = '';
  FilterSelection _filters = const {};
  LiveCounter? _counter;

  static HubTab? _tabOf(String? n) => HubTab.values.where((t) => t.name == n).firstOrNull;

  @override
  void didUpdateWidget(LiveTrackingHubScreen old) {
    super.didUpdateWidget(old);
    if (widget.initialTab != old.initialTab || widget.location != old.location || widget.geofence != old.geofence) {
      _tab = _tabOf(widget.initialTab) ?? _tab;
    }
  }

  void _onCounter(LiveCounter c) {
    if (c == LiveCounter.alerts) {
      context.push(Routes.trackingAlerts);
      return;
    }
    setState(() => _counter = _counter == c ? null : c);
  }

  /// The tracked assets the counters / search / filters let through.
  List<TrackedAsset> _filtered(TrackingSnapshot s) {
    final now = trackingNow();
    final status = _filters['Status'] ?? const {};
    final sites = _filters['Site'] ?? const {};
    return [
      for (final a in s.assets)
        if (a.matches(_query) &&
            switch (_counter) {
              LiveCounter.online => a.isOnline(now),
              LiveCounter.offline => !a.isOnline(now),
              LiveCounter.lowBattery => a.lowBattery,
              _ => true,
            } &&
            (status.isEmpty ||
                status.any(
                  (v) => switch (v) {
                    'Online' => a.isOnline(now),
                    'Offline' => !a.isOnline(now),
                    'Low Battery' => a.lowBattery,
                    'In a zone' => a.zone != null,
                    _ => false,
                  },
                )) &&
            (sites.isEmpty || sites.contains(a.siteName ?? 'No site')))
          a,
    ];
  }

  @override
  Widget build(BuildContext context) {
    final live = ref.watch(trkLiveProvider);
    final s = live.snapshot;
    final now = trackingNow();
    final siteNames = {for (final a in s?.assets ?? const <TrackedAsset>[]) a.siteName ?? 'No site'}.toList()..sort();
    return LivePollingScope(
      child: AppScaffold(
        body: RefreshIndicator(
          onRefresh: () async => refreshTracking(ref),
          child: ListView(
            padding: context.pagePadding,
            children: [
              const PageTitle(
                title: 'Tracking & IoT',
                subtitle: 'Real-time location, telemetry and asset monitoring',
                help:
                    'Live data from the Tracking service, refreshed every 5 seconds while this screen is open. '
                    'Positions are zone-level (the zone of each asset\'s latest location event); floor plans and '
                    'their zones come from Administration → Maps. Markers turn grey when a device has not been '
                    'heard from for 10 minutes.',
              ),
              const SizedBox(height: 14),
              TrackingHubTabs<HubTab>(
                tabs: [for (final t in HubTab.values) (t, t.label, t.icon)],
                selected: _tab,
                onChanged: (v) => setState(() => _tab = v),
              ),
              const SizedBox(height: 14),
              if (_tab != HubTab.sensors) ...[
                if (_tab != HubTab.list) ...[
                  SearchFilterBar(
                    hint: 'Search by asset name, ID or location...',
                    onChanged: (v) => setState(() => _query = v),
                    filterGroups: [
                      const FilterGroup(title: 'Status', options: ['Online', 'Offline', 'Low Battery', 'In a zone']),
                      if (siteNames.isNotEmpty) FilterGroup(title: 'Site', options: siteNames),
                    ],
                    selection: _filters,
                    onFiltersChanged: (f) => setState(() => _filters = f),
                  ),
                  const SizedBox(height: 12),
                ],
                CounterTiles<LiveCounter>(
                  items: [
                    (LiveCounter.online, Icons.battery_full_rounded, s?.online(now) ?? 0, 'Online', TileTone.mint),
                    (LiveCounter.offline, Icons.battery_alert_rounded, s?.offline(now) ?? 0, 'Offline', TileTone.pink),
                    (
                      LiveCounter.lowBattery,
                      Icons.signal_cellular_alt_rounded,
                      s?.lowBattery ?? 0,
                      'Low Battery',
                      TileTone.peach,
                    ),
                    (
                      LiveCounter.alerts,
                      Icons.warning_rounded,
                      live.alertCount?.unresolved ?? 0,
                      'Alerts',
                      TileTone.sky,
                    ),
                  ],
                  selected: _counter,
                  onTap: _onCounter,
                ),
                const SizedBox(height: 12),
              ],
              if (s == null && live.error != null && (_tab == HubTab.map || _tab == HubTab.zones))
                LoadErrorState(error: live.error!, onRetry: () => ref.read(trkLiveProvider.notifier).refresh())
              else
                ...switch (_tab) {
                  // Geofencing shows the live map for now (zone drawing is not offered in API mode).
                  HubTab.map || HubTab.zones => [
                    LiveMapTab(
                      key: ValueKey('map-${widget.location}-${widget.geofence}-${widget.zone}'),
                      initialView: widget.view,
                      initialLocation: widget.location,
                      focusZone: widget.zone,
                      focusGeofence: widget.geofence,
                      visibleAssets: s == null ? null : _filtered(s),
                    ),
                  ],
                  HubTab.list => [TrackingAssetsTab(counter: _counter)],
                  HubTab.sensors => [const LiveSensorsTab()],
                },
            ],
          ),
        ),
      ),
    );
  }
}

/// Colour of the tracked-asset dot in lists (shared with the map card).
Color trackedDotColor(TrackedAsset a, DateTime now) =>
    a.isOnline(now) ? (a.lowBattery ? AppColors.orange : AppColors.green) : AppColors.grey;
