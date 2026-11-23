using System.Text.RegularExpressions;
using A360.Asset.Api.Security;
using A360.Asset.Api.Services;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>
/// Asset Criticality configuration (/api/asset-criticality/config): criteria, templates (versioned, weights total 100),
/// the approval matrix and the review intervals. Everyone with View reads; changing needs Configure (Assets Approve).
/// The score ranges of the levels are the MDM Criticality master (/api/master-data/criticalities).
/// </summary>
public static class AssetCriticalityConfigEndpoints
{
    private const string Configure = AssetCriticalityPermissions.Configure;
    private const string ConfigureAction = "change the Asset Criticality configuration";

    public static RouteGroupBuilder MapAssetCriticalityConfigEndpoints(this RouteGroupBuilder parent)
    {
        var group = parent.MapGroup("/config");

        group.MapGet("/criteria", ListCriteriaAsync);
        group.MapPost("/criteria", CreateCriterionAsync).RequireCriticalityPermission(Configure, ConfigureAction);
        group.MapPut("/criteria/{id}", UpdateCriterionAsync).RequireCriticalityPermission(Configure, ConfigureAction);
        group.MapDelete("/criteria/{id}", DeleteCriterionAsync).RequireCriticalityPermission(Configure, ConfigureAction);

        group.MapGet("/templates", ListTemplatesAsync);
        group.MapGet("/templates/{id}", GetTemplateAsync);
        group.MapGet("/templates/{id}/versions", GetTemplateVersionsAsync);
        group.MapPost("/templates", CreateTemplateAsync).RequireCriticalityPermission(Configure, ConfigureAction);
        group.MapPut("/templates/{id}", UpdateTemplateAsync).RequireCriticalityPermission(Configure, ConfigureAction);
        group.MapDelete("/templates/{id}", RetireTemplateAsync).RequireCriticalityPermission(Configure, ConfigureAction);

        group.MapGet("/approval-rules", ListApprovalRulesAsync);
        group.MapPut("/approval-rules/{scenario}", UpdateApprovalRuleAsync).RequireCriticalityPermission(Configure, ConfigureAction);

        group.MapGet("/reassessment-rules", ListReassessmentRulesAsync);
        group.MapPut("/reassessment-rules/{levelName}", UpdateReassessmentRuleAsync).RequireCriticalityPermission(Configure, ConfigureAction);

        return group;
    }

    // ---------------------------------------------------------------- contracts

    public sealed record CriterionRequest(string? Code, string? Name, string? Description, bool? IsActive, int? SortOrder);

    public sealed record TemplateCriterionRequest(string? CriterionId, decimal? Weight);

    public sealed record TemplateRequest(string? Code, string? Name, string? Industry, string? Description, bool? IsDefault, List<TemplateCriterionRequest>? Criteria);

    public sealed record ApprovalRuleRequest(string? ApproverPermission, List<string>? ApproverRoles, string? ApprovalType, bool? RequiresJustification, string? Description);

    public sealed record ReassessmentRuleRequest(int? ReviewIntervalMonths);

    public sealed record CriterionResponse(string Id, string Code, string Name, string? Description, bool IsActive, int SortOrder)
    {
        public static CriterionResponse From(AssetCriticalityCriterionMaster c) => new(c.Id, c.Code, c.Name, c.Description, c.IsActive, c.SortOrder);
    }

    public sealed record TemplateCriterionResponse(string CriterionId, string Code, string Name, double Weight);

    public sealed record TemplateResponse(
        string Id, string Code, string Name, string? Industry, string? Description, bool IsDefault, int Version, bool IsLatest,
        bool IsActive, double TotalWeight, List<TemplateCriterionResponse> Criteria, DateTime? CreatedAt, string? CreatedBy)
    {
        public static TemplateResponse From(AssetCriticalityTemplate t) => new(
            t.Id, t.Code, t.Name, t.Industry, t.Description, t.IsDefault, t.Version, t.IsLatest,
            !string.Equals(t.Status, "Inactive", StringComparison.OrdinalIgnoreCase), t.Criteria.Sum(c => c.Weight),
            t.Criteria.Select(c => new TemplateCriterionResponse(c.CriterionId, c.Code, c.Name, c.Weight)).ToList(), t.CreatedAt, t.CreatedBy);
    }

