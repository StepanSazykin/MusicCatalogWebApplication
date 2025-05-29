using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MusicCatalogWebApplication.Context;

namespace MusicCatalogWebApplication.Services
{
    public class UserActivityService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UserActivityService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromDays(1); // Проверка раз в день
        private readonly TimeSpan _inactivityThreshold = TimeSpan.FromDays(180); // 6 месяцев

        public UserActivityService(IServiceProvider serviceProvider, ILogger<UserActivityService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var inactiveUsers = await context.Users
                            .Where(u => u.IsActive && u.LastLoginDate.HasValue && DateTime.Now - u.LastLoginDate.Value > _inactivityThreshold)
                            .ToListAsync(stoppingToken);

                        foreach (var user in inactiveUsers)
                        {
                            user.IsActive = false;
                            _logger.LogInformation("Пользователь {UserId} ({Login}) автоматически деактивирован из-за неактивности.", user.ID, user.Login);
                        }

                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при автоматической деактивации пользователей.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}