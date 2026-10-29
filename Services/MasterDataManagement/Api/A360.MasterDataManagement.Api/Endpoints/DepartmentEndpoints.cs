using DepartmentEntity = A360.MasterDataManagement.Domain.Entities.Department;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class DepartmentEndpoints
{
    public static RouteGroupBuilder MapDepartmentEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/departments")
            .WithTags("Departments");

        group.MapGet("", GetDepartmentsAsync)
            .WithName("GetDepartments");

        group.MapGet("/{id}", GetDepartmentByIdAsync)
            .WithName("GetDepartmentById");

        group.MapPost("", CreateDepartmentAsync)
            .WithName("CreateDepartment");

        group.MapPut("/{id}", UpdateDepartmentAsync)
            .WithName("UpdateDepartment");

        group.MapPatch("/{id}/status", UpdateDepartmentStatusAsync)
            .WithName("UpdateDepartmentStatus");

        group.MapDelete("/{id}", DeleteDepartmentAsync)
            .WithName("DeleteDepartment");

        return group;
    }

    private static async Task<IResult> GetDepartmentsAsync(
        string? search,
        string? status,
        string? businessUnitId,
        string? departmentHead,
        string? employee,
        string? asset,
        string? clientId,
        IDepartmentRepository repository,
        IEmployeeRepository employees,
        CancellationToken cancellationToken)
    {
        // Employees know their department, so the employee filter becomes a list of department ids.
        IReadOnlyCollection<string>? departmentIds = null;

        if (!string.IsNullOrWhiteSpace(employee))
        {
            departmentIds = (await employees.FindByReferenceAsync(employee, cancellationToken))
                .Where(x => x.Department?.Id is not null)
                .Select(x => x.Department!.Id!)
                .Distinct()
                .ToList();
        }

        var departments = await repository.SearchAsync(
            new DepartmentFilter(search, status, businessUnitId, departmentHead, departmentIds, asset, clientId),
            cancellationToken);

        // One query for the employees of every department in the page.
        var departmentEmployees = (await employees.GetByDepartmentsAsync(
                departments.Select(x => x.Id),
                cancellationToken))
            .ToLookup(x => x.Department!.Id);

        return Results.Ok(
            departments.Select(x => DepartmentResponse.FromEntity(x, ToEmployeeList(departmentEmployees[x.Id]))));
    }

    private static async Task<IResult> GetDepartmentByIdAsync(
        string id,
        IDepartmentRepository repository,
        IEmployeeRepository employees,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid department id." });
        }

        var department = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (department is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Department",
            department.Id,
            department.DepartmentName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(department, employees, cancellationToken));
    }

    private static async Task<IResult> CreateDepartmentAsync(
        CreateDepartmentRequest request,
        IDepartmentRepository repository,
        IBusinessUnitRepository businessUnits,
        IEmployeeRepository employees,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var department = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            department.Code,
            department.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A department with code '{department.Code}' already exists." });
        }

        var businessUnitError = await MasterDataReferenceResolver.ResolveBusinessUnitAsync(
            department.BusinessUnit,
            businessUnits,
            cancellationToken);

        if (businessUnitError is not null)
        {
            return businessUnitError;
        }

        var employeeIds = request.Employees?.Select(x => x.Id!.Trim()).ToList();

        var employeeError = await ResolveEmployeesAsync(
            department,
            employeeIds,
            employees,
            cancellationToken);

        if (employeeError is not null)
        {
            return employeeError;
        }

        department = await repository.CreateAsync(
            department,
            cancellationToken);

        if (employeeIds is not null)
        {
            await employees.SetDepartmentEmployeesAsync(
                department.Id,
                department.DepartmentName,
                employeeIds,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Department",
            department.Id,
            department.DepartmentName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/departments/{department.Id}",
            await ToResponseAsync(department, employees, cancellationToken));
    }

    private static async Task<IResult> UpdateDepartmentAsync(
        string id,
        UpdateDepartmentRequest request,
        IDepartmentRepository repository,
        IBusinessUnitRepository businessUnits,
        ISiteRepository sites,
        IEmployeeRepository employees,
        ITeamRepository teams,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid department id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var department = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (department is null)
        {
            return Results.NotFound();
        }

        var previousName = department.DepartmentName;

        request.ApplyTo(department);

        var existing = await repository.GetByCodeAsync(
            department.Code,
            department.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A department with code '{department.Code}' already exists." });
        }

        var businessUnitError = await MasterDataReferenceResolver.ResolveBusinessUnitAsync(
            department.BusinessUnit,
            businessUnits,
            cancellationToken);

        if (businessUnitError is not null)
        {
            return businessUnitError;
        }

        var employeeIds = request.Employees?.Select(x => x.Id!.Trim()).ToList();

        var employeeError = await ResolveEmployeesAsync(
            department,
            employeeIds,
            employees,
            cancellationToken);

        if (employeeError is not null)
        {
            return employeeError;
        }

        var updated = await repository.UpdateAsync(
            id,
            department,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (employeeIds is not null)
        {
            await employees.SetDepartmentEmployeesAsync(
                id,
                department.DepartmentName,
                employeeIds,
                cancellationToken);
        }

        if (previousName != department.DepartmentName)
        {
            await businessUnits.UpdateDepartmentNameAsync(
                id,
                department.DepartmentName,
                cancellationToken);

            await sites.UpdateDepartmentNameAsync(
                id,
                department.DepartmentName,
                cancellationToken);

            await employees.UpdateDepartmentNameAsync(
                id,
                department.DepartmentName,
                cancellationToken);

            await teams.UpdateDepartmentNameAsync(
                id,
                department.DepartmentName,
                cancellationToken);

            await users.UpdateDepartmentNameAsync(
                id,
                department.DepartmentName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Department",
            department.Id,
            department.DepartmentName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(department, employees, cancellationToken));
    }

    private static async Task<IResult> UpdateDepartmentStatusAsync(
        string id,
        UpdateDepartmentStatusRequest request,
        IDepartmentRepository repository,
        IEmployeeRepository employees,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid department id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var department = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (department is null)
        {
            return Results.NotFound();
        }

        department.Status = request.Status;
        department.UpdatedBy = request.UpdatedBy;
        department.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            department,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Department",
            department.Id,
            department.DepartmentName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(department, employees, cancellationToken));
    }

    private static async Task<IResult> DeleteDepartmentAsync(
        string id,
        IDepartmentRepository repository,
        IBusinessUnitRepository businessUnits,
        ISiteRepository sites,
        IEmployeeRepository employees,
        ITeamRepository teams,
        ISystemUserRepository users,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid department id." });
        }

        var department = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (department is null)
        {
            return Results.NotFound();
        }

        if (await sites.AnyWithDepartmentAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Sites still belong to this department. Delete or move them first." });
        }

        if (await employees.AnyWithDepartmentAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Employees still belong to this department. Move them to another department first." });
        }

        if (await teams.AnyWithDepartmentAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Teams still belong to this department. Delete or move them first." });
        }

        if (await users.AnyWithDepartmentAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Users still belong to this department. Move them to another department first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        // Drop the deleted department from any business unit that listed it.
        await businessUnits.RemoveDepartmentAsync(
            id,
            cancellationToken);

        await eventLogger.LogAsync(
            "Department",
            department.Id,
            department.DepartmentName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks every listed employee exists, and resolves a department head given by id against the employees.
    /// Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveEmployeesAsync(
        DepartmentEntity department,
        IReadOnlyCollection<string>? employeeIds,
        IEmployeeRepository employees,
        CancellationToken cancellationToken)
    {
        var headId = department.DepartmentHead?.Id;
        var idsToCheck = (employeeIds ?? []).Concat(headId is null ? [] : [headId]).Distinct().ToList();

        if (idsToCheck.Count == 0)
        {
            return null;
        }

        var invalid = idsToCheck.FirstOrDefault(x => !MongoObjectId.IsValid(x));
        var found = invalid is null
            ? (await employees.GetByIdsAsync(idsToCheck, cancellationToken)).ToDictionary(x => x.Id)
            : [];

        invalid ??= idsToCheck.FirstOrDefault(x => !found.ContainsKey(x));

        if (invalid is not null)
        {
            return Results.BadRequest(
                new { message = $"Employee '{invalid}' was not found." });
        }

        if (headId is not null)
        {
            department.DepartmentHead!.Name = found[headId].EmployeeName;
        }

        return null;
    }

    private static async Task<DepartmentResponse> ToResponseAsync(
        DepartmentEntity department,
        IEmployeeRepository employees,
        CancellationToken cancellationToken)
    {
        var departmentEmployees = await employees.GetByDepartmentsAsync(
            [department.Id],
            cancellationToken);

        return DepartmentResponse.FromEntity(department, ToEmployeeList(departmentEmployees));
    }

    private static IReadOnlyList<MasterDataReferenceDto> ToEmployeeList(
        IEnumerable<A360.MasterDataManagement.Domain.Entities.Employee> employees)
    {
        return employees
            .Select(x => new MasterDataReferenceDto(x.Id, x.EmployeeName))
            .ToList();
    }
}
