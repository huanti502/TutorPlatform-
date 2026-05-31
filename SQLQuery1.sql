-- Kiểm tra cột đã tồn tại chưa rồi mới thêm
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'XpPoints'
)
BEGIN
    ALTER TABLE AspNetUsers ADD XpPoints INT NOT NULL DEFAULT 0;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'XpLevel'
)
BEGIN
    ALTER TABLE AspNetUsers ADD XpLevel NVARCHAR(50) NOT NULL DEFAULT N'Mới bắt đầu';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('QuizAttempts') AND name = 'Id'
)
BEGIN
    CREATE TABLE QuizAttempts (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId NVARCHAR(450) NOT NULL,
        SubjectId INT NOT NULL,
        Level NVARCHAR(50) NOT NULL DEFAULT '',
        Score INT NOT NULL DEFAULT 0,
        TotalQuestions INT NOT NULL DEFAULT 10,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
        FOREIGN KEY (SubjectId) REFERENCES Subjects(Id) ON DELETE CASCADE
    );
END