CREATE DATABASE IF NOT EXISTS navlight_registration;
USE navlight_registration;

CREATE TABLE IF NOT EXISTS Event (
	EventId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	Name VARCHAR(100) NOT NULL,
	EventDate DATE NULL
);

CREATE TABLE IF NOT EXISTS Category (
	CategoryId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	EventId INT NOT NULL,
	Name VARCHAR(50) NOT NULL,
	Active BIT NOT NULL DEFAULT 1,
	CONSTRAINT FK_Category_Event
		FOREIGN KEY (EventId) REFERENCES Event(EventId),
	CONSTRAINT UQ_Category_Event_Name UNIQUE (EventId, Name)
);

CREATE TABLE IF NOT EXISTS Course (
	CourseId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	EventId INT NOT NULL,
	Name VARCHAR(50) NOT NULL,
	Active BIT NOT NULL DEFAULT 1,
	CONSTRAINT FK_Course_Event
		FOREIGN KEY (EventId) REFERENCES Event(EventId),
	CONSTRAINT UQ_Course_Event_Name UNIQUE (EventId, Name)
);

CREATE TABLE IF NOT EXISTS Team (
	TeamId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	EventId INT NOT NULL,
	TeamNumber VARCHAR(20) NOT NULL,
	Name VARCHAR(100) NOT NULL,
	CategoryId INT NOT NULL,
	CourseId INT NOT NULL,
	Registered BIT NOT NULL DEFAULT 0,
	RegisteredAt DATETIME NULL,
	LastUpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
	CONSTRAINT FK_Team_Event
		FOREIGN KEY (EventId) REFERENCES Event(EventId),
	CONSTRAINT FK_Team_Category
		FOREIGN KEY (CategoryId) REFERENCES Category(CategoryId),
	CONSTRAINT FK_Team_Course
		FOREIGN KEY (CourseId) REFERENCES Course(CourseId),
	CONSTRAINT UQ_Team_Event_Number UNIQUE (EventId, TeamNumber)
);

CREATE TABLE IF NOT EXISTS Competitor (
	CompetitorId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	TeamId INT NOT NULL,
	Name VARCHAR(100) NOT NULL,
	CONSTRAINT FK_Competitor_Team
		FOREIGN KEY (TeamId) REFERENCES Team(TeamId)
		ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS TagAssignment (
	TagAssignmentId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
	TeamId INT NOT NULL,
	TagCode VARCHAR(50) NOT NULL,
	AssignedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
	CONSTRAINT FK_TagAssignment_Team
		FOREIGN KEY (TeamId) REFERENCES Team(TeamId)
		ON DELETE CASCADE,
	CONSTRAINT UQ_TagAssignment_TagCode UNIQUE (TagCode),
	CONSTRAINT UQ_TagAssignment_Team_Tag UNIQUE (TeamId, TagCode)
);

CREATE INDEX IX_Team_Name ON Team(Name);
CREATE INDEX IX_Competitor_TeamId ON Competitor(TeamId);
CREATE INDEX IX_TagAssignment_TeamId ON TagAssignment(TeamId);

-- Preload data before event day:
-- 1. Insert one row into Event
-- 2. Insert valid categories for that event
-- 3. Insert valid courses for that event
-- 4. Insert teams with EventId, TeamNumber, Name, CategoryId, CourseId
-- 5. Insert competitors linked to each TeamId
