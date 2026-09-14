using ScientificReviews.Bibtex;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ScientificReviews.Helpers
{
    public sealed class SmartSearchParseResult
    {
        private SmartSearchParseResult(SmartSearchFilter filter, string errorMessage)
        {
            Filter = filter;
            ErrorMessage = errorMessage;
        }

        public SmartSearchFilter Filter { get; }

        public string ErrorMessage { get; }

        public bool Success => string.IsNullOrWhiteSpace(ErrorMessage);

        public static SmartSearchParseResult FromFilter(SmartSearchFilter filter)
        {
            return new SmartSearchParseResult(filter, null);
        }

        public static SmartSearchParseResult FromError(string errorMessage)
        {
            return new SmartSearchParseResult(null, errorMessage);
        }
    }

    public sealed class SmartSearchFilter
    {
        private readonly SearchNode _root;

        private SmartSearchFilter(SearchNode root)
        {
            _root = root;
        }

        public static SmartSearchParseResult TryParse(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return SmartSearchParseResult.FromFilter(new SmartSearchFilter(null));

            try
            {
                Parser parser = new Parser(query);
                SearchNode root = parser.Parse();
                return SmartSearchParseResult.FromFilter(new SmartSearchFilter(root));
            }
            catch (SmartSearchParseException ex)
            {
                return SmartSearchParseResult.FromError(ex.Message);
            }
        }

        public bool IsMatch(BibtexEntry entry)
        {
            if (_root == null)
                return true;

            return _root.Evaluate(entry);
        }

        private abstract class SearchNode
        {
            public abstract bool Evaluate(BibtexEntry entry);
        }

        private enum NumericComparisonOperator
        {
            Equal,
            GreaterThan,
            GreaterThanOrEqual,
            LessThan,
            LessThanOrEqual
        }

        private sealed class TermNode : SearchNode
        {
            private readonly string _field;
            private readonly string _value;

            public TermNode(string field, string value)
            {
                _field = string.IsNullOrWhiteSpace(field) ? null : field.Trim();
                _value = value ?? string.Empty;
            }

            public override bool Evaluate(BibtexEntry entry)
            {
                return GetCandidateValues(entry, _field)
                    .Any(candidate => candidate?.IndexOf(_value, StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        private sealed class NumericRangeNode : SearchNode
        {
            private readonly string _field;
            private readonly double _minValue;
            private readonly double _maxValue;

            public NumericRangeNode(string field, double minValue, double maxValue)
            {
                _field = field;
                _minValue = minValue;
                _maxValue = maxValue;
            }

            public override bool Evaluate(BibtexEntry entry)
            {
                return GetNumericCandidateValues(entry, _field)
                    .Any(value => value >= _minValue && value <= _maxValue);
            }
        }

        private sealed class NumericComparisonNode : SearchNode
        {
            private readonly string _field;
            private readonly NumericComparisonOperator _operator;
            private readonly double _expectedValue;

            public NumericComparisonNode(string field, NumericComparisonOperator comparisonOperator, double expectedValue)
            {
                _field = field;
                _operator = comparisonOperator;
                _expectedValue = expectedValue;
            }

            public override bool Evaluate(BibtexEntry entry)
            {
                return GetNumericCandidateValues(entry, _field)
                    .Any(MatchesOperator);
            }

            private bool MatchesOperator(double candidate)
            {
                switch (_operator)
                {
                    case NumericComparisonOperator.Equal:
                        return candidate == _expectedValue;
                    case NumericComparisonOperator.GreaterThan:
                        return candidate > _expectedValue;
                    case NumericComparisonOperator.GreaterThanOrEqual:
                        return candidate >= _expectedValue;
                    case NumericComparisonOperator.LessThan:
                        return candidate < _expectedValue;
                    case NumericComparisonOperator.LessThanOrEqual:
                        return candidate <= _expectedValue;
                    default:
                        return false;
                }
            }
        }

        private sealed class NotNode : SearchNode
        {
            private readonly SearchNode _inner;

            public NotNode(SearchNode inner)
            {
                _inner = inner;
            }

            public override bool Evaluate(BibtexEntry entry)
            {
                return !_inner.Evaluate(entry);
            }
        }

        private sealed class AndNode : SearchNode
        {
            private readonly SearchNode _left;
            private readonly SearchNode _right;

            public AndNode(SearchNode left, SearchNode right)
            {
                _left = left;
                _right = right;
            }

            public override bool Evaluate(BibtexEntry entry)
            {
                return _left.Evaluate(entry) && _right.Evaluate(entry);
            }
        }

        private sealed class OrNode : SearchNode
        {
            private readonly SearchNode _left;
            private readonly SearchNode _right;

            public OrNode(SearchNode left, SearchNode right)
            {
                _left = left;
                _right = right;
            }

            public override bool Evaluate(BibtexEntry entry)
            {
                return _left.Evaluate(entry) || _right.Evaluate(entry);
            }
        }

        private sealed class Parser
        {
            private readonly List<string> _tokens;
            private int _position;

            public Parser(string query)
            {
                _tokens = Tokenize(query);
            }

            public SearchNode Parse()
            {
                SearchNode expression = ParseOr();
                if (expression == null)
                    throw new SmartSearchParseException("Query is empty.");

                if (!IsAtEnd())
                    throw new SmartSearchParseException($"Unexpected token '{Peek()}'.");

                return expression;
            }

            private SearchNode ParseOr()
            {
                SearchNode left = ParseAnd();

                while (MatchOperator("OR"))
                {
                    SearchNode right = ParseAnd();
                    left = new OrNode(left, right);
                }

                return left;
            }

            private SearchNode ParseAnd()
            {
                SearchNode left = ParseUnary();

                while (true)
                {
                    if (MatchOperator("AND"))
                    {
                        SearchNode right = ParseUnary();
                        left = new AndNode(left, right);
                        continue;
                    }

                    if (!ShouldTreatAsImplicitAnd())
                        break;

                    SearchNode implicitRight = ParseUnary();
                    left = new AndNode(left, implicitRight);
                }

                return left;
            }

            private SearchNode ParseUnary()
            {
                if (MatchOperator("NOT"))
                    return new NotNode(ParseUnary());

                return ParsePrimary();
            }

            private SearchNode ParsePrimary()
            {
                if (Match("("))
                {
                    SearchNode inner = ParseOr();
                    Expect(")");
                    return inner;
                }

                return ParseTerm();
            }

            private SearchNode ParseTerm()
            {
                if (IsAtEnd())
                    throw new SmartSearchParseException("Expected a search term.");

                string token = Advance();
                if (IsOperator(token) || token == ")")
                    throw new SmartSearchParseException($"Expected a search term, got '{token}'.");

                SearchNode comparisonNode;
                if (TryParseSeparatedComparison(token, out comparisonNode))
                    return comparisonNode;

                if (TryParseEmbeddedComparison(token, out comparisonNode))
                    return comparisonNode;

                string field = null;
                string value = null;

                if (token.EndsWith(":", StringComparison.Ordinal))
                {
                    field = token.Substring(0, token.Length - 1);
                    value = ReadValueToken(field);
                }
                else
                {
                    int colonIndex = token.IndexOf(':');
                    bool looksLikeUrlScheme = colonIndex > 0 && token.IndexOf("://", StringComparison.Ordinal) == colonIndex;
                    if (colonIndex > 0 && !looksLikeUrlScheme)
                    {
                        field = token.Substring(0, colonIndex);
                        value = token.Substring(colonIndex + 1);

                        if (value.Length == 0)
                            value = ReadValueToken(field);
                    }
                    else
                    {
                        value = token;
                    }
                }

                if (string.IsNullOrWhiteSpace(field) == false && field.IndexOfAny(new[] { '(', ')' }) >= 0)
                    throw new SmartSearchParseException($"Invalid field selector '{field}'.");

                if (string.IsNullOrWhiteSpace(value))
                    throw new SmartSearchParseException($"Field '{field}' is missing a value.");

                if (TryCreateNumericRangeNode(field, value, out SearchNode numericRangeNode))
                    return numericRangeNode;

                if (TryCreateNumericComparisonNode(field, value, out SearchNode numericComparisonNode))
                    return numericComparisonNode;

                return new TermNode(field, value);
            }

            private bool TryParseSeparatedComparison(string token, out SearchNode node)
            {
                node = null;
                if (string.IsNullOrWhiteSpace(token) || token.IndexOf(':') >= 0 || IsAtEnd())
                    return false;

                if (!TryParseComparisonOperator(Peek(), out NumericComparisonOperator comparisonOperator))
                    return false;

                _position++;
                string valueToken = ReadValueToken(token);
                node = CreateNumericComparisonNode(token, comparisonOperator, valueToken);
                return true;
            }

            private bool TryParseEmbeddedComparison(string token, out SearchNode node)
            {
                node = null;
                if (string.IsNullOrWhiteSpace(token))
                    return false;

                foreach (string comparisonToken in new[] { ">=", "<=", ">", "<", "=" })
                {
                    int comparisonIndex = token.IndexOf(comparisonToken, StringComparison.Ordinal);
                    if (comparisonIndex <= 0)
                        continue;

                    string field = token.Substring(0, comparisonIndex).Trim();
                    string value = token.Substring(comparisonIndex + comparisonToken.Length).Trim();
                    if (field.Length == 0 || value.Length == 0)
                        continue;

                    if (!TryParseComparisonOperator(comparisonToken, out NumericComparisonOperator comparisonOperator))
                        continue;

                    node = CreateNumericComparisonNode(field, comparisonOperator, value);
                    return true;
                }

                return false;
            }

            private SearchNode CreateNumericComparisonNode(string field, NumericComparisonOperator comparisonOperator, string valueToken)
            {
                if (!TryParseNumber(valueToken, out double expectedValue))
                    throw new SmartSearchParseException($"Comparison value '{valueToken}' is not a number.");

                return new NumericComparisonNode(field, comparisonOperator, expectedValue);
            }

            private bool TryCreateNumericRangeNode(string field, string value, out SearchNode node)
            {
                node = null;
                if (string.IsNullOrWhiteSpace(field))
                    return false;

                if (!TryParseNumericRange(value, out double minValue, out double maxValue))
                    return false;

                node = new NumericRangeNode(field, minValue, maxValue);
                return true;
            }

            private bool TryCreateNumericComparisonNode(string field, string value, out SearchNode node)
            {
                node = null;
                if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(value))
                    return false;

                foreach (string comparisonToken in new[] { ">=", "<=", ">", "<", "=" })
                {
                    if (!value.StartsWith(comparisonToken, StringComparison.Ordinal))
                        continue;

                    string numericValue = value.Substring(comparisonToken.Length).Trim();
                    if (numericValue.Length == 0)
                        throw new SmartSearchParseException($"Field '{field}' is missing a numeric comparison value.");

                    if (!TryParseComparisonOperator(comparisonToken, out NumericComparisonOperator comparisonOperator))
                        continue;

                    node = CreateNumericComparisonNode(field, comparisonOperator, numericValue);
                    return true;
                }

                return false;
            }

            private string ReadValueToken(string field)
            {
                if (IsAtEnd())
                    throw new SmartSearchParseException($"Field '{field}' is missing a value.");

                string value = Advance();
                if (value == ")" || IsOperator(value))
                    throw new SmartSearchParseException($"Field '{field}' is missing a value.");

                return value;
            }

            private bool ShouldTreatAsImplicitAnd()
            {
                if (IsAtEnd())
                    return false;

                string token = Peek();
                if (token == ")")
                    return false;

                return !string.Equals(token, "OR", StringComparison.OrdinalIgnoreCase);
            }

            private bool MatchOperator(string expected)
            {
                if (IsAtEnd())
                    return false;

                if (!string.Equals(Peek(), expected, StringComparison.OrdinalIgnoreCase))
                    return false;

                _position++;
                return true;
            }

            private bool Match(string token)
            {
                if (IsAtEnd())
                    return false;

                if (!string.Equals(Peek(), token, StringComparison.Ordinal))
                    return false;

                _position++;
                return true;
            }

            private void Expect(string token)
            {
                if (!Match(token))
                    throw new SmartSearchParseException($"Expected '{token}'.");
            }

            private string Advance()
            {
                if (IsAtEnd())
                    throw new SmartSearchParseException("Unexpected end of query.");

                return _tokens[_position++];
            }

            private string Peek()
            {
                return _tokens[_position];
            }

            private bool IsAtEnd()
            {
                return _position >= _tokens.Count;
            }

            private static bool IsOperator(string token)
            {
                return string.Equals(token, "AND", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(token, "OR", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(token, "NOT", StringComparison.OrdinalIgnoreCase);
            }

            private static List<string> Tokenize(string query)
            {
                List<string> tokens = new List<string>();
                StringBuilder current = new StringBuilder();
                bool inQuotes = false;

                Action flush = () =>
                {
                    if (current.Length == 0)
                        return;

                    tokens.Add(current.ToString());
                    current.Clear();
                };

                foreach (char c in query ?? string.Empty)
                {
                    if (inQuotes)
                    {
                        if (c == '"')
                        {
                            inQuotes = false;
                            continue;
                        }

                        current.Append(c);
                        continue;
                    }

                    if (c == '"')
                    {
                        inQuotes = true;
                        continue;
                    }

                    if (c == '(' || c == ')')
                    {
                        flush();
                        tokens.Add(c.ToString());
                        continue;
                    }

                    if (c == ',')
                    {
                        flush();
                        tokens.Add("OR");
                        continue;
                    }

                    if (char.IsWhiteSpace(c))
                    {
                        flush();
                        continue;
                    }

                    current.Append(c);
                }

                if (inQuotes)
                    throw new SmartSearchParseException("Missing closing quote.");

                flush();
                return tokens;
            }
        }

        private static IEnumerable<double> GetNumericCandidateValues(BibtexEntry entry, string field)
        {
            return GetCandidateValues(entry, field)
                .Select(value =>
                {
                    if (TryParseNumber(value, out double numericValue))
                        return new double?(numericValue);

                    return null;
                })
                .Where(value => value.HasValue)
                .Select(value => value.Value);
        }

        private static IEnumerable<string> GetCandidateValues(BibtexEntry entry, string field)
        {
            if (entry == null)
                return Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(field) ||
                string.Equals(field, "*", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field, "all", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field, "any", StringComparison.OrdinalIgnoreCase))
            {
                List<string> values = new List<string>();

                if (string.IsNullOrWhiteSpace(entry.Key) == false)
                    values.Add(entry.Key);

                if (string.IsNullOrWhiteSpace(entry.Type) == false)
                    values.Add(entry.Type);

                foreach (BibtexTag tag in entry.Tags ?? Array.Empty<BibtexTag>())
                {
                    if (tag == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(tag.Key) == false)
                        values.Add(tag.Key);

                    if (string.IsNullOrWhiteSpace(tag.Value) == false)
                        values.Add(tag.Value);
                }

                return values;
            }

            if (string.Equals(field, "key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field, "entrykey", StringComparison.OrdinalIgnoreCase))
                return new[] { entry.Key ?? string.Empty };

            if (string.Equals(field, "type", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field, "entrytype", StringComparison.OrdinalIgnoreCase))
                return new[] { entry.Type ?? string.Empty };

            if (string.Equals(field, "tag", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field, "tags", StringComparison.OrdinalIgnoreCase))
            {
                return (entry.Tags ?? Array.Empty<BibtexTag>())
                    .Where(tag => tag != null && string.IsNullOrWhiteSpace(tag.Key) == false)
                    .Select(tag => tag.Key);
            }

            return (entry.Tags ?? Array.Empty<BibtexTag>())
                .Where(tag => tag != null && string.Equals(tag.Key, field, StringComparison.OrdinalIgnoreCase))
                .Select(tag => tag.Value ?? string.Empty);
        }

        private static bool TryParseComparisonOperator(string token, out NumericComparisonOperator comparisonOperator)
        {
            comparisonOperator = NumericComparisonOperator.Equal;
            switch (token)
            {
                case "=":
                    comparisonOperator = NumericComparisonOperator.Equal;
                    return true;
                case ">":
                    comparisonOperator = NumericComparisonOperator.GreaterThan;
                    return true;
                case ">=":
                    comparisonOperator = NumericComparisonOperator.GreaterThanOrEqual;
                    return true;
                case "<":
                    comparisonOperator = NumericComparisonOperator.LessThan;
                    return true;
                case "<=":
                    comparisonOperator = NumericComparisonOperator.LessThanOrEqual;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseNumericRange(string value, out double minValue, out double maxValue)
        {
            minValue = 0;
            maxValue = 0;

            Match rangeMatch = Regex.Match(
                value ?? string.Empty,
                @"^\s*(?<min>[+-]?\d+(?:[.,]\d+)?)\s*-\s*(?<max>[+-]?\d+(?:[.,]\d+)?)\s*$");

            if (!rangeMatch.Success)
                return false;

            if (!TryParseNumber(rangeMatch.Groups["min"].Value, out minValue) ||
                !TryParseNumber(rangeMatch.Groups["max"].Value, out maxValue))
                return false;

            if (minValue > maxValue)
            {
                double tmp = minValue;
                minValue = maxValue;
                maxValue = tmp;
            }

            return true;
        }

        private static bool TryParseNumber(string value, out double numericValue)
        {
            if (double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out numericValue))
            {
                return true;
            }

            return double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture,
                out numericValue);
        }

        private sealed class SmartSearchParseException : Exception
        {
            public SmartSearchParseException(string message)
                : base(message)
            {
            }
        }
    }
}
