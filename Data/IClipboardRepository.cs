using System.Collections.Generic;
using System.Threading.Tasks;
using ClipboardManager.Models;

namespace ClipboardManager.Data
{
    public class ParsedSearchQuery
    {
        public string FtsSafeExpression { get; init; } = string.Empty;
    }

    public interface IClipboardRepository
    {
        Task AddAsync(ClipboardRecord record);
        Task<List<ClipboardItem>> GetRecentAsync(int limit = 50, int offset = 0);
        Task<List<ClipboardItem>> SearchAsync(ParsedSearchQuery query);
        Task<ClipboardItem?> GetByIdAsync(string id);
        Task SetPinnedAsync(string id, bool isPinned);
        Task DeleteAsync(string id);
        Task ClearUnpinnedAsync();
        Task DeleteExpiredAsync();
    }
}
