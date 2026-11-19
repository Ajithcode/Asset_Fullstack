import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart';
import 'package:asset360_mobile/data/tracking/locate_commands.dart';
import 'package:asset360_mobile/data/tracking/locate_session.dart';
import 'package:asset360_mobile/features/more/tracking/live/rtl/rtl_ui.dart';

// ------------------------------------------------------------------ Locate Asset (picker)

/// "Locate Asset" sheet: search + the overview's assets; target icon = [onTarget] (switch floor,
/// zoom, select, open card), indicator icon = the LED / buzzer panel.
Future<void> showLocateAssetSheet(
  BuildContext context, {
  required List<RtlAsset> assets,
  required ValueChanged<RtlAsset> onTarget,
}) {
  return showModalBottomSheet<void>(
    context: context,
    useRootNavigator: false,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (sheet) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: .7,
      maxChildSize: .95,
      builder: (_, scroll) => _LocateAssetList(
        assets: assets,
        scroll: scroll,
        onTarget: (a) {
          Navigator.pop(sheet);
          onTarget(a);
        },
      ),
    ),
  );
}

class _LocateAssetList extends StatefulWidget {
  const _LocateAssetList({required this.assets, required this.scroll, required this.onTarget});

  final List<RtlAsset> assets;
  final ScrollController scroll;
  final ValueChanged<RtlAsset> onTarget;

  @override
  State<_LocateAssetList> createState() => _LocateAssetListState();
}

class _LocateAssetListState extends State<_LocateAssetList> {
  String _q = '';

  @override
  Widget build(BuildContext context) {
    final now = rtlNow();
    final list = [for (final a in widget.assets) if (a.matches(_q)) a];
    return ListView(
      controller: widget.scroll,
      padding: EdgeInsets.fromLTRB(20, 0, 20, 24 + MediaQuery.paddingOf(context).bottom),
      children: [
        const SheetTitle('Locate Asset', subtitle: 'Find an asset on the map or make its tag blink / beep'),
        TextField(
          key: const ValueKey('locate-search'),
          decoration: const InputDecoration(
            hintText: 'Asset ID, name or device',
            prefixIcon: Icon(Icons.search_rounded),
          ),
          onChanged: (v) => setState(() => _q = v),
        ),
        const SizedBox(height: 10),
        if (list.isEmpty)
          EmptyState(
            icon: Icons.search_off_rounded,
            title: widget.assets.isEmpty ? 'No assets in view yet' : 'No match',
            message: widget.assets.isEmpty
                ? 'Tracked tags appear here when gateways report them.'
                : 'No asset matches "$_q".',
          )
        else
          for (final a in list)
            ListTile(
              key: ValueKey('locate-row-${a.assetId}'),
              contentPadding: EdgeInsets.zero,
              leading: RtlStatusDot(status: rtlStatusOf(a)),
              minLeadingWidth: 12,
              title: Text('${a.tagLabel} ${a.name}', maxLines: 1, overflow: TextOverflow.ellipsis),
              subtitle: Text(
                [a.location?.zoneName ?? 'Unknown zone', rtlAgo(a.lastSeen, now)].join(' · '),
                style: AppText.caption,
              ),
              trailing: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  IconButton(
                    key: ValueKey('locate-target-${a.assetId}'),
                    tooltip: 'Show on map',
                    icon: const Icon(Icons.gps_fixed_rounded),
                    onPressed: () => widget.onTarget(a),
                  ),
                  Tooltip(
                    message: a.hasTags ? 'Blink / beep the tag' : 'No tag linked',
                    child: IconButton(
                      key: ValueKey('locate-led-${a.assetId}'),
                      icon: const Icon(Icons.wb_twilight_rounded),
                      onPressed: a.hasTags
                          ? () async {
                              final closeAll = await showLocatePanel(context, a, fromList: true);
                              if (closeAll == true && context.mounted) Navigator.pop(context);
                            }
                          : null,
                    ),
                  ),
                ],
              ),
            ),
      ],
    );
  }
}

