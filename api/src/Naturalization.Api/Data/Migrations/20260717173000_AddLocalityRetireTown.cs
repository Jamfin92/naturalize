using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Naturalization.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalityRetireTown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retire the TownCode lookup and the applicant's free-text town/zip:
            // residence now lives in the Localities table, referenced by FK.
            migrationBuilder.DropTable(
                name: "TownCodes");

            migrationBuilder.DropColumn(
                name: "TownCode",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "Applicants");

            migrationBuilder.CreateTable(
                name: "Localities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZipCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Localities", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "LocalityId",
                table: "Applicants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_LocalityId",
                table: "Applicants",
                column: "LocalityId");

            migrationBuilder.CreateIndex(
                name: "IX_Localities_Name",
                table: "Localities",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Localities_ZipCode",
                table: "Localities",
                column: "ZipCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Applicants_Localities_LocalityId",
                table: "Applicants",
                column: "LocalityId",
                principalTable: "Localities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applicants_Localities_LocalityId",
                table: "Applicants");

            migrationBuilder.DropTable(
                name: "Localities");

            migrationBuilder.DropIndex(
                name: "IX_Applicants_LocalityId",
                table: "Applicants");

            migrationBuilder.DropColumn(
                name: "LocalityId",
                table: "Applicants");

            migrationBuilder.AddColumn<string>(
                name: "TownCode",
                table: "Applicants",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "Applicants",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TownCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TownCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TownCodes_BaseCode_Code",
                table: "TownCodes",
                columns: new[] { "BaseCode", "Code" },
                unique: true);
        }
    }
}
