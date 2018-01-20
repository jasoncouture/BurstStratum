using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace BurstPool.Migrations
{
    public partial class AddedRewardTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PoolTransactions",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    Adjustment = table.Column<decimal>(nullable: false),
                    Created = table.Column<DateTimeOffset>(nullable: false),
                    Height = table.Column<long>(nullable: false),
                    PoolBalance = table.Column<decimal>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoolTransactions_Blocks_Height",
                        column: x => x.Height,
                        principalTable: "Blocks",
                        principalColumn: "Height",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlockStates",
                columns: table => new
                {
                    Height = table.Column<long>(nullable: false),
                    Created = table.Column<DateTimeOffset>(nullable: false),
                    IsPoolMember = table.Column<bool>(nullable: false),
                    PoolTransactionId = table.Column<string>(nullable: true),
                    Winner = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockStates", x => x.Height);
                    table.ForeignKey(
                        name: "FK_BlockStates_Blocks_Height",
                        column: x => x.Height,
                        principalTable: "Blocks",
                        principalColumn: "Height",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlockStates_PoolTransactions_PoolTransactionId",
                        column: x => x.PoolTransactionId,
                        principalTable: "PoolTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockStates_Created",
                table: "BlockStates",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_BlockStates_PoolTransactionId",
                table: "BlockStates",
                column: "PoolTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PoolTransactions_Created",
                table: "PoolTransactions",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_PoolTransactions_Height",
                table: "PoolTransactions",
                column: "Height");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockStates");

            migrationBuilder.DropTable(
                name: "PoolTransactions");
        }
    }
}
