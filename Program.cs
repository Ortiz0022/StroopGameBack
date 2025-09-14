using Microsoft.EntityFrameworkCore;
using StroobGame.AppDataBase;
using StroobGame.Hubs;
using StroobGame.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
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


// ?? CORS — incluye devtunnels + Live Server (127.0.0.1:5500)
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("client", p => p
        // Agrega aquí otros orígenes si usas alguno distinto
        .WithOrigins(
            "http://127.0.0.1:5500",
            "http://localhost:5500",
            "http://localhost:5173",
            "http://localhost:3000",
            // si abrirás el front también desde https en local:
            "https://127.0.0.1:5500",
            // DevTunnels (tu dominio exacto, sin /):
            "https://0687n4mj-7121.use2.devtunnels.ms"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
    );
    // ?? Si prefieres abrir todo en DEV, usa esta política y cámbiala abajo:
    // opt.AddPolicy("client", p => p
    //     .AllowAnyHeader().AllowAnyMethod().AllowCredentials()
    //     .SetIsOriginAllowed(_ => true));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ?? Orden importante
app.UseRouting();

// ? Aplica CORS ANTES de mapear endpoints
app.UseCors("client");

// (si usas auth, iría aquí UseAuthentication/UseAuthorization)

// ? Requiere CORS explícitamente en Controllers y Hub
app.MapControllers().RequireCors("client");
app.MapHub<GameHub>("/hubs/game").RequireCors("client");

app.Run();
