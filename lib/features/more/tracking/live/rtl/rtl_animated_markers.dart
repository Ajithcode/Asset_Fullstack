import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';

/// One marker of [RtlAnimatedMarkerLayer]: [id] keeps it the same marker between rebuilds.
class RtlMarkerSpec {
  const RtlMarkerSpec({
    required this.id,
    required this.point,
    required this.child,
    this.width = 30,
    this.height = 30,
    this.zoneKey,
    this.moving = false,
  });

  final String id;
  final LatLng point;
  final Widget child;
  final double width, height;

  /// The zone the marker sits in (null = at a gateway plot): a change of it while [moving] leaves a trail.
  final String? zoneKey;

  /// The asset is Moving (changed place in the last minute).
  final bool moving;
}

/// How long a marker glides to its new point: the web's CSS transition on `.fp-asset-marker` (1.2 s,
/// cubic-bezier(.4, 0, .2, 1) = [Curves.fastOutSlowIn]).
const rtlGlide = Duration(milliseconds: 1200);

/// How long the trail of a Moving asset takes to fade out (the web's `fp-trail-fade`, 8 s).
const rtlTrailFade = Duration(seconds: 8);

/// A [MarkerLayer] whose markers glide to a new point instead of jumping (Leaflet `setLatLng` + the web's CSS
/// transition), with the web's fading trail: when a Moving asset changes zone, a dashed green line from the old
/// spot to the new one (3 px, 80 % opacity) fades out over 8 s. Markers are matched by [RtlMarkerSpec.id], so a
/// marker keeps its widget (no flicker); a new one appears at its point; a gone one disappears. A move that starts
/// while another one runs continues from where the marker is drawn now. No animation and no trail when the
/// platform asks to reduce motion.
class RtlAnimatedMarkerLayer extends StatefulWidget {
  const RtlAnimatedMarkerLayer({super.key, required this.markers, this.duration = rtlGlide, this.trailFade = rtlTrailFade});

  final List<RtlMarkerSpec> markers;
  final Duration duration, trailFade;

  @override
  State<RtlAnimatedMarkerLayer> createState() => _RtlAnimatedMarkerLayerState();
}

class _Trail {
  _Trail(this.from, this.to, this.c);

  final LatLng from, to;
  final AnimationController c;
}

class _RtlAnimatedMarkerLayerState extends State<RtlAnimatedMarkerLayer> with TickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: widget.duration);
  final _from = <String, LatLng>{};
  final _to = <String, LatLng>{};
  final _zone = <String, String?>{};
  final _trails = <_Trail>[];

  /// Trails on screen (tests read it).
  @visibleForTesting
  int get trailCount => _trails.length;

  @override
  void initState() {
    super.initState();
    for (final m in widget.markers) {
      _from[m.id] = _to[m.id] = m.point;
      _zone[m.id] = m.zoneKey;
    }
  }

  /// Where marker [id] is drawn right now.
  LatLng _shown(String id) {
    final to = _to[id]!;
    final from = _from[id] ?? to;
    final t = Curves.fastOutSlowIn.transform(_c.value);
    return LatLng(from.latitude + (to.latitude - from.latitude) * t, from.longitude + (to.longitude - from.longitude) * t);
  }

  void _addTrail(LatLng from, LatLng to) {
    final c = AnimationController(vsync: this, duration: widget.trailFade);
    final t = _Trail(from, to, c);
    _trails.add(t);
    c.addListener(() {
      if (mounted) setState(() {});
    });
    c.addStatusListener((s) {
      if (s != AnimationStatus.completed) return;
      _trails.remove(t);
      c.dispose();
      if (mounted) setState(() {});
    });
    c.forward();
  }

  @override
  void didUpdateWidget(RtlAnimatedMarkerLayer old) {
    super.didUpdateWidget(old);
    final reduce = MediaQuery.maybeDisableAnimationsOf(context) ?? false;
    final shown = {for (final id in _to.keys) id: _shown(id)};
    final now = {for (final m in widget.markers) m.id: m};
    for (final id in _to.keys.toList()) {
      if (!now.containsKey(id)) {
        _to.remove(id);
        _from.remove(id);
        _zone.remove(id);
      }
    }
    var moves = false;
    for (final m in now.values) {
      final cur = shown[m.id];
      if (cur == null) {
        // A new marker appears at its point.
        _from[m.id] = _to[m.id] = m.point;
        _zone[m.id] = m.zoneKey;
        continue;
      }
      final to = _to[m.id]!;
      final moved = (to.latitude - m.point.latitude).abs() > 1e-9 || (to.longitude - m.point.longitude).abs() > 1e-9;
      if (moved) {
        moves = true;
        // The web: a Moving asset that changed zone leaves a trail from the old spot to the new one.
        final before = _zone[m.id];
        if (m.moving && m.zoneKey != null && before != null && before != m.zoneKey && !reduce) _addTrail(cur, m.point);
      }
      _zone[m.id] = m.zoneKey;
    }
    if (!moves) return;
    for (final m in now.values) {
      final cur = shown[m.id];
      if (cur == null) continue;
      _from[m.id] = reduce ? m.point : cur;
      _to[m.id] = m.point;
    }
    if (reduce) {
      _c.value = 1;
    } else {
      _c.forward(from: 0);
    }
  }

  @override
  void dispose() {
    _c.dispose();
    for (final t in _trails) {
      t.c.dispose();
    }
    _trails.clear();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Stack(
    fit: StackFit.expand,
    children: [
      if (_trails.isNotEmpty)
        PolylineLayer(
          polylines: [
            for (final t in _trails)
              Polyline(
                points: [t.from, t.to],
                strokeWidth: 3,
                color: AppColors.green.withValues(alpha: .8 * (1 - Curves.easeOut.transform(t.c.value))),
                pattern: StrokePattern.dashed(segments: const [4, 7]),
              ),
          ],
        ),
      AnimatedBuilder(
        animation: _c,
        builder: (context, _) => MarkerLayer(
          markers: [
            for (final m in widget.markers)
              Marker(key: ValueKey('rtl-mk-${m.id}'), point: _shown(m.id), width: m.width, height: m.height, child: m.child),
          ],
        ),
      ),
    ],
  );
}
