using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthService(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        /// <summary>
        /// Hàm Đăng ký tài khoản mới:
        /// 1. Kiểm tra trùng lặp Username (kể cả những user đã bị xóa mềm).
        /// 2. Tự động sinh mã nhân viên kế tiếp (Dạng EMP0001, EMP0002...).
        /// 3. Mã hóa mật khẩu bằng BCrypt và lưu ở trạng thái chờ duyệt (IsApproved = false).
        /// </summary>
        public async Task<string> Register(AuthDto dto)
        {
            // Kiểm tra username tồn tại
            var exists = await _context.Users.IgnoreQueryFilters()
                .AnyAsync(x => x.Username == dto.Username);
            if (exists)
                throw new Exception("Tên đăng nhập đã tồn tại!");

            // Logic sinh mã nhân viên tự động (EmployeeCode)
            var maxEmployee = await _context.Users.IgnoreQueryFilters()
                .Where(u => u.EmployeeCode != null && u.EmployeeCode.StartsWith("EMP"))
                .Select(u => u.EmployeeCode)
                .ToListAsync();

            int nextId = 1;
            if (maxEmployee.Any())
            {
                var maxId = maxEmployee
                    .Select(code => int.TryParse(code.Substring(3), out var num) ? num : 0)
                    .Max();
                nextId = maxId + 1;
            }
            var employeeCode = $"EMP{nextId:D4}";

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = dto.Username,
                FullName = dto.FullName,
                EmployeeCode = employeeCode,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password), // Mã hóa mật khẩu
                Role = "User",
                UnitId = dto.UnitId,
                PhoneNumber = dto.PhoneNumber,
                IsApproved = false  // Mặc định tài khoản mới phải chờ duyệt
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return "Đăng ký thành công! Vui lòng chờ Admin phê duyệt.";
        }

        /// <summary>
        /// Hàm Đăng nhập:
        /// 1. Xác thực sự tồn tại của tài khoản.
        /// 2. Kiểm tra tính chính xác của mật khẩu.
        /// 3. Kiểm tra tài khoản đã được Admin phê duyệt chưa.
        /// 4. Trả về JWT Token nếu hợp lệ.
        /// </summary>
        public async Task<string> Login(string username, string password)
        {
            username = username?.Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Username == username)
                ?? throw new Exception("Tài khoản không tồn tại!");

            // Kiểm tra mật khẩu (so sánh text thuần với hash trong DB)
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new Exception("Mật khẩu không đúng!");

            // Chặn đăng nhập nếu chưa được duyệt
            if (!user.IsApproved)
                throw new Exception("Tài khoản chưa được Admin phê duyệt!");

            return GenerateToken(user);
        }

        /// <summary>
        /// Hàm Đặt lại mật khẩu: Tìm user và cập nhật lại PasswordHash mới.
        /// </summary>
        public async Task<string> ResetPassword(ResetPasswordDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Username == dto.Username)
                ?? throw new Exception("Không tìm thấy tài khoản!");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();
            return "Đổi mật khẩu thành công!";
        }

        /// <summary>
        /// Hàm Phê duyệt tài khoản (Dành cho Admin):
        /// 1. Chuyển trạng thái IsApproved sang true.
        /// 2. Ghi nhận thời gian tham gia (để bắt đầu tính KPI).
        /// 3. Liên kết nhân viên vào phòng ban tương ứng trong bảng UserUnit.
        /// </summary>
        public async Task<string> ApproveUser(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new Exception("Không tìm thấy tài khoản!");

            user.IsApproved = true;
            user.JoinedUnitAt = DateTime.UtcNow;

            // Nếu user đăng ký kèm đơn vị (UnitId), thực hiện map vào bảng quan hệ UserUnit
            if (user.UnitId.HasValue)
            {
                var alreadyMapped = await _context.Set<UserUnit>()
                    .AnyAsync(uu => uu.UserId == userId && uu.UnitId == user.UnitId.Value);
                if (!alreadyMapped)
                {
                    _context.Set<UserUnit>().Add(new UserUnit
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        UnitId = user.UnitId.Value
                    });
                }
            }

            await _context.SaveChangesAsync();
            return $"Đã duyệt tài khoản {user.FullName}!";
        }

        /// <summary>
        /// Hàm Từ chối/Xóa tài khoản: Sử dụng Soft-delete (IsDeleted = true) 
        /// để không hiển thị trong hệ thống nhưng vẫn lưu lại dấu vết trong Database.
        /// </summary>
        public async Task<string> RejectUser(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new Exception("Không tìm thấy tài khoản!");

            user.IsDeleted = true; // Xóa mềm
            await _context.SaveChangesAsync();
            return $"Đã từ chối tài khoản {user.FullName}!";
        }

        /// <summary>
        /// Hàm Lấy danh sách tài khoản chờ duyệt: 
        /// Lọc ra các User có IsApproved = false để Admin xem xét.
        /// </summary>
        public async Task<List<UserDto>> GetPendingUsers()
        {
            return await _context.Users
                .Where(x => x.IsApproved == false)
                .Select(x => new UserDto
                {
                    Id = x.Id,
                    Username = x.Username ?? "",
                    FullName = x.FullName ?? "",
                    EmployeeCode = x.EmployeeCode ?? "",
                    Role = x.Role ?? "",
                    UnitId = x.UnitId,
                    IsApproved = x.IsApproved,
                    PhoneNumber = x.PhoneNumber
                })
                .ToListAsync();
        }

        /// <summary>
        /// Hàm Làm mới Token: Tạo một JWT Token mới cho User mà không bắt họ đăng nhập lại.
        /// </summary>
        public async Task<string> RefreshToken(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new Exception("Không tìm thấy tài khoản!");
            return GenerateToken(user);
        }

        /// <summary>
        /// Hàm phụ trợ (Private): Sinh chuỗi JWT Token.
        /// Chứa các thông tin định danh (Claims) như: ID, Mã nhân viên, Tên, Vai trò.
        /// Thời hạn Token được thiết lập là 3 tiếng.
        /// </summary>
        private string GenerateToken(User user)
        {
            var claims = new[]
            {
                new Claim("id", user.Id.ToString()),
                new Claim("employeeCode", user.EmployeeCode ?? ""),
                new Claim("fullName", user.FullName ?? ""),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(3), // Token hết hạn sau 3 giờ
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}