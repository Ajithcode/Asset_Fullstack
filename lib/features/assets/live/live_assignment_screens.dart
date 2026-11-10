import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/widgets.dart';
import 'package:asset360_mobile/data/api/api_exception.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_repository.dart';
import 'package:asset360_mobile/data/reference/asset_options.dart';
import 'package:asset360_mobile/data/repositories/live_assets_repository.dart';
import 'package:asset360_mobile/features/assets/live/live_detail_common.dart';
import 'package:asset360_mobile/features/assets/more_actions/more_action_widgets.dart';

/// API mode Assign Custodian, Check Out and Move Asset.
///
/// Assign Custodian and Check Out: POST /api/asset-assignments with AssignTo = the "Employee" value of
/// GET /api/asset-assignments/options and the Master Data employee (record id, employee number, name,
/// designation, department), the way the web's Assign Asset wizard does. The server copies the
/// employee onto the asset's assignment (assignedToUserId / assignedToName / custodianDepartment) and
/// sets its status to "Assigned".
///
/// Check In and Return Asset: the asset's Active assignment (GET /api/asset-assignments?assetRegistrationId=
/// &status=Active) is ended with POST /api/asset-assignments/{id}/return {assetRegistrationIds: [asset],
/// returnDate, remarks}; the condition and its note go into remarks. The server gives the asset back the
/// status it had before (or "Available").
///
/// Move Asset: PUT /api/asset-registrations/{id} with the complete locationAssignment section (the
/// new Master Data location + the asset's current assignment unchanged).

/// Status the Asset service gives an asset while it is in an active assignment.
const assignedStatus = 'Assigned';

/// The chosen employee and their details card.
class _EmployeePicker extends ConsumerWidget {
  const _EmployeePicker({required this.label, required this.employeeId, required this.onChanged, this.error});

  final String label;
  final String? employeeId;
  final ValueChanged<String> onChanged;
  final String? error;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final e = employeeById(ref, employeeId);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        formField(
          label,
          required: true,
          PersonField(
            personId: employeeId,
            title: 'Select $label',
            hint: 'Search by name or employee ID',
            error: error,
            onChanged: onChanged,
          ),
        ),
        if (e != null)
          Padding(
            padding: const EdgeInsets.only(bottom: 14),
            child: SectionCard(
              title: 'Employee Details',
              child: Column(
                children: [
                  InfoRow(label: 'Employee ID', value: e.employeeCode.isEmpty ? '—' : e.employeeCode, labelWidth: 96),
                  InfoRow(label: 'Designation', value: e.designation ?? '—', labelWidth: 96),
                  InfoRow(label: 'Department', value: e.department?.name ?? '—', labelWidth: 96),
                  if (e.phone != null) InfoRow(label: 'Phone', value: e.phone!, labelWidth: 96),
                ],
              ),
            ),
          ),
      ],
    );
  }
}

/// The Master Data employee with record id [id] (null while loading or unknown). [watch] = false
/// in tap handlers.
MdEmployee? employeeById(WidgetRef ref, String? id, {bool watch = true}) {
  if (id == null) return null;
  final list = watch ? ref.watch(mdEmployeesProvider) : ref.read(mdEmployeesProvider);
  return list.value?.where((e) => e.id == id).firstOrNull;
}

/// Builds the request for [employee] (all its Master Data facts are copied, as the web does).
/// [assignTo] = the server's "Employee" target ([AssignmentOptions.employee]).
CreateAssetAssignmentRequest assignmentFor(
  AssetRegistration r,
  MdEmployee employee, {
  required String assignTo,
  required String type,
  required DateTime from,
  DateTime? to,
  String? purpose,
  String? remarks,
}) => CreateAssetAssignmentRequest(
  assetRegistrationIds: [r.assetRegistrationId],
  assignTo: assignTo,
  assignmentType: type,
  fromDate: from,
  toDate: to,
  employeeId: employee.id,
  employeeCode: blankToNull(employee.employeeCode),
  employeeName: employee.name,
  designation: employee.designation,
  departmentId: employee.department?.id,
  departmentName: employee.department?.name,
  purpose: purpose,
  remarks: remarks,
);