// ------------------------------------------------------------------ Locate panel (LED / buzzer)

/// The LED / buzzer panel for [asset] (must have a tag). Opens /ws/locate while it is shown.
/// Layout: docs/design/locate_asset_led_buzzer_panel_mockup.html. [fromList] = opened from the
/// Locate Asset list: the header's back arrow returns to it. Completes with true when × closed it
/// (the list closes too).
Future<bool?> showLocatePanel(BuildContext context, RtlAsset asset, {bool fromList = false}) {
  if (!asset.hasTags) {
    context.showSnack('No tag linked');
    return Future.value();
  }
  return showModalBottomSheet<bool>(
    context: context,
    useRootNavigator: false,
    isScrollControlled: true,
    builder: (sheet) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: .9,
      maxChildSize: .95,
      builder: (_, scroll) => LocatePanel(asset: asset, scroll: scroll, fromList: fromList),
    ),
  );
}

class LocatePanel extends ConsumerStatefulWidget {
  const LocatePanel({super.key, required this.asset, this.scroll, this.fromList = false});

  final RtlAsset asset;
  final ScrollController? scroll;
  final bool fromList;

  @override
  ConsumerState<LocatePanel> createState() => _LocatePanelState();
}

class _LocatePanelState extends ConsumerState<LocatePanel> {
  late final LocateSession _session = ref.read(locateSessionFactoryProvider)();
  late String _tag = widget.asset.tags.first;
  LocateMode _mode = LocateMode.led;
  double _brightness = 100;

  /// LED run time of the last LED command (for the Turn off note).
  Duration? _lastLedRun;
  bool _showLedNote = false;

  final _on = TextEditingController(text: '300');
  final _off = TextEditingController(text: '300');
  final _cycles = TextEditingController(text: '10');
  final _beep = TextEditingController(text: '300');
  final _pause = TextEditingController(text: '300');
  final _beeps = TextEditingController(text: '5');
  final _tone = TextEditingController(text: '0');

  @override
  void initState() {
    super.initState();
    _session.addListener(_changed);
    _session.open(_tag);
  }

