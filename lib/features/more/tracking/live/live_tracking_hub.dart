import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/repositories/live_locations_repository.dart' show refreshLocations;
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_hub_lists.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_location_tree.dart';
import 'package:asset360_mobile/features/more/tracking/live/live_tracking_assets_tab.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart' show refreshRtl;
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_view.dart';
import 'package:asset360_mobile/features/more/tracking/live/tracking_live_ui.dart';
import 'package:asset360_mobile/features/more/tracking/tracking_hub_screen.dart' show HubTab;
import 'package:asset360_mobile/features/more/tracking/tracking_widgets.dart';

/// Online / Offline / Low Battery quick filters (Alerts opens the alerts list).
enum LiveCounter { online, offline, lowBattery, alerts }

/// API mode Tracking & IoT hub (design 06, left screen): Live Map / Asset List / Sensors /
/// Geofencing over the Tracking service and the Administration map system. No mock data.
/// Live data polls every 5 s while this screen is visible ([LivePollingScope]).
class LiveTrackingHubScreen extends ConsumerStatefulWidget {
  const LiveTrackingHubScreen({
    super.key,
    this.initialTab,
    this.view,
    this.location,
    this.zone,
    this.geofence,
    this.asset,
    this.plot,
  });

  final String? initialTab;

  /// Live Map deep link (see [Routes.trackingMap]).
  final String? view, location, zone, geofence;

  /// View on Map: the asset to focus and its plot key on the plan.
  final String? asset, plot;

  @override
  ConsumerState<LiveTrackingHubScreen> createState() => _LiveTrackingHubScreenState();
}

class _LiveTrackingHubScreenState extends ConsumerState<LiveTrackingHubScreen> {
  late HubTab _tab = _tabOf(widget.initialTab) ?? HubTab.map;

  /// Live Map / Geofencing show Real-Time Location; "Locations" = the location tree / floor plans /
  /// outdoor map (kept for deep links into a site, floor, zone or geofence).
  late bool _realTime = _wantsRealTime(widget);
  String _query = '';
  FilterSelection _filters = const {};
  LiveCounter? _counter;

  static HubTab? _tabOf(String? n) => HubTab.values.where((t) => t.name == n).firstOrNull;

  static bool _wantsRealTime(LiveTrackingHubScreen w) =>
      w.view == 'rtl' ||
      (w.view == null && w.location == null && w.zone == null && w.geofence == null && w.asset == null);

  @override
  void didUpdateWidget(LiveTrackingHubScreen old) {
    super.didUpdateWidget(old);
    if (widget.initialTab != old.initialTab ||
        widget.location != old.location ||
        widget.geofence != old.geofence ||
        widget.asset != old.asset ||
        widget.view != old.view) {
      _tab = _tabOf(widget.initialTab) ?? _tab;
      _realTime = _wantsRealTime(widget);
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
          onRefresh: () async {
            refreshTracking(ref);
            if (_tab == HubTab.map || _tab == HubTab.zones) {
              _realTime ? refreshRtl(ref) : refreshLocations(ref);
            }
          },
          child: ListView(
            padding: context.pagePadding,
            children: [
              const PageTitle(
                title: 'Tracking & IoT',
                subtitle: 'Real-time location, telemetry and asset monitoring',
                help:
                    'Live data from the Tracking service, refreshed every 5 seconds while this screen is open. '
                    'Live Map places each asset at the site of the gateway that last heard its tag (location '
                    'updates every 10 seconds); it switches to the map when sites, buildings or floors have '
                    'coordinates. Floor plans come from Administration → Maps or the Master Data floor picture. '
                    'Assets are offline when not heard from for 10 minutes.',
              ),
              const SizedBox(height: 14),
              TrackingHubTabs<HubTab>(
                tabs: [for (final t in HubTab.values) (t, t.label, t.icon)],
                selected: _tab,
                onChanged: (v) => setState(() => _tab = v),
              ),
              const SizedBox(height: 14),
              if (_tab == HubTab.map || _tab == HubTab.zones) ...[
                SegmentedButton<bool>(
                  key: const ValueKey('map-mode'),
                  showSelectedIcon: false,
                  segments: const [
                    ButtonSegment(value: true, label: Text('Real-time'), icon: Icon(Icons.my_location_rounded)),
                    ButtonSegment(value: false, label: Text('Locations'), icon: Icon(Icons.account_tree_outlined)),
                  ],
                  selected: {_realTime},
                  onSelectionChanged: (v) => setState(() => _realTime = v.first),
                ),
                const SizedBox(height: 12),
              ],
              if ((_tab == HubTab.map || _tab == HubTab.zones) && _realTime)
                RealTimeLocationView(
                  key: ValueKey('rtl-${widget.asset}'),
                  focusAsset: widget.view == 'rtl' ? widget.asset : null,
                )
              else if (_tab != HubTab.sensors) ...[
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
              if ((_tab == HubTab.map || _tab == HubTab.zones) && _realTime)
                const SizedBox.shrink()
              else if (s == null && live.error != null && (_tab == HubTab.map || _tab == HubTab.zones))
                LoadErrorState(error: live.error!, onRetry: () => ref.read(trkLiveProvider.notifier).refresh())
              else
                ...switch (_tab) {
                  // Geofencing shows the same live location view (no zone drawing on mobile).
                  HubTab.map || HubTab.zones => [
                    LiveLocationView(
                      key: ValueKey(
                        'loc-${widget.view}-${widget.location}-${widget.geofence}-${widget.zone}-${widget.asset}',
                      ),
                      focusAsset: widget.asset,
                      focusPlot: widget.plot,
                      initialView: widget.view,
                      initialLocation: widget.location,
                      focusZone: widget.zone,
                      focusGeofence: widget.geofence,
                      visibleAssets: s == null ? null : _filtered(s),
                      query: _query,
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
