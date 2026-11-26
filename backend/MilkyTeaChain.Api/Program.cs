using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MilkyTeaChain.Api.Data;
using MilkyTeaChain.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký DbContext (SQL Server)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Đăng ký đầy đủ các Service nghiệp vụ
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BranchAccessService>();
builder.Services.AddScoped<BranchService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ReportService>();

// 3. Cấu hình Controllers và JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 4. Cấu hình xác thực JWT (đọc khóa ký từ biến môi trường JWT_SECRET_KEY, User Secrets hoặc appsettings.json)
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Chưa cấu hình khóa ký JWT (Jwt:Key hoặc biến môi trường JWT_SECRET_KEY).");

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "MilkyTeaChain.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "MilkyTeaChain.Clients";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// 5. Cấu hình Swagger kèm nút Authorize (Bearer JWT)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MilkyTeaChain API - Hệ thống quản lý chuỗi Trà sữa/Cà phê",
        Version = "v1",
        Description = "Đề tài 12: ASP.NET Core Web API, EF Core, SQL Server, JWT & Swagger"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Nhập JWT token theo định dạng: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

// Tự động chạy Migration và Seed dữ liệu mẫu (chỉ tạo tài khoản mẫu trong môi trường Development)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
    await DbSeeder.SeedAsync(dbContext, env, logger);
}

// Mở Swagger tại /swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MilkyTeaChain API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

// 6. Phục vụ giao diện từ thư mục ../../frontend khi chạy ở máy local,
// hoặc từ wwwroot khi chạy bản đã publish.
var localFrontendPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "frontend"));
var publishedWwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var activeFrontendPath = Directory.Exists(localFrontendPath) ? localFrontendPath : publishedWwwrootPath;

if (Directory.Exists(activeFrontendPath))
{
    var fileProvider = new PhysicalFileProvider(activeFrontendPath);

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider,
        DefaultFileNames = new List<string> { "index.html" }
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        OnPrepareResponse = context =>
        {
            var extension = Path.GetExtension(context.File.Name);
            if (extension is ".html" or ".css" or ".js")
            {
                context.Context.Response.Headers.CacheControl = "no-cache";
            }
        }
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
