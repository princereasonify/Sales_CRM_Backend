using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CollapseSentToPending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Collapse the legacy "sent" status (link generated, awaiting payment) into
            // "pending" so DB and UI use the same vocabulary: pending | paid | failed.
            migrationBuilder.Sql(@"UPDATE ""PaymentLinks"" SET ""Status"" = 'pending' WHERE ""Status"" = 'sent';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: we cannot reliably distinguish legacy "sent" rows from genuine
            // "pending" rows after the merge.
        }
    }
}
