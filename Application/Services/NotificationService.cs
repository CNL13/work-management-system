using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Ý NGHĨA: Hàm thêm thông báo mới cho một người dùng cụ thể.
        /// - Tạo một đối tượng Notification với các thông tin: ID duy nhất, ID người nhận, nội dung tin nhắn.
        /// - Mặc định trạng thái IsRead là false (chưa đọc) và ghi lại thời gian tạo theo chuẩn UTC.
        /// - Lưu thay đổi vào cơ sở dữ liệu để thông báo có thể hiển thị cho người dùng.
        /// </summary>
        public async Task AddNotification(Guid userId, string message)
        {
            _context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Ý NGHĨA: Hàm lấy danh sách tất cả thông báo của người dùng hiện tại.
        /// - Lọc các thông báo dựa trên UserId của người đang đăng nhập.
        /// - Sắp xếp danh sách theo thời gian tạo giảm dần (thông báo mới nhất hiện lên đầu).
        /// - Ánh xạ dữ liệu sang NotificationDto để trả về cho phía giao diện (Frontend).
        /// </summary>
        public async Task<List<NotificationDto>> GetMyNotifications(Guid userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();
        }

        /// <summary>
        /// Ý NGHĨA: Hàm đánh dấu một thông báo cụ thể là "Đã đọc".
        /// - Tìm kiếm thông báo trong cơ sở dữ liệu dựa trên mã định danh notificationId.
        /// - Nếu tìm thấy, cập nhật thuộc tính IsRead thành true.
        /// - Lưu lại thay đổi giúp hệ thống không còn đếm đây là thông báo mới.
        /// </summary>
        public async Task MarkAsRead(Guid notificationId)
        {
            var notif = await _context.Notifications.FindAsync(notificationId);
            if (notif != null)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Ý NGHĨA: Hàm đếm số lượng thông báo chưa đọc của người dùng.
        /// - Lọc các bản ghi thuộc về người dùng (userId) mà trạng thái IsRead đang là false.
        /// - Trả về một con số nguyên để hiển thị trên biểu tượng chuông thông báo (Badge count).
        /// </summary>
        public async Task<int> GetUnreadCount(Guid userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }
    }
}