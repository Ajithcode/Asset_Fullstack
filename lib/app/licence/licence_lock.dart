import 'dart:ui' show ImageFilter;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:asset360_mobile/app/client_demo.dart';
import 'package:asset360_mobile/app/licence/app_module.dart';
import 'package:asset360_mobile/app/licence/licence_service.dart';
import 'package:asset360_mobile/app/router/routes.dart';
import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/app/theme/app_theme.dart';
import 'package:asset360_mobile/core/widgets/app_scaffold.dart';
import 'package:asset360_mobile/core/widgets/page_title.dart';

/// Wording of the lock card and sheet.
abstract final class LicenceText {
  static const title = 'Activate licence';
  static const body = "This module isn't included in your licence. Activate the licence to get access.";
  static const sheetBody = 'Activate licence to get access';
  static const backToDashboard = 'Back to Dashboard';
}

/// The current licence, read once (callbacks, helpers without a WidgetRef).
LicenceState licenceOf(BuildContext context) => ProviderScope.containerOf(context, listen: false).read(licenceProvider);

/// "Back to Dashboard": Home, with the locked screen gone from the stack.
void goBackToDashboard(BuildContext context) => GoRouter.of(context).go(Routes.home);

// ---------------------------------------------------------------- route gate

/// Wraps a route's screen: a licensed (or always-open) module builds [builder]; a locked
/// module never builds it (so none of its providers / APIs run) and shows [LicenceLockScreen].
class LicenceGate extends ConsumerWidget {
  const LicenceGate({super.key, required this.module, required this.builder});

  final AppModule module;
  final WidgetBuilder builder;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final licensed = ref.watch(licenceProvider.select((l) => l.isLicensed(module)));
    return licensed ? builder(context) : LicenceLockScreen(module: module);
  }
}

/// A locked module's screen: its empty layout under a strong blur + light scrim, nothing
/// behind it reacts, and one card with "Back to Dashboard". Android back also goes Home.
class LicenceLockScreen extends StatelessWidget {
  const LicenceLockScreen({super.key, required this.module});

