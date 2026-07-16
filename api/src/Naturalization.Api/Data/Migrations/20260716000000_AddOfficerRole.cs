using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Naturalization.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /*
             * Backfill existing officers as "Admin", not the model default of
             * "Officer".
             *
             * Before roles existed, anyone who could sign in could do everything,
             * withdrawals and restores included. Defaulting the rows that predate
             * this column to Admin preserves exactly that access for an upgraded
             * database — nobody who could withdraw a record yesterday is locked
             * out of it today. New accounts inserted after this migration get the
             * entity default (Officer) instead.
             */
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Officers",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Admin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Officers");
        }
    }
}
