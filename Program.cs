// Program.cs
using System.Text;
using System.Text.Json.Serialization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookMyService.Models;
using BookMyServiceBE.Repository;
using BookMyServiceBE.Repository.IRepository;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var pathBase = (builder.Configuration["PathBase"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_PATHBASE") ?? "").TrimEnd('/');
if (!string.IsNullOrWhiteSpace(pathBase) && !pathBase.StartsWith('/'))
{
    pathBase = "/" + pathBase;
}

builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

builder.Services.AddScoped<IFileUpload, FileUpload>();
builder.Services.AddScoped<FileUpload>();

builder.Services.AddSingleton<IReceiptPdfService, ReceiptPdfService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddResponseCaching();

// JWT Auth
var jwtKey = builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("Missing Jwt:Key");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "BookMyServiceBE",

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "BookMyServiceFE",

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

// Swagger + Bearer
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BookMyServiceBE", Version = "v1" });

    // ✅ กันชนชื่อ schema/DTO ซ้ำกัน ทำให้ swagger.json 500 ได้
    c.CustomSchemaIds(t => (t.FullName ?? t.Name).Replace("+", "."));

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "ใส่: Bearer {token}"
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
            Array.Empty<string>()
        }
    });
});

// ✅ CORS - อ่านจาก appsettings.json แทนการ hardcode
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" }; // fallback ถ้าไม่มี config

builder.Services.AddCors(o => o.AddPolicy("fe", p =>
    p.WithOrigins(allowedOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
));

var app = builder.Build();

QuestPDF.Settings.License = LicenseType.Community;

if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

// Ensure database is migrated and seed initial data if needed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    var seeder = new DataSeeder(db);
    await seeder.SeedInitialData();
}

app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json; charset=utf-8";
        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        var ex = feature?.Error;
        var message = ex?.Message ?? "An internal server error has occurred.";
        var logger = context.RequestServices.GetService<ILogger<Program>>();
        if (logger != null && ex != null)
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
        await context.Response.WriteAsJsonAsync(new { message, statusCode = 500 });
    });
});

//if (app.Environment.IsDevelopment())
//{
//    // ✅ ให้เห็น error จริงเวลา swagger.json 500
//    app.UseDeveloperExceptionPage();

//    app.UseSwagger();
//    app.UseSwaggerUI();
//}



//app.UseStaticFiles(new StaticFileOptions
//{
//    ServeUnknownFileTypes = false,
//    DefaultContentType = "application/octet-stream"
//});

//if (!app.Environment.IsDevelopment())
//{
//    app.UseHttpsRedirection();
//}

app.UseSwagger(options =>
{
    options.RouteTemplate = "swagger/{documentName}/swagger.json";
    options.PreSerializeFilters.Add((swagger, httpReq) =>
    {
        var basePath = string.IsNullOrEmpty(httpReq.PathBase.Value) ? "" : httpReq.PathBase.Value.TrimEnd('/');
        var serverUrl = $"{httpReq.Scheme}://{httpReq.Host.Value}{basePath}";
        swagger.Servers = new List<OpenApiServer>
        {
            new OpenApiServer { Url = serverUrl }
        };
    });
});
app.UseSwaggerUI(c =>
{
    var swaggerEndpoint = string.IsNullOrEmpty(pathBase) ? "/swagger/v1/swagger.json" : $"{pathBase}/swagger/v1/swagger.json";
    c.RoutePrefix = "swagger";
    c.SwaggerEndpoint(swaggerEndpoint, "BookMyServiceBE v1");
    c.DocumentTitle = "BookMyServiceBE API";
});

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("fe");
app.UseResponseCaching();

// IMPORTANT: auth order
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "API is running").WithName("Root").WithOpenApi();
app.MapFallbackToFile("index.html");

app.Run();
