using A360.Dashboard.Api.Contracts;
using A360.Dashboard.Repository.Repositories;

namespace A360.Dashboard.Api.Endpoints;

public static class DashboardAssetEndpoints
{
    public static RouteGroupBuilder MapDashboardAssetEndpoints(this IEndpointRouteBuilder routes)
    {
        // Lives under /api/dashboards so the gateway's existing dashboard route forwards it.
        var group = routes.MapGroup("/api/dashboards/assets").WithTags("Dashboard Assets");

        group.MapGet("", GetAssetsAsync).WithName("GetDashboardAssets");

        return group;
    }

    private static async Task<IResult> GetAssetsAsync(
        IDashboardAssetRepository repository,
        CancellationToken cancellationToken)
    {
        var assets = await repository.GetAsync(cancellationToken);

        return Results.Ok(DashboardAssetListResponse.FromAssets(assets));
    }
}