  void _changed() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _session
      ..removeListener(_changed)
      ..dispose();
    for (final c in [_on, _off, _cycles, _beep, _pause, _beeps, _tone]) {
      c.dispose();
    }
    super.dispose();
  }

  int _v(TextEditingController c) => int.tryParse(c.text.trim()) ?? 0;

  LedOptions get _led =>
      LedOptions(onMs: _v(_on), offMs: _v(_off), cycles: _v(_cycles), brightness: _brightness.round());
  BuzzerOptions get _buzzer =>
      BuzzerOptions(beepMs: _v(_beep), pauseMs: _v(_pause), cycles: _v(_beeps), level: _v(_tone));

  bool get _valid {
    final led = [
      LocateLimits.ledOn.check(_on.text),
      LocateLimits.ledOff.check(_off.text),
      LocateLimits.ledCycles.check(_cycles.text),
    ];
    final buzzer = [
      LocateLimits.beep.check(_beep.text),
      LocateLimits.pause.check(_pause.text),
      LocateLimits.beepCycles.check(_beeps.text),
      LocateLimits.tone.check(_tone.text),
    ];
    return (!_mode.usesLed || led.every((e) => e == null)) && (!_mode.usesBuzzer || buzzer.every((e) => e == null));
  }

  void _send() {
    if (_mode.usesLed) _lastLedRun = _led.runTime;
    setState(() => _showLedNote = false);
    _session.sendCommand(locateCommand(_mode, _tag, led: _led, buzzer: _buzzer));
  }

  void _turnOff() {
    setState(() => _showLedNote = true);
    _session.sendCommand(turnOffCommand(_tag), turnOff: true);
  }

  void _pattern(LedPattern p) => setState(() {
    _on.text = '${p.onMs}';
    _off.text = '${p.offMs}';
    _cycles.text = '${p.cycles}';
  });

  Widget _field(String key, String label, TextEditingController c, LocateRange range, {String? note}) =>
      _NumField(keyName: key, label: label, c: c, range: range, note: note, onChanged: _changed);

  @override
  Widget build(BuildContext context) {
    final a = widget.asset;
    final s = _session;
    final gw = s.gateway;
    final subscribed = s.connection == LocateConnection.subscribed;
    final tagLine = [
      'Tag ${normalizeTagId(_tag)}',
      if (gw?.gatewayId != null) 'Nearest gateway ${gw!.gatewayId}',
      if (gw?.rssi != null) '${gw!.rssi!.round()} dBm',
      if (s.connection == LocateConnection.connecting) 'Connecting…',
    ].join(' · ');
    return ListView(
      controller: widget.scroll,
      padding: EdgeInsets.fromLTRB(20, 12, 20, 24 + MediaQuery.paddingOf(context).bottom),
      children: [
        // Header: ← (back to the asset list) · Locate asset · ×
        Row(
          children: [
            if (widget.fromList) ...[
              IconButton(
                key: const ValueKey('locate-back'),
                tooltip: 'Back to asset list',
                visualDensity: VisualDensity.compact,
                style: IconButton.styleFrom(
                  side: BorderSide(color: AppColors.border),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                ),
                onPressed: () => Navigator.pop(context),
                icon: const Icon(Icons.arrow_back_rounded, size: 18),
              ),
              const SizedBox(width: 8),
            ],
            Expanded(
              child: Text('Locate asset', style: AppText.cardTitle.copyWith(fontSize: 16, fontWeight: FontWeight.w600)),
            ),
            IconButton(
              key: const ValueKey('locate-close'),
              tooltip: 'Close',
              visualDensity: VisualDensity.compact,
              onPressed: () => Navigator.pop(context, true),
              icon: Icon(Icons.close_rounded, size: 20, color: AppColors.textSecondary),
            ),
          ],
        ),
        const SizedBox(height: 10),
        // Asset + tag box.
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(color: AppColors.tileGrey, borderRadius: BorderRadius.circular(8)),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text.rich(
                TextSpan(
                  children: [
                    TextSpan(text: a.tagLabel, style: const TextStyle(fontWeight: FontWeight.w600)),
                    if (a.name.isNotEmpty) TextSpan(text: ' ${a.name}'),
                  ],
                ),
                style: AppText.body,
              ),
              const SizedBox(height: 2),
              Text(
                tagLine,
                key: const ValueKey('locate-tag-line'),
                style: AppText.caption.copyWith(fontSize: 12, color: AppColors.textSecondary),
              ),
            ],
          ),
        ),
        if (a.tags.length > 1) ...[
          const SizedBox(height: 10),
          _Labeled(
            label: 'Tag',
            child: DropdownButtonFormField<String>(
              key: const ValueKey('locate-tag-picker'),
              initialValue: _tag,
              isDense: true,
              decoration: _boxDecoration(),
              items: [for (final t in a.tags) DropdownMenuItem(value: t, child: Text(t))],
              onChanged: (t) {
                if (t == null || t == _tag) return;
                setState(() => _tag = t);
                _session.open(t);
              },
            ),
          ),
        ],
        if (s.notice != null) ...[
          const SizedBox(height: 10),
          _Banner(text: s.notice!, warning: s.offline),
        ],
        const SizedBox(height: 12),
        SegmentedButton<LocateMode>(
          key: const ValueKey('locate-mode'),
          showSelectedIcon: false,
          style: SegmentedButton.styleFrom(
            visualDensity: VisualDensity.compact,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            textStyle: const TextStyle(fontSize: 13),
          ),
          segments: [
            for (final m in LocateMode.values)
              ButtonSegment(
                value: m,
                label: Text(m.label),
                icon: switch (m) {
                  LocateMode.led => const Icon(Icons.lightbulb_outline_rounded, size: 16),
                  LocateMode.buzzer => const Icon(Icons.volume_up_outlined, size: 16),
                  LocateMode.both => null,
                },
              ),
          ],
          selected: {_mode},
          onSelectionChanged: (v) => setState(() => _mode = v.first),
        ),
        const _SecTitle('Quick patterns'),
        Wrap(
          spacing: 6,
          runSpacing: 6,
          children: [
            for (final p in ledPatterns)
              ActionChip(
                key: ValueKey('pattern-${p.label}'),
                label: Text(p.label, style: const TextStyle(fontSize: 12)),
                visualDensity: VisualDensity.compact,
                padding: const EdgeInsets.symmetric(horizontal: 4),
                onPressed: _mode.usesLed ? () => _pattern(p) : null,
              ),
          ],
        ),
        _Faded(
          enabled: _mode.usesLed,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const _SecTitle('LED settings', icon: Icons.lightbulb_outline_rounded),
              _Grid(
                children: [
                  _field('led-on', 'On time (ms)', _on, LocateLimits.ledOn),
                  _field('led-off', 'Off time (ms)', _off, LocateLimits.ledOff),
                  _field('led-cycles', 'Blink count', _cycles, LocateLimits.ledCycles),
                  _Labeled(
                    label: 'Colour',
                    note: 'This tag has a red LED only',
                    child: DropdownButtonFormField<String>(
                      key: const ValueKey('led-colour'),
                      initialValue: 'Red',
                      isDense: true,
                      decoration: _boxDecoration(),
                      items: const [DropdownMenuItem(value: 'Red', child: Text('Red'))],
                      onChanged: (_) {},
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Text(
                'Brightness ${_brightness.round()}%',
                style: AppText.caption.copyWith(fontSize: 12, color: AppColors.textSecondary),
              ),
              Slider(
                key: const ValueKey('led-brightness'),
                value: _brightness,
                min: 1,
                max: 100,
                divisions: 99,
                label: '${_brightness.round()}%',
                padding: const EdgeInsets.symmetric(vertical: 8),
                onChanged: _mode.usesLed ? (v) => setState(() => _brightness = v) : null,
              ),
            ],
          ),
        ),
        _Faded(
          enabled: _mode.usesBuzzer,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const _SecTitle('Buzzer settings', icon: Icons.volume_up_outlined),
              _Grid(
                children: [
                  _field('bz-beep', 'Beep time (ms)', _beep, LocateLimits.beep),
                  _field('bz-pause', 'Pause (ms)', _pause, LocateLimits.pause),
                  _field('bz-count', 'Beep count', _beeps, LocateLimits.beepCycles, note: '0 = off'),
                  _field('bz-tone', 'Tone level', _tone, LocateLimits.tone, note: '0 = tag default'),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        if (_valid)
          Text(
            runTimeLine(_mode, _led, _buzzer),
            key: const ValueKey('locate-runtime'),
            style: AppText.body.copyWith(fontSize: 13, color: AppColors.textSecondary),
          ),
        const SizedBox(height: 12),
        Row(
          mainAxisAlignment: MainAxisAlignment.end,
          children: [
            Flexible(
              child: OutlinedButton.icon(
                key: const ValueKey('locate-off'),
                style: OutlinedButton.styleFrom(padding: _buttonPadding, minimumSize: const Size(0, 44)),
                onPressed: s.canSend ? _turnOff : null,
                icon: const Icon(Icons.power_settings_new_rounded, size: 18),
                label: const Text('Turn off'),
              ),
            ),
            const SizedBox(width: 8),
            Flexible(
              child: FilledButton.icon(
                key: const ValueKey('locate-send'),
                style: FilledButton.styleFrom(padding: _buttonPadding, minimumSize: const Size(0, 44)),
                onPressed: _valid && s.canSend ? _send : null,
                icon: const Icon(Icons.send_rounded, size: 18),
                label: const Text('Send to tag'),
              ),
            ),
          ],
        ),
        if (s.progress.stage != LocateStage.idle) ...[
          const SizedBox(height: 10),
          _ProgressLine(progress: s.progress),
        ],
        if (_showLedNote) ...[
          const SizedBox(height: 6),
          Text(
            'The LED stops by itself after ${runTimeText(_lastLedRun ?? _led.runTime)}',
            key: const ValueKey('locate-led-note'),
            style: AppText.caption.copyWith(fontSize: 12, color: AppColors.textSecondary),
          ),
        ],
        if (!subscribed && s.connection == LocateConnection.failed) ...[
          const SizedBox(height: 8),
          Align(
            alignment: Alignment.centerRight,
            child: TextButton.icon(
              onPressed: () => _session.open(_tag),
              icon: const Icon(Icons.refresh_rounded),
              label: const Text('Reconnect'),
            ),
          ),
        ],
        const SizedBox(height: 6),
        Align(
          alignment: Alignment.centerLeft,
          child: TextButton.icon(
            key: const ValueKey('locate-history'),
            onPressed: () => _showHistory(context, _tag),
            icon: const Icon(Icons.history_rounded, size: 18),
            label: const Text('History'),
          ),
        ),
      ],
    );
  }
}

const _buttonPadding = EdgeInsets.symmetric(horizontal: 14);

/// Compact outlined input box of the panel's fields.
InputDecoration _boxDecoration({String? hint, bool error = false}) => InputDecoration(
  isDense: true,
  hintText: hint,
  contentPadding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
  border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
  enabledBorder: OutlineInputBorder(
    borderRadius: BorderRadius.circular(8),
    borderSide: BorderSide(color: error ? AppColors.red : AppColors.border),
  ),
);

/// Section title ("LED settings" with its icon), 13 px medium.
class _SecTitle extends StatelessWidget {
  const _SecTitle(this.text, {this.icon});

  final String text;
  final IconData? icon;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(top: 16, bottom: 8),
    child: Row(
      children: [
        if (icon != null) ...[Icon(icon, size: 16, color: AppColors.text), const SizedBox(width: 6)],
        Text(text, style: AppText.bodyStrong.copyWith(fontSize: 13)),
      ],
    ),
  );
}

