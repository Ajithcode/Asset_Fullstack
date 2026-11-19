import 'package:asset360_mobile/data/api/tracking/rtl_api.dart' show normalizeTagId;

export 'package:asset360_mobile/data/api/tracking/rtl_api.dart' show normalizeTagId;

/// Locate (blink LED / beep buzzer) rules, from docs/api/MOBILE-API-HANDOFF.md section 2.2 and
/// docs/backend_ref/Services/Tracking/Api/A360.Tracking.Api/Locating/LocateCommands.cs. Pure Dart:
/// ranges, defaults, the exact JSON frames and the reqId status flow.

/// LED · Buzzer · Both.
enum LocateMode {
  led('LED'),
  buzzer('Buzzer'),
  both('Both');

  const LocateMode(this.label);
  final String label;

  bool get usesLed => this != buzzer;
  bool get usesBuzzer => this != led;
}

/// An inclusive integer range of one option.
class LocateRange {
  const LocateRange(this.min, this.max);

  final int min, max;

  String get hint => 'Min $min, max $max';
  String get error => 'Enter a value from $min to $max';

  /// Null when [text] is a whole number inside the range, else [error].
  String? check(String text) {
    final v = int.tryParse(text.trim());
    return v == null || v < min || v > max ? error : null;
  }
}

/// Every option the server accepts (LocateCommands.cs).
abstract final class LocateLimits {
  static const ledOn = LocateRange(1, 65535);
  static const ledOff = LocateRange(1, 65535);
  static const ledCycles = LocateRange(1, 65535);
  static const brightness = LocateRange(1, 100);
  static const beep = LocateRange(20, 65535);
  static const pause = LocateRange(20, 65535);
  static const beepCycles = LocateRange(0, 65535);
  static const tone = LocateRange(0, 5);

  /// The MBT02 tag only has a red LED (colour 3).
  static const redColor = 3;
}

/// LED options (colour is fixed red).
class LedOptions {
  const LedOptions({this.onMs = 300, this.offMs = 300, this.cycles = 10, this.brightness = 100});

  final int onMs, offMs, cycles, brightness;

  /// How long the LED keeps blinking: cycles × (on + off).
  Duration get runTime => Duration(milliseconds: cycles * (onMs + offMs));

  Map<String, Object> toJson() => {
    'color': LocateLimits.redColor,
    'onMs': onMs,
    'offMs': offMs,
    'cycles': cycles,
    'brightness': brightness,
  };
}

/// Buzzer options ([cycles] 0 = off; [level] 0 = the tag's default tone).
class BuzzerOptions {
  const BuzzerOptions({this.beepMs = 300, this.pauseMs = 300, this.cycles = 5, this.level = 0});

  final int beepMs, pauseMs, cycles, level;

  Duration get runTime => Duration(milliseconds: cycles * (beepMs + pauseMs));

  Map<String, Object> toJson() => {'beepMs': beepMs, 'pauseMs': pauseMs, 'cycles': cycles, 'level': level};
}

/// A quick pattern that fills the LED fields.
typedef LedPattern = ({String label, int onMs, int offMs, int cycles});

const ledPatterns = <LedPattern>[
  (label: 'Fast blink', onMs: 200, offMs: 200, cycles: 25),
  (label: 'Slow blink', onMs: 1000, offMs: 1000, cycles: 15),
  (label: 'Steady on 5 min', onMs: 60000, offMs: 1, cycles: 5),
];

/// The command frame for [mode]: "led" / "buzzer" with flat options, "both" with nested `led` and
/// `buzzer` objects. [tagId] is normalised.
Map<String, Object> locateCommand(LocateMode mode, String tagId, {LedOptions led = const LedOptions(), BuzzerOptions buzzer = const BuzzerOptions()}) {
  final tag = normalizeTagId(tagId);
  return switch (mode) {
    LocateMode.led => {'type': 'led', 'tagid': tag, ...led.toJson()},
    LocateMode.buzzer => {'type': 'buzzer', 'tagid': tag, ...buzzer.toJson()},
    LocateMode.both => {'type': 'both', 'tagid': tag, 'led': led.toJson(), 'buzzer': buzzer.toJson()},
  };
}

