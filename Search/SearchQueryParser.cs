using System;
using System.Linq;
using System.Text.RegularExpressions;
using ClipboardManager.Data;

namespace ClipboardManager.Search
{
    public class SearchQueryParser
    {
        public ParsedSearchQuery Parse(string rawQuery)
        {
            if (string.IsNullOrWhiteSpace(rawQuery))
                return new ParsedSearchQuery { FtsSafeExpression = string.Empty };

            // 1. Strip FTS5 reserved characters that cause syntax errors
            string sanitized = Regex.Replace(rawQuery, @"[""*^:()]", " ");

            // 2. Split into distinct search terms
            var terms = sanitized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (terms.Length == 0)
                return new ParsedSearchQuery { FtsSafeExpression = string.Empty };

            // 3. Construct a safe AND-based prefix query for partial word matching
            // Example: User types "api key" -> Outputs: "api"* AND "key"*
            var safeExpression = string.Join(" AND ", terms.Select(term => $"\"{term}\"*"));

            return new ParsedSearchQuery { FtsSafeExpression = safeExpression };
        }
    }
}