/// Mixin for the two assignment forms: sends the request and maps the server's field errors.
mixin _AssignmentSubmit<T extends ConsumerStatefulWidget> on ConsumerState<T> {
  final errors = <String, String>{};
  bool busy = false;

  /// [request] gets the server's AssignTo value for an employee (from /options).
  Future<void> submit(
    AssetRegistration r,
    CreateAssetAssignmentRequest Function(String assignTo) request,
    String done,
  ) async {
    final assignTo = ref.read(assignmentOptionsProvider).value?.employee;
    if (assignTo == null) {
      context.showSnack('The assignment options are not loaded; tap Retry first');
      return;
    }
    setState(() => busy = true);
    try {
      final a = await ref.read(assetsApiProvider).createAssignment(request(assignTo));
      invalidateLiveAsset(ref, r.assetRegistrationId);
      ref
        ..invalidate(liveAssetListProvider)
        ..invalidate(assetSummaryProvider);
      if (mounted) finishAction(context, '$done${a.assignmentNumber.isEmpty ? '' : ' (${a.assignmentNumber})'}');
    } catch (e) {
      if (!mounted) return;
      final err = ApiException.from(e);
      setState(() {
        busy = false;
        errors.addAll(err.fieldErrors);
      });
      context.showApiError(err, onRetry: () => submit(r, request, done));
    }
  }

  /// Loading / failed state of GET /api/asset-assignments/options (AssignTo values); null when the
  /// server's "Employee" target is known.
  Widget? assignToStatus() {
    final v = ref.watch(assignmentOptionsProvider);
    void retry() => ref.invalidate(assignmentOptionsProvider);
    final Widget? status;
    if (v.hasError && !v.isLoading) {
      status = InlineRetry(
        text: 'Could not load the assignment options: ${ApiException.from(v.error!).message}',
        onRetry: retry,
        error: true,
      );
    } else if (!v.hasValue) {
      status = const Align(
        alignment: Alignment.centerLeft,
        child: SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2)),
      );
    } else if (v.requireValue.employee == null) {
      status = InlineRetry(
        text:
            'The server does not offer assigning to an employee '
            '(Assign To: ${v.requireValue.assignTo.isEmpty ? 'none' : v.requireValue.assignTo.join(', ')})',
        onRetry: retry,
        error: true,
      );
    } else {
      status = null;
    }
    return status == null ? null : Padding(padding: const EdgeInsets.only(bottom: 14), child: status);
  }

  /// "In assignment ASG-… already" notice when the asset is assigned.
  Widget? assignedNotice(AssetRegistration r) {
    if (r.basicInformation.status?.trim().toLowerCase() != assignedStatus.toLowerCase()) return null;
    final who = r.locationAssignment.assignment.assignedToName;
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: IconLine(
        icon: Icons.info_outline_rounded,
        text:
            '${r.title} is already assigned${who == null ? '' : ' to $who'}. '
            'The server accepts one active assignment per asset.',
      ),
    );
  }
}

// ---------------------------------------------------------------- Assign Custodian

class LiveAssignCustodianScreen extends ConsumerStatefulWidget {
  const LiveAssignCustodianScreen({super.key, required this.assetId});

  final String? assetId;

  @override
  ConsumerState<LiveAssignCustodianScreen> createState() => _LiveAssignCustodianScreenState();
}

