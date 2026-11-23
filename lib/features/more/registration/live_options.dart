import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show ProviderListenable, ProviderOrFamily;

import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/api_exception.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_repository.dart';
import 'package:asset360_mobile/data/reference/asset_options.dart';
import 'package:asset360_mobile/data/reference/registration_options.dart';
import 'package:asset360_mobile/data/repositories/asset_categories_repository.dart';
import 'package:asset360_mobile/data/repositories/live_assets_repository.dart';

/// The options of one Register Asset dropdown: a local list (apiMode = false) or a server list
/// that may still be loading or have failed (apiMode = true).
/// Suffix for a saved value the server list does not hold because the backend set it (asset Status
/// "Assigned" / "Available" after an assignment).
const setBySystem = '(set by system)';

class OptionSource {
  const OptionSource(this.options, {this.loading = false, this.error, this.onRetry, this.empty});

  final List<String> options;
  final bool loading;

  /// Why loading failed (the server's message); shown with [onRetry].
  final String? error;

  /// Reloads a server list (shown with [error], and with [empty] when the list is empty).
  final void Function()? onRetry;

  /// Hint when [options] is empty ("No categories on the server yet").
  final String? empty;

  bool get ready => !loading && error == null;
}

/// Every Register Asset value list for the current mode.
///
/// API mode — the SAME source as the web registration form (Asset_Fullstack demo/customer,
/// projects/assets/src/app/services/asset-dropdowns/asset-dropdown.ts):
/// * Asset service: Category (GET /api/asset-categories, parentCategoryId null), Sub Category
///   (?parentCategoryId=), Asset Type (GET /api/asset-category-types?categoryId= of the sub category, else
///   the category). See asset_categories_repository.dart.
/// * Master Data service (MASTER_DATA_BASE_URL): Model (/asset-models, narrowed to the chosen
///   manufacturer), Manufacturer, Condition, Criticality, Department (+ Custodian / Department), Business
///   Unit, Supplier / Vendor, Service Provider, Responsible Team, employees, locations.
/// * Asset service GET /api/asset-registrations/options: Status, Function / Purpose, Tagging Method,
///   Compliance Category, Lifecycle Stage, Ownership Type, Quantity Unit, Currency, Payment Terms,
///   Procurement Method, Warranty Type, Period Unit, Support Coverage, Operating Shift and the step-5 units.
/// * Local: Cost Center (no list on any service; the web has none either).
///
/// Every value is saved as its NAME, like the web's request body (the Asset service stores text).
class RegLists {
  RegLists(this.ref);

  final WidgetRef ref;

  OptionSource _from<T>(
    ProviderListenable<AsyncValue<T>> provider,
    List<String> Function(T) pick,
    String what,
    void Function() retry,
  ) {
    final v = ref.watch(provider);
    final empty = 'No $what on the server yet';
    if (v.hasError && !v.isLoading) {
      return OptionSource(
        const [],
        error: 'Could not load $what: ${ApiException.from(v.error!).message}',
        onRetry: retry,
        empty: empty,
      );
    }
    // A reload keeps showing the previous list until the new one arrives.
    if (v.hasValue) return OptionSource(pick(v.requireValue), empty: empty, onRetry: retry);
    return OptionSource(const [], loading: true, empty: empty);
  }

  /// A Master Data list as names (first of each name only), or [local] in mock mode.
  OptionSource _md<T extends MdEntry>(
    ProviderListenable<AsyncValue<List<T>>> provider,
    ProviderOrFamily invalidate,
    String what, {
    List<String> local = const [],
  }) => apiMode ? _from(provider, namesOf, what, () => ref.invalidate(invalidate)) : OptionSource(local);

  /// A list from GET /api/asset-registrations/options, or [local] in mock mode.
  OptionSource _options(List<String> local, List<String> Function(RegistrationOptions) pick, String what) => apiMode
      ? _from(registrationOptionsProvider, pick, what, () => ref.invalidate(registrationOptionsProvider))
      : OptionSource(local);

  // Step 1
  OptionSource get statuses => _options(AssetOptions.statuses, (o) => o.statuses, 'statuses');
  OptionSource get conditions =>
      _md(mdConditionsProvider, mdConditionsProvider, 'conditions', local: AssetOptions.conditions);

  // Step 2
  OptionSource get criticalities =>
      _md(mdCriticalitiesProvider, mdCriticalitiesProvider, 'criticalities', local: AssetOptions.criticalityLevels);
  OptionSource get departments =>
      _md(mdDepartmentsProvider, mdDepartmentsProvider, 'departments', local: AssetOptions.departments);
  OptionSource get businessUnits =>
      _md(mdBusinessUnitsProvider, mdBusinessUnitsProvider, 'business units', local: AssetOptions.businessUnits);

