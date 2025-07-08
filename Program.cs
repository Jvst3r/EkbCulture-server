using EkbCulture.AppHost.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EkbCulture.AppHost.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Добавляем сервисы
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Disposition"); 
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Подключаем БД
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 20 * 1024 * 1024; // 20 MB
    options.Limits.MinRequestBodyDataRate = null;
    options.Limits.MinResponseDataRate = null;
});

var app = builder.Build();

// Настройка middleware (будет Swagger если приложение на этапе Debug,т.е. разработки)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers();

// Проверка подключения к БД
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();

        // Применяем миграции автоматически
        db.Database.Migrate();
        Console.WriteLine("\nМИГРАЦИИ ПРИМЕНЕНЫ\n");

        // Тестовые данные
        if (!db.Users.Any())
        {
            db.Users.Add(new User("test", "test@test.com", "test"));
            db.SaveChanges();
            Console.WriteLine("\nТЕСТОВЫЙ ПОЛЬЗОВАТЕЛЬ ДОБАВЛЕН\n");
        }
        Console.WriteLine("\nБАЗА ДАННЫХ РАБОТАЕТ БЕЗ ОШИБОК\n");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "\nОШИБКА ПРИ ПРИМЕНЕНИИ МИГРАЦИЙ\n");
    }
}

// Проверка системных лимитов
Console.WriteLine($"Maximum file handles: {GetMaximumFileHandles()}");
Console.WriteLine($"Current file handles: {Process.GetCurrentProcess().HandleCount}");

 static int GetMaximumFileHandles()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        return 16384; // Стандартное значение для Windows
    }
    else
    {
        try
        {
            return int.Parse(File.ReadAllText("/proc/sys/fs/file-max"));
        }
        catch
        {
            return 1024;
        }
    }
}

app.Run();