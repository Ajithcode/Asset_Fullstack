import 'package:flutter/material.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/data/api/api_config.dart';

/// Wraps the whole app (MaterialApp builder): in mock mode ([apiMode] false, --dart-define=USE_API=false)
/// a thin orange strip under the status bar says the data is not real, on every screen incl. Login,
/// dialogs and sheets. In API mode it returns [child] unchanged.
class MockModeBanner extends StatelessWidget {
  const MockModeBanner({super.key, required this.child});

  static const text = 'MOCK MODE — demo data, not connected to a server';

  final Widget child;

  @override
  Widget build(BuildContext context) {
    if (apiMode) return child;
    final media = MediaQuery.of(context);
    return Column(
      children: [
        Material(
          key: const ValueKey('mock-mode-banner'),
          color: AppColors.orange,
          child: Padding(
            padding: EdgeInsets.only(top: media.padding.top, left: 12, right: 12),
            child: const SizedBox(
              height: 22,
              width: double.infinity,
              child: Center(
                child: Text(
                  text,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    letterSpacing: 0.3,
                  ),
                ),
              ),
            ),
          ),
        ),
        Expanded(
          child: MediaQuery.removePadding(context: context, removeTop: true, child: child),
        ),
      ],
    );
  }
}
