using System.Net;
using System.Text.Json;
using Serilog;

namespace WorkManagementSystem.API.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Lỗi không mong muốn: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            int statusCode;
            object response;

            if (exception is UnauthorizedAccessException)
            {
                // Lỗi phân quyền
                statusCode = (int)HttpStatusCode.Forbidden;
                response = new { message = exception.Message ?? "Bạn không có quyền thực hiện hành động này.", details = "" };
            }
            else if (exception is BadHttpRequestException badRequestEx && badRequestEx.Message.Contains("Request body too large"))
            {
                // Lỗi file quá lớn
                statusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                response = new { message = "File đính kèm hoặc nội dung quá lớn (vượt giới hạn máy chủ).", details = exception.Message };
            }
            else if (exception is Exception && !string.IsNullOrEmpty(exception.Message) 
                     && !exception.Message.Contains("Object reference")
                     && !exception.Message.Contains("NullReference")
                     && !exception.Message.Contains("SqlException")
                     && !exception.Message.Contains("InvalidOperation")
                     && exception.StackTrace?.Contains("Application.Services") == true)
            {
                // ✅ SỬA: Lỗi nghiệp vụ (throw new Exception từ Service layer)
                // → Trả đúng message cho frontend hiển thị
                statusCode = (int)HttpStatusCode.BadRequest;
                response = new { message = exception.Message, details = "" };
            }
            else
            {
                // Lỗi hệ thống thật sự (DB, null reference, v.v.)
                statusCode = (int)HttpStatusCode.InternalServerError;
                response = new { message = "Lỗi hệ thống nội bộ. Vui lòng thử lại sau.", details = exception.Message };
            }

            context.Response.StatusCode = statusCode;

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }
    }
}