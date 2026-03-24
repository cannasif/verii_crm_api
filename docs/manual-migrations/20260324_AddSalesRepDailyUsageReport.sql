/*
  Sales rep daily usage report
  - Counts business-card scans from RII_CUSTOMER_IMAGE
  - Counts created customers from RII_CUSTOMER
  - Counts created contacts from RII_CONTACT
  - Counts created activities from RII_ACTIVITY
  - Creates / updates a Report Builder report definition
  - Assigns the report to a target user in RII_REPORT_ASSIGNMENTS

  Preconditions:
  1. RII_REPORT_ASSIGNMENTS migration must already be applied.
  2. Creator and target viewer users must exist in RII_USERS.
*/

SET NOCOUNT ON;

DECLARE @CreatorEmail NVARCHAR(256) = N'admin@v3rii.com';
DECLARE @ViewerEmail NVARCHAR(256) = N'can.nasif@v3rii.com';
DECLARE @ReportName NVARCHAR(200) = N'Plasiyer Günlük Kullanım Raporu';
DECLARE @ReportDescription NVARCHAR(500) = N'Gün bazında hangi personelin kaç kart okuttuğunu ve kaç aktivite eklediğini gösterir.';
DECLARE @DataSourceName NVARCHAR(128) = N'dbo.RII_FN_SALESMAN_DAILY_USAGE';

DECLARE @CreatorUserId BIGINT;
DECLARE @ViewerUserId BIGINT;
DECLARE @ReportId BIGINT;

SELECT @CreatorUserId = Id
FROM RII_USERS
WHERE IsDeleted = 0
  AND Email = @CreatorEmail;

SELECT @ViewerUserId = Id
FROM RII_USERS
WHERE IsDeleted = 0
  AND Email = @ViewerEmail;

IF @CreatorUserId IS NULL
BEGIN
    THROW 50001, 'Creator user was not found in RII_USERS.', 1;
END;

IF @ViewerUserId IS NULL
BEGIN
    THROW 50002, 'Viewer user was not found in RII_USERS.', 1;
END;

