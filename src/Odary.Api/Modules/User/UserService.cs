using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Odary.Api.Common.Database;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Services;
using Odary.Api.Infrastructure.Email;

namespace Odary.Api.Modules.User;

public interface IUserService
{
    Task<UserResources.V1.CreateUserResponse> CreateUserAsync(UserCommands.V1.CreateUser command, CancellationToken cancellationToken = default);
    Task<UserQueries.V1.GetUser.Response> GetUserAsync(UserQueries.V1.GetUser query, CancellationToken cancellationToken = default);
    Task<UserQueries.V1.GetUsers.Response> GetUsersAsync(UserQueries.V1.GetUsers query, CancellationToken cancellationToken = default);
    Task<UserResources.V1.User> UpdateEmailAsync(UserCommands.V1.UpdateEmail command, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(UserCommands.V1.DeleteUser command, CancellationToken cancellationToken = default);

    Task<UserResources.V1.InvitationResponse> InviteUserAsync(UserCommands.V1.InviteUser command, CancellationToken cancellationToken = default);
    Task<UserResources.V1.UserProfile> UpdateUserProfileAsync(UserCommands.V1.UpdateUserProfile command, CancellationToken cancellationToken = default);
    Task LockUserAsync(UserCommands.V1.LockUser command, CancellationToken cancellationToken = default);
    Task UnlockUserAsync(UserCommands.V1.UnlockUser command, CancellationToken cancellationToken = default);
}

public class UserService(
    IValidationService validationService,
    UserManager<Domain.User> userManager,
    OdaryDbContext dbContext,
    ILogger<UserService> logger,
    ICurrentUserService currentUserService,
    IEmailService emailService) : BaseService(currentUserService), IUserService
{
    private const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";

    public async Task<UserResources.V1.CreateUserResponse> CreateUserAsync(
        UserCommands.V1.CreateUser command,
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Validate tenant access for Admin users (can only create users in their own tenant)
        if (CurrentUser.IsAdmin && command.TenantId != CurrentUser.TenantId)
            throw new BusinessException("You can only create users within your own tenant");

        // Validate that the specified tenant exists
        var tenantExists = await dbContext.Tenants
            .AnyAsync(t => t.Id == command.TenantId, cancellationToken);
        
        if (!tenantExists)
            throw new BusinessException($"Tenant with ID {command.TenantId} not found");

        // Check if user already exists
        var existingUser = await userManager.FindByEmailAsync(command.Email);
        if (existingUser != null)
            throw new BusinessException("User with this email already exists");

        // Generate random password
        var generatedPassword = GenerateTemporaryPassword();

        // Create user with UserManager (proper Identity way)
        var user = new Domain.User(
            command.TenantId, 
            command.Email, 
            command.Email.Split('@')[0], // Default first name from email prefix
            "",                         // Default empty last name
            command.Role                // Use role from command
        );
        
        // Apply business logic - set default active state
        user.IsActive = true;
        
        // Use UserManager to create user with generated password
        var result = await userManager.CreateAsync(user, generatedPassword);
        if (!result.Succeeded)
            throw new BusinessException(string.Join(", ", result.Errors.Select(e => e.Description)));

        logger.LogInformation("User created successfully with ID: {UserId} in Tenant: {TenantId}", user.Id, command.TenantId);
        
        return new UserResources.V1.CreateUserResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            GeneratedPassword = generatedPassword
        };
    }

    public async Task<UserQueries.V1.GetUser.Response> GetUserAsync(
        UserQueries.V1.GetUser query,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == query.Id && u.TenantId == CurrentUser.TenantId, cancellationToken);

        if (user == null)
            throw new NotFoundException($"User with ID {query.Id} not found in your tenant");

        // Validate tenant access for Admin users
        if (user.TenantId != null)
            ValidateTenantAccess(user.TenantId, "user");

