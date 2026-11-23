import 'package:asset360_mobile/data/api/assets/json_read.dart';

/// Row of the Asset service GET /api/asset-categories — the categories the web shows and edits
/// (Assets → Categories). One flat list: `parentCategoryId` null = top-level Category, else a Sub
/// Category of that parent (the web registration form uses exactly these two levels).
class AssetCategory {
  const AssetCategory({
    required this.id,
    required this.name,
    this.parentCategoryId,
    this.parentCategoryName,
    this.code,
    this.status,
    this.isDeleted = false,
    this.details = const AssetCategoryDetails(),
  });

  final String id;
  final String name;
  final String? parentCategoryId;
  final String? parentCategoryName;
  final String? code;
  final String? status;
  final bool isDeleted;
  final AssetCategoryDetails details;

  bool get isTopLevel => parentCategoryId == null;

  /// Offered on mobile: not deleted, status Active (or unset) and not hidden with `showInMobile: false`.
  bool get visibleOnMobile =>
      !isDeleted && (status == null || status!.trim().toLowerCase() == 'active') && details.showInMobile != false;

  /// Sub Category is required unless the category says `subcategoryRequired: false` (the web always requires it).
  bool get subCategoryRequired => details.subcategoryRequired ?? true;

  factory AssetCategory.fromJson(Map<String, dynamic> j) => AssetCategory(
    id: jStr(j['id']) ?? '',
    name: (jStr(j['name']) ?? jStr(j['categoryName']) ?? '').trim(),
    parentCategoryId: jStr(j['parentCategoryId']),
    parentCategoryName: jStr(j['parentCategoryName']),
    code: jStr(j['code']),
    status: jStr(j['status']),
    isDeleted: jBool(j['isDeleted']),
    details: AssetCategoryDetails.fromJson(jMap(j['details'])),
  );
}

/// `details` of an [AssetCategory]. The web registration form reads none of these as defaults, so the app
/// only uses [showInMobile] (hide) and [subcategoryRequired] (Sub Category required in step 2).
class AssetCategoryDetails {
  const AssetCategoryDetails({
    this.assetType,
    this.subcategoryRequired,
    this.showInMobile,
    this.trackingMethod,
    this.tagPrefix,
    this.defaultCondition,
  });

  final String? assetType;
  final bool? subcategoryRequired;
  final bool? showInMobile;
  final String? trackingMethod;
  final String? tagPrefix;
  final String? defaultCondition;

  static bool? _bool(Object? v) => v is bool ? v : null;

  factory AssetCategoryDetails.fromJson(Map<String, dynamic> j) => AssetCategoryDetails(
    assetType: jStr(j['assetType']),
    subcategoryRequired: _bool(j['subcategoryRequired']),
    showInMobile: _bool(j['showInMobile']),
    trackingMethod: jStr(j['trackingMethod']),
    tagPrefix: jStr(j['tagPrefix']),
    defaultCondition: jStr(j['defaultCondition']),
  );
}

/// Row of the Asset service GET /api/asset-category-types?categoryId= — the asset types linked to a
/// category or sub category (label = `typeName`).
class AssetCategoryType {
  const AssetCategoryType({required this.id, required this.name, this.categoryId, this.status, this.isDeleted = false});

  final String id;
  final String name;
  final String? categoryId;
  final String? status;
  final bool isDeleted;

  /// Same rule as the web's dropdown lists: not deleted, not Inactive / Disabled / Deleted.
  bool get active => !isDeleted && !const {'inactive', 'disabled', 'deleted'}.contains(status?.trim().toLowerCase());

  factory AssetCategoryType.fromJson(Map<String, dynamic> j) => AssetCategoryType(
    id: jStr(j['id']) ?? '',
    name: (jStr(j['typeName']) ?? jStr(j['name']) ?? jStr(j['code']) ?? '').trim(),
    categoryId: jStr(j['categoryId']),
    status: jStr(j['status']),
    isDeleted: jBool(j['isDeleted']),
  );
}

/// Every [AssetCategory] in [body] (a bare array or `{items: []}`) that is offered on mobile.
List<AssetCategory> parseAssetCategories(Object? body) => [
  for (final r in jItems(body))
    if (r is Map) AssetCategory.fromJson(jMap(r)),
].where((c) => c.id.isNotEmpty && c.name.isNotEmpty && c.visibleOnMobile).toList();

/// Every active [AssetCategoryType] in [body].
List<AssetCategoryType> parseAssetCategoryTypes(Object? body) => [
  for (final r in jItems(body))
    if (r is Map) AssetCategoryType.fromJson(jMap(r)),
].where((t) => t.id.isNotEmpty && t.name.isNotEmpty && t.active).toList();
