namespace Odary.Api.Common.Authorization;

public class ClaimDefinition
{
    public string ClaimName { get; set; } = string.Empty;
    public string[] DefaultAssignments { get; set; } = [];
} 