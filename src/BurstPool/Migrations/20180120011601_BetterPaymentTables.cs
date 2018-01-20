using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace BurstPool.Migrations
{
    public partial class BetterPaymentTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PaymentsCalculated",
                table: "BlockStates",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SharesAveraged",
                table: "BlockStates",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AccountAverageShareHistory",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    AccountId = table.Column<ulong>(nullable: false),
                    AverageShares = table.Column<decimal>(nullable: false),
                    Created = table.Column<DateTimeOffset>(nullable: false),
                    Height = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountAverageShareHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountAverageShareHistory_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountAverageShareHistory_Blocks_Height",
                        column: x => x.Height,
                        principalTable: "Blocks",
                        principalColumn: "Height",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountAverageShareHistory_AccountId",
                table: "AccountAverageShareHistory",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountAverageShareHistory_Height",
                table: "AccountAverageShareHistory",
                column: "Height");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountAverageShareHistory");

            migrationBuilder.DropColumn(
                name: "PaymentsCalculated",
                table: "BlockStates");

            migrationBuilder.DropColumn(
                name: "SharesAveraged",
                table: "BlockStates");
        }
    }
}