  /// Local in both modes: neither Master Data nor the Asset service has a cost center list.
  OptionSource get costCenters => const OptionSource(AssetOptions.costCenters);
  OptionSource get functionPurposes =>
      _options(AssetOptions.functionPurposes, (o) => o.functionPurposes, 'functions / purposes');
  OptionSource get taggingMethods => _options(AssetOptions.taggingMethods, (o) => o.taggingMethods, 'tagging methods');
  OptionSource get complianceCategories =>
      _options(AssetOptions.complianceCategories, (o) => o.complianceCategories, 'compliance categories');
  OptionSource get lifecycleStages =>
      _options(AssetOptions.lifecycleStages, (o) => o.lifecycleStages, 'lifecycle stages');

  // Step 3
  OptionSource get ownershipTypes => _options(AssetOptions.ownershipTypes, (o) => o.ownershipTypes, 'ownership types');

  /// API mode: Master Data /teams of the chosen [department] (the web asks teams?departmentId=); every
  /// team while no (listed) department is chosen. Mock mode: [AssetOptions.responsibleTeams].
  OptionSource responsibleTeams(String? department) {
    if (!apiMode) return const OptionSource(AssetOptions.responsibleTeams);
    final deptId = department == null
        ? null
        : ref.watch(mdDepartmentsProvider).value?.where((d) => d.name == department).firstOrNull?.id;
    return _from(
      mdTeamsProvider,
      (List<MdTeam> l) => namesOf(deptId == null ? l : l.where((t) => t.department?.id == deptId)),
      'teams',
      () => ref.invalidate(mdTeamsProvider),
    );
  }

  // Step 4
  /// /options purchaseWarranty.quantityUnits, as the web (not Master Data /uoms).
  OptionSource get quantityUnits => _options(AssetOptions.quantityUnits, (o) => o.quantityUnits, 'quantity units');

  /// Supplier / Vendor suggestions (free text stays allowed). Mock mode: [local].
  OptionSource suppliers(List<String> local) =>
      _md(mdSuppliersProvider, mdSuppliersProvider, 'suppliers', local: local);

  /// Service Provider: Master Data /service-providers, whatever the warranty type (as the web).
  OptionSource get serviceProviders => _md(
    mdServiceProvidersProvider,
    mdServiceProvidersProvider,
    'service providers',
    local: AssetOptions.serviceProviders,
  );

  /// Mock mode stores the ISO code ("AED", label "AED - UAE Dirham"); the server's list holds the
  /// labels themselves, stored as sent.
  OptionSource get currencies => _options(AssetOptions.currencies.keys.toList(), (o) => o.currencies, 'currencies');
  OptionSource get paymentTerms => _options(AssetOptions.paymentTerms, (o) => o.paymentTerms, 'payment terms');
  OptionSource get procurementMethods =>
      _options(AssetOptions.procurementMethods, (o) => o.procurementMethods, 'procurement methods');
  OptionSource get warrantyTypes => _options(AssetOptions.warrantyTypes, (o) => o.warrantyTypes, 'warranty types');
  OptionSource get warrantyPeriodUnits =>
      _options(AssetOptions.warrantyPeriodUnits, (o) => o.warrantyPeriodUnits, 'period units');
  OptionSource get supportCoverages =>
      _options(AssetOptions.supportCoverages, (o) => o.supportCoverages, 'support coverages');

  // Step 5
  OptionSource additional(String name, List<String> local, String what) =>
      _options(local, (o) => o.additional(name), what);
  OptionSource get dimensionUnits => additional('dimensionUnits', AssetOptions.dimensionUnits, 'dimension units');
  OptionSource get weightUnits => additional('weightUnits', AssetOptions.weightUnits, 'weight units');
  OptionSource get lifespanUnits => additional('lifespanUnits', AssetOptions.lifespanUnits, 'lifespan units');
  OptionSource get operatingHoursUnits =>
      additional('operatingHoursUnits', AssetOptions.operatingHoursUnits, 'operating hours units');

  /// /options additionalInformation.operatingShifts, as the web (not Master Data /shifts).
  OptionSource get operatingShifts => additional('operatingShifts', AssetOptions.operatingShifts, 'operating shifts');
  OptionSource get utilizations => additional('utilizations', AssetOptions.utilizations, 'utilizations');
  OptionSource get maintenanceFrequencyUnits =>
      additional('maintenanceFrequencyUnits', AssetOptions.maintenanceFrequencyUnits, 'frequency units');
  OptionSource get complianceStandards =>
      additional('complianceStandards', AssetOptions.complianceStandards, 'compliance standards');
  OptionSource get customFieldTypes =>
      additional('customFieldTypes', AssetOptions.customFieldTypes, 'custom field types');

