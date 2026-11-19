import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/app_restart.dart';
import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/dialogs.dart';
import 'package:asset360_mobile/core/widgets/feedback.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/db/db_providers.dart';
import 'package:asset360_mobile/data/repositories/auth_repository.dart';
import 'package:asset360_mobile/data/repositories/server_repository.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';
import 'package:asset360_mobile/data/repositories/tracking_live_repository.dart';

/// Developer "Backend server" switcher (only when [showServerSwitcher]): presets Demo cloud / .29 / .26, the
/// build's own server when it is none of them, or Custom (IP / host → each service's port; https URL → used
/// as-is for every service).
Future<void> showServerSwitcherSheet(BuildContext context, WidgetRef ref) async {
  final picked = await showModalBottomSheet<String>(
    context: context,
    useRootNavigator: false,
    isScrollControlled: true,
    builder: (_) => const _ServerSheet(),
  );
  if (picked == null || !context.mounted) return;
  if (picked == currentServerOrigin) return context.showSnack('Already using ${serverDisplayOf(picked)}');
  final ok = await showConfirmDialog(
    context,
    title: 'Switch to ${serverLabelOf(picked)}?',
    message:
        '${isGatewayOrigin(picked) ? 'All services will use $picked (one gateway, no ports).' : 'All services will use ${Uri.parse(picked).host} (same ports).'} '
        'You will be signed out and cached server data is cleared.',
    confirmLabel: 'Switch',
  );
  if (!ok || !context.mounted) return;
  await switchServer(context, ref, picked);
}

/// Signs out (the tokens belong to the old server), clears cached API data, saves [origin] and
/// restarts the app's state, which opens Login again.
Future<void> switchServer(BuildContext context, WidgetRef ref, String origin) async {
  if (apiMode) await ref.read(authProvider.notifier).signOutForServerSwitch();
  ref.read(sessionProvider.notifier).logout();
  try {
    await ref.read(appDatabaseProvider).clearCachedResponses(); // Master Data offline copies
  } catch (_) {}
  await FloorPlanImageCache.clearDisk();
  PaintingBinding.instance.imageCache
    ..clear()
    ..clearLiveImages();
  await ref.read(serverProvider.notifier).select(origin);
  if (!context.mounted) return;
  if (!AppRestartScope.restart(context)) context.go(Routes.login);
}

const _custom = 'custom';

class _ServerSheet extends StatefulWidget {
  const _ServerSheet();

  @override
  State<_ServerSheet> createState() => _ServerSheetState();
}

class _ServerSheetState extends State<_ServerSheet> {
  final _current = currentServerOrigin;
  final _build = buildServerOrigin;
  late final List<(String label, String origin)> _options = [
    for (final p in serverPresets) (p.label, p.origin),
    if (_build != null && !serverPresets.any((p) => p.origin == _build)) ('Build default', _build),
  ];
  late String _choice;
  late final TextEditingController _host;
  String? _error;

  @override
  void initState() {
    super.initState();
    final known = _options.any((o) => o.$2 == _current);
    _choice = known || _current == null ? (_current ?? _options.first.$2) : _custom;
    _host = TextEditingController(
      text: known || _current == null ? '' : (isGatewayOrigin(_current) ? _current : Uri.parse(_current).host),
    );
  }

  @override
  void dispose() {
    _host.dispose();
    super.dispose();
  }

  void _submit() {
    if (_choice != _custom) return Navigator.pop(context, _choice);
    final o = normalizeServerOrigin(_host.text);
    if (o == null) {
      return setState(() => _error = 'Enter an IP address, a host name or an https URL, e.g. 172.16.100.40');
    }
    Navigator.pop(context, o);
  }

  @override
  Widget build(BuildContext context) => sheetFrame(context, [
    SheetTitle('Backend server', subtitle: 'Current: ${serverDisplayOf(_current)}'),
    RadioGroup<String>(
      groupValue: _choice,
      onChanged: (v) => setState(() {
        _choice = v ?? _choice;
        _error = null;
      }),
      child: Column(
        children: [
          for (final (label, origin) in _options)
            RadioListTile<String>(
              value: origin,
              contentPadding: EdgeInsets.zero,
              title: Text(label, style: AppText.bodyStrong),
              subtitle: Text(
                origin == _build && label != 'Build default' ? '$origin · build default' : origin,
                style: AppText.caption,
              ),
            ),
          RadioListTile<String>(
            value: _custom,
            contentPadding: EdgeInsets.zero,
            title: Text('Custom', style: AppText.bodyStrong),
            subtitle: Text('An IP address / host, or a full https URL', style: AppText.caption),
          ),
        ],
      ),
    ),
    if (_choice == _custom)
      TextField(
        controller: _host,
        autofocus: true,
        keyboardType: TextInputType.url,
        textInputAction: TextInputAction.done,
        onChanged: (_) => setState(() => _error = null),
        onSubmitted: (_) => _submit(),
        decoration: InputDecoration(hintText: 'e.g. 172.16.100.40 or https://a360demo.purpleiq.ai', errorText: _error),
      ),
    const SizedBox(height: 8),
    Text(
      "An IP or host keeps each service's port (5018, 5401, 5300, 5500, 5460, 5470, 5490). "
      'An https URL is one gateway, used as-is for every service.',
      style: AppText.caption.copyWith(color: AppColors.textSecondary),
    ),
    const SizedBox(height: 12),
    FilledButton(onPressed: _submit, child: const Text('Use this server')),
  ]);
}
