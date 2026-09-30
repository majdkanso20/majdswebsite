using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Hr;

[Authorize]
public class EmployeesController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("list")]
    public async Task<ResponseDto<PagedResponse<EmployeeDto>>> List([FromQuery] PagedRequest request) =>
        Ok(await mediator.Send(new ListEmployeesQuery(request)));

    [HttpGet("get")]
    public async Task<ResponseDto<EmployeeDto>> Get([FromQuery] int id) => Ok(await mediator.Send(new GetEmployeeQuery(id)));

    [HttpPost("create")]
    public async Task<ResponseDto<int>> Create([FromBody] EmployeeInput employee) => Ok(await mediator.Send(new CreateEmployeeCommand(employee)));

    [HttpPost("update")]
    public async Task<ResponseDto<object?>> Update([FromBody] UpdateEmployeeRequest request)
    {
        await mediator.Send(new UpdateEmployeeCommand(request.Id, request.Employee));
        return Ok<object?>(null);
    }

    [HttpPost("delete")]
    public async Task<ResponseDto<object?>> Delete([FromBody] DeleteEmployeeCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }
}

public record UpdateEmployeeRequest(int Id, EmployeeInput Employee);
