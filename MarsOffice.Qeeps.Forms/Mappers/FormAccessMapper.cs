using AutoMapper;
using MarsOffice.Qeeps.Forms.Abstractions;
using MarsOffice.Qeeps.Forms.Entities;

namespace MarsOffice.Qeeps.Forms.Mappers
{
    public class FormAccessMapper : Profile
    {
        public  FormAccessMapper()
        {
            CreateMap<FormAccessEntity, FormAccessDto>().PreserveReferences()
            .ReverseMap().PreserveReferences();
        }
    }
}