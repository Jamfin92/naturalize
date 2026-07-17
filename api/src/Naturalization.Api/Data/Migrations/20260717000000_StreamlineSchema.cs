using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Naturalization.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StreamlineSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applicants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlienNumber = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    NaturalizationNumber = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    PetitionNumber = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AdmissionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Address1 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TownCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    ZipCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DecisionDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DecisionNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applicants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FieldOffice = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CountryCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseCode = table.Column<string>(type: "TEXT", maxLength: 1, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TownCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseCode = table.Column<string>(type: "TEXT", maxLength: 1, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TownCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_AlienNumber",
                table: "Applicants",
                column: "AlienNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_IsDeleted",
                table: "Applicants",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_Status",
                table: "Applicants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_Email",
                table: "ApplicationUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityType_EntityId",
                table: "AuditEvents",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt",
                table: "AuditEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_CountryCodes_BaseCode_Code",
                table: "CountryCodes",
                columns: new[] { "BaseCode", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TownCodes_BaseCode_Code",
                table: "TownCodes",
                columns: new[] { "BaseCode", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Applicants");

            migrationBuilder.DropTable(
                name: "ApplicationUsers");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "CountryCodes");

            migrationBuilder.DropTable(
                name: "TownCodes");
        }
    }
}
