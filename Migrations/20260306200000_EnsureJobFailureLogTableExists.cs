using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using crm_api.Data;

#nullable disable

namespace crm_api.Migrations
{
    [DbContext(typeof(CmsDbContext))]
    [Migration("20260306200000_EnsureJobFailureLogTableExists")]
    public partial class EnsureJobFailureLogTableExists : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RII_JOB_FAILURE_LOG]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[RII_JOB_FAILURE_LOG](
                        [Id] BIGINT IDENTITY(1,1) NOT NULL,
                        [JobId] NVARCHAR(100) NOT NULL,
                        [JobName] NVARCHAR(500) NOT NULL,
                        [FailedAt] DATETIME2 NOT NULL,
                        [Reason] NVARCHAR(2000) NULL,
                        [ExceptionType] NVARCHAR(500) NULL,
                        [ExceptionMessage] NVARCHAR(4000) NULL,
                        [StackTrace] NVARCHAR(4000) NULL,
                        [Queue] NVARCHAR(100) NULL,
                        [RetryCount] INT NOT NULL,
                        [CreatedDate] DATETIME2 NOT NULL CONSTRAINT [DF_RII_JOB_FAILURE_LOG_CreatedDate] DEFAULT (GETUTCDATE()),
                        [UpdatedDate] DATETIME2 NULL,
                        [DeletedDate] DATETIME2 NULL,
                        [IsDeleted] BIT NOT NULL,
                        [CreatedBy] BIGINT NULL,
                        [UpdatedBy] BIGINT NULL,
                        [DeletedBy] BIGINT NULL,
                        CONSTRAINT [PK_RII_JOB_FAILURE_LOG] PRIMARY KEY CLUSTERED ([Id] ASC)
                    );
                END
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RII_JOB_FAILURE_LOG]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_JobFailureLog_JobId' AND object_id = OBJECT_ID(N'[dbo].[RII_JOB_FAILURE_LOG]'))
                BEGIN
                    CREATE INDEX [IX_JobFailureLog_JobId] ON [dbo].[RII_JOB_FAILURE_LOG]([JobId]);
                END
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RII_JOB_FAILURE_LOG]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_JobFailureLog_FailedAt' AND object_id = OBJECT_ID(N'[dbo].[RII_JOB_FAILURE_LOG]'))
                BEGIN
                    CREATE INDEX [IX_JobFailureLog_FailedAt] ON [dbo].[RII_JOB_FAILURE_LOG]([FailedAt]);
                END
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RII_JOB_FAILURE_LOG]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_JobFailureLog_JobName' AND object_id = OBJECT_ID(N'[dbo].[RII_JOB_FAILURE_LOG]'))
                BEGIN
                    CREATE INDEX [IX_JobFailureLog_JobName] ON [dbo].[RII_JOB_FAILURE_LOG]([JobName]);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RII_JOB_FAILURE_LOG]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [dbo].[RII_JOB_FAILURE_LOG];
                END
                """);
        }
    }
}
