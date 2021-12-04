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

            RuleFor(x => x.Title).NotEmpty().WithMessage("forms.createEditForm.titleRequired")
        .MinimumLength(6).WithMessage("forms.createEditForm.titleTooShort|chars:6");

            RuleFor(x => x.CronExpression).NotEmpty().WithMessage("form.formDto.cronExpressionRequired")
                .Custom((x, ctx) =>
                {
                    try
                    {
                        var cronExpression = CronExpression.Parse(x, CronFormat.IncludeSeconds);
                        if (!x.StartsWith("0 0 "))
                        {
                            ctx.AddFailure("CronExpression", "forms.createEditForm.invalidCronExpression");
                        }
                        var occurences = cronExpression.GetOccurrences(DateTime.UtcNow, DateTime.UtcNow.AddSeconds(30), true, true);
                        if (occurences.Count() > 1)
                        {
                            ctx.AddFailure("CronExpression", "forms.createEditForm.invalidCronExpression");
                        }
                    }
                    catch (Exception)
                    {
                        ctx.AddFailure("CronExpression", "forms.createEditForm.invalidCronExpression");
                    }
                })
                .When(x => x.IsRecurrent);

            RuleForEach(x => x.Attachments).ChildRules(x =>
            {
                x.RuleFor(x => x.Filename).NotEmpty().WithMessage("forms.createEditForm.attachmentFilenameRequired");
                x.RuleFor(x => x.FileId).NotEmpty().WithMessage("forms.createEditForm.attachmentFileIdRequired");
                x.RuleFor(x => x.Location).NotEmpty().WithMessage("forms.createEditForm.attachmentLocationRequired");
                x.RuleFor(x => x.UploadSessionId).NotEmpty().WithMessage("forms.createEditForm.attachmentUploadSessionIdRequired");
                x.RuleFor(x => x.UserId).NotEmpty().WithMessage("forms.createEditForm.attachmentUserIdRequired");
            });

            RuleForEach(x => x.Columns).ChildRules(x =>
            {
                x.RuleFor(x => x.DropdownOptions).NotNull().WithMessage("forms.createEditForm.dropdownOptionsRequired")
                    .When(x => x.DataType == ColumnDataType.Dropdown)
                    .NotEmpty().WithMessage("forms.createEditForm.dropdownOptionsRequired")
                    .When(x => x.DataType == ColumnDataType.Dropdown);
                x.RuleFor(x => x.Name).NotEmpty().WithMessage("forms.createEditForm.columnNameRequired");
                x.RuleFor(x => x.Reference).NotEmpty().WithMessage("forms.createEditForm.columnReferenceRequired");
            });

            RuleFor(x => x.Rows).Must((f, list) => list.All(r => r.Count() == f.Columns.Count()))
                .WithMessage("forms.createEditForm.allRowValuesMustMatchColumnCount")
                .When(x => x.Rows != null && x.Rows.Any());

            RuleFor(x => x.Deadline.Value).GreaterThan(DateTime.UtcNow).WithMessage("forms.createEditForm.invalidDeadline")
                .When(x => x.Deadline != null);

            RuleForEach(x => x.FormAccesses).ChildRules(x =>
            {
                x.RuleFor(x => x.OrganisationId).NotEmpty().WithMessage("forms.createEditForm.accessOrganisationRequired");
            });

        }
    }
}