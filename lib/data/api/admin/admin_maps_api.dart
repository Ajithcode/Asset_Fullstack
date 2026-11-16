import 'package:asset360_mobile/data/api/admin/map_models.dart';
import 'package:asset360_mobile/data/api/api_client.dart';

export 'package:asset360_mobile/data/api/admin/map_models.dart';

/// The READ-ONLY map endpoints of the Administration service (ADMIN_BASE_URL, port 5460).
/// Paths and query parameters: docs/api/administration.json (live OpenAPI) — identical to
/// docs/backend_ref/Services/Administration/Api/A360.Administration.Api/Endpoints/MapEndpoints.cs.
class AdminMapsApi {
  const AdminMapsApi(this.client);

  final ApiClient client;

  static const base = '/api/administration/maps';
  static const floorPlans = '$base/floor-plans';
  static const settings = '$base/settings';
  static String zones(String floorPlanId) => '$floorPlans/${Uri.encodeComponent(floorPlanId)}/zones';
  static String plots(String floorPlanId) => '$floorPlans/${Uri.encodeComponent(floorPlanId)}/plots';
  static String layers(String floorPlanId) => '$floorPlans/${Uri.encodeComponent(floorPlanId)}/layers';

  Future<Object?> _get(String path, [Map<String, Object?> query = const {}]) async {
    final q = <String, dynamic>{
      for (final e in query.entries)
        if (e.value != null && !(e.value is String && (e.value as String).trim().isEmpty)) e.key: e.value,
    };
    return (await client.get<Object?>(path, query: q.isEmpty ? null : q)).data;
  }

  /// Floor plans; [locationId] = the Master Data site / building / floor id (exact match),
  /// [status] = Draft / Published / Archived.
  Future<List<MapFloorPlan>> getFloorPlans({String? locationId, String? status}) async =>
      MapFloorPlan.parseList(await _get(floorPlans, {'locationId': locationId, 'status': status}));

  /// The PUBLISHED plan of one location (newest published first), or null.
  Future<MapFloorPlan?> getPublishedPlan(String locationId) async {
    final plans = await getFloorPlans(locationId: locationId, status: 'Published');
    plans.sort((a, b) => (b.publishedAt ?? DateTime(0)).compareTo(a.publishedAt ?? DateTime(0)));
    return plans.firstOrNull;
  }

  Future<List<MapZone>> getZones(String floorPlanId) async => MapZone.parseList(await _get(zones(floorPlanId)));

  /// [kind] = asset | device | null (both).
  Future<List<MapPlot>> getPlots(String floorPlanId, {String? kind}) async =>
      MapPlot.parseList(await _get(plots(floorPlanId), {'kind': kind}));

  Future<List<MapLayer>> getLayers(String floorPlanId) async => MapLayer.parseList(await _get(layers(floorPlanId)));

  Future<MapSettings> getSettings() async => MapSettings.fromJson(await _get(settings));
}
