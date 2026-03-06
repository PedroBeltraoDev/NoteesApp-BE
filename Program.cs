using Microsoft.EntityFrameworkCore;
using NotesApp.Api.Data;
using NotesApp.Api.Repositories;
using NotesApp.Api.Services;
using NotesApp.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

//Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Registrar Repositories e Services (ISSO ESTAVA FALTANDO!)
builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddScoped<INoteService, NoteService>();

//Configurar Entity Framework com PostgreSQL (Neon)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

//Configurar CORS para permitir o Vue.js
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp",
        policy => policy.WithOrigins(
                "http://localhost:5173",
                "https://seu-site.vercel.app"
            )
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

//Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//Usar middleware de tratamento de exceções
app.UseMiddleware<ExceptionHandlingMiddleware>();

//Usar CORS
app.UseCors("AllowVueApp");

app.UseAuthorization();

app.MapControllers();

app.Run();