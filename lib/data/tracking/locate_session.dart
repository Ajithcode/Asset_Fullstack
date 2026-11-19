import 'dart:async';
import 'dart:convert';
import 'dart:io' show WebSocketException;

import 'package:flutter/foundation.dart';
import 'package:web_socket_channel/io.dart';
import 'package:web_socket_channel/web_socket_channel.dart' show WebSocketChannelException;

import 'package:asset360_mobile/data/api/dashboard/network_status_live.dart' show LiveScheduler, withAccessToken;
import 'package:asset360_mobile/data/api/tracking/rtl_models.dart' show LocateTagGateway;
import 'package:asset360_mobile/data/tracking/locate_commands.dart';

/// One open /ws/locate connection, reduced to what [LocateSession] needs (tests pass a fake).
abstract class LocateChannel {
  Future<void> get ready;
  Stream<Object?> get messages;
  void send(String text);
  Future<void> close();
}

/// Opens [uri]; [bearer] goes in the `Authorization` header of the upgrade (null = none).
typedef LocateConnector = LocateChannel Function(Uri uri, String? bearer);

LocateChannel ioLocateChannel(Uri uri, String? bearer) => _IoLocateChannel(
  IOWebSocketChannel.connect(
    uri,
    headers: bearer == null || bearer.isEmpty ? null : {'Authorization': 'Bearer $bearer'},
    pingInterval: const Duration(seconds: 20),
    connectTimeout: const Duration(seconds: 10),
  ),
);

class _IoLocateChannel implements LocateChannel {
  _IoLocateChannel(this.channel);

  final IOWebSocketChannel channel;

  @override
  Future<void> get ready => channel.ready;

  @override
  Stream<Object?> get messages => channel.stream;

  @override
  void send(String text) => channel.sink.add(text);

  @override
  Future<void> close() => channel.sink.close();
}

/// True when a failed upgrade was HTTP 401 (no / expired token).
bool isUnauthorizedUpgrade(Object? error) => switch (error) {
  WebSocketException(:final httpStatusCode) => httpStatusCode == 401,
  WebSocketChannelException(:final inner?) => isUnauthorizedUpgrade(inner),
  _ => '$error'.contains('HTTP status code: 401'),
};

/// `wss://<host>/ws/locate` for an https base, `ws://` for http (port kept, path replaced).
Uri locateSocketUri(String trackingBaseUrl) {
  final base = Uri.parse(trackingBaseUrl);
  return Uri(
    scheme: base.scheme == 'https' ? 'wss' : 'ws',
    host: base.host,
    port: base.hasPort ? base.port : null,
    path: '/ws/locate',
  );
}

/// Connection state of the Locate panel.
enum LocateConnection { connecting, subscribed, failed, closed }

/// The Locate panel's WebSocket session (docs/api/MOBILE-API-HANDOFF.md 2.1–2.3): opens
/// /ws/locate with the token in the Authorization header, subscribes to one tag, sends LED / buzzer
/// frames and follows their `commandSent` → `commandStatus` progress by reqId. A 401 reconnects once
/// with a fresh token, then once with `?access_token=` (servers that ignore the header). The token
/// is never logged. No MQTT.
class LocateSession extends ChangeNotifier {
  LocateSession({
    required this.uri,
    required this.token,
    this.refreshToken,
    LocateConnector? connect,
    LiveScheduler? schedule,
  }) : _connect = connect ?? ioLocateChannel,
       _schedule = schedule ?? Timer.new;

  final Uri uri;
  final Future<String?> Function() token;

  /// A new access token after a 401 (null = signed out).
  final Future<String?> Function()? refreshToken;
  final LocateConnector _connect;
  final LiveScheduler _schedule;

  LocateChannel? _channel;
  StreamSubscription<Object?>? _sub;
  Timer? _timeout;
  bool _disposed = false;
  bool _turningOff = false;

  /// Attempts so far for the current [open]: 0 = header, 1 = fresh token, 2 = query token.
  int _attempt = 0;
  String? _fresh;

  String? tagId;
  LocateConnection connection = LocateConnection.connecting;

  /// From the `subscribed` frame.
  LocateTagGateway? gateway;

  /// An `error` frame / connection problem shown above the buttons (e.g. "No readings for this tag yet").
  String? notice;
  LocateProgress progress = LocateProgress.idle;

  /// Frames sent (tests read them; never contains the token).
  @visibleForTesting
  final sent = <Map<String, Object>>[];

