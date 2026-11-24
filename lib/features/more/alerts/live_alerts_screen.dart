import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/api/api_exception.dart';
import 'package:asset360_mobile/data/repositories/alert_feed_repository.dart';
import 'package:asset360_mobile/data/tracking/rtl_logic.dart' show rtlAgo;

/// Severity → colour and icon of an alert card (the feed's `severity`: Critical / High / Medium / Low).
({Color color, IconData icon}) alertSeverityLook(String severity) => switch (severity.toLowerCase()) {
  'critical' => (color: AppColors.red, icon: Icons.error_rounded),
  'high' => (color: AppColors.orange, icon: Icons.warning_rounded),
  'medium' => (color: AppColors.amber, icon: Icons.warning_amber_rounded),
  'low' => (color: AppColors.blue, icon: Icons.info_rounded),
  _ => (color: AppColors.grey, icon: Icons.notifications_rounded),
};

/// "Alerts" (API mode; the bell opens it): the Open alerts of GET /api/alerts/feed, newest first — severity colour and
/// icon, asset, zone, time ago — with Acknowledge per alert and Acknowledge all (after a confirmation). Works for every
/// signed-in user (no licence lock). [alertId] = a notification was tapped: scroll to that alert and mark it.
class LiveAlertsScreen extends ConsumerStatefulWidget {
  const LiveAlertsScreen({super.key, this.alertId});

  final String? alertId;

  @override
  ConsumerState<LiveAlertsScreen> createState() => _LiveAlertsScreenState();
}

class _LiveAlertsScreenState extends ConsumerState<LiveAlertsScreen> {
  Timer? _tick;
  final _keys = <String, GlobalKey>{};
  String? _scrolledTo;

  @override
  void initState() {
    super.initState();
    // "5 min ago" moves on.
    _tick = Timer.periodic(const Duration(seconds: 30), (_) {
      if (mounted && TickerMode.of(context)) setState(() {});
    });
  }

  @override
  void dispose() {
    _tick?.cancel();
    super.dispose();
  }

  void _scrollToFocus(List<FeedAlert> items) {
    final id = widget.alertId;
    if (id == null || _scrolledTo == id || !items.any((a) => a.alertId == id)) return;
    _scrolledTo = id;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final ctx = _keys[id]?.currentContext;
      if (ctx != null && ctx.mounted) Scrollable.ensureVisible(ctx, duration: const Duration(milliseconds: 300), alignment: .1);
    });
  }

  Future<void> _acknowledge(FeedAlert a) async {
    try {
      await ref.read(alertFeedProvider.notifier).acknowledge(a.alertId);
      if (mounted) context.showSnack('${a.assetName.isEmpty ? a.alertId : a.assetName}: alert acknowledged');
    } catch (e) {
      if (mounted) context.showSnack('Could not acknowledge: ${ApiException.from(e).message}', actionLabel: 'Retry', onAction: () => _acknowledge(a));
    }
  }

  Future<void> _acknowledgeAll() async {
    final n = ref.read(alertFeedProvider).unacknowledged;
    final ok = await showConfirmDialog(
      context,
      title: 'Acknowledge all alerts?',
      message: n > 0
          ? 'This acknowledges every open alert ($n), including any that are not shown here.'
          : 'This acknowledges every open alert.',
      confirmLabel: 'Acknowledge all',
    );
    if (!ok || !mounted) return;
    try {
      final done = await ref.read(alertFeedProvider.notifier).acknowledgeAll();
      if (mounted) context.showSnack(done == 1 ? '1 alert acknowledged' : '$done alerts acknowledged');
    } catch (e) {
      if (mounted) context.showSnack('Could not acknowledge all: ${ApiException.from(e).message}', actionLabel: 'Retry', onAction: _acknowledgeAll);
    }
  }

  Future<void> _turnOnNotifications() async {
    final notifier = ref.read(alertFeedProvider.notifier);
    if (!await notifier.requestPermission()) {
      // Refused (maybe for good): the system settings are the way to allow them.
      await notifier.openNotificationSettings();
    }
  }

  @override
  Widget build(BuildContext context) {
    final feed = ref.watch(alertFeedProvider);
    final notifier = ref.read(alertFeedProvider.notifier);
    final now = DateTime.now().toUtc();
    _scrollToFocus(feed.items);
    final Widget body;
    if (!feed.loaded && feed.error != null) {
      body = LoadErrorState(error: feed.error!, title: 'Could not load alerts', onRetry: notifier.pollNow);
    } else if (!feed.loaded) {
      body = const ListSkeleton(rows: 3);
    } else if (feed.items.isEmpty) {
      body = const EmptyState(
        key: ValueKey('alerts-empty'),
        icon: Icons.notifications_off_outlined,
        title: 'No open alerts',
        message: 'New alerts appear here as soon as they are raised.',
      );
    } else {
      body = Column(
        children: [
          for (final a in feed.items)
            _AlertCard(
              key: _keys.putIfAbsent(a.alertId, GlobalKey.new),
              alert: a,
              now: now,
              focused: a.alertId == widget.alertId,
              busy: feed.busy,
              onAcknowledge: () => _acknowledge(a),
            ),
        ],
      );
    }
    return AppScaffold(
      body: RefreshIndicator(
        onRefresh: notifier.pollNow,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: context.pagePadding,
          children: [
            PageTitle(
              title: 'Alerts',
              subtitle: feed.unacknowledged == 0
                  ? 'Nothing waiting for you'
                  : '${feed.unacknowledged} unacknowledged',
              trailing: TextButton(
                key: const ValueKey('alerts-ack-all'),
                onPressed: feed.busy || (feed.items.isEmpty && feed.unacknowledged == 0) ? null : _acknowledgeAll,
                child: const Text('Acknowledge all'),
              ),
            ),
            const SizedBox(height: 10),
            if (feed.notificationsAllowed == false)
              _Banner(
                key: const ValueKey('alerts-notifications-off'),
                icon: Icons.notifications_off_outlined,
                text: 'Notifications are off, so new alerts will not pop up. The list and the bell still update.',
                action: 'Turn on',
                onAction: _turnOnNotifications,
              ),
            if (feed.loaded && feed.error != null)
              _Banner(
                key: const ValueKey('alerts-offline'),
                icon: Icons.cloud_off_rounded,
                color: AppColors.red,
                text: feed.updatedAt == null
                    ? ApiException.from(feed.error!).message
                    : 'Could not update (${ApiException.from(feed.error!).message}). Showing the list from ${rtlAgo(feed.updatedAt, now)}.',
                action: 'Retry',
                onAction: notifier.pollNow,
              )
            else if (feed.fromCache)
              const _Banner(
                key: ValueKey('alerts-cached'),
                icon: Icons.history_rounded,
                text: 'Showing the last saved list while the latest loads.',
              ),
            body,
          ],
        ),
      ),
    );
  }
}

