using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Enrichly.JobAutomation.Tests;

/// <summary>
/// Covers two of the assignment's explicitly-named risky areas that were previously untested:
/// authorization (ownership) and duplicate-request idempotency. Both run against the real
/// controllers, auth pipeline, and a real database via <see cref="ApiTestFactory"/>.
/// </summary>
public sealed class ApiIntegrationTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private sealed record AuthResponseDto(Guid UserId, string Email, string Token);
    private sealed record JobDto(Guid Id, string Name, string TargetUrl, string HttpMethod, bool IsEnabled);

    private async Task<HttpClient> RegisterAndAuthenticateAsync(string emailPrefix)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = $"{emailPrefix}-{Guid.NewGuid():N}@test.local",
            Password = "correct-horse-battery-1"
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private static async Task<JobDto> CreateJobAsync(HttpClient client, string name = "Integration test job")
    {
        var response = await client.PostAsJsonAsync("/api/jobs", new
        {
            Name = name,
            TargetUrl = "https://httpbin.org/post",
            HttpMethod = "POST",
            RequestBody = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JobDto>())!;
    }

    [Fact]
    public async Task UserCannotReadAnotherUsersJob()
    {
        var owner = await RegisterAndAuthenticateAsync("owner");
        var job = await CreateJobAsync(owner);

        var stranger = await RegisterAndAuthenticateAsync("stranger");
        var response = await stranger.GetAsync($"/api/jobs/{job.Id}");

        // Ownership violations return 404, not 403 - the controller doesn't reveal that
        // the job exists at all to a non-owner. This is a deliberate choice, not an oversight;
        // it avoids leaking which job IDs are in use to users who shouldn't see them.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UserCannotRunAnotherUsersJob()
    {
        var owner = await RegisterAndAuthenticateAsync("owner");
        var job = await CreateJobAsync(owner);

        var stranger = await RegisterAndAuthenticateAsync("stranger");
        var response = await stranger.PostAsync($"/api/jobs/{job.Id}/run", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UserCannotSeeAnotherUsersExecutionHistory()
    {
        var owner = await RegisterAndAuthenticateAsync("owner");
        var job = await CreateJobAsync(owner);

        var stranger = await RegisterAndAuthenticateAsync("stranger");
        var response = await stranger.GetAsync($"/api/jobs/{job.Id}/executions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SecondRunNowWhileAnExecutionIsActiveReturnsConflict()
    {
        var client = await RegisterAndAuthenticateAsync("idempotency");
        var job = await CreateJobAsync(client, "Idempotency test job");

        var first = await client.PostAsync($"/api/jobs/{job.Id}/run", content: null);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        // This is the core "duplicate Run Now click" scenario from the assignment brief.
        // The first execution is still Pending/Running (no worker is running in this test),
        // so the second call must be rejected rather than silently queuing a second execution.
        var second = await client.PostAsync($"/api/jobs/{job.Id}/run", content: null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
