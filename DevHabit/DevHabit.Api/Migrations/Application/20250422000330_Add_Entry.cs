using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHabit.Api.Migrations.Application;

    /// <inheritdoc />
    public partial class Add_Entry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entries",
                schema: "dev_habit",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    habit_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    user_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    external_id = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_entries_habits_habit_id",
                        column: x => x.habit_id,
                        principalSchema: "dev_habit",
                        principalTable: "habits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_entries_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dev_habit",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entry_import_jobs",
                schema: "dev_habit",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    user_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_content = table.Column<byte[]>(type: "bytea", nullable: false),
                    total_records = table.Column<int>(type: "integer", nullable: false),
                    processed_records = table.Column<int>(type: "integer", nullable: false),
                    successful_records = table.Column<int>(type: "integer", nullable: false),
                    failed_records = table.Column<int>(type: "integer", nullable: false),
                    errors = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entry_import_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_entry_import_jobs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dev_habit",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entries_external_id",
                schema: "dev_habit",
                table: "entries",
                column: "external_id",
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_entries_habit_id",
                schema: "dev_habit",
                table: "entries",
                column: "habit_id");

            migrationBuilder.CreateIndex(
                name: "ix_entries_user_id",
                schema: "dev_habit",
                table: "entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_entry_import_jobs_user_id",
                schema: "dev_habit",
                table: "entry_import_jobs",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entries",
                schema: "dev_habit");

            migrationBuilder.DropTable(
                name: "entry_import_jobs",
                schema: "dev_habit");
        }
    }