  final AppModule module;

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) goBackToDashboard(context);
      },
      child: Material(
        type: MaterialType.transparency,
        child: Stack(
          fit: StackFit.expand,
          children: [
            IgnorePointer(
              child: ExcludeSemantics(
                child: AppScaffold(body: _EmptyModuleLayout(module: module)),
              ),
            ),
            ClipRect(
              child: BackdropFilter(
                filter: ImageFilter.blur(sigmaX: 8, sigmaY: 8),
                child: ColoredBox(color: Colors.white.withValues(alpha: .28)),
              ),
            ),
            // Swallows every tap / scroll that misses the card.
            const ModalBarrier(dismissible: false),
            SafeArea(
              child: Center(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.fromLTRB(24, 24, 24, 110),
                  child: LicenceLockCard(module: module, onBackToDashboard: () => goBackToDashboard(context)),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Placeholder page (title + grey blocks) shown behind the blur instead of the real screen,
/// so no data is loaded for a locked module.
class _EmptyModuleLayout extends StatelessWidget {
  const _EmptyModuleLayout({required this.module});

  final AppModule module;

  @override
  Widget build(BuildContext context) {
    Widget block(double h) => Container(
      height: h,
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(color: AppColors.tileLavender, borderRadius: BorderRadius.circular(AppRadius.card)),
    );
    return ListView(
      physics: const NeverScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(16, 18, 16, 16),
      children: [
        PageTitle(title: module.label, subtitle: ' '),
        const SizedBox(height: 16),
        Row(
          children: [
            for (var i = 0; i < 3; i++) ...[if (i > 0) const SizedBox(width: 10), Expanded(child: block(72))],
          ],
        ),
        block(44),
        for (var i = 0; i < 5; i++) block(76),
      ],
    );
  }
}

/// Lock icon, module name, "Activate licence" and the explanation. [onBackToDashboard]
/// adds the single "Back to Dashboard" button (left out inside asset-detail tabs).
class LicenceLockCard extends StatelessWidget {
  const LicenceLockCard({super.key, required this.module, this.onBackToDashboard});

  final AppModule module;
  final VoidCallback? onBackToDashboard;

  @override
  Widget build(BuildContext context) {
    return Container(
      key: ValueKey('licence-lock-${module.key}'),
      constraints: const BoxConstraints(maxWidth: 360),
      padding: const EdgeInsets.fromLTRB(20, 22, 20, 20),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.sheet),
        border: Border.all(color: AppColors.divider),
        boxShadow: const [BoxShadow(color: Color(0x22000000), blurRadius: 24, offset: Offset(0, 8))],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const _LockIcon(size: 60),
          const SizedBox(height: 12),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(module.icon, size: 16, color: AppColors.textMuted),
              const SizedBox(width: 6),
              Flexible(
                child: Text(module.label, style: AppText.secondary.copyWith(fontWeight: FontWeight.w600)),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(LicenceText.title, textAlign: TextAlign.center, style: AppText.sectionTitle.copyWith(fontSize: 19)),
          const SizedBox(height: 8),
          Text(LicenceText.body, textAlign: TextAlign.center, style: AppText.secondary),
          if (onBackToDashboard != null) ...[
            const SizedBox(height: 18),
            SizedBox(
              width: double.infinity,
              child: FilledButton.icon(
                onPressed: onBackToDashboard,
                icon: const Icon(Icons.dashboard_rounded, size: 18),
                label: const Text(LicenceText.backToDashboard),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// A tab of the asset detail that belongs to [module] (Maintenance, Inspection, Financial):
/// the lock card in place of [child] (never built, so nothing loads) when it is locked.
class AssetTabLicence extends ConsumerWidget {
  const AssetTabLicence({super.key, required this.module, required this.child});

  final AppModule module;
  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (ref.watch(licenceProvider.select((l) => l.isLicensed(module)))) return child;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Center(child: LicenceLockCard(module: module)),
    );
  }
}

class _LockIcon extends StatelessWidget {
  const _LockIcon({required this.size});

  final double size;

  @override
  Widget build(BuildContext context) => Container(
    width: size,
    height: size,
    decoration: BoxDecoration(color: AppColors.tileLavender, shape: BoxShape.circle),
    child: Icon(Icons.lock_rounded, size: size * .5, color: AppColors.primary),
  );
}

/// Small lock badge for the faded More-sheet / Work-hub tiles of locked modules.
class LicenceLockBadge extends StatelessWidget {
  const LicenceLockBadge({super.key});

  @override
  Widget build(BuildContext context) => Container(
    key: const ValueKey('licence-lock-badge'),
    width: 20,
    height: 20,
    decoration: BoxDecoration(
      color: AppColors.surface,
      shape: BoxShape.circle,
      border: Border.all(color: AppColors.divider),
    ),
    child: Icon(Icons.lock_rounded, size: 12, color: AppColors.textMuted),
  );
}

/// A More-sheet / Work-hub tile of a locked module: still visible and tappable, faded,
/// with a lock badge at the top-right. [locked] false returns [child] unchanged.
class LicenceTileLook extends StatelessWidget {
  const LicenceTileLook({super.key, required this.locked, required this.child});

  final bool locked;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    if (!locked) return child;
    return Stack(
      clipBehavior: Clip.none,
      children: [
        Positioned.fill(child: Opacity(opacity: .5, child: child)),
        const Positioned(top: 5, right: 5, child: IgnorePointer(child: LicenceLockBadge())),
      ],
    );
  }
}

// ---------------------------------------------------------------- cross-links

/// Cross-link into a locked module from inside a licensed screen: the user stays where
/// they are; lock icon, module name, "Activate licence to get access" and one OK button.
Future<void> showLicenceSheet(BuildContext context, AppModule module) {
  return showModalBottomSheet<void>(
    context: context,
    useRootNavigator: false,
    showDragHandle: true,
    builder: (context) => SafeArea(
      child: Padding(
        key: ValueKey('licence-sheet-${module.key}'),
        padding: const EdgeInsets.fromLTRB(24, 0, 24, 20),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const _LockIcon(size: 56),
            const SizedBox(height: 12),
            Text(module.label, textAlign: TextAlign.center, style: AppText.sectionTitle),
            const SizedBox(height: 6),
            Text(LicenceText.sheetBody, textAlign: TextAlign.center, style: AppText.secondary),
            const SizedBox(height: 18),
            SizedBox(
              width: double.infinity,
              child: FilledButton(onPressed: () => Navigator.pop(context), child: const Text('OK')),
            ),
          ],
        ),
      ),
    ),
  );
}

/// Licence-aware links for cross-links inside licensed screens (alongside `DemoGuard`,
/// which still decides the client-demo allow-list first).
///
/// A target in a locked module opens [showLicenceSheet] instead of navigating. Entry
/// points that should show the blurred screen (More sheet, Work hub, Home quick actions,
/// notifications) keep plain navigation: the route itself is gated by [LicenceGate].
abstract final class LicenceGuard {
  /// A callback that pushes [route], the licence sheet when its module is locked,
  /// or null when the client demo does not allow the screen.
  static VoidCallback? push(BuildContext context, String route) =>
      demoCanOpen(route) ? () => open(context, route) : null;

  /// Like [push] with `go`.
  static VoidCallback? go(BuildContext context, String route) =>
      demoCanOpen(route) ? () => open(context, route, go: true) : null;

  /// Navigates to [route], or shows the licence sheet when its module is locked.
  static void open(BuildContext context, String route, {bool go = false}) {
    final locked = licenceOf(context).lockedModuleOf(route);
    if (locked != null) {
      showLicenceSheet(context, locked);
    } else if (go) {
      context.go(route);
    } else {
      context.push(route);
    }
  }

  /// [onTap] when [module] is licensed, else a callback that shows the licence sheet.
  /// Null stays null (the control is inert for another reason).
  static VoidCallback? tap(BuildContext context, AppModule module, VoidCallback? onTap) {
    if (onTap == null) return null;
    return () => licenceOf(context).isLicensed(module) ? onTap() : showLicenceSheet(context, module);
  }
}
