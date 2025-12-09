# FPS/RTS 하이브리드 게임 밸런싱 가이드

## 1. 밸런싱 목표

```
FPS 팀 승률 목표: 45-55% (Normal 난이도)

• Easy: 65% 이상 (초보자 친화적)
• Normal: 45-55% (경쟁적)
• Hard: 25% 이하 (도전 난이도)

→ 게임 길이: 15-20분
→ 평균 참여: 100% (게임 포기 최소화)
```

---

## 2. 난이도 조정 시스템

### 2.1 AI 정확도 곡선

```json
{
  "difficulty_settings": {
    "easy": {
      "accuracy_multiplier": 0.6,
      "reaction_time_multiplier": 1.5,
      "bot_count": 50,
      "description": "AI가 자주 빗맞춤, 반응 느림"
    },
    "normal": {
      "accuracy_multiplier": 1.0,
      "reaction_time_multiplier": 1.0,
      "bot_count": 100,
      "description": "균형잡힌 난이도"
    },
    "hard": {
      "accuracy_multiplier": 1.5,
      "reaction_time_multiplier": 0.7,
      "bot_count": 150,
      "description": "AI가 정확하고 빠른 반응"
    },
    "extreme": {
      "accuracy_multiplier": 2.0,
      "reaction_time_multiplier": 0.5,
      "bot_count": 200,
      "description": "거의 모든 샷이 명중"
    }
  }
}
```

### 2.2 정확도 곡선 함수

```csharp
public class DifficultyModifiers {
    private const float BaseAccuracy = 0.5f;
    
    public static float GetAccuracyForDifficulty(Difficulty difficulty) {
        return difficulty switch {
            Difficulty.Easy => BaseAccuracy * 0.6f,      // 30%
            Difficulty.Normal => BaseAccuracy,            // 50%
            Difficulty.Hard => BaseAccuracy * 1.5f,       // 75%
            Difficulty.Extreme => BaseAccuracy * 2.0f,    // 100%
            _ => BaseAccuracy
        };
    }
    
    public static float GetReactionTimeMultiplier(Difficulty difficulty) {
        return difficulty switch {
            Difficulty.Easy => 1.5f,      // 느린 반응
            Difficulty.Normal => 1.0f,    // 보통
            Difficulty.Hard => 0.7f,      // 빠른 반응
            Difficulty.Extreme => 0.5f,   // 즉각적 반응
            _ => 1.0f
        };
    }
    
    public static int GetBotCountForDifficulty(Difficulty difficulty) {
        return difficulty switch {
            Difficulty.Easy => 50,
            Difficulty.Normal => 100,
            Difficulty.Hard => 150,
            Difficulty.Extreme => 200,
            _ => 100
        };
    }
}
```

---

## 3. 무기 밸런싱

### 3.1 무기별 DPS 비교

```
| 무기 | TTK(명중) | 정확도 | DPS | 용도 |
|------|---------|-------|-----|------|
| AR   | 0.6s    | 75%   | 250 | 중거리 |
| SMG  | 0.4s    | 60%   | 300 | 근거리 |
| SR   | 0.8s    | 85%   | 180 | 원거리 |
| SG   | 0.5s    | 50%   | 400 | 매우근거리 |
| LP   | 1.2s    | 70%   | 120 | 백업무기 |

TTK = Time To Kill (풀피 기준)
DPS = Damage Per Second (평균 명중률 기준)
```

### 3.2 무기 클래스별 역할

```csharp
public enum WeaponClass {
    AssaultRifle,    // 밸런스형: 중거리 안정성
    SMG,             // 근접형: 높은 연사속도
    SniperRifle,     // 저격형: 높은 피해
    Shotgun,         // 산탄형: 강력하지만 범위 제한
    Pistol           // 보조형: 탄약 절감
}

public class WeaponBalance {
    public static float GetDamageMultiplier(WeaponClass weapon) {
        return weapon switch {
            WeaponClass.AssaultRifle => 1.0f,      // 기준선
            WeaponClass.SMG => 0.7f,               // 낮은 피해, 높은 연사속도
            WeaponClass.SniperRifle => 1.8f,       // 높은 피해
            WeaponClass.Shotgun => 2.2f,           // 매우 높은 피해 (범위 제한)
            WeaponClass.Pistol => 0.5f,            // 낮은 피해
            _ => 1.0f
        };
    }
}
```

---

## 4. FPS 팀 강화 옵션

### 4.1 스쿼드 강화

