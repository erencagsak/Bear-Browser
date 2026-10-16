using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bear_Browser.Models;

namespace Bear_Browser.Services.Interfaces
{
    public interface IPasswordService
    {
        List<PasswordItem> Passwords { get; }
        event EventHandler? PasswordsUpdated;

        Task AddOrUpdatePasswordAsync(string url, string userName, string password);
        Task RemovePasswordAsync(Guid id);
        Task ClearPasswordsAsync();
        Task ExportPasswordsCsvAsync(string filePath);
        Task<int> ImportPasswordsCsvAsync(string filePath);
    }
}
