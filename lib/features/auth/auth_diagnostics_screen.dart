import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/server_switcher.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/auth_diagnostics.dart';
import 'package:asset360_mobile/data/repositories/rtl_repository.dart' show RtlSocketDiag, rtlAgo, rtlProvider, rtlSocketDiagProvider;

/// Auth diagnostics (release builds too): the last call of each login-service endpoint as
/// received, secrets shortened, with Copy for the whole report. Opened from Login (outside the
/// shell) or Settings (inside the shell, [inShell]).
class AuthDiagnosticsScreen extends ConsumerWidget {
  const AuthDiagnosticsScreen({super.key, this.inShell = false});

  final bool inShell;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final calls = ref.watch(authDiagnosticsProvider);
    final notifier = ref.read(authDiagnosticsProvider.notifier);
    void copy() {
      final socket = apiMode
          ? '\n\n${ref.read(rtlSocketDiagProvider).report(ref.read(rtlProvider), DateTime.now().toUtc())}'
          : '';
      Clipboard.setData(ClipboardData(text: '${notifier.report()}$socket'));
      context.showSnack('Report copied – paste it to your developer');
    }

    final body = ListView(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
      children: [
        if (inShell)
          const PageTitle(title: 'Auth diagnostics', subtitle: 'What the login service returned')
        else
          Text('What the login service returned', style: AppText.pageSubtitle),
        const SizedBox(height: 12),
        SectionCard(
          title: 'Configuration',
          child: Column(
            children: [
              InfoRow(label: 'USE_API', value: '$apiMode', labelWidth: 168),
              InfoRow(label: 'Server', value: serverDisplayOf(currentServerOrigin), labelWidth: 168),
              for (final s in BackendService.values)
                InfoRow(label: s.define, value: s.baseUrl.isEmpty ? 'Not set' : s.baseUrl, labelWidth: 168),
            ],
          ),
        ),
        if (apiMode) ...[const SizedBox(height: 10), const _LiveMapSocketCard()],
        if (showServerSwitcher) ...[
          const SizedBox(height: 10),
          OutlinedButton.icon(
            onPressed: () => showServerSwitcherSheet(context, ref),
            icon: const Icon(Icons.dns_outlined, size: 18),
            label: const Text('Backend server'),
          ),
        ],
        const SizedBox(height: 10),
        Row(
          children: [
            Expanded(
              child: FilledButton.icon(
                onPressed: copy,
                icon: const Icon(Icons.copy_rounded, size: 18),
                label: const Text('Copy report'),
              ),
            ),
            const SizedBox(width: 10),
            OutlinedButton.icon(
              onPressed: calls.isEmpty ? null : notifier.clear,
              icon: const Icon(Icons.delete_sweep_outlined, size: 18),
              label: const Text('Clear'),
            ),
          ],
        ),
        const SizedBox(height: 6),
        Text(
          'Values whose name contains token, key, qrcode, password, secret or otp show only their first 6 characters.',
          style: AppText.caption,
        ),
        const SizedBox(height: 12),
        for (final e in authEndpoints) _CallCard(endpoint: e, call: calls[e]),
      ],
    );

    if (inShell) return AppScaffold(body: body);
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Auth diagnostics'),
        leading: IconButton(
          tooltip: 'Back',
          icon: const Icon(Icons.arrow_back_rounded),
          onPressed: () => context.canPop() ? context.pop() : context.go(Routes.login),
        ),
      ),
      body: SafeArea(top: false, child: body),
    );
  }
}

/// "Live map socket": the Live Map's tags/live WebSocket (state, URL without the token, last snapshot / update,
/// frames, last close, last error). Never shows a token.
class _LiveMapSocketCard extends ConsumerWidget {
  const _LiveMapSocketCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(rtlProvider);
    final RtlSocketDiag d = ref.watch(rtlSocketDiagProvider);
    final now = DateTime.now().toUtc();
    String at(DateTime? t) => t == null ? 'Never' : '${t.toLocal().toIso8601String().split('.').first} (${rtlAgo(t, now)})';
    final close = d.closeCode == null && (d.closeReason?.isEmpty ?? true)
        ? 'None yet'
        : 'Code ${d.closeCode ?? '-'}${d.closeReason?.isNotEmpty ?? false ? ' · ${d.closeReason}' : ''}';
    return SectionCard(
      title: 'Live map socket',
      child: Column(
        key: const ValueKey('diag-live-socket'),
        children: [
          InfoRow(label: 'State', value: d.stateOf(state), labelWidth: 168),
          InfoRow(label: 'Socket URL', value: d.url ?? 'Not opened yet', labelWidth: 168),
          InfoRow(label: 'Last snapshot', value: at(d.lastSnapshotAt), labelWidth: 168),
          InfoRow(label: 'Last update', value: at(d.lastUpdateAt), labelWidth: 168),
          InfoRow(label: 'Frames received', value: '${d.frames}', labelWidth: 168),
          InfoRow(label: 'Last close', value: close, labelWidth: 168),
          InfoRow(label: 'Last error', value: d.lastError ?? 'None', labelWidth: 168),
        ],
      ),
    );
  }
}

class _CallCard extends StatelessWidget {
  const _CallCard({required this.endpoint, required this.call});

  final String endpoint;
  final AuthCallRecord? call;

  @override
  Widget build(BuildContext context) {
    final c = call;
    final ok = c?.status != null && c!.status! >= 200 && c.status! < 300;
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: AppCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(child: Text(endpoint, style: AppText.bodyStrong)),
                if (c == null)
                  const StatusChip('Not called', tone: ChipTone.grey)
                else
                  StatusChip(
                    c.status == null ? 'No answer' : 'HTTP ${c.status}',
                    tone: ok ? ChipTone.green : ChipTone.red,
                  ),
              ],
            ),
            if (c != null) ...[
              const SizedBox(height: 6),
              Text('${_time(c.at)} · ${c.method} ${c.url}', style: AppText.caption),
              if (c.error != null) ...[
                const SizedBox(height: 6),
                Text(c.error!, style: const TextStyle(color: AppColors.red, fontSize: 12.5)),
              ],
              if (c.note != null) ...[
                const SizedBox(height: 6),
                Text('App: ${c.note}', style: AppText.caption.copyWith(color: AppColors.primary)),
              ],
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(color: AppColors.greyBg, borderRadius: BorderRadius.circular(8)),
                child: SelectableText(
                  c.response == null ? '(empty response)' : const JsonEncoder.withIndent('  ').convert(c.response),
                  style: const TextStyle(fontFamily: 'monospace', fontSize: 11.5, height: 1.35),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  static String _time(DateTime t) =>
      '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}:${t.second.toString().padLeft(2, '0')}';
}
