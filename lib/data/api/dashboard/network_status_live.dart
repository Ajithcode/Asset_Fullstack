import 'dart:async';
import 'dart:convert';
import 'dart:io' show WebSocketException;

import 'package:web_socket_channel/io.dart';
import 'package:web_socket_channel/web_socket_channel.dart' show WebSocketChannelException;

import 'package:asset360_mobile/data/api/dashboard/dashboard_models.dart';

/// One open WebSocket, reduced to what [NetworkStatusSocket] needs (tests pass a fake).
abstract class LiveSocket {
  /// Completes when the connection is open; errors when it could not be opened.
  Future<void> get ready;

  /// Text messages; done when the server or the network closes the connection.
  Stream<Object?> get messages;

  Future<void> close();
}

/// Optional extra of a [LiveSocket]: how the connection ended (the diagnostics show it).
abstract interface class LiveSocketCloseInfo {
  int? get closeCode;
  String? get closeReason;
}

/// Opens [uri] (Bearer [token] in the upgrade request when there is one; [uri] already carries it as
/// `?access_token=`, see [withAccessToken]).
typedef LiveSocketConnector = LiveSocket Function(Uri uri, String? token);

/// [uri] with `access_token=<token>` added (browsers can't set headers on a WebSocket, so the gateway may
/// read the query). The token is also sent as the Authorization header. Never log the result.
Uri withAccessToken(Uri uri, String? token) => token == null || token.isEmpty
    ? uri
    : uri.replace(queryParameters: {...uri.queryParameters, 'access_token': token});

/// True when a failed WebSocket upgrade was an HTTP 404 (the path does not exist on this server).
bool isNotFoundUpgrade(Object? error) => switch (error) {
  WebSocketException(:final httpStatusCode) => httpStatusCode == 404,
  WebSocketChannelException(:final inner?) => isNotFoundUpgrade(inner),
  _ => '$error'.contains('HTTP status code: 404'),
};

/// Runs [callback] after [delay]; returns something to cancel it (tests pass a fake).
typedef LiveScheduler = Timer Function(Duration delay, void Function() callback);

/// The real connector: dart:io WebSocket via web_socket_channel.
LiveSocket ioLiveSocket(Uri uri, String? token) => _IoLiveSocket(
  IOWebSocketChannel.connect(
    uri,
    headers: token == null || token.isEmpty ? null : {'Authorization': 'Bearer $token'},
    pingInterval: const Duration(seconds: 20),
    connectTimeout: const Duration(seconds: 10),
  ),
);

class _IoLiveSocket implements LiveSocket, LiveSocketCloseInfo {
  _IoLiveSocket(this.channel);

  final IOWebSocketChannel channel;

  @override
  Future<void> get ready => channel.ready;

  @override
  Stream<Object?> get messages => channel.stream;

  @override
  Future<void> close() => channel.sink.close();

  @override
  int? get closeCode => channel.closeCode;

  @override
  String? get closeReason => channel.closeReason;
}

/// Connection state of the live devices counter.
enum LiveConnection {
  /// Not running (Home hidden / app in the background).
  stopped,
  connecting,
  connected,

  /// Dropped; a reconnect is scheduled after [NetworkStatusSocket.nextDelay].
  waiting,
}

/// GET-free part of Live Devices: `wss://<DASHBOARD base>/api/dashboards/network-status/gateways/live` (falls
/// back to [fallbackUri] = `/api/dashboards/network-status/live` when the new path answers 404) pushes
/// `{totalOnline, totalOffline}` on connect and whenever the counts change. When the socket cannot
/// open or drops, it reconnects after 2 s, 5 s, 10 s, then every 30 s ([backoff]); a message
/// resets the backoff. [start] / [stop] follow Home's visibility and the app lifecycle.
class NetworkStatusSocket {
  NetworkStatusSocket({
    required this.uri,
    this.fallbackUri,
    required this.onCounts,
    required this.onConnection,
    this.token,
    LiveSocketConnector? connect,
    LiveScheduler? schedule,
  }) : _connect = connect ?? ioLiveSocket,
       _schedule = schedule ?? Timer.new;

