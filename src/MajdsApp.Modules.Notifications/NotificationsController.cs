using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Notifications;

[Authorize]
public class NotificationsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("list")]
    public async Task<ResponseDto<PagedResponse<NotificationDto>>> List([FromQuery] PagedRequest request) =>
        Ok(await mediator.Send(new ListMyNotificationsQuery(request)));

    [HttpGet("unread-count")]
    public async Task<ResponseDto<int>> UnreadCount() => Ok(await mediator.Send(new GetUnreadCountQuery()));

    [HttpPost("mark-read")]
    public async Task<ResponseDto<object?>> MarkRead([FromBody] MarkNotificationsReadCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpGet("subscriptions/get")]
    public async Task<ResponseDto<IReadOnlyList<SubscriptionDto>>> GetSubscriptions() =>
        Ok(await mediator.Send(new GetMySubscriptionsQuery()));

    [HttpPost("subscriptions/update")]
    public async Task<ResponseDto<object?>> UpdateSubscriptions([FromBody] UpdateMySubscriptionsCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("test-channel")]
    public async Task<ResponseDto<object?>> TestChannel()
    {
        await mediator.Send(new TestEmailChannelCommand());
        return Ok<object?>(null);
    }

    [HttpPost("send")]
    public async Task<ResponseDto<object?>> Send([FromBody] SendNotificationCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }
}
