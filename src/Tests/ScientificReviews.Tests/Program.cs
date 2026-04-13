using System;
using System.Collections.Generic;

namespace ScientificReviews.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            List<string> failures = new List<string>();

            RunTest(nameof(DoiNormalizationHelperTests.PrepareDoiForLookup_StripsKnownPrefixesAndTrailingPunctuation), DoiNormalizationHelperTests.PrepareDoiForLookup_StripsKnownPrefixesAndTrailingPunctuation, failures);
            RunTest(nameof(DoiNormalizationHelperTests.NormalizeArxivIdentifier_NormalizesSupportedInputs), DoiNormalizationHelperTests.NormalizeArxivIdentifier_NormalizesSupportedInputs, failures);
            RunTest(nameof(DoiNormalizationHelperTests.BuildArxivDoi_BuildsCanonicalDataciteFormat), DoiNormalizationHelperTests.BuildArxivDoi_BuildsCanonicalDataciteFormat, failures);
            RunTest(nameof(DoiNormalizationHelperTests.TryExtractArxivIdentifier_ExtractsFromRawIdentifier), DoiNormalizationHelperTests.TryExtractArxivIdentifier_ExtractsFromRawIdentifier, failures);
            RunTest(nameof(DoiNormalizationHelperTests.TryExtractArxivIdentifier_ExtractsFromArxivDoi), DoiNormalizationHelperTests.TryExtractArxivIdentifier_ExtractsFromArxivDoi, failures);
            RunTest(nameof(DoiNormalizationHelperTests.TryExtractArxivIdentifier_ReturnsNullForClassicDoi), DoiNormalizationHelperTests.TryExtractArxivIdentifier_ReturnsNullForClassicDoi, failures);
            RunTest(nameof(DoiNormalizationHelperTests.GetDoiValueKind_ClassifiesExpectedKinds), DoiNormalizationHelperTests.GetDoiValueKind_ClassifiesExpectedKinds, failures);
            RunTest(nameof(DoiNormalizationHelperTests.NormalizeDoiValue_NormalizesClassicAndArxivInputs), DoiNormalizationHelperTests.NormalizeDoiValue_NormalizesClassicAndArxivInputs, failures);
            RunTest(nameof(DoiNormalizationHelperTests.RequestedDoiMatchesCandidateDoi_MatchesEquivalentArxivForms), DoiNormalizationHelperTests.RequestedDoiMatchesCandidateDoi_MatchesEquivalentArxivForms, failures);
            RunTest(nameof(DoiNormalizationHelperTests.RequestedDoiMatchesCandidateDoi_MatchesWhenRequestedValueIsArxivDoi), DoiNormalizationHelperTests.RequestedDoiMatchesCandidateDoi_MatchesWhenRequestedValueIsArxivDoi, failures);
            RunTest(nameof(DoiNormalizationHelperTests.RequestedDoiMatchesCandidateDoi_MatchesClassicDoiCaseInsensitively), DoiNormalizationHelperTests.RequestedDoiMatchesCandidateDoi_MatchesClassicDoiCaseInsensitively, failures);
            RunTest(nameof(DoiNormalizationHelperTests.RequestedDoiMatchesCandidateDoi_RejectsDifferentKindsAndInvalidValues), DoiNormalizationHelperTests.RequestedDoiMatchesCandidateDoi_RejectsDifferentKindsAndInvalidValues, failures);

            if (failures.Count == 0)
            {
                Console.WriteLine("All tests passed.");
                return 0;
            }

            Console.Error.WriteLine($"{failures.Count} test(s) failed:");
            foreach (string failure in failures)
                Console.Error.WriteLine(failure);

            return 1;
        }

        private static void RunTest(string name, Action test, List<string> failures)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failures.Add($"FAIL {name}: {ex.Message}");
            }
        }
    }
}
