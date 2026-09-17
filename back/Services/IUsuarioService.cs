using back.DTOs;
using back.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace back.Services
{
    public interface IUsuarioService
    {
        Task<IEnumerable<UserDto>> GetAllUsersAsync();
        Task<UserDto> GetUserByIdAsync(int id);
        Task<(IEnumerable<UserDto> Users, int TotalCount)> GetUsersPaginatedAsync(int page, int pageSize);
        Task<bool> ActivateUserAsync(int userId);
        Task<bool> DeactivateUserAsync(int userId);
        Task<bool> SetUserRoleAsync(SetUserRoleDto setRoleDto);
    }
}
