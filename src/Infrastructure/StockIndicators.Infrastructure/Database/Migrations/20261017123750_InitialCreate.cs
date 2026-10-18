using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockIndicators.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "indicators");

            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Ticker = table.Column<string>(type: "varchar(64)", nullable: false),
                    ExchangeName = table.Column<string>(type: "varchar(10)", nullable: false, defaultValue: ""),
                    ClassCode = table.Column<string>(type: "varchar(20)", nullable: true),
                    Currency = table.Column<string>(type: "varchar(10)", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Isin = table.Column<string>(type: "varchar(12)", nullable: true),
                    Sector = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "varchar(10)", nullable: true),
                    Source = table.Column<string>(type: "varchar(20)", nullable: false),
                    TickerNorm = table.Column<string>(type: "varchar(64)", nullable: false),
                    NameNorm = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    NameTranslit = table.Column<string>(type: "text", nullable: true),
                    Aliases = table.Column<string[]>(type: "text[]", nullable: false, defaultValue: new string[0]),
                    AliasesTop = table.Column<string[]>(type: "text[]", nullable: false, defaultValue: new string[0]),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => new { x.Ticker, x.ExchangeName });
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "varchar(50)", nullable: false),
                    IsBundled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Template = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Credentials = table.Column<string>(type: "jsonb", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RulePresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "varchar(60)", nullable: false),
                    Indicator = table.Column<string>(type: "varchar(40)", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    NameEn = table.Column<string>(type: "varchar(100)", nullable: false),
                    NameRu = table.Column<string>(type: "varchar(100)", nullable: false),
                    NameEs = table.Column<string>(type: "varchar(100)", nullable: false),
                    NameIt = table.Column<string>(type: "varchar(100)", nullable: true),
                    NameDe = table.Column<string>(type: "varchar(100)", nullable: true),
                    NameFr = table.Column<string>(type: "varchar(100)", nullable: true),
                    NamePtBr = table.Column<string>(type: "varchar(100)", nullable: true),
                    NameUk = table.Column<string>(type: "varchar(100)", nullable: true),
                    NameBe = table.Column<string>(type: "varchar(100)", nullable: true),
                    DescriptionEn = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionRu = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionEs = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionIt = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionDe = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionFr = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionPtBr = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionUk = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionBe = table.Column<string>(type: "varchar(300)", nullable: true),
                    Family = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelEn = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelRu = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelEs = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelIt = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelDe = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelFr = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelPtBr = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelUk = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelBe = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentEn = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentRu = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentEs = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentIt = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentDe = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentFr = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentPtBr = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentUk = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentBe = table.Column<string>(type: "varchar(40)", nullable: true),
                    RuleData = table.Column<string>(type: "jsonb", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RulePresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrailingStopAnchors",
                schema: "indicators",
                columns: table => new
                {
                    ConditionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Anchor = table.Column<double>(type: "double precision", nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrailingStopAnchors", x => x.ConditionId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "varchar(255)", nullable: true),
                    DisplayName = table.Column<string>(type: "varchar(255)", nullable: true),
                    Language = table.Column<string>(type: "varchar(10)", nullable: true),
                    Timezone = table.Column<string>(type: "varchar(64)", nullable: true),
                    SubscriptionsSort = table.Column<string>(type: "varchar(32)", nullable: true),
                    ChartPreferences = table.Column<string>(type: "jsonb", nullable: true),
                    InitOnboardingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubscriptionOnboardingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "varchar(10)", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    PublicKey = table.Column<string>(type: "text", nullable: true),
                    AuthKey = table.Column<string>(type: "text", nullable: true),
                    DeviceLabel = table.Column<string>(type: "varchar(100)", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", nullable: false),
                    RuleData = table.Column<string>(type: "jsonb", nullable: false),
                    ConditionCount = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rules_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TelegramChats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "varchar(64)", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramChats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelegramChats_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuleIndicators",
                columns: table => new
                {
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ticker = table.Column<string>(type: "varchar(20)", nullable: false),
                    ExchangeName = table.Column<string>(type: "varchar(10)", nullable: false, defaultValue: ""),
                    Indicator = table.Column<string>(type: "varchar(30)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleIndicators", x => new { x.RuleId, x.Ticker, x.ExchangeName, x.Indicator });
                    table.ForeignKey(
                        name: "FK_RuleIndicators_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuleInstruments",
                columns: table => new
                {
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ticker = table.Column<string>(type: "varchar(20)", nullable: false),
                    ExchangeName = table.Column<string>(type: "varchar(10)", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleInstruments", x => new { x.RuleId, x.Ticker, x.ExchangeName });
                    table.ForeignKey(
                        name: "FK_RuleInstruments_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    Cooldown = table.Column<TimeSpan>(type: "interval", nullable: false),
                    IgnoreExtendedSessions = table.Column<bool>(type: "boolean", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RuleGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Rules_RuleGroupId",
                        column: x => x.RuleGroupId,
                        principalTable: "Rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionOutputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "varchar(20)", nullable: false),
                    NotificationTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    PushSubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChatId = table.Column<long>(type: "bigint", nullable: true),
                    Target = table.Column<string>(type: "varchar(2048)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionOutputs_NotificationTemplates_NotificationTempl~",
                        column: x => x.NotificationTemplateId,
                        principalTable: "NotificationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubscriptionOutputs_PushSubscriptions_PushSubscriptionId",
                        column: x => x.PushSubscriptionId,
                        principalTable: "PushSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubscriptionOutputs_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TriggerHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerValue = table.Column<string>(type: "text", nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggeredCandleAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TriggeredConditionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriggerHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriggerHistory_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TriggerSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ticker = table.Column<string>(type: "varchar(20)", nullable: false),
                    ExchangeName = table.Column<string>(type: "varchar(10)", nullable: false, defaultValue: ""),
                    TimeFrame = table.Column<byte>(type: "smallint", nullable: false),
                    IndicatorNames = table.Column<string>(type: "text", nullable: false),
                    LastCandleAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenderedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriggerSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriggerSnapshots_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TriggerDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerHistoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "varchar(20)", nullable: false),
                    Status = table.Column<string>(type: "varchar(10)", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriggerDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriggerDeliveries_TriggerHistory_TriggerHistoryId",
                        column: x => x.TriggerHistoryId,
                        principalTable: "TriggerHistory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_ExchangeName",
                table: "Instruments",
                column: "ExchangeName");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_TickerNorm",
                table: "Instruments",
                column: "TickerNorm");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Type",
                table: "Instruments",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId_Endpoint",
                table: "PushSubscriptions",
                columns: new[] { "UserId", "Endpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleIndicators_Indicator",
                table: "RuleIndicators",
                column: "Indicator");

            migrationBuilder.CreateIndex(
                name: "IX_RuleInstruments_Ticker_ExchangeName",
                table: "RuleInstruments",
                columns: new[] { "Ticker", "ExchangeName" });

            migrationBuilder.CreateIndex(
                name: "IX_RulePresets_Key",
                table: "RulePresets",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RulePresets_Order",
                table: "RulePresets",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_UserId",
                table: "Rules",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionOutputs_NotificationTemplateId",
                table: "SubscriptionOutputs",
                column: "NotificationTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionOutputs_PushSubscriptionId",
                table: "SubscriptionOutputs",
                column: "PushSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionOutputs_SubscriptionId",
                table: "SubscriptionOutputs",
                column: "SubscriptionId",
                unique: true,
                filter: "\"Channel\" = 'Telegram'");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_RuleGroupId",
                table: "Subscriptions",
                column: "RuleGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramChats_ChatId",
                table: "TelegramChats",
                column: "ChatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramChats_UserId",
                table: "TelegramChats",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriggerDeliveries_TriggerHistoryId_Channel",
                table: "TriggerDeliveries",
                columns: new[] { "TriggerHistoryId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_TriggerHistory_SubscriptionId_TriggeredAt",
                table: "TriggerHistory",
                columns: new[] { "SubscriptionId", "TriggeredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TriggerSnapshots_SubscriptionId_Ticker_ExchangeName_TimeFra~",
                table: "TriggerSnapshots",
                columns: new[] { "SubscriptionId", "Ticker", "ExchangeName", "TimeFrame" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "\"Email\" IS NOT NULL");

            // Out-of-model schema for the instrument search: the trigram extension and its
            // GIN indexes back the % similarity fallback (InstrumentSearchParameters), and
            // the UNLOGGED staging table is the COPY target of the daily instruments
            // mirror (InstrumentsTableSync). Neither is an EF entity, so both live as raw SQL.
            migrationBuilder.Sql(
                """
                CREATE EXTENSION IF NOT EXISTS pg_trgm;
                """);

            // array_to_string is only STABLE, and the generated column below needs an
            // IMMUTABLE expression; this trivial wrapper is the standard workaround
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION immutable_array_to_string(text[], text)
                    RETURNS text
                    LANGUAGE sql IMMUTABLE PARALLEL SAFE
                    RETURN COALESCE(array_to_string($1, $2), '');
                """);

            // Full-text column over the normalized text (not mapped in EF - a generated
            // column): ticker lexemes weigh above name/transliterated-name lexemes, which
            // weigh above the canonical crypto asset names, which weigh above the clone
            // names. 'simple' config: no stemming, the search side queries prefixes.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Instruments"
                    ADD COLUMN "Search" tsvector
                    GENERATED ALWAYS AS (
                        setweight(to_tsvector('simple', "TickerNorm"), 'A') ||
                        setweight(to_tsvector('simple', "NameNorm" || ' ' || COALESCE("NameTranslit", '')), 'B') ||
                        setweight(to_tsvector('simple', immutable_array_to_string("AliasesTop", ' ')), 'B') ||
                        setweight(to_tsvector('simple', immutable_array_to_string("Aliases", ' ')), 'C')
                    ) STORED;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_Instruments_Search" ON "Instruments" USING GIN ("Search");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_Instruments_NameNorm_trgm" ON "Instruments" USING GIN ("NameNorm" gin_trgm_ops);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_Instruments_TickerNorm_trgm" ON "Instruments" USING GIN ("TickerNorm" gin_trgm_ops);
                """);

            // UNLOGGED staging for the daily warm-up mirror (COPY + merge upsert + prune);
            // the PK also keeps a single load free of duplicate (ticker, exchange) rows
            migrationBuilder.Sql(
                """
                CREATE UNLOGGED TABLE "InstrumentSyncStaging" (
                    "Ticker" varchar(64) NOT NULL,
                    "ExchangeName" varchar(10) NOT NULL DEFAULT '',
                    "ClassCode" varchar(20),
                    "Currency" varchar(10),
                    "Name" text,
                    "Isin" varchar(12),
                    "Sector" text,
                    "Type" varchar(10),
                    "Source" varchar(20) NOT NULL,
                    "TickerNorm" varchar(64) NOT NULL,
                    "NameNorm" text NOT NULL DEFAULT '',
                    "NameTranslit" text,
                    "Aliases" text[] NOT NULL DEFAULT '{}',
                    "AliasesTop" text[] NOT NULL DEFAULT '{}',
                    PRIMARY KEY ("Ticker", "ExchangeName")
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // the trgm indexes go with the Instruments table; the extension stays (Postgres
            // has no dependency to track, and dropping it could hit other databases' objects)
            migrationBuilder.Sql(
                """
                DROP TABLE IF EXISTS "InstrumentSyncStaging";
                """);

            migrationBuilder.DropTable(
                name: "Instruments");

            migrationBuilder.DropTable(
                name: "ProviderSettings");

            migrationBuilder.DropTable(
                name: "RuleIndicators");

            migrationBuilder.DropTable(
                name: "RuleInstruments");

            migrationBuilder.DropTable(
                name: "RulePresets");

            migrationBuilder.DropTable(
                name: "SubscriptionOutputs");

            migrationBuilder.DropTable(
                name: "TelegramChats");

            migrationBuilder.DropTable(
                name: "TrailingStopAnchors",
                schema: "indicators");

            migrationBuilder.DropTable(
                name: "TriggerDeliveries");

            migrationBuilder.DropTable(
                name: "TriggerSnapshots");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropTable(
                name: "TriggerHistory");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Rules");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
