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

    /// <summary>The public half of the VAPID key pair a subscribing browser needs; null when push is not set up.</summary>
    [HttpGet("push/vapid-public-key")]
    public async Task<ResponseDto<string?>> VapidPublicKey() => Ok(await mediator.Send(new GetVapidPublicKeyQuery()));

    [HttpPost("push/subscribe")]
    public async Task<ResponseDto<object?>> Subscribe([FromBody] SubscribeToPushCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("push/unsubscribe")]
    public async Task<ResponseDto<object?>> Unsubscribe([FromBody] UnsubscribeFromPushCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    /// <summary>A fresh VAPID key pair for the settings page's "Generate" button — push needs no third-party account, just this.</summary>
    [HttpGet("push/generate-vapid-keys")]
    public async Task<ResponseDto<VapidKeyPairDto>> GenerateVapidKeys() => Ok(await mediator.Send(new GenerateVapidKeysQuery()));
}