class _Banner extends StatelessWidget {
  const _Banner({super.key, required this.icon, required this.text, this.action, this.onAction, this.color});

  final IconData icon;
  final String text;
  final String? action;
  final VoidCallback? onAction;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final c = color ?? AppColors.amber;
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.fromLTRB(12, 8, 6, 8),
      decoration: BoxDecoration(color: c.withValues(alpha: .12), borderRadius: BorderRadius.circular(10)),
      child: Row(
        children: [
          Icon(icon, size: 18, color: c),
          const SizedBox(width: 8),
          Expanded(child: Text(text, style: AppText.caption.copyWith(color: AppColors.text))),
          if (action != null) TextButton(onPressed: onAction, child: Text(action!)),
        ],
      ),
    );
  }
}

class _AlertCard extends StatelessWidget {
  const _AlertCard({
    super.key,
    required this.alert,
    required this.now,
    required this.focused,
    required this.busy,
    required this.onAcknowledge,
  });

  final FeedAlert alert;
  final DateTime now;
  final bool focused, busy;
  final VoidCallback onAcknowledge;

  @override
  Widget build(BuildContext context) {
    final a = alert;
    final look = alertSeverityLook(a.severity);
    final where = [a.zone, a.siteName].where((s) => s != null && s.isNotEmpty).join(' · ');
    return Container(
      key: ValueKey('alert-${a.alertId}'),
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.fromLTRB(12, 12, 12, 8),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: focused ? look.color : AppColors.border, width: focused ? 2 : 1),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(look.icon, color: look.color, size: 24, semanticLabel: a.severity),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(a.eventType.isEmpty ? 'Alert' : a.eventType, style: AppText.bodyStrong),
                    const SizedBox(height: 2),
                    Text(
                      [a.assetName, a.assetId].where((s) => s != null && s.isNotEmpty).join(' · '),
                      key: ValueKey('alert-asset-${a.alertId}'),
                      style: AppText.body,
                    ),
                    if (where.isNotEmpty) Text(where, style: AppText.caption),
                    if (a.message != null) ...[
                      const SizedBox(height: 4),
                      Text(a.message!, maxLines: 2, overflow: TextOverflow.ellipsis, style: AppText.caption),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                    decoration: BoxDecoration(color: look.color.withValues(alpha: .14), borderRadius: BorderRadius.circular(12)),
                    child: Text(
                      a.severity.isEmpty ? 'Alert' : a.severity,
                      style: AppText.caption.copyWith(color: look.color, fontWeight: FontWeight.w700),
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(rtlAgo(a.raisedAt, now), key: ValueKey('alert-time-${a.alertId}'), style: AppText.caption),
                ],
              ),
            ],
          ),
          Align(
            alignment: Alignment.centerRight,
            child: TextButton.icon(
              key: ValueKey('alert-ack-${a.alertId}'),
              onPressed: busy ? null : onAcknowledge,
              icon: const Icon(Icons.check_circle_outline_rounded, size: 18),
              label: const Text('Acknowledge'),
            ),
          ),
        ],
      ),
    );
  }
}