  static const backoff = [Duration(seconds: 2), Duration(seconds: 5), Duration(seconds: 10), Duration(seconds: 30)];

  final Uri uri;

  /// Used instead of [uri] after [uri]'s upgrade answered 404 (before any connection on it opened).
  final Uri? fallbackUri;
  final void Function(NetworkStatusCounts counts) onCounts;
  final void Function(LiveConnection state) onConnection;

  /// The Bearer token for the upgrade request (null = none).
  final Future<String?> Function()? token;
  final LiveSocketConnector _connect;
  final LiveScheduler _schedule;

  bool _useFallback = false;
  bool _openedOnce = false;

  /// The path in use: [uri], or [fallbackUri] after a 404.
  Uri get currentUri => _useFallback ? fallbackUri! : uri;

  LiveSocket? _socket;
  StreamSubscription<Object?>? _sub;
  Timer? _retry;
  bool _running = false;

  /// Failed attempts since the last good message (picks the next [backoff] step).
  int failures = 0;
  LiveConnection state = LiveConnection.stopped;

  /// How long the next reconnect waits.
  Duration get nextDelay => backoff[(failures - 1).clamp(0, backoff.length - 1)];

  bool get running => _running;

  void start() {
    if (_running) return;
    _running = true;
    failures = 0;
    _open();
  }

  Future<void> stop() async {
    _running = false;
    _retry?.cancel();
    _retry = null;
    await _closeSocket();
    _set(LiveConnection.stopped);
  }

  void _set(LiveConnection s) {
    if (state == s) return;
    state = s;
    onConnection(s);
  }

  Future<void> _open() async {
    if (!_running) return;
    _set(LiveConnection.connecting);
    String? t;
    try {
      t = await token?.call();
    } catch (_) {
      t = null;
    }
    if (!_running) return;
    final LiveSocket socket;
    try {
      socket = _connect(withAccessToken(currentUri, t), t);
    } catch (e) {
      _dropped(null, e);
      return;
    }
    _socket = socket;
    _sub = socket.messages.listen(
      _onMessage,
      onError: (Object e) => _dropped(socket, e),
      onDone: () => _dropped(socket),
      cancelOnError: true,
    );
    try {
      await socket.ready;
      if (_running && identical(_socket, socket)) {
        _openedOnce = true;
        _set(LiveConnection.connected);
      }
    } catch (e) {
      _dropped(socket, e);
    }
  }

  void _onMessage(Object? data) {
    try {
      final body = data is String ? jsonDecode(data) : (data is List<int> ? jsonDecode(utf8.decode(data)) : data);
      if (body is! Map) return;
      failures = 0;
      _set(LiveConnection.connected);
      onCounts(NetworkStatusCounts.fromJson(body));
    } catch (_) {
      // Not JSON: ignore the message, keep the connection.
    }
  }

  /// The socket closed or failed: schedule the next attempt (only once per socket).
  void _dropped([LiveSocket? socket, Object? error]) {
    if (socket != null && !identical(_socket, socket)) return;
    _closeSocket();
    if (!_running) return;
    // The new path does not exist on this server: use the older one from now on.
    if (!_useFallback && !_openedOnce && fallbackUri != null && error != null && isNotFoundUpgrade(error)) {
      _useFallback = true;
    }
    failures++;
    _set(LiveConnection.waiting);
    _retry?.cancel();
    _retry = _schedule(nextDelay, () {
      _retry = null;
      _open();
    });
  }

  Future<void> _closeSocket() async {
    final sub = _sub, socket = _socket;
    _sub = null;
    _socket = null;
    await sub?.cancel();
    try {
      await socket?.close();
    } catch (_) {
      // Already closed.
    }
  }
}
