-- Ennen kuin luodaan tietokantoja:
--     sudo /opt/mssql/bin/mssql-conf set-collation
-- jolle sanotaan:
--     Finnish_Swedish_100_CI_AS_SC_UTF8
-- Tarkoittaa "Case Insensitive, Accent Sensitive, Supplementary Characters, UTF-8" tjsp.

--CREATE DATABASE Kulunvalv COLLATE Finnish_Swedish_100_CI_AS_SC_UTF8;
-- Vaan tuo collate mahtaa tulla defaulttina serverin asetuksesta
USE master;
GO
DROP DATABASE Kulunvalv;
GO
CREATE DATABASE Kulunvalv;
GO
ALTER DATABASE Kulunvalv SET RECOVERY SIMPLE;
GO
ALTER DATABASE Kulunvalv SET AUTO_CLOSE OFF;
GO
USE Kulunvalv;
GO

--DROP TABLE Holidays;
--DROP TABLE Loggings;
--DROP TABLE Tags;
--DROP TABLE Users;

CREATE TABLE Users
(
	id       INT           NOT NULL PRIMARY KEY IDENTITY(1,1),
	name     VARCHAR(100)  NOT NULL,
	country  CHAR(2)       NOT NULL CHECK (country IN ('fi', 'no', 'se')),
	-- country needed to determine which day are holidays etc.
	status   TINYINT       NOT NULL DEFAULT 0
	-- The least significant bit of status indicates whether user is currently logged in.
	-- 0 means out and 1 means in.
	-- Other bits available as flags etc. for future use.
);

-- RFID tags in use and whom they are currently assigned to
-- https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-clustered-indexes?view=sql-server-ver16
-- Create table to add the clustered index
CREATE TABLE Tags
(
	tag_id   INT          NOT NULL IDENTITY(1,1),
	rfid_id  VARCHAR(24)  NOT NULL UNIQUE,
	serial   VARCHAR(52)  NULL,
	user_id  INT          NULL     REFERENCES Users(id) ON DELETE SET NULL,
	CONSTRAINT PK_Tags_tag_id PRIMARY KEY NONCLUSTERED (tag_id)
);
-- Now add the clustered index
CREATE CLUSTERED INDEX CIX_rfid_id ON Tags (rfid_id);

-- Table of all log in/out events
-- new_status is the status obtained because of this event.
CREATE TABLE Loggings
(
	event_id    INT       NOT NULL IDENTITY(1,1),
	user_id     INT       NOT NULL REFERENCES Users(id) ON DELETE CASCADE,
	date        DATETIME  NOT NULL,
	new_status  TINYINT   NOT NULL,
	CONSTRAINT PK_Loggings_event_id PRIMARY KEY NONCLUSTERED (event_id)
);
CREATE CLUSTERED INDEX CIX_date ON Loggings (date);


-- Special days for each country
-- Negative value indicates that the whole day is off or "distance".
-- Non-negative value means there are that many fewer minutes of work that day.
CREATE TABLE Holidays
(
	day  date  NOT NULL PRIMARY KEY,
	fi   int   NOT NULL,
	se   int   NOT NULL,
	no   int   NOT NULL
);

INSERT INTO Holidays(day, fi, se, no) VALUES('2024-01-02',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-01-03',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-01-04',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-01-05',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-03-25',   0,   0,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-03-26',   0,   0,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-03-27',   0,   0,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-03-28', 125, 125,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-03-29',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-04-01',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-04-02',   0,   0,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-04-30', 125, 125, 125);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-05-01',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-05-09',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-05-10',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-05-17',   0,   0,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-05-20',   0,   0,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-06-06',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-06-07',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-06-20', 230, 230, 230);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-06-21',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-07-08',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-07-09',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-07-10',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-07-11',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-07-12',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-07-15',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-07-16',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-07-17',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-07-18',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-07-19',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-08-21',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-08-22',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-08-23',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-11-01', 190, 190, 190);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-12-06',  -1,   0,   0);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-12-23',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-12-24',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-12-25',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-12-26',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-12-27',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-12-30',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2024-12-31',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2025-01-01',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2025-01-02',  -1,  -1,  -1);
INSERT INTO Holidays(day, fi, se, no) VALUES('2025-01-03',  -1,  -1,  -1);


-- Stored procedure for updating two tables in one shot hopefully
-- cf. https://learn.microsoft.com/en-us/sql/t-sql/statements/create-procedure-transact-sql?view=sql-server-ver15
IF OBJECT_ID ( 'Update_Status', 'P' ) IS NOT NULL
	DROP PROCEDURE Update_Status;
GO
CREATE PROCEDURE Update_Status @user INT, @newStatus TINYINT
AS
	SET NOCOUNT ON;
	BEGIN TRY
		BEGIN TRANSACTION
			UPDATE Users SET status = @newStatus WHERE id = @user;
			INSERT INTO Loggings(user_id, date, new_status) VALUES(@user, GETDATE(), @newStatus);	
		COMMIT
	END TRY
	BEGIN CATCH
		-- Determine if an error occurred.
		IF @@TRANCOUNT > 0
			ROLLBACK
		-- Return the error information.
		DECLARE @ErrorMessage NVARCHAR(4000), @ErrorSeverity INT;
		SELECT @ErrorMessage = ERROR_MESSAGE(),@ErrorSeverity = ERROR_SEVERITY();
		RAISERROR(@ErrorMessage, @ErrorSeverity, 1);
	END CATCH;
GO
