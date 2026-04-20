namespace WorkManagementSystem.Domain.Entities
{
    public class UploadFile
    {
        public Guid Id { get; set; }

        public string FileName { get; set; }      // tên file gốc
        public string FilePath { get; set; }      // đường dẫn lưu
        public DateTime CreatedAt { get; set; }   // thời gian upload

        public Guid? ProgressId { get; set; }     // optional
        public Guid? TaskId { get; set; }         // ✅ MỚI: Liên kết với Task gốc (Minh chứng ban đầu)
    }
}