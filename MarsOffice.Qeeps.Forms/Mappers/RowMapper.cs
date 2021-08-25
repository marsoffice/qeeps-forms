using AutoMapper;
using MarsOffice.Qeeps.Forms.Abstractions;
using MarsOffice.Qeeps.Forms.Entities;

namespace MarsOffice.Qeeps.Forms.Mappers
{
    public class RowMapper : Profile
    {
        public  RowMapper()
        {
            CreateMap<RowEntity, RowDto>().PreserveReferences()
            .ReverseMap().PreserveReferences();
        }
    }
}