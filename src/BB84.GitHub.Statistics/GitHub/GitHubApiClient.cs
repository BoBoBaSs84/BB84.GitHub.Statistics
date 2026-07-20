using BB84.GitHub.Statistics.GitHub.Models;
using BB84.GitHub.Statistics.Logging;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace BB84.GitHub.Statistics.GitHub;

/// <summary>A raw API response: the status code is part of the contract.</summary>
internal readonly record struct ApiResponse(HttpStatusCode Status, string Body)
{
	public bool IsOk => Status == HttpStatusCode.OK;
}

/// <summary>
/// Thin GitHub API wrapper over <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not Octokit. The contributor-statistics endpoint drives control
/// flow from its status code (202 while GitHub computes, 403/429 when rate
/// limited), and Octokit's <c>GetQueuedOperation</c> absorbs those into an
/// internal polling loop and returns an empty list. That is indistinguishable
/// from a genuine zero and would silently disable the clone fallback. GraphQL is
/// also unavailable in Octokit's REST-only client.
/// </para>
/// <para>
/// The Zig original carried a workaround for pooled keep-alive connections being
/// closed server-side; <see cref="SocketsHttpHandler"/> handles that, so only a
/// small retry for transient socket failures remains.
/// </para>
/// </remarks>
internal sealed class GitHubApiClient(HttpClient http, ILogger<GitHubApiClient> logger)
{
	private const string GraphQlEndpoint = "https://api.github.com/graphql";
	private const int TransientRetries = 8;

	public static HttpClient CreateHttpClient(string token)
	{
		SocketsHttpHandler handler = new()
		{
			PooledConnectionLifetime = TimeSpan.FromMinutes(2),
			AutomaticDecompression = DecompressionMethods.All,
		};

		HttpClient client = new(handler);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("github-stats", "3.0.0"));
		client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
		client.Timeout = TimeSpan.FromMinutes(2);
		return client;
	}

	public Task<ApiResponse> GetAsync(string url, CancellationToken ct) =>
			SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);

	public Task<ApiResponse> GraphQlAsync(
			string query,
			DateRangeVariables? variables,
			CancellationToken ct)
	{
		GraphQlRequest payload = new() { Query = query, Variables = variables };
		string json = JsonSerializer.Serialize(payload, GitHubJsonContext.Default.GraphQlRequest);

		return SendAsync(
				() => new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint)
				{
					Content = new StringContent(json, Encoding.UTF8, "application/json"),
				},
				ct);
	}

	/// <summary>Deserializes a response body, returning <c>null</c> when the payload is unusable.</summary>
	public T? TryDeserialize<T>(ApiResponse response, JsonTypeInfo<T> typeInfo, string context)
			where T : class
	{
		try
		{
			return JsonSerializer.Deserialize(response.Body, typeInfo);
		}
		catch (JsonException e)
		{
			Log.SkippingInvalidResponse(logger, context, e.Message);
			return null;
		}
	}

	/// <summary>
	/// Sends a request, rebuilding it on each attempt because an
	/// <see cref="HttpRequestMessage"/> cannot be sent twice.
	/// </summary>
	private async Task<ApiResponse> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct)
	{
		for (int attempt = 0; ; attempt++)
		{
			using HttpRequestMessage request = requestFactory();
			try
			{
				using HttpResponseMessage response = await http.SendAsync(request, ct).ConfigureAwait(false);
				string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
				return new ApiResponse(response.StatusCode, body);
			}
			catch (HttpRequestException e) when (attempt < TransientRetries)
			{
				Log.RequestRetrying(logger, e.Message, attempt + 1, TransientRetries);
			}
		}
	}
}
