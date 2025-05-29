using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicCatalogWebApplication.Context;
using MusicCatalogWebApplication.Models;
using System.Threading.Tasks;

namespace MusicCatalogWebApplication.Controllers
{
    public class BaseController : Controller
    {
        protected readonly ApplicationDbContext _context;

        public BaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        protected async Task<bool> CanEdit(int ownerId)
        {
            if (User.IsInRole("Admin"))
            {
                return true;
            }

            if (!User.Identity.IsAuthenticated || User.IsInRole("Guest"))
            {
                return false;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            return user != null && user.ID == ownerId;
        }

        protected async Task CreatePublicProposalAsync(string tableName, int recordId, string changeDescription, int userId)
        {
            // Обрезаем tableName до 32 символов (nvarchar(32))
            tableName = tableName.Length > 32 ? tableName.Substring(0, 32) : tableName;

            // Обрезаем changeDescription до 128 символов (nvarchar(128))
            changeDescription = changeDescription.Length > 128 ? changeDescription.Substring(0, 128) : changeDescription;

            var proposal = new EditProposal
            {
                User_ID = userId,
                TableName = tableName,
                Record_ID = recordId,
                ProposedChange = changeDescription,
                Status = "pending", // используем строчное значение, как в БД
                CreatedDate = DateTime.Now
            };
            _context.EditProposals.Add(proposal);
            await _context.SaveChangesAsync();
        }
    }
}