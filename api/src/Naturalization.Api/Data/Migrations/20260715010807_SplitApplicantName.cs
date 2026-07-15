using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Naturalization.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitApplicantName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Applicants");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Applicants",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Applicants",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "Applicants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "Applicants");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Applicants",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
