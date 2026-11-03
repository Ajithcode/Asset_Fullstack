using A360.UserAccount.Repository.Repositories;
using RoleEntity = A360.UserAccount.Domain.Entities.Role;

namespace A360.UserAccount.Api.IoC;

/// <summary>
/// Creates the default roles on startup when a role with the same name does not exist yet,
/// so a fresh database can be signed up to straight away.
/// </summary>
public sealed class DefaultRoleSeeder : BackgroundService
{
    private static readonly (string RoleId, string RoleName, string Description)[] DefaultRoles =
    [
        ("R-ADMINISTRATOR", "Administrator", "Full access to every module and setting."),
        ("R-MANAGER", "Manager", "Manages assets, maintenance and reports."),
        ("R-USER", "User", "Default role for self-registered users.")
    ];

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DefaultRoleSeeder> _logger;

    public DefaultRoleSeeder(
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration,
        ILogger<DefaultRoleSeeder> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
            var existingRoles = await roleRepository.GetAllAsync(stoppingToken);

            foreach (var (roleId, roleName, description) in DefaultRoles)
            {
                if (existingRoles.Any(r => !r.IsDeleted && string.Equals(r.RoleName, roleName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                await roleRepository.CreateAsync(new RoleEntity
                {
                    RoleId = roleId,
                    RoleName = roleName,
                    Description = description,
                    CreatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    ClientId = _configuration["Registration:ClientId"],
                    TenantId = _configuration["Registration:TenantId"],
                    Status = "Active",
                    IsDeleted = false,
                    Action = "Created"
                }, stoppingToken);

                _logger.LogInformation("Created default role {RoleName}.", roleName);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Creating the default roles failed. The service will continue running.");
        }
    }
}
