import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:asset360_mobile/app/theme/app_colors.dart';
import 'package:asset360_mobile/app/theme/app_text.dart';
import 'package:asset360_mobile/core/widgets/dialogs.dart';
import 'package:asset360_mobile/core/widgets/form_fields.dart';
import 'package:asset360_mobile/core/widgets/live_states.dart';
import 'package:asset360_mobile/data/api/api_config.dart';
import 'package:asset360_mobile/data/api/master_data/master_data_repository.dart';
import 'package:asset360_mobile/data/models/person.dart';
import 'package:asset360_mobile/data/repositories/session_repository.dart';

/// API mode: a Master Data employee as a [Person]. [Person.id] is the employee RECORD id, which
/// the Asset service stores as `assignedToUserId` (its assign wizard and bulk assignment do the
/// same); the employee number ("EMP-0042") is only shown.
Person personOfEmployee(MdEmployee e) =>
    Person(id: e.id, name: e.name, role: e.designation ?? 'Employee', department: e.department?.name, phone: e.phone);

/// API mode: the employee with record id [id] as a person (null while loading or unknown).
final masterPersonProvider = Provider.family<Person?, String?>((ref, id) {
  if (id == null) return null;
  final e = ref.watch(mdEmployeesProvider).value?.where((e) => e.id == id).firstOrNull;
  return e == null ? null : personOfEmployee(e);
});

/// The person with [id] in the current mode (mock people, or Master Data employees).
Person? personOf(WidgetRef ref, String? id) =>
    apiMode ? ref.watch(masterPersonProvider(id)) : ref.watch(personByIdProvider(id));

/// Bottom sheet with a searchable people list (name, role, department, employee ID).
/// Returns the chosen person's ID. API mode: Master Data /employees (active), with loading,
/// error + Retry and "No employees on the server yet".
///
/// [department] (API mode): list only that department's employees, like the web (employees?departmentId=),
/// with a "Show all employees" switch to see everyone.
Future<String?> showPersonPicker(
  BuildContext context, {
  String title = 'Select Person',
  String? selectedId,
  String? department,
}) {
  return showModalBottomSheet<String>(
    context: context,
    isScrollControlled: true,
    builder: (_) => _PersonPickerSheet(title: title, selectedId: selectedId, department: department),
  );
}

/// API mode: whether [e] belongs to the department named [department] (by record id once the department
/// is listed, else by name).
bool employeeInDepartment(MdEmployee e, String department, List<MdDepartment> departments) {
  final dept = departments.where((d) => d.name.trim().toLowerCase() == department.trim().toLowerCase()).firstOrNull;
  final ref = e.department;
  if (dept != null && ref?.id != null) return ref!.id == dept.id;
  return (ref?.name ?? '').trim().toLowerCase() == department.trim().toLowerCase();
}

/// Employee ID shown for a person ("EMP-REX").
String employeeId(Person p) => 'EMP-${p.id.toUpperCase()}';

class _PersonPickerSheet extends ConsumerStatefulWidget {
  const _PersonPickerSheet({required this.title, this.selectedId, this.department});

  final String title;
  final String? selectedId;
  final String? department;

  @override
  ConsumerState<_PersonPickerSheet> createState() => _PersonPickerSheetState();
}

class _PersonPickerSheetState extends ConsumerState<_PersonPickerSheet> {
  String _query = '';
  bool _showAll = false;

