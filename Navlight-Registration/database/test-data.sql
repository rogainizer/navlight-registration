USE `navlight-registration`;

-- Clear existing sample data in dependency order.
DELETE FROM TagAssignment;
DELETE FROM Competitor;
DELETE FROM Team;
DELETE FROM Course;
DELETE FROM Category;
DELETE FROM Event;

INSERT INTO Event (EventId, Name, EventDate)
VALUES
    (1, 'Navlight Rogaine 2026', '2026-06-14');

INSERT INTO Category (CategoryId, EventId, Name, Active)
VALUES
    (1, 1, '6 Hour Open', 1),
    (2, 1, '6 Hour Veterans', 1),
    (3, 1, '3 Hour Open', 1),
    (4, 1, '3 Hour Family', 1),
    (5, 1, '24 Hour Open', 1);

INSERT INTO Course (CourseId, EventId, Name, Active)
VALUES
    (1, 1, '4hr', 1),
    (2, 1, '6hr', 1),
    (3, 1, '12hr', 1),
    (4, 1, '24hr', 1);

INSERT INTO Team (TeamId, EventId, TeamNumber, Name, CategoryId, CourseId, Registered, RegisteredAt)
VALUES
    (1, 1, '101', 'Bush Bandits', 1, 1, 0, NULL),
    (2, 1, '102', 'Checkpoint Chasers', 3, 2, 1, '2026-06-14 07:45:00'),
    (3, 1, '103', 'Muddy Compasses', 2, 1, 0, NULL),
    (4, 1, '104', 'Twilight Navigators', 4, 3, 0, NULL),
    (5, 1, '105', 'Lantern Legends', 5, 4, 1, '2026-06-14 06:55:00'),
    (6, 1, '106', 'Creek Bandits', 1, 2, 0, NULL);

INSERT INTO Competitor (TeamId, Name)
VALUES
    (1, 'Alice Brown'),
    (1, 'Ben Brown'),
    (2, 'Chloe Davis'),
    (2, 'Lachlan Davis'),
    (3, 'Emma Stone'),
    (3, 'Noah Stone'),
    (4, 'Mia Walker'),
    (4, 'Sam Walker'),
    (4, 'Ruby Walker'),
    (5, 'Jack Turner'),
    (5, 'Ethan Turner'),
    (6, 'Olivia Harris'),
    (6, 'Lucas Harris');

INSERT INTO TagAssignment (TeamId, TagCode)
VALUES
    (2, 'TAG-2001'),
    (5, 'TAG-2002'),
    (5, 'TAG-2003');
