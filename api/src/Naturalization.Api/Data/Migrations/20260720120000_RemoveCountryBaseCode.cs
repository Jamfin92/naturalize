using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Naturalization.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCountryBaseCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BaseCode was "C" for every country row — it carried no information.
            // Drop it and reduce the unique index to Code alone.
            migrationBuilder.DropIndex(
                name: "IX_CountryCodes_BaseCode_Code",
                table: "CountryCodes");

            migrationBuilder.DropColumn(
                name: "BaseCode",
                table: "CountryCodes");

            migrationBuilder.CreateIndex(
                name: "IX_CountryCodes_Code",
                table: "CountryCodes",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CountryCodes_Code",
                table: "CountryCodes");

            migrationBuilder.AddColumn<string>(
                name: "BaseCode",
                table: "CountryCodes",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "C");

            migrationBuilder.CreateIndex(
                name: "IX_CountryCodes_BaseCode_Code",
                table: "CountryCodes",
                columns: new[] { "BaseCode", "Code" },
                unique: true);
        }
    }
}
