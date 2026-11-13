using System.Text.RegularExpressions;

namespace A360.Domain.Entities;

/// <summary>System Audit Log tabs. Every event is in exactly one of them; "All Events" is all of them.</summary>
public static class AuditCategory
{
    public const string UserActivity = "user-activity";
    public const string Configuration = "configuration";
    public const string Security = "security";
    public const string DataChanges = "data-changes";
    public const string Integrations = "integrations";
    public const string Workflow = "workflow";
    public const string ImportsExports = "imports-exports";

    /// <summary>Tab order and labels, as on the System Audit Log page.</summary>
    public static readonly IReadOnlyList<(string Key, string Label)> All =
    [
        (UserActivity, "User Activity"),
        (Configuration, "Configuration"),
        (Security, "Security"),
        (DataChanges, "Data Changes"),
        (Integrations, "Integrations"),
        (Workflow, "Workflow"),
        (ImportsExports, "Imports/Exports")
    ];

    public static string LabelOf(string? key) =>
        All.FirstOrDefault(category => string.Equals(category.Key, key, StringComparison.OrdinalIgnoreCase)).Label ?? key ?? string.Empty;

    /// <summary>Accepts the key ("user-activity") or the label ("User Activity"); null when unknown.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var match = All.FirstOrDefault(category =>
            string.Equals(category.Key, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(category.Label, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(category.Label.Replace(" ", string.Empty), trimmed.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase));
        return match.Key;
    }
}