/// The settings of the mode not in use: faded and not editable (and not validated).
class _Faded extends StatelessWidget {
  const _Faded({required this.enabled, required this.child});

  final bool enabled;
  final Widget child;

  @override
  Widget build(BuildContext context) => AnimatedOpacity(
    duration: const Duration(milliseconds: 150),
    opacity: enabled ? 1 : .45,
    child: IgnorePointer(ignoring: !enabled, child: child),
  );
}

/// Two columns, 12 px apart, rows 10 px apart.
class _Grid extends StatelessWidget {
  const _Grid({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) => Column(
    children: [
      for (var i = 0; i < children.length; i += 2) ...[
        if (i > 0) const SizedBox(height: 10),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: children[i]),
            const SizedBox(width: 12),
            Expanded(child: i + 1 < children.length ? children[i + 1] : const SizedBox.shrink()),
          ],
        ),
      ],
    ],
  );
}

/// Label above, the control, a small note / error under it.
class _Labeled extends StatelessWidget {
  const _Labeled({required this.label, required this.child, this.note, this.error = false});

  final String label;
  final Widget child;
  final String? note;
  final bool error;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Text(label, style: AppText.caption.copyWith(fontSize: 12, color: AppColors.textSecondary)),
      const SizedBox(height: 4),
      child,
      if (note != null) ...[
        const SizedBox(height: 3),
        Text(note!, style: AppText.caption.copyWith(fontSize: 11, color: error ? AppColors.red : AppColors.textMuted)),
      ],
    ],
  );
}

