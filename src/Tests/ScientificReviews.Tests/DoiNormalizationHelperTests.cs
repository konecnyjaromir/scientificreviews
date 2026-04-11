using ScientificReviews.Helpers;
using System;

namespace ScientificReviews.Tests
{
    internal static class DoiNormalizationHelperTests
    {
        public static void PrepareDoiForLookup_StripsKnownPrefixesAndTrailingPunctuation()
        {
            AssertEqual("10.48550/arXiv.2310.08864", DoiNormalizationHelper.PrepareDoiForLookup("https://doi.org/10.48550/arXiv.2310.08864."));
            AssertEqual("10.1000/XYZ", DoiNormalizationHelper.PrepareDoiForLookup("doi:10.1000/XYZ,"));
        }

        public static void NormalizeArxivIdentifier_NormalizesSupportedInputs()
        {
            AssertEqual("2310.08864", DoiNormalizationHelper.NormalizeArxivIdentifier("arXiv:2310.08864"));
            AssertEqual("2310.08864v2", DoiNormalizationHelper.NormalizeArxivIdentifier("https://arxiv.org/abs/2310.08864v2"));
            AssertNull(DoiNormalizationHelper.NormalizeArxivIdentifier("10.1000/xyz"));
        }

        public static void TryExtractArxivIdentifier_ExtractsFromRawIdentifier()
        {
            AssertEqual("2310.08864", DoiNormalizationHelper.TryExtractArxivIdentifier("2310.08864"));
            AssertEqual("hep-th/9901001", DoiNormalizationHelper.TryExtractArxivIdentifier("arXiv:hep-th/9901001"));
        }

        public static void TryExtractArxivIdentifier_ExtractsFromArxivDoi()
        {
            AssertEqual("2310.08864", DoiNormalizationHelper.TryExtractArxivIdentifier("10.48550/arXiv.2310.08864"));
            AssertEqual("2310.08864", DoiNormalizationHelper.TryExtractArxivIdentifier("https://doi.org/10.48550/arXiv.2310.08864"));
            AssertEqual("2310.08864", DoiNormalizationHelper.TryExtractArxivIdentifier("doi:10.48550/arXiv.2310.08864"));
        }

        public static void TryExtractArxivIdentifier_ReturnsNullForClassicDoi()
        {
            AssertNull(DoiNormalizationHelper.TryExtractArxivIdentifier("10.1000/xyz"));
            AssertNull(DoiNormalizationHelper.TryExtractArxivIdentifier("not-a-doi"));
        }

        public static void GetDoiValueKind_ClassifiesExpectedKinds()
        {
            AssertEqual(DoiValueKind.Empty, DoiNormalizationHelper.GetDoiValueKind(null));
            AssertEqual(DoiValueKind.Classic, DoiNormalizationHelper.GetDoiValueKind("10.1000/xyz"));
            AssertEqual(DoiValueKind.Arxiv, DoiNormalizationHelper.GetDoiValueKind("2310.08864"));
            AssertEqual(DoiValueKind.Arxiv, DoiNormalizationHelper.GetDoiValueKind("10.48550/arXiv.2310.08864"));
            AssertEqual(DoiValueKind.Invalid, DoiNormalizationHelper.GetDoiValueKind("wrong"));
        }

        public static void NormalizeDoiValue_NormalizesClassicAndArxivInputs()
        {
            AssertEqual("10.1000/xyz", DoiNormalizationHelper.NormalizeDoiValue("DOI:10.1000/XYZ"));
            AssertEqual("2310.08864", DoiNormalizationHelper.NormalizeDoiValue("https://doi.org/10.48550/arXiv.2310.08864"));
            AssertEqual("wrong", DoiNormalizationHelper.NormalizeDoiValue("wrong"));
        }

        public static void RequestedDoiMatchesCandidateDoi_MatchesEquivalentArxivForms()
        {
            AssertTrue(DoiNormalizationHelper.RequestedDoiMatchesCandidateDoi("2310.08864", "10.48550/arXiv.2310.08864"));
            AssertTrue(DoiNormalizationHelper.RequestedDoiMatchesCandidateDoi("2310.08864v2", "arXiv:2310.08864v2"));
        }

        public static void RequestedDoiMatchesCandidateDoi_MatchesWhenRequestedValueIsArxivDoi()
        {
            AssertTrue(DoiNormalizationHelper.RequestedDoiMatchesCandidateDoi("10.48550/arXiv.2310.08864", "2310.08864"));
            AssertTrue(DoiNormalizationHelper.RequestedDoiMatchesCandidateDoi("https://doi.org/10.48550/arXiv.2310.08864", "10.48550/arXiv.2310.08864"));
        }

        public static void RequestedDoiMatchesCandidateDoi_MatchesClassicDoiCaseInsensitively()
        {
            AssertTrue(DoiNormalizationHelper.RequestedDoiMatchesCandidateDoi("10.1000/xyz", "https://doi.org/10.1000/XYZ"));
        }

        public static void RequestedDoiMatchesCandidateDoi_RejectsDifferentKindsAndInvalidValues()
        {
            AssertFalse(DoiNormalizationHelper.RequestedDoiMatchesCandidateDoi("10.1000/xyz", "2310.08864"));
            AssertFalse(DoiNormalizationHelper.RequestedDoiMatchesCandidateDoi("wrong", "10.1000/xyz"));
            AssertFalse(DoiNormalizationHelper.RequestedDoiMatchesCandidateDoi("2310.08864", null));
        }

        private static void AssertEqual(string expected, string actual)
        {
            if (string.Equals(expected, actual, StringComparison.Ordinal) == false)
                throw new InvalidOperationException($"Expected '{expected}', got '{actual ?? "<null>"}'.");
        }

        private static void AssertEqual(DoiValueKind expected, DoiValueKind actual)
        {
            if (expected != actual)
                throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }

        private static void AssertTrue(bool value)
        {
            if (value == false)
                throw new InvalidOperationException("Expected true, got false.");
        }

        private static void AssertFalse(bool value)
        {
            if (value)
                throw new InvalidOperationException("Expected false, got true.");
        }

        private static void AssertNull(string value)
        {
            if (value != null)
                throw new InvalidOperationException($"Expected <null>, got '{value}'.");
        }
    }
}
