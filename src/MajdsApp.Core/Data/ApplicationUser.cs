using MajdsApp.SharedKernel.Data;
using Microsoft.AspNetCore.Identity;

namespace MajdsApp.Data;

/// <summary>
/// Extends (never forks) the existing Identity user with the fields F-Users needs — administrative
/// CRUD, soft delete, and audit trail — while everything already built (2FA, external logins, email
/// confirmation) keeps working unchanged, since those only ever depended on <see cref="IdentityUser"/>'s
/// own members.
/// </summary>
public class ApplicationUser : IdentityUser, IAuditable, ISoftDelete
{
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? ProfilePictureId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}
