-- ============================================
-- AuthServer Database Setup Script
-- ============================================
-- 즉시 실행 가능한 데이터베이스 설정 스크립트
-- 기본 관리자 계정 포함
-- ============================================

-- 1. 데이터베이스 생성
CREATE DATABASE IF NOT EXISTS `authserver`
CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;

USE `authserver`;

-- 2. Users 테이블 생성
DROP TABLE IF EXISTS `Users`;

CREATE TABLE `Users` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `Username` VARCHAR(50) NOT NULL UNIQUE,
    `Email` VARCHAR(100) NOT NULL UNIQUE,
    `PasswordHash` VARCHAR(255) NOT NULL,
    `CreatedAt` DATETIME NOT NULL,
    `LastLoginAt` DATETIME NULL,
    `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
    `LoginAttempts` INT NOT NULL DEFAULT 0,
    `LockedUntil` DATETIME NULL,
    INDEX `idx_username` (`Username`),
    INDEX `idx_email` (`Email`),
    INDEX `idx_isactive` (`IsActive`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. 기본 관리자 계정 생성
-- Username: admin
-- Password: admin123
-- ⚠️ 프로덕션 환경에서는 반드시 비밀번호 변경 필요!
INSERT INTO `Users` (`Username`, `Email`, `PasswordHash`, `CreatedAt`, `IsActive`, `LoginAttempts`)
VALUES (
    'admin',
    'admin@example.com',
    'pV+KiuqRBUUIgqlz5T9klplSS1KowrnHmkaSqeEDtI0=',  -- SHA256("admin123", 1000 iterations)
    UTC_TIMESTAMP(),
    1,
    0
);

-- 4. 테스트용 일반 사용자 계정 (선택사항)
-- Username: testuser
-- Password: test123
INSERT INTO `Users` (`Username`, `Email`, `PasswordHash`, `CreatedAt`, `IsActive`, `LoginAttempts`)
VALUES (
    'testuser',
    'test@example.com',
    '2HZ7+mmUKeUoYc5tlxM6963CLvNzMyXpur571uPcnnU=',  -- SHA256("test123", 1000 iterations)
    UTC_TIMESTAMP(),
    1,
    0
);

-- ============================================
-- 설치 완료 확인
-- ============================================
SELECT 'Database setup completed!' AS Status;
SELECT COUNT(*) AS UserCount FROM `Users`;
SELECT `Id`, `Username`, `Email`, `CreatedAt`, `IsActive` FROM `Users`;

-- ============================================
-- 다음 단계
-- ============================================
-- 1. Redis 설치 및 실행
--    Windows: https://github.com/microsoftarchive/redis/releases
--    Linux/Mac: apt-get install redis-server / brew install redis
--
-- 2. appsettings.json 설정
--    - DatabaseSettings: MySQL 연결 정보
--    - RedisSettings: Redis 연결 정보
--    - JwtSettings: JWT 시크릿 키 설정
--
-- 3. 애플리케이션 실행
--    dotnet run
--
-- 4. 보안 설정
--    - admin 계정 비밀번호 변경
--    - JWT 시크릿 키 변경 (appsettings.json)
-- ============================================