EXEC('
CREATE OR ALTER FUNCTION dbo.RII_FN_SALESMAN_DAILY_USAGE
(
    @StartDate DATE = NULL,
    @EndDate DATE = NULL
)
RETURNS TABLE
AS
RETURN
WITH CardScans AS
(
    SELECT
        CAST(ci.CreatedDate AS DATE) AS UsageDate,
        ci.CreatedBy AS UserId,
        COUNT(*) AS CardScanCount
    FROM RII_CUSTOMER_IMAGE ci
    WHERE ci.IsDeleted = 0
      AND ci.CreatedBy IS NOT NULL
      AND (@StartDate IS NULL OR CAST(ci.CreatedDate AS DATE) >= @StartDate)
      AND (@EndDate IS NULL OR CAST(ci.CreatedDate AS DATE) <= @EndDate)
    GROUP BY CAST(ci.CreatedDate AS DATE), ci.CreatedBy
),
CustomersCreated AS
(
    SELECT
        CAST(c.CreatedDate AS DATE) AS UsageDate,
        c.CreatedBy AS UserId,
        COUNT(*) AS CreatedCustomerCount
    FROM RII_CUSTOMER c
    WHERE c.IsDeleted = 0
      AND c.CreatedBy IS NOT NULL
      AND (@StartDate IS NULL OR CAST(c.CreatedDate AS DATE) >= @StartDate)
      AND (@EndDate IS NULL OR CAST(c.CreatedDate AS DATE) <= @EndDate)
    GROUP BY CAST(c.CreatedDate AS DATE), c.CreatedBy
),
ContactsCreated AS
(
    SELECT
        CAST(c.CreatedDate AS DATE) AS UsageDate,
        c.CreatedBy AS UserId,
        COUNT(*) AS CreatedContactCount
    FROM RII_CONTACT c
    WHERE c.IsDeleted = 0
      AND c.CreatedBy IS NOT NULL
      AND (@StartDate IS NULL OR CAST(c.CreatedDate AS DATE) >= @StartDate)
      AND (@EndDate IS NULL OR CAST(c.CreatedDate AS DATE) <= @EndDate)
    GROUP BY CAST(c.CreatedDate AS DATE), c.CreatedBy
),
ActivitiesCreated AS
(
    SELECT
        CAST(a.CreatedDate AS DATE) AS UsageDate,
        a.CreatedBy AS UserId,
        COUNT(*) AS ActivityCreatedCount
    FROM RII_ACTIVITY a
    WHERE a.IsDeleted = 0
      AND a.CreatedBy IS NOT NULL
      AND (@StartDate IS NULL OR CAST(a.CreatedDate AS DATE) >= @StartDate)
      AND (@EndDate IS NULL OR CAST(a.CreatedDate AS DATE) <= @EndDate)
    GROUP BY CAST(a.CreatedDate AS DATE), a.CreatedBy
),
UsageKeys AS
(
    SELECT UsageDate, UserId FROM CardScans
    UNION
    SELECT UsageDate, UserId FROM CustomersCreated
    UNION
    SELECT UsageDate, UserId FROM ContactsCreated
    UNION
    SELECT UsageDate, UserId FROM ActivitiesCreated
)
SELECT
    uk.UsageDate,
    u.Id AS UserId,
    COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(ISNULL(u.FirstName, ''''), '' '', ISNULL(u.LastName, '''')))), ''''), u.Username, u.Email) AS UserFullName,
    ISNULL(cs.CardScanCount, 0) AS CardScanCount,
    ISNULL(cc.CreatedCustomerCount, 0) AS CreatedCustomerCount,
    ISNULL(ct.CreatedContactCount, 0) AS CreatedContactCount,
    ISNULL(ac.ActivityCreatedCount, 0) AS ActivityCreatedCount,
    ISNULL(cs.CardScanCount, 0) + ISNULL(ac.ActivityCreatedCount, 0) AS TotalActionCount
FROM UsageKeys uk
INNER JOIN RII_USERS u
    ON u.Id = uk.UserId
   AND u.IsDeleted = 0
LEFT JOIN CardScans cs
    ON cs.UsageDate = uk.UsageDate
   AND cs.UserId = uk.UserId
LEFT JOIN CustomersCreated cc
    ON cc.UsageDate = uk.UsageDate
   AND cc.UserId = uk.UserId
LEFT JOIN ContactsCreated ct
    ON ct.UsageDate = uk.UsageDate
   AND ct.UserId = uk.UserId
LEFT JOIN ActivitiesCreated ac
    ON ac.UsageDate = uk.UsageDate
   AND ac.UserId = uk.UserId
');

DECLARE @ConfigJson NVARCHAR(MAX) = N'{
  "chartType": "stackedBar",
  "axis": { "field": "UsageDate", "dateGrouping": "day" },
  "values": [
    { "field": "CardScanCount", "aggregation": "sum" },
    { "field": "ActivityCreatedCount", "aggregation": "sum" }
  ],
  "legend": { "field": "UserFullName" },
  "sorting": { "by": "axis", "direction": "asc" },
  "filters": [],
  "datasetParameters": [],
  "calculatedFields": [],
  "lifecycle": {
    "status": "published",
    "version": 1,
    "releaseNote": "Plasiyer günlük kullanım raporu"
  },
  "governance": {
    "category": "Satış Operasyon",
    "tags": ["plasiyer", "gunluk-kullanim", "kart", "aktivite"],
    "audience": "private",
    "refreshCadence": "daily",
    "favorite": false,
    "sharedWith": ["can.nasif@v3rii.com"],
    "owner": "Veri Admin",
    "certified": true
  },
  "widgets": [
    {
      "id": "salesrep-daily-usage-chart",
      "title": "Günlük plasiyer kullanım grafiği",
      "size": "full",
      "height": "md",
      "chartType": "stackedBar",
      "axis": { "field": "UsageDate", "dateGrouping": "day" },
      "values": [
        { "field": "CardScanCount", "aggregation": "sum" },
        { "field": "ActivityCreatedCount", "aggregation": "sum" }
      ],
      "legend": { "field": "UserFullName" },
      "filters": []
    },
    {
      "id": "salesrep-daily-usage-table",
      "title": "Detay tablo",
      "size": "full",
      "height": "lg",
      "chartType": "table",
      "axis": { "field": "UsageDate", "dateGrouping": "day" },
      "values": [
        { "field": "CardScanCount", "aggregation": "sum" },
        { "field": "CreatedCustomerCount", "aggregation": "sum" },
        { "field": "CreatedContactCount", "aggregation": "sum" },
        { "field": "ActivityCreatedCount", "aggregation": "sum" }
      ],
      "legend": { "field": "UserFullName" },
      "filters": []
    }
  ],
  "activeWidgetId": "salesrep-daily-usage-chart"
}';

SELECT @ReportId = Id
FROM RII_REPORT_DEFINITIONS
WHERE IsDeleted = 0
  AND Name = @ReportName
  AND ConnectionKey = N'CRM'
  AND DataSourceType = N'function'
  AND DataSourceName = @DataSourceName;

IF @ReportId IS NULL
BEGIN
    INSERT INTO RII_REPORT_DEFINITIONS
    (
        Name,
        Description,
        ConnectionKey,
        DataSourceType,
        DataSourceName,
        ConfigJson,
        CreatedDate,
        UpdatedDate,
        DeletedDate,
        IsDeleted,
        CreatedBy,
        UpdatedBy,
        DeletedBy
    )
    VALUES
    (
        @ReportName,
        @ReportDescription,
        N'CRM',
        N'function',
        @DataSourceName,
        @ConfigJson,
        GETDATE(),
        NULL,
        NULL,
        0,
        @CreatorUserId,
        NULL,
        NULL
    );

    SET @ReportId = SCOPE_IDENTITY();
END
ELSE
BEGIN
    UPDATE RII_REPORT_DEFINITIONS
    SET
        Description = @ReportDescription,
        ConfigJson = @ConfigJson,
        UpdatedDate = GETDATE(),
        UpdatedBy = @CreatorUserId
    WHERE Id = @ReportId;
END;

UPDATE RII_REPORT_ASSIGNMENTS
SET
    IsDeleted = 1,
    DeletedDate = GETDATE(),
    DeletedBy = @CreatorUserId,
    UpdatedDate = GETDATE(),
    UpdatedBy = @CreatorUserId
WHERE ReportDefinitionId = @ReportId
  AND IsDeleted = 0
  AND UserId <> @ViewerUserId;

IF EXISTS
(
    SELECT 1
    FROM RII_REPORT_ASSIGNMENTS
    WHERE ReportDefinitionId = @ReportId
      AND UserId = @ViewerUserId
)
BEGIN
    UPDATE RII_REPORT_ASSIGNMENTS
    SET
        IsDeleted = 0,
        DeletedDate = NULL,
        DeletedBy = NULL,
        UpdatedDate = GETDATE(),
        UpdatedBy = @CreatorUserId
    WHERE ReportDefinitionId = @ReportId
      AND UserId = @ViewerUserId;
END
ELSE
BEGIN
    INSERT INTO RII_REPORT_ASSIGNMENTS
    (
        ReportDefinitionId,
        UserId,
        CreatedDate,
        UpdatedDate,
        DeletedDate,
        IsDeleted,
        CreatedBy,
        UpdatedBy,
        DeletedBy
    )
    VALUES
    (
        @ReportId,
        @ViewerUserId,
        GETDATE(),
        NULL,
        NULL,
        0,
        @CreatorUserId,
        NULL,
        NULL
    );
END;

SELECT
    @ReportId AS ReportId,
    @CreatorUserId AS CreatorUserId,
    @ViewerUserId AS ViewerUserId,
    @DataSourceName AS DataSourceName;
