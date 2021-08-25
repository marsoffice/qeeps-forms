using AutoMapper;
using MarsOffice.Qeeps.Forms.Abstractions;
using MarsOffice.Qeeps.Forms.Entities;

namespace MarsOffice.Qeeps.Forms.Mappers
{
    public class FormMapper : Profile
    {
        public  FormMapper()
        {
            CreateMap<FormEntity, FormDto>().PreserveReferences()
            .ReverseMap().PreserveReferences();
        }
    }
}