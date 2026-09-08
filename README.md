<div align="center">

# ShipSurivor

**Roguelite Bullet Heaven** · Unity 6 URP · 1인 개발

[![Play in browser](Docs/images/play-banner.png)](https://n0wst4ndup.github.io/ocean-survival/)

![gameplay](Docs/images/hero.gif)

</div>

배틀쉽을 조종해 바다를 항해하며, 네임드를 처치해 떨어뜨린 부품으로 자기 배를 강화하는
자동 사격 로그라이트. 한 판 약 10분, 보스를 잡으면 클리어다.

Vampire Survivors의 자동 사격, Time Wasters의 컴포넌트 빌드, Battleship의 선박 조작감을 합쳤다.

## 조작

| 입력 | 동작 |
| --- | --- |
| `W` 홀드 | 전진 가속 — 뗄 때 감속 |
| `S` / `Left Ctrl` | 브레이크 |
| `A` `D` / `←` `→` | 선회 |
| `Esc` | 일시정지 |
| — | 사격은 자동 |

배는 관성으로 움직인다. 제자리 회피가 아니라 **궤적 예측**으로 살아남는 게임이다.

## 주요 시스템

### 드롭과 흡수

네임드 적은 컴포넌트를 하나 장착한 채 등장한다. 처치하면 그 부품이 부위 표기가 붙은 채
필드에 떨어지고, 30초 뒤 바다 밑으로 가라앉는다.

떨어진 부품은 플레이어만의 것이 아니다. **다른 네임드도 같은 규칙으로 흡수할 수 있고,
흡수한 네임드는 그 자리에서 강해진다.** 먼저 닿은 쪽이 가져가는 선착순이다.

필요 없는 부위의 네임드는 싸우지 않고 피할 수도 있다 — 위험 회피와 전리품 포기의 맞교환이다.

![드롭과 흡수](Docs/images/component.gif)

> **구현 현황**: 흡수 규칙 자체는 동작하지만, 네임드가 드롭을 *향해 이동하는* 추격 AI는
> 3주 개발 기간 안에 넣지 못했다. 현재 네임드는 무작위 배회(`NamedWander`) 중 우연히
> 드롭에 닿았을 때만 흡수한다. 설계 의도였던 "드롭 경쟁"은 여기까지다 (GDD §5.5).

### 슬롯 & 컴포넌트

주포(Main) / 부포(Sub) / 후방(Rear) 슬롯이 각각 1칸. 같은 부위를 다시 얻으면 레벨업하거나
데코레이터로 진화한다 (레벨 상한 3).

`Cannon` 에 `Double` 과 `Triple` 이 겹치면 1틱에 2 × 3 = **6발**이 나간다.

### 레벨업 3택

일반 몹이 떨구는 EXP 젬을 모아 레벨업하면 화면이 멈추고 카드 3장이 뜬다.
스탯 강화 카드와 컴포넌트 슬롯 카드가 같은 풀에서 나온다.

![레벨업 3택](Docs/images/levelup.gif)

### 보스 — Pirate Lord

180초가 지나면 등장한다. leash가 없어 따돌릴 수 없고, 플레이어(10 m/s)보다 빠른
14 m/s로 따라붙는다. 체력 구간에 따라 3페이즈로 전환된다.

패턴 상세는 [Docs/Boss/PirateLord.md](Docs/Boss/PirateLord.md) 참고.

![보스전](Docs/images/boss.gif)

### 기록과 리더보드

클리어 타임을 측정해 개인 베스트를 갱신하고, 전체 유저 랭킹에 반영한다.

<p>
  <img src="Docs/images/leaderboard.png" width="45%" alt="리더보드">
  <img src="Docs/images/result.png" width="45%" alt="결과 화면">
</p>

## 기술 스택

| 영역 | 사용 |
| --- | --- |
| 엔진 | Unity 6000.3.15f1, URP 17.3.0 |
| 입력 | Input System 1.19.0 |
| 카메라 | Cinemachine 3.1.6 |
| 적 AI | Unity Behavior 1.0.15 (비헤이비어 트리) + 자체 FSM |
| 비동기 | UniTask |
| 백엔드 | Firebase Auth + Realtime Database 13.16.0 |

## 웹 데모 안내

브라우저 데모는 게스트 모드로 동작한다. Firebase Unity SDK가 WebGL 네이티브
바이너리를 제공하지 않기 때문에, 웹에서는 기록이 브라우저 로컬에만 저장된다.
계정 로그인과 전체 유저 리더보드는 PC 빌드에서 동작한다.

플랫폼별 구현은 `Assets/Scripts/Core/Firebase/` 의 `#if UNITY_WEBGL` 분기로 나뉘며,
로컬 폴백은 [LocalStore.cs](Assets/Scripts/Core/Firebase/Local/LocalStore.cs) 가 담당한다.

## 빌드

Unity 6000.3.15f1 (Web 모듈 포함)이 필요하다.

```
1. 프로젝트를 Unity Hub로 연다
2. File > Build Profiles 에서 대상 플랫폼 선택
3. 씬 목록: Assets/Scenes/EntryPoint.unity, Assets/Scenes/InGame.unity
4. Build
```

Web으로 빌드할 경우 `Player > Web > Publishing Settings` 에서 `Decompression Fallback` 을
켜야 정적 호스팅(GitHub Pages 등)에서 로딩된다. 이 설정 없이 Brotli 압축 빌드를 올리면
서버가 `Content-Encoding` 헤더를 붙이지 않아 로딩 바 0%에서 멈춘다.

## 문서

| 문서 | 내용 |
| --- | --- |
| [Docs/GDD.md](Docs/GDD.md) | 게임 디자인 문서. 코어 루프 / 메커닉 / 마일스톤 |
| [Docs/Balancing.md](Docs/Balancing.md) | 밸런싱 수치와 근거 |
| [Docs/Boss/PirateLord.md](Docs/Boss/PirateLord.md) | 해적왕 보스 패턴 명세 |
| [CLAUDE.md](CLAUDE.md) | 개발 프로세스 / 브랜치 / 이슈 규약 |