class _NumField extends StatelessWidget {
  const _NumField({
    required this.keyName,
    required this.label,
    required this.c,
    required this.range,
    required this.onChanged,
    this.note,
  });

  final String keyName, label;
  final TextEditingController c;
  final LocateRange range;
  final VoidCallback onChanged;

  /// Extra hint after "Min X, max Y" (e.g. "0 = off").
  final String? note;

  @override
  Widget build(BuildContext context) {
    final error = range.check(c.text);
    return _Labeled(
      label: label,
      error: error != null,
      note: error ?? [range.hint, ?note].join(' · '),
      child: TextField(
        key: ValueKey(keyName),
        controller: c,
        keyboardType: TextInputType.number,
        inputFormatters: [FilteringTextInputFormatter.digitsOnly],
        style: AppText.body,
        decoration: _boxDecoration(hint: '${range.min} – ${range.max}', error: error != null),
        onChanged: (_) => onChanged(),
      ),
    );
  }
}

class _Banner extends StatelessWidget {
  const _Banner({required this.text, required this.warning});

  final String text;
  final bool warning;

  @override
  Widget build(BuildContext context) {
    final c = warning ? AppColors.orange : AppColors.red;
    return Container(
      key: const ValueKey('locate-notice'),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(color: c.withValues(alpha: .1), borderRadius: BorderRadius.circular(10)),
      child: Row(
        children: [
          Icon(warning ? Icons.warning_amber_rounded : Icons.error_outline_rounded, color: c, size: 18),
          const SizedBox(width: 8),
          Expanded(child: Text(text, style: AppText.caption.copyWith(color: c))),
        ],
      ),
    );
  }
}

