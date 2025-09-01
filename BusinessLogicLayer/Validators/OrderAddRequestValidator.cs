using BusinessLogicLayer.DTO;
using FluentValidation;

namespace BusinessLogicLayer.Validators;
public class OrderAddRequestValidator : AbstractValidator<OrderAddRequest>
{
    public OrderAddRequestValidator()
    {
        RuleFor(temp => temp.UserID)
            .NotEmpty().WithMessage("User ID can't be blank");

        RuleFor(temp => temp.OrderDate)
            .NotEmpty().WithMessage("Order date can't be blank");

        RuleFor(temp => temp.OrderItems)
            .NotEmpty().WithMessage("Order items can't be blank");
    }
}
