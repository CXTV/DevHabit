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

//�쳣�����м��ע��
app.UseExceptionHandler();
app.UseCors(CorsOptions.PolicyName);

app.UseResponseCaching();

app.UseAuthentication();

app.UseAuthorization();
//����ٶ����ƣ����λ�ÿ��Ը���
app.UseRateLimiter();

app.UseMiddleware<ETagMiddleware>();

app.MapControllers();

await app.RunAsync();
