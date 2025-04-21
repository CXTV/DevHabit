using DevHabit.Api.Extensions;
using DevHabit.Api;
using DevHabit.Api.Settings;

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
    await app.SeedInitialDataAsync();

}

app.UseHttpsRedirection();

//�쳣�����м��ע��
app.UseExceptionHandler();

app.UseCors(CorsOptions.PolicyName);


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
