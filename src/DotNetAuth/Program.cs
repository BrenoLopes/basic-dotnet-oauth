using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Modules.IdentityAccess.Composition;
using DotNetAuth.Infrastructure.DependencyInjection;
using DotNetAuth.Infrastructure.IdentityAccess.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddInfrastructure(
    builder.Configuration,
    builder.Environment);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IConfirmationLinkBuilder, AspNetConfirmationLinkBuilder>();
builder.Services.AddScoped<RegisterUserService>();
builder.Services.AddScoped<ConfirmUserEmailService>();
builder.Services.AddScoped<LoginUserService>();
builder.Services.AddScoped<LogoutUserService>();
builder.Services.AddScoped<GetCurrentUserService>();
builder.Services.AddScoped<AuthorizeUserService>();
builder.Services.AddScoped<GetUserProfileService>();
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
if (app.Configuration.GetValue<bool>("Web:Authentication:Registration"))
{
    app.MapRazorPages();
}

if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);
}

app.Run();

public partial class Program;
