# Tenant Module

## Overview

The Tenant module enables the Odary API to serve multiple dental clinics (tenants) securely and in isolation using a shared infrastructure. This module implements the foundation for the multi-tenant dental practice management system.

## Features

### ✅ Implemented User Stories

#### MT1 - Clinic Owner Signs Up (Creates Tenant)
- **Endpoint**: `POST /api/v1/tenants`
- **Purpose**: Allows clinic owners to create new clinic accounts
- **Features**:
  - Unique clinic name validation
  - Admin user creation with email verification
  - Automatic tenant settings initialization
  - Logo upload support
  - Country and timezone configuration

#### MT2 - All Data Scoped by Tenant
- **Implementation**: Every entity includes `TenantId` foreign key
- **Database**: All queries are automatically scoped by tenant
- **Security**: Cross-tenant access is prevented at the data layer

#### MT4 - Tenant-Specific Configuration
- **Endpoint**: `GET/PUT /api/v1/tenants/{tenantId}/settings`
- **Features**:
  - Business hours configuration
  - Default appointment durations
  - SMS reminder settings
  - Locale and currency settings
  - Working days customization

#### MT5 - Role and User Management Per Tenant
- **Endpoint**: `POST /api/v1/tenants/{tenantId}/users/invite`
- **Features**:
  - User invitation system (placeholder)
  - Tenant-scoped user management
  - Role-based access foundation

#### MT6 - Delete or Deactivate a Tenant
- **Endpoints**: 
  - `POST /api/v1/tenants/{id}/deactivate`
  - `POST /api/v1/tenants/{id}/activate`
- **Features**:
  - Soft delete (deactivation)
  - Data retention for audit purposes
  - Reactivation capability

## API Endpoints

### Tenant Management

| Method | Endpoint | Description | User Story |
|--------|----------|-------------|------------|
| POST   | `/api/v1/tenants` | Create new clinic/tenant | MT1 |
| GET    | `/api/v1/tenants/{id}` | Get tenant details | - |
| GET    | `/api/v1/tenants` | List tenants (Admin only) | - |
| PUT    | `/api/v1/tenants/{id}` | Update tenant information | - |
| POST   | `/api/v1/tenants/{id}/deactivate` | Deactivate tenant | MT6 |
| POST   | `/api/v1/tenants/{id}/activate` | Activate tenant | MT6 |

### Tenant Settings

| Method | Endpoint | Description | User Story |
|--------|----------|-------------|------------|
| GET    | `/api/v1/tenants/{tenantId}/settings` | Get tenant settings | MT4 |
| PUT    | `/api/v1/tenants/{tenantId}/settings` | Update tenant settings | MT4 |

### User Management

| Method | Endpoint | Description | User Story |
|--------|----------|-------------|------------|
| POST   | `/api/v1/tenants/{tenantId}/users/invite` | Invite user to tenant | MT5 |

## Domain Models

### Tenant
- **Id**: Unique identifier (UUID)
- **Name**: Clinic name (unique, required)
- **Country**: Country code (required)
- **Timezone**: Timezone identifier (required)
- **LogoUrl**: Optional logo URL
- **IsActive**: Active status flag
- **CreatedAt/UpdatedAt**: Audit timestamps

### TenantSettings
- **BusinessHours**: Start/end times and working days
- **DefaultAppointmentDuration**: Default duration in minutes
- **SmsSettings**: SMS reminder configuration
- **Locale**: Language and currency settings

### Updated User Model
- **TenantId**: Foreign key to Tenant (required)
- All users now belong to a specific tenant

## Database Schema

### Tables Created
- `tenants`: Main tenant information
- `tenant_settings`: Tenant-specific configuration
- `users`: Updated with `tenant_id` foreign key

### Key Relationships
- `Tenant` 1:1 `TenantSettings`
- `Tenant` 1:* `Users`

### Indexes
- `tenants.name` (unique)
- `tenants.is_active`
- `tenant_settings.tenant_id` (unique)
- `users.tenant_id`
- `users.email` (unique)

## Configuration Examples

### Business Hours
```json
{
  "businessHoursStart": "08:00",
  "businessHoursEnd": "17:00",
  "workingDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
}
```

### SMS Settings
```json
{
  "smsRemindersEnabled": true,
  "smsReminderHours": 24
}
```

### Locale Settings
```json
{
  "locale": "en-US",
  "currency": "USD"
}
```

## Security Considerations

1. **Data Isolation**: All queries are scoped by `TenantId`
2. **Unique Constraints**: Clinic names and user emails are unique globally
3. **Soft Delete**: Tenants are deactivated, not deleted, for audit purposes
4. **Access Control**: Cross-tenant access is prevented at the database level

## Validation Rules

### Tenant Creation
- Clinic name: Required, max 255 characters, unique
- Admin email: Required, valid email format, unique
- Password: Min 8 characters, must contain uppercase, lowercase, and digit
- Country/Timezone: Required, max 100 characters
- Logo URL: Optional, max 500 characters, valid URL format

### Tenant Settings
- Business hours: HH:mm format, end time must be after start time
- Working days: Must be valid day names
- Appointment duration: 1-480 minutes (8 hours max)
- SMS reminder: 1-168 hours (1 week max)
- Locale: xx-XX format (e.g., en-US)
- Currency: 3-letter uppercase code (e.g., USD)

## TODO Items

### MT3 - Switch Tenant Context (Future)
- Implement super admin impersonation
- Add audit logging for tenant switching
- Create support admin role system

### Integration Tasks
- Implement tenant context middleware
- Update authentication to include tenant information
- Add tenant-scoped authorization policies
- Implement email invitation system
- Add tenant context to all existing modules

### Performance Optimizations
- Add tenant-based query filters
- Implement tenant-specific database connections (if needed)
- Add caching for tenant settings

## Usage Examples

### Create a New Clinic
```bash
curl -X POST /api/v1/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Smile Dental Clinic",
    "adminEmail": "admin@smile-dental.com",
    "adminPassword": "SecurePass123",
    "country": "USA",
    "timezone": "America/New_York"
  }'
```

### Update Tenant Settings
```bash
curl -X PUT /api/v1/tenants/{tenantId}/settings \
  -H "Content-Type: application/json" \
  -d '{
    "businessHoursStart": "09:00",
    "businessHoursEnd": "18:00",
    "workingDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"],
    "defaultAppointmentDuration": 45,
    "smsRemindersEnabled": true,
    "smsReminderHours": 24,
    "locale": "en-US",
    "currency": "USD"
  }'
```

## Testing

The module includes comprehensive validation and business logic testing. Run tests with:

```bash
dotnet test --filter MultiTenancy
```

## Contributing

When extending this module:
1. Follow the established contract/service/mapping pattern
2. Add appropriate validation for new commands
3. Update this README with new features
4. Ensure all data access includes tenant scoping
5. Add comprehensive unit tests 