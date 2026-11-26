using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilkyTeaChain.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteAndPreparationStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreparationStage",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "OrderItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreparationStage",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "OrderItems");
        }
    }
}
