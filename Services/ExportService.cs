using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AttandenceDesktop.Models;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Helpers;

namespace AttandenceDesktop.Services
{
    /// <summary>
    /// Provides utilities for exporting <see cref="AttendanceReportItem"/> collections
    /// to a variety of formats (Excel, PDF, CSV, Word, TXT).
    /// All paged documents are generated in landscape orientation.
    /// </summary>
    public class ExportService
    {
        // Static constructor to set QuestPDF license once when the class is first used
        static ExportService()
        {
            // Set QuestPDF license to Community to avoid license validation errors
            QuestPDF.Settings.License = LicenseType.Community;
        }
        
        public async Task ExportAsync(IEnumerable<AttendanceReportItem> data, string format, string filePath)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrWhiteSpace(format)) throw new ArgumentException("Format must be provided", nameof(format));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path must be provided", nameof(filePath));

            // Materialise to avoid multiple enumeration and to keep consistent order
            var list = data.ToList();

            switch (format.ToLowerInvariant())
            {
                case "excel":
                case "xlsx":
                    await ExportToExcelAsync(list, filePath);
                    break;
                case "csv":
                    await ExportToCsvAsync(list, filePath);
                    break;
                case "pdf":
                    await ExportToPdfAsync(list, filePath);
                    break;
                case "word":
                case "docx":
                    await ExportToWordAsync(list, filePath);
                    break;
                case "txt":
                    await ExportToTxtAsync(list, filePath);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported export format: {format}");
            }
        }

        #region Excel
        private Task ExportToExcelAsync(IReadOnlyList<AttendanceReportItem> data, string filePath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("Report");

            // Group data by employee and date
            var employeeData = data
                .GroupBy(x => new { x.EmployeeId, x.EmployeeName, x.DepartmentName })
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(x => x.Date.Date, x => x)
                );

            // Sort employees by name for consistent ordering
            var sortedEmployeeData = employeeData
                .OrderBy(x => x.Key.EmployeeName)
                .ToList();

            // Find date range (first day to last day)
            var allDates = data.Select(x => x.Date.Date).Distinct().OrderBy(x => x).ToList();
            var startDate = allDates.FirstOrDefault();
            var endDate = allDates.LastOrDefault();
            
            // Generate all dates in range for complete calendar
            var dateRange = new List<DateTime>();
            if (startDate != default && endDate != default)
            {
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    dateRange.Add(date);
                }
            }

            // Header row with dates
            ws.Cell(2, 1).Value = "Employee";
            ws.Cell(2, 2).Value = "Department";
            
            for (int i = 0; i < dateRange.Count; i++)
            {
                var date = dateRange[i];
                ws.Cell(2, i + 3).Value = date.ToString("MMM dd");
                ws.Cell(3, i + 3).Value = date.ToString("ddd");
            }

            // Add summary column headers after dates
            int summaryStartCol = dateRange.Count + 3;
            ws.Cell(2, summaryStartCol).Value = "LE Hrs";  // Late / Early
            ws.Cell(2, summaryStartCol + 1).Value = "OT Hrs"; // Overtime
            ws.Cell(2, summaryStartCol + 2).Value = "PR Hrs"; // Present
            ws.Cell(2, summaryStartCol + 3).Value = "EX Hrs"; // Expected
            ws.Cell(2, summaryStartCol + 4).Value = "Absent";

            // Style the header rows (now include summary columns)
            var headerRange = ws.Range(2, 1, 3, summaryStartCol + 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.OutsideBorderColor = XLColor.Black;

            // Data rows start at row 4 now
            int rowIndex = 4;
            const int EmployeesPerPage = 10; // number of employees per A4 page
            int employeeCounter = 0;         // track how many employees have been written so far
            foreach (var employee in sortedEmployeeData)
            {
                var employeeKey = employee.Key;
                var attendanceByDate = employee.Value;

                ws.Cell(rowIndex, 1).Value = employeeKey.EmployeeName;
                ws.Cell(rowIndex, 2).Value = employeeKey.DepartmentName;

                // Add status for each date
                for (int i = 0; i < dateRange.Count; i++)
                {
                    var date = dateRange[i];
                    var cell = ws.Cell(rowIndex, i + 3);
                    
                    if (attendanceByDate.TryGetValue(date, out var attendance))
                    {
                        var code = GetDisplayCode(attendance);
                        if (code == "0.5")
                        {
                            cell.Value = 0.5;
                            cell.Style.NumberFormat.Format = "0.0";
                        }
                        else
                        {
                            cell.Value = code;
                        }
                        var color = GetStatusColor(attendance.Status);
                        if (color != null)
                        {
                            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thick;
                            cell.Style.Border.BottomBorderColor = color;
                        }
                    }
                    
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.Black;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // Calculate summary values for this employee
                var items = attendanceByDate.Values.ToList();
                double lateEarlyHours = items.Sum(it => (it.LateMinutes?.TotalHours ?? 0) + (it.EarlyDepartureMinutes?.TotalHours ?? 0));
                double overtimeHours = items.Sum(it => it.OvertimeMinutes?.TotalHours ?? 0);
                double presentHours = items.Sum(it => it.WorkDuration?.TotalHours ?? 0);
                double expectedHours = items.Sum(it => it.ExpectedWorkHours);
                int absentDays = items.Count(it => !it.IsHoliday && !it.IsNonWorkingDay && !it.CheckInTime.HasValue);

                // Fill summary columns
                ws.Cell(rowIndex, summaryStartCol).Value = lateEarlyHours;
                ws.Cell(rowIndex, summaryStartCol + 1).Value = overtimeHours;
                ws.Cell(rowIndex, summaryStartCol + 2).Value = presentHours;
                ws.Cell(rowIndex, summaryStartCol + 3).Value = expectedHours;
                ws.Cell(rowIndex, summaryStartCol + 4).Value = absentDays;

                // Numeric format for hours columns
                ws.Range(rowIndex, summaryStartCol, rowIndex, summaryStartCol + 3).Style.NumberFormat.Format = "0.0";

                // Borders for summary columns
                ws.Range(rowIndex, summaryStartCol, rowIndex, summaryStartCol + 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(rowIndex, summaryStartCol, rowIndex, summaryStartCol + 4).Style.Border.OutsideBorderColor = XLColor.Black;
                ws.Range(rowIndex, summaryStartCol, rowIndex, summaryStartCol + 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Borders for first two columns
                ws.Cell(rowIndex, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Cell(rowIndex, 1).Style.Border.OutsideBorderColor = XLColor.Black;
                ws.Cell(rowIndex, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Cell(rowIndex, 2).Style.Border.OutsideBorderColor = XLColor.Black;

                rowIndex++;

                employeeCounter++;

                // Insert a manual horizontal page break after every 10 employees to ensure
                // each A4 page contains at most 10 employees when printed.
                if (employeeCounter % EmployeesPerPage == 0)
                {
                    // ClosedXML uses the row index (1-based) where the break is placed *below* that row.
                    // We therefore add the break at the current rowIndex, which already points to the
                    // next (unused) row after incrementing above.
                    ws.PageSetup.AddHorizontalPageBreak(rowIndex);
                }
            }

            ws.Columns().AdjustToContents();

            // === Row height adjustments ===
            // Make rows taller to create larger square-like cells for better readability
            if (rowIndex > 3)
            {
                // Apply a uniform height to all populated rows (headers + data)
                ws.Rows(2, rowIndex - 1).Height = 30; // 30 points height for square appearance

                // Optionally make the header rows slightly taller for emphasis
                ws.Row(2).Height = 32;
                ws.Row(3).Height = 32;
            }

            // === Center align text in all populated cells ===
            var usedRange = ws.Range(2, 1, rowIndex - 1, summaryStartCol + 4);
            usedRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // Configure page setup for proper printing on A4 landscape
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            // Only fit width to 1 page, but allow multiple pages vertically (0 = auto)
            ws.PageSetup.FitToPages(1, 0);

            // Set print area to include all data
            var printRange = ws.Range(2, 1, rowIndex - 1, summaryStartCol + 4);
            ws.PageSetup.PrintAreas.Clear();
            ws.PageSetup.PrintAreas.Add($"{printRange.RangeAddress}");
            
            // Add header and footer
            ws.PageSetup.Header.Left.AddText("Attendance Report");
            ws.PageSetup.Header.Right.AddText(DateTime.Now.ToString("yyyy-MM-dd"));
            ws.PageSetup.Footer.Center.AddText(XLHFPredefinedText.PageNumber);
            ws.PageSetup.Footer.Center.AddText(" of ");
            ws.PageSetup.Footer.Center.AddText(XLHFPredefinedText.NumberOfPages);
            
            // Repeat header rows on each page
            ws.PageSetup.SetRowsToRepeatAtTop(2, 3);

            workbook.SaveAs(filePath);
            return Task.CompletedTask;
        }
        
        // New helper replicating UI logic for status code
        private string GetDisplayCode(AttendanceReportItem item)
        {
            if (item == null)
                return string.Empty;

            // Holidays and non-working days first
            if (item.IsHoliday)
                return "H";
            if (item.IsNonWorkingDay)
                return "W";

            // Absent (no check-in/out)
            if (!item.CheckInTime.HasValue)
                return "A";

            if (item.IsFlexibleSchedule)
            {
                // Half-day based on percentage or explicit status first
                if (item.Status.Equals("Half Day", StringComparison.OrdinalIgnoreCase) ||
                    (item.ExpectedWorkHours > 0 && item.WorkDuration.HasValue &&
                     item.WorkHoursPercentage >= 40 && item.WorkHoursPercentage < 90))
                {
                    return "0.5";
                }

                // Then overtime
                if (item.IsOvertime)
                    return "O";

                return "P";
            }
            else
            {
                // Regular schedule rules
                if (item.Status.Equals("Half Day", StringComparison.OrdinalIgnoreCase))
                    return "0.5";

                if (item.IsLate && item.IsEarlyDeparture)
                    return "L+E";
                if (item.IsLate)
                    return "L";
                if (item.IsEarlyDeparture)
                    return "E";
                if (item.IsOvertime)
                    return "O";
                if (item.IsEarlyArrival)
                    return "EA";
                return "P";
            }
        }
        
        // Helper method to get color for status
        private XLColor GetStatusColor(string status)
        {
            return status?.ToUpper() switch
            {
                "PRESENT" => XLColor.Green,
                "ABSENT" => XLColor.Red,
                "LEAVE" => XLColor.Orange,
                "EXCUSED" => XLColor.Orange,
                "OVERTIME" => XLColor.Cyan,
                "HALF_DAY" => XLColor.Green,
                "EARLY_ARRIVAL" => XLColor.Green,
                _ => null
            };
        }
        #endregion

        #region CSV / TXT
        private Task ExportToCsvAsync(IReadOnlyList<AttendanceReportItem> data, string filePath)
        {
            // Group data by employee and date
            var employeeData = data
                .GroupBy(x => new { x.EmployeeId, x.EmployeeName, x.DepartmentName })
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(x => x.Date.Date, x => x)
                );

            // Find date range (first day to last day)
            var allDates = data.Select(x => x.Date.Date).Distinct().OrderBy(x => x).ToList();
            var startDate = allDates.FirstOrDefault();
            var endDate = allDates.LastOrDefault();
            
            // Generate all dates in range for complete calendar
            var dateRange = new List<DateTime>();
            if (startDate != default && endDate != default)
            {
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    dateRange.Add(date);
                }
            }

            var sb = new StringBuilder();
            
            // Header row with dates
            sb.Append("Employee,Department");
            foreach (var date in dateRange)
            {
                sb.Append($",{date.ToString("MMM dd")}");
            }
            sb.AppendLine();
            
            // Subheader with day names
            sb.Append(","); // Empty cells for Employee and Department
            foreach (var date in dateRange)
            {
                sb.Append($",{date.ToString("ddd")}");
            }
            sb.AppendLine();
            
            // Data rows for each employee
            foreach (var employee in employeeData)
            {
                var employeeKey = employee.Key;
                var attendanceByDate = employee.Value;
                
                sb.Append($"{EscapeCsv(employeeKey.EmployeeName)},{EscapeCsv(employeeKey.DepartmentName)}");
                
                // Add status for each date
                foreach (var date in dateRange)
                {
                    if (attendanceByDate.TryGetValue(date, out var attendance))
                    {
                        string statusCode = GetDisplayCode(attendance);
                        sb.Append($",{EscapeCsv(statusCode)}");
                    }
                    else
                    {
                        sb.Append(",");
                    }
                }
                sb.AppendLine();
            }
            
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            return Task.CompletedTask;
        }

        private Task ExportToTxtAsync(IReadOnlyList<AttendanceReportItem> data, string filePath)
        {
            // Group data by employee and date
            var employeeData = data
                .GroupBy(x => new { x.EmployeeId, x.EmployeeName, x.DepartmentName })
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(x => x.Date.Date, x => x)
                );

            // Find date range (first day to last day)
            var allDates = data.Select(x => x.Date.Date).Distinct().OrderBy(x => x).ToList();
            var startDate = allDates.FirstOrDefault();
            var endDate = allDates.LastOrDefault();
            
            // Generate all dates in range for complete calendar
            var dateRange = new List<DateTime>();
            if (startDate != default && endDate != default)
            {
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    dateRange.Add(date);
                }
            }

            var lines = new List<string>();
            
            // Header row with dates
            var headerLine = "Employee\tDepartment";
            foreach (var date in dateRange)
            {
                headerLine += $"\t{date.ToString("MMM dd")}";
            }
            lines.Add(headerLine);
            
            // Subheader with day names
            var subheaderLine = "\t"; // Empty cells for Employee and Department
            foreach (var date in dateRange)
            {
                subheaderLine += $"\t{date.ToString("ddd")}";
            }
            lines.Add(subheaderLine);
            
            // Data rows for each employee
            foreach (var employee in employeeData)
            {
                var employeeKey = employee.Key;
                var attendanceByDate = employee.Value;
                
                var line = $"{employeeKey.EmployeeName}\t{employeeKey.DepartmentName}";
                
                // Add status for each date
                foreach (var date in dateRange)
                {
                    if (attendanceByDate.TryGetValue(date, out var attendance))
                    {
                        string statusCode = GetDisplayCode(attendance);
                        line += $"\t{statusCode}";
                    }
                    else
                    {
                        line += "\t";
                    }
                }
                lines.Add(line);
            }
            
            File.WriteAllLines(filePath, lines, Encoding.UTF8);
            return Task.CompletedTask;
        }
        #endregion

        #region PDF
        private Task ExportToPdfAsync(IReadOnlyList<AttendanceReportItem> data, string filePath)
        {
            // Group data by employee and date
            var employeeData = data
                .GroupBy(x => new { x.EmployeeId, x.EmployeeName, x.DepartmentName })
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(x => x.Date.Date, x => x)
                );

            // Find date range (first day to last day)
            var allDates = data.Select(x => x.Date.Date).Distinct().OrderBy(x => x).ToList();
            var startDate = allDates.FirstOrDefault();
            var endDate = allDates.LastOrDefault();
            
            // Generate all dates in range for complete calendar
            var dateRange = new List<DateTime>();
            if (startDate != default && endDate != default)
            {
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    dateRange.Add(date);
                }
            }

            // QuestPDF expects synchronous generation. We return Task afterwards.
            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Content().Table(table =>
                    {
                        // Define columns: Employee, Department, and one for each date
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Employee
                            columns.RelativeColumn(2); // Department
                            
                            // Add columns for each date
                            foreach (var _ in dateRange)
                            {
                                columns.RelativeColumn(1); // Date column
                            }

                            // Add columns for summary values (wider to fit numbers)
                            columns.RelativeColumn(2); // Late/Early
                            columns.RelativeColumn(2); // Overtime
                            columns.RelativeColumn(2); // Present
                            columns.RelativeColumn(2); // Expected
                            columns.RelativeColumn(2); // Absent
                        });

                        // Header row with dates
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Employee");
                            header.Cell().Element(HeaderCellStyle).Text("Department");
                            
                            foreach (var date in dateRange)
                            {
                                header.Cell().Element(HeaderCellStyle).Column(column =>
                                {
                                    column.Item().AlignCenter().Text(date.ToString("MMM dd"));
                                    column.Item().AlignCenter().Text(date.ToString("ddd"));
                                });
                            }

                            // Summary header cells (single line, shorter labels)
                            header.Cell().Element(HeaderCellStyle).Text("L/E");
                            header.Cell().Element(HeaderCellStyle).Text("OT");
                            header.Cell().Element(HeaderCellStyle).Text("PR");
                            header.Cell().Element(HeaderCellStyle).Text("EX");
                            header.Cell().Element(HeaderCellStyle).Text("Abs");
                        });

                        // Data rows for each employee
                        foreach (var employee in employeeData)
                        {
                            var employeeKey = employee.Key;
                            var attendanceByDate = employee.Value;

                            table.Cell().Element(CellStyle).Text(employeeKey.EmployeeName);
                            table.Cell().Element(CellStyle).Text(employeeKey.DepartmentName);
                            
                            // Add status for each date
                            foreach (var date in dateRange)
                            {
                                if (attendanceByDate.TryGetValue(date, out var attendance))
                                {
                                    string statusCode = GetDisplayCode(attendance);
                                    string colorHex = GetStatusIndicatorColorHex(statusCode);

                                    table.Cell().Element(cell =>
                                    {
                                        cell.Border(0.5f).BorderColor("#DDDDDD").Padding(1).Column(col =>
                                        {
                                            // Status code text centred in the remaining space
                                            col.Item().AlignCenter().AlignMiddle().Text(statusCode)
                                                .FontSize(9)
                                                .FontFamily("Helvetica")
                                                .SemiBold();

                                            // Bottom indicator bar with the specified colour
                                            col.Item().Height(4).Background(colorHex);
                                        });
                                    });
                                }
                                else
                                {
                                    table.Cell().Element(cell =>
                                    {
                                        cell.Border(0.5f).BorderColor("#DDDDDD").Padding(1).Column(col =>
                                        {
                                            col.Item().AlignCenter().AlignMiddle().Text(string.Empty);
                                            col.Item().Height(4).Background("#BDBDBD");
                                        });
                                    });
                                }
                            }

                            // === Summary columns ===
                            var items = attendanceByDate.Values.ToList();
                            double lateEarlyHours = items.Sum(it => (it.LateMinutes?.TotalHours ?? 0) + (it.EarlyDepartureMinutes?.TotalHours ?? 0));
                            double overtimeHours = items.Sum(it => it.OvertimeMinutes?.TotalHours ?? 0);
                            double presentHours = items.Sum(it => it.WorkDuration?.TotalHours ?? 0);
                            double expectedHours = items.Sum(it => it.ExpectedWorkHours);
                            int absentDays = items.Count(it => !it.IsHoliday && !it.IsNonWorkingDay && !it.CheckInTime.HasValue);

                            // Late/Early
                            table.Cell().Element(CellStyle).Text(lateEarlyHours.ToString("F1"));
                            // Overtime
                            table.Cell().Element(CellStyle).Text(overtimeHours.ToString("F1"));
                            // Present
                            table.Cell().Element(CellStyle).Text(presentHours.ToString("F1"));
                            // Expected
                            table.Cell().Element(CellStyle).Text(expectedHours.ToString("F1"));
                            // Absent Days
                            table.Cell().Element(CellStyle).Text(absentDays.ToString());
                        }
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                });
            }).GeneratePdf(filePath);

            return Task.CompletedTask;
        }

        // Helper method for PDF data cell styling (employee, department)
        private static IContainer CellStyle(IContainer container) =>
            container.Border(0.5f).BorderColor("#DDDDDD").Padding(2).AlignCenter().AlignMiddle();

        // Helper method for PDF header cell styling
        private static IContainer HeaderCellStyle(IContainer container) =>
            container.Border(0.5f).BorderColor("#DDDDDD").Background("#F5F5F5")
                     .Padding(2).AlignCenter().AlignMiddle();

        // Helper to map status code to indicator colour (hex) matching UI style
        private static string GetStatusIndicatorColorHex(string statusCode)
        {
            // Normalise to upper for comparison
            statusCode = statusCode?.ToUpperInvariant() ?? string.Empty;

            return statusCode switch
            {
                "P" => "#4CAF50",           // Present (green)
                "0.5" => "#4CAF50",         // Half-day (same green)
                "A" => "#F44336",           // Absent (red)
                "L" => "#FF9800",           // Late arrival (orange)
                "E" => "#FF5722",           // Early departure (deep orange)
                "EA" => "#8BC34A",          // Early arrival (light green)
                "O" => "#00BCD4",           // Overtime (cyan)
                "H" => "#2196F3",           // Holiday (blue)
                "W" => "#9C27B0",           // Weekend (purple)
                "NH" => "#9E9E9E",          // Not hired / future date (grey)
                _ => "#9E9E9E"               // Default grey
            };
        }
        #endregion

        #region Word
        private Task ExportToWordAsync(IReadOnlyList<AttendanceReportItem> data, string filePath)
        {
            // Group data by employee and date
            var employeeData = data
                .GroupBy(x => new { x.EmployeeId, x.EmployeeName, x.DepartmentName })
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(x => x.Date.Date, x => x)
                );

            // Find date range (first day to last day)
            var allDates = data.Select(x => x.Date.Date).Distinct().OrderBy(x => x).ToList();
            var startDate = allDates.FirstOrDefault();
            var endDate = allDates.LastOrDefault();
            
            // Generate all dates in range for complete calendar
            var dateRange = new List<DateTime>();
            if (startDate != default && endDate != default)
            {
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    dateRange.Add(date);
                }
            }

            using (var document = WordprocessingDocument.Create(filePath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                // Main part
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = mainPart.Document.AppendChild(new Body());

                // Landscape section
                var sectionProps = new SectionProperties();
                var pageSize = new DocumentFormat.OpenXml.Wordprocessing.PageSize { Width = 16840, Height = 11907, Orient = PageOrientationValues.Landscape };
                sectionProps.Append(pageSize);
                body.Append(sectionProps);

                // Table
                var table = new Table();
                var tblProps = new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableLayout { Type = TableLayoutValues.Fixed }
                );
                table.AppendChild(tblProps);

                // Define table grid (columns)
                var tblGrid = new TableGrid();
                tblGrid.Append(new GridColumn { Width = "1200" }); // Employee
                tblGrid.Append(new GridColumn { Width = "1000" }); // Department
                
                // Date columns
                foreach (var _ in dateRange)
                {
                    tblGrid.Append(new GridColumn { Width = "400" });
                }
                
                table.AppendChild(tblGrid);

                // Header row with dates
                var headerRow = new TableRow();
                
                // Employee and Department headers
                headerRow.Append(CreateHeaderCell("Employee"));
                headerRow.Append(CreateHeaderCell("Department"));
                
                // Date headers
                foreach (var date in dateRange)
                {
                    headerRow.Append(CreateHeaderCell(date.ToString("MMM dd")));
                }
                
                table.AppendChild(headerRow);
                
                // Subheader row with day names
                var subheaderRow = new TableRow();
                
                // Empty cells for Employee and Department
                subheaderRow.Append(CreateCell(""));
                subheaderRow.Append(CreateCell(""));
                
                // Day name headers
                foreach (var date in dateRange)
                {
                    subheaderRow.Append(CreateHeaderCell(date.ToString("ddd")));
                }
                
                table.AppendChild(subheaderRow);

                // Data rows for each employee
                foreach (var employee in employeeData)
                {
                    var employeeKey = employee.Key;
                    var attendanceByDate = employee.Value;
                    
                    var row = new TableRow();
                    
                    // Employee and Department
                    row.Append(CreateCell(employeeKey.EmployeeName));
                    row.Append(CreateCell(employeeKey.DepartmentName));
                    
                    // Status for each date
                    foreach (var date in dateRange)
                    {
                        if (attendanceByDate.TryGetValue(date, out var attendance))
                        {
                            string statusCode = GetDisplayCode(attendance);
                            row.Append(CreateCell(statusCode));
                        }
                        else
                        {
                            row.Append(CreateCell(""));
                        }
                    }
                    
                    table.AppendChild(row);
                }

                body.Append(table);
                mainPart.Document.Save();
            }
            return Task.CompletedTask;
        }

        private static TableCell CreateHeaderCell(string text)
        {
            var cell = new TableCell();
            var paragraph = new Paragraph(new Run(new Text(text)));
            
            // Bold text
            paragraph.ParagraphProperties = new ParagraphProperties();
            paragraph.ParagraphProperties.AppendChild(new Justification { Val = JustificationValues.Center });
            
            var runProperties = new RunProperties();
            runProperties.AppendChild(new Bold());
            paragraph.GetFirstChild<Run>().PrependChild(runProperties);
            
            // Cell properties
            var cellProps = new TableCellProperties();
            cellProps.AppendChild(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            cellProps.AppendChild(new Shading { 
                Fill = "DDDDDD", 
                Val = ShadingPatternValues.Clear, 
                Color = "auto" 
            });
            
            cell.AppendChild(cellProps);
            cell.AppendChild(paragraph);
            return cell;
        }

        private static TableCell CreateCell(string text)
        {
            var cell = new TableCell();
            var paragraph = new Paragraph(new Run(new Text(text)));
            
            // Center alignment
            paragraph.ParagraphProperties = new ParagraphProperties();
            paragraph.ParagraphProperties.AppendChild(new Justification { Val = JustificationValues.Center });
            
            // Cell properties
            var cellProps = new TableCellProperties();
            cellProps.AppendChild(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
            
            cell.AppendChild(cellProps);
            cell.AppendChild(paragraph);
            return cell;
        }
        #endregion

        // Helper for CSV escaping of fields containing commas, quotes, or newlines
        private static string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }
    }
} 