class _LiveAssignCustodianScreenState extends ConsumerState<LiveAssignCustodianScreen>
    with _AssignmentSubmit<LiveAssignCustodianScreen> {
  String? _employeeId;
  DateTime? _from = liveToday();
  DateTime? _to;
  final _notes = TextEditingController();

  @override
  void dispose() {
    _notes.dispose();
    super.dispose();
  }

  void _save(AssetRegistration r) {
    errors.clear();
    final e = employeeById(ref, _employeeId, watch: false);
    if (e == null) errors['Employee.EmployeeId'] = 'Choose the custodian';
    if (_from == null) errors['FromDate'] = 'Choose the effective date';
    if (_from != null && _to != null && _to!.isBefore(_from!)) {
      errors['ToDate'] = 'The end date cannot be before the start';
    }
    setState(() {});
    if (errors.isNotEmpty) return;
    final employee = e!;
    submit(
      r,
      (assignTo) => assignmentFor(
        r,
        employee,
        assignTo: assignTo,
        type: CreateAssetAssignmentRequest.custodian,
        from: _from!,
        to: _to,
        remarks: blankToNull(_notes.text),
      ),
      '${r.title}: custodian set to ${employee.name}',
    );
  }

  @override
  Widget build(BuildContext context) => LiveActionPage(
    action: MoreAction.assignCustodian,
    assetId: widget.assetId,
    button: (r) => busyButton(busy: busy, onPressed: () => _save(r), label: 'Assign Custodian', icon: Icons.person_add),
    builder: (context, r) {
      final current = r.locationAssignment.assignment.assignedToName;
      return [
        ?assignToStatus(),
        ?assignedNotice(r),
        Padding(
          padding: const EdgeInsets.only(bottom: 14),
          child: Text('Current custodian: ${current ?? 'none'}', style: AppText.caption),
        ),
        _EmployeePicker(
          label: 'Custodian',
          employeeId: _employeeId,
          error: errors['Employee.EmployeeId'],
          onChanged: (id) => setState(() {
            _employeeId = id;
            errors.remove('Employee.EmployeeId');
          }),
        ),
        formField(
          'Effective Date',
          required: true,
          DateBox(
            value: _from,
            error: errors['FromDate'],
            onPicked: (d) => setState(() {
              _from = d;
              errors.remove('FromDate');
            }),
          ),
        ),
        formField(
          'End Date (empty = open ended)',
          DateBox(
            value: _to,
            firstDate: _from,
            error: errors['ToDate'],
            hint: 'Open ended',
            onPicked: (d) => setState(() {
              _to = d;
              errors.remove('ToDate');
            }),
          ),
        ),
        formField(
          'Notes',
          TextField(
            controller: _notes,
            maxLines: 3,
            maxLength: 500,
            textCapitalization: TextCapitalization.sentences,
            style: inputStyle,
            decoration: InputDecoration(hintText: 'Why this person is responsible', errorText: errors['Remarks']),
          ),
        ),
      ];
    },
  );
}

// ---------------------------------------------------------------- Check Out / Check In

class LiveCheckInOutScreen extends ConsumerStatefulWidget {
  const LiveCheckInOutScreen({super.key, required this.assetId, this.initialTab});

  final String? assetId;

  /// 0 = Check Out, 1 = Check In; null = Check In when the asset has an Active assignment, else Check Out.
  final int? initialTab;

  @override
  ConsumerState<LiveCheckInOutScreen> createState() => _LiveCheckInOutScreenState();
}

