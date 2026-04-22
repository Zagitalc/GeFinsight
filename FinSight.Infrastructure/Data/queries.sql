-- FinSight SQL Reference
-- These are the raw SQL equivalents of the EF Core queries in the app.
-- Useful for SQL Server interviews and understanding the data model.

-- ── Schema ────────────────────────────────────────────────────────────

CREATE TABLE Categories (
    Id      INT PRIMARY KEY IDENTITY,
    Name    NVARCHAR(100) NOT NULL UNIQUE,
    Icon    NVARCHAR(10)  NOT NULL DEFAULT N'💳',
    Colour  NVARCHAR(7)   NOT NULL DEFAULT '#6c757d',
    IsSystem BIT          NOT NULL DEFAULT 0
);

CREATE TABLE AspNetUsers (
    Id              NVARCHAR(450) PRIMARY KEY,  -- ASP.NET Identity
    DisplayName     NVARCHAR(200) NOT NULL,
    PreferredCurrency NVARCHAR(3) NOT NULL DEFAULT 'GBP',
    -- Identity columns (Email, PasswordHash etc.) added by EF
);

CREATE TABLE Transactions (
    Id           INT PRIMARY KEY IDENTITY,
    UserId       NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    Amount       DECIMAL(18,2) NOT NULL,        -- Positive = income, Negative = expense
    CategoryName NVARCHAR(100) NOT NULL,
    [Date]       DATE          NOT NULL,
    Note         NVARCHAR(500) NOT NULL DEFAULT '',
    [Type]       INT           NOT NULL,        -- 0 = Income, 1 = Expense
    -- TPH discriminator for RecurringTransaction
    Discriminator    NVARCHAR(50)  NULL,
    Interval         INT           NULL,        -- 0=Weekly 1=Monthly 2=Yearly
    NextOccurrence   DATE          NULL,
    EndDate          DATE          NULL
);

CREATE TABLE Budgets (
    Id           INT PRIMARY KEY IDENTITY,
    UserId       NVARCHAR(450) NOT NULL REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    CategoryName NVARCHAR(100) NOT NULL,
    LimitAmount  DECIMAL(18,2) NOT NULL,
    RuleType     INT           NOT NULL,        -- 0=HardCap 1=Rolling 2=Percentage 3=Velocity
    IsActive     BIT           NOT NULL DEFAULT 1
);

-- ── Useful queries ────────────────────────────────────────────────────

-- Monthly summary (income vs expenses)
SELECT
    [Type],
    SUM(ABS(Amount)) AS Total
FROM Transactions
WHERE UserId = @UserId
  AND YEAR([Date])  = @Year
  AND MONTH([Date]) = @Month
GROUP BY [Type];

-- Spending by category for a given month
SELECT
    CategoryName,
    SUM(ABS(Amount)) AS Total,
    COUNT(*)          AS TxnCount
FROM Transactions
WHERE UserId = @UserId
  AND [Type]         = 1          -- Expense
  AND YEAR([Date])   = @Year
  AND MONTH([Date])  = @Month
GROUP BY CategoryName
ORDER BY Total DESC;

-- Rolling 30-day total per category (used by RollingAverageRule)
SELECT
    CategoryName,
    SUM(ABS(Amount)) AS RollingTotal,
    SUM(ABS(Amount)) / 30.0 AS DailyAverage
FROM Transactions
WHERE UserId   = @UserId
  AND [Type]   = 1
  AND [Date]  >= DATEADD(DAY, -30, GETDATE())
GROUP BY CategoryName;

-- Monthly trend — last 6 months
SELECT
    YEAR([Date])  AS [Year],
    MONTH([Date]) AS [Month],
    DATENAME(MONTH, [Date]) + ' ' + CAST(YEAR([Date]) AS NVARCHAR) AS MonthLabel,
    SUM(ABS(Amount)) AS TotalExpenses
FROM Transactions
WHERE UserId = @UserId
  AND [Type]  = 1
  AND [Date] >= DATEADD(MONTH, -6, GETDATE())
GROUP BY YEAR([Date]), MONTH([Date]), DATENAME(MONTH, [Date])
ORDER BY [Year], [Month];

-- Velocity check — how much spent so far this month vs days elapsed
DECLARE @DaysElapsed INT = DAY(GETDATE());
DECLARE @DaysInMonth INT = DAY(EOMONTH(GETDATE()));

SELECT
    CategoryName,
    SUM(ABS(Amount))                                AS SpentSoFar,
    @DaysElapsed                                    AS DaysElapsed,
    @DaysInMonth                                    AS DaysInMonth,
    SUM(ABS(Amount)) / @DaysElapsed                 AS DailyRateSoFar,
    SUM(ABS(Amount)) / @DaysElapsed * @DaysInMonth  AS ProjectedMonthTotal
FROM Transactions
WHERE UserId   = @UserId
  AND [Type]   = 1
  AND YEAR([Date])  = YEAR(GETDATE())
  AND MONTH([Date]) = MONTH(GETDATE())
GROUP BY CategoryName;

-- All active budgets with current month spend (JOIN example)
SELECT
    b.CategoryName,
    b.LimitAmount,
    b.RuleType,
    ISNULL(SUM(ABS(t.Amount)), 0) AS CurrentSpend,
    CASE
        WHEN ISNULL(SUM(ABS(t.Amount)), 0) > b.LimitAmount THEN 'Breached'
        WHEN ISNULL(SUM(ABS(t.Amount)), 0) > b.LimitAmount * 0.8 THEN 'Warning'
        ELSE 'Ok'
    END AS Status
FROM Budgets b
LEFT JOIN Transactions t
    ON  t.UserId       = b.UserId
    AND t.CategoryName = b.CategoryName
    AND t.[Type]       = 1
    AND YEAR(t.[Date])  = YEAR(GETDATE())
    AND MONTH(t.[Date]) = MONTH(GETDATE())
WHERE b.UserId   = @UserId
  AND b.IsActive = 1
GROUP BY b.CategoryName, b.LimitAmount, b.RuleType;
