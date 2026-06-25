using VisualAlpha.FundLens.Core.Domain;

namespace VisualAlpha.FundLens.Core.Interfaces;

public interface IReportOnboardingService
{
    Task<OnboardingResult> OnboardAsync(Stream pdfStream);
}
