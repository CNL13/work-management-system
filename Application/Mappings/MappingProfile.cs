using AutoMapper;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Domain.Entities;
// ❌ Bỏ dòng này - không cần
// using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WorkManagementSystem.Application.Mappings  // ✅ THÊM namespace này
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<CreateTaskDto, TaskItem>();
            CreateMap<CreateProgressDto, Progress>();
        }
    }
}