using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bear_Browser.Models;

namespace Bear_Browser.Services.Interfaces
{
    public interface IBookmarkService
    {
        List<BookmarkItem> Bookmarks { get; }
        List<string> Folders { get; }
        List<string> BarOrder { get; }
        void EnsureBarOrder();
        event EventHandler? BookmarksUpdated;

        Task<BookmarkItem> AddOrUpdateBookmarkAsync(string url, string title, string folder);
        Task RemoveBookmarkByUrlAsync(string url);
        Task RemoveBookmarkAsync(Guid id);
        Task AddFolderAsync(string folderName);
        Task RemoveFolderAsync(string folderName);
        bool IsBookmarked(string url);
        BookmarkItem? GetBookmark(string url);
        Task UpdateFaviconAsync(string url, string faviconUri);
        Task MoveBookmarkAsync(Guid id, string targetFolder, int newIndex);
        Task ReorderBookmarksAsync(List<BookmarkItem> newOrder);
        Task SaveChangesAsync();
        Task ExportBookmarksHtmlAsync(string filePath);
        Task<int> ImportBookmarksHtmlAsync(string filePath);
    }
}
