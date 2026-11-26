using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilkyTeaChain.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Branches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Branches");
        }
    }
}