  @override
  Widget build(BuildContext context) {
    final q = _query.trim().toLowerCase();
    final employees = apiMode ? ref.watch(mdEmployeesProvider) : null;
    final status = employees == null
        ? null
        : serverListStatus(employees, what: 'employees', onRetry: () => ref.invalidate(mdEmployeesProvider));
    final codes = {for (final e in employees?.value ?? const <MdEmployee>[]) e.id: e.employeeCode};
    String idOf(Person p) => apiMode ? (codes[p.id] ?? '') : employeeId(p);
    final dept = apiMode ? widget.department : null;
    final departments = dept == null ? const <MdDepartment>[] : ref.watch(mdDepartmentsProvider).value ?? const [];
    final all = employees == null
        ? ref.watch(peopleProvider)
        : [
            for (final e in employees.value ?? const <MdEmployee>[])
              if (dept == null || _showAll || employeeInDepartment(e, dept, departments)) personOfEmployee(e),
          ];
    final people = all
        .where(
          (p) =>
              q.isEmpty ||
              p.name.toLowerCase().contains(q) ||
              p.role.toLowerCase().contains(q) ||
              (p.department?.toLowerCase().contains(q) ?? false) ||
              idOf(p).toLowerCase().contains(q),
        )
        .toList();
    return SafeArea(
      child: SizedBox(
        height: MediaQuery.sizeOf(context).height * .75,
        child: Padding(
          padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 0, 20, 0),
                child: SheetTitle(widget.title, subtitle: '${people.length} people'),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                child: TextField(
                  autofocus: false,
                  style: inputStyle,
                  decoration: const InputDecoration(
                    hintText: 'Search name, role, department or ID',
                    prefixIcon: Icon(Icons.search_rounded),
                  ),
                  onChanged: (v) => setState(() => _query = v),
                ),
              ),
              if (dept != null)
                SwitchListTile(
                  dense: true,
                  contentPadding: const EdgeInsets.symmetric(horizontal: 20),
                  title: Text(_showAll ? 'Showing all employees' : 'Showing $dept employees', style: AppText.caption),
                  subtitle: const Text('Show all employees'),
                  value: _showAll,
                  onChanged: (v) => setState(() => _showAll = v),
                ),
              if (status != null) Padding(padding: const EdgeInsets.symmetric(horizontal: 16), child: status),
              Expanded(
                child: status != null
                    ? const SizedBox.shrink()
                    : people.isEmpty
                    ? Center(child: Text('No one matches "$_query"', style: AppText.secondary))
                    : ListView.builder(
                        itemCount: people.length,
                        itemBuilder: (_, i) {
                          final p = people[i];
                          final selected = p.id == widget.selectedId;
                          return ListTile(
                            leading: PersonAvatar(person: p),
                            title: Text(p.name, style: AppText.bodyStrong),
                            subtitle: Text(
                              [p.role, ?p.department, if (idOf(p).isNotEmpty) idOf(p)].join(' · '),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: AppText.caption,
                            ),
                            trailing: selected ? Icon(Icons.check_rounded, color: AppColors.primary) : null,
                            onTap: () => Navigator.pop(context, p.id),
                          );
                        },
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Violet circle with the person's initials.
class PersonAvatar extends StatelessWidget {
  const PersonAvatar({super.key, required this.person, this.size = 40});

  final Person person;
  final double size;

  @override
  Widget build(BuildContext context) => CircleAvatar(
    radius: size / 2,
    backgroundColor: AppColors.primarySoft,
    child: Text(
      person.initials,
      style: TextStyle(color: AppColors.primary, fontWeight: FontWeight.w700, fontSize: size * .36),
    ),
  );
}

/// Form field that shows the chosen person (or a search hint) and opens [showPersonPicker].
class PersonField extends ConsumerWidget {
  const PersonField({
    super.key,
    required this.personId,
    required this.onChanged,
    this.hint = 'Search people',
    this.error,
    this.title = 'Select Person',
    this.fallbackLabel,
    this.department,
  });

  /// API mode: list only this department's employees (with a "Show all employees" switch).
  final String? department;

  /// Shown when [personId] is set but not in the list (yet), e.g. a saved assignedToName.
  final String? fallbackLabel;
  final String? personId;
  final ValueChanged<String> onChanged;
  final String hint;
  final String? error;
  final String title;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final p = personOf(ref, personId);
    final label = p?.labelWithRole ?? (personId == null ? null : fallbackLabel);
    return InkWell(
      borderRadius: BorderRadius.circular(10),
      onTap: () async {
        final id = await showPersonPicker(context, title: title, selectedId: personId, department: department);
        if (id != null) onChanged(id);
      },
      child: InputDecorator(
        decoration: InputDecoration(
          errorText: error,
          prefixIcon: Icon(Icons.search_rounded, size: 20, color: AppColors.textSecondary),
          suffixIcon: const Icon(Icons.keyboard_arrow_down_rounded),
        ),
        child: Text(
          label ?? hint,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: label == null ? hintStyle : inputStyle,
        ),
      ),
    );
  }
}
