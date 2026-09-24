using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Plugins.Tasks;

[Authorize]
public class TasksController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("list")]
    public async Task<ResponseDto<PagedResponse<TaskDto>>> List([FromQuery] PagedRequest request) =>
        Ok(await mediator.Send(new ListTasksQuery(request)));

    [HttpGet("get")]
    public async Task<ResponseDto<TaskDto>> Get([FromQuery] Guid id) => Ok(await mediator.Send(new GetTaskQuery(id)));

    [HttpPost("create")]
    public async Task<ResponseDto<Guid>> Create([FromBody] CreateTaskCommand command) => Ok(await mediator.Send(command));

    [HttpPost("update")]
    public async Task<ResponseDto<object?>> Update([FromBody] UpdateTaskCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("delete")]
    public async Task<ResponseDto<object?>> Delete([FromBody] DeleteTaskCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpGet("ui-schema")]
    public async Task<ResponseDto<UiSchemaDto>> UiSchema() => Ok(await mediator.Send(new GetTasksUiSchemaQuery()));
}
