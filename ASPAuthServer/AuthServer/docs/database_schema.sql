-- ============================================
-- AuthServer Database Schema (DDL)
-- ============================================
-- Database: authserver
-- Character Set: utf8mb4
-- Collation: utf8mb4_unicode_ci
-- ============================================

-- 1. 데이터베이스 생성
CREATE DATABASE IF NOT EXISTS `authserver`
CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;

USE `authserver`;

-- ============================================
-- 2. Users 테이블
-- ============================================
-- 사용자 정보를 저장하는 테이블
-- 회원가입, 로그인, 계정 잠금 기능 지원
-- ============================================

CREATE TABLE IF NOT EXISTS `Users` (
    -- 기본 정보
    `Id` INT AUTO_INCREMENT PRIMARY KEY COMMENT '사용자 고유 ID',
    `Username` VARCHAR(50) NOT NULL UNIQUE COMMENT '사용자명 (로그인 ID)',
    `Email` VARCHAR(100) NOT NULL UNIQUE COMMENT '이메일 주소',
    `PasswordHash` VARCHAR(255) NOT NULL COMMENT 'SHA256 해시된 비밀번호',

    -- 타임스탬프
    `CreatedAt` DATETIME NOT NULL COMMENT '계정 생성 시간 (UTC)',
    `LastLoginAt` DATETIME NULL COMMENT '마지막 로그인 시간 (UTC)',

    -- 계정 상태
    `IsActive` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '계정 활성화 여부 (1: 활성, 0: 비활성)',

    -- 보안 관련
    `LoginAttempts` INT NOT NULL DEFAULT 0 COMMENT '연속 로그인 실패 횟수',
    `LockedUntil` DATETIME NULL COMMENT '계정 잠금 해제 시간 (NULL: 잠금 안됨)',

    -- 인덱스
    INDEX `idx_username` (`Username`),
    INDEX `idx_email` (`Email`),
    INDEX `idx_isactive` (`IsActive`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='사용자 정보 테이블';

-- ============================================
-- 3. 기본 데이터 시딩 (선택사항)
-- ============================================
-- 기본 관리자 계정 생성
-- Username: admin
-- Password: admin123 (SHA256 1000 iterations)
-- ⚠️ 보안을 위해 프로덕션 환경에서는 즉시 변경 필요
-- ============================================

INSERT INTO `Users` (`Username`, `Email`, `PasswordHash`, `CreatedAt`, `IsActive`, `LoginAttempts`)
SELECT 'admin', 'admin@example.com',
       -- SHA256("admin123", 1000 iterations) 결과값
       'YourHashedPasswordHere',
       UTC_TIMESTAMP(), 1, 0
WHERE NOT EXISTS (SELECT 1 FROM `Users` WHERE `Username` = 'admin');

-- ============================================
-- 참고사항
-- ============================================
-- 1. Redis 설정 필요
--    - Login Token 저장 (1회용, TTL: 설정값)
--    - Refresh Token 저장 (TTL: 설정값)
--    - Used Login Token 추적 (재사용 방지)
--
-- 2. 토큰 관리 (Redis 키 구조)
--    - Login Token (Active): "login_token:active:{jti}"
--    - Login Token (Used): "login_token:used:{jti}"
--    - Refresh Token: "refresh_token:{userId}:{deviceId}"
--
-- 3. Access Token
--    - Stateless JWT (Redis 저장 안함)
--    - Signature로만 검증
--
-- 4. 보안 설정
--    - 로그인 실패 5회 시 계정 잠금 (기본값)
--    - 계정 잠금 시간: 15분 (기본값)
--    - 비밀번호 해싱: SHA256 + 1000 iterations
-- ============================================
