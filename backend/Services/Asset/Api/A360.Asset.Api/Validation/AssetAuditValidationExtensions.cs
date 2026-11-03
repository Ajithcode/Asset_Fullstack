using System.Globalization;
using System.Net.Mail;
using A360.Asset.Api.Contracts;
using A360.Asset.Api.Endpoints;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Validation;

/// <summary>
/// Rules for each step of the New Asset Audit wizard, checked on the values as they will be saved.
/// A draft (Save as Draft) skips the fields marked * on screen, except the audit name; values that are sent are
/// always checked. Submit runs every step's rules as if none were a draft.
/// </summary>
public static class AssetAuditValidationExtensions
{
    public const int MaxAssets = 5000;
    public const int MaxComponentsPerAsset = 200;
    public const int MaxCriteria = 100;
    public const int MaxTeamMembers = 50;
    public const int MaxAttachments = 20;
    public const int MaxScheduleDays = 366;

    private const int NameMaxLength = 200;
    private const int ReferenceMaxLength = 50;
    private const int ShortTextMaxLength = 100;
    private const int DescriptionMaxLength = 1000;
    private const int NotesMaxLength = 2000;

    /// <summary>Step 1: Basic Information, Scope &amp; Assets and the Audit Criteria checkboxes.</summary>
    public static void ValidateBasicInformation(this AssetAudit audit, Dictionary<string, string[]> errors, bool saveAsDraft, string prefix = "")
    {
        Required(errors, prefix + "AuditName", audit.AuditName);
        MaxLength(errors, prefix + "AuditName", audit.AuditName, NameMaxLength);
        MaxLength(errors, prefix + "AuditReference", audit.AuditReference, ReferenceMaxLength);
        MaxLength(errors, prefix + "Description", audit.Description, DescriptionMaxLength);
        Option(errors, prefix + "AuditType", audit.AuditType, AssetAuditOptions.AuditTypes, saveAsDraft);

        var scope = audit.Scope;
        if (!saveAsDraft && string.IsNullOrWhiteSpace(scope.LocationId))
        {
            errors[prefix + "Scope.LocationId"] = ["Select a location / site"];
        }

        Option(errors, prefix + "Scope.SelectionMethod", scope.SelectionMethod, AssetAuditOptions.SelectionMethods, saveAsDraft);

        if (scope.SelectionMethod == AssetAuditOptions.TagRange)
        {
            if (!saveAsDraft)
            {
                Required(errors, prefix + "Scope.TagFrom", scope.TagFrom);
                Required(errors, prefix + "Scope.TagTo", scope.TagTo);
            }

            if (scope.TagFrom is not null && scope.TagTo is not null && string.CompareOrdinal(scope.TagFrom, scope.TagTo) > 0)
            {
                errors[prefix + "Scope.TagTo"] = ["TagTo cannot come before TagFrom"];
            }
        }

        if (scope.SelectionMethod == AssetAuditOptions.Filter && !saveAsDraft
            && scope.AssetStatus is null && scope.AssetType is null && scope.Criticality is null)
        {
            errors[prefix + "Scope.AssetStatus"] = ["Choose at least one filter: asset status, asset type or criticality"];
        }

        var unknown = audit.CriteriaOptions.Where(option => AssetAuditCriteriaOptions.Normalize(option) is null).ToList();
        if (unknown.Count > 0)
        {
            errors[prefix + "CriteriaOptions"] = [$"Unknown options: {string.Join(", ", unknown)}"];
        }

        if (audit.CriteriaOptions.Contains(AssetAuditCriteriaOptions.Other) && !saveAsDraft)
        {
            Required(errors, prefix + "OtherCriteriaDetails", audit.OtherCriteriaDetails);
        }

        MaxLength(errors, prefix + "OtherCriteriaDetails", audit.OtherCriteriaDetails, NameMaxLength);
    }