class _LiveCheckInOutScreenState extends ConsumerState<LiveCheckInOutScreen>
    with _AssignmentSubmit<LiveCheckInOutScreen>, _ReturnSubmit<LiveCheckInOutScreen> {
  /// Null until chosen (see [LiveCheckInOutScreen.initialTab]).
  late int? _tab = widget.initialTab?.clamp(0, 1);

  int _tabOf(AssetRegistration r) =>
      _tab ?? (ref.watch(liveActiveAssignmentProvider(r.assetRegistrationId)).value == null ? 0 : 1);
  String? _employeeId;
  DateTime? _return;
  final _purpose = TextEditingController();
  final _notes = TextEditingController();

  @override
  void dispose() {
    _purpose.dispose();
    _notes.dispose();
    super.dispose();
  }

  void _checkOut(AssetRegistration r) {
    errors.clear();
    final e = employeeById(ref, _employeeId, watch: false);
    if (e == null) errors['Employee.EmployeeId'] = 'Choose who takes the asset';
    setState(() {});
    if (errors.isNotEmpty) return;
    final employee = e!;
    submit(
      r,
      (assignTo) => assignmentFor(
        r,
        employee,
        assignTo: assignTo,
        type: CreateAssetAssignmentRequest.checkOut,
        from: liveToday(),
        to: _return,
        purpose: blankToNull(_purpose.text),
        remarks: blankToNull(_notes.text),
      ),
      '${r.title} checked out to ${employee.name}${_return == null ? '' : ' until ${liveDate(_return)}'}',
    );
  }

  @override
  Widget build(BuildContext context) => LiveActionPage(
    action: MoreAction.checkInOut,
    assetId: widget.assetId,
    button: (r) => _tabOf(r) == 0
        ? busyButton(busy: busy, onPressed: () => _checkOut(r), label: 'Check Out', icon: Icons.logout_rounded)
        : returnButton(r, label: 'Check In', verb: 'checked in', icon: Icons.login_rounded),
    builder: (context, r) => [
      SegmentTabs(
        tabs: const [SegmentTab('Check Out'), SegmentTab('Check In')],
        selected: _tabOf(r),
        onChanged: (i) => setState(() {
          _tab = i;
          errors.clear();
          returnErrors.clear();
        }),
      ),
      const SizedBox(height: 14),
      if (_tabOf(r) == 1)
        ...returnFields(r)
      else ...[
        ?assignToStatus(),
        ?assignedNotice(r),
        _EmployeePicker(
          label: 'Assign To',
          employeeId: _employeeId,
          error: errors['Employee.EmployeeId'],
          onChanged: (id) => setState(() {
            _employeeId = id;
            errors.remove('Employee.EmployeeId');
          }),
        ),
        formField(
          'Expected Return Date',
          DateBox(
            value: _return,
            firstDate: liveToday(),
            hint: 'Open ended',
            error: errors['ToDate'],
            onPicked: (d) => setState(() {
              _return = d;
              errors.remove('ToDate');
            }),
          ),
        ),
        formField(
          'Purpose',
          TextField(
            controller: _purpose,
            maxLength: 500,
            textCapitalization: TextCapitalization.sentences,
            style: inputStyle,
            decoration: InputDecoration(hintText: 'e.g. Site visit, project work', errorText: errors['Purpose']),
          ),
        ),
        formField(
          'Notes',
          TextField(
            controller: _notes,
            maxLines: 3,
            maxLength: 500,
            textCapitalization: TextCapitalization.sentences,
            style: inputStyle,
            decoration: InputDecoration(hintText: 'Anything to know', errorText: errors['Remarks']),
          ),
        ),
      ],
    ],
  );
}

// ---------------------------------------------------------------- Check In / Return (End Assignment)

/// Shown when the asset has no Active assignment to end.
const notAssignedMessage = "This asset isn't assigned or checked out.";

