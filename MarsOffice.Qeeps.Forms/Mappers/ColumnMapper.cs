using AutoMapper;
using MarsOffice.Qeeps.Forms.Abstractions;
using MarsOffice.Qeeps.Forms.Entities;

namespace MarsOffice.Qeeps.Forms.Mappers
{
    public class ColumnMapper : Profile
    {
        public  ColumnMapper()
        {
            CreateMap<ColumnEntity, ColumnDto>().PreserveReferences()
            .ReverseMap().PreserveReferences();
        }
    }
}