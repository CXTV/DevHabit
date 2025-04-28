using DevHabit.Api.DTOs.GitHub;
using Refit;

namespace DevHabit.Api.Services;

//每次调用这个接口的时候，自动给 HTTP 请求带上这两个头
[Headers("User-Agent: DevHabit/1.0", "Accept: application/vnd.github+json")]
public interface IGitHubApi
{
    [Get("/user")] //GET 请求，访问的是 GitHub API 的 /user 路径
    Task<ApiResponse<GitHubUserProfileDto>> GetUserProfile(
        [Authorize(scheme: "Bearer")] string accessToken, //自动添加jwt Token在请求头中
        CancellationToken cancellationToken = default);

    [Get("/users/{username}/events")] //GET 请求，访问的是 GitHub API 的 /users/{username}/events 路径
    Task<ApiResponse<IReadOnlyList<GitHubEventDto>>> GetUserEvents(
        string username,
        [Authorize(scheme: "Bearer")] string accessToken, // accessToken自动插到请求头里（带身份认证）
        int page = 1,
        [AliasAs("per_page")] int perPage = 100, //告诉RefitGitHub API 要求参数名是 per_page（不是 C# 里的驼峰 PerPage
        CancellationToken cancellationToken = default);
}
