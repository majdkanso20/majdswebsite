using MajdsApp.SharedKernel.Features;

namespace MajdsApp.Modules.Hr;

public static class HrFeatures
{
    public static class Flags
    {
        public static readonly FeatureDefinition Hr = new(
            "Hr", "HR: departments and employees", defaultEnabled: true,
            description: "Departments and Employees under the HR menu.");
    }
}
