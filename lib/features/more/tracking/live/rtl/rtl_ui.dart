import 'package:flutter/material.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';

/// Shared colours, dots and legend of the Real-Time Location screen.

/// Device clock (server data is real time).
DateTime rtlNow() => DateTime.now().toUtc();

Color rtlStatusColor(RtlStatus s) => switch (s) {
  RtlStatus.liveMoving => AppColors.green,
  RtlStatus.liveStationary => AppColors.blue,
  RtlStatus.stale => AppColors.orange,
  RtlStatus.lowConfidence => AppColors.grey,
  RtlStatus.offline => AppColors.red,
};

Color gatewayColor(RtlGatewayState s) => switch (s) {
  RtlGatewayState.online => AppColors.green,
  RtlGatewayState.idle => AppColors.amber,
  RtlGatewayState.offline => AppColors.grey,
};

/// Restricted zones are drawn in this colour.
const rtlRestrictedColor = AppColors.red;

/// A geofence breach (open location alert) badge colour.
const rtlAlertColor = AppColors.red;

class RtlStatusDot extends StatelessWidget {
  const RtlStatusDot({super.key, required this.status, this.size = 10});

  final RtlStatus status;
  final double size;

  @override
  Widget build(BuildContext context) => Container(
    width: size,
    height: size,
    decoration: BoxDecoration(color: rtlStatusColor(status), shape: BoxShape.circle),
  );
}

/// Legend row: Live (moving), Live (stationary), Stale, Low confidence, Offline, Geofence breach, Gateway.
class RtlLegend extends StatelessWidget {
  const RtlLegend({super.key});

  @override
  Widget build(BuildContext context) {
    Widget item(Widget mark, String label) => Row(
      mainAxisSize: MainAxisSize.min,
      children: [mark, const SizedBox(width: 4), Text(label, style: AppText.caption.copyWith(fontSize: 11))],
    );
    return Wrap(
      spacing: 12,
      runSpacing: 6,
      children: [
        for (final s in RtlStatus.values) item(RtlStatusDot(status: s), s.label),
        item(const Icon(Icons.warning_rounded, size: 13, color: rtlAlertColor), 'Geofence breach'),
        item(const Icon(Icons.router_rounded, size: 13, color: AppColors.iconBlue), 'Gateway'),
      ],
    );
  }
}

/// Icon of a location event type.
IconData rtlEventIcon(String type) {
  final t = type.toLowerCase();
  if (t.contains('geofence')) return Icons.fence_rounded;
  if (t.contains('entry')) return Icons.login_rounded;
  if (t.contains('exit')) return Icons.logout_rounded;
  if (t.contains('floor')) return Icons.stairs_rounded;
  if (t.contains('stale')) return Icons.hourglass_bottom_rounded;
  if (t.contains('lost')) return Icons.location_off_rounded;
  if (t.contains('detected')) return Icons.my_location_rounded;
  return Icons.place_rounded;
}

ChipTone rtlSeverityTone(String severity) => switch (severity.toLowerCase()) {
  'critical' || 'high' => ChipTone.red,
  'warning' || 'medium' => ChipTone.orange,
  _ => ChipTone.blue,
};

/// Match green, Mismatch red, Unknown / Not set grey.
ChipTone rtlComparisonTone(String status) => switch (status.toLowerCase()) {
  'match' => ChipTone.green,
  'mismatch' => ChipTone.red,
  _ => ChipTone.grey,
};

/// "High" / "Medium" / "Low" confidence colour.
ChipTone rtlConfidenceTone(String c) => switch (c.toLowerCase()) {
  'high' => ChipTone.green,
  'medium' => ChipTone.amber,
  'low' => ChipTone.red,
  _ => ChipTone.grey,
};

/// Round icon of an asset (status coloured).
class RtlAssetAvatar extends StatelessWidget {
  const RtlAssetAvatar({super.key, required this.asset, this.size = 40});

  final RtlAsset asset;
  final double size;

  @override
  Widget build(BuildContext context) {
    final c = rtlStatusColor(rtlStatusOf(asset));
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(color: c.withValues(alpha: .14), shape: BoxShape.circle),
      child: Icon(Icons.inventory_2_rounded, color: c, size: size * .5),
    );
  }
}
