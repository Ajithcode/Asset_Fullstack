using System.Collections.Concurrent;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Services;

/// <summary>
/// Spec defaults for the Asset Criticality configuration (7 criteria, the default template 25/25/15/10/10/10/5, the
/// approval matrix and the review intervals). Seeded once per client, the first time the configuration is read, and
/// only into collections that are still empty, so edits are never overwritten.
/// </summary>
public sealed class AssetCriticalityConfigSeeder(
    AssetCriticalityCriteriaRepository criteria,
    AssetCriticalityTemplateRepository templates,
    AssetCriticalityApprovalRuleRepository approvalRules,
    AssetCriticalityReassessmentRuleRepository reassessmentRules)
{
    private static readonly ConcurrentDictionary<string, bool> Done = new();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static readonly (string Code, string Name, string Description, double Weight)[] DefaultCriteria =
    [
        ("SAFETY", "Safety & Human Impact", "Risk of injury or loss of life if the asset fails.", 25),
        ("OPERATIONAL", "Operational / Production Impact", "Effect on production or service delivery when the asset is down.", 25),
        ("FINANCIAL", "Financial Impact", "Cost of downtime, repair and lost revenue.", 15),
        ("ENVIRONMENTAL", "Environmental Impact", "Risk of spills, emissions or other environmental harm.", 10),
        ("REGULATORY", "Regulatory / Compliance Impact", "Breach of legal, statutory or contractual obligations.", 10),
        ("REDUNDANCY", "Redundancy / Alternative Availability", "Lack of a standby or alternative asset (5 = no alternative).", 10),
        ("CUSTOMER", "Customer / Service Impact", "Effect on customers or the service they receive.", 5)
    ];

    public async Task EnsureAsync(string? clientId, string? tenantId, string? user, CancellationToken cancellationToken)
    {
        var key = clientId ?? string.Empty;
        if (Done.ContainsKey(key))
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (Done.ContainsKey(key))
            {
                return;
            }

            var now = DateTime.UtcNow;
            T Stamp<T>(T entity) where T : A360.Domain.Entities.BaseEntity
            {
                entity.ClientId = clientId;
                entity.TenantId = tenantId;
                entity.CreatedBy = user ?? "system";
                entity.CreatedAt = now;
                entity.Status = "Active";
                return entity;
            }

            var seededCriteria = new List<AssetCriticalityCriterionMaster>();
            if (await criteria.CountAsync(clientId, cancellationToken) == 0)
            {
                var order = 0;
                foreach (var (code, name, description, _) in DefaultCriteria)
                {
                    seededCriteria.Add(await criteria.CreateAsync(
                        Stamp(new AssetCriticalityCriterionMaster { Code = code, Name = name, Description = description, IsActive = true, SortOrder = ++order }),
                        cancellationToken));
                }
            }

            if (await templates.CountAsync(clientId, cancellationToken) == 0)
            {
                var source = seededCriteria.Count > 0 ? seededCriteria : await criteria.ListAsync(clientId, false, cancellationToken);
                var weights = DefaultCriteria.ToDictionary(c => c.Code, c => c.Weight);
                var items = source.Where(c => weights.ContainsKey(c.Code))
                    .Select(c => new AssetCriticalityTemplateCriterion { CriterionId = c.Id, Code = c.Code, Name = c.Name, Weight = weights[c.Code] })
                    .ToList();
                if (items.Count == DefaultCriteria.Length)
                {
                    await templates.CreateAsync(Stamp(new AssetCriticalityTemplate
                    {
                        Code = "TPL-DEFAULT",
                        Name = "Default Criticality Template",
                        Industry = "General",
                        Description = "Seven criteria rated 1-5; score = sum of rating / 5 x weight.",
                        IsDefault = true,
                        Version = 1,
                        IsLatest = true,
                        Criteria = items
                    }), cancellationToken);
                }
            }

            if ((await approvalRules.ListAsync(clientId, cancellationToken)).Count == 0)
            {
                var order = 0;
                foreach (var rule in DefaultApprovalRules())
                {
                    rule.SortOrder = ++order;
                    await approvalRules.CreateAsync(Stamp(rule), cancellationToken);
                }
            }

            if ((await reassessmentRules.ListAsync(clientId, cancellationToken)).Count == 0)
            {
                foreach (var (level, months) in new[] { ("Critical", 6), ("High", 12), ("Medium", 24), ("Low", 36) })
                {
                    await reassessmentRules.CreateAsync(Stamp(new AssetCriticalityReassessmentRule { LevelName = level, ReviewIntervalMonths = months }), cancellationToken);
                }
            }

            Done[key] = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Approval matrix from the specification (§4).</summary>
    public static IEnumerable<AssetCriticalityApprovalRule> DefaultApprovalRules() =>
    [
        new() { Scenario = "Level:Low", Label = "Initial Low", ApproverPermission = AssetCriticalityApproverPermissions.Assess, ApproverRoles = ["Asset Owner"], Description = "Asset owner or an authorized assessor." },
        new() { Scenario = "Level:Medium", Label = "Initial Medium", ApproverPermission = AssetCriticalityApproverPermissions.Assess, ApproverRoles = ["Asset Owner"], Description = "Asset owner or an authorized assessor." },
        new() { Scenario = "Level:High", Label = "Initial High", ApproverPermission = AssetCriticalityApproverPermissions.Approve, ApproverRoles = ["Maintenance Manager"], Description = "Maintenance Manager." },
        new() { Scenario = "Level:Critical", Label = "Initial Critical", ApproverPermission = AssetCriticalityApproverPermissions.Approve, ApproverRoles = ["Maintenance Manager", "Risk / Operations Authority"], ApprovalType = "All", Description = "Maintenance Manager and the Risk / Operations authority." },
        new() { Scenario = "LevelReduced", Label = "Level reduced", ApproverPermission = AssetCriticalityApproverPermissions.Approve, ApproverRoles = ["Maintenance Manager"], RequiresJustification = true, Description = "Higher-level approval and a justification (Critical lowered: Risk / Operations authority as well)." },
        new() { Scenario = "ReviewNoChange", Label = "Periodic review, no change", ApproverPermission = AssetCriticalityApproverPermissions.Assess, ApproverRoles = ["Asset Owner"], Description = "Simplified review." }
    ];
}