  bool get offline => gateway?.mqttConnected == false;
  bool get canSend => connection == LocateConnection.subscribed && progress.finished;

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  /// Connects (if needed) and subscribes to [tag].
  Future<void> open(String tag) async {
    tagId = normalizeTagId(tag);
    gateway = null;
    notice = null;
    progress = LocateProgress.idle;
    _attempt = 0;
    _fresh = null;
    await _reconnect();
  }

  Future<void> _reconnect() async {
    await _closeChannel();
    if (_disposed) return;
    connection = LocateConnection.connecting;
    _notify();
    String? t;
    try {
      // After a 401: a fresh token (kept for the ?access_token= fallback).
      if (_attempt == 1 && refreshToken != null) _fresh = await refreshToken!();
      t = _attempt >= 1 && _fresh != null ? _fresh : await token();
    } catch (_) {
      t = null;
    }
    if (_disposed) return;
    final useQuery = _attempt >= 2;
    final LocateChannel ch;
    try {
      ch = _connect(useQuery ? withAccessToken(uri, t) : uri, useQuery ? null : t);
    } catch (e) {
      _failed(e);
      return;
    }
    _channel = ch;
    _sub = ch.messages.listen(
      _onMessage,
      onError: (Object e) => identical(_channel, ch) ? _failed(e) : null,
      onDone: () {
        if (identical(_channel, ch) && connection != LocateConnection.closed) {
          connection = LocateConnection.failed;
          notice ??= 'The locating connection closed';
          _notify();
        }
      },
      cancelOnError: true,
    );
    try {
      await ch.ready;
    } catch (e) {
      if (identical(_channel, ch)) _failed(e);
      return;
    }
    if (_disposed || !identical(_channel, ch) || tagId == null) return;
    _send(subscribeFrame(tagId!));
  }

  void _failed(Object e) {
    if (_disposed) return;
    if (isUnauthorizedUpgrade(e) && _attempt < 2) {
      _attempt++;
      unawaited(_reconnect());
      return;
    }
    connection = LocateConnection.failed;
    notice = isUnauthorizedUpgrade(e) ? 'Sign in again to locate tags' : 'Could not connect to the locating service';
    _notify();
  }

  void _send(Map<String, Object> frame) {
    sent.add(frame);
    _channel?.send(jsonEncode(frame));
  }

  /// Sends a command frame ([locateCommand]); progress follows the server frames.
  void sendCommand(Map<String, Object> frame, {bool turnOff = false}) {
    if (_channel == null || connection != LocateConnection.subscribed) return;
    _turningOff = turnOff;
    notice = null;
    progress = const LocateProgress(LocateStage.sending, message: 'Sending…');
    _send(frame);
    _timeout?.cancel();
    _timeout = _schedule(locateAnswerTimeout + const Duration(seconds: 5), () {
      if (!progress.finished) {
        progress = LocateProgress(LocateStage.timeout, reqId: progress.reqId, message: locateTimeoutMessage);
        _notify();
      }
    });
    _notify();
  }

  void _onMessage(Object? data) {
    Map<String, dynamic> f;
    try {
      final body = data is String ? jsonDecode(data) : (data is List<int> ? jsonDecode(utf8.decode(data)) : data);
      if (body is! Map) return;
      f = Map<String, dynamic>.from(body);
    } catch (_) {
      return;
    }
    switch (f['type']) {
      case 'subscribed':
        gateway = LocateTagGateway.fromJson(f);
        connection = LocateConnection.subscribed;
        notice = gateway!.mqttConnected == false ? 'Locating service is offline' : null;
      case 'error':
        final m = f['message'];
        final text = m is String && m.isNotEmpty ? m : 'Locating error';
        if (progress.finished) {
          notice = text;
        } else {
          progress = applyLocateFrame(progress, f);
        }
      case 'unsubscribed':
        connection = LocateConnection.closed;
      default:
        progress = applyLocateFrame(progress, f);
        if (_turningOff && progress.stage == LocateStage.completed) {
          progress = LocateProgress(LocateStage.completed, reqId: progress.reqId, command: 'buzzer', message: 'Buzzer off');
        }
    }
    if (progress.finished) _timeout?.cancel();
    _notify();
  }

  Future<void> _closeChannel() async {
    final ch = _channel;
    _channel = null;
    // Close the socket first; the subscription's cancel may complete later.
    final sub = _sub;
    _sub = null;
    unawaited(sub?.cancel());
    if (ch != null) {
      try {
        await ch.close();
      } catch (_) {}
    }
  }

  /// Closes the socket (the panel closed).
  Future<void> close() async {
    connection = LocateConnection.closed;
    _timeout?.cancel();
    await _closeChannel();
  }

  @override
  void dispose() {
    _disposed = true;
    unawaited(close());
    super.dispose();
  }
}
