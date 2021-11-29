using System;
using System.Linq;
using Cronos;
using FluentValidation;
using MarsOffice.Qeeps.Forms.Abstractions;

namespace MarsOffice.Qeeps.Forms.Validators
{
    public class FormDtoValidator : AbstractValidator<FormDto>
    {
        public FormDtoValidator()
        {

            RuleFor(x => x.Title).NotEmpty().WithMessage("forms.createFormDto.titleRequired")
        .MinimumLength(6).WithMessage("forms.createFormDto.titleTooShort|chars:6");

            RuleFor(x => x.CronExpression).NotEmpty().WithMessage("form.formDto.cronExpressionRequired")
                .Custom((x, ctx) =>
                {
                    try
                    {
                        var cronExpression = CronExpression.Parse(x, CronFormat.IncludeSeconds);
                        if (!x.StartsWith("0 0 "))
                        {
                            ctx.AddFailure("CronExpression", "forms.createFormDto.invalidCronExpression");
                        }
                    }
                    catch (Exception)
                    {
                        ctx.AddFailure("CronExpression", "forms.createFormDto.invalidCronExpression");
                    }
                })
                .When(x => x.IsRecurrent);

            RuleForEach(x => x.Attachments).ChildRules(x =>
            {
                x.RuleFor(x => x.Filename).NotEmpty().WithMessage("forms.createFormDto.attachmentFilenameRequired");
                x.RuleFor(x => x.FileId).NotEmpty().WithMessage("forms.createFormDto.attachmentFileIdRequired");
                x.RuleFor(x => x.Location).NotEmpty().WithMessage("forms.createFormDto.attachmentLocationRequired");
                x.RuleFor(x => x.UploadSessionId).NotEmpty().WithMessage("forms.createFormDto.attachmentUploadSessionIdRequired");
                x.RuleFor(x => x.UserId).NotEmpty().WithMessage("forms.createFormDto.attachmentUserIdRequired");
            });

            RuleForEach(x => x.Columns).ChildRules(x =>
            {
                x.RuleFor(x => x.DropdownOptions).NotNull().WithMessage("forms.createFormDto.dropdownOptionsRequired")
                    .When(x => x.DataType == ColumnDataType.Dropdown)
                    .NotEmpty().WithMessage("forms.createFormDto.dropdownOptionsRequired")
                    .When(x => x.DataType == ColumnDataType.Dropdown);
                x.RuleFor(x => x.Name).NotEmpty().WithMessage("forms.createFormDto.columnNameRequired");
                x.RuleFor(x => x.Reference).NotEmpty().WithMessage("forms.createFormDto.columnReferenceRequired");
            });

            RuleFor(x => x.Rows).Must((f, list) => list.All(r => r.Count() == f.Columns.Count()))
                .WithMessage("forms.createFormDto.allRowValuesMustMatchColumnCount")
                .When(x => x.Rows != null && x.Rows.Any());

            RuleForEach(x => x.FormAccesses).ChildRules(x =>
            {
                x.RuleFor(x => x.OrganisationId).NotEmpty().WithMessage("forms.createFormDto.accessOrganisationRequired");
            });

        }
    }
}