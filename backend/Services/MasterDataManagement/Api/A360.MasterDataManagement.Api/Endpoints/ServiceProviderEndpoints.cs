using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class ServiceProviderEndpoints
{
    public static RouteGroupBuilder MapServiceProviderEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/service-providers")
            .WithTags("ServiceProviders");

        group.MapGet("", GetServiceProvidersAsync)
            .WithName("GetServiceProviders");

        group.MapGet("/{id}", GetServiceProviderByIdAsync)
            .WithName("GetServiceProviderById");

        group.MapPost("", CreateServiceProviderAsync)
            .WithName("CreateServiceProvider");

        group.MapPut("/{id}", UpdateServiceProviderAsync)
            .WithName("UpdateServiceProvider");

        group.MapPatch("/{id}/status", UpdateServiceProviderStatusAsync)
            .WithName("UpdateServiceProviderStatus");

        group.MapDelete("/{id}", DeleteServiceProviderAsync)
            .WithName("DeleteServiceProvider");

        return group;
    }

    private static async Task<IResult> GetServiceProvidersAsync(
        string? search,
        string? status,
        string? country,
        string? serviceType,
        string? clientId,
        IServiceProviderRepository repository,
        CancellationToken cancellationToken)
    {
        var serviceProviders = await repository.SearchAsync(
            new ServiceProviderFilter(search, status, country, serviceType, clientId),
            cancellationToken);

        return Results.Ok(
            serviceProviders.Select(ServiceProviderResponse.FromEntity));
    }

    private static async Task<IResult> GetServiceProviderByIdAsync(
        string id,
        IServiceProviderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid service provider id." });
        }

        var serviceProvider = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (serviceProvider is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ServiceProvider",
            serviceProvider.Id,
            serviceProvider.ServiceProviderName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ServiceProviderResponse.FromEntity(serviceProvider));
    }

    private static async Task<IResult> CreateServiceProviderAsync(
        CreateServiceProviderRequest request,
        IServiceProviderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var serviceProvider = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            serviceProvider.Code,
            serviceProvider.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A service provider with code '{serviceProvider.Code}' already exists." });
        }

        serviceProvider = await repository.CreateAsync(
            serviceProvider,
            cancellationToken);

        await eventLogger.LogAsync(
            "ServiceProvider",
            serviceProvider.Id,
            serviceProvider.ServiceProviderName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/service-providers/{serviceProvider.Id}",
            ServiceProviderResponse.FromEntity(serviceProvider));
    }

    private static async Task<IResult> UpdateServiceProviderAsync(
        string id,
        UpdateServiceProviderRequest request,
        IServiceProviderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid service provider id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var serviceProvider = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (serviceProvider is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(serviceProvider);

        var existing = await repository.GetByCodeAsync(
            serviceProvider.Code,
            serviceProvider.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A service provider with code '{serviceProvider.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            serviceProvider,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ServiceProvider",
            serviceProvider.Id,
            serviceProvider.ServiceProviderName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ServiceProviderResponse.FromEntity(serviceProvider));
    }

    private static async Task<IResult> UpdateServiceProviderStatusAsync(
        string id,
        UpdateServiceProviderStatusRequest request,
        IServiceProviderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid service provider id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var serviceProvider = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (serviceProvider is null)
        {
            return Results.NotFound();
        }

        serviceProvider.Status = request.Status;
        serviceProvider.UpdatedBy = request.UpdatedBy;
        serviceProvider.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            serviceProvider,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ServiceProvider",
            serviceProvider.Id,
            serviceProvider.ServiceProviderName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ServiceProviderResponse.FromEntity(serviceProvider));
    }

    private static async Task<IResult> DeleteServiceProviderAsync(
        string id,
        IServiceProviderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid service provider id." });
        }

        var serviceProvider = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (serviceProvider is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ServiceProvider",
            serviceProvider.Id,
            serviceProvider.ServiceProviderName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
