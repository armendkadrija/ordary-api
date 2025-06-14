using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Odary.Api.Common.Interfaces;
using Odary.Api.Constants;
using System.Text.Json;
using Odary.Api.Extensions;

namespace Odary.Api.Common.Services;

public interface IAuditService
{
    Task<List<Domain.AuditLog>> CreateAuditLogsAsync(IEnumerable<EntityEntry> entries);
}

public class AuditService(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider) : IAuditService
{
    public Task<List<Domain.AuditLog>> CreateAuditLogsAsync(IEnumerable<EntityEntry> entries)
    {
        var auditLogs = new List<Domain.AuditLog>();
        var currentUserId = GetCurrentUserId();
        var ipAddress = GetCurrentUserIpAddress();
        var userAgent = GetCurrentUserAgent();

        foreach (var entry in entries)
        {
            if (entry.Entity is not IAuditable auditableEntity)
                continue;

            var entityId = auditableEntity.Id;
            var entityType = auditableEntity.GetEntityType();

            // Skip if we don't have essential audit info
            if (string.IsNullOrEmpty(currentUserId))
                continue;

            string action;
            string? oldValues = null;
            string? newValues = null;

            switch (entry.State)
            {
                case EntityState.Added:
                    action = Actions.CREATE;
                    newValues = JsonSerializer.Serialize(auditableEntity.GetAuditableProperties());
                    break;

                case EntityState.Modified:
                    action = Actions.UPDATE;
                    
                    // Get original values
                    var originalValues = new Dictionary<string, object?>();
                    var currentValues = new Dictionary<string, object?>();
                    
                    foreach (var property in entry.Properties)
                    {
                        var propertyName = property.Metadata.Name;
                        
                        // Only include properties that are part of auditable properties
                        var auditableProperties = auditableEntity.GetAuditableProperties();
                        if (!auditableProperties.ContainsKey(propertyName))
                            continue;

                        if (property.IsModified)
                        {
                            originalValues[propertyName] = property.OriginalValue;
                            currentValues[propertyName] = property.CurrentValue;
                        }
                    }
                    
                    // Only create audit log if there are actual changes
                    if (originalValues.Any())
                    {
                        oldValues = JsonSerializer.Serialize(originalValues);
                        newValues = JsonSerializer.Serialize(currentValues);
                    }
                    else
                    {
                        continue; // Skip if no auditable changes
                    }
                    break;

                case EntityState.Deleted:
                    action = Actions.DELETE;
                    oldValues = JsonSerializer.Serialize(auditableEntity.GetAuditableProperties());
                    break;

                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    continue; // Skip unchanged entities
            }

            var auditLog = new Domain.AuditLog(
                currentUserId,
                action,
                entityType,
                entityId,
                oldValues,
                newValues,
                ipAddress,
                userAgent
            );

            auditLogs.Add(auditLog);
        }

        return Task.FromResult(auditLogs);
    }

    private string? GetCurrentUserId()
    {
        try
        {
            var user = httpContextAccessor.HttpContext?.User;
            return user?.GetUserId();
        }
        catch
        {
            return null; // Return null if unable to get user context (e.g., during seeding)
        }
    }

    private string? GetCurrentUserIpAddress()
    {
        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            return httpContext?.Connection.RemoteIpAddress?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private string? GetCurrentUserAgent()
    {
        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            return httpContext?.Request.Headers.UserAgent.ToString();
        }
        catch
        {
            return null;
        }
    }
} 