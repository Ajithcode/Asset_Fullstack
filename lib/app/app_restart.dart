import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;

/// The app's [ProviderScope], which [restart] replaces with a fresh one: every provider (session,
/// API clients, cached lists) starts again and the router opens on Splash. Used by the server switch.
class AppRestartScope extends StatefulWidget {
  const AppRestartScope({super.key, required this.child, this.overrides = const []});

  final Widget child;

  /// Passed to every new [ProviderScope] (tests).
  final List<Override> overrides;

  /// Restarts the app's state. False when there is no [AppRestartScope] above [context] (tests).
  static bool restart(BuildContext context) {
    final state = context.findAncestorStateOfType<_AppRestartScopeState>();
    state?._restart();
    return state != null;
  }

  @override
  State<AppRestartScope> createState() => _AppRestartScopeState();
}

class _AppRestartScopeState extends State<AppRestartScope> {
  var _generation = 0;

  void _restart() => setState(() => _generation++);

  @override
  Widget build(BuildContext context) =>
      ProviderScope(key: ValueKey(_generation), overrides: widget.overrides, child: widget.child);
}
