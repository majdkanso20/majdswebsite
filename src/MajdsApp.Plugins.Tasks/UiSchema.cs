using MediatR;

namespace MajdsApp.Plugins.Tasks;

// Minimal, hand-rolled stand-in for P5's "metadata/dynamic-template renderer" option (FR-PLUG-015):
// rather than shipping a compiled Angular bundle (Native Federation), this CRUD-only plugin ships
// just data + this schema, and the host's ONE generic PluginCrudPage renders it using the same
// shared DataGrid/dialog components every other admin screen uses.
public record UiFieldSchema(string Key, string Label, string Type, bool Required, IReadOnlyList<string>? Options = null);
public record UiColumnSchema(string Key, string Header);

public record UiSchemaDto(
    string Title,
    IReadOnlyList<UiColumnSchema> Columns,
    IReadOnlyList<UiFieldSchema> Fields,
    string ViewPermission,
    string CreatePermission,
    string EditPermission,
    string DeletePermission);

public record GetTasksUiSchemaQuery : IRequest<UiSchemaDto>;

public class GetTasksUiSchemaQueryHandler : IRequestHandler<GetTasksUiSchemaQuery, UiSchemaDto>
{
    public Task<UiSchemaDto> Handle(GetTasksUiSchemaQuery request, CancellationToken ct) => Task.FromResult(new UiSchemaDto(
        Title: "Tasks",
        Columns:
        [
            new UiColumnSchema("title", "Title"),
            new UiColumnSchema("status", "Status"),
            new UiColumnSchema("dueDate", "Due date")
        ],
        Fields:
        [
            new UiFieldSchema("title", "Title", "text", Required: true),
            new UiFieldSchema("description", "Description", "textarea", Required: false),
            new UiFieldSchema("status", "Status", "select", Required: true, Options: Enum.GetNames<TaskItemStatus>()),
            new UiFieldSchema("dueDate", "Due date", "date", Required: false)
        ],
        ViewPermission: Permissions.Tasks.View,
        CreatePermission: Permissions.Tasks.Create,
        EditPermission: Permissions.Tasks.Edit,
        DeletePermission: Permissions.Tasks.Delete));
}
