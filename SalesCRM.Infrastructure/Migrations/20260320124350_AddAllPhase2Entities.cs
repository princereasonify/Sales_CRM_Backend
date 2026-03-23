using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SalesCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAllPhase2Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllowanceConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Scope = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ScopeId = table.Column<int>(type: "integer", nullable: true),
                    RatePerKm = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    MaxDailyAllowance = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    MinDistanceForAllowance = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SetById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowanceConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllowanceConfigs_Users_SetById",
                        column: x => x.SetById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyRoutePlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PlanDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Stops = table.Column<string>(type: "text", nullable: false),
                    TotalEstimatedDistanceKm = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    TotalEstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    TotalActualDistanceKm = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    OptimizationMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRoutePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyRoutePlans_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemoAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeadId = table.Column<int>(type: "integer", nullable: false),
                    SchoolId = table.Column<int>(type: "integer", nullable: false),
                    RequestedById = table.Column<int>(type: "integer", nullable: false),
                    AssignedToId = table.Column<int>(type: "integer", nullable: false),
                    ApprovedById = table.Column<int>(type: "integer", nullable: true),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledStartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ScheduledEndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    DemoMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MeetingLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordingUrl = table.Column<string>(type: "text", nullable: true),
                    RecordingConsentGiven = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemoAssignments_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DemoAssignments_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DemoAssignments_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DemoAssignments_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemoAssignments_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnboardAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeadId = table.Column<int>(type: "integer", nullable: false),
                    DealId = table.Column<int>(type: "integer", nullable: true),
                    SchoolId = table.Column<int>(type: "integer", nullable: false),
                    AssignedToId = table.Column<int>(type: "integer", nullable: false),
                    AssignedById = table.Column<int>(type: "integer", nullable: false),
                    ScheduledStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Modules = table.Column<string>(type: "text", nullable: true),
                    TrainingDates = table.Column<string>(type: "text", nullable: true),
                    CompletionPercentage = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardAssignments_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OnboardAssignments_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OnboardAssignments_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OnboardAssignments_Users_AssignedById",
                        column: x => x.AssignedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardAssignments_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DealId = table.Column<int>(type: "integer", nullable: false),
                    SchoolId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GatewayProvider = table.Column<string>(type: "text", nullable: true),
                    ChequeNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ChequeImageUrl = table.Column<string>(type: "text", nullable: true),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpiId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceiptUrl = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CollectedById = table.Column<int>(type: "integer", nullable: false),
                    VerifiedById = table.Column<int>(type: "integer", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Payments_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Users_CollectedById",
                        column: x => x.CollectedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Users_VerifiedById",
                        column: x => x.VerifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserReassignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OldUserId = table.Column<int>(type: "integer", nullable: false),
                    NewUserId = table.Column<int>(type: "integer", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    ReassignedById = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReassignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserReassignments_Users_NewUserId",
                        column: x => x.NewUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserReassignments_Users_OldUserId",
                        column: x => x.OldUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserReassignments_Users_ReassignedById",
                        column: x => x.ReassignedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VisitFieldConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FieldName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FieldType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Options = table.Column<string>(type: "text", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitFieldConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitFieldConfigs_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisitReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SchoolVisitLogId = table.Column<int>(type: "integer", nullable: true),
                    ActivityId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    SchoolId = table.Column<int>(type: "integer", nullable: true),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PersonMetId = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NextAction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NextActionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextActionNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomFields = table.Column<string>(type: "text", nullable: true),
                    Photos = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitReports_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VisitReports_Contacts_PersonMetId",
                        column: x => x.PersonMetId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VisitReports_SchoolVisitLogs_SchoolVisitLogId",
                        column: x => x.SchoolVisitLogId,
                        principalTable: "SchoolVisitLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VisitReports_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VisitReports_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AllDay = table.Column<bool>(type: "boolean", nullable: false),
                    SchoolId = table.Column<int>(type: "integer", nullable: true),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    DemoAssignmentId = table.Column<int>(type: "integer", nullable: true),
                    OnboardAssignmentId = table.Column<int>(type: "integer", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_DemoAssignments_DemoAssignmentId",
                        column: x => x.DemoAssignmentId,
                        principalTable: "DemoAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_OnboardAssignments_OnboardAssignmentId",
                        column: x => x.OnboardAssignmentId,
                        principalTable: "OnboardAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceConfigs_Scope_ScopeId",
                table: "AllowanceConfigs",
                columns: new[] { "Scope", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceConfigs_SetById",
                table: "AllowanceConfigs",
                column: "SetById");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_DemoAssignmentId",
                table: "CalendarEvents",
                column: "DemoAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_LeadId",
                table: "CalendarEvents",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_OnboardAssignmentId",
                table: "CalendarEvents",
                column: "OnboardAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_SchoolId",
                table: "CalendarEvents",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_UserId_StartTime",
                table: "CalendarEvents",
                columns: new[] { "UserId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyRoutePlans_UserId_PlanDate",
                table: "DailyRoutePlans",
                columns: new[] { "UserId", "PlanDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DemoAssignments_ApprovedById",
                table: "DemoAssignments",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_DemoAssignments_AssignedToId_ScheduledDate",
                table: "DemoAssignments",
                columns: new[] { "AssignedToId", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DemoAssignments_LeadId",
                table: "DemoAssignments",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_DemoAssignments_RequestedById",
                table: "DemoAssignments",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_DemoAssignments_SchoolId",
                table: "DemoAssignments",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_DemoAssignments_Status",
                table: "DemoAssignments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardAssignments_AssignedById",
                table: "OnboardAssignments",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardAssignments_AssignedToId",
                table: "OnboardAssignments",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardAssignments_DealId",
                table: "OnboardAssignments",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardAssignments_LeadId",
                table: "OnboardAssignments",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardAssignments_SchoolId",
                table: "OnboardAssignments",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardAssignments_Status",
                table: "OnboardAssignments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CollectedById",
                table: "Payments",
                column: "CollectedById");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DealId",
                table: "Payments",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SchoolId",
                table: "Payments",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status",
                table: "Payments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_VerifiedById",
                table: "Payments",
                column: "VerifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_UserReassignments_NewUserId",
                table: "UserReassignments",
                column: "NewUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReassignments_OldUserId",
                table: "UserReassignments",
                column: "OldUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReassignments_ReassignedById",
                table: "UserReassignments",
                column: "ReassignedById");

            migrationBuilder.CreateIndex(
                name: "IX_VisitFieldConfigs_CreatedById",
                table: "VisitFieldConfigs",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_VisitReports_ActivityId",
                table: "VisitReports",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitReports_PersonMetId",
                table: "VisitReports",
                column: "PersonMetId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitReports_SchoolId",
                table: "VisitReports",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitReports_SchoolVisitLogId",
                table: "VisitReports",
                column: "SchoolVisitLogId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitReports_UserId",
                table: "VisitReports",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllowanceConfigs");

            migrationBuilder.DropTable(
                name: "CalendarEvents");

            migrationBuilder.DropTable(
                name: "DailyRoutePlans");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "UserReassignments");

            migrationBuilder.DropTable(
                name: "VisitFieldConfigs");

            migrationBuilder.DropTable(
                name: "VisitReports");

            migrationBuilder.DropTable(
                name: "DemoAssignments");

            migrationBuilder.DropTable(
                name: "OnboardAssignments");
        }
    }
}
