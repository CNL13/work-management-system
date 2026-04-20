using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Application.Services
{
    /// <summary>
    /// Service đảm nhận trách nhiệm xử lý logic thay đổi mật khẩu cho người dùng.
    /// </summary>
    public class ChangePasswordService : IChangePasswordService
    {
        private readonly AppDbContext _context;

        // Khởi tạo Service với DbContext để tương tác với cơ sở dữ liệu
        public ChangePasswordService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Hàm ChangePassword: Thực hiện thay đổi mật khẩu của người dùng.
        /// Các bước xử lý bao gồm kiểm tra dữ liệu đầu vào, xác thực mật khẩu cũ và cập nhật mật khẩu mới.
        /// </summary>
        /// <param name="userId">ID định danh của người dùng đang thực hiện đổi mật khẩu</param>
        /// <param name="dto">Đối tượng chứa: mật khẩu cũ, mật khẩu mới và xác nhận mật khẩu mới</param>
        /// <returns>Chuỗi thông báo kết quả (thành công hoặc lý do thất bại)</returns>
        public async Task<string> ChangePassword(Guid userId, ChangePasswordDto dto)
        {
            // 1. Kiểm tra tính khớp lệnh của mật khẩu mới
            // Đảm bảo người dùng không gõ nhầm mật khẩu mới ở bước xác nhận
            if (dto.NewPassword != dto.ConfirmPassword)
                return "Mật khẩu mới không khớp!";

            // 2. Kiểm tra độ phức tạp/độ dài mật khẩu
            // Quy định tối thiểu 6 ký tự để đảm bảo an toàn cơ bản
            if (dto.NewPassword.Length < 6)
                return "Mật khẩu mới phải có ít nhất 6 ký tự!";

            // 3. Truy vấn thông tin người dùng từ Database
            // Tìm user dựa theo ID để lấy thông tin mật khẩu cũ (đã mã hóa)
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return "Không tìm thấy người dùng!";

            // 4. Xác thực mật khẩu cũ (OldPassword)
            // Sử dụng thư viện BCrypt để so sánh mật khẩu text thuần (người dùng nhập) 
            // với chuỗi PasswordHash đã mã hóa trong Database.
            if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash))
                return "Mật khẩu cũ không đúng!";

            // 5. Cập nhật mật khẩu mới
            // Mã hóa (Hash) mật khẩu mới trước khi lưu để đảm bảo an toàn thông tin
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

            // 6. Lưu thay đổi xuống Database
            await _context.SaveChangesAsync();

            return "Đổi mật khẩu thành công!";
        }
    }
}