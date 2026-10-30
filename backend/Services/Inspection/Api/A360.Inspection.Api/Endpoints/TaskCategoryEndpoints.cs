using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class TaskCategoryEndpoints
{
    private const string SequenceName = "task_category";
    private const string CategoryCodePrefix = "TC";

    public static RouteGroupBuilder MapTaskCategoryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/task-categories").WithTags("TaskCategories");

        group.MapGet("", GetTaskCategoriesAsync).WithName("GetTaskCategories");
        group.MapGet("/{id}", GetTaskCategoryByIdAsync).WithName("GetTaskCategoryById");
        group.MapPost("", CreateTaskCategoryAsync).WithName("CreateTaskCategory");
        group.MapPut("/{id}", UpdateTaskCategoryAsync).WithName("UpdateTaskCategory");
        group.MapDelete("/{id}", DeleteTaskCategoryAsync).WithName("DeleteTaskCategory");

        return group;
    }

    private static async Task<IResult> GetTaskCategoriesAsync(
        ITaskCategoryRepository repository,
        CancellationToken cancellationToken)
    {
        var taskCategories = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(taskCategories.Select(TaskCategoryResponse.FromEntity));
    }

    private static async Task<IResult> GetTaskCategoryByIdAsync(
        string id,
        ITaskCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid task category id." });
        }

        var taskCategory = await repository.GetByIdAsync(id, cancellationToken);
        if (taskCategory is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("TaskCategory", taskCategory.CategoryCode, taskCategory.CategoryName, EventAction.Viewed, cancellationToken);
        return Results.Ok(TaskCategoryResponse.FromEntity(taskCategory));
    }

    private static async Task<IResult> CreateTaskCategoryAsync(
        CreateTaskCategoryRequest request,
        ITaskCategoryRepository repository,
        ISequenceGenerator sequenceGenerator,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var categoryCode = $"{CategoryCodePrefix}{nextSequence:D6}";

        var taskCategory = await repository.CreateAsync(request.ToEntity(categoryCode), cancellationToken);

        await eventLogger.LogAsync("TaskCategory", taskCategory.CategoryCode, taskCategory.CategoryName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/task-categories/{taskCategory.Id}", TaskCategoryResponse.FromEntity(taskCategory));
    }

    private static async Task<IResult> UpdateTaskCategoryAsync(
        string id,
        UpdateTaskCategoryRequest request,
        ITaskCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid task category id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var taskCategory = await repository.GetByIdAsync(id, cancellationToken);
        if (taskCategory is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(taskCategory);

        var updated = await repository.UpdateAsync(id, taskCategory, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("TaskCategory", taskCategory.CategoryCode, taskCategory.CategoryName, EventAction.Updated, cancellationToken);
        return Results.Ok(TaskCategoryResponse.FromEntity(taskCategory));
    }

    private static async Task<IResult> DeleteTaskCategoryAsync(
        string id,
        ITaskCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid task category id." });
        }

        var taskCategory = await repository.GetByIdAsync(id, cancellationToken);
        if (taskCategory is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("TaskCategory", taskCategory.CategoryCode, taskCategory.CategoryName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