/// Check In and Return Asset: the asset's current assignment, return date, condition and a note.
mixin _ReturnSubmit<T extends ConsumerStatefulWidget> on ConsumerState<T> {
  DateTime? returnDate = liveToday();
  String? returnCondition;
  final returnNote = TextEditingController();
  final returnErrors = <String, String>{};
  bool returning = false;

  @override
  void dispose() {
    returnNote.dispose();
    super.dispose();
  }

  /// "Condition at return: Good. Scratch on the lid" (null when nothing was entered).
  String? get returnRemarks {
    final note = blankToNull(returnNote.text);
    final parts = [if (returnCondition != null) 'Condition at return: $returnCondition', ?note];
    return parts.isEmpty ? null : parts.join('. ');
  }

  bool _validateReturn() {
    returnErrors.clear();
    if (returnCondition == null) returnErrors['condition'] = 'Choose the condition';
    final d = returnDate;
    if (d == null) {
      returnErrors['ReturnDate'] = 'Choose the return date';
    } else if (d.isAfter(liveToday())) {
      returnErrors['ReturnDate'] = 'The return date cannot be in the future';
    }
    setState(() {});
    return returnErrors.isEmpty;
  }

  /// Finds the Active assignment again (it may have changed since the screen opened) and ends it for [r].
  Future<void> submitReturn(AssetRegistration r, String verb) async {
    if (!_validateReturn()) return;
    final id = r.assetRegistrationId;
    setState(() => returning = true);
    try {
      final api = ref.read(assetsApiProvider);
      final active = await api.activeAssignment(id);
      if (!mounted) return;
      if (active == null) {
        ref.invalidate(liveActiveAssignmentProvider(id));
        setState(() => returning = false);
        context.showSnack(notAssignedMessage);
        return;
      }
      await api.returnAssignment(
        active.id,
        ReturnAssetAssignmentRequest(assetRegistrationIds: [id], returnDate: returnDate, remarks: returnRemarks),
      );
      invalidateLiveAsset(ref, id);
      ref
        ..invalidate(liveAssetListProvider)
        ..invalidate(recentLiveAssetsProvider)
        ..invalidate(assetSummaryProvider);
      final from = active.assignedToName;
      if (mounted) {
        finishAction(
          context,
          '${r.title} $verb${from == null ? '' : ' from $from'} '
          '(${active.assignmentNumber.isEmpty ? 'assignment ended' : '${active.assignmentNumber} returned'})',
        );
      }
    } catch (e) {
      if (!mounted) return;
      final err = ApiException.from(e);
      setState(() {
        returning = false;
        returnErrors.addAll(err.fieldErrors);
      });
      final status = err.fieldErrors['Status'];
      if (status != null) {
        // "Only an active assignment can be returned": someone ended it meanwhile.
        ref.invalidate(liveActiveAssignmentProvider(id));
        context.showSnack(status);
      } else {
        context.showApiError(err, onRetry: () => submitReturn(r, verb));
      }
    }
  }

  /// The confirm button; with no Active assignment it only explains why.
  Widget returnButton(AssetRegistration r, {required String label, required String verb, required IconData icon}) {
    final active = ref.watch(liveActiveAssignmentProvider(r.assetRegistrationId));
    if (active case AsyncData(value: null)) {
      return FilledButton.icon(
        onPressed: () => context.showSnack(notAssignedMessage),
        icon: Icon(icon),
        label: Text(label),
      );
    }
    return busyButton(busy: returning, onPressed: () => submitReturn(r, verb), label: label, icon: icon);
  }

  /// Current assignment card + Return Date, Condition and Condition Note.
  List<Widget> returnFields(AssetRegistration r) {
    final id = r.assetRegistrationId;
    final error = returnErrors['condition'];
    return [
      Padding(
        padding: const EdgeInsets.only(bottom: 14),
        child: SectionCard(
          title: 'Current Assignment',
          child: LiveAsync<AssetAssignmentRecord?>(
            value: ref.watch(liveActiveAssignmentProvider(id)),
            onRetry: () => ref.invalidate(liveActiveAssignmentProvider(id)),
            data: (a) => a == null
                ? const IconLine(icon: Icons.info_outline_rounded, text: notAssignedMessage)
                : LiveInfoRows([
                    ('Assignment', a.assignmentNumber),
                    ('Assigned To', a.assignedToName),
                    ('Type', a.assignmentType),
                    ('Since', liveDate(a.fromDate)),
                    ('Due Back', a.isOpenEnded ? 'Open ended' : liveDate(a.toDate)),
                    ('Purpose', a.purpose),
                  ]),
          ),
        ),
      ),
      formField(
        'Return Date',
        required: true,
        DateBox(
          value: returnDate,
          error: returnErrors['ReturnDate'],
          onPicked: (d) => setState(() {
            returnDate = d;
            returnErrors.remove('ReturnDate');
          }),
        ),
      ),
      formField(
        'Condition',
        required: true,
        Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final c in AssetOptions.conditions)
                  IconFilterChip(
                    label: c,
                    selected: returnCondition == c,
                    onTap: () => setState(() {
                      returnCondition = c;
                      returnErrors.remove('condition');
                    }),
                  ),
              ],
            ),
            if (error != null)
              Padding(
                padding: const EdgeInsets.only(top: 6),
                child: Text(error, style: AppText.caption.copyWith(color: AppColors.red)),
              ),
          ],
        ),
      ),
      formField(
        'Condition Note',
        TextField(
          controller: returnNote,
          maxLines: 3,
          maxLength: 500,
          textCapitalization: TextCapitalization.sentences,
          style: inputStyle,
          decoration: InputDecoration(hintText: 'Anything missing or damaged?', errorText: returnErrors['Remarks']),
        ),
      ),
    ];
  }
}

/// API mode Return Asset (/work/movement/return): ends the asset's Active assignment.
class LiveReturnAssetScreen extends ConsumerStatefulWidget {
  const LiveReturnAssetScreen({super.key, required this.assetId});

  final String? assetId;

  @override
  ConsumerState<LiveReturnAssetScreen> createState() => _LiveReturnAssetScreenState();
}

