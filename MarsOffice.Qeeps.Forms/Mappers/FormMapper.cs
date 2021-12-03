using AutoMapper;
using MarsOffice.Qeeps.Forms.Abstractions;
using MarsOffice.Qeeps.Forms.Entities;

namespace MarsOffice.Qeeps.Forms.Mappers
{
    public class FormMapper : Profile
    {
        public FormMapper()
        {
            var m1 = CreateMap<FormEntity, FormDto>().PreserveReferences();
            var m2 = m1.ReverseMap().PreserveReferences();
            m2.ForMember(x => x.CreatedDate, y => y.Ignore())
                .ForMember(x => x.Id, y => y.Ignore())
                .ForMember(x => x.UserId, y => y.Ignore());
        }
    }
}