```json
{
  "squad_abilities": {
    "leader_abilities": {
      "group_heal": {"cooldown": 30, "duration": 5, "hp_per_tick": 5},
      "radar_ping": {"cooldown": 20, "reveal_radius": 50},
      "suppressive_fire": {"cooldown": 15, "accuracy_penalty": -30}
    },
    "medic_abilities": {
      "emergency_heal": {"cooldown": 10, "heal_amount": 50},
      "bandage_revive": {"cooldown": 60, "restore_health": 30},
      "adrenaline_shot": {"cooldown": 30, "movement_speed_boost": 1.5, "duration": 10}
    },
    "engineer_abilities": {
      "fortify_position": {"cooldown": 25, "cover_health": 200},
      "ammo_cache": {"cooldown": 30, "resupply_radius": 10},
      "trap_placement": {"cooldown": 20, "damage": 50, "max_traps": 3}
    },
    "sniper_abilities": {
      "headshot_guarantee": {"cooldown": 10, "duration": 5},
      "wall_penetration": {"cooldown": 30, "bullets": 5},
      "spawn_prediction": {"cooldown": 45, "reveal_duration": 10}
    }
  }
}
```

### 4.2 스쿼드 능력 사용률 분석

```
측정 지표:

• 능력 사용 빈도 (평균 10회 / 게임 목표)
• 능력당 처치 기여도 (5-10 처치 추가)
• 능력 쿨다운 설정 적절성

만약 능력 사용이 10회 미만:
  → 쿨다운 감소 (30s → 25s)
  → 효과 증대 (healing +20%)

만약 능력 사용이 20회 이상:
  → 쿨다운 증가 (15s → 20s)
  → 효과 감소 (damage -15%)
```

---

## 5. AI 봇 밸런싱

### 5.1 봇 성능 프로필

```csharp
public class BotDifficultyProfile {
    public float AccuracyMultiplier { get; set; }
    public float ReactionTimeMultiplier { get; set; }
    public float AggresionLevel { get; set; }
    public float TacticalAwareness { get; set; }
    public float CoverUtilization { get; set; }
    
    public static BotDifficultyProfile GetProfileForDifficulty(Difficulty diff) {
        return diff switch {
            Difficulty.Easy => new BotDifficultyProfile {
                AccuracyMultiplier = 0.6f,
                ReactionTimeMultiplier = 1.5f,
                AggresionLevel = 0.4f,
                TacticalAwareness = 0.3f,
                CoverUtilization = 0.2f
            },
            Difficulty.Normal => new BotDifficultyProfile {
                AccuracyMultiplier = 1.0f,
                ReactionTimeMultiplier = 1.0f,
                AggresionLevel = 0.6f,
                TacticalAwareness = 0.6f,
                CoverUtilization = 0.6f
            },
            Difficulty.Hard => new BotDifficultyProfile {
                AccuracyMultiplier = 1.5f,
                ReactionTimeMultiplier = 0.7f,
                AggresionLevel = 0.8f,
                TacticalAwareness = 0.85f,
                CoverUtilization = 0.9f
            },
            _ => new BotDifficultyProfile()
        };
    }
}
```

### 5.2 봇 팀 구성

```
균형잡힌 봇 팀 구성 (100 봇 기준):

• 리더: 10명 (10%)
  └─ 전술 명령, 팀 조정

• 공격수: 40명 (40%)
  └─ 선봉, 부하 처리

• 지원병: 20명 (20%)
  └─ 의료, 탄약 공급

• 저격수: 15명 (15%)
  └─ 원거리 지원

• 정찰병: 15명 (15%)
  └─ 지도 인식, 정보 수집
```

---

## 6. 동적 난이도 조정

### 6.1 자동 난이도 스케일링

```csharp
public class DynamicDifficultyAdjuster {
    private float currentWinRate = 0.5f;
    private const float TargetWinRate = 0.5f;
    private const float AdjustmentRate = 0.02f;  // 게임당 2% 조정
    
    public void UpdateDifficultyBasedOnPerformance(GameResult result) {
        // FPS 팀 승률 측정
        if (result.FpsTeamWon) {
            currentWinRate = Mathf.Lerp(currentWinRate, 1.0f, AdjustmentRate);
        } else {
            currentWinRate = Mathf.Lerp(currentWinRate, 0.0f, AdjustmentRate);
        }
        
        // 승률이 높으면 난이도 상향
        if (currentWinRate > 0.6f) {
            IncreaseDifficulty();
        }
        // 승률이 낮으면 난이도 하향
        else if (currentWinRate < 0.4f) {
            DecreaseDifficulty();
        }
        
        Debug.Log($"Current FPS win rate: {currentWinRate:P1}");
    }
    
    private void IncreaseDifficulty() {
        // 1단계: 봇 정확도 +10%
        // 2단계: 봇 수 증가
        // 3단계: 난이도 설정 상향
    }
    
    private void DecreaseDifficulty() {
        // 역순 조정
    }
}
```

### 6.2 밸런싱 모니터링

