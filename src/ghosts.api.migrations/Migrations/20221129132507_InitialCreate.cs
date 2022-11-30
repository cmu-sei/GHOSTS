using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ghosts.api.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "machine_timelines",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    machineid = table.Column<Guid>(type: "uuid", nullable: false),
                    timeline = table.Column<string>(type: "jsonb", nullable: true),
                    createdutc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_machine_timelines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "machine_updates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    machineid = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    activeutc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    createdutc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    update = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_machine_updates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "machines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true),
                    fqdn = table.Column<string>(type: "text", nullable: true),
                    domain = table.Column<string>(type: "text", nullable: true),
                    host = table.Column<string>(type: "text", nullable: true),
                    resolvedhost = table.Column<string>(type: "text", nullable: true),
                    hostip = table.Column<string>(type: "text", nullable: true),
                    ipaddress = table.Column<string>(type: "text", nullable: true),
                    currentusername = table.Column<string>(type: "text", nullable: true),
                    clientversion = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    createdutc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    statusup = table.Column<int>(type: "integer", nullable: false),
                    lastreportedutc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_machines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "surveys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    machineid = table.Column<Guid>(type: "uuid", nullable: false),
                    created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    uptime = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_surveys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trackables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    machineid = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trackables", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhooks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    postbackurl = table.Column<string>(type: "text", nullable: true),
                    postbackmethod = table.Column<int>(type: "integer", nullable: false),
                    postbackformat = table.Column<string>(type: "text", nullable: true),
                    createdutc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    applicationuserid = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhooks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_machines",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    groupid = table.Column<int>(type: "integer", nullable: false),
                    machineid = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_machines", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_machines_groups_groupid",
                        column: x => x.groupid,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "history_health",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    machineid = table.Column<Guid>(type: "uuid", nullable: false),
                    createdutc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    internet = table.Column<bool>(type: "boolean", nullable: true),
                    permissions = table.Column<bool>(type: "boolean", nullable: true),
                    executiontime = table.Column<long>(type: "bigint", nullable: false),
                    errors = table.Column<string>(type: "text", nullable: true),
                    loggedonusers = table.Column<string>(type: "text", nullable: true),
                    stats = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_history_health", x => x.id);
                    table.ForeignKey(
                        name: "fk_history_health_machines_machineid",
                        column: x => x.machineid,
                        principalTable: "machines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "history_machine",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    createdutc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    @object = table.Column<string>(name: "object", type: "text", nullable: true),
                    machineid = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_history_machine", x => x.id);
                    table.ForeignKey(
                        name: "fk_history_machine_machines_machineid",
                        column: x => x.machineid,
                        principalTable: "machines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "history_timeline",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    machineid = table.Column<Guid>(type: "uuid", nullable: false),
                    createdutc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    handler = table.Column<string>(type: "text", nullable: true),
                    command = table.Column<string>(type: "text", nullable: true),
                    commandarg = table.Column<string>(type: "text", nullable: true),
                    result = table.Column<string>(type: "text", nullable: true),
                    tags = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_history_timeline", x => x.id);
                    table.ForeignKey(
                        name: "fk_history_timeline_machines_machineid",
                        column: x => x.machineid,
                        principalTable: "machines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "history_trackables",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    machineid = table.Column<Guid>(type: "uuid", nullable: false),
                    trackableid = table.Column<Guid>(type: "uuid", nullable: false),
                    createdutc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    handler = table.Column<string>(type: "text", nullable: true),
                    command = table.Column<string>(type: "text", nullable: true),
                    commandarg = table.Column<string>(type: "text", nullable: true),
                    result = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_history_trackables", x => x.id);
                    table.ForeignKey(
                        name: "fk_history_trackables_machines_machineid",
                        column: x => x.machineid,
                        principalTable: "machines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_drives",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    surveyid = table.Column<int>(type: "integer", nullable: false),
                    availablefreespace = table.Column<long>(type: "bigint", nullable: false),
                    driveformat = table.Column<string>(type: "text", nullable: true),
                    drivetype = table.Column<string>(type: "text", nullable: true),
                    isready = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true),
                    rootdirectory = table.Column<string>(type: "text", nullable: true),
                    totalfreespace = table.Column<long>(type: "bigint", nullable: false),
                    totalsize = table.Column<long>(type: "bigint", nullable: false),
                    volumelabel = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_drives", x => x.id);
                    table.ForeignKey(
                        name: "fk_survey_drives_surveys_surveyid",
                        column: x => x.surveyid,
                        principalTable: "surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_event_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    surveyid = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_event_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_survey_event_logs_surveys_surveyid",
                        column: x => x.surveyid,
                        principalTable: "surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_interfaces",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    surveyid = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_interfaces", x => x.id);
                    table.ForeignKey(
                        name: "fk_survey_interfaces_surveys_surveyid",
                        column: x => x.surveyid,
                        principalTable: "surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_local_processes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    surveyid = table.Column<int>(type: "integer", nullable: false),
                    privatememorysize64 = table.Column<long>(type: "bigint", nullable: false),
                    mainwindowtitle = table.Column<string>(type: "text", nullable: true),
                    processname = table.Column<string>(type: "text", nullable: true),
                    starttime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    filename = table.Column<string>(type: "text", nullable: true),
                    owner = table.Column<string>(type: "text", nullable: true),
                    ownerdomain = table.Column<string>(type: "text", nullable: true),
                    ownersid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_local_processes", x => x.id);
                    table.ForeignKey(
                        name: "fk_survey_local_processes_surveys_surveyid",
                        column: x => x.surveyid,
                        principalTable: "surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_ports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    surveyid = table.Column<int>(type: "integer", nullable: false),
                    localport = table.Column<string>(type: "text", nullable: true),
                    localaddress = table.Column<string>(type: "text", nullable: true),
                    foreignaddress = table.Column<string>(type: "text", nullable: true),
                    foreignport = table.Column<string>(type: "text", nullable: true),
                    pid = table.Column<int>(type: "integer", nullable: false),
                    process = table.Column<string>(type: "text", nullable: true),
                    protocol = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_ports", x => x.id);
                    table.ForeignKey(
                        name: "fk_survey_ports_surveys_surveyid",
                        column: x => x.surveyid,
                        principalTable: "surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    surveyid = table.Column<int>(type: "integer", nullable: false),
                    username = table.Column<string>(type: "text", nullable: true),
                    domain = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_survey_users_surveys_surveyid",
                        column: x => x.surveyid,
                        principalTable: "surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_event_log_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eventlogid = table.Column<int>(type: "integer", nullable: false),
                    created = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    entrytype = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_event_log_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_survey_event_log_entries_survey_event_logs_eventlogid",
                        column: x => x.eventlogid,
                        principalTable: "survey_event_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_interface_bindings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    interfaceid = table.Column<int>(type: "integer", nullable: false),
                    internetaddress = table.Column<string>(type: "text", nullable: true),
                    physicaladdress = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_interface_bindings", x => x.id);
                    table.ForeignKey(
                        name: "fk_survey_interface_bindings_survey_interfaces_interfaceid",
                        column: x => x.interfaceid,
                        principalTable: "survey_interfaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_group_machines_groupid",
                table: "group_machines",
                column: "groupid");

            migrationBuilder.CreateIndex(
                name: "ix_group_machines_machineid",
                table: "group_machines",
                column: "machineid");

            migrationBuilder.CreateIndex(
                name: "ix_groups_name",
                table: "groups",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_history_health_createdutc",
                table: "history_health",
                column: "createdutc");

            migrationBuilder.CreateIndex(
                name: "ix_history_health_machineid",
                table: "history_health",
                column: "machineid");

            migrationBuilder.CreateIndex(
                name: "ix_history_machine_createdutc",
                table: "history_machine",
                column: "createdutc");

            migrationBuilder.CreateIndex(
                name: "ix_history_machine_machineid",
                table: "history_machine",
                column: "machineid");

            migrationBuilder.CreateIndex(
                name: "ix_history_timeline_createdutc",
                table: "history_timeline",
                column: "createdutc");

            migrationBuilder.CreateIndex(
                name: "ix_history_timeline_machineid",
                table: "history_timeline",
                column: "machineid");

            migrationBuilder.CreateIndex(
                name: "ix_history_timeline_tags",
                table: "history_timeline",
                column: "tags");

            migrationBuilder.CreateIndex(
                name: "ix_history_trackables_machineid",
                table: "history_trackables",
                column: "machineid");

            migrationBuilder.CreateIndex(
                name: "ix_machine_updates_activeutc",
                table: "machine_updates",
                column: "activeutc");

            migrationBuilder.CreateIndex(
                name: "ix_machine_updates_createdutc",
                table: "machine_updates",
                column: "createdutc");

            migrationBuilder.CreateIndex(
                name: "ix_machine_updates_status",
                table: "machine_updates",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_machines_createdutc",
                table: "machines",
                column: "createdutc");

            migrationBuilder.CreateIndex(
                name: "ix_machines_lastreportedutc",
                table: "machines",
                column: "lastreportedutc");

            migrationBuilder.CreateIndex(
                name: "ix_machines_status",
                table: "machines",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_survey_drives_surveyid",
                table: "survey_drives",
                column: "surveyid");

            migrationBuilder.CreateIndex(
                name: "ix_survey_event_log_entries_eventlogid",
                table: "survey_event_log_entries",
                column: "eventlogid");

            migrationBuilder.CreateIndex(
                name: "ix_survey_event_logs_surveyid",
                table: "survey_event_logs",
                column: "surveyid");

            migrationBuilder.CreateIndex(
                name: "ix_survey_interface_bindings_interfaceid",
                table: "survey_interface_bindings",
                column: "interfaceid");

            migrationBuilder.CreateIndex(
                name: "ix_survey_interfaces_surveyid",
                table: "survey_interfaces",
                column: "surveyid");

            migrationBuilder.CreateIndex(
                name: "ix_survey_local_processes_surveyid",
                table: "survey_local_processes",
                column: "surveyid");

            migrationBuilder.CreateIndex(
                name: "ix_survey_ports_surveyid",
                table: "survey_ports",
                column: "surveyid");

            migrationBuilder.CreateIndex(
                name: "ix_survey_users_surveyid",
                table: "survey_users",
                column: "surveyid");

            migrationBuilder.CreateIndex(
                name: "ix_surveys_machineid",
                table: "surveys",
                column: "machineid");

            migrationBuilder.CreateIndex(
                name: "ix_webhooks_createdutc",
                table: "webhooks",
                column: "createdutc");

            migrationBuilder.CreateIndex(
                name: "ix_webhooks_status",
                table: "webhooks",
                column: "status");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_machines");

            migrationBuilder.DropTable(
                name: "history_health");

            migrationBuilder.DropTable(
                name: "history_machine");

            migrationBuilder.DropTable(
                name: "history_timeline");

            migrationBuilder.DropTable(
                name: "history_trackables");

            migrationBuilder.DropTable(
                name: "machine_timelines");

            migrationBuilder.DropTable(
                name: "machine_updates");

            migrationBuilder.DropTable(
                name: "survey_drives");

            migrationBuilder.DropTable(
                name: "survey_event_log_entries");

            migrationBuilder.DropTable(
                name: "survey_interface_bindings");

            migrationBuilder.DropTable(
                name: "survey_local_processes");

            migrationBuilder.DropTable(
                name: "survey_ports");

            migrationBuilder.DropTable(
                name: "survey_users");

            migrationBuilder.DropTable(
                name: "trackables");

            migrationBuilder.DropTable(
                name: "webhooks");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "machines");

            migrationBuilder.DropTable(
                name: "survey_event_logs");

            migrationBuilder.DropTable(
                name: "survey_interfaces");

            migrationBuilder.DropTable(
                name: "surveys");
        }
    }
}
