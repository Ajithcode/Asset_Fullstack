using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class InspectionPlanEndpoints
{
    private const string SequenceName = InspectionCodes.InspectionPlanSequence;
    private const string PlanCodePrefix = "PLN";

    public static RouteGroupBuilder MapInspectionPlanEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/inspection-plans").WithTags("InspectionPlans");

        group.MapGet("", GetInspectionPlansAsync).WithName("GetInspectionPlans");
        group.MapGet("/{id}", GetInspectionPlanByIdAsync).WithName("GetInspectionPlanById");
        group.MapPost("", CreateInspectionPlanAsync).WithName("CreateInspectionPlan");
        group.MapPut("/{id}", UpdateInspectionPlanAsync).WithName("UpdateInspectionPlan");
        group.MapDelete("/{id}", DeleteInspectionPlanAsync).WithName("DeleteInspectionPlan");

        return group;
    }

    private static async Task<IResult> GetInspectionPlansAsync(
        IInspectionPlanRepository repository,
        CancellationToken cancellationToken)
    {
        var plans = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(plans.Select(InspectionPlanResponse.FromEntity));
    }

    private static async Task<IResult> GetInspectionPlanByIdAsync(
        string id,
        IInspectionPlanRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection plan id." });
        }

        var plan = await repository.GetByIdAsync(id, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionPlan", plan.PlanCode, plan.PlanName, EventAction.Viewed, cancellationToken);
        return Results.Ok(InspectionPlanResponse.FromEntity(plan));
    }

    private static async Task<IResult> CreateInspectionPlanAsync(
        CreateInspectionPlanRequest request,
        IInspectionPlanRepository repository,
        ISequenceGenerator sequenceGenerator,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var planCode = await InspectionCodes.NextAsync(sequenceGenerator, SequenceName, PlanCodePrefix, cancellationToken);

        var plan = await repository.CreateAsync(request.ToEntity(planCode), cancellationToken);

        await eventLogger.LogAsync("InspectionPlan", plan.PlanCode, plan.PlanName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/inspection-plans/{plan.Id}", InspectionPlanResponse.FromEntity(plan));
    }

    private static async Task<IResult> UpdateInspectionPlanAsync(
        string id,
        UpdateInspectionPlanRequest request,
        IInspectionPlanRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection plan id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var plan = await repository.GetByIdAsync(id, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(plan);

        var updated = await repository.UpdateAsync(id, plan, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionPlan", plan.PlanCode, plan.PlanName, EventAction.Updated, cancellationToken);
        return Results.Ok(InspectionPlanResponse.FromEntity(plan));
    }

    private static async Task<IResult> DeleteInspectionPlanAsync(
        string id,
        IInspectionPlanRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection plan id." });
        }

        var plan = await repository.GetByIdAsync(id, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionPlan", plan.PlanCode, plan.PlanName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
