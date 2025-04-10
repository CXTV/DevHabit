using DevHabit.Api.Extensions;
using DevHabit.Api;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder
    .AddApiServices()
    .AddErrorHandling()
    .AddDatabase()
    .AddObservability()
    .AddApplicationServices()
    .AddAuthenticationServices();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.ApplyMigrations();
}

app.UseHttpsRedirection();

//异常处理中间件注册
app.UseExceptionHandler();


app.MapControllers();

await app.RunAsync();
