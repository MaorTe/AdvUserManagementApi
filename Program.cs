using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using UserManagementApi.Data;
using UserManagementApi.Endpoints;
using UserManagementApi.Infrastructure.Hosting;
using UserManagementApi.Middleware;
using UserManagementApi.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ??? 1. Configuration & DbContext ????????????????????????????????????????
// Configure Kestrel for HTTPS/TLS
//builder.WebHost.ConfigureKestrel((context, options) => {
//    options.ConfigureTls(context.Configuration);
//    // endpoints via context.Configuration.GetSection("Kestrel") or defaults
//});

// Database context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AutoShopDbContext>(options =>
    options.UseSqlServer(connectionString)
);

// ??? 2. Application Services ?????????????????????????????????????????????
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICarService, CarService>();
builder.Services.AddScoped<IDataMigrationService, DataMigrationService>();
builder.Services.AddSingleton<IFileTransferService, SftpFileTransferService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
builder.Services.AddHostedService<IdempotencyCleanupService>();

// ??? 3. Authentication & Authorization ??????????????????????????????????
builder.Services.AddJwtAuthentication(builder.Configuration);

// ??? 4. Cross-cutting Concerns ??????????????????????????????????????????
// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo {
        Title = "User & Car Management API",
        Version = "v1"
    });
});

// CORS policy
builder.Services.AddCors(options => {
    options.AddPolicy("AllowFrontEnd", policy =>
        policy
        //.WithOrigins(
        //    "https://user-management-frontend-h8hc.onrender.com",
        //    "http://localhost:5173",
        //    "https://localhost:5173"
        //)
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()
    );
});

// HTTPS enforcement
//builder.Services.AddHttpsRedirection(options => {
//    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
//    options.HttpsPort = 5204;
//});
//builder.Services.AddHsts(options => {
//    options.Preload = true;
//    options.IncludeSubDomains = true;
//    options.MaxAge = TimeSpan.FromDays(180);
//});

builder.Services.AddRateLimiter(options => {
    // a fixed?window policy: 100 requests per minute per client
    options.AddFixedWindowLimiter("Global", config => {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueLimit = 0;
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

builder.Services.AddOutputCache(options => {
    options.AddPolicy("Short30s", policy =>
        policy.Expire(TimeSpan.FromSeconds(30)));
});
// ??? 5. Build & Migrate ?????????????????????????????????????????????????
var app = builder.Build();

// Apply migrations
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AutoShopDbContext>();
    db.Database.Migrate();
}

// ??? 6. HTTP Pipeline ??????????????????????????????????????????????????? 
// Middleware pipeline
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        c.RoutePrefix = string.Empty;
    });
}

// Enforce HSTS and redirect HTTP to HTTPS
app.UseHsts();
app.UseHttpsRedirection();
app.UseCors("AllowFrontEnd");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

// Global error handling
ExceptionHandler.Configure(app);

// ??? 7. Map Endpoints ???????????????????????????????????????????????????
// Auth (login) lives in Endpoints/AuthEndpoints.cs
app.MapAuthEndpoints();

// All your domain endpoints, protected by [Authorize]
app.MapUserEndpoints();
app.MapCarEndpoints();
app.MapDataMigrationEndpoints();
app.MapReportEndpoints();

// ??? 8. Run ?????????????????????????????????????????????????????????????
app.Run();
