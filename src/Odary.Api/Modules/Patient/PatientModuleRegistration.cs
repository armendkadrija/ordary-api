using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Odary.Api.Modules.Patient;

public static class PatientModuleRegistration
{
    public static IServiceCollection AddPatientModule(this IServiceCollection services)
    {
        // Register validators
        services.AddScoped<IValidator<PatientCommands.V1.CreatePatient>, Validators.CreatePatientValidator>();
        services.AddScoped<IValidator<PatientCommands.V1.UpdatePatient>, Validators.UpdatePatientValidator>();
        services.AddScoped<IValidator<PatientCommands.V1.ArchivePatient>, Validators.ArchivePatientValidator>();

        // Register services
        services.AddScoped<IPatientService, PatientService>();

        return services;
    }

    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/patients").WithTags("Patients");

        // Create patient
        group.MapPost("/", async (
            [FromBody] PatientCommands.V1.CreatePatient command,
            IPatientService patientService,
            CancellationToken cancellationToken) =>
        {
            var result = await patientService.CreatePatientAsync(command, cancellationToken);
            return Results.Created($"/api/v1/patients/{result.Id}", result);
        })
        .WithName("CreatePatient")
        .WithSummary("Create a new patient")
        .Produces<PatientResources.V1.CreatePatientResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        // Get patient by ID
        group.MapGet("/{id}", async (
            string id,
            IPatientService patientService,
            CancellationToken cancellationToken) =>
        {
            var query = new PatientQueries.V1.GetPatient(id);
            var result = await patientService.GetPatientAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetPatient")
        .WithSummary("Get patient by ID")
        .Produces<PatientQueries.V1.GetPatient.Response>();

        // Get patients with filtering and pagination
        group.MapGet("/", async (
            [AsParameters] PatientQueries.V1.GetPatients query,
            IPatientService patientService,
            CancellationToken cancellationToken) =>
        {
            var result = await patientService.GetPatientsAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetPatients")
        .WithSummary("Get patients with filtering and pagination")
        .Produces<PatientQueries.V1.GetPatients.Response>();

        // Update patient
        group.MapPut("/{id}", async (
            string id,
            [FromBody] PatientCommands.V1.UpdatePatient command,
            IPatientService patientService,
            CancellationToken cancellationToken) =>
        {
            // Create a new command with the ID from the route
            var commandWithId = command with { Id = id };
            var result = await patientService.UpdatePatientAsync(commandWithId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UpdatePatient")
        .WithSummary("Update patient information")
        .Produces<PatientResources.V1.Patient>()
        .ProducesValidationProblem();

        // Archive patient
        group.MapPost("/{id}/archive", async (
            string id,
            [FromBody] PatientCommands.V1.ArchivePatient command,
            IPatientService patientService,
            CancellationToken cancellationToken) =>
        {
            // Create a new command with the ID from the route
            var commandWithId = command with { Id = id };
            var result = await patientService.ArchivePatientAsync(commandWithId, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ArchivePatient")
        .WithSummary("Archive a patient")
        .Produces<PatientResources.V1.Patient>()
        .ProducesValidationProblem();

        // Unarchive patient
        group.MapPost("/{id}/unarchive", async (
            string id,
            IPatientService patientService,
            CancellationToken cancellationToken) =>
        {
            var command = new PatientCommands.V1.UnarchivePatient(id);
            var result = await patientService.UnarchivePatientAsync(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UnarchivePatient")
        .WithSummary("Unarchive a patient")
        .Produces<PatientResources.V1.Patient>();

        return app;
    }
} 