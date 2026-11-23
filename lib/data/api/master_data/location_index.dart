import 'package:asset360_mobile/data/api/master_data/master_data_models.dart';
export 'package:asset360_mobile/data/api/master_data/master_data_models.dart' show MdLocationLevel;
import 'package:asset360_mobile/data/models/asset_registration.dart';

/// A picked location: the nodes from the country down to the deepest level chosen. A level can be
/// skipped (a zone straight under a building or a site), so levels come from each node's [MdLocationNode.level].
class MdLocationPath {
  const MdLocationPath([this.nodes = const []]);

  static const empty = MdLocationPath();

  final List<MdLocationNode> nodes;

  bool get isEmpty => nodes.isEmpty;

  MdLocationNode? at(MdLocationLevel level) => nodes.where((n) => n.level == level).firstOrNull;

  MdLocationNode? get deepest => nodes.isEmpty ? null : nodes.last;

  MdLocationLevel? get level => nodes.isEmpty ? null : nodes.last.level;

  /// The path above [level] with [node] at that level (that level and the ones below are dropped).
  MdLocationPath pick(MdLocationLevel level, MdLocationNode? node) =>
      MdLocationPath([...nodes.where((n) => (n.level?.index ?? 0) < level.index), ?node]);

  /// "UAE > Dubai Factory > Building A > Floor 2".
  String get label => [for (final n in nodes) n.name].join(' > ');

  /// Writes every level's id + name (levels not picked are cleared) and the path into [l].
  void writeTo(AssetLocation l) {
    String? id(MdLocationLevel v) => at(v)?.id;
    String? name(MdLocationLevel v) => at(v)?.name;
    l
      ..countryId = id(MdLocationLevel.country)
      ..countryName = name(MdLocationLevel.country)
      ..siteId = id(MdLocationLevel.site)
      ..siteName = name(MdLocationLevel.site)
      ..buildingId = id(MdLocationLevel.building)
      ..buildingName = name(MdLocationLevel.building)
      ..floorId = id(MdLocationLevel.floor)
      ..floorName = name(MdLocationLevel.floor)
      ..zoneId = id(MdLocationLevel.zone)
      ..zoneName = name(MdLocationLevel.zone)
      ..roomId = id(MdLocationLevel.room)
      ..roomName = name(MdLocationLevel.room)
      ..serverLocationPath = isEmpty ? null : label;
  }

  @override
  bool operator ==(Object other) =>
      other is MdLocationPath &&
      other.nodes.length == nodes.length &&
      [for (var i = 0; i < nodes.length; i++) other.nodes[i].id == nodes[i].id].every((e) => e);

  @override
  int get hashCode => Object.hashAll(nodes.map((n) => n.id));
}

/// Lookups over the location tree: by id, by code, children, and the path of a node.
class MdLocationIndex {
  MdLocationIndex(this.roots) {
    void walk(MdLocationNode n, MdLocationNode? parent) {
      _byId[n.id] = n;
      if (parent != null) _parent[n.id] = parent;
      final code = n.code.trim().toLowerCase();
      if (code.isNotEmpty) _byCode.putIfAbsent(code, () => n);
      for (final c in n.children) {
        walk(c, n);
      }
    }

    for (final r in roots) {
      walk(r, null);
    }
  }

  final List<MdLocationNode> roots;
  final _byId = <String, MdLocationNode>{};
  final _byCode = <String, MdLocationNode>{};
  final _parent = <String, MdLocationNode>{};

  bool get isEmpty => roots.isEmpty;

  int get length => _byId.length;

  MdLocationNode? byId(String? id) => id == null ? null : _byId[id];

  /// Case-insensitive match on the location's `code` (printed as "LOC:<code>").
  MdLocationNode? byCode(String? code) => code == null ? null : _byCode[code.trim().toLowerCase()];

  /// Roots for [parent] == null, else the parent's children.
  List<MdLocationNode> childrenOf(MdLocationNode? parent) => parent == null ? roots : parent.children;

  /// Root → [node]; empty for an unknown node.
  MdLocationPath pathTo(MdLocationNode? node) {
    if (node == null || !_byId.containsKey(node.id)) return MdLocationPath.empty;
    final chain = <MdLocationNode>[node];
    for (var p = _parent[node.id]; p != null; p = _parent[p.id]) {
      chain.insert(0, p);
    }
    return MdLocationPath(chain);
  }

  /// The deepest saved level of [l] that is in the tree (ids first, then names under the parent).
  MdLocationPath pathOf(AssetLocation l) {
    final ids = [l.countryId, l.siteId, l.buildingId, l.floorId, l.zoneId, l.roomId];
    for (final id in ids.reversed) {
      final n = byId(id);
      if (n != null) return pathTo(n);
    }
    final names = [l.countryName, l.siteName, l.buildingName, l.floorName, l.zoneName, l.roomName];
    MdLocationNode? at;
    for (final name in names) {
      if (name == null) continue; // a skipped level (e.g. a zone straight under a building)
      final next = childrenOf(at).where((c) => c.name == name).firstOrNull;
      if (next == null) break;
      at = next;
    }
    return pathTo(at);
  }
}

