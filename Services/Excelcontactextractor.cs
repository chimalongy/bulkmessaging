using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace BulkMessaging.Services
{
    /// <summary>
    /// Reads a single column of values out of an uploaded .xlsx workbook.
    /// The column can be identified by letter ("B"), 1-based index ("2"),
    /// or a header name matched against row 1 ("Email").
    ///
    /// Requires the ClosedXML NuGet package:
    ///   dotnet add package ClosedXML
    /// </summary>
    public static class ExcelContactExtractor
    {
        public static List<string> ExtractColumnValues(Stream fileStream, string columnIdentifier)
        {
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.First();

            var columnNumber = ResolveColumnNumber(worksheet, columnIdentifier);
            if (columnNumber == null)
            {
                throw new InvalidOperationException(
                    $"Couldn't find a column matching \"{columnIdentifier}\". " +
                    "Use a column letter (e.g. \"B\"), a column number (e.g. \"2\"), or the exact header text from row 1.");
            }

            var values = new List<string>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

            // Row 1 is assumed to be the header row, so data starts at row 2.
            for (int row = 2; row <= lastRow; row++)
            {
                var cellValue = worksheet.Cell(row, columnNumber.Value).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(cellValue))
                    values.Add(cellValue);
            }

            return values;
        }

        private static int? ResolveColumnNumber(IXLWorksheet worksheet, string columnIdentifier)
        {
            if (string.IsNullOrWhiteSpace(columnIdentifier))
                return null;

            var trimmed = columnIdentifier.Trim();

            // Pure letters -> spreadsheet-style column letters (A, B, ..., Z, AA, ...)
            if (trimmed.Length > 0 && trimmed.All(char.IsLetter))
            {
                return ColumnLetterToNumber(trimmed);
            }

            // Pure digits -> a 1-based column index
            if (int.TryParse(trimmed, out var index) && index > 0)
            {
                return index;
            }

            // Otherwise, try to match a header name in row 1
            var headerRow = worksheet.Row(1);
            var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            for (int col = 1; col <= lastCol; col++)
            {
                var header = headerRow.Cell(col).GetString().Trim();
                if (string.Equals(header, trimmed, StringComparison.OrdinalIgnoreCase))
                    return col;
            }

            return null;
        }

        private static int? ColumnLetterToNumber(string letters)
        {
            letters = letters.Trim().ToUpperInvariant();
            if (letters.Length == 0 || !letters.All(c => c >= 'A' && c <= 'Z'))
                return null;

            int result = 0;
            foreach (var c in letters)
            {
                result = result * 26 + (c - 'A' + 1);
            }
            return result;
        }
    }
}