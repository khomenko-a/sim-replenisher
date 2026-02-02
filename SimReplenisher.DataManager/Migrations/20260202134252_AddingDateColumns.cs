using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimReplenisher.DataManager.Migrations
{
    /// <inheritdoc />
    public partial class AddingDateColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AddingDate",
                table: "ReplenishmentRequests",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutionDate",
                table: "ReplenishmentRequests",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddingDate",
                table: "ReplenishmentRequests");

            migrationBuilder.DropColumn(
                name: "ExecutionDate",
                table: "ReplenishmentRequests");
        }
    }
}
