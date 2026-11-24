import 'package:asset360_mobile/data/api/api_client.dart';
import 'package:asset360_mobile/data/api/tracking/rtl_models.dart';
import 'package:asset360_mobile/data/api/tracking/tag_live.dart';

export 'package:asset360_mobile/data/api/tracking/rtl_models.dart';
export 'package:asset360_mobile/data/api/tracking/tag_live.dart';

/// Real-Time Location + Locate REST calls of the Tracking service (docs/api/MOBILE-API-HANDOFF.md
/// sections 1 and 2.1; routes in RealTimeLocationEndpoints.cs / LocatingExtensions.cs). The Tracking
/// [ApiClient] adds `clientId` / `tenantId`. LED / buzzer commands go over the /ws/locate WebSocket
/// only (`LocateSocket`), never REST.
class RtlApi {
  const RtlApi(this.client);

  final ApiClient client;

  static const overviewPath = '/api/real-time-location/overview';
  static const tagsPath = '/api/real-time-location/tags';
  static const eventsPath = '/api/real-time-location/events';
  static String trailPath(String assetId) => '/api/real-time-location/assets/${Uri.encodeComponent(assetId)}/trail';
  static String tagGatewayPath(String tagId) => '/api/locating/tags/${Uri.encodeComponent(tagId)}/gateway';
  static String tagCommandsPath(String tagId) => '/api/locating/tags/${Uri.encodeComponent(tagId)}/commands';

  /// Trail ranges (server default 30m) and event ranges (server default 24h).
  static const trailRanges = ['30m', '1h', '24h', '7d'];
  static const eventRanges = ['1h', '24h', '7d'];

  Future<Object?> _get(String path, [Map<String, Object?> query = const {}]) async {
    final q = <String, dynamic>{
      for (final e in query.entries)
        if (e.value != null && !(e.value is String && (e.value as String).trim().isEmpty)) e.key: e.value,
    };
    return (await client.get<Object?>(path, query: q.isEmpty ? null : q)).data;
  }

  /// [floorPlanId] = the plan shown on the map ("assets in view" = located on it); empty = all floors.
  Future<RtlOverview> getOverview({String? siteId, String? floorPlanId}) async =>
      RtlOverview.fromJson(await _get(overviewPath, {'siteId': siteId, 'floorPlanId': floorPlanId}));

  /// GET /tags: the newest location of every tag heard in the last [minutes] (the socket's rows; the
  /// fallback poll while the socket is down). Same filters as `tagsLiveUri`.
  Future<List<TagLocation>> getTags({
    int minutes = 1440,
    String? siteId,
    String? floorPlanId,
    String? zoneId,
    bool zonedOnly = false,
  }) async => TagLocation.parseList(
    await _get(tagsPath, {
      'minutes': minutes,
      'siteId': siteId,
      'floorPlanId': floorPlanId,
      'zoneId': zoneId,
      'zonedOnly': zonedOnly ? true : null,
    }),
  );

  /// [pageSize] 1–100.
  Future<RtlEventPage> getEvents({
    String range = '24h',
    String? siteId,
    int page = 1,
    int pageSize = 10,
    String? assetId,
    String? eventType,
  }) async => RtlEventPage.fromJson(
    await _get(eventsPath, {
      'range': range,
      'siteId': siteId,
      'page': page,
      'pageSize': pageSize.clamp(1, 100),
      'assetId': assetId,
      'eventType': eventType,
    }),
  );

  /// 404 (no tracked tag linked) comes out as an `ApiException` with `isNotFound`.
  Future<RtlTrail> getTrail(String assetId, {String range = '30m'}) async =>
      RtlTrail.fromJson(await _get(trailPath(assetId), {'range': range}));

  Future<LocateTagGateway> getTagGateway(String tagId) async =>
      LocateTagGateway.fromJson(await _get(tagGatewayPath(normalizeTagId(tagId))));

  /// [limit] 1–100 (server default 20).
  Future<List<LocateCommandRecord>> getTagCommands(String tagId, {int limit = 20}) async =>
      LocateCommandRecord.parseList(await _get(tagCommandsPath(normalizeTagId(tagId)), {'limit': limit.clamp(1, 100)}));
}

/// Tag ids are case-insensitive with ':' and '-' stripped (`C3:00:00:AA:BB:01` → `c30000aabb01`),
/// like TagGatewayLookup.NormalizeId on the server.
String normalizeTagId(String id) => id.replaceAll(':', '').replaceAll('-', '').trim().toLowerCase();
