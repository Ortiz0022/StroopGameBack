using Microsoft.EntityFrameworkCore;
using StroobGame.AppDataBase;
using StroobGame.Hubs;
using StroobGame.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Controllers (camelCase opcional)
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// SignalR (camelCase opcional)
builder.Services.AddSignalR()
    .AddJsonProtocol(o =>
    {
        o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.PayloadSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext
builder.Services.AddScoped<AppDbContext>();

// DI
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddSingleton<ConnectionRegistry>();
builder.Services.AddScoped<IGameService, StroopService>();

// CORS
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("client", p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowed(_ => true) // ⚠️ SOLO para desarrollo
    );
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ⚠️ Si tu front consume por HTTP (http://localhost:5266), deja ESTO COMENTADO.
// app.UseHttpsRedirection();

app.UseRouting();

// Auth (si usas): app.UseAuthentication(); app.UseAuthorization();

app.UseCors("client");

// ✅ Usa UseEndpoints para mapear el hub y los controllers (no dupliques con MapHub/MapControllers abajo)
app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<GameHub>("/hubs/game");
    endpoints.MapControllers();
});

app.Run();
