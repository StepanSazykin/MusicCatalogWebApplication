using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MusicCatalogWebApplication.Context;
using MusicCatalogWebApplication.Models;

namespace MusicCatalogWebApplication.Services
{
    public class AuthService
    {
        private readonly ApplicationDbContext _context;

        public AuthService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User> AuthenticateAsync(string login, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == login && u.IsActive);
            if (user == null || !PasswordHelper.VerifyPassword(password, user.Password))
            {
                return null;
            }

            user.LastLoginDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> RegisterAsync(string login, string? email, string password)
        {
            if (await _context.Users.AnyAsync(u => u.Login == login))
            {
                throw new ArgumentException("Логин уже занят.");
            }

            if (!string.IsNullOrEmpty(email) && await _context.Users.AnyAsync(u => u.Email == email))
            {
                throw new ArgumentException("Email уже занят.");
            }

            var user = new User
            {
                Login = login,
                Email = email,
                Password = PasswordHelper.HashPassword(password),
                RegistrationDate = DateTime.Now,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task ToggleAdminStatusAsync(int userId, int currentUserId)
        {
            if (userId == currentUserId)
            {
                throw new InvalidOperationException("Вы не можете изменить собственный статус администратора.");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("Пользователь не найден.");
            }

            if (!user.IsActive)
            {
                throw new InvalidOperationException("Нельзя назначить администратором неактивного пользователя.");
            }

            // Получаем количество активных администраторов (исключая текущего пользователя)
            var activeAdminsCount = await _context.Users
                .CountAsync(u => u.IsAdmin && u.IsActive && u.ID != userId);

            // Если пытаемся снять статус администратора
            if (user.IsAdmin)
            {
                if (activeAdminsCount < 1)
                {
                    throw new InvalidOperationException("Нельзя снять статус последнего активного администратора.");
                }
            }
            // Если пытаемся назначить администратором
            else
            {

            }

            user.IsAdmin = !user.IsAdmin;
            await _context.SaveChangesAsync();
        }

        public async Task ToggleActiveStatusAsync(int userId, int currentUserId)
        {
            if (userId == currentUserId)
            {
                throw new InvalidOperationException("Вы не можете заблокировать или разблокировать самого себя.");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("Пользователь не найден.");
            }

            if (!user.IsActive && user.IsAdmin && await _context.Users.CountAsync(u => u.IsAdmin && u.IsActive) <= 1)
            {
                throw new InvalidOperationException("Нельзя заблокировать последнего активного администратора.");
            }

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();
        }
    }
}