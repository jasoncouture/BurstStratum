﻿// <auto-generated />
using BurstPool.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;

namespace BurstPool.Migrations
{
    [DbContext(typeof(PoolContext))]
    [Migration("20180110025814_AddedEarnings")]
    partial class AddedEarnings
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .HasAnnotation("ProductVersion", "2.0.1-rtm-125");

            modelBuilder.Entity("BurstPool.Database.Models.Account", b =>
                {
                    b.Property<ulong>("Id");

                    b.HasKey("Id");

                    b.ToTable("Accounts");
                });

            modelBuilder.Entity("BurstPool.Database.Models.Block", b =>
                {
                    b.Property<long>("Height");

                    b.Property<ulong>("BaseTarget");

                    b.Property<decimal>("Difficulty");

                    b.HasKey("Height");

                    b.ToTable("Blocks");
                });

            modelBuilder.Entity("BurstPool.Database.Models.Earnings", b =>
                {
                    b.Property<long>("Height");

                    b.Property<decimal>("Amount");

                    b.HasKey("Height");

                    b.ToTable("Earnings");
                });

            modelBuilder.Entity("BurstPool.Database.Models.Share", b =>
                {
                    b.Property<string>("Id");

                    b.Property<ulong>("AccountId");

                    b.Property<long>("BlockId");

                    b.Property<DateTimeOffset>("Created");

                    b.Property<ulong>("Deadline");

                    b.Property<ulong>("Nonce");

                    b.Property<decimal>("ShareValue");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.HasIndex("BlockId");

                    b.ToTable("Shares");
                });

            modelBuilder.Entity("BurstPool.Database.Models.Share", b =>
                {
                    b.HasOne("BurstPool.Database.Models.Account", "Account")
                        .WithMany("Shares")
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("BurstPool.Database.Models.Block", "Block")
                        .WithMany()
                        .HasForeignKey("BlockId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