    /// <summary>Step 2: Select Assets &amp; Sensors. Existence of the assets and components is checked by the endpoint.</summary>
    public static void ValidateAssets(this AssetAudit audit, Dictionary<string, string[]> errors, bool saveAsDraft, string prefix = "")
    {
        if (audit.Assets.Count > MaxAssets)
        {
            errors[prefix + "Assets"] = [$"At most {MaxAssets} assets can be audited at once"];
        }
        else if (!saveAsDraft && audit.Scope.SelectionMethod == AssetAuditOptions.SelectedAssets && audit.Assets.Count == 0)
        {
            errors[prefix + "Assets"] = ["Select at least one asset"];
        }

        var index = 0;
        foreach (var asset in audit.Assets)
        {
            if (asset.ComponentIds.Count > MaxComponentsPerAsset)
            {
                errors[$"{prefix}Assets[{index}].ComponentIds"] = [$"At most {MaxComponentsPerAsset} sensors / components per asset"];
            }

            index++;
        }
    }

    /// <summary>Step 3: Audit Criteria and Criteria Settings.</summary>
    /// <param name="assetsResolved">The audit's assets are final (on submit), so "Selected Assets" criteria can be checked against them.</param>
    public static void ValidateCriteria(this AssetAudit audit, Dictionary<string, string[]> errors, bool saveAsDraft, string prefix = "", bool assetsResolved = false)
    {
        if (audit.Criteria.Count > MaxCriteria)
        {
            errors[prefix + "Criteria"] = [$"At most {MaxCriteria} criteria can be added"];
            return;
        }

        if (!saveAsDraft && audit.Criteria.Count == 0)
        {
            errors[prefix + "Criteria"] = ["Add at least one criterion"];
        }

        // "Selected Assets" criteria must point at assets in the audit; with another selection method the
        // assets are only known once resolved on submit, so they are checked then.
        var auditAssets = assetsResolved || audit.Scope.SelectionMethod == AssetAuditOptions.SelectedAssets || audit.Status != AssetAuditRepository.DraftStatus
            ? audit.Assets.Select(asset => asset.AssetRegistrationId).ToHashSet(StringComparer.Ordinal)
            : null;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var criterion in audit.Criteria)
        {
            var key = $"{prefix}Criteria[{index}]";

            Required(errors, key + ".Name", criterion.Name);
            MaxLength(errors, key + ".Name", criterion.Name, NameMaxLength);
            MaxLength(errors, key + ".Description", criterion.Description, DescriptionMaxLength);
            MaxLength(errors, key + ".Category", criterion.Category, ShortTextMaxLength);
            MaxLength(errors, key + ".CheckMethod", criterion.CheckMethod, ShortTextMaxLength);
            Option(errors, key + ".Type", criterion.Type, AssetAuditOptions.CriterionTypes, saveAsDraft: false);
            Option(errors, key + ".AppliesTo", criterion.AppliesTo, AssetAuditOptions.AppliesTo, saveAsDraft: false);

            if (!string.IsNullOrWhiteSpace(criterion.Name) && !names.Add(criterion.Name.Trim()))
            {
                errors[key + ".Name"] = [$"Criterion '{criterion.Name}' is listed more than once"];
            }

            if (criterion.AppliesTo == AssetAuditOptions.SelectedAssets)
            {
                if (!saveAsDraft && criterion.AssetRegistrationIds.Count == 0)
                {
                    errors[key + ".AssetRegistrationIds"] = ["Select the assets this criterion applies to"];
                }
                else if (auditAssets is not null && criterion.AssetRegistrationIds.FirstOrDefault(id => !auditAssets.Contains(id)) is { } missing)
                {
                    errors[key + ".AssetRegistrationIds"] = [$"Asset '{missing}' is not in this audit"];
                }
            }

            index++;
        }
    }

    /// <summary>Step 4: Assign Audit Team, Schedule Audit and Audit Plan.</summary>
    public static void ValidateSchedule(this AssetAudit audit, Dictionary<string, string[]> errors, bool saveAsDraft, string prefix = "")
    {
        ValidateTeam(errors, audit.Team, saveAsDraft, prefix);

        var schedule = audit.Schedule;
        if (!saveAsDraft)
        {
            if (schedule.StartDate is null)
            {
                errors[prefix + "Schedule.StartDate"] = ["StartDate is required"];
            }

            if (schedule.EndDate is null)
            {
                errors[prefix + "Schedule.EndDate"] = ["EndDate is required"];
            }
        }

        if (schedule.StartDate is not null && schedule.EndDate is not null)
        {
            if (schedule.EndDate < schedule.StartDate)
            {
                errors[prefix + "Schedule.EndDate"] = ["EndDate cannot be before StartDate"];
            }
            else if ((schedule.EndDate.Value - schedule.StartDate.Value).TotalDays >= MaxScheduleDays)
            {
                errors[prefix + "Schedule.EndDate"] = [$"An audit cannot run longer than {MaxScheduleDays} days"];
            }
        }

        Option(errors, prefix + "Schedule.WorkingDays", schedule.WorkingDays, AssetAuditOptions.WorkingDays, saveAsDraft);
        if (schedule.WorkingDays == AssetAuditOptions.CustomDays)
        {
            var unknownDays = schedule.CustomDays.Where(day => !AssetAuditOptions.DaysOfWeek.IsValid(day)).ToList();
            if (unknownDays.Count > 0)
            {
                errors[prefix + "Schedule.CustomDays"] = [$"Unknown days: {string.Join(", ", unknownDays)}"];
            }
            else if (!saveAsDraft && schedule.CustomDays.Count == 0)
            {
                errors[prefix + "Schedule.CustomDays"] = ["Pick at least one working day"];
            }
        }

        var startTime = Time(errors, prefix + "Schedule.DailyStartTime", schedule.DailyStartTime);
        var endTime = Time(errors, prefix + "Schedule.DailyEndTime", schedule.DailyEndTime);
        if (startTime is not null && endTime is not null && endTime <= startTime)
        {
            errors[prefix + "Schedule.DailyEndTime"] = ["DailyEndTime must be after DailyStartTime"];
        }

        Option(errors, prefix + "Schedule.LocationSequence", schedule.LocationSequence, AssetAuditOptions.LocationSequences, saveAsDraft);

        if (!saveAsDraft && schedule.StartDate is not null && schedule.EndDate >= schedule.StartDate
            && !errors.ContainsKey(prefix + "Schedule.CustomDays") && AssetAuditPlanner.WorkingDates(schedule).Count == 0)
        {
            errors[prefix + "Schedule.WorkingDays"] = ["There are no working days between StartDate and EndDate"];
        }

        ValidatePlan(errors, audit, saveAsDraft, prefix);
    }

    /// <summary>Step 5: Attachments and Notes &amp; Instructions.</summary>
    public static void ValidateReview(this AssetAudit audit, Dictionary<string, string[]> errors, string prefix = "")
    {
        if (audit.Attachments.Count > MaxAttachments)
        {
            errors[prefix + "Attachments"] = [$"At most {MaxAttachments} attachments can be added"];
        }
        else
        {
            var index = 0;
            foreach (var attachment in audit.Attachments)
            {
                Required(errors, $"{prefix}Attachments[{index}].FileName", attachment.FileName);
                Required(errors, $"{prefix}Attachments[{index}].FileUrl", attachment.FileUrl);
                index++;
            }
        }

        MaxLength(errors, prefix + "Notes", audit.Notes, NotesMaxLength);
    }

    /// <summary>Submit: every step, as if none were a draft. Keys start with the step, e.g. "Schedule.Team".</summary>
    public static Dictionary<string, string[]> ValidateForSubmit(this AssetAudit audit)
    {
        var errors = new Dictionary<string, string[]>();
        audit.ValidateBasicInformation(errors, saveAsDraft: false, "BasicInformation.");
        audit.ValidateAssets(errors, saveAsDraft: false, "Assets.");
        audit.ValidateCriteria(errors, saveAsDraft: false, "Criteria.", assetsResolved: true);
        audit.ValidateSchedule(errors, saveAsDraft: false, "AssignSchedule.");
        audit.ValidateReview(errors, "Review.");

        if (audit.Assets.Count == 0 && !errors.ContainsKey("Assets.Assets"))
        {
            errors["Assets.Assets"] = ["No assets are in the audit's scope"];
        }

        return errors;
    }

    private static void ValidateTeam(Dictionary<string, string[]> errors, IReadOnlyCollection<AssetAuditTeamMember> team, bool saveAsDraft, string prefix)
    {
        if (team.Count > MaxTeamMembers)
        {
            errors[prefix + "Team"] = [$"At most {MaxTeamMembers} team members can be added"];
            return;
        }

        if (!saveAsDraft)
        {
            if (team.Count == 0)
            {
                errors[prefix + "Team"] = ["Add at least one team member"];
            }
            else if (team.Count(member => member.Role == AssetAuditOptions.LeadAuditor) != 1)
            {
                errors[prefix + "Team"] = ["Choose exactly one Lead Auditor"];
            }
        }

        var userIds = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var member in team)
        {
            var key = $"{prefix}Team[{index}]";
            Required(errors, key + ".UserId", member.UserId);
            Required(errors, key + ".Name", member.Name);
            Option(errors, key + ".Role", member.Role, AssetAuditOptions.Roles, saveAsDraft: false);
            MaxLength(errors, key + ".Name", member.Name, NameMaxLength);

            if (!string.IsNullOrWhiteSpace(member.UserId) && !userIds.Add(member.UserId))
            {
                errors[key + ".UserId"] = [$"{member.Name} is in the team more than once"];
            }

            if (member.Email is not null && !MailAddress.TryCreate(member.Email, out _))
            {
                errors[key + ".Email"] = ["Email is not a valid email address"];
            }

            index++;
        }
    }

    private static void ValidatePlan(Dictionary<string, string[]> errors, AssetAudit audit, bool saveAsDraft, string prefix)
    {
        var schedule = audit.Schedule;
        if (!saveAsDraft && audit.Plan.Count == 0)
        {
            errors[prefix + "Plan"] = ["The audit plan is empty; regenerate it or add plan entries"];
            return;
        }

        var teamIds = audit.Team.Select(member => member.UserId).ToHashSet(StringComparer.Ordinal);
        var index = 0;
        foreach (var entry in audit.Plan)
        {
            var key = $"{prefix}Plan[{index}]";

            if (schedule.StartDate is not null && schedule.EndDate is not null && (entry.Date < schedule.StartDate || entry.Date > schedule.EndDate))
            {
                errors[key + ".Date"] = ["Date must be between the schedule's StartDate and EndDate"];
            }

            if (entry.AssignedToUserId is not null && !teamIds.Contains(entry.AssignedToUserId))
            {
                errors[key + ".AssignedToUserId"] = ["Assign the plan entry to a member of the audit team"];
            }
            else if (!saveAsDraft && entry.AssignedToUserId is null)
            {
                errors[key + ".AssignedToUserId"] = ["AssignedToUserId is required"];
            }

            Option(errors, key + ".Status", entry.Status, AssetAuditOptions.PlanStatuses, saveAsDraft: false);
            MaxLength(errors, key + ".Location", entry.Location, NameMaxLength);
            index++;
        }
    }

    /// <summary>"HH:mm"; null when empty or not valid (an error is added for the latter).</summary>
    private static TimeOnly? Time(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (TimeOnly.TryParseExact(value, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        errors[field] = [$"{field.Split('.').Last()} must be a time such as 09:00"];
        return null;
    }

    private static void Option(Dictionary<string, string[]> errors, string field, string? value, OptionList options, bool saveAsDraft)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (!saveAsDraft)
            {
                errors[field] = [$"{field.Split('.').Last()} is required"];
            }
        }
        else if (!options.IsValid(value))
        {
            errors[field] = [$"{field.Split('.').Last()} must be one of: {string.Join(", ", options.All)}"];
        }
    }

    private static void Required(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field.Split('.').Last()} is required"];
        }
    }

    private static void MaxLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field.Split('.').Last()} cannot be longer than {maxLength} characters"];
        }
    }
}
