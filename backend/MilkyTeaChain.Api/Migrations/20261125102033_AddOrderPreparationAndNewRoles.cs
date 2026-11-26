using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MilkyTeaChain.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPreparationAndNewRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrepared",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreparedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPrepared",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PreparedAt",
                table: "Orders");
        }
    }
}