        return user.ToGetUserResponse();
    }

    public async Task<UserQueries.V1.GetUsers.Response> GetUsersAsync(
        UserQueries.V1.GetUsers query,
        CancellationToken cancellationToken = default)
    {
        // Filter users by current user's tenant
        var usersQuery = dbContext.Users
            .Where(u => u.TenantId == CurrentUser.TenantId)
            .AsQueryable();

        // Apply additional filters
        if (!string.IsNullOrEmpty(query.Email))
        {
            usersQuery = usersQuery.Where(u => u.Email != null && u.Email.Contains(query.Email));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);

        var users = await usersQuery
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return new UserQueries.V1.GetUsers.Response
        {
            Items = users.Select(u => u.ToContract()).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<UserResources.V1.User> UpdateEmailAsync(
        UserCommands.V1.UpdateEmail command,
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == command.Id && u.TenantId == CurrentUser.TenantId, cancellationToken);

        if (user == null)
            throw new NotFoundException($"User with ID {command.Id} not found in your tenant");

        // Validate tenant access for Admin users
        if (user.TenantId != null)
            ValidateTenantModification(user.TenantId, "update", "user");

        // Check if email is already taken by another user within the same tenant
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == command.Email && u.Id != command.Id && u.TenantId == CurrentUser.TenantId, cancellationToken);
        
        if (existingUser != null)
            throw new BusinessException("Email is already taken by another user in your tenant");

        user.Email = command.Email;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User updated successfully with ID: {UserId}", user.Id);
        return user.ToContract();
    }

    public async Task DeleteUserAsync(
        UserCommands.V1.DeleteUser command,
        CancellationToken cancellationToken = default)
    {
        
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == command.Id && (CurrentUser.IsSuperAdmin || u.TenantId == CurrentUser.TenantId), cancellationToken);

        if (user == null)
            throw new NotFoundException($"User with ID {command.Id} not found in your tenant");

        if (user.Id == CurrentUser.UserId)
            throw new BusinessException("Cannot delete your own account");

        // Validate tenant access for Admin users
        if (user.TenantId != null)
            ValidateTenantModification(user.TenantId, "delete", "user");

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User deleted successfully with ID: {UserId}", user.Id);
    }

    // User management methods moved from AuthService

    public async Task<UserResources.V1.InvitationResponse> InviteUserAsync(UserCommands.V1.InviteUser command, CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Validate tenant access for Admin users (SuperAdmins can invite to any tenant)
        if (CurrentUser.IsAdmin && command.TenantId != CurrentUser.TenantId)
            throw new BusinessException("You can only invite users to your own tenant");

        // Validate that the specified tenant exists
        var tenantExists = await dbContext.Tenants
            .AnyAsync(t => t.Id == command.TenantId, cancellationToken);
        
        if (!tenantExists)
            throw new BusinessException($"Tenant with ID {command.TenantId} not found");

        var existingUser = await userManager.FindByEmailAsync(command.Email);
        if (existingUser != null)
            throw new BusinessException("A user with this email already exists");

        var user = new Domain.User(command.TenantId, command.Email, command.FirstName, command.LastName, command.Role)
        {
            IsActive = false // User needs to complete invitation
        };

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            throw new BusinessException(string.Join(", ", result.Errors.Select(e => e.Description)));

        var invitationToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(1);

        // Send invitation email
        try
        {
            var emailCommand = new EmailCommands.V1.SendUserInvitation(
                command.Email,
                command.FirstName,
                command.LastName,
                $"{user.Id}|{invitationToken}",
                expiresAt);
            
            await emailService.SendUserInvitationAsync(emailCommand);
            logger.LogInformation("Invitation email sent successfully to {Email}", command.Email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send invitation email to {Email}, but user was created", command.Email);
        }

        logger.LogInformation("User invitation created successfully with ID: {UserId} in Tenant: {TenantId}", user.Id, command.TenantId);
        
        return new UserResources.V1.InvitationResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            InvitationToken = $"{user.Id}|{invitationToken}",
            ExpiresAt = expiresAt.DateTime
        };
    }

    public async Task<UserResources.V1.UserProfile> UpdateUserProfileAsync(UserCommands.V1.UpdateUserProfile command, CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var user = await userManager.FindByIdAsync(command.Id);
        if (user == null)
            throw new NotFoundException("User not found");

        // Validate tenant access for Admin users
        if (user.TenantId != null)
            ValidateTenantModification(user.TenantId, "update", "user profile");

        user.FirstName = command.FirstName;
        user.LastName = command.LastName;
        user.Role = command.Role;
        user.IsActive = command.IsActive;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new BusinessException(string.Join(", ", result.Errors.Select(e => e.Description)));

        logger.LogInformation("User profile updated successfully with ID: {UserId}", user.Id);
        return user.ToUserProfile();
    }



    public async Task LockUserAsync(UserCommands.V1.LockUser command, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(command.Id);
        if (user == null)
            throw new NotFoundException("User not found");

        if (user.Id == CurrentUser.UserId)
            throw new BusinessException("Cannot lock your own account");

        // Validate tenant access for Admin users
        if (user.TenantId != null)
            ValidateTenantModification(user.TenantId, "lock", "user");

        user.LockedUntil = DateTime.UtcNow.AddYears(1); // Lock indefinitely
        await userManager.UpdateAsync(user);

        logger.LogInformation("User locked successfully with ID: {UserId}", user.Id);
    }

    public async Task UnlockUserAsync(UserCommands.V1.UnlockUser command, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(command.Id);
        if (user == null)
            throw new NotFoundException("User not found");

        // Validate tenant access for Admin users
        if (user.TenantId != null)
            ValidateTenantModification(user.TenantId, "unlock", "user");

        user.LockedUntil = null;
        user.FailedLoginAttempts = 0;
        await userManager.UpdateAsync(user);

        logger.LogInformation("User unlocked successfully with ID: {UserId}", user.Id);
    }

    private static string GenerateTemporaryPassword()
    {
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
} 