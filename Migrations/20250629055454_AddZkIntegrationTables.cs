using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AttandenceDesktop.Migrations
{
    /// <inheritdoc />
    public partial class AddZkIntegrationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IPAddress = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    MachineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSyncTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkCalendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    EntryType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRecurringAnnually = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsFlexibleSchedule = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    TotalWorkHours = table.Column<double>(type: "REAL", nullable: false),
                    IsWorkingDaySunday = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsWorkingDayMonday = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsWorkingDayTuesday = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsWorkingDayWednesday = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsWorkingDayThursday = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsWorkingDayFriday = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsWorkingDaySaturday = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FlexTimeAllowanceMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DepartmentId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DepartmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkScheduleId = table.Column<int>(type: "INTEGER", nullable: true),
                    EmployeeCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ZkUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    FingerprintTemplate1 = table.Column<byte[]>(type: "BLOB", nullable: true),
                    FingerprintTemplate2 = table.Column<byte[]>(type: "BLOB", nullable: true),
                    Position = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    HireDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsFlexibleHours = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiredWorkHoursPerDay = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_WorkSchedules_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalTable: "WorkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CheckOutTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    WorkDuration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    IsComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLateArrival = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEarlyDeparture = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsOvertime = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsEarlyArrival = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LateMinutes = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    EarlyDepartureMinutes = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    OvertimeMinutes = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    EarlyArrivalMinutes = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    IsFlexibleSchedule = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpectedWorkHours = table.Column<double>(type: "REAL", nullable: false),
                    AttendanceCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PunchLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    PunchTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PunchType = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceRowId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunchLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PunchLogs_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PunchLogs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "HR" },
                    { 2, "IT" },
                    { 3, "Finance" },
                    { 4, "Operations" },
                    { 5, "Marketing" }
                });

            migrationBuilder.InsertData(
                table: "WorkSchedules",
                columns: new[] { "Id", "DepartmentId", "Description", "EndTime", "FlexTimeAllowanceMinutes", "IsFlexibleSchedule", "IsWorkingDayFriday", "IsWorkingDayMonday", "IsWorkingDaySaturday", "IsWorkingDaySunday", "IsWorkingDayThursday", "IsWorkingDayTuesday", "IsWorkingDayWednesday", "Name", "StartTime", "TotalWorkHours" },
                values: new object[] { 1, null, "Standard work schedule from 9 AM to 5 PM, Monday to Friday", new TimeSpan(0, 17, 0, 0, 0), 15, false, true, true, false, false, true, true, true, "Standard 9-5", new TimeSpan(0, 9, 0, 0, 0), 8.0 });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EmployeeId",
                table: "Attendances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_WorkScheduleId",
                table: "Employees",
                column: "WorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_PunchLogs_DeviceId",
                table: "PunchLogs",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_PunchLogs_EmployeeId",
                table: "PunchLogs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_DepartmentId",
                table: "WorkSchedules",
                column: "DepartmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "PunchLogs");

            migrationBuilder.DropTable(
                name: "WorkCalendars");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "WorkSchedules");

            migrationBuilder.DropTable(
                name: "Departments");
        }
    }
}
