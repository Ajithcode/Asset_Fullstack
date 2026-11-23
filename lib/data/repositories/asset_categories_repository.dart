import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/data/api/assets/assets_api.dart';

// Registration Category / Sub Category / Asset Type, from the SAME Asset service lists as the web form
// (projects/assets … asset-dropdown.ts): GET /api/asset-categories, ?parentCategoryId=, and
// GET /api/asset-category-types?categoryId= (the sub category's types, else its category's).
// Kept for the session; [refreshAssetCategories] reloads them (Register Asset open / pull-to-refresh).

/// Top-level categories (parentCategoryId null) offered on mobile, in the server's order.
final assetCategoriesProvider = FutureProvider<List<AssetCategory>>(
  (ref) async => [
    for (final c in await ref.watch(assetsApiProvider).categories())
      if (c.isTopLevel) c,
  ],
);

/// The sub categories of a category id (parentCategoryId = the id) offered on mobile.
final assetSubCategoriesProvider = FutureProvider.family<List<AssetCategory>, String>(
  (ref, categoryId) async => [
    for (final c in await ref.watch(assetsApiProvider).categories(parentCategoryId: categoryId))
      if (c.parentCategoryId == categoryId) c,
  ],
);

/// The asset types for a (category id, sub category id) pair, as the web: the sub category's types; when it
/// has none (or no sub category is chosen), the category's.
final assetCategoryTypesProvider =
    FutureProvider.family<List<AssetCategoryType>, ({String categoryId, String? subCategoryId})>((ref, key) async {
      final api = ref.watch(assetsApiProvider);
      final sub = key.subCategoryId;
      if (sub != null) {
        final types = await api.categoryTypes(sub);
        if (types.isNotEmpty) return types;
      }
      return api.categoryTypes(key.categoryId);
    });

/// Every active asset type (GET /api/asset-category-types without categoryId), e.g. the Assets filter sheet
/// before a category is chosen.
final allAssetCategoryTypesProvider = FutureProvider<List<AssetCategoryType>>(
  (ref) => ref.watch(assetsApiProvider).categoryTypes(null),
);

/// Reloads the category lists (screens keep the old values until the new ones arrive).
void refreshAssetCategories(WidgetRef ref) {
  ref
    ..invalidate(assetCategoriesProvider)
    ..invalidate(assetSubCategoriesProvider)
    ..invalidate(assetCategoryTypesProvider)
    ..invalidate(allAssetCategoryTypesProvider);
}