class _ProgressLine extends StatelessWidget {
  const _ProgressLine({required this.progress});

  final LocateProgress progress;

  @override
  Widget build(BuildContext context) {
    final p = progress;
    final color = p.success ? AppColors.green : (p.problem ? AppColors.red : AppColors.primary);
    return Row(
      key: const ValueKey('locate-progress'),
      children: [
        if (p.finished)
          Icon(p.success ? Icons.check_circle_rounded : Icons.error_rounded, color: color, size: 18)
        else
          SizedBox.square(dimension: 16, child: CircularProgressIndicator(strokeWidth: 2, color: color)),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            p.message,
            style: AppText.body.copyWith(fontSize: 13, color: color, fontWeight: FontWeight.w500),
          ),
        ),
      ],
    );
  }
}

/// GET /api/locating/tags/{tagid}/commands?limit=20: command, status, sentAt (the raw request is
/// never read).
Future<void> _showHistory(BuildContext context, String tagId) {
  return showModalBottomSheet<void>(
    context: context,
    useRootNavigator: false,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (_) => Consumer(
      builder: (context, ref, _) {
        final async = ref.watch(locateHistoryProvider(normalizeTagId(tagId)));
        final fmt = DateFormat('d MMM, HH:mm:ss');
        return sheetFrame(context, [
          SheetTitle('Locate history', subtitle: 'Tag ${normalizeTagId(tagId)} · last 20 commands'),
          async.when(
            loading: () => const ListSkeleton(rows: 3),
            error: (e, _) =>
                LoadErrorState(error: e, onRetry: () => ref.invalidate(locateHistoryProvider(normalizeTagId(tagId)))),
            data: (rows) => rows.isEmpty
                ? const EmptyState(icon: Icons.history_rounded, title: 'No commands sent to this tag yet')
                : Column(
                    children: [
                      for (final r in rows)
                        ListTile(
                          contentPadding: EdgeInsets.zero,
                          leading: Icon(switch (r.command) {
                            'buzzer' => Icons.volume_up_rounded,
                            'both' => Icons.notifications_active_rounded,
                            _ => Icons.wb_twilight_rounded,
                          }),
                          title: Text(switch (r.command) {
                            'buzzer' => 'Buzzer',
                            'both' => 'LED + buzzer',
                            _ => 'LED',
                          }),
                          subtitle: Text(
                            [if (r.sentAt != null) fmt.format(r.sentAt!.toLocal()), if (r.gatewayId != null) r.gatewayId!]
                                .join(' · '),
                          ),
                          trailing: StatusChip(
                            r.status.isEmpty ? 'sent' : r.status,
                            tone: switch (r.status) {
                              'completed' => ChipTone.green,
                              'failed' || 'timeout' => ChipTone.red,
                              'accepted' => ChipTone.blue,
                              _ => ChipTone.grey,
                            },
                          ),
                        ),
                    ],
                  ),
          ),
        ]);
      },
    ),
  );
}
