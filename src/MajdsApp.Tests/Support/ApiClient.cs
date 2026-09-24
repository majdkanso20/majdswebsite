using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MajdsApp.Tests.Support;

/// <summary>The platform's <c>ResponseDto</c> envelope as a client sees it (P1).</summary>
public record Envelope<T>(int Code, string? Message, T? Data, string[]? Errors);

/// <summary>A response with its HTTP status and the parsed envelope, so a test asserts on both.</summary>
public record ApiResult<T>(HttpStatusCode Status, Envelope<T>? Body, HttpResponseMessage Raw)
{
    public T? Data => Body is null ? default : Body.Data;
    public int Code => Body?.Code ?? 0;
    public string[] Errors => Body?.Errors ?? [];
}

public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public HttpClient Http => http;

    public Task<ApiResult<T>> GetAsync<T>(string url) => SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, url));

    public Task<ApiResult<T>> PostAsync<T>(string url, object? body = null) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) });

    public Task<ApiResult<object>> PostAsync(string url, object? body = null) => PostAsync<object>(url, body);

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request)
    {
        var response = await http.SendAsync(request);
        Envelope<T>? body = null;
        var text = await response.Content.ReadAsStringAsync();
        if (text.StartsWith('{'))
        {
            try { body = JsonSerializer.Deserialize<Envelope<T>>(text, Json); }
            catch (JsonException) { /* not an envelope (for example a file); status alone is asserted */ }
        }

        return new ApiResult<T>(response.StatusCode, body, response);
    }
}

public record PagedData<T>(List<T> Items, int TotalCount, int Page, int PageSize);
