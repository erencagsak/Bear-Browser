using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Bear_Browser.ViewModels
{
    public partial class PasswordsViewModel : ObservableRecipient
    {
        private readonly IPasswordService _passwordService;

        public ObservableCollection<PasswordItem> DisplayedPasswords { get; } = new();

        [ObservableProperty]
        private string _searchQuery = "";

        public PasswordsViewModel(IPasswordService passwordService, IMessenger messenger) : base(messenger)
        {
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
            RefreshList();
            _passwordService.PasswordsUpdated += (s, e) => RefreshList();
        }

        public void RefreshList()
        {
            DisplayedPasswords.Clear();
            var items = _passwordService.Passwords.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                items = items.Where(p =>
                    p.Domain.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    p.UserName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in items)
            {
                DisplayedPasswords.Add(item);
            }
        }

        partial void OnSearchQueryChanged(string value) => RefreshList();

        public void TogglePasswordVisibility(PasswordItem item)
        {
            if (item != null)
            {
                item.IsPasswordVisible = !item.IsPasswordVisible;
            }
        }

        public async Task RemovePasswordAsync(Guid id)
        {
            await _passwordService.RemovePasswordAsync(id);
            RefreshList();
        }

        public async Task ClearPasswordsAsync()
        {
            await _passwordService.ClearPasswordsAsync();
            RefreshList();
        }

        public async Task<int> ImportPasswordsCsvAsync(string filePath)
        {
            int count = await _passwordService.ImportPasswordsCsvAsync(filePath);
            RefreshList();
            return count;
        }

        public async Task ExportPasswordsCsvAsync(string filePath)
        {
            await _passwordService.ExportPasswordsCsvAsync(filePath);
        }
    }
}
