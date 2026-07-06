using RustIQ.Application.ServerQuery;
using RustIQ.Api.Endpoints;
using RustIQ.Api.Realtime;
using RustIQ.Api.Workers;
using RustIQ.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
const string DevCorsPolicy = "DevCorsPolicy";

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                var isLocalhost = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
                var isLoopback = string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);

                return (isLocalhost || isLoopback)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services
    .AddOptions<RustServerQueryOptions>()
    .Bind(builder.Configuration.GetSection(RustServerQueryOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Rust server query host is required.")
    .Validate(options => options.QueryPort is > 0 and <= 65535, "Rust server query port must be between 1 and 65535.")
    .Validate(options => options.TimeoutMilliseconds > 0, "Rust server query timeout must be greater than 0.")
    .ValidateOnStart();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ServerPollingWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(DevCorsPolicy);

app.MapServerEndpoints();
app.MapHub<ServerUpdatesHub>("/hubs/servers");

app.Run();