    public sealed record ApprovalRuleResponse(string Id, string Scenario, string Label, string ApproverPermission, List<string> ApproverRoles, string ApprovalType, bool RequiresJustification, string? Description)
    {
        public static ApprovalRuleResponse From(AssetCriticalityApprovalRule r) =>
            new(r.Id, r.Scenario, r.Label, r.ApproverPermission, r.ApproverRoles, r.ApprovalType, r.RequiresJustification, r.Description);
    }

    public sealed record ReassessmentRuleResponse(string Id, string LevelName, int ReviewIntervalMonths)
    {
        public static ReassessmentRuleResponse From(AssetCriticalityReassessmentRule r) => new(r.Id, r.LevelName, r.ReviewIntervalMonths);
    }

    // ---------------------------------------------------------------- helpers

    private static string? ClientOf(HttpContext http) => Claim(http, A360ClaimTypes.ClientId);

    private static string? Claim(HttpContext http, string type)
    {
        var value = http.User.FindFirst(type)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string UserOf(HttpContext http) => Claim(http, A360ClaimTypes.UserName) ?? Claim(http, A360ClaimTypes.Email) ?? "unknown";

    private static Task SeedAsync(HttpContext http, AssetCriticalityConfigSeeder seeder) =>
        seeder.EnsureAsync(ClientOf(http), Claim(http, A360ClaimTypes.TenantId), UserOf(http), http.RequestAborted);

    private static IResult Problem(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static string CodeFrom(string? code, string name, string prefix)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            return code.Trim().ToUpperInvariant();
        }

        var slug = Regex.Replace(name.ToUpperInvariant(), "[^A-Z0-9]+", "-").Trim('-');
        return $"{prefix}-{(slug.Length > 24 ? slug[..24].TrimEnd('-') : slug)}";
    }

    // ---------------------------------------------------------------- criteria

    private static async Task<IResult> ListCriteriaAsync(HttpContext http, AssetCriticalityCriteriaRepository repository, AssetCriticalityConfigSeeder seeder, bool? includeInactive = null)
    {
        await SeedAsync(http, seeder);
        var items = await repository.ListAsync(ClientOf(http), includeInactive ?? true, http.RequestAborted);
        return Results.Ok(items.Select(CriterionResponse.From));
    }

