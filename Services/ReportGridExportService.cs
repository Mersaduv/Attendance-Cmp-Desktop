using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AttandenceDesktop.Models;
using ClosedXML.Excel;

namespace AttandenceDesktop.Services
{
    /// <summary>
    /// Exports the tabular data shown in ReportView (single row per attendance entry)
    /// to an Excel workbook that mimics the on-screen grid order and formatting.
    /// </summary>
    public class ReportGridExportService
    {
        public async Task ExportToExcelAsync(IEnumerable<AttendanceReportItem> data, string filePath)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path must be provided", nameof(filePath));

            using var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("Report");

            // === Header row styling ===
            string[] headers =
            {
                "Date", "Employee", "Department", "Check In", "Check Out", "Duration",
                "Status", "Late", "Early Arrival", "Left Early", "Overtime"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.Black;
            }

            // Freeze header row
            ws.SheetView.FreezeRows(1);

            // Sort data by date then employee for consistent ordering
            var rows = data.OrderBy(r => r.Date).ThenBy(r => r.EmployeeName).ToList();

            int rowIndex = 2;
            int employeeCounter = 0;
            int employeesPerPage = 10;
            int? currentEmployeeId = null;
            foreach (var item in rows)
            {
                // Detect change of employee to count
                if (currentEmployeeId != item.EmployeeId)
                {
                    currentEmployeeId = item.EmployeeId;
                    employeeCounter++;
                    if (employeeCounter > 0 && employeeCounter % employeesPerPage == 1 && rowIndex > 2)
                    {
                        // Insert page break above this row (ClosedXML uses row index where break placed below)
                        ws.PageSetup.AddHorizontalPageBreak(rowIndex - 1);
                    }
                }
                ws.Cell(rowIndex, 1).Value = item.Date.ToString("yyyy-MM-dd");
                ws.Cell(rowIndex, 2).Value = item.EmployeeName;
                ws.Cell(rowIndex, 3).Value = item.DepartmentName;
                ws.Cell(rowIndex, 4).Value = item.CheckInTime?.ToString("HH:mm:ss") ?? string.Empty;
                ws.Cell(rowIndex, 5).Value = item.CheckOutTime?.ToString("HH:mm:ss") ?? string.Empty;
                ws.Cell(rowIndex, 6).Value = item.WorkDuration?.ToString(@"hh\:mm\:ss") ?? string.Empty;
                ws.Cell(rowIndex, 7).Value = item.Status;
                ws.Cell(rowIndex, 8).Value = item.LateMinutes?.ToString(@"hh\:mm") ?? "-";
                ws.Cell(rowIndex, 9).Value = item.EarlyArrivalMinutes?.ToString(@"hh\:mm") ?? "-";
                ws.Cell(rowIndex, 10).Value = item.EarlyDepartureMinutes?.ToString(@"hh\:mm") ?? "-";
                ws.Cell(rowIndex, 11).Value = item.OvertimeMinutes?.ToString(@"hh\:mm") ?? "-";

                // Center align time cells
                ws.Range(rowIndex, 4, rowIndex, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Borders
                var dataRange = ws.Range(rowIndex, 1, rowIndex, headers.Length);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.OutsideBorderColor = XLColor.Black;

                // Zebra striping
                if ((rowIndex % 2) == 0)
                {
                    dataRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F9F9F9");
                }

                rowIndex++;
            }

            // Auto size columns based on content first
            ws.Columns(1, headers.Length).AdjustToContents();

            // Ensure columns are not too narrow; enforce a minimum width
            for (int col = 1; col <= headers.Length; col++)
            {
                var column = ws.Column(col);
                if (column.Width < 13) column.Width = 13; // widen narrow columns
            }

            // Center text horizontally and vertically in all populated cells
            var usedRange = ws.Range(1, 1, rowIndex - 1, headers.Length);
            usedRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // === Row height adjustments ===
            // Apply uniform height to every row that has been written
            if (rowIndex > 1)
            {
                // Set all rows (header + data) to 30 points
                ws.Rows(1, rowIndex - 1).Height = 30;

                // Optionally, make the header row slightly taller for emphasis
                ws.Row(1).Height = 32;
            }

            // Page setup for A4 Landscape
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0); // fit width

            // Center the sheet horizontally on the page
            ws.PageSetup.CenterHorizontally = true;

            // Reduce margins to utilise page area
            ws.PageSetup.Margins.Left = 0.3;
            ws.PageSetup.Margins.Right = 0.3;
            ws.PageSetup.Margins.Top = 0.5;
            ws.PageSetup.Margins.Bottom = 0.5;

            // Repeat header row on every page
            ws.PageSetup.SetRowsToRepeatAtTop(1, 1);

            // Save
            workbook.SaveAs(filePath);
            await Task.CompletedTask;
        }
    }
} 