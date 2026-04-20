using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Infrastructure.Data;

namespace WorkManagementSystem.Application.Services
{
    public class ExportService : IExportService
    {
        private readonly AppDbContext _context;

        public ExportService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Ý NGHĨA: Xuất danh sách toàn bộ công việc ra tệp Excel (.xlsx).
        /// - Thu thập dữ liệu từ các bảng Tasks, TaskAssignees và Units.
        /// - Khởi tạo Workbook và thiết kế giao diện bảng (Header màu xanh, dòng kẻ xen kẽ).
        /// - Thực hiện ánh xạ (Mapping) để lấy tên các phòng ban liên quan đến từng công việc.
        /// - Trả về mảng byte (byte array) để phía Client có thể tải về.
        /// </summary>
        public async Task<byte[]> ExportTasksToExcel()
        {
            var tasks = await _context.Tasks.ToListAsync();
            var taskAssignees = await _context.TaskAssignees.ToListAsync();
            var units = await _context.Units.ToListAsync();

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Danh sách công việc");

            // Thiết lập tiêu đề cột (Header)
            sheet.Cell(1, 1).Value = "STT";
            sheet.Cell(1, 2).Value = "Tên công việc";
            sheet.Cell(1, 3).Value = "Mô tả";
            sheet.Cell(1, 4).Value = "Deadline";
            sheet.Cell(1, 5).Value = "Phòng ban";

            // Định dạng kiểu dáng cho Header (In đậm, nền xanh, chữ trắng, căn giữa)
            var headerRow = sheet.Range("A1:E1");
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
            headerRow.Style.Font.FontColor = XLColor.White;
            headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Đổ dữ liệu vào các dòng (Data Rows)
            int row = 2;
            int stt = 1;
            foreach (var task in tasks)
            {
                // Truy vấn tên các đơn vị/phòng ban được giao công việc này
                var unitNames = taskAssignees
                    .Where(ta => ta.TaskId == task.Id && ta.UnitId.HasValue)
                    .Select(ta => units.FirstOrDefault(u => u.Id == ta.UnitId)?.Name ?? "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .ToList();

                sheet.Cell(row, 1).Value = stt++;
                sheet.Cell(row, 2).Value = task.Title;
                sheet.Cell(row, 3).Value = task.Description ?? "";
                sheet.Cell(row, 4).Value = task.DueDate.HasValue
                    ? task.DueDate.Value.ToString("dd/MM/yyyy")
                    : "Không có";
                // Nối tên các phòng ban cách nhau bởi dấu phẩy
                sheet.Cell(row, 5).Value = string.Join(", ", unitNames);

                // Tạo hiệu ứng dòng chẵn màu xám nhạt để dễ quan sát
                if (row % 2 == 0)
                    sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

                row++;
            }

            // Tự động điều chỉnh độ rộng cột theo nội dung
            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        /// <summary>
        /// Ý NGHĨA: Xuất lịch sử tiến độ cập nhật công việc ra tệp Excel.
        /// - Truy xuất dữ liệu từ bảng Progresses kèm thông tin Task và User liên quan.
        /// - Chuyển đổi trạng thái (Enum) sang ngôn ngữ tiếng Việt dễ hiểu cho người dùng.
        /// - Thống kê chi tiết phần trăm hoàn thành và thời gian cập nhật cụ thể.
        /// </summary>
        public async Task<byte[]> ExportProgressToExcel()
        {
            var progresses = await _context.Progresses.ToListAsync();
            var tasks = await _context.Tasks.ToListAsync();
            var users = await _context.Users.ToListAsync();

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Tiến độ công việc");

            // Thiết lập tiêu đề cột
            sheet.Cell(1, 1).Value = "STT";
            sheet.Cell(1, 2).Value = "Công việc";
            sheet.Cell(1, 3).Value = "Nhân viên";
            sheet.Cell(1, 4).Value = "Mô tả tiến độ";
            sheet.Cell(1, 5).Value = "Phần trăm";
            sheet.Cell(1, 6).Value = "Trạng thái";
            sheet.Cell(1, 7).Value = "Ngày cập nhật";

            // Định dạng kiểu dáng cho Header
            var headerRow = sheet.Range("A1:G1");
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
            headerRow.Style.Font.FontColor = XLColor.White;
            headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Đổ dữ liệu tiến độ vào bảng
            int row = 2;
            int stt = 1;
            foreach (var p in progresses)
            {
                var taskTitle = tasks.FirstOrDefault(t => t.Id == p.TaskId)?.Title ?? "";
                var userName = users.FirstOrDefault(u => u.Id == p.UserId)?.FullName ?? "";

                // Chuyển đổi mã trạng thái hệ thống sang mô tả tiếng Việt
                var statusText = p.Status.ToString() switch
                {
                    "NotStarted" => "Chưa bắt đầu",
                    "InProgress" => "Đang thực hiện",
                    "Submitted" => "Chờ duyệt",
                    "Approved" => "Đã phê duyệt",
                    "Rejected" => "Bị từ chối",
                    _ => p.Status.ToString()
                };

                sheet.Cell(row, 1).Value = stt++;
                sheet.Cell(row, 2).Value = taskTitle;
                sheet.Cell(row, 3).Value = userName;
                sheet.Cell(row, 4).Value = p.Description ?? "";
                sheet.Cell(row, 5).Value = p.Percent + "%";
                sheet.Cell(row, 6).Value = statusText;
                sheet.Cell(row, 7).Value = p.UpdatedAt.ToString("dd/MM/yyyy HH:mm");

                // Tạo hiệu ứng dòng kẻ xen kẽ
                if (row % 2 == 0)
                    sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

                row++;
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}