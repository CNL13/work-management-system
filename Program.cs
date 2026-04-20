using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;
using System.Text;
using WorkManagementSystem.API.Middlewares;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Mappings;
using WorkManagementSystem.Application.Services;
using WorkManagementSystem.Infrastructure.Data;
using WorkManagementSystem.Infrastructure.Repositories;
using WorkManagementSystem.Domain.Entities;
using Microsoft.AspNetCore.Http.Features;
using WorkManagementSystem.API.Hubs; // ✅ MỚI

// ================= SERILOG =================
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ================= SERVICES =================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR(); // ✅ MỚI

// DB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Repository
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IGenericRepository<TaskHistory>, GenericRepository<TaskHistory>>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IUnitService, UnitService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IChangePasswordService, ChangePasswordService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ICommentService, CommentService>(); // ✅ MỚI
builder.Services.AddScoped<ISubTaskService, SubTaskService>(); // ✅ MỚI
builder.Services.AddScoped<IGenericRepository<TaskComment>, GenericRepository<TaskComment>>(); // ✅ MỚI
builder.Services.AddScoped<IGenericRepository<SubTask>, GenericRepository<SubTask>>(); // ✅ MỚI

// AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// ================= LIMITS =================
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

// ================= CORS ================= 
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // ✅ CẦN CHO SIGNALR
    });
});

// ================= AUTH (JWT) =================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

// ================= SWAGGER =================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WorkManagement API",
        Version = "v1",
        Description = "API"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập token dạng: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

    var xmlFile = $"`$`(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name).xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// ================= BUILD APP =================
var app = builder.Build();

// ✅ MỚI: Tự động chạy SQL manual migration cho JoinedUnitAt
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        context.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (SELECT * FROM sys.columns 
                           WHERE object_id = OBJECT_ID(N'[dbo].[Users]') 
                           AND name = 'JoinedUnitAt')
            BEGIN
                ALTER TABLE [dbo].[Users] ADD [JoinedUnitAt] DATETIME2 NOT NULL DEFAULT '2026-01-01';
            END
        ");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Lỗi khi chạy manual migration JoinedUnitAt");
    }
}

// ================= MIDDLEWARE =================
var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "Uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles();
app.UseCors();
app.UseMiddleware<ExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<DiscussionHub>("/discussionHub"); 

app.Run();
