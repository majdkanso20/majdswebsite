using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Hr;

[Authorize]
public class DepartmentsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("list")]
    public async Task<ResponseDto<PagedResponse<DepartmentDto>>> List([FromQuery] PagedRequest request) =>
        Ok(await mediator.Send(new ListDepartmentsQuery(request)));

    [HttpGet("list-all")]
    public async Task<ResponseDto<IReadOnlyList<DepartmentDto>>> ListAll() => Ok(await mediator.Send(new ListAllDepartmentsQuery()));

    [HttpPost("create")]
    public async Task<ResponseDto<int>> Create([FromBody] CreateDepartmentCommand command) => Ok(await mediator.Send(command));

    [HttpPost("update")]
    public async Task<ResponseDto<object?>> Update([FromBody] UpdateDepartmentCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("delete")]
    public async Task<ResponseDto<object?>> Delete([FromBody] DeleteDepartmentCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }
}
