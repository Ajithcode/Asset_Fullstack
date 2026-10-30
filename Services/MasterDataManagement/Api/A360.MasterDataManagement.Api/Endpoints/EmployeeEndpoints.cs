using EmployeeEntity = A360.MasterDataManagement.Domain.Entities.Employee;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class EmployeeEndpoints
{
    public static RouteGroupBuilder MapEmployeeEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/employees")
            .WithTags("Employees");

        group.MapGet("", GetEmployeesAsync)
            .WithName("GetEmployees");

        group.MapGet("/{id}", GetEmployeeByIdAsync)
            .WithName("GetEmployeeById");

        group.MapPost("", CreateEmployeeAsync)
            .WithName("CreateEmployee");

        group.MapPut("/{id}", UpdateEmployeeAsync)
            .WithName("UpdateEmployee");

        group.MapPatch("/{id}/status", UpdateEmployeeStatusAsync)
            .WithName("UpdateEmployeeStatus");

        group.MapDelete("/{id}", DeleteEmployeeAsync)
            .WithName("DeleteEmployee");

        return group;
    }

    private static async Task<IResult> GetEmployeesAsync(
        string? search,
        string? status,
        string? departmentId,
        string? designation,
        string? clientId,
        IEmployeeRepository repository,
        CancellationToken cancellationToken)
    {
        var employees = await repository.SearchAsync(
            new EmployeeFilter(search, status, departmentId, designation, clientId),
            cancellationToken);

        return Results.Ok(
            employees.Select(EmployeeResponse.FromEntity));
    }

    private static async Task<IResult> GetEmployeeByIdAsync(
        string id,
        IEmployeeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid employee id." });
        }

        var employee = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (employee is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Employee",
            employee.Id,
            employee.EmployeeName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(EmployeeResponse.FromEntity(employee));
    }

    private static async Task<IResult> CreateEmployeeAsync(
        CreateEmployeeRequest request,
        IEmployeeRepository repository,
        IDepartmentRepository departments,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var employee = request.ToEntity();

        var existing = await repository.GetByEmployeeIdAsync(
            employee.EmployeeId,
            employee.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"An employee with employee id '{employee.EmployeeId}' already exists." });
        }

        var departmentError = await ResolveDepartmentAsync(
            employee,
            departments,
            cancellationToken);

        if (departmentError is not null)
        {
            return departmentError;
        }

        var managerError = await MasterDataReferenceResolver.ResolveEmployeeAsync(
            employee.ReportingManager,
            repository,
            cancellationToken);

        if (managerError is not null)
        {
            return managerError;
        }

        employee = await repository.CreateAsync(
            employee,
            cancellationToken);

        await eventLogger.LogAsync(
            "Employee",
            employee.Id,
            employee.EmployeeName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/employees/{employee.Id}",
            EmployeeResponse.FromEntity(employee));
    }

    private static async Task<IResult> UpdateEmployeeAsync(
        string id,
        UpdateEmployeeRequest request,
        IEmployeeRepository repository,
        IDepartmentRepository departments,
        IWarehouseRepository warehouses,
        ITeamRepository teams,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid employee id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var employee = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (employee is null)
        {
            return Results.NotFound();
        }

        var previousName = employee.EmployeeName;

        request.ApplyTo(employee);

        var existing = await repository.GetByEmployeeIdAsync(
            employee.EmployeeId,
            employee.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"An employee with employee id '{employee.EmployeeId}' already exists." });
        }

        var departmentError = await ResolveDepartmentAsync(
            employee,
            departments,
            cancellationToken);

        if (departmentError is not null)
        {
            return departmentError;
        }

        if (employee.ReportingManager?.Id == id)
        {
            return Results.BadRequest(
                new { message = "An employee can't report to themselves." });
        }

        var managerError = await MasterDataReferenceResolver.ResolveEmployeeAsync(
            employee.ReportingManager,
            repository,
            cancellationToken);

        if (managerError is not null)
        {
            return managerError;
        }

        var updated = await repository.UpdateAsync(
            id,
            employee,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != employee.EmployeeName)
        {
            await departments.UpdateDepartmentHeadNameAsync(
                id,
                employee.EmployeeName,
                cancellationToken);

            await warehouses.UpdateManagerNameAsync(
                id,
                employee.EmployeeName,
                cancellationToken);

            await repository.UpdateReportingManagerNameAsync(
                id,
                employee.EmployeeName,
                cancellationToken);

            await teams.UpdateTeamLeaderNameAsync(
                id,
                employee.EmployeeName,
                cancellationToken);

            await teams.UpdateEmployeeMemberNameAsync(
                id,
                employee.EmployeeName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Employee",
            employee.Id,
            employee.EmployeeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(EmployeeResponse.FromEntity(employee));
    }

    private static async Task<IResult> UpdateEmployeeStatusAsync(
        string id,
        UpdateEmployeeStatusRequest request,
        IEmployeeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid employee id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var employee = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (employee is null)
        {
            return Results.NotFound();
        }

        employee.Status = request.Status;
        employee.UpdatedBy = request.UpdatedBy;
        employee.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            employee,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Employee",
            employee.Id,
            employee.EmployeeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(EmployeeResponse.FromEntity(employee));
    }

    private static async Task<IResult> DeleteEmployeeAsync(
        string id,
        IEmployeeRepository repository,
        IDepartmentRepository departments,
        IWarehouseRepository warehouses,
        ITeamRepository teams,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid employee id." });
        }

        var employee = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (employee is null)
        {
            return Results.NotFound();
        }

        if (await departments.AnyWithDepartmentHeadAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "This employee is a department head. Choose another head for that department first." });
        }

        if (await warehouses.AnyWithManagerAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "This employee manages a warehouse. Choose another manager for that warehouse first." });
        }

        if (await teams.AnyWithTeamLeaderAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "This employee leads a team. Choose another team leader first." });
        }

        if (await repository.AnyWithReportingManagerAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Other employees report to this employee. Change their manager first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await teams.RemoveEmployeeMemberAsync(
            id,
            cancellationToken);

        await eventLogger.LogAsync(
            "Employee",
            employee.Id,
            employee.EmployeeName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult?> ResolveDepartmentAsync(
        EmployeeEntity employee,
        IDepartmentRepository departments,
        CancellationToken cancellationToken)
    {
        return employee.Department is null
            ? null
            : await MasterDataReferenceResolver.ResolveDepartmentsAsync(
                [employee.Department],
                departments,
                cancellationToken);
    }
}
