import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_asset_sheet.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_ui.dart';

/// Range chips (1h / 24h / 7d) + event type dropdown (from the reply's `eventTypes`).
class RtlEventFilters extends StatelessWidget {
  const RtlEventFilters({
    super.key,
    required this.range,
    required this.eventType,
    required this.eventTypes,
    required this.onRange,
    required this.onType,
  });

  final String range;
  final String? eventType;
  final List<String> eventTypes;
  final ValueChanged<String> onRange;
  final ValueChanged<String?> onType;

  @override
  Widget build(BuildContext context) {
    final types = {...eventTypes, ?eventType}.toList();
    return Row(
      children: [
        for (final r in RtlApi.eventRanges) ...[
          ChoiceChip(
            key: ValueKey('events-range-$r'),
            label: Text(r),
            selected: r == range,
            onSelected: (_) => onRange(r),
          ),
          const SizedBox(width: 6),
        ],
        const Spacer(),
        Flexible(
          child: DropdownButton<String?>(
            key: const ValueKey('events-type'),
            isExpanded: true,
            value: eventType,
            hint: const Text('All types'),
            underline: const SizedBox.shrink(),
            items: [
              const DropdownMenuItem<String?>(value: null, child: Text('All types')),
              for (final t in types) DropdownMenuItem<String?>(value: t, child: Text(t, overflow: TextOverflow.ellipsis)),
            ],
            onChanged: onType,
          ),
        ),
      ],
    );
  }
}

/// One event row: time, asset, type with icon, zone, details, severity colour. Tap → asset card.
class RtlEventRow extends StatelessWidget {
  const RtlEventRow({super.key, required this.event, this.onShowOnMap});

  final RtlEvent event;
  final ValueChanged<String>? onShowOnMap;

  @override
  Widget build(BuildContext context) {
    final e = event;
    final tone = rtlSeverityTone(e.severity);
    final when = e.at == null ? '' : DateFormat('d MMM, HH:mm').format(e.at!.toLocal());
    return InkWell(
      key: ValueKey('rtl-event-${e.eventId}'),
      onTap: () => openRtlAssetById(
        context,
        e.assetId,
        onShowOnMap: onShowOnMap == null || e.assetId == null ? null : () => onShowOnMap!(e.assetId!),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 34,
              height: 34,
              decoration: BoxDecoration(color: tone.background, shape: BoxShape.circle),
              child: Icon(rtlEventIcon(e.eventType), size: 18, color: tone.foreground),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(child: Text(e.eventType, style: AppText.bodyStrong)),
                      Text(when, style: AppText.caption),
                    ],
                  ),
                  Text(
                    [e.assetId, e.assetName].nonNulls.where((s) => s.isNotEmpty).join(' · '),
                    style: AppText.secondary,
                  ),
                  Text(
                    [e.zone, e.floor, e.details].nonNulls.where((s) => s.isNotEmpty).join(' · '),
                    style: AppText.caption,
                  ),
                ],
              ),
            ),
            const SizedBox(width: 6),
            StatusChip(e.severity, tone: tone),
          ],
        ),
      ),
    );
  }
}

/// "Recent Location Events" card on the Real-Time Location screen (10 per page, "View all").
class RtlEventsSection extends ConsumerStatefulWidget {
  const RtlEventsSection({super.key, this.siteId, this.onShowOnMap});

  final String? siteId;
  final ValueChanged<String>? onShowOnMap;

  @override
  ConsumerState<RtlEventsSection> createState() => _RtlEventsSectionState();
}

class _RtlEventsSectionState extends ConsumerState<RtlEventsSection> {
  String _range = '24h';
  String? _type;

