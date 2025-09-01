using BusinessLogicLayer.DTO;
using FluentValidation;

namespace BusinessLogicLayer.Validators;
public class OrderItemAddRequestValidator : AbstractValidator<OrderItemAddRequest>
{
    public OrderItemAddRequestValidator()
    {
        RuleFor(temp => temp.ProductID)
            .NotEmpty().WithMessage("Product ID can't be blank");

        RuleFor(temp => temp.UnitPrice)
            .NotEmpty().WithMessage("Unit price can't be blank")
            .GreaterThan(0).WithMessage("Unit price can't be less than or equal to zero");

        RuleFor(temp => temp.Quantity)
            .NotEmpty().WithMessage("Quantity can't be blank")
            .GreaterThan(0).WithMessage("Quantity can't be less than or equal to zero");
    }
}
