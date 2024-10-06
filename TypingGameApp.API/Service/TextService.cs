using Microsoft.EntityFrameworkCore;
using TypingGameApp.API.Models;

namespace TypingGameApp.API.Service
{
    public class TextService
    {
        private readonly AppDbContext _context;

        public TextService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetRandomTextAsync(string difficulty)
        {
            var query = _context.GameTexts.AsQueryable();

            if (!string.IsNullOrEmpty(difficulty))
            {
                query = query.Where(t => t.Difficulty == difficulty);
            }

            var count = await query.CountAsync();
            var random = new Random();
            var randomText = await query.Skip(random.Next(0, count)).FirstOrDefaultAsync();

            return randomText.Text;
        }
    }
}