  @override
  Widget build(BuildContext context) {
    final q = (range: _range, eventType: _type, siteId: widget.siteId, assetId: null, page: 1, pageSize: 10);
    final async = ref.watch(rtlEventsProvider(q));
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SectionHeader(
            title: 'Recent Location Events',
            actionLabel: 'View all',
            onAction: () => context.push(Routes.trackingEvents(range: _range, type: _type, site: widget.siteId)),
          ),
          const SizedBox(height: 6),
          RtlEventFilters(
            range: _range,
            eventType: _type,
            eventTypes: async.value?.eventTypes ?? const [],
            onRange: (r) => setState(() => _range = r),
            onType: (t) => setState(() => _type = t),
          ),
          async.when(
            skipLoadingOnReload: true,
            loading: () => const ListSkeleton(rows: 3),
            error: (e, _) => LoadErrorState(error: e, onRetry: () => ref.invalidate(rtlEventsProvider(q))),
            data: (p) => p.items.isEmpty
                ? EmptyState(
                    icon: Icons.event_note_rounded,
                    title: 'No location events',
                    message: 'Nothing happened in the ${p.rangeLabel.isEmpty ? 'selected range' : p.rangeLabel.toLowerCase()}.',
                  )
                : Column(
                    children: [
                      for (final (i, e) in p.items.indexed) ...[
                        if (i > 0) Divider(height: 1, color: AppColors.divider),
                        RtlEventRow(event: e, onShowOnMap: widget.onShowOnMap),
                      ],
                      if (p.total > p.items.length)
                        Padding(
                          padding: const EdgeInsets.only(top: 6),
                          child: Text('Showing ${p.items.length} of ${p.total}', style: AppText.caption),
                        ),
                    ],
                  ),
          ),
        ],
      ),
    );
  }
}

/// `/more/tracking/location-events`: every location event, paged (25 per page, Load more).
class RtlEventsScreen extends ConsumerStatefulWidget {
  const RtlEventsScreen({super.key, this.range, this.eventType, this.siteId});

  final String? range, eventType, siteId;

  @override
  ConsumerState<RtlEventsScreen> createState() => _RtlEventsScreenState();
}

class _RtlEventsScreenState extends ConsumerState<RtlEventsScreen> {
  static const pageSize = 25;
  late String _range = RtlApi.eventRanges.contains(widget.range) ? widget.range! : '24h';
  late String? _type = widget.eventType;
  int _pages = 1;

  RtlEventsQuery _q(int page) =>
      (range: _range, eventType: _type, siteId: widget.siteId, assetId: null, page: page, pageSize: pageSize);

  @override
  Widget build(BuildContext context) {
    final pages = [for (var p = 1; p <= _pages; p++) ref.watch(rtlEventsProvider(_q(p)))];
    final first = pages.first;
    final last = pages.last;
    final items = [for (final p in pages) ...?p.value?.items];
    return LivePageFrame(
      onRefresh: () async {
        ref.invalidate(rtlEventsProvider);
        setState(() => _pages = 1);
      },
      children: [
        const PageTitle(title: 'Location Events', subtitle: 'Zone entries, exits, floor changes and geofence alerts'),
        const SizedBox(height: 12),
        RtlEventFilters(
          range: _range,
          eventType: _type,
          eventTypes: first.value?.eventTypes ?? const [],
          onRange: (r) => setState(() {
            _range = r;
            _pages = 1;
          }),
          onType: (t) => setState(() {
            _type = t;
            _pages = 1;
          }),
        ),
        const SizedBox(height: 8),
        if (first.hasError && items.isEmpty)
          LoadErrorState(error: first.error!, onRetry: () => ref.invalidate(rtlEventsProvider(_q(1))))
        else if (first.isLoading && items.isEmpty)
          const ListSkeleton(rows: 5)
        else if (items.isEmpty)
          const EmptyState(icon: Icons.event_note_rounded, title: 'No location events')
        else
          AppCard(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
            child: Column(
              children: [
                for (final (i, e) in items.indexed) ...[
                  if (i > 0) Divider(height: 1, color: AppColors.divider),
                  RtlEventRow(event: e),
                ],
              ],
            ),
          ),
        const SizedBox(height: 10),
        if (items.isNotEmpty) Text('Showing ${items.length} of ${first.value?.total ?? items.length}', style: AppText.caption),
        if (last.value?.hasMore ?? false)
          TextButton(
            key: const ValueKey('events-more'),
            onPressed: () => setState(() => _pages++),
            child: const Text('Load more'),
          )
        else if (last.isLoading && _pages > 1)
          const Padding(padding: EdgeInsets.all(12), child: Center(child: CircularProgressIndicator())),
      ],
    );
  }
}

/// Scrollable page with pull-to-refresh (the bar stays visible: AppScaffold).
class LivePageFrame extends StatelessWidget {
  const LivePageFrame({super.key, required this.children, required this.onRefresh});

  final List<Widget> children;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) => AppScaffold(
    body: RefreshIndicator(
      onRefresh: onRefresh,
      child: ListView(padding: context.pagePadding, children: children),
    ),
  );
}
