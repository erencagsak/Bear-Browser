using System;
using System.Threading.Tasks;
using Bear_Browser.Models;

namespace Bear_Browser.Services.Interfaces
{
    public interface IProfileService
    {
        UserProfile CurrentProfile { get; }
        event EventHandler<UserProfile>? ProfileUpdated;

        Task UpdateProfileAsync(string newName, string? newSourceImagePath);
        Task UpdateProfileAvatarPathAsync(string newName, string avatarPath);
        Task UpdateProfileNameAsync(string newName);
    }
}
