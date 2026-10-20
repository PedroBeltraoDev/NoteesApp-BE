using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using NotesApp.Api.Data;
using NotesApp.Api.DTOs;
using NotesApp.Api.Health;
using NotesApp.Api.Middleware;
using NotesApp.Api.Repositories;
using NotesApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Services
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => e.Key,
                    e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray());

            var message = string.Join("; ", errors.Values.SelectMany(m => m));
            return new BadRequestObjectResult(ApiResponseDto<object>.Fail(message, errors));
        };
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NoteesApp API",
        Version = "v1",
        Description = "API RESTful para gerenciamento de notas"
    });
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddScoped<INoteService, NoteService>();

// Entity Framework
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS - origens permitidas vêm da configuração (Cors:AllowedOrigins), ex.: Cors__AllowedOrigins__0
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is not { Length: > 0 })
    allowedOrigins = ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Swagger só em Development ou com ENABLE_SWAGGER=true
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ENABLE_SWAGGER"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NoteesApp API v1");
        c.RoutePrefix = "swagger";
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Necessário para os testes de integração (WebApplicationFactory<Program>)
public partial class Program;