  // ---------------------------------------------------------------- categories, types, models

  /// API mode: the top-level category named [name] (null when unknown or not loaded).
  AssetCategory? category(String? name) =>
      name == null ? null : ref.watch(assetCategoriesProvider).value?.where((c) => c.name == name).firstOrNull;

  /// API mode: the sub category [name] under [category].
  AssetCategory? subCategory(String? category, String? name) {
    final parent = this.category(category);
    if (parent == null || name == null) return null;
    return ref.watch(assetSubCategoriesProvider(parent.id)).value?.where((s) => s.name == name).firstOrNull;
  }

  /// Sub Category is required unless the chosen category says `details.subcategoryRequired: false`.
  bool subCategoryRequired(String? category) => !apiMode || (this.category(category)?.subCategoryRequired ?? true);

  /// API mode: Asset service GET /api/asset-categories, top level only (parentCategoryId null).
  OptionSource get categories => apiMode
      ? _from(
          assetCategoriesProvider,
          (l) => {for (final c in l) c.name}.toList(),
          'categories',
          () => ref.invalidate(assetCategoriesProvider),
        )
      : OptionSource([for (final o in assetCategories) o.name]);

  /// API mode: GET /api/asset-categories?parentCategoryId= of the chosen category.
  OptionSource subCategories(String? category) {
    if (!apiMode) {
      final c = assetCategories.where((o) => o.name == category).firstOrNull;
      return OptionSource([for (final s in c?.subCategories ?? const <SubCategoryOption>[]) s.name]);
    }
    if (category == null) return const OptionSource([], empty: 'Choose a category first');
    final parent = this.category(category);
    if (parent == null) {
      final src = categories;
      return src.ready ? const OptionSource([], empty: 'No sub categories on the server yet') : src;
    }
    return _from(
      assetSubCategoriesProvider(parent.id),
      (l) => {for (final c in l) c.name}.toList(),
      'sub categories',
      () => ref.invalidate(assetSubCategoriesProvider(parent.id)),
    );
  }

  /// API mode: GET /api/asset-category-types?categoryId= of the chosen sub category; when it has none (or
  /// the category needs no sub category and none is chosen), the category's. Mock mode: [local].
  OptionSource assetTypes(String? category, String? subCategory, {required List<String> local}) {
    if (!apiMode) return OptionSource(local);
    final parent = this.category(category);
    if (subCategory == null && (parent == null || parent.subCategoryRequired)) {
      return const OptionSource([], empty: 'Choose a sub category first');
    }
    if (parent == null) {
      final src = categories;
      return src.ready ? const OptionSource([], empty: 'No asset types on the server yet') : src;
    }
    String? subId;
    if (subCategory != null) {
      final subs = subCategories(category);
      subId = this.subCategory(category, subCategory)?.id;
      if (subId == null) return subs.ready ? const OptionSource([], empty: 'No asset types on the server yet') : subs;
    }
    final p = assetCategoryTypesProvider((categoryId: parent.id, subCategoryId: subId));
    return _from(p, (l) => {for (final t in l) t.name}.toList(), 'asset types', () => ref.invalidate(p));
  }

  /// API mode: Master Data /asset-models for the Model suggestions (free text stays allowed), narrowed to
  /// the chosen [manufacturer] when it is a listed one — as the web. Empty in mock mode.
  OptionSource models(String? manufacturer) {
    if (!apiMode) return const OptionSource([]);
    final p = mdAssetModelsProvider(null);
    final maker = ref.watch(mdManufacturersProvider).value?.where((m) => m.name == manufacturer).firstOrNull;
    return _from(
      p,
      (List<MdAssetModel> l) => namesOf(maker == null ? l : l.where((m) => m.manufacturer?.id == maker.id)),
      'asset models',
      () => ref.invalidate(p),
    );
  }

  /// API mode: the listed manufacturer of the model named [model] (picking a model fills it, as the web).
  String? manufacturerOfModel(String? model) {
    if (!apiMode || model == null) return null;
    final m = ref.read(mdAssetModelsProvider(null)).value?.where((e) => e.name == model).firstOrNull;
    final id = m?.manufacturer?.id;
    if (id == null) return null;
    return ref.read(mdManufacturersProvider).value?.where((e) => e.id == id).firstOrNull?.name;
  }

  /// Manufacturer suggestions. API mode: Master Data /manufacturers; mock mode: [local].
  OptionSource manufacturers(List<String> Function() local) =>
      apiMode ? _md(mdManufacturersProvider, mdManufacturersProvider, 'manufacturers') : OptionSource(local());
}

/// The names of [entries], each once, in the server's order.
List<String> namesOf(Iterable<MdEntry> entries) => {
  for (final e in entries)
    if (e.name.isNotEmpty) e.name,
}.toList();
