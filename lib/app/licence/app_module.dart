import 'package:flutter/material.dart';

import 'package:asset360_mobile/app/router/routes.dart';

/// The licensable modules of Asset360 (same split as the web app's licence).
///
/// [key] is the value used in the `LICENSED_MODULES` build setting and, later, by the
/// backend licence endpoint. Screens that belong to no module (sign-in, Home itself,
/// Settings, Profile, Help, Notifications, diagnostics, API samples) are always open:
/// [moduleForLocation] returns null for them.
enum AppModule {
  dashboard('dashboard', 'Dashboard', Icons.dashboard_rounded),
  assets('assets', 'Assets', Icons.inventory_2_rounded),
  scan('scan', 'Scan', Icons.qr_code_scanner_rounded),
  movement('movement', 'Movement & Custody', Icons.swap_horiz_rounded),
  tracking('tracking', 'Tracking & IoT', Icons.location_on_rounded),
  account('account', 'Account', Icons.manage_accounts_rounded),
  maintenance('maintenance', 'Maintenance', Icons.build_rounded),
  inspection('inspection', 'Inspection', Icons.fact_check_rounded),
  issues('issues', 'Issues / Defects', Icons.warning_rounded),
  wip('wip', 'WIP / Operations', Icons.precision_manufacturing_rounded),
  audit('audit', 'Audit & Compliance', Icons.verified_user_rounded),
  inventory('inventory', 'Inventory', Icons.category_rounded),
  documents('documents', 'Documents', Icons.description_rounded),
  alerts('alerts', 'Alerts & Notifications', Icons.notifications_active_rounded),
  contracts('contracts', 'Contracts & Warranty', Icons.handshake_rounded),
  financial('financial', 'Financial', Icons.account_balance_wallet_rounded),
  reports('reports', 'Reports & Analytics', Icons.bar_chart_rounded),
  administration('administration', 'Administration', Icons.admin_panel_settings_rounded);

  const AppModule(this.key, this.label, this.icon);

  /// Build-setting / backend key, e.g. `maintenance`.
  final String key;
  final String label;
  final IconData icon;

  /// Other accepted spellings of a key (`admin` = the web app's name for Administration).
  static const _aliases = {'admin': 'administration'};

  /// The module with build-setting key [key] (case-insensitive; also [_aliases]), or null.
  static AppModule? fromKey(String key) {
    var k = key.trim().toLowerCase();
    k = _aliases[k] ?? k;
    for (final m in values) {
      if (m.key == k) return m;
    }
    return null;
  }
}

/// Licensed when neither the backend nor `LICENSED_MODULES` says otherwise.
const defaultLicensedModules = {
  AppModule.dashboard,
  AppModule.assets,
  AppModule.scan,
  AppModule.movement,
  AppModule.tracking,
  AppModule.reports,
  AppModule.administration,
  AppModule.issues,
  AppModule.alerts,
  AppModule.documents,
  AppModule.account,
};

/// Home drill-downs that belong to another module than the Dashboard.
const _homeChildren = {
  'maintenance-performance': AppModule.maintenance,
  'recent-alerts': AppModule.alerts,
};

const _workChildren = {
  'maintenance': AppModule.maintenance,
  'inspection': AppModule.inspection,
  'issues': AppModule.issues,
  'wip': AppModule.wip,
  'movement': AppModule.movement,
  'audit': AppModule.audit,
};

/// More-tab children; a segment missing here (settings, help, scanner …) is always open.
const _moreChildren = {
  'tracking': AppModule.tracking,
  'inventory': AppModule.inventory,
  'documents': AppModule.documents,
  'registration': AppModule.assets,
  'alerts': AppModule.alerts,
  'contracts': AppModule.contracts,
  'financial': AppModule.financial,
  'reports': AppModule.reports,
  'admin': AppModule.administration,
  'sync': AppModule.account,
  'local-data': AppModule.account,
};

/// The module that owns [location] (a path, a route pattern with `:params`, or a
/// location with a query), or null for an always-open screen.
AppModule? moduleForLocation(String location) {
  final s = Uri.parse(location).pathSegments.where((p) => p.isNotEmpty).toList();
  if (s.isEmpty || !Routes.tabRoots.contains('/${s.first}')) return null; // sign-in screens, markers
  if (s.length == 1) return s.first == 'assets' ? AppModule.assets : null; // Home, Work hub, All Modules
  // Shared children registered under every tab.
  switch (s[1]) {
    case Routes.scanSegment:
      return AppModule.scan;
    case Routes.moreActionsSegment:
      return AppModule.assets;
    case Routes.notificationsSegment || Routes.profileSegment:
      return null;
  }
  return switch (s.first) {
    'home' => _homeChildren[s[1]] ?? AppModule.dashboard,
    'assets' => AppModule.assets,
    'work' => _workChildren[s[1]],
    _ => _moreChildren[s[1]],
  };
}
