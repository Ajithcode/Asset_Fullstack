using A360.Repository.Sequences;

namespace A360.Inspection.Api.Endpoints;

/// <summary>
/// Sequence names and code formats for generated Inspection references (IT-001, INS-2026-0148, ...).
/// Names are prefixed with "inspection." because the counters collection is shared by every service.
/// </summary>
public static class InspectionCodes
{
    public const string InspectionTypeSequence = "inspection.inspection_type.it";
    public const string TaskCategorySequence = "inspection.task_category.tc";
    public const string InspectionTaskSequence = "inspection.inspection_task.itk";
    public const string ChecklistSequence = "inspection.checklist.chk";
    public const string SignatureSequence = "inspection.signature.sig";
    public const string StampSequence = "inspection.stamp.stp";
    public const string NotificationTemplateSequence = "inspection.notification_template.nt";
    public const string ReportTemplateSequence = "inspection.report_template.rt";
    public const string NumberingSequenceSequence = "inspection.numbering_sequence.ns";
    public const string CalendarSequence = "inspection.calendar.cal";
    public const string InspectionPlanSequence = "inspection.inspection_plan.pln";
    public const string InspectionSequencePrefix = "inspection.inspection.ins";
    public const string FindingSequencePrefix = "inspection.inspection_finding.fnd";

    /// <summary>Formats PREFIX-001 (3 digits, grows past 999).</summary>
    public static string Format(string prefix, long value)
    {
        return $"{prefix}-{value:D3}";
    }

    /// <summary>Formats PREFIX-YYYY-0001 (4 digits, grows past 9999).</summary>
    public static string FormatYearly(string prefix, int year, long value)
    {
        return $"{prefix}-{year}-{value:D4}";
    }

    /// <summary>Per-year counter name, so yearly references restart at 0001 each January.</summary>
    public static string YearlySequenceName(string sequencePrefix, int year)
    {
        return $"{sequencePrefix}.{year}";
    }

    public static async Task<string> NextAsync(
        ISequenceGenerator sequenceGenerator,
        string sequenceName,
        string prefix,
        CancellationToken cancellationToken)
    {
        var next = await sequenceGenerator.GetNextValueAsync(sequenceName, cancellationToken);
        return Format(prefix, next);
    }

    public static async Task<string> NextYearlyAsync(
        ISequenceGenerator sequenceGenerator,
        string sequencePrefix,
        string prefix,
        CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var next = await sequenceGenerator.GetNextValueAsync(YearlySequenceName(sequencePrefix, year), cancellationToken);
        return FormatYearly(prefix, year, next);
    }
}