String _norm(String? s) => (s ?? '').trim().toLowerCase();

/// Builds the location tree exactly like the web registration form (`buildMasterLocationTree` in
/// Asset_Fullstack): Country → Site → Building → Floor → Zone → Room, from the separate Master Data lists.
///
/// * Countries come from each site's country name (id = the name, "Unspecified" when empty).
/// * A building hangs off its site, a floor off its building (rows whose parent is missing are dropped).
/// * A zone hangs off its floor, else its building, else its site (an outdoor zone).
/// * A room (`/locations`) names its zone / floor / building: its parent is the zone with that name (narrowed
///   by floor and building), else the floor, else the building.
/// * Every id is the Master Data record id, so floor ids equal the floor-plan `locationId`.
/// Deleted / inactive rows are expected to be dropped already (see [mdParseList]).
List<MdLocationNode> buildMdLocationTree({
  List<MdSite> sites = const [],
  List<MdSpace> buildings = const [],
  List<MdSpace> floors = const [],
  List<MdSpace> zones = const [],
  List<MdRoom> rooms = const [],
}) {
  final byId = <String, _MutableNode>{};
  _MutableNode make(MdLocationLevel level, String id, String name, String code, _MutableNode? parent, {String? image}) {
    final n = _MutableNode(level, id, name, code, parent?.id, image);
    byId[id] = n;
    parent?.children.add(n);
    return n;
  }

  final countries = <_MutableNode>[];
  for (final site in sites) {
    final countryName = (site.country?.trim().isNotEmpty ?? false) ? site.country!.trim() : 'Unspecified';
    var country = countries.where((c) => _norm(c.name) == _norm(countryName)).firstOrNull;
    if (country == null) {
      country = _MutableNode(MdLocationLevel.country, countryName, countryName, '', null, null);
      byId.putIfAbsent(countryName, () => country!);
      countries.add(country);
    }
    make(MdLocationLevel.site, site.id, site.name, site.code, country);
  }
  for (final b in buildings) {
    final parent = byId[b.site?.id];
    if (parent != null && parent.level == MdLocationLevel.site) {
      make(MdLocationLevel.building, b.id, b.name, b.code, parent);
    }
  }
  for (final f in floors) {
    final parent = byId[f.buildingId];
    if (parent != null && parent.level == MdLocationLevel.building) {
      make(MdLocationLevel.floor, f.id, f.name, f.code, parent, image: f.imageUrl);
    }
  }
  for (final z in zones) {
    final parent = byId[z.floorId] ?? byId[z.buildingId] ?? byId[z.site?.id];
    if (parent != null && parent.level != MdLocationLevel.country) {
      make(MdLocationLevel.zone, z.id, z.name, z.code, parent);
    }
  }

  // Rooms name their parents: find them by name, narrowing by building and floor when given.
  final all = byId.values.toList();
  List<_MutableNode> named(Set<MdLocationLevel> levels, String? name) => [
    for (final n in all)
      if (levels.contains(n.level) && name != null && (_norm(n.name) == _norm(name) || _norm(n.code) == _norm(name))) n,
  ];
  bool under(_MutableNode n, List<_MutableNode>? ancestors) =>
      ancestors == null || ancestors.isEmpty || ancestors.any((a) => a.contains(n));
  for (final room in rooms) {
    final bs = room.buildingName?.isNotEmpty == true ? named({MdLocationLevel.building}, room.buildingName) : null;
    final fs = room.floorName?.isNotEmpty == true
        ? named({MdLocationLevel.floor}, room.floorName).where((f) => under(f, bs)).toList()
        : null;
    final parent =
        named({MdLocationLevel.zone}, room.zoneName).where((z) => under(z, fs) && under(z, bs)).firstOrNull ??
        fs?.firstOrNull ??
        bs?.firstOrNull;
    if (parent != null) make(MdLocationLevel.room, room.id, room.name, room.code, parent);
  }
  return [for (final c in countries) c.freeze()];
}

class _MutableNode {
  _MutableNode(this.level, this.id, this.name, this.code, this.parentId, this.image);

  final MdLocationLevel level;
  final String id, name, code;
  final String? parentId, image;
  final children = <_MutableNode>[];

  bool contains(_MutableNode n) => identical(this, n) || children.any((c) => c.contains(n));

  MdLocationNode freeze() => MdLocationNode(
    id: id,
    name: name,
    code: code,
    type: level.label,
    parentId: parentId,
    level: level,
    mapImage: image,
    children: [for (final c in children) c.freeze()],
  );
}
