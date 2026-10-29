using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;

namespace A360.Administration.Api.Endpoints;

public static class UserEndpoints
{
    private const string EntityType = "User";

    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users").WithTags("Users");

        group.MapGet("/{userId}", GetUserByIdAsync).WithName("GetUserById");
        group.MapPost("", CreateUserAsync).WithName("CreateUser");

        return group;
    }

    private static async Task<IResult> GetUserByIdAsync(
        string userId,
        IUserRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var user = await repository.GetByUserIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, user.Id, user.Name, EventAction.Viewed, cancellationToken);

        return Results.Ok(UserResponse.FromEntity(user));
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        IUserRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var user = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.UsernameExistsAsync(user.Username, user.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Username '{user.Username}' already exists" });
        }

        if (await repository.EmailExistsAsync(user.Email, user.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Email '{user.Email}' already exists" });
        }

        user = await repository.CreateAsync(user, cancellationToken);

        await eventLogger.LogAsync(EntityType, user.Id, user.Name, EventAction.Created, cancellationToken);

        return Results.Created($"/api/users/{user.UserId}", UserResponse.FromEntity(user));
    }
}