    private static async Task<IResult> CreateCriterionAsync(CriterionRequest request, HttpContext http, AssetCriticalityCriteriaRepository repository, AssetCriticalityConfigSeeder seeder, IEventLogger events)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem("Name", "Name is required");
        }

        await SeedAsync(http, seeder);
        var clientId = ClientOf(http);
        var name = request.Name.Trim();
        var code = CodeFrom(request.Code, name, "CRT");
        if (await repository.GetByCodeOrNameAsync(clientId, code, name, http.RequestAborted) is not null)
        {
            return Results.Conflict(new { message = $"A criterion with code '{code}' or name '{name}' already exists." });
        }

        var existing = await repository.ListAsync(clientId, true, http.RequestAborted);
        var criterion = await repository.CreateAsync(new AssetCriticalityCriterionMaster
        {
            Code = code, Name = name, Description = request.Description?.Trim(), IsActive = request.IsActive ?? true,
            SortOrder = request.SortOrder ?? (existing.Count == 0 ? 1 : existing.Max(c => c.SortOrder) + 1),
            ClientId = clientId, TenantId = Claim(http, A360ClaimTypes.TenantId), CreatedBy = UserOf(http), CreatedAt = DateTime.UtcNow, Status = "Active"
        }, http.RequestAborted);

        await events.LogAsync("AssetCriticalityCriteria", criterion.Id, criterion.Name, EventAction.Created, http.RequestAborted);
        return Results.Created($"/api/asset-criticality/config/criteria/{criterion.Id}", CriterionResponse.From(criterion));
    }

    private static async Task<IResult> UpdateCriterionAsync(string id, CriterionRequest request, HttpContext http, AssetCriticalityCriteriaRepository repository, IEventLogger events)
    {
        var criterion = await repository.GetByIdAsync(id, http.RequestAborted);
        if (criterion is null || criterion.IsDeleted)
        {
            return Results.NotFound();
        }

        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem("Name", "Name is required");
        }

        var name = request.Name?.Trim() ?? criterion.Name;
        var code = string.IsNullOrWhiteSpace(request.Code) ? criterion.Code : request.Code.Trim().ToUpperInvariant();
        var clash = await repository.GetByCodeOrNameAsync(criterion.ClientId, code, name, http.RequestAborted);
        if (clash is not null && clash.Id != id)
        {
            return Results.Conflict(new { message = $"A criterion with code '{code}' or name '{name}' already exists." });
        }

        // Templates keep a copy of the name and code, so renaming never changes an existing template version.
        criterion.Name = name;
        criterion.Code = code;
        criterion.Description = request.Description is null ? criterion.Description : request.Description.Trim();
        criterion.IsActive = request.IsActive ?? criterion.IsActive;
        criterion.SortOrder = request.SortOrder ?? criterion.SortOrder;
        criterion.UpdatedBy = UserOf(http);
        criterion.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(id, criterion, http.RequestAborted);

        await events.LogAsync("AssetCriticalityCriteria", criterion.Id, criterion.Name, EventAction.Updated, http.RequestAborted);
        return Results.Ok(CriterionResponse.From(criterion));
    }

    // Soft delete; templates (and their old versions) keep their copy.
    private static async Task<IResult> DeleteCriterionAsync(string id, HttpContext http, AssetCriticalityCriteriaRepository repository, AssetCriticalityTemplateRepository templates, IEventLogger events)
    {
        var criterion = await repository.GetByIdAsync(id, http.RequestAborted);
        if (criterion is null || criterion.IsDeleted)
        {
            return Results.NotFound();
        }

        var inUse = (await templates.ListLatestAsync(criterion.ClientId, http.RequestAborted))
            .Where(t => !string.Equals(t.Status, "Inactive", StringComparison.OrdinalIgnoreCase) && t.Criteria.Any(c => c.CriterionId == id))
            .Select(t => t.Name).ToList();
        if (inUse.Count > 0)
        {
            return Results.Conflict(new { message = $"The criterion is used by template(s): {string.Join(", ", inUse)}. Remove it from them first, or set it inactive." });
        }

        criterion.IsDeleted = true;
        criterion.IsActive = false;
        criterion.UpdatedBy = UserOf(http);
        criterion.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(id, criterion, http.RequestAborted);

        await events.LogAsync("AssetCriticalityCriteria", criterion.Id, criterion.Name, EventAction.Deleted, http.RequestAborted);
        return Results.NoContent();
    }

    // ---------------------------------------------------------------- templates

    private static async Task<IResult> ListTemplatesAsync(HttpContext http, AssetCriticalityTemplateRepository repository, AssetCriticalityConfigSeeder seeder)
    {
        await SeedAsync(http, seeder);
        return Results.Ok((await repository.ListLatestAsync(ClientOf(http), http.RequestAborted)).Select(TemplateResponse.From));
    }

    private static async Task<IResult> GetTemplateAsync(string id, AssetCriticalityTemplateRepository repository, HttpContext http)
    {
        var template = await repository.GetByIdAsync(id, http.RequestAborted);
        return template is null || template.IsDeleted ? Results.NotFound() : Results.Ok(TemplateResponse.From(template));
    }

    private static async Task<IResult> GetTemplateVersionsAsync(string id, AssetCriticalityTemplateRepository repository, HttpContext http)
    {
        var template = await repository.GetByIdAsync(id, http.RequestAborted);
        if (template is null || template.IsDeleted)
        {
            return Results.NotFound();
        }

        return Results.Ok((await repository.GetVersionsAsync(template.ClientId, template.Code, http.RequestAborted)).Select(TemplateResponse.From));
    }

    /// <summary>Weights add up to exactly 100; criteria exist, are active and are listed once. Returns the template criteria or an error.</summary>
    private static async Task<(List<AssetCriticalityTemplateCriterion>? Criteria, IResult? Error)> BuildCriteriaAsync(
        TemplateRequest request, string? clientId, AssetCriticalityCriteriaRepository criteriaRepository, CancellationToken cancellationToken)
    {
        if (request.Criteria is null || request.Criteria.Count == 0)
        {
            return (null, Problem("Criteria", "Add at least one criterion."));
        }

        if (request.Criteria.Any(c => string.IsNullOrWhiteSpace(c.CriterionId)))
        {
            return (null, Problem("Criteria", "Every row needs a criterion."));
        }

        if (request.Criteria.GroupBy(c => c.CriterionId).Any(g => g.Count() > 1))
        {
            return (null, Problem("Criteria", "A criterion can be listed only once."));
        }

        if (request.Criteria.Any(c => c.Weight is null or <= 0 or > 100 || decimal.Round(c.Weight.Value, 2) != c.Weight))
        {
            return (null, Problem("Criteria", "Each weight must be above 0 and at most 100, with up to 2 decimals."));
        }

        var total = request.Criteria.Sum(c => c.Weight!.Value);
        if (total != 100m)
        {
            return (null, Problem("Criteria", $"The weights must total exactly 100 (now {total:0.##})."));
        }

        var known = (await criteriaRepository.ListAsync(clientId, true, cancellationToken)).ToDictionary(c => c.Id);
        var result = new List<AssetCriticalityTemplateCriterion>();
        foreach (var row in request.Criteria)
        {
            if (!known.TryGetValue(row.CriterionId!, out var criterion))
            {
                return (null, Problem("Criteria", $"Criterion '{row.CriterionId}' was not found."));
            }

            if (!criterion.IsActive)
            {
                return (null, Problem("Criteria", $"Criterion '{criterion.Name}' is inactive."));
            }

            result.Add(new AssetCriticalityTemplateCriterion { CriterionId = criterion.Id, Code = criterion.Code, Name = criterion.Name, Weight = (double)row.Weight!.Value });
        }

        return (result, null);
    }

    private static async Task<IResult> CreateTemplateAsync(
        TemplateRequest request, HttpContext http, AssetCriticalityTemplateRepository repository,
        AssetCriticalityCriteriaRepository criteriaRepository, AssetCriticalityConfigSeeder seeder, IEventLogger events)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem("Name", "Name is required");
        }

        await SeedAsync(http, seeder);
        var clientId = ClientOf(http);
        var (criteria, error) = await BuildCriteriaAsync(request, clientId, criteriaRepository, http.RequestAborted);
        if (error is not null)
        {
            return error;
        }

        var name = request.Name.Trim();
        var code = CodeFrom(request.Code, name, "TPL");
        if (await repository.GetLatestByCodeOrNameAsync(clientId, code, name, http.RequestAborted) is not null)
        {
            return Results.Conflict(new { message = $"A template with code '{code}' or name '{name}' already exists." });
        }

        var isDefault = request.IsDefault == true || await repository.GetDefaultAsync(clientId, http.RequestAborted) is null;
        var template = await repository.CreateAsync(new AssetCriticalityTemplate
        {
            Code = code, Name = name, Industry = request.Industry?.Trim(), Description = request.Description?.Trim(),
            IsDefault = isDefault, Version = 1, IsLatest = true, Criteria = criteria!,
            ClientId = clientId, TenantId = Claim(http, A360ClaimTypes.TenantId), CreatedBy = UserOf(http), CreatedAt = DateTime.UtcNow, Status = "Active"
        }, http.RequestAborted);

        if (isDefault)
        {
            await repository.ClearDefaultAsync(clientId, code, http.RequestAborted);
        }

        await events.LogAsync("AssetCriticalityTemplate", template.Id, $"{template.Name} v{template.Version}", EventAction.Created, http.RequestAborted);
        return Results.Created($"/api/asset-criticality/config/templates/{template.Id}", TemplateResponse.From(template));
    }

    /// <summary>Editing saves a NEW version (version + 1); the old one is kept for assessments that used it.</summary>
    private static async Task<IResult> UpdateTemplateAsync(
        string id, TemplateRequest request, HttpContext http, AssetCriticalityTemplateRepository repository,
        AssetCriticalityCriteriaRepository criteriaRepository, IEventLogger events)
    {
        var current = await repository.GetByIdAsync(id, http.RequestAborted);
        if (current is null || current.IsDeleted)
        {
            return Results.NotFound();
        }

        if (!current.IsLatest)
        {
            return Results.Conflict(new { message = $"Version {current.Version} is an old version and cannot be edited. Edit the latest version." });
        }

        var (criteria, error) = await BuildCriteriaAsync(request, current.ClientId, criteriaRepository, http.RequestAborted);
        if (error is not null)
        {
            return error;
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? current.Name : request.Name.Trim();
        if (!name.Equals(current.Name, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await repository.GetLatestByCodeOrNameAsync(current.ClientId, current.Code, name, http.RequestAborted);
            if (clash is not null && clash.Id != id)
            {
                return Results.Conflict(new { message = $"A template named '{name}' already exists." });
            }
        }

        var wantsDefault = request.IsDefault ?? current.IsDefault;
        if (current.IsDefault && !wantsDefault)
        {
            return Problem("IsDefault", "There must be one default template. Make another template the default instead.");
        }

        var next = new AssetCriticalityTemplate
        {
            Code = current.Code, Name = name, Industry = request.Industry is null ? current.Industry : request.Industry.Trim(),
            Description = request.Description is null ? current.Description : request.Description.Trim(),
            IsDefault = wantsDefault, Version = current.Version + 1, IsLatest = true, Criteria = criteria!,
            ClientId = current.ClientId, TenantId = current.TenantId, CreatedBy = UserOf(http), CreatedAt = DateTime.UtcNow,
            Status = current.Status ?? "Active"
        };

        try
        {
            next = await repository.CreateAsync(next, http.RequestAborted);
        }
        catch (MongoDB.Driver.MongoWriteException exception) when (exception.WriteError.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
        {
            return Results.Conflict(new { message = "The template was changed by someone else. Reload and try again." });
        }

        await repository.SupersedeAsync(current.ClientId, current.Code, next.Id, http.RequestAborted);
        if (next.IsDefault)
        {
            await repository.ClearDefaultAsync(current.ClientId, current.Code, http.RequestAborted);
        }

        await events.LogAsync("AssetCriticalityTemplate", next.Id, $"{next.Name} v{next.Version}", EventAction.Updated, http.RequestAborted);
        return Results.Ok(TemplateResponse.From(next));
    }

    // Retire: the template is no longer offered for new assessments; every version is kept.
    private static async Task<IResult> RetireTemplateAsync(string id, HttpContext http, AssetCriticalityTemplateRepository repository, IEventLogger events)
    {
        var template = await repository.GetByIdAsync(id, http.RequestAborted);
        if (template is null || template.IsDeleted)
        {
            return Results.NotFound();
        }

        if (template.IsDefault)
        {
            return Results.Conflict(new { message = "The default template cannot be retired. Make another template the default first." });
        }

        template.Status = "Inactive";
        template.UpdatedBy = UserOf(http);
        template.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(id, template, http.RequestAborted);

        await events.LogAsync("AssetCriticalityTemplate", template.Id, $"{template.Name} v{template.Version}", EventAction.Deleted, http.RequestAborted);
        return Results.Ok(TemplateResponse.From(template));
    }

    // ---------------------------------------------------------------- approval matrix

    private static async Task<IResult> ListApprovalRulesAsync(HttpContext http, AssetCriticalityApprovalRuleRepository repository, AssetCriticalityConfigSeeder seeder)
    {
        await SeedAsync(http, seeder);
        return Results.Ok((await repository.ListAsync(ClientOf(http), http.RequestAborted)).Select(ApprovalRuleResponse.From));
    }

    private static async Task<IResult> UpdateApprovalRuleAsync(string scenario, ApprovalRuleRequest request, HttpContext http, AssetCriticalityApprovalRuleRepository repository, IEventLogger events)
    {
        var rule = await repository.GetByScenarioAsync(ClientOf(http), scenario, http.RequestAborted);
        if (rule is null)
        {
            return Results.NotFound();
        }

        var permission = request.ApproverPermission?.Trim().ToLowerInvariant() ?? rule.ApproverPermission;
        if (!AssetCriticalityApproverPermissions.All.Contains(permission))
        {
            return Problem("ApproverPermission", $"Approver permission must be one of: {string.Join(", ", AssetCriticalityApproverPermissions.All)}.");
        }

        var type = request.ApprovalType is null ? rule.ApprovalType : AssetCriticalityApproverPermissions.ApprovalTypes.FirstOrDefault(t => t.Equals(request.ApprovalType.Trim(), StringComparison.OrdinalIgnoreCase));
        if (type is null)
        {
            return Problem("ApprovalType", "Approval type must be Any or All.");
        }

        rule.ApproverPermission = permission;
        rule.ApprovalType = type;
        rule.ApproverRoles = request.ApproverRoles is null ? rule.ApproverRoles : request.ApproverRoles.Select(r => r.Trim()).Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        rule.RequiresJustification = request.RequiresJustification ?? rule.RequiresJustification;
        rule.Description = request.Description is null ? rule.Description : request.Description.Trim();
        rule.UpdatedBy = UserOf(http);
        rule.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(rule.Id, rule, http.RequestAborted);

        await events.LogAsync("AssetCriticalityApprovalRule", rule.Id, rule.Label, EventAction.Updated, http.RequestAborted);
        return Results.Ok(ApprovalRuleResponse.From(rule));
    }

    // ---------------------------------------------------------------- reassessment rules

    private static async Task<IResult> ListReassessmentRulesAsync(HttpContext http, AssetCriticalityReassessmentRuleRepository repository, AssetCriticalityConfigSeeder seeder)
    {
        await SeedAsync(http, seeder);
        var rules = await repository.ListAsync(ClientOf(http), http.RequestAborted);
        return Results.Ok(rules.OrderByDescending(r => CriticalityAccess.LevelRank(r.LevelName)).ThenBy(r => r.LevelName).Select(ReassessmentRuleResponse.From));
    }

    private static async Task<IResult> UpdateReassessmentRuleAsync(string levelName, ReassessmentRuleRequest request, HttpContext http, AssetCriticalityReassessmentRuleRepository repository, IEventLogger events)
    {
        if (request.ReviewIntervalMonths is null or < 1 or > 120)
        {
            return Problem("ReviewIntervalMonths", "The review interval must be between 1 and 120 months.");
        }

        var clientId = ClientOf(http);
        var rule = await repository.GetByLevelAsync(clientId, levelName, http.RequestAborted);
        if (rule is null)
        {
            rule = await repository.CreateAsync(new AssetCriticalityReassessmentRule
            {
                LevelName = levelName.Trim(), ReviewIntervalMonths = request.ReviewIntervalMonths.Value,
                ClientId = clientId, TenantId = Claim(http, A360ClaimTypes.TenantId), CreatedBy = UserOf(http), CreatedAt = DateTime.UtcNow, Status = "Active"
            }, http.RequestAborted);
        }
        else
        {
            rule.ReviewIntervalMonths = request.ReviewIntervalMonths.Value;
            rule.UpdatedBy = UserOf(http);
            rule.UpdatedAt = DateTime.UtcNow;
            await repository.UpdateAsync(rule.Id, rule, http.RequestAborted);
        }

        await events.LogAsync("AssetCriticalityReassessmentRule", rule.Id, rule.LevelName, EventAction.Updated, http.RequestAborted);
        return Results.Ok(ReassessmentRuleResponse.From(rule));
    }
}
