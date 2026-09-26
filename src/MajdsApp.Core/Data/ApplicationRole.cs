using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Data;

/// <summary>Extends the existing Identity role with the fields F-Roles needs (F1/F-ROLE-003).</summary>
public class ApplicationRole : IdentityRole, MajdsApp.SharedKernel.Data.IEntity<string>
{
    public string? DisplayName { get; set; }

    /// <summary>Seeded role; cannot be deleted or renamed (FR-ROLE-003).</summary>
    public bool IsStatic { get; set; }

    /// <summary>Auto-assigned to new users on registration (FR-ROLE-003, AC-ROLE-2).</summary>
    public bool IsDefault { get; set; }
}
