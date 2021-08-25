using FluentValidation;
using MarsOffice.Qeeps.Forms.Abstractions;

namespace MarsOffice.Qeeps.Forms.Validators
{
    public class FormDtoValidator : AbstractValidator<FormDto>
    {
        public FormDtoValidator()
        {
            RuleFor(x => x.Title).NotEmpty().WithMessage("forms.formDto.titleRequired")
                .MinimumLength(6).WithMessage("forms.formDto.titleTooShort|chars:6");
            
            RuleFor(x => x.CronExpression).NotEmpty().WithMessage("form.formDto.cronExpressionRequired")
                .When(x => x.IsRecurrent);

            RuleForEach(x => x.Attachments).ChildRules(x => {
                x.RuleFor(x => x.Filename).NotEmpty().WithMessage("forms.formDto.attachmentFilenameRequired");
                x.RuleFor(x => x.Id).NotEmpty().WithMessage("forms.formDto.attachmentIdRequired");
            });

            RuleFor(x => x.UserId).NotEmpty().WithMessage("forms.formDto.userIdRequired");

            RuleForEach(x => x.Columns).ChildRules(x => {
                x.RuleFor(x => x.DropdownOptions).NotNull().WithMessage("forms.formDto.dropdownOptionsRequired")
                    .When(x => x.DataType == ColumnDataType.Dropdown)
                    .NotEmpty().WithMessage("forms.formDto.dropdownOptionsRequired")
                    .When(x => x.DataType == ColumnDataType.Dropdown);
                x.RuleFor(x => x.Name).NotEmpty().WithMessage("forms.formDto.columnNameRequired");
            });

            RuleFor(x => x.FormAccesses).NotNull().WithMessage("forms.formDto.formAccessesRequired")
                .NotEmpty().WithMessage("forms.formDto.formAccessesRequired");

            RuleForEach(x => x.FormAccesses).ChildRules(x => {
                x.RuleFor(x => x.FullOrganisationId).NotEmpty().WithMessage("forms.formDto.accessOrganisationRequired");
                x.RuleFor(x => x.OrganisationId).NotEmpty().WithMessage("forms.formDto.accessOrganisationRequired");
            });
        }
    }
}