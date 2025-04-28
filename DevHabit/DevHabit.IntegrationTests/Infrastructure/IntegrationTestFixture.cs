using System.Net.Http.Headers;
using System.Net.Http.Json;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevHabit.IntegrationTests.Infrastructure;

//IClassFixture整个测试类共享一个 WebAppFactory
public abstract class IntegrationTestFixture(DevHabitWebAppFactory factory) : IClassFixture<DevHabitWebAppFactory>
{
    //1.定义了一个缓存的 HttpClient，用于存储登录过的、有认证 token 的客户端
    private HttpClient? _authorizedClient;

    //2.创建一个普通的（未登录的）HttpClient
    public HttpClient CreateClient() => factory.CreateClient();

    //3.创建已经登录好的 HttpClient 的方法
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "test@test.com",
        string password = "Test123!")
    {
        if (_authorizedClient is not null)
        {
            return _authorizedClient;
        }

        HttpClient client = CreateClient();

        // Check if user exists
        bool userExists;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            using ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            userExists = await dbContext.Users.AnyAsync(u => u.Email == email);
        }

        if (!userExists)
        {
            // Register a new user
            HttpResponseMessage registerResponse = await client.PostAsJsonAsync(Routes.Auth.Register,
                new RegisterUserDto
                {
                    Email = email,
                    Name = email,
                    Password = password,
                    ConfirmPassword = password
                });

            registerResponse.EnsureSuccessStatusCode();
        }

        // Login to get the token
        HttpResponseMessage loginResponse = await client.PostAsJsonAsync(Routes.Auth.Login,
            new LoginUserDto
            {
                Email = email,
                Password = password
            });

        loginResponse.EnsureSuccessStatusCode();

        AccessTokensDto? loginResult = await loginResponse.Content.ReadFromJsonAsync<AccessTokensDto>();

        if (loginResult?.AccessToken is null)
        {
            throw new InvalidOperationException("Failed to get authentication token");
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);
        _authorizedClient = client;

        return client;
    }
}