public static class AuditSeverity
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    public static readonly IReadOnlyList<string> All = [Low, Medium, High, Critical];

    public static int RankOf(string? severity) => severity switch
    {
        Critical => 4,
        High => 3,
        Medium => 2,
        _ => 1
    };

    public static string? Normalize(string? value) =>
        All.FirstOrDefault(severity => string.Equals(severity, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class AuditOutcome
{
    public const string Success = "Success";
    public const string Failed = "Failed";

    public static readonly IReadOnlyList<string> All = [Success, Failed];

    public static string? Normalize(string? value) =>
        All.FirstOrDefault(outcome => string.Equals(outcome, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public sealed record AuditClassification(string Category, string Severity, string Outcome, string Module);

/// <summary>
/// Sorts an event log entry into a System Audit Log tab and gives it a severity, outcome and module, from the service
/// that wrote it, the entity type and the action. Used when the event is written and to fill in older events.
/// </summary>
public static class AuditClassifier
{
    /// <summary>Bump when the rules below change, so stored classifications are redone.</summary>
    public const int Version = 2;

    /// <summary>Sign-in, one-time code, authenticator and session events (not a plain sign-in / sign-out).</summary>
    private static readonly HashSet<string> SecurityActions = new(StringComparer.OrdinalIgnoreCase)
    {
        EventAction.LoginFailed, EventAction.LoginBlocked,
        EventAction.OtpSent, EventAction.OtpSendFailed, EventAction.OtpFailed, EventAction.OtpLocked,
        EventAction.TotpSetupStarted, EventAction.TotpEnabled,
        EventAction.TokenRefreshed, EventAction.TokenRefreshFailed, EventAction.TokenReuseDetected
    };

    /// <summary>One event per imported / exported file.</summary>
    private static readonly HashSet<string> ImportExportActions = new(StringComparer.OrdinalIgnoreCase)
    {
        EventAction.Imported, EventAction.ImportFailed, EventAction.Exported
    };

    private static readonly HashSet<string> FailedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        EventAction.ImportFailed, EventAction.LoginFailed, EventAction.LoginBlocked, EventAction.OtpSendFailed, EventAction.OtpFailed,
        EventAction.OtpLocked, EventAction.TokenRefreshFailed, EventAction.TokenReuseDetected
    };

    /// <summary>Users and their roles: changes to these are User Activity.</summary>
    private static readonly HashSet<string> UserEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        "User", "SystemUser", "Role", "UserProfile", "UserRole"
    };

    /// <summary>Who may do what: changes to these are Security.</summary>
    private static readonly HashSet<string> AccessEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Permission", "ModuleAccess", "AccessPolicy"
    };

    private static readonly HashSet<string> IntegrationEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Integration", "ApiSyncStatus", "UpdateSource"
    };

    private static readonly HashSet<string> ImportExportEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        "DataImportJob", "AssetImportExport"
    };

    private static readonly HashSet<string> ConfigurationServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "Administration", "MasterDataManagement"
    };

    /// <summary>Settings-like entities in other services: field layouts, templates, rules, numbering.</summary>
    private static readonly Regex ConfigurationEntity = new(
        "(Settings|Configuration|Template|Sequence|Rules?|FieldGroup|FieldLayout|CustomFieldDefinition|CategoryAttribute|CategoryCustomField)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> ServiceModules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Asset"] = "Assets",
        ["Maintenance"] = "Maintenance",
        ["Wip"] = "WIP / Operations",
        ["Administration"] = "Administration",
        ["MasterDataManagement"] = "Master Data Management",
        ["ReportAnalytics"] = "Reports & Analytics",
        ["Tracking"] = "Tracking & IoT",
        ["UserAccount"] = "Security",
        ["Dashboard"] = "Dashboard",
        ["Inspection"] = "Inspection",
        ["Workflows"] = "Workflow",
        ["Media"] = "Media"
    };

    private static readonly Dictionary<string, string> EntityModules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Integration"] = "Integration & API",
        ["ApiSyncStatus"] = "Integration & API",
        // Administration › Data Import / Export
        ["DataImportJob"] = "Administration",
        ["AssetImportExport"] = "Administration",
        ["User"] = "User Management",
        ["SystemUser"] = "User Management",
        ["Role"] = "Role & Access Control",
        ["Permission"] = "Role & Access Control",
        ["AccessPolicy"] = "Role & Access Control",
        ["ModuleAccess"] = "Module Access"
    };

    public static AuditClassification Classify(string? serviceName, string? entityType, string? action)
    {
        serviceName ??= string.Empty;
        entityType ??= string.Empty;
        action ??= string.Empty;

        var category = CategoryOf(serviceName, entityType, action);
        var outcome = FailedActions.Contains(action) ? AuditOutcome.Failed : AuditOutcome.Success;
        var severity = SeverityOf(category, entityType, action);
        var module = ModuleOf(serviceName, entityType, action);

        return new AuditClassification(category, severity, outcome, module);
    }

    /// <summary>Applies <see cref="Classify"/> to the entry.</summary>
    public static void Apply(EventLog eventLog)
    {
        var classification = Classify(eventLog.ServiceName, eventLog.EntityType, eventLog.Action);
        eventLog.Category = classification.Category;
        eventLog.Severity = classification.Severity;
        eventLog.SeverityRank = AuditSeverity.RankOf(classification.Severity);
        eventLog.Outcome = classification.Outcome;
        eventLog.Module = classification.Module;
        eventLog.AuditVersion = Version;
    }

    /// <summary>Readable event name: "User signed in", "Failed login attempt", "Asset registration updated".</summary>
    public static string EventNameOf(string? entityType, string? action, string? entityName = null)
    {
        var known = action switch
        {
            EventAction.Imported => "Data import completed",
            EventAction.ImportFailed => "Data import failed",
            EventAction.Exported => "Data export generated",
            EventAction.LoggedIn => "User signed in",
            EventAction.LoggedOut => "User signed out",
            EventAction.LoginFailed => "Failed login attempt",
            EventAction.LoginBlocked => "Sign-in blocked",
            EventAction.OtpSent => "Sign-in code sent",
            EventAction.OtpSendFailed => "Sign-in code could not be sent",
            EventAction.OtpFailed => "Wrong sign-in code entered",
            EventAction.OtpLocked => "Sign-in locked after wrong codes",
            EventAction.TotpSetupStarted => "Authenticator setup started",
            EventAction.TotpEnabled => "Authenticator app enabled",
            EventAction.TokenRefreshed => "Session refreshed",
            EventAction.TokenRefreshFailed => "Session refresh failed",
            EventAction.TokenReuseDetected => "Session token reuse detected",
            _ => null
        };

        if (known is not null)
        {
            return known;
        }

        // Asset bulk import / export jobs logged before the Imported / Exported actions existed
        if (string.Equals(entityType, "AssetImportExport", StringComparison.OrdinalIgnoreCase))
        {
            var isExport = entityName?.Contains("exported", StringComparison.OrdinalIgnoreCase) == true;
            return action switch
            {
                EventAction.Created when isExport => "Data export generated",
                EventAction.Created => "Data import file uploaded",
                EventAction.Updated => "Data import completed",
                EventAction.Deleted => "Data import cancelled",
                _ => "Data import " + Humanize(action ?? "event").ToLowerInvariant()
            };
        }

        if (string.Equals(entityType, "DataImportJob", StringComparison.OrdinalIgnoreCase))
        {
            return "Data import job " + Humanize(action ?? "event").ToLowerInvariant();
        }

        var verb = string.IsNullOrWhiteSpace(action) ? "event" : Humanize(action).ToLowerInvariant();
        var entity = string.IsNullOrWhiteSpace(entityType) ? "Record" : Humanize(entityType);
        return $"{entity} {verb}";
    }

    /// <summary>"AssetRegistration" → "Asset registration"; acronyms such as IoT, AMC and API are kept.</summary>
    public static string Humanize(string value)
    {
        var words = Regex.Split(value.Trim(), "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|[_\\s-]+")
            .Where(word => word.Length > 0)
            .ToList();

        if (words.Count == 0)
        {
            return value;
        }

        // "Io" + "T..." from "IoTDevice": put the acronym back together
        for (var i = 0; i < words.Count - 1; i++)
        {
            if (words[i] == "Io" && words[i + 1].StartsWith('T'))
            {
                words[i] = "IoT";
                words[i + 1] = words[i + 1][1..];
                if (words[i + 1].Length == 0)
                {
                    words.RemoveAt(i + 1);
                }
            }
        }

        static bool IsAcronym(string word) => word == "IoT" || word.Length > 1 && word.All(char.IsUpper);

        return string.Join(' ', words.Select((word, index) =>
            index == 0 ? char.ToUpperInvariant(word[0]) + word[1..]
            : IsAcronym(word) ? word
            : word.ToLowerInvariant()));
    }

    private static string CategoryOf(string serviceName, string entityType, string action)
    {
        if (SecurityActions.Contains(action) || AccessEntities.Contains(entityType))
        {
            return AuditCategory.Security;
        }

        if (ImportExportActions.Contains(action))
        {
            return AuditCategory.ImportsExports;
        }

        if (action is EventAction.LoggedIn or EventAction.LoggedOut or EventAction.Viewed || UserEntities.Contains(entityType))
        {
            return AuditCategory.UserActivity;
        }

        if (ImportExportEntities.Contains(entityType)
            || entityType.Contains("Import", StringComparison.OrdinalIgnoreCase)
            || entityType.Contains("Export", StringComparison.OrdinalIgnoreCase))
        {
            return AuditCategory.ImportsExports;
        }

        if (string.Equals(serviceName, "Workflows", StringComparison.OrdinalIgnoreCase)
            || entityType.StartsWith("Workflow", StringComparison.OrdinalIgnoreCase))
        {
            return AuditCategory.Workflow;
        }

        if (IntegrationEntities.Contains(entityType) || entityType.Contains("Integration", StringComparison.OrdinalIgnoreCase))
        {
            return AuditCategory.Integrations;
        }

        if (ConfigurationServices.Contains(serviceName) || ConfigurationEntity.IsMatch(entityType))
        {
            return AuditCategory.Configuration;
        }

        return AuditCategory.DataChanges;
    }

    private static string SeverityOf(string category, string entityType, string action)
    {
        switch (action)
        {
            case EventAction.TokenReuseDetected:
            case EventAction.OtpLocked:
                return AuditSeverity.Critical;
            case EventAction.LoginFailed:
            case EventAction.LoginBlocked:
            case EventAction.OtpFailed:
            case EventAction.OtpSendFailed:
            case EventAction.TokenRefreshFailed:
                return AuditSeverity.High;
            case EventAction.ImportFailed:
                return AuditSeverity.High;
            case EventAction.Imported:
            case EventAction.Exported:
            case EventAction.TotpSetupStarted:
            case EventAction.TotpEnabled:
                return AuditSeverity.Medium;
            case EventAction.Viewed:
            case EventAction.LoggedIn:
            case EventAction.LoggedOut:
            case EventAction.OtpSent:
            case EventAction.TokenRefreshed:
                return AuditSeverity.Low;
        }

        var isDelete = string.Equals(action, EventAction.Deleted, StringComparison.OrdinalIgnoreCase);
        var guardsAccess = AccessEntities.Contains(entityType) || UserEntities.Contains(entityType);

        // Credentials / connections to other systems, and removing users, roles or permissions
        if (category == AuditCategory.Integrations && !string.Equals(action, EventAction.Created, StringComparison.OrdinalIgnoreCase)
            || isDelete && guardsAccess)
        {
            return AuditSeverity.Critical;
        }

        if (isDelete || AccessEntities.Contains(entityType) || string.Equals(entityType, "Role", StringComparison.OrdinalIgnoreCase))
        {
            return AuditSeverity.High;
        }

        return category == AuditCategory.DataChanges ? AuditSeverity.Low : AuditSeverity.Medium;
    }

    private static string ModuleOf(string serviceName, string entityType, string action)
    {
        if (SecurityActions.Contains(action))
        {
            return "Security";
        }

        if (action is EventAction.LoggedIn or EventAction.LoggedOut)
        {
            return "Authentication";
        }

        if (EntityModules.TryGetValue(entityType, out var module))
        {
            return module;
        }

        return ServiceModules.TryGetValue(serviceName, out module) ? module : Humanize(serviceName);
    }
}
