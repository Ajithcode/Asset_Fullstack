import 'package:flutter/material.dart';

/// The eye at the end of every password field. Hidden (obscured) → crossed-out eye, "Show password";
/// visible → open eye, "Hide password". Tapping calls [onToggle]; fields start hidden.
class PasswordEyeButton extends StatelessWidget {
  const PasswordEyeButton({super.key, required this.obscured, required this.onToggle, this.color});

  /// True while the password is hidden.
  final bool obscured;
  final VoidCallback onToggle;
  final Color? color;

  @override
  Widget build(BuildContext context) => IconButton(
    key: const ValueKey('password-eye'),
    tooltip: obscured ? 'Show password' : 'Hide password',
    onPressed: onToggle,
    icon: Icon(obscured ? Icons.visibility_off_outlined : Icons.visibility_outlined, color: color),
  );
}