```
실시간 모니터링:

매 게임마다:
  ✓ FPS 팀 K/D 비율
  ✓ 평균 존속 시간
  ✓ 봇 처치 속도 (봇/분)
  ✓ 게임 길이
  ✓ 팀 와이프 횟수

주간 분석:
  ✓ 전체 승률 (목표: 45-55%)
  ✓ 난이도별 승률
  ✓ 클래스별 성능
  ✓ 무기별 사용률

기준치 미달 시:
  ① 원인 분석 (무기? 맵? 전략?)
  ② 변수 조정 (작은 것부터)
  ③ A/B 테스트 (평행 실험)
  ④ 배포 (검증 후)
```

---

## 7. 맵 밸런싱

### 7.1 맵 크기별 밸런싱

```
맵 크기: 300m × 300m (중형)

구역별 전술 분석:
  • 북쪽: 개방지형 (저격수 유리)
  • 동쪽: 건물군 (근접전 유리)
  • 남쪽: 산악지형 (엄폐유리)
  • 서쪽: 수로 (독특한 전술)

밸런스 지표:
  ✓ FPS 스폰 포인트: 중앙에서 100m 거리
  ✓ 초기 봇 위치: 주변 중심
  ✓ 엄폐물 밀도: 40% (노출/엄폐 균형)
  ✓ 고지대 비율: 25% (저격수 제한)
```

### 7.2 맵별 추천 전략

```json
{
  "maps": {
    "industrial_zone": {
      "fps_recommended_loadout": ["Shotgun", "AR", "Pistol"],
      "fps_strategy": "Close quarters, use buildings for cover",
      "bot_counter_strategy": "Spread formation, suppress entry points"
    },
    "mountain_pass": {
      "fps_recommended_loadout": ["Sniper", "AR", "Pistol"],
      "fps_strategy": "Elevated positions, long sight lines",
      "bot_counter_strategy": "Rush aggressively before positions locked"
    },
    "urban_district": {
      "fps_recommended_loadout": ["AR", "SMG", "Pistol"],
      "fps_strategy": "Balanced approach, multi-floor tactics",
      "bot_counter_strategy": "Pincer movement, divide and conquer"
    }
  }
}
```

---

## 8. 밸런싱 체크리스트

```
사전 배포:
  ✓ 모든 난이도에서 45-55% 승률
  ✓ 모든 무기 사용률 > 10%
  ✓ 게임 길이 15-20분
  ✓ 능력 사용 > 8회/게임

배포 후:
  ✓ 일일 승률 모니터링
  ✓ 주간 상세 분석
  ✓ 플레이어 피드백 수집
  ✓ 필요시 핫픽스 준비

조정 주기:
  ✓ 주간 마이크로 조정 (±5%)
  ✓ 월간 메이저 패치 (새 무기/맵)
  ✓ 계절 시즌 (큰 변화)
```

---

## 9. 대충돌 감지 및 조정

```csharp
public class BalanceAnomalyDetector {
    public bool DetectAnomalies(GameStatistics stats) {
        bool isAnomaly = false;
        
        // 1. 극단적 승률 (>70% 또는 <30%)
        if (stats.FpsWinRate > 0.7f || stats.FpsWinRate < 0.3f) {
            Debug.LogError($"CRITICAL: FPS win rate {stats.FpsWinRate:P1}");
            isAnomaly = true;
        }
        
        // 2. 게임이 너무 짧음 (<10분)
        if (stats.AverageGameDuration < 600) {
            Debug.LogWarning($"Games too short: {stats.AverageGameDuration}s");
            isAnomaly = true;
        }
        
        // 3. 한 무기의 사용률이 50% 초과
        if (stats.MostUsedWeaponPercentage > 0.5f) {
            Debug.LogWarning($"Weapon imbalance: {stats.MostUsedWeapon} {stats.MostUsedWeaponPercentage:P1}");
            isAnomaly = true;
        }
        
        // 4. 클래스 승률 차이 > 20%
        float classWinRateDifference = stats.HighestClassWinRate - stats.LowestClassWinRate;
        if (classWinRateDifference > 0.2f) {
            Debug.LogWarning($"Class imbalance: {classWinRateDifference:P1} difference");
            isAnomaly = true;
        }
        
        return isAnomaly;
    }
}
```

---

## 10. 밸런싱 리소스

### 참고 자료
- Dota 2 밸런싱 가이드 (게임 선택지 다양성)
- Valorant 에이전트 밸런싱 (역할별 강점)
- Counter-Strike 맵 밸런싱 (대칭성 원칙)

### 주기적 검토
- 주간: 승률, 무기 사용률
- 월간: 클래스별 성능, 맵 밸런스
- 분기: 전체 시스템 리디자인 검토

