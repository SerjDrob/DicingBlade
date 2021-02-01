using FluentValidation;

namespace DicingBlade.Classes
{
    internal class TechnologySettingsValidator : AbstractValidator<ITechnology>
    {
        public TechnologySettingsValidator()
        {
            RuleFor(technology => technology.SpindleFreq).NotEmpty().LessThan(30001).GreaterThan(17999);
            RuleFor(technology => technology.FeedSpeed).NotEmpty().LessThan(20).GreaterThan(0.1);
            RuleFor(technology => technology.WaferBladeGap).NotEmpty().LessThan(5).GreaterThan(0.5);
            RuleFor(technology => technology.FilmThickness).NotEmpty().LessThan(0.5).GreaterThan(0);
            RuleFor(technology => technology.UnterCut).LessThan(3).GreaterThan(t => -t.FilmThickness);
            RuleFor(technology => technology.StartControlNum).NotEmpty().LessThan(10000).GreaterThan(0);
            RuleFor(technology => technology.ControlPeriod).NotEmpty().LessThan(10000).GreaterThan(0);
            RuleFor(technology => technology.PassCount).NotEmpty().LessThan(10).GreaterThan(0);
        }
    }
}
