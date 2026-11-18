import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/api_samples.dart';

/// Settings → Developer → API samples (dev builds only, SHOW_SERVER_SWITCHER): GET each preset
/// endpoint (or a custom one) with the signed-in user's token on the current server; shows URL,
/// HTTP status, time and the reply as pretty JSON (lists cut to 3 items, 4,000 characters, secrets
/// redacted). Copy = URL + status + that JSON.
class ApiSamplesScreen extends ConsumerStatefulWidget {
  const ApiSamplesScreen({super.key});

  @override
  ConsumerState<ApiSamplesScreen> createState() => _ApiSamplesScreenState();
}

class _ApiSamplesScreenState extends ConsumerState<ApiSamplesScreen> {
  static const _customKey = 'custom';

  /// Result per row key (preset label or [_customKey]); null value = loading.
  final _results = <String, ApiSampleResult?>{};

  BackendService _customService = BackendService.asset;
  final _customPath = TextEditingController(text: '/api/');
  final _customQuery = TextEditingController();

  @override
  void dispose() {
    _customPath.dispose();
    _customQuery.dispose();
    super.dispose();
  }

  String _keyOf(ApiSample s) => '${s.service.name} ${s.label}';

  Future<void> _fetch(String key, BackendService service, String path, String query) async {
    setState(() => _results[key] = null);
    final r = await ref.read(apiSampleFetcherProvider)(service, path, query);
    if (mounted) setState(() => _results[key] = r);
  }

  @override
  Widget build(BuildContext context) {
    if (!showServerSwitcher) {
      return const AppScaffold(
        body: EmptyState(
          icon: Icons.developer_mode_rounded,
          title: 'Developer builds only',
          message: 'API samples are available in builds with SHOW_SERVER_SWITCHER=true.',
        ),
      );
    }
    return AppScaffold(
      body: ListView(
        padding: context.pagePadding,
        children: [
          PageTitle(
            title: 'API samples',
            subtitle: 'GET with your token on ${serverDisplayOf(currentServerOrigin)}',
            help:
                'Each Fetch calls the endpoint with the signed-in user\'s token on the current server. Replies are '
                'shortened: lists to their first $sampleListItems items, the text to '
                '${sampleMaxChars ~/ 1000},000 characters; values whose key contains token, key, password, secret, '
                'qrcode or otp show their first 6 characters only.',
          ),
          for (final service in {for (final s in apiSamples) s.service}) ...[
            const SizedBox(height: 14),
            SectionHeader(title: '${service.label} · ${Uri.tryParse(service.baseUrl)?.port ?? ''}'),
            const SizedBox(height: 8),
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  for (final (i, s) in apiSamples.where((s) => s.service == service).indexed) ...[
                    if (i > 0) const Divider(height: 1),
                    _SampleRow(
                      label: s.label,
                      fetched: _results.containsKey(_keyOf(s)),
                      result: _results[_keyOf(s)],
                      onFetch: () => _fetch(_keyOf(s), s.service, s.path, s.query),
                    ),
                  ],
                ],
              ),
            ),
          ],
          const SizedBox(height: 14),
          const SectionHeader(title: 'Custom'),
          const SizedBox(height: 8),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                DropdownButtonFormField<BackendService>(
                  initialValue: _customService,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Service'),
                  items: [
                    for (final s in apiSampleServices)
                      DropdownMenuItem(
                        value: s,
                        child: Text(
                          '${s.label} · ${Uri.tryParse(s.baseUrl)?.port ?? ''}',
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                  ],
                  onChanged: (v) => setState(() => _customService = v ?? _customService),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: _customPath,
                  decoration: const InputDecoration(labelText: 'Path', hintText: '/api/...'),
                  autocorrect: false,
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: _customQuery,
                  decoration: const InputDecoration(labelText: 'Query (optional)', hintText: 'page=1&pageSize=3'),
                  autocorrect: false,
                ),
                const SizedBox(height: 4),
                _SampleRow(
                  label: 'GET ${_customService.code}',
                  fetched: _results.containsKey(_customKey),
                  result: _results[_customKey],
                  padding: EdgeInsets.zero,
                  onFetch: () => _fetch(_customKey, _customService, _customPath.text, _customQuery.text),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SampleRow extends StatelessWidget {
  const _SampleRow({
    required this.label,
    required this.fetched,
    required this.result,
    required this.onFetch,
    this.padding = const EdgeInsets.fromLTRB(14, 6, 8, 6),
  });

  final String label;

  /// Fetch pressed at least once ([result] null = loading).
  final bool fetched;
  final ApiSampleResult? result;
  final VoidCallback onFetch;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    final loading = fetched && result == null;
    return Padding(
      padding: padding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(label, style: AppText.bodyStrong.copyWith(fontFamily: 'monospace', fontSize: 12.5)),
              ),
              TextButton(
                onPressed: loading ? null : onFetch,
                child: loading
                    ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                    : Text(fetched ? 'Fetch again' : 'Fetch'),
              ),
            ],
          ),
          if (result case final r?) ...[_ResultView(result: r), const SizedBox(height: 8)],
        ],
      ),
    );
  }
}

class _ResultView extends StatelessWidget {
  const _ResultView({required this.result});

  final ApiSampleResult result;

  @override
  Widget build(BuildContext context) {
    final r = result;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SelectableText(r.url, style: AppText.caption.copyWith(fontFamily: 'monospace')),
        const SizedBox(height: 6),
        Row(
          children: [
            StatusChip(
              r.status == null ? 'No reply' : 'HTTP ${r.status}',
              tone: r.ok ? ChipTone.green : (r.status == null ? ChipTone.grey : ChipTone.red),
            ),
            const SizedBox(width: 8),
            Text('${r.elapsed.inMilliseconds} ms', style: AppText.caption),
            const Spacer(),
            TextButton.icon(
              onPressed: () async {
                await Clipboard.setData(ClipboardData(text: r.copyText));
                if (context.mounted) context.showSnack('Copied URL, status and the shortened JSON');
              },
              icon: const Icon(Icons.copy_rounded, size: 16),
              label: const Text('Copy'),
            ),
          ],
        ),
        if (r.error != null) Text(r.error!, style: AppText.caption.copyWith(color: AppColors.red)),
        if (r.body.isNotEmpty) ...[
          const SizedBox(height: 6),
          Container(
            constraints: const BoxConstraints(maxHeight: 360),
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(color: AppColors.background, borderRadius: BorderRadius.circular(8)),
            child: SingleChildScrollView(
              child: SelectableText(
                r.body,
                style: TextStyle(fontFamily: 'monospace', fontSize: 11.5, height: 1.35, color: AppColors.text),
              ),
            ),
          ),
        ],
      ],
    );
  }
}