class _LiveReturnAssetScreenState extends ConsumerState<LiveReturnAssetScreen>
    with _ReturnSubmit<LiveReturnAssetScreen> {
  @override
  Widget build(BuildContext context) => LiveActionPage.titled(
    title: 'Return Asset',
    subtitle: 'End the asset\'s assignment and take it back',
    assetId: widget.assetId,
    button: (r) => returnButton(r, label: 'Return Asset', verb: 'returned', icon: Icons.undo_rounded),
    builder: (context, r) => returnFields(r),
  );
}

// ---------------------------------------------------------------- Move Asset

/// API mode Move Asset: the new location from the Master Data tree (cascading Country → Room or a
/// scanned location tag), saved with PUT /api/asset-registrations/{id}. The server replaces the
/// whole Location & Assignment section, so the section is sent complete: [movedTo].
/// The backend has no place for a move reason or a movement history, so none is asked.
class LiveMoveScreen extends ConsumerStatefulWidget {
  const LiveMoveScreen({super.key, required this.assetId});

  final String? assetId;

  @override
  ConsumerState<LiveMoveScreen> createState() => _LiveMoveScreenState();
}

/// [r] moved to [to]: a copy whose location is the picked Master Data path (ids, names and path of
/// every level; levels below the pick cleared). The project id and the whole assignment (custodian,
/// department, team, ownership, cost center, dates) stay as they are. The GPS position belonged to
/// the old place, so it is cleared.
AssetRegistration movedTo(AssetRegistration r, MdLocationPath to) {
  final moved = r.copy();
  final l = moved.locationAssignment.location;
  to.writeTo(l);
  l
    ..latitude = null
    ..longitude = null;
  return moved;
}

class _LiveMoveScreenState extends ConsumerState<LiveMoveScreen> {
  MdLocationPath _to = MdLocationPath.empty;
  String? _error;
  bool _busy = false;

  Future<void> _move(AssetRegistration r) async {
    final ok = (_to.level?.index ?? -1) >= MdLocationLevel.floor.index;
    setState(() => _error = ok ? null : 'Choose at least the country, site, building and floor');
    if (!ok) return;
    setState(() => _busy = true);
    try {
      final saved = await LiveAssetEditor(ref).save(r, movedTo(r, _to));
      if (!mounted) return;
      if (saved == null) {
        setState(() => _busy = false);
        context.showSnack('${r.title} is already at ${_to.label}');
        return;
      }
      finishAction(context, '${r.title} moved to ${_to.label}');
    } catch (e) {
      if (!mounted) return;
      final err = ApiException.from(e);
      // "LocationAssignment.Location.FloorId" … under the location; any other field (the whole
      // asset is re-checked) as a message that points to Edit Asset.
      final location = err.fieldErrors.entries.where((f) => f.key.startsWith('LocationAssignment.')).firstOrNull;
      final other = err.fieldErrors.entries.where((f) => !f.key.startsWith('LocationAssignment.')).firstOrNull;
      setState(() {
        _busy = false;
        _error = location?.value;
      });
      if (other != null && location == null) {
        context.showSnack('${other.value} · fix it with Edit Asset, then move again');
      } else {
        context.showApiError(err, onRetry: () => _move(r));
      }
    }
  }

  @override
  Widget build(BuildContext context) => LiveActionPage.titled(
    title: 'Move Asset',
    subtitle: 'Move the asset to another location',
    assetId: widget.assetId,
    button: (r) => busyButton(busy: _busy, onPressed: () => _move(r), label: 'Move', icon: Icons.swap_horiz_rounded),
    builder: (context, r) => [
      const FieldLabel('Current Location'),
      Padding(
        padding: const EdgeInsets.only(bottom: 14),
        child: IconLine(
          icon: Icons.location_on_outlined,
          text: r.locationAssignment.location.locationPath ?? 'No location yet',
        ),
      ),
      const FieldLabel('New Location', required: true),
      MasterLocationField(value: _to, errorText: _error, onChanged: (v) => setState(() => _to = v)),
      const SizedBox(height: 14),
      Text(
        'The custodian, department and other assignment details stay as they are.',
        style: AppText.caption.copyWith(color: AppColors.textSecondary),
      ),
    ],
  );
}