/// "Turn off": there is no stop command; buzzer beep count 0 silences the buzzer. The LED cannot be
/// switched off (cycles ≥ 1) and stops by itself after its run time.
Map<String, Object> turnOffCommand(String tagId) => {'type': 'buzzer', 'tagid': normalizeTagId(tagId), 'cycles': 0};

Map<String, Object> subscribeFrame(String tagId) => {'type': 'subscribe', 'tagid': normalizeTagId(tagId)};

/// "6.0 s" / "5.0 min".
String runTimeText(Duration d) {
  final s = d.inMilliseconds / 1000;
  return s < 120 ? '${s.toStringAsFixed(1)} s' : '${(s / 60).toStringAsFixed(1)} min';
}

/// "LED runs 6.0 s · buzzer runs 3.0 s" for the parts [mode] sends.
String runTimeLine(LocateMode mode, LedOptions led, BuzzerOptions buzzer) => [
  if (mode.usesLed) 'LED runs ${runTimeText(led.runTime)}',
  if (mode.usesBuzzer) 'buzzer runs ${runTimeText(buzzer.runTime)}',
].join(' · ');

// ------------------------------------------------------------------ progress

/// Where a sent command is.
enum LocateStage { idle, sending, sent, accepted, completed, failed, timeout, error }

/// The progress line under the buttons.
class LocateProgress {
  const LocateProgress(this.stage, {this.reqId, this.message = '', this.command});

  static const idle = LocateProgress(LocateStage.idle);

  final LocateStage stage;
  final int? reqId;
  final String message;

  /// led | buzzer | both (from the server frames).
  final String? command;

  bool get finished => const {
    LocateStage.idle,
    LocateStage.completed,
    LocateStage.failed,
    LocateStage.timeout,
    LocateStage.error,
  }.contains(stage);

  bool get success => stage == LocateStage.completed;
  bool get problem => const {LocateStage.failed, LocateStage.timeout, LocateStage.error}.contains(stage);
}

/// Local fallback when the server's own 60 s timeout frame never arrives.
const locateAnswerTimeout = Duration(seconds: 60);
const locateTimeoutMessage = 'No answer from the gateway in time';

/// Applies one server frame to [p] (the command this panel sent). The first `commandSent` after
/// sending gives the reqId; later frames with another reqId are ignored.
LocateProgress applyLocateFrame(LocateProgress p, Map<String, dynamic> f) {
  final type = f['type'];
  final reqId = f['reqId'] is num ? (f['reqId'] as num).toInt() : int.tryParse('${f['reqId']}');
  final command = f['command'] is String ? f['command'] as String : p.command;
  switch (type) {
    case 'error':
      if (p.stage == LocateStage.idle) return p;
      final m = f['message'];
      return LocateProgress(LocateStage.error, reqId: p.reqId, message: m is String && m.isNotEmpty ? m : 'Command failed');
    case 'commandSent':
      if (p.stage != LocateStage.sending && reqId != p.reqId) return p;
      final gw = f['gatewayid'];
      return LocateProgress(
        LocateStage.sent,
        reqId: reqId,
        command: command,
        message: 'Sent to gateway ${gw is String && gw.isNotEmpty ? gw : '—'}',
      );
    case 'commandStatus':
      if (reqId == null || reqId != p.reqId) return p;
      final status = '${f['status']}'.toLowerCase();
      final m = f['message'] is String ? f['message'] as String : '';
      return switch (status) {
        'accepted' => LocateProgress(LocateStage.accepted, reqId: reqId, command: command, message: 'Gateway accepted'),
        'completed' => LocateProgress(
          LocateStage.completed,
          reqId: reqId,
          command: command,
          message: switch (command) {
            'buzzer' => 'Tag is beeping',
            'both' => 'Tag is blinking and beeping',
            _ => 'Tag is blinking',
          },
        ),
        'failed' => LocateProgress(
          LocateStage.failed,
          reqId: reqId,
          command: command,
          message: m.isNotEmpty ? m : 'The tag did not respond',
        ),
        'timeout' => LocateProgress(LocateStage.timeout, reqId: reqId, command: command, message: locateTimeoutMessage),
        _ => p,
      };
    default:
      return p;
  }
}
