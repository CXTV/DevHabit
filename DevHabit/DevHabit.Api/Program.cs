using DevHabit.Api.Extensions;
using DevHabit.Api;
using DevHabit.Api.Middleware;
using DevHabit.Api.Settings;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder
    .AddApiServices()
    .AddErrorHandling()
    .AddDatabase()
    .AddObservability()
    .AddApplicationServices()
    .AddAuthenticationServices()
    //.AddBackgroundJobs()
    .AddCorsPolicy()
    .AddRateLimiting();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.ApplyMigrations();
    await app.SeedInitialDataAsync();

}

app.UseHttpsRedirection();

//异常处理中间件注册
app.UseExceptionHandler();
app.UseCors(CorsOptions.PolicyName);

app.UseResponseCaching();

app.UseAuthentication();

app.UseAuthorization();
//添加速度限制，这个位置可以更改
app.UseRateLimiter();

app.UseMiddleware<ETagMiddleware>();

app.MapControllers();

await app.RunAsync();
